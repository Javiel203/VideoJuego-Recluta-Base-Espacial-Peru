using UnityEngine;

// Sonidos de la Prueba de Memoria Espacial, sintetizados por código (sin archivos de audio).
// Funciona también en WebGL (el navegador habilita el audio tras el primer clic: el botón JUGAR).
public static class MemoryAudio
{
    const int Rate = 44100;

    // Una nota por cristal: azul = Do5, verde = Mi5, naranja = Sol5 (acorde mayor, suena agradable).
    static readonly float[] CrystalFrequencies = { 523.25f, 659.25f, 783.99f };

    static AudioClip[] crystalClips;
    static AudioClip success, error, victory, defeat;

    public static AudioClip Crystal(int index)
    {
        if (crystalClips == null)
        {
            crystalClips = new AudioClip[CrystalFrequencies.Length];
            for (int i = 0; i < CrystalFrequencies.Length; i++)
                crystalClips[i] = Bell("Cristal" + i, CrystalFrequencies[i], 0.9f);
        }
        return crystalClips[Mathf.Clamp(index, 0, crystalClips.Length - 1)];
    }

    public static AudioClip Success => success != null ? success
        : (success = Arpeggio("Correcto", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.075f, 0.5f));

    public static AudioClip Victory => victory != null ? victory
        : (victory = Arpeggio("Victoria",
            new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 783.99f, 1046.5f, 1318.5f }, 0.12f, 1.2f));

    public static AudioClip Defeat => defeat != null ? defeat
        : (defeat = Arpeggio("Derrota", new[] { 392f, 349.23f, 311.13f, 261.63f }, 0.22f, 0.9f));

    public static AudioClip Error => error != null ? error : (error = Buzz());

    // ---------- síntesis ----------

    // Campana cristalina: fundamental + armónicos con un parcial inarmónico para el brillo.
    static AudioClip Bell(string name, float freq, float duration)
    {
        int n = Mathf.CeilToInt(Rate * duration);
        var data = new float[n];
        AddBell(data, 0, freq, duration, 0.55f);
        return Make(name, data);
    }

    static void AddBell(float[] data, int start, float freq, float duration, float volume)
    {
        int n = Mathf.Min(data.Length - start, Mathf.CeilToInt(Rate * duration));
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float attack = Mathf.Clamp01(t / 0.006f);
            float env = attack * Mathf.Exp(-t * 4.5f);
            float s = Mathf.Sin(2f * Mathf.PI * freq * t)
                      + 0.45f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * Mathf.Exp(-t * 3f)
                      + 0.2f * Mathf.Sin(2f * Mathf.PI * freq * 3f * t) * Mathf.Exp(-t * 6f)
                      + 0.12f * Mathf.Sin(2f * Mathf.PI * freq * 4.2f * t) * Mathf.Exp(-t * 9f);
            data[start + i] += s * env * volume * 0.5f;
        }
    }

    static AudioClip Arpeggio(string name, float[] notes, float step, float tail)
    {
        float duration = step * notes.Length + tail;
        var data = new float[Mathf.CeilToInt(Rate * duration)];
        for (int k = 0; k < notes.Length; k++)
            AddBell(data, Mathf.RoundToInt(k * step * Rate), notes[k], tail + step, 0.4f);
        Normalize(data, 0.8f);
        return Make(name, data);
    }

    // Zumbido grave descendente para el error.
    static AudioClip Buzz()
    {
        const float duration = 0.45f;
        int n = Mathf.CeilToInt(Rate * duration);
        var data = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float freq = Mathf.Lerp(170f, 95f, t / duration);
            phase += freq / Rate;
            float saw = (float)(2.0 * (phase - System.Math.Floor(phase + 0.5)));
            float square = Mathf.Sign(Mathf.Sin((float)(phase * 2.0 * System.Math.PI)));
            float env = Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((duration - t) / 0.08f);
            data[i] = (saw * 0.5f + square * 0.25f) * env * 0.35f;
        }
        return Make("Error", data);
    }

    static void Normalize(float[] data, float peak)
    {
        float max = 0f;
        foreach (float v in data) max = Mathf.Max(max, Mathf.Abs(v));
        if (max < 1e-4f) return;
        float k = peak / max;
        for (int i = 0; i < data.Length; i++) data[i] *= k;
    }

    static AudioClip Make(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
