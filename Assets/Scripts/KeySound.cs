using UnityEngine;

// Sonido de tecla de teclado (sintetizado, no necesita archivos de audio).
// Se usa en el texto que aparece letra a letra (Historia) y al deletrear el nombre por voz.
public static class KeySound
{
    const int Variants = 4;
    const float MinInterval = 0.045f;   // como mucho ~22 teclas por segundo

    static AudioClip[] clips;
    static AudioSource source;
    static float lastPlay = -10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        source = null;
        lastPlay = -10f;
    }

    public static void Play(float volume = 0.55f)
    {
        float now = Time.unscaledTime;
        if (now - lastPlay < MinInterval) return;
        lastPlay = now;

        if (source == null)
        {
            var go = new GameObject("KeySound");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }
        if (clips == null)
        {
            clips = new AudioClip[Variants];
            for (int i = 0; i < Variants; i++) clips[i] = CreateClip(i);
        }

        source.pitch = Random.Range(0.92f, 1.08f);
        source.PlayOneShot(clips[Random.Range(0, Variants)], volume);
    }

    // "Clac" de tecla: chasquido de ruido muy corto + golpe grave de la tecla al tocar fondo.
    static AudioClip CreateClip(int seed)
    {
        const int rate = 44100;
        const float duration = 0.06f;
        int n = Mathf.CeilToInt(rate * duration);
        var data = new float[n];
        var rng = new System.Random(1234 + seed * 77);

        float thumpFreq = 140f + seed * 18f;
        float release = 0.022f + seed * 0.003f;   // segundo chasquido (la tecla al subir)
        int releaseStart = Mathf.RoundToInt(rate * release);
        float prevNoise = 0f;
        double phase = 0;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            float noise = white - prevNoise * 0.85f;   // paso alto sencillo: más "clic", menos siseo
            prevNoise = white;

            float click = noise * Mathf.Exp(-t / 0.0025f) * 0.9f;
            if (i >= releaseStart)
                click += noise * Mathf.Exp(-(t - release) / 0.0015f) * 0.35f;

            phase += 2.0 * Mathf.PI * thumpFreq / rate;
            float thump = (float)System.Math.Sin(phase) * Mathf.Exp(-t / 0.012f) * 0.5f;

            float attack = Mathf.Clamp01(i / 20f);
            data[i] = Mathf.Clamp((click + thump) * attack, -1f, 1f) * 0.7f;
        }

        var clip = AudioClip.Create("Tecla" + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
