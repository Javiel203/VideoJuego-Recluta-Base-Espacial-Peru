using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Prueba automática del MODO COOPERATIVO de la misión final (Play Mode, sin intervención):
//   sesión cooperativa → despegue → pantalla dividida (cámaras, HUD, controles) → objetivos en los dos HUD
//   → la IA ataca al jugador más cercano → nave derribada y reemplazo → jugador fuera de combate
//   → derrota del equipo → victoria en cooperativo → vuelta al modo individual.
// Restaura los PlayerPrefs del progreso al terminar.
// Línea de comandos: Unity -batchmode -projectPath ... -executeMethod DefensaCoopSmokeTest.Run [-captureDir carpeta]
// (sin -quit: la prueba cierra Unity con código 0 si todo pasa, 1 si algo falla).
public static class DefensaCoopSmokeTest
{
    static IEnumerator routine;
    static double waitUntil;
    static readonly List<string> failures = new List<string>();
    static readonly List<string> errors = new List<string>();
    static readonly string[] Keys =
    {
        "Trial_Destreza_0", "Trial_Memoria_0", "Trial_Defensa_0",
        "Trial_Destreza_1", "Trial_Memoria_1", "Trial_Defensa_1",
        "Trial_Destreza_2", "Trial_Memoria_2", "Trial_Defensa_2",
        "Defensa_HighScore", "Difficulty",
    };
    static readonly Dictionary<string, int> savedPrefs = new Dictionary<string, int>();

    [MenuItem("Astro Rocket/Defensa espacial/Prueba automática cooperativo (Play Mode)")]
    public static void Run()
    {
        failures.Clear();
        errors.Clear();
        savedPrefs.Clear();
        foreach (string k in Keys) savedPrefs[k] = PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k) : -1;

        Difficulty.Current = DifficultyLevel.Facil;
        EditorSceneManager.OpenScene("Assets/Scenes/DefensaEspacial.unity", OpenSceneMode.Single);
        Application.logMessageReceived += OnLog;
        routine = Scenario();
        waitUntil = 0;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add($"{type}: {message}\n{stack}");
        if (message.Contains("Invalid AABB")) DumpBadGraphics();
    }

    static bool dumping;
    static void DumpBadGraphics()
    {
        if (dumping) return;
        dumping = true;
        foreach (Graphic g in Resources.FindObjectsOfTypeAll<Graphic>())
        {
            if (g == null || EditorUtility.IsPersistent(g)) continue;
            RectTransform rt = g.rectTransform;
            Vector3 p = rt.position, k = rt.lossyScale;
            Rect r = rt.rect;
            if (Bad(p.x) || Bad(p.y) || Bad(p.z) || Bad(k.x) || Bad(k.y) || Bad(r.width) || Bad(r.height) || Bad(r.x) || Bad(r.y))
            {
                string path = rt.name;
                for (Transform t = rt.parent; t != null; t = t.parent) path = t.name + "/" + path;
                Debug.Log($"[COOP AABB] {path} activo={g.isActiveAndEnabled} pos={p} escala={k} rect={r}");
            }
        }
        dumping = false;
    }

    static void Tick()
    {
        if (routine == null) return;
        if (EditorApplication.timeSinceStartup < waitUntil) return;
        bool more;
        try { more = routine.MoveNext(); }
        catch (System.Exception e) { failures.Add("Excepción en el escenario: " + e); more = false; }
        if (!more) { routine = null; Finish(); return; }
        if (routine.Current is float seconds) waitUntil = EditorApplication.timeSinceStartup + seconds;
    }

    static void Check(bool condition, string what)
    {
        Debug.Log((condition ? "[COOP OK] " : "[COOP FALLO] ") + what);
        if (!condition) failures.Add(what);
    }

    static T Find<T>() where T : Object => Object.FindAnyObjectByType<T>();

    static DefensaCoop Coop => Find<DefensaCoop>();

    static void ClickButton(string name)
    {
        GameObject go = GameObject.Find(name);
        go.GetComponent<Button>().onClick.Invoke();
    }

    static void SpawnEnemy()
    {
        var manager = Find<EnemyShipManager>();
        typeof(EnemyShipManager).GetMethod("SpawnEnemyShip", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(manager, null);
    }

    static EnemyShipController FirstEnemy()
    {
        foreach (var enemy in Object.FindObjectsByType<EnemyShipController>(FindObjectsSortMode.None))
            if (enemy.gameObject.activeInHierarchy) return enemy;
        return null;
    }

    static void Kill(DefensaCoop.Player p) => p.Hull.TakeDamage(100000000, p.Ship.transform.position);

    // Omite el video de introducción (como el jugador con Esc / "omitir").
    static void SkipIntro()
    {
        var intro = Find<DefensaIntroVideo>();
        if (intro == null) return;
        typeof(DefensaIntroVideo).GetMethod("RequestSkip", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?.Invoke(intro, null);
    }

    static bool Bad(float f) => float.IsNaN(f) || float.IsInfinity(f) || Mathf.Abs(f) > 1e6f;

    // Rutas de los RectTransform con valores no válidos (provocan "Invalid AABB" al dibujar la UI).
    static string InvalidUi()
    {
        var bad = new List<string>();
        foreach (RectTransform rt in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
        {
            Vector3 p = rt.localPosition, k = rt.localScale;
            Rect r = rt.rect;
            if (Bad(p.x) || Bad(p.y) || Bad(p.z) || Bad(k.x) || Bad(k.y) || Bad(r.width) || Bad(r.height))
            {
                string path = rt.name;
                for (Transform t = rt.parent; t != null; t = t.parent) path = t.name + "/" + path;
                bad.Add($"{path} pos={p} scale={k} rect={r}");
            }
        }
        return string.Join(" | ", bad);
    }

    static string TextOf(string name) => GameObject.Find(name)?.GetComponent<Text>()?.text ?? "-";

    // Captura de la pantalla completa (las dos mitades) a 1600 x 900. Sólo con -captureDir.
    static void Capture(string name)
    {
        string dir = null;
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-captureDir") dir = args[i + 1];
        if (dir == null) return;

        const int w = 1600, h = 900;
        var rt = new RenderTexture(w, h, 24);
        RenderCameras(rt, name + "_escena", dir);
        // Cámara auxiliar (no pinta escena) para los Canvas "Overlay", que no se ven en una RenderTexture.
        var uiCamGo = new GameObject("CapturaUI");
        Camera uiCam = uiCamGo.AddComponent<Camera>();
        uiCam.clearFlags = CameraClearFlags.Nothing;
        uiCam.cullingMask = 1 << 5;   // capa UI
        uiCam.depth = 100;
        var changed = new List<Canvas>();
        var layers = new Dictionary<Transform, int>();
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = uiCam;
            c.planeDistance = 1f;
            changed.Add(c);
            foreach (Transform t in c.GetComponentsInChildren<Transform>(true))
            {
                layers[t] = t.gameObject.layer;
                t.gameObject.layer = 5;
            }
        }
        Canvas.ForceUpdateCanvases();

        var cams = new List<Camera>(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None));
        cams.RemoveAll(c => !c.enabled || !c.gameObject.activeInHierarchy || c.targetTexture != null);
        cams.Sort((a, b) => a.depth.CompareTo(b.depth));
        RenderTexture prevActive = RenderTexture.active;
        foreach (Camera cam in cams)
        {
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
        }
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        RenderTexture.active = prevActive;
        Object.DestroyImmediate(tex);
        rt.Release();
        foreach (Canvas c in changed) c.renderMode = RenderMode.ScreenSpaceOverlay;
        foreach (var kv in layers) if (kv.Key) kv.Key.gameObject.layer = kv.Value;
        Object.DestroyImmediate(uiCamGo);
    }

    // Sólo las cámaras del juego (escena + HUD del shooter de cada mitad).
    static void RenderCameras(RenderTexture rt, string name, string dir)
    {
        var cams = new List<Camera>(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None));
        cams.RemoveAll(c => !c.enabled || !c.gameObject.activeInHierarchy || c.targetTexture != null);
        cams.Sort((a, b) => a.depth.CompareTo(b.depth));
        RenderTexture prevActive = RenderTexture.active;
        foreach (Camera cam in cams)
        {
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
        }
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        RenderTexture.active = prevActive;
        Object.DestroyImmediate(tex);
    }

    static IEnumerator Scenario()
    {
        while (!EditorApplication.isPlaying) yield return 0.2f;
        yield return 1.5f;

        // ================= 1) Sesión informativa y modo cooperativo =================
        Check(GameObject.Find("DosJugadoresButton") != null, "La sesión informativa ofrece '2 jugadores'");
        ClickButton("DosJugadoresButton");
        yield return 0.3f;
        Check(GameObject.Find("SesionCooperativa") != null, "Se muestra la sesión del modo cooperativo");
        Check(TextOf("Jugador2Controles").Contains("FLECHAS"), "La sesión explica los controles del jugador 2");
        Capture("coop_1_sesion");
        ClickButton("DespegarButton");
        for (int f = 0; f < 20; f++)
        {
            Debug.Log($"[COOP DIAG] t+{f * 0.05f:F2}s UI inválida: " + InvalidUi());
            yield return 0.05f;
        }
        SkipIntro();
        for (float t = 0f; t < 20f && !(Coop != null && Coop.Deployed); t += 0.5f) yield return 0.5f;

        DefensaCoop coop = Coop;
        Check(coop != null && coop.Deployed, "El jugador 2 se despliega al terminar el despegue");
        if (coop == null || !coop.Deployed) yield break;
        Debug.Log("[COOP] UI inválida al desplegar: " + InvalidUi());
        yield return 1f;

        // ================= 2) Pantalla dividida =================
        DefensaCoop.Player p1 = coop.P1, p2 = coop.P2;
        Check(ShipController.Players.Count == 2, $"Hay 2 naves de jugador ({ShipController.Players.Count})");
        Check(p2.Ship != p1.Ship && p2.Ship.gameObject.activeInHierarchy, "La nave 2 existe y está activa");
        float distance = Vector3.Distance(p1.Ship.transform.position, p2.Ship.transform.position);
        Check(distance > 1f && distance < 200f, $"La nave 2 vuela en formación junto a la 1 ({distance:F1} m)");
        Check(p1.Camera.rect == DefensaCoop.LeftView && p2.Camera.rect == DefensaCoop.RightView, "Cámaras: J1 izquierda, J2 derecha");
        Check(Camera.main == p1.Camera, "Camera.main sigue siendo la del jugador 1");
        Check(p1.Camera.GetComponent<CinemachineBrain>().ChannelMask == OutputChannels.Default &&
              p2.Camera.GetComponent<CinemachineBrain>().ChannelMask == OutputChannels.Channel01,
            "Cada cámara usa su canal de Cinemachine");
        Check(System.Array.FindAll(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None), l => l.isActiveAndEnabled).Length == 1,
            "Un solo AudioListener activo");
        Check(InvalidUi() == "", "Ningún elemento de UI con posición o escala inválida: " + InvalidUi());
        Check(UIManager.All.Count == 2 && p1.Ui != p2.Ui, "Hay un HUD del shooter por jugador");
        Check(p1.Ui.MainCanvas.renderMode == RenderMode.ScreenSpaceCamera && p1.Ui.MainCanvas.worldCamera == p1.Camera &&
              p2.Ui.MainCanvas.worldCamera == p2.Camera, "Cada HUD se dibuja en su mitad");
        Check(DesktopMovementControls.MouseViewport == DefensaCoop.LeftView, "El ratón del jugador 1 apunta respecto a su mitad");
        var movementField = typeof(ShipController).GetField("_movementControls", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(movementField.GetValue(p2.Ship) is CoopKeyboardMovement, "La nave 2 usa los controles de teclado del jugador 2");
        Check(movementField.GetValue(p1.Ship) is DesktopMovementControls, "La nave 1 sigue con ratón + teclado");
        Check(p2.Ship.GetComponentInChildren<Blaster>().enabled && p2.Ship.GetComponentInChildren<MissileLauncher>().enabled,
            "La nave 2 tiene las armas activas");
        Check(!p2.Body.isKinematic, "La nave 2 no es cinemática (vuela con física)");
        BlasterUI blasterUi2 = p2.Ship.GetComponentInChildren<BlasterUI>(true);
        var barsField = typeof(BlasterUI).GetField("_blasterBars", BindingFlags.Instance | BindingFlags.NonPublic);
        Image[] bars2 = blasterUi2 != null ? (Image[])barsField.GetValue(blasterUi2) : null;
        Check(bars2 != null && bars2.Length > 0 && bars2[0].transform.IsChildOf(p2.Ui.MainCanvas.transform),
            "Las barras del láser de la nave 2 están en el HUD del jugador 2");
        Blaster blaster2 = p2.Ship.GetComponentInChildren<Blaster>();
        Check(blaster2.CapacitorChargePercentage > 0.5f, $"Láser de la nave 2 cargado ({blaster2.CapacitorChargePercentage:F2})");
        float fill2 = bars2 != null && bars2.Length > 0 ? bars2[0].fillAmount : -1f;
        Check(fill2 > 0.5f && fill2 <= 1f, $"La barra del láser del jugador 2 se ve cargada (fill {fill2})");
        Check(Mathf.Approximately(p2.Hull.DamageMultiplier, 0.05f), "FÁCIL: la nave 2 también recibe el 5 % del daño");
        Check(TextOf("CascoTextoJUGADOR 1").Contains("CASCO  100%") && TextOf("CascoTextoJUGADOR 2").Contains("CASCO  100%"),
            $"Un casco por jugador ({TextOf("CascoTextoJUGADOR 1")} / {TextOf("CascoTextoJUGADOR 2")})");
        Check(GameObject.Find("Division") != null, "Línea divisoria en el centro");
        Capture("coop_2_pantalla_dividida");

        // Color: la nave 2 lleva el casco azul (copias de material), la 1 sigue gris.
        bool painted2 = false, painted1 = false;
        foreach (Renderer r in p2.Ship.GetComponentsInChildren<Renderer>(true))
            foreach (Material m in r.sharedMaterials) if (m != null && m.name.EndsWith("(J2)")) painted2 = true;
        foreach (Renderer r in p1.Ship.GetComponentsInChildren<Renderer>(true))
            foreach (Material m in r.sharedMaterials) if (m != null && m.name.EndsWith("(J2)")) painted1 = true;
        Check(painted2 && !painted1, "La nave 2 está pintada de azul y la 1 no cambia");

        // Cámara exterior en las dos mitades (como pulsar 2 y 9) para ver las naves desde fuera.
        MethodInfo setCam = typeof(CameraManager).GetMethod("SetActiveCamera", BindingFlags.Instance | BindingFlags.NonPublic);
        System.Type camEnum = typeof(CameraManager).GetNestedType("VirtualCameras", BindingFlags.NonPublic);
        foreach (CameraManager cm in Object.FindObjectsByType<CameraManager>(FindObjectsSortMode.None))
            setCam.Invoke(cm, new[] { System.Enum.ToObject(camEnum, 1) });
        yield return 2.5f;
        Capture("coop_2b_naves_exterior");
        foreach (CameraManager cm in Object.FindObjectsByType<CameraManager>(FindObjectsSortMode.None))
            setCam.Invoke(cm, new[] { System.Enum.ToObject(camEnum, 0) });
        yield return 0.5f;

        // ================= 3) Objetivos, IA y derribo =================
        SpawnEnemy();
        yield return 1f;
        EnemyShipController enemy = FirstEnemy();
        Check(enemy != null, "Aparece una nave enemiga");
        if (enemy == null) yield break;
        int indicators1 = p1.Ui.MainCanvas.GetComponentsInChildren<TargetIndicator>(true).Length;
        int indicators2 = p2.Ui.MainCanvas.GetComponentsInChildren<TargetIndicator>(true).Length;
        Check(indicators1 > 0 && indicators1 == indicators2, $"Los dos HUD tienen los mismos objetivos ({indicators1} / {indicators2})");

        // La IA elige al jugador más cercano.
        enemy.transform.position = p2.Ship.transform.position + p2.Ship.transform.right * 300f;
        typeof(EnemyShipController).GetField("_nextRetarget", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, 0f);
        var playerProp = typeof(EnemyShipController).GetProperty("PlayerShip", BindingFlags.Instance | BindingFlags.NonPublic);
        Check((Transform)playerProp.GetValue(enemy) == p2.Ship.transform, "La IA enemiga apunta al jugador más cercano (J2)");
        enemy.transform.position = p1.Ship.transform.position - p1.Ship.transform.right * 300f;
        typeof(EnemyShipController).GetField("_nextRetarget", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, 0f);
        Check((Transform)playerProp.GetValue(enemy) == p1.Ship.transform, "…y cambia al J1 cuando éste queda más cerca");
        // Jugador 2: la nariz sigue a la mira (como el ratón del jugador 1) y Espacio apunta solo al enemigo.
        CoopKeyboardMovement keys2 = p2.Keys;
        FieldInfo aimField = typeof(CoopKeyboardMovement).GetField("aim", BindingFlags.Instance | BindingFlags.NonPublic);
        aimField.SetValue(keys2, new Vector2(0.8f, 0.5f));   // mira arriba a la derecha
        yield return 0.1f;
        Check(Mathf.Approximately(keys2.YawAmount, 0.8f) && Mathf.Approximately(keys2.PitchAmount, -0.5f),
            $"La mira arriba-derecha gira la nave a la derecha y sube el morro (yaw {keys2.YawAmount}, pitch {keys2.PitchAmount})");
        Check(GameObject.Find("MiraJugador2") != null, "Se ve la mira del jugador 2");
        aimField.SetValue(keys2, new Vector2(0.05f, -0.05f));
        yield return 0.1f;
        Check(keys2.YawAmount == 0f && keys2.PitchAmount == 0f, "Con la mira en el centro (zona muerta) la nave va recta");
        keys2.AimAtTarget(false);   // Espacio
        yield return 0.2f;
        Check(keys2.AutoAiming && keys2.Target == enemy.transform, "Espacio: apunta solo a la nave enemiga");
        Vector3 toEnemy = p2.Ship.transform.InverseTransformPoint(enemy.transform.position);
        Check(Mathf.Sign(keys2.YawAmount) == Mathf.Sign(toEnemy.x) && Mathf.Abs(keys2.YawAmount) > 0.01f,
            $"La asistencia gira la nave 2 hacia el enemigo (yaw {keys2.YawAmount:F2}, enemigo x {toEnemy.x:F0})");
        yield return 0.5f;
        Capture("coop_3_combate");
        keys2.ResetAim();
        enemy.GetComponent<DamageHandler>().TakeDamage(100000000, enemy.transform.position);
        yield return 1f;
        Check(TrialProgress.DefensaKills == 1, "Derribo contado para el equipo (1/2)");

        // ================= 4) Nave derribada y reemplazo =================
        Kill(p2);
        yield return 0.5f;
        Check(TrialProgress.DefensaLives == 2, $"Se gasta una nave de reserva ({TrialProgress.DefensaLives})");
        Check(!p2.Alive && p1.Alive, "Sólo cae la nave 2; la 1 sigue volando (no se recarga la escena)");
        Check(GameObject.Find("Derribado2") != null, "Aviso de nave destruida en la mitad del jugador 2");
        Check(TextOf("CascoTextoJUGADOR 2").Contains("CASCO  0%"), "El casco del jugador 2 marca 0 %");
        Capture("coop_4_derribado");
        yield return 3.5f;
        Check(p2.Alive, "La nave 2 reaparece a los 3 s");
        Check(ShipController.Players.Count == 2, "Vuelven a estar las 2 naves");
        Check(GameObject.Find("Derribado2") == null, "Se quita el aviso de la mitad del jugador 2");
        Check(Vector3.Distance(p1.Ship.transform.position, p2.Ship.transform.position) < 300f, "Reaparece junto al compañero");
        int hp = p2.Hull.Health;
        p2.Hull.TakeDamage(5000, p2.Ship.transform.position);
        Check(p2.Hull.Health == hp, "Escudo de 3 s al reaparecer");
        Check(p2.Hull.Health == p2.Hull.MaxHealth, "La nave nueva tiene el casco completo");

        // ================= 5) Sin reservas: fuera de combate y derrota =================
        Kill(p1);
        yield return 4f;
        Check(TrialProgress.DefensaLives == 1 && p1.Alive, "La nave 1 también se reemplaza (queda 1 de reserva)");
        Kill(p2);
        yield return 4f;
        Check(TrialProgress.DefensaLives == 0, "Sin naves de reserva");
        Check(!p2.Alive && p1.Alive, "El jugador 2 queda fuera de combate y el 1 sigue");
        Check(Find<DefeatScreen>() == null, "No hay derrota mientras un compañero vuela");
        Check((GameObject.Find("Derribado2")?.GetComponentInChildren<Text>()?.text ?? "-").Contains("FUERA DE COMBATE"), "Aviso de fuera de combate en la mitad del jugador 2");
        Kill(p1);
        yield return 3.5f;
        Check(Find<DefeatScreen>() != null, "Sin naves ni reservas: derrota del equipo");
        Capture("coop_5_derrota");

        // ================= 6) Victoria en cooperativo =================
        ClickButton("RetryButton");   // REINTENTAR
        yield return 2f;
        Check(GameObject.Find("SesionInformativa") != null, "REINTENTAR vuelve a la sesión informativa");
        Check(DefensaCoop.Enabled, "Se recuerda la elección de 2 jugadores");
        ClickButton("DosJugadoresButton");
        yield return 0.3f;
        ClickButton("DespegarButton");
        yield return 1f;
        SkipIntro();
        for (float t = 0f; t < 20f && !(Coop != null && Coop.Deployed); t += 0.5f) yield return 0.5f;
        Check(Coop != null && Coop.Deployed, "Nueva partida cooperativa desplegada");
        for (int i = 0; i < 2; i++)
        {
            SpawnEnemy();
            yield return 1f;
            EnemyShipController e = FirstEnemy();
            if (e != null) e.GetComponent<DamageHandler>().TakeDamage(100000000, e.transform.position);
            yield return 1f;
        }
        yield return 2.5f;
        Check(Find<VictoryScreen>() != null, "Victoria en cooperativo al derribar 2 naves");
        Capture("coop_6_victoria");

        // ================= 7) De vuelta al modo individual =================
        TrialProgress.StartDefensa();
        yield return 2.5f;
        ClickButton("DespegarButton");   // 1 jugador
        yield return 1f;
        SkipIntro();
        yield return 9f;
        Check(Find<DefensaCoop>() != null && !Find<DefensaCoop>().Deployed, "En individual no se despliega el jugador 2");
        Check(ShipController.Players.Count == 1, $"Una sola nave de jugador ({ShipController.Players.Count})");
        Check(Camera.main != null && Camera.main.rect == new Rect(0f, 0f, 1f, 1f), "Cámara a pantalla completa");
        Check(DesktopMovementControls.MouseViewport == new Rect(0f, 0f, 1f, 1f), "Ratón respecto a toda la pantalla");
        Check(UIManager.All.Count == 1 && UIManager.Instance.MainCanvas.renderMode == RenderMode.ScreenSpaceOverlay,
            "Un solo HUD del shooter (Overlay, como antes)");
        Check(TextOf("CascoTexto").Contains("CASCO"), "Casco único del modo individual");
        Capture("coop_7_individual");
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        foreach (string e in errors) Debug.Log("[COOP ERROR-LOG] " + e);
        foreach (string f in failures) Debug.Log("[COOP DETALLE] " + f);
        bool ok = failures.Count == 0 && errors.Count == 0;
        Debug.Log($"[COOP RESULTADO] {(ok ? "OK" : "FALLÓ")}: {failures.Count} comprobaciones fallidas, {errors.Count} errores en consola");
        EditorApplication.playModeStateChanged += AfterExit;
        EditorApplication.ExitPlaymode();

        void AfterExit(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= AfterExit;
            foreach (var kv in savedPrefs)
            {
                if (kv.Value < 0) PlayerPrefs.DeleteKey(kv.Key); else PlayerPrefs.SetInt(kv.Key, kv.Value);
            }
            PlayerPrefs.Save();
            Debug.Log("[COOP] PlayerPrefs restaurados");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
