using System;
using System.Collections.Generic;

// "Oído" del juego. Hay uno por plataforma; el resto del sistema de voz es común:
//  - WindowsVoiceBackend: reconocedor de Windows (PC y Editor).
//  - WebVoiceBackend: Web Speech API del navegador (build WebGL, Chrome / Edge).
public interface IVoiceBackend : IDisposable
{
    string Status { get; }

    // Windows sólo reconoce una lista fija de palabras; el navegador escucha frases libres.
    bool NeedsKeywords { get; }
    void SetKeywords(ICollection<string> words);

    void Start();

    // Se llama cada frame (cambios de modo que necesitan esperar al sistema de voz).
    void Tick();

    // Nivel de sonido del micrófono (RMS, 0..1). Sólo se mide si se pidió con SetMicWanted(true).
    void SetMicWanted(bool wanted);
    float ReadMicLevel();

    // Dictado libre (nombre del piloto). Mientras dura, Heard entrega frases libres.
    bool CanDictate { get; }
    void StartDictation();
    void StopDictation();
    event Action<string> DictationFailed;   // mensaje para el jugador

    // id de la frase (para no repetir órdenes entre resultados parciales y finales), texto, ¿final?
    event Action<string, string, bool> Heard;
}

// Plataformas sin reconocimiento de voz.
public sealed class NullVoiceBackend : IVoiceBackend
{
    public string Status => "Voz no disponible en esta plataforma";
    public bool NeedsKeywords => false;
    public void SetKeywords(ICollection<string> words) { }
    public void Start() { }
    public void Tick() { }
    public void SetMicWanted(bool wanted) { }
    public float ReadMicLevel() => 0f;
    public bool CanDictate => false;
    public void StartDictation() { }
    public void StopDictation() { }
    public event Action<string> DictationFailed { add { } remove { } }
    public event Action<string, string, bool> Heard { add { } remove { } }
    public void Dispose() { }
}
