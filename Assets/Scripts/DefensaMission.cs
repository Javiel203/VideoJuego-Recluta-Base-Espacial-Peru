using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// MISIÓN FINAL: DEFENSA ESPACIAL (escena DefensaEspacial, el shooter 3D).
// Se activa sola al cargar la escena, sin tocarla:
//  - Sesión informativa al entrar (objetivo y controles) con el juego en pausa.
//  - HUD: naves derribadas (x / 5) y oportunidades (3 naves).
//  - Derribar 5 naves enemigas → VictoryScreen (Trial.Defensa).
//  - Perder la nave: se resta una oportunidad y se recarga la escena (las naves derribadas se conservan).
//    Sin oportunidades → DefeatScreen.
//  - Esc → elegir prueba.
//  - Usa la gravedad del shooter original y restaura la de Astro Rocket al salir.
//  - Modo COOPERATIVO (2 jugadores, pantalla dividida, mismo teclado): se elige en la sesión informativa.
//    Lo monta DefensaCoop. Las naves de reserva y los derribos son del equipo; una nave derribada se
//    reemplaza junto al compañero sin recargar la escena. Sin naves de reserva y sin nadie volando → derrota.
public class DefensaMission : MonoBehaviour
{
    const string MissionTitle = "MISIÓN FINAL: DEFENSA ESPACIAL";
    static string MissionWithDifficulty => $"{MissionTitle}  ·  {Difficulty.CurrentName}";
    static readonly Vector3 ShooterGravity = new Vector3(0f, -9.81f, 0f);

    static readonly Color LifeOn = new Color(0.851f, 0.063f, 0.137f);
    static readonly Color LifeOff = new Color(1f, 1f, 1f, 0.15f);
    static readonly Color NoticeBad = new Color(0.7f, 0.05f, 0.1f, 0.92f);
    static readonly Color NoticeGood = new Color(0.06f, 0.45f, 0.2f, 0.92f);

    enum State { Intro, Briefing, Launching, Playing, Ended }

    State state;
    DefensaLaunch launch;   // salida desde el hangar de la nave nodriza
    DefensaIntroVideo intro;   // video de introducción (Assets/Video/videoFinal.mp4)
    Vector3 savedGravity;
    Canvas canvas;
    GameObject briefing;
    Text killsText;
    Image[] lifeIcons;
    // Barras de casco: una en individual, una por jugador en cooperativo.
    class HullBar
    {
        public DamageHandler Hull;
        public RectTransform Bar;
        public Image Fill;
        public Text Label;
        public string Prefix;
    }
    readonly List<HullBar> hullBars = new List<HullBar>();

    // Cooperativo
    DefensaCoop coop;
    bool coopSelected;        // se eligió "2 jugadores" y DefensaCoop.Setup() funcionó
    bool CoopActive => coopSelected && coop != null && coop.Deployed;
    int pendingRespawns;      // naves de reemplazo en camino (no hay derrota mientras llega una)
    readonly Dictionary<int, Text> downPanels = new Dictionary<int, Text>();
    CanvasGroup noticeGroup;
    Image noticeBg;
    Text noticeText;
    Coroutine noticeRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Play directamente desde la escena en el Editor: con "Enter Play Mode Options" sin recarga de escena
    // no se dispara sceneLoaded para la escena abierta.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CheckActiveScene() => OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != TrialProgress.DefensaScene) return;
        if (FindAnyObjectByType<DefensaMission>() != null) return;
        new GameObject("DefensaMission").AddComponent<DefensaMission>();
    }

    void Awake()
    {
        savedGravity = Physics.gravity;
        Physics.gravity = ShooterGravity;
        ShipController.ShipKilled += OnShipKilled;
        gameObject.AddComponent<DefensaVoice>();   // control por voz con asistencia de apuntado
        gameObject.AddComponent<DefensaTouch>();   // controles táctiles (móvil)
        launch = gameObject.AddComponent<DefensaLaunch>();
        intro = gameObject.AddComponent<DefensaIntroVideo>();
        coop = gameObject.AddComponent<DefensaCoop>();
    }

    void OnDestroy()
    {
        ShipController.ShipKilled -= OnShipKilled;
        foreach (HullBar bar in hullBars)
            if (bar.Hull != null) bar.Hull.HealthChanged.RemoveListener(RefreshHull);
        Physics.gravity = savedGravity;
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void Start()
    {
        PeruUI.EnsureEventSystem();
        canvas = PeruUI.CreateCanvas("DefensaHUD", 150);
        canvas.transform.SetParent(transform, false);
        BuildHud(canvas.transform);

        // Salud de la nave del jugador (barra "CASCO").
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.TryGetComponent(out DamageHandler playerHull))
        {
            // Dificultad: Fácil 5 %, Intermedio 50 %, Avanzado 100 % del daño.
            playerHull.DamageMultiplier = Difficulty.DefensaDamageTaken(Difficulty.Current);
            playerHull.HealthChanged.AddListener(RefreshHull);
            hullBars[0].Hull = playerHull;
        }
        RefreshHull();

        // Resplandor suave para motores, luces y disparos.
        DefensaBloom.Enable(transform);

        // La nave espera dentro del hangar de la nave nodriza (si falta el prefab, empieza como antes).
        launch.Prepare();

        if (TrialProgress.DefensaFreshStart)
            ShowBriefing();       // sesión informativa → DESPEGAR → video (se puede omitir) → salida del hangar
        else
            BeginLaunch(false);   // nave nueva tras perder una: salida corta
    }

    // Al pulsar DESPEGAR: video con el juego en pausa; al terminar (u omitirlo) sale la nave del hangar.
    void PlayIntro()
    {
        if (briefing != null) Destroy(briefing);
        state = State.Intro;
        Time.timeScale = 0f;
        EventSystem.current?.SetSelectedGameObject(null);
        intro.Play(() => StartCoroutine(LaunchAfterIntro()));
    }

    // Un frame después: la tecla que omitió el video (Esc / Enter / Espacio) no debe salir de la misión.
    IEnumerator LaunchAfterIntro()
    {
        yield return null;
        if (state == State.Intro) BeginLaunch(true);
    }

    // Despegue desde el hangar: completo al empezar la misión, corto con cada nave nueva.
    void BeginLaunch(bool full)
    {
        if (briefing != null) Destroy(briefing);
        state = State.Launching;
        Time.timeScale = 1f;
        canvas.enabled = false;   // sin marcador durante la animación
        EventSystem.current?.SetSelectedGameObject(null);
        launch.Play(full, () => OnLaunched(full));
    }

    void OnLaunched(bool full)
    {
        if (state != State.Launching) return;
        canvas.enabled = true;
        StartPlaying();
        if (coopSelected)
        {
            coop.Deploy();
            BuildCoopHud(canvas.transform);
            ShowNotice("¡DESPEGUE EN EQUIPO!\n<size=32>Jugador 1 a la izquierda  ·  Jugador 2 a la derecha</size>", NoticeGood, 2f);
        }
        else if (full)
        {
            ShowNotice("¡DESPEGUE!\n<size=32>Defiende la Base Espacial del Perú</size>", NoticeGood, 1.6f);
        }
        else
        {
            int lives = TrialProgress.DefensaLives;
            ShowNotice($"NUEVA NAVE LISTA\n<size=32>Te {(lives == 1 ? "queda" : "quedan")} {lives} {(lives == 1 ? "oportunidad" : "oportunidades")}</size>",
                NoticeGood, 1.8f);
        }
    }

    void Update()
    {
        // En pausa o en pantallas finales el cursor debe verse (el shooter lo oculta).
        if (state != State.Playing)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        // Durante el video, Esc lo omite (lo gestiona DefensaIntroVideo) en vez de salir de la misión.
        if (state != State.Ended && state != State.Intro && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TrialProgress.GoToSelection();
    }

    // ---------- Flujo ----------

    void StartPlaying()
    {
        if (briefing != null) Destroy(briefing);
        state = State.Playing;
        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
        EventSystem.current?.SetSelectedGameObject(null);
    }

    void OnShipKilled(ShipController ship)
    {
        if (state == State.Ended) return;

        // El prefab EnemyShip del proyecto original lleva además un ShipController normal (duplicado),
        // así que la nave del jugador se reconoce por su tag y sólo cuenta el EnemyShipController.
        if (ship.CompareTag("Player"))
        {
            DefensaCoop.Player coopPlayer = CoopActive ? coop.PlayerOf(ship) : null;
            if (coopPlayer != null)
                StartCoroutine(CoopPlayerDown(coopPlayer));
            else
                StartCoroutine(PlayerDown());
        }
        else if (ship is EnemyShipController)
        {
            int kills = TrialProgress.AddDefensaKill();
            RefreshHud();
            if (kills >= TrialProgress.DefensaKillsToWin)
            {
                StartCoroutine(Victory());
                return;
            }
            ShowNotice($"¡NAVE ENEMIGA DERRIBADA!\n<size=32>{kills} de {TrialProgress.DefensaKillsToWin}</size>", NoticeGood, 1.4f);
        }
    }

    IEnumerator PlayerDown()
    {
        state = State.Ended;
        int lives = TrialProgress.LoseDefensaLife();
        RefreshHud();
        ShowNotice(lives > 0
                ? $"¡NAVE DESTRUIDA!\n<size=32>Te {(lives == 1 ? "queda" : "quedan")} {lives} {(lives == 1 ? "oportunidad" : "oportunidades")}</size>"
                : "¡NAVE DESTRUIDA!\n<size=32>Sin oportunidades</size>",
            NoticeBad, 2f);

        yield return new WaitForSecondsRealtime(2.5f);

        if (lives > 0)
        {
            SceneManager.LoadScene(TrialProgress.DefensaScene);
            yield break;
        }

        Time.timeScale = 0f;
        HideHud();
        int kills = TrialProgress.DefensaKills;
        DefeatScreen.Show(MissionWithDifficulty,
            $"Perdiste tus {TrialProgress.DefensaMaxLives} naves. Derribaste {kills} de {TrialProgress.DefensaKillsToWin} naves enemigas.\n¡No te rindas, recluta! Vuelve a intentarlo.",
            TrialProgress.StartDefensa);
    }

    IEnumerator Victory()
    {
        state = State.Ended;
        if (GameManager.Instance) GameManager.Instance.PlayerWon();
        ShowNotice("¡MISIÓN CUMPLIDA!", NoticeGood, 1.5f);
        yield return new WaitForSecondsRealtime(2f);
        Time.timeScale = 0f;
        HideHud();
        VictoryScreen.Show(Trial.Defensa, TrialProgress.StartDefensa, "Elegir prueba");
    }

    // ---------- HUD ----------

    void BuildHud(Transform root)
    {
        // Naves derribadas (arriba al centro).
        RectTransform box = PeruUI.Rect(root, "Derribos", new Vector2(0.5f, 1f), new Vector2(620f, 92f));
        box.pivot = new Vector2(0.5f, 1f);
        box.anchoredPosition = new Vector2(0f, -66f);   // debajo de la puntuación del shooter
        var boxBg = box.gameObject.AddComponent<Image>();
        boxBg.sprite = PeruTheme.RoundedSprite;
        boxBg.type = Image.Type.Sliced;
        boxBg.color = new Color(PeruTheme.Panel.r, PeruTheme.Panel.g, PeruTheme.Panel.b, 0.8f);
        boxBg.raycastTarget = false;
        PeruUI.Label(box, "Titulo", $"NAVES ENEMIGAS DERRIBADAS  ·  {Difficulty.CurrentName}", new Vector2(0.5f, 0.74f), new Vector2(600f, 30f),
            PeruTheme.HeadingFont, 20, PeruTheme.TextSoft);
        killsText = PeruUI.Label(box, "Contador", "", new Vector2(0.5f, 0.33f), new Vector2(600f, 50f),
            PeruTheme.HeadingFont, 40, PeruTheme.White);
        PeruUI.AddSoftShadow(killsText.gameObject);

        // Oportunidades (arriba a la derecha).
        lifeIcons = new Image[TrialProgress.DefensaMaxLives];
        for (int i = 0; i < lifeIcons.Length; i++)
        {
            RectTransform icon = PeruUI.Rect(root, "Oportunidad" + (i + 1), new Vector2(1f, 1f), new Vector2(46f, 46f));
            icon.pivot = new Vector2(1f, 1f);
            icon.anchoredPosition = new Vector2(-40f - (lifeIcons.Length - 1 - i) * 60f, -30f);
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = PeruTheme.RoundedSprite;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            var outline = icon.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            lifeIcons[i] = img;
        }
        Text livesLabel = PeruUI.Label(root, "OportunidadesTexto", TouchMode.Active ? "OPORTUNIDADES" : "OPORTUNIDADES   ·   ESC: SALIR", new Vector2(1f, 1f),
            new Vector2(420f, 30f), PeruTheme.HeadingFont, 18, PeruTheme.TextSoft, TextAnchor.MiddleRight);
        livesLabel.rectTransform.pivot = new Vector2(1f, 1f);
        livesLabel.rectTransform.anchoredPosition = new Vector2(-40f, -84f);
        PeruUI.AddSoftShadow(livesLabel.gameObject);

        // Casco de la nave (abajo al centro).
        hullBars.Add(CreateHullBar(root, 0.5f, 520f, ""));

        // Aviso central (oculto).
        RectTransform notice = PeruUI.Rect(root, "Aviso", new Vector2(0.5f, 0.68f), new Vector2(900f, 150f));
        noticeBg = notice.gameObject.AddComponent<Image>();
        noticeBg.sprite = PeruTheme.RoundedSprite;
        noticeBg.type = Image.Type.Sliced;
        noticeBg.raycastTarget = false;
        var bgOutline = notice.gameObject.AddComponent<Outline>();
        bgOutline.effectColor = new Color(1f, 1f, 1f, 0.7f);
        bgOutline.effectDistance = new Vector2(3f, -3f);
        noticeGroup = notice.gameObject.AddComponent<CanvasGroup>();
        noticeGroup.alpha = 0f;
        noticeGroup.blocksRaycasts = false;
        noticeGroup.interactable = false;
        noticeText = PeruUI.Label(notice, "Texto", "", new Vector2(0.5f, 0.5f), new Vector2(860f, 140f),
            PeruTheme.HeadingFont, 46, PeruTheme.White, TextAnchor.MiddleCenter, wrap: true);
        noticeText.supportRichText = true;
        PeruUI.AddSoftShadow(noticeText.gameObject);

        RefreshHud();
    }

    void RefreshHud()
    {
        killsText.text = $"{Mathf.Min(TrialProgress.DefensaKills, TrialProgress.DefensaKillsToWin)} / {TrialProgress.DefensaKillsToWin}";
        int lives = TrialProgress.DefensaLives;
        for (int i = 0; i < lifeIcons.Length; i++)
            lifeIcons[i].color = i < lives ? LifeOn : LifeOff;
    }

    HullBar CreateHullBar(Transform root, float x, float width, string prefix)
    {
        string suffix = prefix.Length > 0 ? prefix.Trim(' ', '·') : "";
        RectTransform hull = PeruUI.Rect(root, "Casco" + suffix, new Vector2(x, 0f), new Vector2(width, 26f));
        hull.pivot = new Vector2(0.5f, 0f);
        hull.anchoredPosition = new Vector2(0f, 34f);
        var hullBg = hull.gameObject.AddComponent<Image>();
        hullBg.color = new Color(0f, 0f, 0f, 0.55f);
        hullBg.raycastTarget = false;
        var hullOutline = hull.gameObject.AddComponent<Outline>();
        hullOutline.effectColor = new Color(1f, 1f, 1f, 0.7f);
        hullOutline.effectDistance = new Vector2(2f, -2f);
        RectTransform fillRt = PeruUI.Rect(hull, "Relleno", Vector2.zero, Vector2.zero);
        PeruUI.Stretch(fillRt, 3f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        var fill = fillRt.gameObject.AddComponent<Image>();
        fill.color = LifeOn;
        fill.raycastTarget = false;
        Text label = PeruUI.Label(root, "CascoTexto" + suffix, "CASCO", new Vector2(x, 0f), new Vector2(width, 30f),
            PeruTheme.HeadingFont, 20, PeruTheme.TextSoft);
        label.rectTransform.pivot = new Vector2(0.5f, 0f);
        label.rectTransform.anchoredPosition = new Vector2(0f, 64f);
        PeruUI.AddSoftShadow(label.gameObject);
        return new HullBar { Bar = hull, Fill = fill, Label = label, Prefix = prefix };
    }

    void RefreshHull()
    {
        foreach (HullBar bar in hullBars)
        {
            if (bar.Fill == null) continue;
            DamageHandler hull = bar.Hull;
            float k = hull != null && hull.MaxHealth > 0 ? Mathf.Clamp01((float)hull.Health / hull.MaxHealth) : 1f;
            if (hull != null && !hull.gameObject.activeInHierarchy) k = 0f;   // cooperativo: nave derribada
            // La barra se encoge hacia la izquierda.
            bar.Fill.rectTransform.anchorMax = new Vector2(k, 1f);
            bar.Fill.color = k > 0.3f ? new Color(0.25f, 0.85f, 0.4f) : LifeOn;
            bar.Label.text = $"{bar.Prefix}CASCO  {Mathf.RoundToInt(k * 100f)}%";
        }
    }

    void HideHud()
    {
        if (canvas != null) canvas.gameObject.SetActive(false);
    }

    void ShowNotice(string message, Color color, float duration)
    {
        if (noticeRoutine != null) StopCoroutine(noticeRoutine);
        noticeText.text = message;
        noticeBg.color = color;
        noticeRoutine = StartCoroutine(NoticeRoutine(duration));
    }

    IEnumerator NoticeRoutine(float duration)
    {
        Transform t = noticeGroup.transform;
        for (float e = 0f; e < 0.15f; e += Time.unscaledDeltaTime)
        {
            float k = e / 0.15f;
            noticeGroup.alpha = k;
            t.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, k);
            yield return null;
        }
        noticeGroup.alpha = 1f;
        t.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(duration);
        for (float e = 0f; e < 0.3f; e += Time.unscaledDeltaTime)
        {
            noticeGroup.alpha = 1f - e / 0.3f;
            yield return null;
        }
        noticeGroup.alpha = 0f;
        noticeRoutine = null;
    }

    // ---------- Sesión informativa ----------

    void ShowBriefing()
    {
        state = State.Briefing;
        Time.timeScale = 0f;

        RectTransform overlay = PeruUI.Rect(canvas.transform, "SesionInformativa", Vector2.zero, Vector2.zero);
        PeruUI.Stretch(overlay);
        briefing = overlay.gameObject;
        var bg = overlay.gameObject.AddComponent<Image>();
        bg.color = new Color(PeruTheme.Night.r, PeruTheme.Night.g, PeruTheme.Night.b, 0.9f);
        PeruUI.ScreenStripes(overlay, 14f);

        RectTransform panel = PeruUI.Panel(overlay, "Panel", new Vector2(0.5f, 0.5f), new Vector2(1500f, 920f));
        PeruUI.Emblem(panel, new Vector2(0.5f, 0.88f), 140f);

        Text title = PeruUI.Title(panel, MissionTitle, new Vector2(0.5f, 0.75f), 54);
        Text diff = PeruUI.Label(panel, "Dificultad", "DIFICULTAD: " + Difficulty.CurrentName, new Vector2(0.5f, 0.675f),
            new Vector2(800f, 40f), PeruTheme.HeadingFont, 30, PeruTheme.White);
        PeruUI.AddSoftShadow(diff.gameObject);
        title.rectTransform.sizeDelta = new Vector2(1440f, 90f);

        string pilot = string.IsNullOrWhiteSpace(PilotData.Name) ? "recluta" : PilotData.Name;
        Text story = PeruUI.Label(panel, "Historia",
            $"¡Alerta, {pilot}! Naves enemigas se acercan a la Base Espacial del Perú.\n" +
            $"Derriba {TrialProgress.DefensaKillsToWin} naves enemigas para convertirte en astronauta especializado. " +
            $"Tienes {TrialProgress.DefensaMaxLives} naves (oportunidades).",
            new Vector2(0.5f, 0.57f), new Vector2(1380f, 130f), PeruTheme.BodyBoldFont, 34, PeruTheme.White,
            TextAnchor.MiddleCenter, wrap: true);
        PeruUI.AddSoftShadow(story.gameObject);

        PeruUI.Label(panel, "ControlesTitulo", "CONTROLES", new Vector2(0.5f, 0.465f), new Vector2(800f, 50f),
            PeruTheme.HeadingFont, 36, PeruTheme.Red);
        bool touch = TouchMode.Active;
        PeruUI.Label(panel, "ControlesIzq", touch
            ? "JOYSTICK (izquierda)  —  apuntar\n" +
              "OBJETIVO  —  la nave apunta sola al enemigo\n" +
              "ACELERAR  —  al soltar sigue a media velocidad · FRENAR la detiene"
            : "RATÓN  —  apuntar (la nave gira hacia el cursor)\n" +
              "W  —  acelerar (al soltar sigue a media velocidad) · S  —  frenar\n" +
              "Q / E  —  rodar a la izquierda / derecha",
            new Vector2(0.27f, 0.32f), new Vector2(680f, 170f), PeruTheme.BodyBoldFont, 28, PeruTheme.TextSoft,
            TextAnchor.MiddleLeft, wrap: true);
        PeruUI.Label(panel, "ControlesDer", touch
            ? "FUEGO  —  mantener, o un toque = disparo continuo\n" +
              "MISIL  —  lanzar misil (al objetivo fijado)\n" +
              "SALIR  —  volver a elegir prueba"
            : "CLIC IZQUIERDO  —  disparar láser\n" +
              "CLIC DERECHO  —  lanzar misil (al objetivo fijado)\n" +
              "T  —  apuntar solo a la nave enemiga (otra vez: siguiente)\n" +
              "1 / 2 / 3  —  cámara     ESC  —  salir",
            new Vector2(0.73f, 0.32f), new Vector2(680f, 170f), PeruTheme.BodyBoldFont, 28, PeruTheme.TextSoft,
            TextAnchor.MiddleLeft, wrap: true);

        Text voice = PeruUI.Label(panel, "ControlesVoz",
            "VOZ:  di \"OBJETIVO\" (apunta solo) y \"FUEGO\"  ·  MISIL  ·  ACELERA / FRENA  ·  ALTO  ·  di \"DESPEGAR\" para empezar",
            new Vector2(0.5f, 0.195f), new Vector2(1420f, 44f), PeruTheme.BodyBoldFont, 26, PeruTheme.White,
            TextAnchor.MiddleCenter, wrap: true);
        PeruUI.AddSoftShadow(voice.gameObject);

        bool coopAvailable = DefensaCoop.Available;
        Button back = PeruUI.Button(panel, "VolverButton", "Elegir prueba",
            new Vector2(coopAvailable ? 0.2f : 0.33f, 0.1f), new Vector2(coopAvailable ? 400f : 460f, 100f));
        back.onClick.AddListener(TrialProgress.GoToSelection);
        Button go = PeruUI.Button(panel, "DespegarButton", "Despegar",
            new Vector2(coopAvailable ? 0.5f : 0.67f, 0.1f), new Vector2(coopAvailable ? 400f : 460f, 100f));
        go.onClick.AddListener(StartSingle);   // DESPEGAR → video → salida del hangar

        GameObject selected = go.gameObject;
        if (coopAvailable)
        {
            // Modo cooperativo: 2 jugadores en la misma computadora, pantalla dividida.
            Button duo = PeruUI.Button(panel, "DosJugadoresButton", "2 jugadores", new Vector2(0.8f, 0.1f), new Vector2(400f, 100f));
            duo.onClick.AddListener(ShowCoopBriefing);
            if (DefensaCoop.Enabled) selected = duo.gameObject;   // se recuerda la última elección
        }

        EventSystem.current?.SetSelectedGameObject(selected);
    }

    void StartSingle()
    {
        DefensaCoop.Enabled = false;
        coopSelected = false;
        PlayIntro();
    }

    // ---------- Cooperativo (2 jugadores) ----------

    void ShowCoopBriefing()
    {
        if (briefing != null) Destroy(briefing);
        state = State.Briefing;
        Time.timeScale = 0f;

        RectTransform overlay = PeruUI.Rect(canvas.transform, "SesionCooperativa", Vector2.zero, Vector2.zero);
        PeruUI.Stretch(overlay);
        briefing = overlay.gameObject;
        var bg = overlay.gameObject.AddComponent<Image>();
        bg.color = new Color(PeruTheme.Night.r, PeruTheme.Night.g, PeruTheme.Night.b, 0.9f);
        PeruUI.ScreenStripes(overlay, 14f);

        RectTransform panel = PeruUI.Panel(overlay, "Panel", new Vector2(0.5f, 0.5f), new Vector2(1500f, 920f));
        PeruUI.Emblem(panel, new Vector2(0.5f, 0.89f), 120f);

        Text title = PeruUI.Title(panel, "MODO COOPERATIVO", new Vector2(0.5f, 0.775f), 54);
        title.rectTransform.sizeDelta = new Vector2(1440f, 90f);
        Text diff = PeruUI.Label(panel, "Dificultad", "2 JUGADORES  ·  PANTALLA DIVIDIDA  ·  " + Difficulty.CurrentName,
            new Vector2(0.5f, 0.7f), new Vector2(1300f, 40f), PeruTheme.HeadingFont, 28, PeruTheme.White);
        PeruUI.AddSoftShadow(diff.gameObject);

        Text story = PeruUI.Label(panel, "Historia",
            $"Defiendan juntos la Base Espacial del Perú: derriben {TrialProgress.DefensaKillsToWin} naves enemigas entre los dos.\n" +
            $"El equipo comparte {TrialProgress.DefensaMaxLives} naves de reserva: si cae una nave, llega otra junto al compañero.",
            new Vector2(0.5f, 0.6f), new Vector2(1400f, 110f), PeruTheme.BodyBoldFont, 30, PeruTheme.White,
            TextAnchor.MiddleCenter, wrap: true);
        PeruUI.AddSoftShadow(story.gameObject);

        PeruUI.Label(panel, "Jugador1Titulo", "JUGADOR 1  ·  IZQUIERDA", new Vector2(0.27f, 0.49f), new Vector2(680f, 46f),
            PeruTheme.HeadingFont, 30, PeruTheme.Red);
        PeruUI.Label(panel, "Jugador2Titulo", "JUGADOR 2  ·  DERECHA", new Vector2(0.73f, 0.49f), new Vector2(680f, 46f),
            PeruTheme.HeadingFont, 30, PeruTheme.Red);
        PeruUI.Label(panel, "Jugador1Controles",
            "RATÓN  —  apuntar\n" +
            "W  —  acelerar · S  —  frenar\n" +
            "Q / E  —  rodar a la izquierda / derecha\n" +
            "CLIC IZQUIERDO  —  láser · CLIC DERECHO  —  misil\n" +
            "1 / 2 / 3  —  cámara",
            new Vector2(0.27f, 0.335f), new Vector2(680f, 240f), PeruTheme.BodyBoldFont, 23, PeruTheme.TextSoft,
            TextAnchor.MiddleLeft, wrap: true);
        PeruUI.Label(panel, "Jugador2Controles",
            CoopKeyboardMovement.Help + "\n" + CoopKeyboardWeapons.Help,
            new Vector2(0.73f, 0.335f), new Vector2(680f, 240f), PeruTheme.BodyBoldFont, 23, PeruTheme.TextSoft,
            TextAnchor.MiddleLeft, wrap: true);

        Text note = PeruUI.Label(panel, "Nota", "ESC  —  salir de la misión",
            new Vector2(0.5f, 0.185f), new Vector2(1300f, 40f), PeruTheme.BodyBoldFont, 24, PeruTheme.White);
        PeruUI.AddSoftShadow(note.gameObject);

        Button back = PeruUI.Button(panel, "VolverButton", "Volver", new Vector2(0.33f, 0.09f), new Vector2(460f, 100f));
        back.onClick.AddListener(() =>
        {
            if (briefing != null) Destroy(briefing);
            ShowBriefing();
        });
        Button go = PeruUI.Button(panel, "DespegarButton", "Despegar", new Vector2(0.67f, 0.09f), new Vector2(460f, 100f));
        go.onClick.AddListener(StartCoop);

        EventSystem.current?.SetSelectedGameObject(go.gameObject);
    }

    void StartCoop()
    {
        // Prepara (oculto) todo lo del jugador 2; si algo falla se juega en individual.
        coopSelected = coop.Setup();
        DefensaCoop.Enabled = coopSelected;
        PlayIntro();
    }

    // HUD del cooperativo: línea divisoria, un casco por jugador y avisos de nave derribada en cada mitad.
    void BuildCoopHud(Transform root)
    {
        if (!CoopActive) return;

        RectTransform divider = PeruUI.Rect(root, "Division", new Vector2(0.5f, 0.5f), Vector2.zero);
        divider.anchorMin = new Vector2(0.5f, 0f);
        divider.anchorMax = new Vector2(0.5f, 1f);
        divider.sizeDelta = new Vector2(6f, 0f);
        var line = divider.gameObject.AddComponent<Image>();
        line.color = new Color(1f, 1f, 1f, 0.85f);
        line.raycastTarget = false;
        divider.SetAsFirstSibling();

        // El casco único se cambia por uno en cada mitad.
        foreach (HullBar bar in hullBars)
        {
            if (bar.Bar != null) Destroy(bar.Bar.gameObject);
            if (bar.Label != null) Destroy(bar.Label.gameObject);
        }
        hullBars.Clear();
        foreach (DefensaCoop.Player p in coop.Players)
        {
            HullBar bar = CreateHullBar(root, p.Number == 1 ? 0.25f : 0.75f, 420f, $"JUGADOR {p.Number}  ·  ");
            bar.Hull = p.Hull;
            hullBars.Add(bar);
            if (p.Hull != null)
            {
                p.Hull.HealthChanged.RemoveListener(RefreshHull);
                p.Hull.HealthChanged.AddListener(RefreshHull);
            }
            downPanels[p.Number] = CreateDownPanel(root, p.Number);
        }
        RefreshHull();
    }

    // Velo sobre la mitad de un jugador derribado (cuenta atrás de la nave nueva o "fuera de combate").
    Text CreateDownPanel(Transform root, int number)
    {
        RectTransform half = PeruUI.Rect(root, "Derribado" + number, Vector2.zero, Vector2.zero);
        PeruUI.Stretch(half);
        half.anchorMin = new Vector2(number == 1 ? 0f : 0.5f, 0f);
        half.anchorMax = new Vector2(number == 1 ? 0.5f : 1f, 1f);
        var bg = half.gameObject.AddComponent<Image>();
        bg.color = new Color(PeruTheme.Night.r, PeruTheme.Night.g, PeruTheme.Night.b, 0.78f);
        bg.raycastTarget = false;
        Text text = PeruUI.Label(half, "Texto", "", new Vector2(0.5f, 0.5f), new Vector2(880f, 220f),
            PeruTheme.HeadingFont, 44, PeruTheme.White, TextAnchor.MiddleCenter, wrap: true);
        text.supportRichText = true;
        PeruUI.AddSoftShadow(text.gameObject);
        half.SetSiblingIndex(1);   // encima de la división, debajo del marcador y los avisos
        half.gameObject.SetActive(false);
        return text;
    }

    void SetDownPanel(int number, string message)
    {
        if (!downPanels.TryGetValue(number, out Text text) || text == null) return;
        text.transform.parent.gameObject.SetActive(message != null);
        if (message != null) text.text = message;
    }

    // Una nave del equipo cae: se gasta una nave de reserva y llega otra a los 3 s.
    // Sin reservas, ese jugador queda fuera; si tampoco vuela el compañero, el equipo pierde.
    IEnumerator CoopPlayerDown(DefensaCoop.Player player)
    {
        int lives = TrialProgress.LoseDefensaLife();
        bool replace = lives > 0;
        if (replace) pendingRespawns++;
        RefreshHud();

        yield return null;   // la nave ya está desactivada
        RefreshHull();

        if (!replace && !coop.AnyAlive && pendingRespawns == 0)
        {
            StartCoroutine(CoopDefeat());
            yield break;
        }

        string who = $"JUGADOR {player.Number}";
        if (!replace)
        {
            ShowNotice($"¡{who} FUERA DE COMBATE!\n<size=32>No quedan naves de reserva: ¡sigue luchando, compañero!</size>", NoticeBad, 2.2f);
            SetDownPanel(player.Number, "FUERA DE COMBATE\n<size=30>No quedan naves de reserva.\n¡Anima a tu compañero!</size>");
            yield break;
        }

        ShowNotice($"¡{who} DERRIBADO!\n<size=32>{(lives == 1 ? "Queda 1 nave" : $"Quedan {lives} naves")} de reserva</size>", NoticeBad, 1.8f);
        for (int s = 3; s > 0; s--)
        {
            if (state == State.Ended) yield break;
            SetDownPanel(player.Number, $"NAVE DESTRUIDA\n<size=30>Nueva nave en {s}…</size>");
            yield return new WaitForSeconds(1f);
        }
        pendingRespawns--;
        if (state == State.Ended) yield break;

        coop.Respawn(player);
        SetDownPanel(player.Number, null);
        RefreshHull();
        ShowNotice($"NUEVA NAVE: {who}\n<size=32>3 segundos de escudo</size>", NoticeGood, 1.4f);
    }

    IEnumerator CoopDefeat()
    {
        state = State.Ended;
        ShowNotice("¡NAVES DESTRUIDAS!\n<size=32>El equipo no tiene más naves</size>", NoticeBad, 2f);
        yield return new WaitForSecondsRealtime(2.5f);

        Time.timeScale = 0f;
        HideHud();
        int kills = TrialProgress.DefensaKills;
        DefeatScreen.Show(MissionWithDifficulty,
            $"El equipo perdió todas sus naves. Derribaron {kills} de {TrialProgress.DefensaKillsToWin} naves enemigas.\n¡No se rindan, reclutas! Vuelvan a intentarlo.",
            TrialProgress.StartDefensa);
    }
}
