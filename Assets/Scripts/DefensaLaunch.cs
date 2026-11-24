using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

// MISIÓN FINAL: la nave del jugador sale del hangar de la NAVE NODRIZA (Resources/Defensa/Portanaves,
// la genera Tools/RECLUTA/Misión final: nave nodriza). DefensaMission lo añade y lo usa así:
//   Prepare()  → al cargar la escena: coloca la nodriza detrás del punto de inicio y la nave dentro del hangar.
//   Play(full) → al pulsar DESPEGAR (completa, 6 s) o al recibir una nave nueva (corta, 2 s).
// Secuencia completa:
//   0–1,5 s   plano exterior de la nodriza, luces de alerta.
//   1,5–2,5 s se abren las compuertas; plano desde dentro del hangar.
//   2,5–4,5 s la nave acelera por la pista y sale; plano exterior junto a la boca.
//   4,5–6 s   la cámara vuelve a la cabina y el jugador toma el control (ya en crucero).
// Se puede saltar con un toque, un clic, una tecla o diciendo "saltar".
// Si falta el prefab, la misión empieza como antes (sin animación).
public class DefensaLaunch : MonoBehaviour
{
    const string PrefabPath = "Defensa/Portanaves";

    // Medidas en largos de la nave del jugador (U).
    const float MouthBehindStartU = 4f;   // la boca del hangar queda 4 U detrás del punto de inicio
    const float ShipInsideU = 3.6f;       // la nave empieza 3,6 U dentro del hangar
    const float ShortStartInsideU = 1.4f; // en la versión corta empieza más cerca de la boca
    const float TopSpeedU = 4.4f;         // velocidad de salida (U por segundo)

    const float FullDuration = 6f, ShortDuration = 2f;
    const float FullAccelStart = 2.5f, AccelRamp = 1.2f;
    const float FullCameraRelease = 4.5f, ShortCameraRelease = 1.2f;
    const float SkipGrace = 0.35f;        // el clic de DESPEGAR no cuenta como "saltar"

    public static bool InProgress { get; private set; }

    Transform ship;
    Rigidbody shipBody;
    readonly List<Behaviour> disabledWeapons = new List<Behaviour>();
    EnemyShipManager enemies;

    Transform carrier, doorTop, doorBottom;
    Vector3 doorTopClosed, doorBottomClosed, doorTopOpen, doorBottomOpen;
    // Planos de cámara (puntos en el mundo). Cada nodriza puede traer los suyos (grupo "Planos").
    Vector3 shotAFrom, shotATo, shotALook, shotBFrom, shotBLook, shotCFrom;
    readonly List<Renderer> runway = new List<Renderer>();
    readonly List<Renderer> alerts = new List<Renderer>();
    readonly List<Renderer> engines = new List<Renderer>();
    MaterialPropertyBlock block;
    Color runwayColor, alertColor, engineColor;

    float unit;                 // U en metros
    Vector3 forward, up, right, mouth, shipStart;
    Quaternion shipRotation;
    bool prepared;

    CinemachineBrain brain;
    CinemachineCamera cinematic;
    CinemachineBlendDefinition savedBlend;

    AudioSource audioSource;
    AudioClip alarmClip, rumbleClip;
    VoiceContext voice;
    bool skipRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => InProgress = false;

    // ---------- Preparación (al cargar la escena) ----------

    public bool Prepare()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (player == null || prefab == null)
        {
            if (prefab == null) Debug.LogWarning("DefensaLaunch: falta " + PrefabPath + " (Tools/RECLUTA/Misión final: nave nodriza).");
            return false;
        }

        ship = player.transform;
        shipBody = player.GetComponent<Rigidbody>();
        shipRotation = ship.rotation;
        unit = MeasureShip(player);
        forward = ship.forward;
        up = ship.up;
        right = Vector3.Cross(up, forward).normalized;

        Vector3 startPoint = ship.position;
        mouth = startPoint - forward * (MouthBehindStartU * unit);
        shipStart = mouth - forward * (ShipInsideU * unit);

        carrier = Instantiate(prefab, mouth, Quaternion.LookRotation(forward, up)).transform;
        carrier.name = "Portanaves";
        carrier.localScale = Vector3.one * unit;
        CollectParts();

        // La nave espera dentro del hangar, quieta y sin armas.
        if (shipBody != null)
        {
            shipBody.linearVelocity = Vector3.zero;
            shipBody.angularVelocity = Vector3.zero;
            shipBody.isKinematic = true;
        }
        PlaceShip(0f);
        foreach (Behaviour b in player.GetComponentsInChildren<Blaster>(true)) Disable(b);
        foreach (Behaviour b in player.GetComponentsInChildren<MissileLauncher>(true)) Disable(b);

        // Los enemigos empiezan a contar su tiempo de aparición después de despegar.
        enemies = FindAnyObjectByType<EnemyShipManager>();
        if (enemies != null) enemies.enabled = false;

        ClearAsteroids();
        SetDoors(0f);
        SetAlerts(false, 0f);
        prepared = true;
        return true;
    }

    void Disable(Behaviour b)
    {
        if (!b.enabled) return;
        b.enabled = false;
        disabledWeapons.Add(b);
    }

    // Largo de la nave a partir de sus colisionadores (el retículo de puntería está lejos y no cuenta).
    public static float MeasureShip(GameObject player)
    {
        bool any = false;
        Bounds b = default;
        foreach (Collider c in player.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger) continue;
            if (!any) { b = c.bounds; any = true; }
            else b.Encapsulate(c.bounds);
        }
        float size = any ? Mathf.Max(b.size.x, b.size.z) : 0f;
        return size > 0.5f ? size : 10f;
    }

    void CollectParts()
    {
        doorTop = FindDeep(carrier, "DoorTop");
        doorBottom = FindDeep(carrier, "DoorBottom");
        if (doorTop != null) doorTopClosed = doorTop.localPosition;
        if (doorBottom != null) doorBottomClosed = doorBottom.localPosition;
        // Posición abierta: marcadores DoorTopOpen / DoorBottomOpen, o 1 unidad arriba / abajo.
        Transform topOpen = FindDeep(carrier, "DoorTopOpen"), bottomOpen = FindDeep(carrier, "DoorBottomOpen");
        doorTopOpen = topOpen != null ? topOpen.localPosition : doorTopClosed + Vector3.up;
        doorBottomOpen = bottomOpen != null ? bottomOpen.localPosition : doorBottomClosed + Vector3.down;
        CollectShots();

        Collect(FindDeep(carrier, "RunwayLights"), runway);
        Collect(FindDeep(carrier, "AlertLights"), alerts);
        Collect(FindDeep(carrier, "EngineGlow"), engines);
        // Las luces de pista, ordenadas desde el fondo hacia la boca (para la luz en cadena).
        runway.Sort((a, b) => a.transform.localPosition.z.CompareTo(b.transform.localPosition.z));

        block = new MaterialPropertyBlock();
        runwayColor = EmissionOf(runway);
        alertColor = EmissionOf(alerts);
        engineColor = EmissionOf(engines);
    }

    // Planos de cámara: los del prefab si los trae; si no, los de la nodriza simple (hangar en el costado).
    void CollectShots()
    {
        float U = unit;
        shotAFrom = Shot("CamA_Desde", mouth + forward * 9f * U - right * 10f * U + up * 3f * U);
        shotATo = Shot("CamA_Hasta", mouth + forward * 8f * U - right * 8.5f * U + up * 2.6f * U);
        shotALook = Shot("CamA_Mira", mouth - forward * 1.5f * U);
        shotBFrom = Shot("CamB_Desde", shipStart - forward * 1.05f * U + up * 0.4f * U);
        shotBLook = Shot("CamB_Mira", mouth + forward * 3f * U + up * 0.1f * U);
        shotCFrom = Shot("CamC_Desde", mouth + forward * 4.5f * U + right * 4.5f * U + up * 1.2f * U);
    }

    Vector3 Shot(string name, Vector3 fallback)
    {
        Transform t = FindDeep(carrier, name);
        return t != null ? t.position : fallback;
    }

    static void Collect(Transform parent, List<Renderer> list)
    {
        if (parent != null) list.AddRange(parent.GetComponentsInChildren<Renderer>());
    }

    static Color EmissionOf(List<Renderer> list) =>
        list.Count > 0 && list[0].sharedMaterial != null && list[0].sharedMaterial.HasProperty("_EmissionColor")
            ? list[0].sharedMaterial.GetColor("_EmissionColor")
            : Color.white;

    static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    // Quita los asteroides que atravesarían la nodriza, la pista de salida o el primer plano de cámara.
    void ClearAsteroids()
    {
        Bounds zone = new Bounds(carrier.position, Vector3.zero);
        foreach (Renderer r in carrier.GetComponentsInChildren<Renderer>()) zone.Encapsulate(r.bounds);
        zone.Expand(3f * unit);

        Vector3 pathEnd = mouth + forward * (25f * unit);
        Vector3 cameraSpot = shotAFrom;
        float pathRadius = 4f * unit;

        foreach (Asteroid a in FindObjectsByType<Asteroid>(FindObjectsSortMode.None))
        {
            Vector3 p = a.transform.position;
            if (zone.Contains(p) || DistanceToSegment(p, shipStart, pathEnd) < pathRadius ||
                (p - cameraSpot).sqrMagnitude < pathRadius * pathRadius)
                Destroy(a.gameObject);
        }
    }

    static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector3.Distance(p, a + ab * t);
    }

    // ---------- Secuencia ----------

    public void Play(bool full, Action onDone)
    {
        if (!prepared)
        {
            onDone?.Invoke();
            return;
        }
        StartCoroutine(Run(full, onDone));
    }

    IEnumerator Run(bool full, Action onDone)
    {
        InProgress = true;
        skipRequested = false;
        float duration = full ? FullDuration : ShortDuration;
        float release = full ? FullCameraRelease : ShortCameraRelease;
        float accelStart = full ? FullAccelStart : 0f;

        voice = new VoiceContext("VOZ:  di SALTAR para omitir el despegue").On(() => skipRequested = true, "saltar", "omitir");
        VoiceCommands.Push(voice);
        SetupAudio();
        StartCinematicCamera();
        if (!full) SetDoors(1f);

        float t = 0f;
        bool cruising = false, released = false;
        int alarmsPlayed = 0;
        UpdateCamera(0f, full);

        while (t < duration)
        {
            if (!skipRequested && t > SkipGrace && SkipPressed()) skipRequested = true;
            if (skipRequested) break;

            // Compuertas, alerta y luces.
            if (full) SetDoors(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.5f, 2.4f, t)));
            bool alerting = full ? t < 3f : t < 1.2f;
            SetAlerts(alerting, t);
            UpdateRunway(t);

            if (full && alarmsPlayed < 4 && t >= 0.3f + alarmsPlayed * 0.6f)
            {
                alarmsPlayed++;
                if (audioSource != null) audioSource.PlayOneShot(alarmClip, 0.35f);
            }

            // Motores: la nave sale ya en crucero (media potencia al tomar el control).
            if (!cruising && t >= accelStart)
            {
                cruising = true;
                ShipThrottle.StartCruise();
                if (audioSource != null) audioSource.PlayOneShot(rumbleClip, 0.6f);
            }

            PlaceShip(Distance(t, full));
            if (!released) UpdateCamera(t, full);
            if (!released && t >= release)
            {
                released = true;
                ReleaseCamera(full ? 1.5f : 0.8f);
            }

            t += Time.deltaTime;
            yield return null;
        }

        // Fin (o se saltó): la nave queda fuera del hangar, ya en movimiento.
        float end = duration;
        PlaceShip(Distance(end, full));
        SetDoors(1f);
        SetAlerts(false, 0f);
        ShipThrottle.StartCruise();
        if (!released) ReleaseCamera(0.4f);

        if (shipBody != null)
        {
            shipBody.isKinematic = false;
            shipBody.linearVelocity = forward * (TopSpeedU * unit);
            shipBody.angularVelocity = Vector3.zero;
        }
        foreach (Behaviour b in disabledWeapons)
            if (b != null) b.enabled = true;
        disabledWeapons.Clear();
        if (enemies != null) enemies.enabled = true;

        VoiceCommands.Pop(voice);
        InProgress = false;
        prepared = false;
        onDone?.Invoke();
    }

    // Distancia recorrida desde la posición inicial: acelera (rampa) y luego sigue a velocidad máxima.
    float Distance(float t, bool full)
    {
        float vmax = TopSpeedU * unit;
        if (full)
        {
            float tau = t - FullAccelStart;
            if (tau <= 0f) return 0f;
            if (tau < AccelRamp) return 0.5f * vmax / AccelRamp * tau * tau;
            return 0.5f * vmax * AccelRamp + vmax * (tau - AccelRamp);
        }

        // Versión corta: ya cerca de la boca y moviéndose.
        float offset = (ShipInsideU - ShortStartInsideU) * unit;
        float v0 = 0.5f * vmax, ramp = 0.5f;
        if (t < ramp) return offset + v0 * t + 0.5f * (vmax - v0) / ramp * t * t;
        return offset + v0 * ramp + 0.5f * (vmax - v0) * ramp + vmax * (t - ramp);
    }

    void PlaceShip(float distance)
    {
        Vector3 p = shipStart + forward * distance;
        ship.SetPositionAndRotation(p, shipRotation);
        if (shipBody != null)
        {
            shipBody.position = p;
            shipBody.rotation = shipRotation;
        }
    }

    static bool SkipPressed()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        foreach (TouchPointers.Pointer p in TouchPointers.Current)
            if (p.began) return true;   // también el clic del ratón
        return false;
    }

    // ---------- Compuertas y luces ----------

    void SetDoors(float open)
    {
        if (doorTop != null) doorTop.localPosition = Vector3.Lerp(doorTopClosed, doorTopOpen, open);
        if (doorBottom != null) doorBottom.localPosition = Vector3.Lerp(doorBottomClosed, doorBottomOpen, open);
    }

    void SetAlerts(bool on, float t)
    {
        float k = on ? (Mathf.Repeat(t, 0.5f) < 0.25f ? 1.4f : 0.15f) : 0.1f;
        SetEmission(alerts, alertColor * k);
    }

    void UpdateRunway(float t)
    {
        for (int i = 0; i < runway.Count; i++)
        {
            int row = i / 2;   // dos luces (izquierda y derecha) por fila
            float wave = Mathf.Max(0f, Mathf.Sin((t * 2.2f - row * 0.12f) * Mathf.PI * 2f));
            SetEmission(runway[i], runwayColor * (0.5f + 2f * wave));
        }
    }

    void Update()
    {
        // La nodriza "respira": brillo de motores suave todo el tiempo.
        if (carrier == null || engines.Count == 0) return;
        SetEmission(engines, engineColor * (0.85f + 0.15f * Mathf.Sin(Time.time * 3f)));
    }

    void SetEmission(List<Renderer> list, Color c)
    {
        foreach (Renderer r in list) SetEmission(r, c);
    }

    void SetEmission(Renderer r, Color c)
    {
        if (r == null) return;
        r.GetPropertyBlock(block);
        block.SetColor("_EmissionColor", c);
        r.SetPropertyBlock(block);
    }

    // ---------- Cámara de cine (Cinemachine) ----------

    void StartCinematicCamera()
    {
        // La cámara del jugador 1 (en cooperativo la del jugador 2 no lleva el tag MainCamera).
        Camera main = Camera.main;
        brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
        if (brain == null) brain = FindAnyObjectByType<CinemachineBrain>();
        if (brain == null) return;

        var go = new GameObject("CamaraDespegue");
        cinematic = go.AddComponent<CinemachineCamera>();
        cinematic.Priority = 1000;
        Camera output = brain.OutputCamera;
        cinematic.Lens.FieldOfView = 55f;
        if (output != null)
        {
            cinematic.Lens.NearClipPlane = output.nearClipPlane;
            cinematic.Lens.FarClipPlane = output.farClipPlane;
        }

        savedBlend = brain.DefaultBlend;
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);   // entrada sin transición
    }

    void UpdateCamera(float t, bool full)
    {
        if (cinematic == null) return;
        float U = unit;
        Vector3 pos, look;

        if (full && t < 1.5f)
        {
            // Plano general exterior, acercándose despacio.
            pos = Vector3.Lerp(shotAFrom, shotATo, t / 1.5f);
            look = shotALook;
        }
        else if (full && t < 3.3f)
        {
            // Dentro del hangar, detrás de la nave: se abren las compuertas y la nave arranca.
            pos = shotBFrom + forward * 0.1f * U * (t - 1.5f);
            look = shotBLook;
        }
        else
        {
            // Exterior junto a la boca: la nave pasa y se aleja.
            pos = shotCFrom;
            look = ship.position + forward * 0.5f * U;
        }

        cinematic.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, up));
    }

    // Devuelve el control a las cámaras del juego (cabina) con una transición suave.
    void ReleaseCamera(float blend)
    {
        if (cinematic == null || brain == null) return;
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blend);
        cinematic.Priority = -1000;
        StartCoroutine(CleanupCamera(blend + 0.2f));
    }

    IEnumerator CleanupCamera(float delay)
    {
        CinemachineCamera cam = cinematic;
        cinematic = null;
        yield return new WaitForSeconds(delay);
        if (cam != null) Destroy(cam.gameObject);
        if (brain != null) brain.DefaultBlend = savedBlend;
    }

    // ---------- Sonido (generado: no hace falta ningún archivo) ----------

    void SetupAudio()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
        if (alarmClip == null) alarmClip = MakeAlarm();
        if (rumbleClip == null) rumbleClip = MakeRumble();
    }

    // Dos tonos de aviso (hangar).
    static AudioClip MakeAlarm()
    {
        const int rate = 22050;
        int n = (int)(rate * 0.45f);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float freq = t < 0.22f ? 880f : 660f;
            float env = Mathf.Clamp01(Mathf.Min(t * 60f, (0.45f - t) * 30f));
            data[i] = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t)) * 0.25f * env;
        }
        var clip = AudioClip.Create("AlarmaHangar", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Rugido de motores: ruido grave que sube y se apaga (3,5 s).
    static AudioClip MakeRumble()
    {
        const int rate = 22050;
        const float length = 3.5f;
        int n = (int)(rate * length);
        var data = new float[n];
        var rng = new System.Random(7);
        float brown = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            brown = Mathf.Clamp(brown + ((float)rng.NextDouble() * 2f - 1f) * 0.06f, -1f, 1f) * 0.995f;
            float env = Mathf.Clamp01(t / 0.8f) * Mathf.Clamp01((length - t) / 1.2f);
            float hum = Mathf.Sin(2f * Mathf.PI * (55f + 25f * t) * t) * 0.25f;
            data[i] = (brown * 0.8f + hum) * env * 0.6f;
        }
        var clip = AudioClip.Create("RugidoDespegue", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
