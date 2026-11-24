using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Prueba automática en Play Mode (sin intervención):
//  - Misión final en FÁCIL: sesión informativa → derribo → daño reducido → pérdida de nave y recarga
//    → victoria con 2 naves → derrota.
//  - Reglas por dificultad: naves y daño (misión final), niveles (memoria), último nivel (destreza),
//    progreso separado por dificultad y botones de dificultad en CharacterSelect.
// Restaura los PlayerPrefs del progreso al terminar.
// Línea de comandos: Unity -batchmode -projectPath ... -executeMethod DefensaSmokeTest.Run
// (sin -quit: la prueba cierra Unity con código 0 si todo pasa, 1 si algo falla).
public static class DefensaSmokeTest
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

    [MenuItem("Astro Rocket/Defensa espacial/Prueba automática (Play Mode)")]
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
        Debug.Log((condition ? "[SMOKE OK] " : "[SMOKE FALLO] ") + what);
        if (!condition) failures.Add(what);
    }

    static T Find<T>() where T : Object => Object.FindAnyObjectByType<T>();

    static GameObject Player => GameObject.FindGameObjectWithTag("Player");

    static void SpawnEnemy()
    {
        var manager = Find<EnemyShipManager>();
        typeof(EnemyShipManager).GetMethod("SpawnEnemyShip", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(manager, null);
    }

    static bool KillOneEnemy()
    {
        foreach (var enemy in Object.FindObjectsByType<EnemyShipController>(FindObjectsSortMode.None))
        {
            if (!enemy.gameObject.activeInHierarchy) continue;
            enemy.GetComponent<DamageHandler>().TakeDamage(1000000, enemy.transform.position);
            return true;
        }
        return false;
    }

    static void KillPlayer()
    {
        GameObject p = Player;
        p.GetComponent<DamageHandler>().TakeDamage(1000000, p.transform.position);
    }

    // Captura opcional (argumento -captureDir): los Canvas "Overlay" se pasan un momento a "Screen Space - Camera".
    static void Capture(string name)
    {
        string dir = null;
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-captureDir") dir = args[i + 1];
        if (dir == null) return;
        Camera cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (cam == null) return;
        var changed = new List<Canvas>();
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = cam;
            c.planeDistance = Mathf.Max(cam.nearClipPlane + 0.05f, 0.5f);
            changed.Add(c);
        }
        Canvas.ForceUpdateCanvases();
        const int w = 1600, h = 900;
        var rt = new RenderTexture(w, h, 24);
        RenderTexture prevTarget = cam.targetTexture, prevActive = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        Object.DestroyImmediate(tex);
        rt.Release();
        foreach (Canvas c in changed) c.renderMode = RenderMode.ScreenSpaceOverlay;
    }

    static void ClickButton(string name)
    {
        GameObject go = GameObject.Find(name);
        go.GetComponent<Button>().onClick.Invoke();
    }

    static IEnumerator Scenario()
    {
        while (!EditorApplication.isPlaying) yield return 0.2f;
        yield return 1.5f;

        // ================= MISIÓN FINAL EN FÁCIL =================
        // 1) Sesión informativa (partida nueva).
        Check(Find<DefensaMission>() != null, "DefensaMission se crea al cargar la escena");
        Check(GameObject.Find("SesionInformativa") != null, "Se muestra la sesión informativa");
        Check(Mathf.Approximately(Time.timeScale, 0f), "El juego está en pausa durante la sesión informativa");
        Check(Mathf.Approximately(Physics.gravity.y, -9.81f), "Gravedad del shooter aplicada (-9.81)");
        Check(TrialProgress.DefensaLives == 3 && TrialProgress.DefensaKills == 0, "Empieza con 3 oportunidades y 0 derribos");
        Check(TrialProgress.DefensaKillsToWin == 2, "FÁCIL: hay que derribar 2 naves");
        Check(ScoreManager.Instance.Score == 0, $"La puntuación empieza en 0 ({ScoreManager.Instance.Score})");
        Text diffText = GameObject.Find("Dificultad")?.GetComponent<Text>();
        Check(diffText != null && diffText.text.Contains("FÁCIL"), "La sesión informativa muestra la dificultad");
        Capture("1_sesion_informativa");
        ClickButton("DespegarButton");
        yield return 0.5f;
        Check(Mathf.Approximately(Time.timeScale, 1f), "DESPEGAR reanuda el juego");
        Check(GameObject.Find("SesionInformativa") == null, "La sesión informativa se cierra");
        Check(GameManager.Instance != null && MusicManager.Instance != null, "Managers del shooter activos");
        Text counter = GameObject.Find("Contador")?.GetComponent<Text>();
        Check(counter != null && counter.text == "0 / 2", $"HUD: contador 0 / 2 ({(counter ? counter.text : "-")})");

        // 2) Derribar una nave enemiga.
        SpawnEnemy();
        yield return 1f;
        Check(Find<EnemyShipController>() != null, "Aparece una nave enemiga");
        Check(KillOneEnemy(), "Se puede dañar la nave enemiga");
        yield return 1f;
        Check(TrialProgress.DefensaKills == 1, "Derribo contado (1/2)");

        // 3a) Daño reducido al 5 % en FÁCIL.
        DamageHandler hull = Player.GetComponent<DamageHandler>();
        Check(hull != null && Mathf.Approximately(hull.DamageMultiplier, 0.05f), "FÁCIL: la nave recibe el 5 % del daño");
        Collider childCollider = Player.GetComponentInChildren<Collider>();
        Check(childCollider != null && childCollider.GetComponentInParent<IDamageable>() != null,
            "Un impacto en un collider hijo de la nave llega a su DamageHandler");
        int before = hull.Health;
        hull.TakeDamage(1000, Player.transform.position);    // 1000 de daño → 50 con el 5 %
        Check(before - hull.Health == 50, $"1000 de daño quitan sólo 50 de casco ({before} → {hull.Health})");
        yield return 0.3f;
        Text hullText = GameObject.Find("CascoTexto")?.GetComponent<Text>();
        string expected = $"{Mathf.RoundToInt(100f * hull.Health / hull.MaxHealth)}%";
        Check(hullText != null && hullText.text.Contains(expected), $"HUD del casco actualizado ({(hullText ? hullText.text : "-")}, esperado {expected})");
        Capture("2_hud_en_juego");

        // 3b) Perder la nave: resta oportunidad y recarga la escena, conservando los derribos.
        KillPlayer();
        yield return 1f;
        Check(TrialProgress.DefensaLives == 2, "Al perder la nave quedan 2 oportunidades");
        yield return 4f;
        Check(SceneManager.GetActiveScene().name == TrialProgress.DefensaScene && Player != null, "La escena se recarga con nave nueva");
        Check(GameObject.Find("SesionInformativa") == null, "Tras recargar no se repite la sesión informativa");
        Check(TrialProgress.DefensaKills == 1, "Los derribos se conservan tras recargar");
        Check(ScoreManager.Instance.Score == 0 && ScoreManager.Instance.HighScore <= 1000,
            $"Recargar no suma puntos por los asteroides descargados (puntos {ScoreManager.Instance.Score}, récord {ScoreManager.Instance.HighScore})");
        Check(Mathf.Approximately(Time.timeScale, 1f), "Tras recargar el juego no está en pausa");

        // 4) Victoria: segunda nave derribada (2/2).
        SpawnEnemy();
        yield return 1f;
        KillOneEnemy();
        yield return 3f;
        Check(TrialProgress.DefensaKills == 2, "2 derribos");
        Check(Find<VictoryScreen>() != null, "FÁCIL: victoria al derribar 2 naves");
        Check(TrialProgress.DefensaCompleted, "La misión final (FÁCIL) queda guardada como completada");
        Capture("3_victoria");

        // 5) Reintentar → perder 3 naves → derrota.
        ClickButton("Button0");   // REINTENTAR
        yield return 2f;
        Check(GameObject.Find("SesionInformativa") != null && TrialProgress.DefensaLives == 3 && TrialProgress.DefensaKills == 0,
            "REINTENTAR empieza la misión desde cero");
        ClickButton("DespegarButton");
        yield return 0.5f;
        for (int life = 3; life >= 1; life--)
        {
            KillPlayer();
            yield return 4f;
        }
        Check(Find<DefeatScreen>() != null, "Sin oportunidades: pantalla de derrota");
        Capture("4_derrota");

        // ================= INTERMEDIO Y AVANZADO (misión final) =================
        foreach (var (level, kills, damage) in new[] { (DifficultyLevel.Intermedio, 3, 0.5f), (DifficultyLevel.Avanzado, 5, 1f) })
        {
            Difficulty.Current = level;
            TrialProgress.StartDefensa();
            yield return 2.5f;
            ClickButton("DespegarButton");
            yield return 0.5f;
            Text c = GameObject.Find("Contador")?.GetComponent<Text>();
            DamageHandler h = Player.GetComponent<DamageHandler>();
            Check(TrialProgress.DefensaKillsToWin == kills && c != null && c.text == $"0 / {kills}",
                $"{Difficulty.Name(level)}: hay que derribar {kills} naves ({(c ? c.text : "-")})");
            Check(Mathf.Approximately(h.DamageMultiplier, damage), $"{Difficulty.Name(level)}: daño recibido x{damage}");
            Check(!TrialProgress.DefensaCompleted, $"{Difficulty.Name(level)}: progreso separado (no aparece completada)");
        }

        // ================= PRUEBA DE MEMORIA: niveles por dificultad =================
        foreach (var (level, levels) in new[] { (DifficultyLevel.Facil, 3), (DifficultyLevel.Intermedio, 6), (DifficultyLevel.Avanzado, 10) })
        {
            Difficulty.Current = level;
            TrialProgress.GoToMemoria();
            yield return 2f;
            var game = Find<MemoryGame>();
            int got = (int)typeof(MemoryGame).GetField("levelsToWin", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
            Check(got == levels, $"Memoria {Difficulty.Name(level)}: {levels} niveles ({got})");
            bool textOk = false;
            foreach (Text t in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.text.Contains($"<b>nivel {levels}</b>")) textOk = true;
            Check(textOk, $"Memoria {Difficulty.Name(level)}: las instrucciones dicen nivel {levels}");
            if (level == DifficultyLevel.Facil) Capture("6_memoria_facil");
        }

        // ================= PRUEBA DE DESTREZA: último nivel por dificultad =================
        MethodInfo loadNext = typeof(CollisionHandler).GetMethod("LoadNextLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var (level, final) in new[] { (DifficultyLevel.Facil, "Under"), (DifficultyLevel.Intermedio, "Through"), (DifficultyLevel.Avanzado, "Over") })
        {
            Difficulty.Current = level;
            TrialProgress.StartDestrezaLevels();
            yield return 2f;
            // Pasa niveles hasta que aparezca la victoria (máximo 4 intentos).
            string reached = SceneManager.GetActiveScene().name;
            for (int i = 0; i < 4 && Find<VictoryScreen>() == null; i++)
            {
                reached = SceneManager.GetActiveScene().name;
                loadNext.Invoke(Find<CollisionHandler>(), null);
                yield return 2f;
            }
            Check(Find<VictoryScreen>() != null && reached == final,
                $"Destreza {Difficulty.Name(level)}: termina en {final} (terminó en {reached})");
            Check(TrialProgress.DestrezaCompleted, $"Destreza {Difficulty.Name(level)}: queda completada");
        }

        // Progreso separado por dificultad.
        Difficulty.Current = DifficultyLevel.Facil;
        Check(!TrialProgress.MemoriaCompleted && TrialProgress.DestrezaCompleted && TrialProgress.DefensaCompleted,
            "FÁCIL: destreza y misión final completadas, memoria pendiente");
        Difficulty.Current = DifficultyLevel.Intermedio;
        Check(!TrialProgress.DefensaCompleted, "INTERMEDIO: la misión final sigue pendiente");

        // ================= CHARACTERSELECT: botones de dificultad =================
        Difficulty.Current = DifficultyLevel.Facil;
        TrialProgress.GoToSelection();
        yield return 2f;
        Check(Mathf.Approximately(Physics.gravity.y, -15f), "Gravedad de Astro Rocket restaurada (-15)");
        Check(Mathf.Approximately(Time.timeScale, 1f), "timeScale restaurado");
        Check(Find<GameManager>() == null && Find<MusicManager>() == null, "Los managers del shooter no sobreviven al cambio de escena");
        Check(GameObject.Find("Difficulty0") != null && GameObject.Find("Difficulty2") != null, "CharacterSelect tiene los botones de dificultad");
        Check(GameObject.Find("DefensaButton") != null, "CharacterSelect tiene el botón de la misión final");
        Capture("5_seleccion_facil");
        ClickButton("Difficulty2");
        yield return 0.3f;
        Text desc = GameObject.Find("DefensaButton")?.transform.Find("Description")?.GetComponent<Text>();
        Check(Difficulty.Current == DifficultyLevel.Avanzado, "El botón AVANZADO cambia la dificultad");
        Check(desc != null && desc.text.Contains("5 naves"), $"La descripción se adapta ({(desc ? desc.text : "-")})");
        Capture("7_seleccion_avanzado");
        ClickButton("Difficulty1");
        yield return 0.3f;
        Check(Difficulty.Current == DifficultyLevel.Intermedio, "El botón INTERMEDIO cambia la dificultad");
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        foreach (string e in errors) Debug.Log("[SMOKE ERROR-LOG] " + e);
        foreach (string f in failures) Debug.Log("[SMOKE DETALLE] " + f);
        bool ok = failures.Count == 0 && errors.Count == 0;
        Debug.Log($"[SMOKE RESULTADO] {(ok ? "OK" : "FALLÓ")}: {failures.Count} comprobaciones fallidas, {errors.Count} errores en consola");
        // Los PlayerPrefs se restauran DESPUÉS de salir del Play Mode (al salir aún se descargan objetos del juego).
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
            Debug.Log("[SMOKE] PlayerPrefs restaurados");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
