using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

// MISIÓN FINAL: video (Assets/Video/videoFinal.mp4) entre DESPEGAR y la salida del hangar.
//  - Se reproduce por URL: en el Editor desde Assets/Video; en las builds desde StreamingAssets/Video
//    (IntroVideoBuildStep lo copia solo al compilar). Así funciona también en WebGL.
//  - Se puede omitir: botón OMITIR VIDEO (también con el dedo y diciendo "omitir"), Espacio, Enter o Esc.
//  - Pausa la música del juego mientras suena el video. Si el video no carga, se continúa sin él.
public class DefensaIntroVideo : MonoBehaviour
{
    public const string FileName = "videoFinal.mp4";
    const float PrepareTimeout = 20f;

    Canvas canvas;
    RawImage screen;
    AspectRatioFitter fitter;
    Text loadingText;
    VideoPlayer player;
    RenderTexture texture;
    VoiceContext voice;
    readonly List<AudioSource> pausedAudio = new List<AudioSource>();
    bool finished, skipRequested;
    Action onDone;

    public static bool InProgress { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => InProgress = false;

    static string VideoUrl
    {
        get
        {
#if UNITY_EDITOR
            return Application.dataPath + "/Video/" + FileName;
#elif UNITY_WEBGL
            return Application.streamingAssetsPath + "/Video/" + FileName;   // ya es una URL http(s)
#else
            return Application.streamingAssetsPath + "/Video/" + FileName;
#endif
        }
    }

    public void Play(Action done)
    {
        if (InProgress || finished) return;   // una sola vez por misión
        onDone = done;
        InProgress = true;
        BuildUi();
        PauseGameAudio();

        voice = new VoiceContext("VOZ:  di OMITIR para saltar el video").On(RequestSkip, "omitir", "saltar");
        VoiceCommands.Push(voice);

        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.source = VideoSource.Url;
        player.url = VideoUrl;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.audioOutputMode = VideoAudioOutputMode.Direct;   // el único modo de audio que admite WebGL
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;   // el juego está en pausa
        player.skipOnDrop = true;
        player.isLooping = false;
        player.errorReceived += OnError;
        player.loopPointReached += _ => Finish();

        StartCoroutine(PrepareAndPlay());
    }

    IEnumerator PrepareAndPlay()
    {
        player.Prepare();
        float waited = 0f;
        while (!player.isPrepared && !finished)
        {
            if (skipRequested || waited > PrepareTimeout)
            {
                if (waited > PrepareTimeout) Debug.LogWarning("Video de introducción: tardó demasiado en cargar, se omite.");
                Finish();
                yield break;
            }
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        if (finished) yield break;

        int w = (int)Mathf.Max(16, player.width), h = (int)Mathf.Max(16, player.height);
        texture = new RenderTexture(w, h, 0);
        player.targetTexture = texture;
        screen.texture = texture;
        screen.color = Color.white;
        fitter.aspectRatio = (float)w / h;
        loadingText.gameObject.SetActive(false);
        player.Play();
    }

    void OnError(VideoPlayer source, string message)
    {
        Debug.LogWarning("Video de introducción: no se pudo reproducir (" + message + "). Se continúa sin video.");
        Finish();
    }

    void Update()
    {
        if (!InProgress || finished) return;
        Keyboard k = Keyboard.current;
        if (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame))
            RequestSkip();
        if (skipRequested) Finish();
    }

    void RequestSkip() => skipRequested = true;

    void Finish()
    {
        if (finished) return;
        finished = true;
        InProgress = false;

        if (player != null)
        {
            player.Stop();
            Destroy(player);
        }
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
        if (canvas != null) Destroy(canvas.gameObject);
        VoiceCommands.Pop(voice);
        ResumeGameAudio();
        onDone?.Invoke();
    }

    // ---------- Pantalla ----------

    void BuildUi()
    {
        PeruUI.EnsureEventSystem();
        canvas = PeruUI.CreateCanvas("VideoIntro", 300);   // encima de todo
        canvas.transform.SetParent(transform, false);

        RectTransform bg = PeruUI.Rect(canvas.transform, "Fondo", Vector2.zero, Vector2.zero);
        PeruUI.Stretch(bg);
        bg.gameObject.AddComponent<Image>().color = Color.black;

        RectTransform area = PeruUI.Rect(bg, "Video", Vector2.zero, Vector2.zero);
        PeruUI.Stretch(area);
        screen = area.gameObject.AddComponent<RawImage>();
        screen.color = Color.black;
        screen.raycastTarget = false;
        fitter = area.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 16f / 9f;

        loadingText = PeruUI.Label(bg, "Cargando", "CARGANDO VIDEO…", new Vector2(0.5f, 0.5f), new Vector2(900f, 60f),
            PeruTheme.HeadingFont, 36, PeruTheme.White);

        Button skip = PeruUI.Button(bg, "OmitirButton", "Omitir video", new Vector2(1f, 0f), new Vector2(340f, 90f));
        var rt = (RectTransform)skip.transform;
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-40f, 40f);
        skip.onClick.AddListener(RequestSkip);

        if (!TouchMode.Active)
        {
            Text hint = PeruUI.Label(bg, "Pista", "ESPACIO / ENTER: omitir", new Vector2(1f, 0f), new Vector2(340f, 30f),
                PeruTheme.BodyFont, 20, PeruTheme.TextSoft);
            hint.rectTransform.pivot = new Vector2(1f, 0f);
            hint.rectTransform.anchoredPosition = new Vector2(-40f, 136f);
        }
    }

    // ---------- Música del juego en pausa ----------

    void PauseGameAudio()
    {
        foreach (AudioSource a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (!a.isPlaying) continue;
            a.Pause();
            pausedAudio.Add(a);
        }
    }

    void ResumeGameAudio()
    {
        foreach (AudioSource a in pausedAudio)
            if (a != null) a.UnPause();
        pausedAudio.Clear();
    }

    void OnDestroy()
    {
        if (texture != null) texture.Release();
        if (InProgress) InProgress = false;
    }
}
