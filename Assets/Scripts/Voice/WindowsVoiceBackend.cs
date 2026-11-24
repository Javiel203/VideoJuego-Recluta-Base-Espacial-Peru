#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Windows.Speech;

// Reconocimiento de voz de Windows (PC y Editor).
//  - Órdenes: KeywordRecognizer (lista fija de palabras).
//  - Dictado (nombre del piloto): DictationRecognizer. No pueden funcionar a la vez, así que para
//    dictar se apaga el sistema de palabras clave y al terminar se vuelve a encender.
// Para que entienda bien el español, el idioma de voz de Windows debe estar en Español.
// El dictado además necesita "Reconocimiento de voz en línea" activado (Configuración → Privacidad → Voz).
// El nivel de sonido (propulsión del cohete) se mide aparte con el micrófono de Unity.
public sealed class WindowsVoiceBackend : IVoiceBackend
{
    enum Mode { Keywords, DictationRequested, StoppingKeywords, Dictating, StoppingDictation, RestartingKeywords,
                Names, StoppingNames }

    const float ModeTimeout = 1.5f;   // por si el sistema de voz no confirma el cambio

    // Si el dictado libre falla (Windows 11 reciente: error 0x80045509 aunque la voz en línea esté
    // activada), el nombre se reconoce sin conexión con la lista de PilotNames.
    static bool dictationBroken;
    bool fallbackPending;

    KeywordRecognizer recognizer;
    DictationRecognizer dictation;
    List<string> keywords = new List<string>();
    Mode mode = Mode.Keywords;
    float modeSince;
    int counter;
    string status = "Iniciando…";

    AudioClip micClip;
    readonly float[] micBuffer = new float[512];   // ~32 ms a 16 kHz
    bool micWanted;
    bool micWarned;
    float nextMicRetry;

    public string Status => micWanted && micClip == null ? "No se detecta micrófono" : status;
    public bool NeedsKeywords => true;
    public bool CanDictate => PhraseRecognitionSystem.isSupported;
    public event Action<string, string, bool> Heard;
    public event Action<string> DictationFailed;

    public void Start()
    {
        if (!PhraseRecognitionSystem.isSupported)
        {
            status = "Voz no disponible en este equipo";
            Debug.LogWarning("Voz: el reconocimiento de voz de Windows no está disponible en este sistema.");
        }
    }

    // ---------- Órdenes (palabras clave) ----------

    public void SetKeywords(ICollection<string> words)
    {
        keywords = new List<string>(words);
        if (mode == Mode.Keywords) BuildKeywordRecognizer();   // si se está dictando, se aplican al volver
    }

    void BuildKeywordRecognizer()
    {
        if (!PhraseRecognitionSystem.isSupported) return;
        DisposeRecognizer();
        if (keywords.Count == 0) return;
        try
        {
            recognizer = new KeywordRecognizer(keywords.ToArray(), ConfidenceLevel.Low);
            recognizer.OnPhraseRecognized += OnPhraseRecognized;
            recognizer.Start();
            status = "Micrófono activo";
        }
        catch (Exception e)
        {
            status = "Error de voz";
            Debug.LogWarning("Voz: no se pudo iniciar el reconocedor de Windows: " + e.Message);
        }
    }

    void OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        if (args.confidence == ConfidenceLevel.Rejected) return;
        Heard?.Invoke("w" + (++counter), args.text, true);
    }

    // ---------- Dictado ----------

    public void StartDictation()
    {
        if (!CanDictate || mode != Mode.Keywords) return;
        // Se hace en el siguiente Tick: esta llamada suele venir desde el evento del propio reconocedor.
        SetMode(Mode.DictationRequested);
    }

    public void StopDictation()
    {
        fallbackPending = false;
        switch (mode)
        {
            case Mode.DictationRequested:
                SetMode(Mode.Keywords);
                break;
            case Mode.Names:
                // Se rehace en el siguiente Tick: suele llamarse desde el evento del propio reconocedor.
                SetMode(Mode.StoppingNames);
                break;
            case Mode.StoppingKeywords:
                PhraseRecognitionSystem.Restart();
                SetMode(Mode.RestartingKeywords);
                break;
            case Mode.Dictating:
                if (dictation != null && dictation.Status == SpeechSystemStatus.Running) dictation.Stop();
                SetMode(Mode.StoppingDictation);
                break;
        }
    }

    public void Tick()
    {
        bool timedOut = Time.unscaledTime - modeSince > ModeTimeout;
        switch (mode)
        {
            case Mode.DictationRequested:
                DisposeRecognizer();
                if (dictationBroken)
                {
                    BeginNames();
                    break;
                }
                if (PhraseRecognitionSystem.Status == SpeechSystemStatus.Running) PhraseRecognitionSystem.Shutdown();
                SetMode(Mode.StoppingKeywords);
                break;

            case Mode.StoppingNames:
                SetMode(Mode.Keywords);
                BuildKeywordRecognizer();
                break;

            case Mode.StoppingKeywords:
                if (PhraseRecognitionSystem.Status != SpeechSystemStatus.Running || timedOut) BeginDictation();
                break;

            case Mode.StoppingDictation:
                if (dictation == null || dictation.Status != SpeechSystemStatus.Running || timedOut)
                {
                    DisposeDictation();
                    PhraseRecognitionSystem.Restart();
                    SetMode(Mode.RestartingKeywords);
                }
                break;

            case Mode.RestartingKeywords:
                if (PhraseRecognitionSystem.Status == SpeechSystemStatus.Running || timedOut)
                {
                    if (fallbackPending)
                    {
                        fallbackPending = false;
                        BeginNames();
                        break;
                    }
                    SetMode(Mode.Keywords);
                    BuildKeywordRecognizer();
                }
                break;
        }
    }

    // Reconocimiento del nombre sin conexión: sólo se escuchan los nombres de la lista y "cancelar".
    void BeginNames()
    {
        DisposeRecognizer();
        try
        {
            var words = new HashSet<string>(PilotNames.Common) { "cancelar" };
            recognizer = new KeywordRecognizer(words.ToArray(), ConfidenceLevel.Medium);
            recognizer.OnPhraseRecognized += OnPhraseRecognized;
            recognizer.Start();
            SetMode(Mode.Names);
            status = "Escuchando nombre…";
        }
        catch (Exception e)
        {
            Debug.LogWarning("Voz: no se pudo iniciar el reconocimiento de nombres: " + e.Message);
            SetMode(Mode.Keywords);
            BuildKeywordRecognizer();
            DictationFailed?.Invoke("No se pudo escuchar el nombre: escríbelo con el teclado");
        }
    }

    void BeginDictation()
    {
        try
        {
            dictation = new DictationRecognizer(ConfidenceLevel.Low, DictationTopicConstraint.Dictation)
            {
                InitialSilenceTimeoutSeconds = 8f,
                AutoSilenceTimeoutSeconds = 2f,
            };
            dictation.DictationHypothesis += OnHypothesis;
            dictation.DictationResult += OnResult;
            dictation.DictationComplete += OnComplete;
            dictation.DictationError += OnError;
            dictation.Start();
            SetMode(Mode.Dictating);
            status = "Dictando…";
        }
        catch (Exception e)
        {
            Debug.LogWarning("Voz: no se pudo iniciar el dictado, se usa la lista de nombres: " + e.Message);
            dictationBroken = true;
            DisposeDictation();
            PhraseRecognitionSystem.Restart();
            fallbackPending = true;
            SetMode(Mode.RestartingKeywords);
        }
    }

    void OnHypothesis(string text) => Heard?.Invoke("d" + counter, text, false);

    void OnResult(string text, ConfidenceLevel confidence)
    {
        Heard?.Invoke("d" + counter, text, true);
        counter++;
    }

    void OnComplete(DictationCompletionCause cause)
    {
        if (cause == DictationCompletionCause.Complete || fallbackPending || mode != Mode.Dictating) return;
        switch (cause)
        {
            case DictationCompletionCause.TimeoutExceeded:
                DictationFailed?.Invoke("No se oyó ningún nombre");
                break;
            case DictationCompletionCause.MicrophoneUnavailable:
                DictationFailed?.Invoke("Micrófono no disponible");
                break;
            default:
                // NetworkFailure, UnknownError, AuthenticationFailure…: a veces Windows termina así sin
                // lanzar DictationError. Se pasa igualmente a la lista de nombres sin conexión.
                SwitchToNames("dictado terminado: " + cause);
                break;
        }
    }

    void OnError(string error, int hresult) => SwitchToNames($"{error}, 0x{hresult:X8}");

    void SwitchToNames(string reason)
    {
        if (mode != Mode.Dictating || fallbackPending) return;
        Debug.LogWarning($"Voz: dictado libre no disponible ({reason}); se usa la lista de nombres.");
        // Se cambia al reconocimiento de nombres sin conexión (y ya no se vuelve a intentar el dictado).
        dictationBroken = true;
        fallbackPending = true;
        if (dictation != null && dictation.Status == SpeechSystemStatus.Running) dictation.Stop();
        SetMode(Mode.StoppingDictation);
    }

    void SetMode(Mode value)
    {
        mode = value;
        modeSince = Time.unscaledTime;
    }

    // ---------- Nivel del micrófono ----------

    public void SetMicWanted(bool wanted)
    {
        if (wanted == micWanted) return;
        micWanted = wanted;
        if (wanted) StartMic();
        else StopMic();
    }

    void StartMic()
    {
        nextMicRetry = Time.unscaledTime + 2f;
        if (Microphone.devices.Length == 0)
        {
            // Se avisa una sola vez; se vuelve a intentar cada 2 s por si se conecta después.
            if (!micWarned) Debug.Log("Voz: no se detecta micrófono (la propulsión por sonido no funcionará hasta conectarlo).");
            micWarned = true;
            return;
        }
        micClip = Microphone.Start(null, true, 1, 16000);
    }

    void StopMic()
    {
        if (micClip == null) return;
        Microphone.End(null);
        micClip = null;
    }

    public float ReadMicLevel()
    {
        if (micClip == null && micWanted && Time.unscaledTime >= nextMicRetry) StartMic();
        if (micClip == null) return 0f;
        int position = Microphone.GetPosition(null);
        if (position <= 0) return 0f;
        int start = position - micBuffer.Length;
        if (start < 0) start += micClip.samples;
        micClip.GetData(micBuffer, start);   // el clip es circular: GetData da la vuelta solo

        double sum = 0;
        foreach (float s in micBuffer) sum += s * s;
        return Mathf.Sqrt((float)(sum / micBuffer.Length));
    }

    // ---------- Limpieza ----------

    void DisposeRecognizer()
    {
        if (recognizer == null) return;
        if (recognizer.IsRunning) recognizer.Stop();
        recognizer.OnPhraseRecognized -= OnPhraseRecognized;
        recognizer.Dispose();
        recognizer = null;
    }

    void DisposeDictation()
    {
        if (dictation == null) return;
        dictation.DictationHypothesis -= OnHypothesis;
        dictation.DictationResult -= OnResult;
        dictation.DictationComplete -= OnComplete;
        dictation.DictationError -= OnError;
        if (dictation.Status == SpeechSystemStatus.Running) dictation.Stop();
        dictation.Dispose();
        dictation = null;
    }

    public void Dispose()
    {
        DisposeRecognizer();
        DisposeDictation();
        StopMic();
    }
}
#endif
