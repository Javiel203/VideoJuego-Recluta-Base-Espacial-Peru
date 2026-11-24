#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// Reconocimiento de voz en la build WebGL: usa la Web Speech API del navegador
// (Plugins/WebGL/VoiceWeb.jslib). Funciona en Chrome y Edge (necesita internet); Firefox no la tiene.
// La página debe servirse por HTTPS (o localhost) para que el navegador permita el micrófono.
public sealed class WebVoiceBackend : IVoiceBackend
{
    [DllImport("__Internal")] static extern int VoiceWeb_IsSupported();
    [DllImport("__Internal")] static extern void VoiceWeb_Start(string gameObjectName, string language);
    [DllImport("__Internal")] static extern void VoiceWeb_Stop();
    [DllImport("__Internal")] static extern float VoiceWeb_GetLevel();

    const string Language = "es-PE";

    readonly string receiverName;
    string status = "Iniciando…";

    public WebVoiceBackend(string receiverName) { this.receiverName = receiverName; }

    public string Status => status;
    public bool NeedsKeywords => false;   // el navegador escucha frases libres
    public event Action<string, string, bool> Heard;

    public void SetKeywords(ICollection<string> words) { }

    public void Start()
    {
        if (VoiceWeb_IsSupported() == 0)
        {
            status = "Usa Chrome o Edge para jugar con voz";
            return;
        }
        status = "Permite el micrófono en el navegador";
        VoiceWeb_Start(receiverName, Language);
    }

    public void Tick() { }

    // El navegador ya escucha frases libres: para dictar basta con usar el siguiente texto que llegue.
    public bool CanDictate => true;
    public void StartDictation() { }
    public void StopDictation() { }
    public event Action<string> DictationFailed { add { } remove { } }

    // El micrófono del navegador ya está abierto (lo usa el reconocimiento): siempre se puede medir.
    public void SetMicWanted(bool wanted) { }
    public float ReadMicLevel() => VoiceWeb_GetLevel();

    // ---------- Mensajes del navegador (llegan a VoiceCommands por SendMessage) ----------

    public void OnStarted() => status = "Micrófono activo";

    // payload = "id|texto"
    public void OnResult(string payload, bool final)
    {
        int bar = payload.IndexOf('|');
        if (bar < 0) return;
        Heard?.Invoke(payload.Substring(0, bar), payload.Substring(bar + 1), final);
    }

    public void OnError(string error)
    {
        switch (error)
        {
            case "no-speech":
            case "aborted":
                return;   // normales: silencio o reinicio
            case "not-allowed":
            case "service-not-allowed":
                status = "Permiso de micrófono denegado";
                break;
            case "network":
                status = "La voz necesita conexión a internet";
                break;
            case "unsupported":
                status = "Usa Chrome o Edge para jugar con voz";
                break;
            default:
                status = "Error de voz: " + error;
                break;
        }
    }

    public void Dispose() => VoiceWeb_Stop();
}
#endif
