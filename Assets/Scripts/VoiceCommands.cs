using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Gestor de voz de todo el juego (en español). Se crea solo al arrancar y sobrevive entre escenas.
//  - Escucha con el "oído" de la plataforma: Windows (PC/Editor) o el navegador (WebGL).
//  - Cada juego activa sus órdenes con un VoiceContext (cohete, cristales, defensa…).
//  - Si una palabra no es del contexto activo, se pulsa el botón visible cuyo texto coincide
//    ("Jugar", "Reintentar", "Despegar", "Elegir prueba"…).
//  - Mide el nivel de sonido del micrófono para la propulsión del cohete (SoundActive).
public class VoiceCommands : MonoBehaviour
{
    // Órdenes de la prueba de destreza, para la pantalla de instrucciones.
    public static readonly string[] HelpLines =
    {
        "HAZ SONIDO (\"suuuube\")  —  el propulsor empuja mientras suene tu voz",
        "\"IZQUIERDA\"  —  la nave gira a la izquierda",
        "\"DERECHA\"  —  la nave gira a la derecha",
        "\"ALTO\" o \"PARA\"  —  detiene el giro",
    };

    static readonly List<VoiceContext> contexts = new List<VoiceContext>();
    static VoiceCommands instance;

    // ---------- API para los juegos ----------

    public static void Push(VoiceContext context)
    {
        contexts.Remove(context);
        contexts.Add(context);
        if (instance != null) instance.vocabularyDirty = true;
    }

    public static void Pop(VoiceContext context)
    {
        if (contexts.Remove(context) && instance != null) instance.vocabularyDirty = true;
    }

    // ---------- Apagar / encender la voz ----------
    // "desactivar voz" (o F5) deja de obedecer órdenes y apaga la propulsión por sonido.
    // Mientras está apagada sólo se escucha "activar voz" (o F5 otra vez) para volver a encenderla.

    static readonly string[] PauseWords = { "desactivar voz", "apagar voz", "desactivar comandos", "desactivar comandos de voz" };
    static readonly string[] ResumeWords = { "activar voz", "encender voz", "activar comandos", "activar comandos de voz" };

    public static bool Paused => instance != null && instance.paused;

    public static void SetPaused(bool value)
    {
        if (instance == null || instance.paused == value) return;
        instance.paused = value;
        instance.EndDictation(false);
        instance.soundActive = false;
        instance.vocabularyDirty = true;
        instance.nextVocabularyBuild = 0f;   // cambiar ya las palabras que escucha Windows
        instance.ShowNotice(value ? "Voz desactivada: di \"ACTIVAR VOZ\" o pulsa F5 para volver a usarla"
                                  : "Voz activada");
        FancyButton.PlayClick();
    }

    static VoiceContext Active => contexts.Count > 0 ? contexts[contexts.Count - 1] : null;

    // Espacio libre abajo (px de referencia 1080) para no tapar los controles táctiles.
    public static float HudBottomOffset { get; set; }

    // true mientras el micrófono capta sonido por encima del ruido de fondo.
    public static bool SoundActive => instance != null && instance.soundActive;

    // ---------- Dictado (texto libre: nombre del piloto) ----------

    public static bool IsDictating => instance != null && instance.dictationHandler != null;

    // Empieza a escuchar texto libre. onText(texto, ¿final?) devuelve true si acepta el texto
    // (termina el dictado). onEnd(¿éxito?) se llama siempre al terminar. "cancelar" lo cancela.
    public static bool Dictate(string prompt, Func<string, bool, bool> onText, Action<bool> onEnd, float timeout = 12f)
    {
        if (instance == null || !instance.backend.CanDictate) return false;
        instance.EndDictation(false);
        instance.dictationPrompt = prompt;
        instance.dictationPreview = "";
        instance.dictationHandler = onText;
        instance.dictationEnd = onEnd;
        instance.dictationUntil = Time.unscaledTime + timeout;
        instance.backend.StartDictation();
        return true;
    }

    // ---------- Estado ----------

    const string SensitivityKey = "Voice_Sensitivity";
    const float BaseThreshold = 0.02f;      // RMS mínimo para contar como sonido
    const float NoiseFactor = 3f;           // el sonido debe superar 3 veces el ruido de fondo

    IVoiceBackend backend;
    // Palabras para Windows: textos de botones (se acumulan) + órdenes de los contextos activos
    // (al quitar un contexto se quitan sus palabras: así las letras del deletreo no molestan en el juego).
    readonly Dictionary<string, string> buttonWords = new Dictionary<string, string>();   // normalizada → hablada
    readonly HashSet<string> appliedKeywords = new HashSet<string>();
    bool vocabularyDirty;
    float nextVocabularyBuild;
    float nextButtonScan;
    readonly Dictionary<string, int> executedPerPhrase = new Dictionary<string, int>();

    // Anti-eco (el propulsor suena por los altavoces y el micrófono lo vuelve a captar).
    const float EchoMargin = 1.6f;          // la voz debe superar 1,6 veces el eco estimado
    const float EchoHoldSeconds = 0.35f;    // cubre el retraso altavoz → micrófono
    const float EchoSteadySeconds = 0.4f;   // sólo se aprende con el sonido del juego estable
    readonly float[] outputBuffer = new float[1024];
    float outputLevel;        // nivel del sonido del juego (con retención)
    float outputOnSince = -1f;
    float echoCoupling = 0.6f; // cuánto del sonido del juego llega al micrófono (se aprende solo)

    float micLevel;
    float noiseFloor = 0.01f;
    float sensitivity = 1f;   // >1 = más sensible
    bool soundActive;
    bool micWanted;
    bool paused;

    Func<string, bool, bool> dictationHandler;
    Action<bool> dictationEnd;
    float dictationUntil;
    string dictationPrompt = "", dictationPreview = "";
    string notice = "";
    float noticeUntil;

    string lastHeard = "";
    float lastHeardTime = -10f;
    GUIStyle style, smallStyle;
    Texture2D pixel;
    bool victoryVisible;
    float nextVictoryCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        contexts.Clear();
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("VoiceCommands");   // el .jslib envía los resultados a este nombre
        DontDestroyOnLoad(go);
        go.AddComponent<VoiceCommands>();
    }

    void Awake()
    {
        instance = this;
        sensitivity = PlayerPrefs.GetFloat(SensitivityKey, 1f);

#if UNITY_WEBGL && !UNITY_EDITOR
        backend = new WebVoiceBackend(gameObject.name);
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
        backend = new WindowsVoiceBackend();
#else
        backend = new NullVoiceBackend();
#endif
        backend.Heard += OnHeard;
        backend.DictationFailed += OnDictationFailed;
        backend.Start();

        vocabularyDirty = true;   // contextos activados antes de existir
        ScanButtons();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (backend != null)
        {
            backend.Heard -= OnHeard;
            backend.DictationFailed -= OnDictationFailed;
            backend.Dispose();
        }
        if (instance == this) instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EndDictation(false);
        nextButtonScan = 0f;   // aprender ya los botones de la escena nueva
    }

    void Update()
    {
        float now = Time.unscaledTime;
        backend.Tick();
        if (dictationHandler != null && now >= dictationUntil)
        {
            ShowNotice("No se oyó nada: vuelve a intentarlo");
            EndDictation(false);
        }

        // Windows necesita conocer las palabras de antemano: se aprenden los textos de los botones.
        if (backend.NeedsKeywords && now >= nextButtonScan)
        {
            nextButtonScan = now + 1f;
            ScanButtons();
        }
        if (vocabularyDirty && now >= nextVocabularyBuild)
        {
            vocabularyDirty = false;
            ApplyVocabulary(now);
        }

        if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame) SetPaused(!paused);

        UpdateSound();

        if (now >= nextVictoryCheck)
        {
            nextVictoryCheck = now + 0.5f;
            victoryVisible = FindAnyObjectByType<VictoryScreen>() != null;
        }
    }

    // ---------- Vocabulario ----------

    void AddWord(string normalized, string spoken)
    {
        if (normalized.Length == 0 || buttonWords.ContainsKey(normalized)) return;
        buttonWords[normalized] = spoken;
        vocabularyDirty = true;
    }

    void ApplyVocabulary(float now)
    {
        HashSet<string> spoken;
        if (paused)
        {
            spoken = new HashSet<string>(ResumeWords);   // apagada: sólo se espera "activar voz"
        }
        else
        {
            var words = new Dictionary<string, string>(buttonWords);
            foreach (VoiceContext c in contexts)
                foreach (var w in c.Words)
                    if (!words.ContainsKey(w.Key)) words[w.Key] = w.Value;
            spoken = new HashSet<string>(words.Values);
            spoken.UnionWith(PauseWords);
        }

        if (spoken.SetEquals(appliedKeywords)) return;   // nada cambió: no se reinicia el reconocedor
        appliedKeywords.Clear();
        appliedKeywords.UnionWith(spoken);
        nextVocabularyBuild = now + 0.5f;
        backend.SetKeywords(spoken);
    }

    void ScanButtons()
    {
        foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string label = ButtonLabel(b);
            if (label != null) AddWord(VoiceText.Normalize(label), label.ToLowerInvariant());
        }
    }

    // Primera línea del texto del botón (sin etiquetas de formato).
    static string ButtonLabel(Button b)
    {
        Text text = b.GetComponentInChildren<Text>(true);
        if (text == null || string.IsNullOrWhiteSpace(text.text)) return null;
        string label = text.text.Split('\n')[0].Trim();
        return label.Length > 0 ? label : null;
    }

    // ---------- Reconocimiento ----------

    void OnHeard(string id, string text, bool final)
    {
        string padded = " " + VoiceText.Normalize(text) + " ";
        if (ContainsAny(padded, ResumeWords)) { SetPaused(false); return; }
        if (ContainsAny(padded, PauseWords)) { SetPaused(true); return; }
        if (paused) return;

        if (dictationHandler != null)
        {
            HandleDictation(text, final);
            return;
        }

        List<(string phrase, Action action)> matches = Match(text);
        executedPerPhrase.TryGetValue(id, out int done);

        // Las órdenes ya ejecutadas con un resultado parcial no se repiten con el final.
        for (int i = done; i < matches.Count; i++)
        {
            lastHeard = matches[i].phrase.ToUpperInvariant();
            lastHeardTime = Time.unscaledTime;
            try { matches[i].action(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        if (final) executedPerPhrase.Remove(id);
        else if (matches.Count > done) executedPerPhrase[id] = matches.Count;
        if (executedPerPhrase.Count > 64) executedPerPhrase.Clear();
    }

    // " desactivar voz " no contiene " activar voz " (se comparan palabras completas).
    static bool ContainsAny(string padded, string[] phrases)
    {
        foreach (string p in phrases)
            if (padded.Contains(" " + VoiceText.Normalize(p) + " ")) return true;
        return false;
    }

    void HandleDictation(string text, bool final)
    {
        string normalized = VoiceText.Normalize(text);
        if (normalized == "cancelar" || normalized.EndsWith(" cancelar"))
        {
            EndDictation(false);
            return;
        }
        dictationPreview = text;
        bool accepted;
        try { accepted = dictationHandler(text, final); }
        catch (Exception e) { Debug.LogException(e); accepted = false; }
        if (accepted) EndDictation(true);
    }

    void EndDictation(bool success)
    {
        if (dictationHandler == null) return;
        Action<bool> end = dictationEnd;
        dictationHandler = null;
        dictationEnd = null;
        backend.StopDictation();
        try { end?.Invoke(success); }
        catch (Exception e) { Debug.LogException(e); }
    }

    void OnDictationFailed(string message)
    {
        if (dictationHandler == null) return;
        ShowNotice(message);
        EndDictation(false);
    }

    void ShowNotice(string message)
    {
        notice = message;
        noticeUntil = Time.unscaledTime + 5f;
    }

    // Busca órdenes en la frase, de izquierda a derecha (también de varias palabras: "elegir prueba").
    List<(string, Action)> Match(string text)
    {
        var result = new List<(string, Action)>();
        string[] tokens = VoiceText.Tokens(VoiceText.Normalize(text));
        if (tokens.Length == 0) return result;

        VoiceContext context = Active;
        List<(string label, Button button)> buttons = null;

        int i = 0;
        while (i < tokens.Length)
        {
            bool found = false;
            for (int len = Mathf.Min(4, tokens.Length - i); len >= 1 && !found; len--)
            {
                string phrase = string.Join(" ", tokens, i, len);
                if (context != null && context.TryGet(phrase, out Action action))
                {
                    result.Add((phrase, action));
                    found = true;
                }
                else
                {
                    if (buttons == null) buttons = VisibleButtons();
                    foreach (var (label, button) in buttons)
                    {
                        if (label != phrase) continue;
                        Button target = button;
                        result.Add((phrase, () => { if (target != null && target.IsInteractable()) target.onClick.Invoke(); }));
                        found = true;
                        break;
                    }
                }
                if (found) i += len;
            }
            if (!found) i++;
        }
        return result;
    }

    static List<(string, Button)> VisibleButtons()
    {
        var list = new List<(string, Button)>();
        foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!b.isActiveAndEnabled || !b.IsInteractable()) continue;
            string label = ButtonLabel(b);
            if (label != null) list.Add((VoiceText.Normalize(label), b));
        }
        return list;
    }

    // ---------- Nivel de sonido (propulsión del cohete) ----------

    void UpdateSound()
    {
        VoiceContext context = Active;
        bool wanted = !paused && context != null && context.UsesMicLevel;
        if (wanted != micWanted)
        {
            micWanted = wanted;
            backend.SetMicWanted(wanted);
            soundActive = false;
        }
        if (!micWanted) return;

        // Sensibilidad ajustable con F7 (menos) / F8 (más); se guarda para la próxima vez.
        if (Keyboard.current != null)
        {
            if (Keyboard.current.f7Key.wasPressedThisFrame) SetSensitivity(sensitivity / 1.25f);
            if (Keyboard.current.f8Key.wasPressedThisFrame) SetSensitivity(sensitivity * 1.25f);
        }

        micLevel = backend.ReadMicLevel();
        float dt = Time.unscaledDeltaTime;
        UpdateEcho(dt);

        // Ruido de fondo: baja rápido si hay silencio y sube muy despacio (no "se come" la voz).
        // Mientras suena el juego no se actualiza: el eco no es ruido de fondo.
        if (outputLevel < 0.005f)
        {
            noiseFloor = Mathf.Lerp(noiseFloor, micLevel, micLevel < noiseFloor ? dt * 2f : dt * 0.05f);
            noiseFloor = Mathf.Clamp(noiseFloor, 0.0005f, 0.2f);
        }

        float threshold = Threshold;
        soundActive = soundActive ? micLevel > threshold * 0.75f : micLevel > threshold;   // con histéresis
    }

    // El umbral sube con el eco esperado: el sonido del propulsor ya no se "auto-activa".
    float Threshold => Mathf.Max(BaseThreshold / sensitivity, noiseFloor * NoiseFactor,
                                 outputLevel * echoCoupling * EchoMargin);

    // Mide lo que el juego está sacando por los altavoces y aprende cuánto de eso vuelve al micrófono.
    // En WebGL GetOutputData devuelve ceros (allí ya actúa la cancelación de eco del navegador).
    void UpdateEcho(float dt)
    {
        AudioListener.GetOutputData(outputBuffer, 0);
        double sum = 0;
        foreach (float s in outputBuffer) sum += s * s;
        float current = Mathf.Sqrt((float)(sum / outputBuffer.Length));

        // Retención: sube al instante y baja despacio (el eco llega con retraso y se apaga con retraso).
        outputLevel = Mathf.Max(current, outputLevel - outputLevel * dt / EchoHoldSeconds);

        float now = Time.unscaledTime;
        if (current < 0.005f) { outputOnSince = -1f; return; }
        if (outputOnSince < 0f) outputOnSince = now;
        if (now - outputOnSince < EchoSteadySeconds) return;

        // Igual que el ruido de fondo: el jugador no habla todo el rato, así que el mínimo de
        // micrófono / salida es el eco. Baja rápido, sube despacio.
        float ratio = micLevel / Mathf.Max(current, 0.001f);
        echoCoupling = Mathf.Lerp(echoCoupling, ratio, ratio < echoCoupling ? dt * 1.5f : dt * 0.1f);
        echoCoupling = Mathf.Clamp(echoCoupling, 0.02f, 4f);
    }

    void SetSensitivity(float value)
    {
        sensitivity = Mathf.Clamp(value, 0.25f, 8f);
        PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
        PlayerPrefs.Save();
    }

    // ---------- Mensajes del navegador (VoiceWeb.jslib) ----------

#if UNITY_WEBGL && !UNITY_EDITOR
    WebVoiceBackend Web => backend as WebVoiceBackend;
    void OnWebVoiceStarted(string _) => Web?.OnStarted();
    void OnWebVoiceInterim(string payload) => Web?.OnResult(payload, false);
    void OnWebVoiceFinal(string payload) => Web?.OnResult(payload, true);
    void OnWebVoiceError(string error) => Web?.OnError(error);
#endif

    // ---------- Indicador en pantalla ----------

    void OnGUI()
    {
        VoiceContext context = Active;
        bool recentlyHeard = Time.unscaledTime - lastHeardTime < 1.5f;
        bool showNotice = Time.unscaledTime < noticeUntil;
        bool dictating = dictationHandler != null;
        if (victoryVisible) return;
        if (paused) context = null;   // apagada: sin ayuda de órdenes ni barra del micrófono
        else if (context == null && !recentlyHeard && !showNotice && !dictating) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { font = PeruTheme.BodyBoldFont, wordWrap = true };
            style.normal.textColor = Color.white;
            smallStyle = new GUIStyle(style) { font = PeruTheme.BodyFont };
            smallStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
            pixel = Texture2D.whiteTexture;
        }

        float scale = Screen.height / 1080f;
        style.fontSize = Mathf.RoundToInt(26 * scale);
        smallStyle.fontSize = Mathf.RoundToInt(21 * scale);
        float x = 24f * scale;
        float width = Mathf.Min(Screen.width * 0.42f, 820f * scale);
        float y = Screen.height - (60f + HudBottomOffset) * scale;

        // Línea principal: estado + última orden oída (o el dictado en curso).
        string text;
        if (paused)
            text = "Voz DESACTIVADA   ·   di ACTIVAR VOZ o pulsa F5";
        else if (dictating)
            text = dictationPrompt + (dictationPreview.Length > 0 ? "   ·   \"" + dictationPreview + "\"" : "…") +
                   "   ·   di CANCELAR para salir";
        else
        {
            text = "Voz: " + backend.Status;
            if (recentlyHeard) text += "   ·   Oído: " + lastHeard;
            else text += "   ·   DESACTIVAR VOZ (F5)";
        }
        GUI.Label(new Rect(x, y, 1400f * scale, 50f * scale), text, style);

        if (showNotice)
        {
            y -= 34f * scale;
            float w = Mathf.Min(Screen.width - 2f * x, 1400f * scale);
            DrawRect(new Rect(x - 8f * scale, y - 2f * scale, w + 16f * scale, 32f * scale), new Color(0.7f, 0.05f, 0.1f, 0.85f));
            GUI.Label(new Rect(x, y, w, 32f * scale), notice, smallStyle);
        }

        if (context == null) return;

        // Barra de nivel del micrófono (sólo en el cohete).
        if (context.UsesMicLevel)
        {
            y -= 30f * scale;
            float barW = 320f * scale, barH = 14f * scale;
            float max = 0.25f;
            DrawRect(new Rect(x, y, barW, barH), new Color(0f, 0f, 0f, 0.55f));
            DrawRect(new Rect(x, y, barW * Mathf.Clamp01(micLevel / max), barH),
                soundActive ? new Color(0.25f, 0.9f, 0.4f) : new Color(1f, 1f, 1f, 0.6f));
            DrawRect(new Rect(x + barW * Mathf.Clamp01(Threshold / max) - 1f, y - 3f * scale, 2f * scale, barH + 6f * scale),
                new Color(0.85f, 0.06f, 0.14f));
            GUI.Label(new Rect(x + barW + 12f * scale, y - 8f * scale, 600f * scale, 30f * scale),
                soundActive ? "PROPULSOR" : "F7 / F8: sensibilidad", smallStyle);
        }

        // Ayuda del contexto (órdenes disponibles).
        if (!string.IsNullOrEmpty(context.Help))
        {
            float h = smallStyle.CalcHeight(new GUIContent(context.Help), width);
            y -= h + 8f * scale;
            DrawRect(new Rect(x - 8f * scale, y - 4f * scale, width + 16f * scale, h + 8f * scale), new Color(0f, 0f, 0f, 0.45f));
            GUI.Label(new Rect(x, y, width, h), context.Help, smallStyle);
        }
    }

    void DrawRect(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, pixel);
        GUI.color = old;
    }
}
