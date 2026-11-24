using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

// MISIÓN FINAL EN COOPERATIVO LOCAL (2 jugadores, pantalla dividida, misma computadora).
// DefensaMission lo usa así:
//   Setup()   → al elegir "2 JUGADORES" en la sesión informativa: crea (oculto) todo lo del jugador 2
//               copiando lo del jugador 1: nave, cámaras (Cinemachine en el canal 01) y HUD del shooter.
//   Deploy()  → al terminar el despegue: la nave 2 aparece en formación junto a la 1 y la pantalla se divide
//               (jugador 1 a la izquierda con ratón + teclado, jugador 2 a la derecha solo con teclado).
//   Respawn() → nave nueva para un jugador derribado (sin recargar la escena).
// El modo individual no pasa por aquí: sin Setup() todo funciona como antes.
public class DefensaCoop : MonoBehaviour
{
    // Elección de la sesión informativa (se recuerda al reintentar la misión).
    public static bool Enabled { get; set; }
    // En móvil (modo táctil) no hay cooperativo: un solo teclado/pantalla táctil.
    public static bool Available => !TouchMode.Active;

    public static readonly Rect LeftView = new Rect(0f, 0f, 0.5f, 1f);
    public static readonly Rect RightView = new Rect(0.5f, 0f, 0.5f, 1f);
    static readonly Rect FullView = new Rect(0f, 0f, 1f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Enabled = false;
        DesktopMovementControls.MouseViewport = FullView;
    }

    public class Player
    {
        public int Number;                  // 1 o 2
        public ShipController Ship;
        public Rigidbody Body;
        public DamageHandler Hull;
        public Camera Camera;
        public Camera Background;
        public UIManager Ui;
        public CoopKeyboardMovement Keys;   // sólo el jugador 2
        public bool Alive => Ship != null && Ship.gameObject.activeInHierarchy;
    }

    public Player P1 { get; private set; }
    public Player P2 { get; private set; }
    public bool Deployed { get; private set; }
    public IEnumerable<Player> Players { get { yield return P1; yield return P2; } }

    GameObject p2Root;          // contenedor (inactivo hasta Deploy) con todo lo del jugador 2
    float unit = 10f;           // largo de la nave
    Vector3 spawnPoint;         // donde reaparece una nave si no hay compañero vivo
    Quaternion spawnRotation;

    // ---------- Preparación ----------

    public bool Setup()
    {
        if (p2Root != null) return true;

        GameObject shipGo = GameObject.FindGameObjectWithTag("Player");
        Camera mainCamera = Camera.main;
        UIManager ui = UIManager.Instance;
        CameraManager cameraManager = FindAnyObjectByType<CameraManager>();
        if (shipGo == null || mainCamera == null || ui == null || cameraManager == null ||
            !shipGo.TryGetComponent(out ShipController ship))
        {
            Debug.LogWarning("DefensaCoop: no se encontró la nave, la cámara o el HUD del jugador 1; se juega en individual.");
            return false;
        }

        P1 = new Player
        {
            Number = 1,
            Ship = ship,
            Body = ship.GetComponent<Rigidbody>(),
            Hull = ship.GetComponent<DamageHandler>(),
            Camera = mainCamera,
            Background = FindBackgroundCamera(null),
            Ui = ui,
        };

        // Lo que se copia para el jugador 2. Se meten juntos bajo una plantilla y se instancian de una vez:
        // así Unity reconecta las referencias entre ellos (la cámara sigue a la nave 2, el HUD 2 muestra
        // las armas de la nave 2, etc.).
        var members = new List<Transform> { ship.transform, ui.transform, cameraManager.transform };
        Transform cameraRoot = mainCamera.transform.root;
        // "--- Camera Setup ---" (cámara principal + cámaras virtuales). Si la cámara cuelga de algo con
        // otros sistemas, sólo se copian la cámara y las cámaras virtuales sueltas.
        if (cameraRoot != mainCamera.transform && cameraRoot.GetComponentInChildren<GameManager>(true) == null &&
            cameraRoot.GetComponentInChildren<ShipController>(true) == null)
        {
            members.Add(cameraRoot);
        }
        else
        {
            members.Add(mainCamera.transform);
            foreach (CinemachineVirtualCameraBase vcam in FindObjectsByType<CinemachineVirtualCameraBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!vcam.transform.IsChildOf(ship.transform) && !members.Contains(vcam.transform))
                    members.Add(vcam.transform);
        }
        if (P1.Background != null && !P1.Background.transform.IsChildOf(cameraRoot)) members.Add(P1.Background.transform);

        // Contenedor inactivo: la copia no se despierta (Awake/OnEnable/Start) hasta Deploy.
        p2Root = new GameObject("Jugador2");
        p2Root.transform.SetParent(transform, false);
        p2Root.SetActive(false);

        GameObject template = new GameObject("PlantillaJugador2");
        var parents = new List<(Transform t, Transform parent, int index)>();
        foreach (Transform t in members)
        {
            parents.Add((t, t.parent, t.GetSiblingIndex()));
            t.SetParent(template.transform, true);
        }
        GameObject copy = Instantiate(template, p2Root.transform);
        copy.name = "Copia";
        foreach (var (t, parent, index) in parents)
        {
            t.SetParent(parent, true);
            t.SetSiblingIndex(index);
        }
        Destroy(template);

        // ---- Nave 2
        ShipController ship2 = null;
        foreach (ShipController s in copy.GetComponentsInChildren<ShipController>(true))
            if (s.CompareTag("Player")) { ship2 = s; break; }
        Camera camera2 = null;
        foreach (CinemachineBrain b in copy.GetComponentsInChildren<CinemachineBrain>(true))
        {
            camera2 = b.GetComponent<Camera>();
            if (camera2 != null) break;
        }
        if (ship2 == null || camera2 == null)
        {
            Debug.LogWarning("DefensaCoop: la copia del jugador 2 está incompleta; se juega en individual.");
            Destroy(p2Root);
            p2Root = null;
            return false;
        }

        ship2.name = "Player Ship J2";
        PaintHull(ship2.gameObject, Player2Color);
        var controls = new GameObject("ControlesJugador2");
        controls.transform.SetParent(p2Root.transform, false);
        var keys = controls.AddComponent<CoopKeyboardMovement>();
        var weapons = controls.AddComponent<CoopKeyboardWeapons>();
        ship2.SetControls(keys, weapons);

        // La nave 1 está en el hangar sin armas (DefensaLaunch): la copia sale con las armas listas.
        foreach (Blaster b in ship2.GetComponentsInChildren<Blaster>(true)) b.enabled = true;
        foreach (MissileLauncher m in ship2.GetComponentsInChildren<MissileLauncher>(true)) m.enabled = true;
        if (ship2.TryGetComponent(out Rigidbody body2)) body2.isKinematic = false;
        keys.Init(ship2.transform, body2);   // la asistencia de apuntado (Espacio) necesita la nave

        // ---- Cámaras 2: sólo ven sus cámaras virtuales (canal 01); la del jugador 1, las del canal por defecto.
        camera2.tag = "Untagged";   // Camera.main sigue siendo la del jugador 1
        if (camera2.TryGetComponent(out AudioListener listener)) listener.enabled = false;
        camera2.GetComponent<CinemachineBrain>().ChannelMask = OutputChannels.Channel01;
        foreach (CinemachineVirtualCameraBase vcam in copy.GetComponentsInChildren<CinemachineVirtualCameraBase>(true))
            vcam.OutputChannel = OutputChannels.Channel01;
        if (mainCamera.TryGetComponent(out CinemachineBrain brain1)) brain1.ChannelMask = OutputChannels.Default;

        CameraManager cameraManager2 = copy.GetComponentInChildren<CameraManager>(true);
        if (cameraManager2 != null) cameraManager2.Keys = new[] { KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0 };

        // ---- HUD 2: sin los indicadores de objetivo que ya tenía el HUD 1 (se crean de nuevo para la cámara 2).
        UIManager ui2 = copy.GetComponentInChildren<UIManager>(true);
        if (ui2 != null && ui2.MainCanvas != null)
        {
            foreach (TargetIndicator indicator in ui2.MainCanvas.GetComponentsInChildren<TargetIndicator>(true))
                Destroy(indicator.gameObject);
            foreach (Transform child in ui2.MainCanvas.transform)
                if (child.name.Contains("Lead Indicator")) Destroy(child.gameObject);
            ui2.Configure(ship2.transform, camera2);
        }
        ui.Configure(ship.transform, mainCamera);

        RadarScreen radar1 = ship.GetComponentInChildren<RadarScreen>(true);
        if (radar1 != null) radar1.Ui = ui;
        RadarScreen radar2 = ship2.GetComponentInChildren<RadarScreen>(true);
        if (radar2 != null) radar2.Ui = ui2;

        P2 = new Player
        {
            Number = 2,
            Ship = ship2,
            Body = body2,
            Hull = ship2.GetComponent<DamageHandler>(),
            Camera = camera2,
            Background = FindBackgroundCamera(copy.transform),
            Ui = ui2,
            Keys = keys,
        };

        // El daño recibido según la dificultad (DefensaMission lo pone en la nave 1).
        if (P2.Hull != null && P1.Hull != null) P2.Hull.DamageMultiplier = P1.Hull.DamageMultiplier;
        return true;
    }

    // ---------- Color de la nave 2 ----------

    // Azul: se distingue de la nave 1 (gris) y de las enemigas (rojas).
    public static readonly Color Player2Color = new Color(0.25f, 0.55f, 1f);

    // Materiales del casco (alas, motores, fuselaje y exterior de la cabina). No se tocan la librea peruana
    // (bandera, franjas, emblema), el interior y el cristal de la cabina, los equipos ni las armas.
    static readonly string[] HullMaterials = { "Wing2", "Wing6", "Engine1", "MainBody2", "MainBody13", "Cockpit3Grey" };

    // Pinta el casco con copias de sus materiales (los originales, compartidos con la nave 1, no cambian).
    public static void PaintHull(GameObject ship, Color color)
    {
        var copies = new Dictionary<Material, Material>();
        foreach (Renderer r in ship.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null || System.Array.IndexOf(HullMaterials, m.name) < 0) continue;
                if (!copies.TryGetValue(m, out Material painted))
                {
                    painted = new Material(m) { name = m.name + " (J2)" };
                    if (painted.HasProperty("_BaseColor")) painted.SetColor("_BaseColor", m.GetColor("_BaseColor") * color);
                    if (painted.HasProperty("_Color")) painted.SetColor("_Color", m.GetColor("_Color") * color);
                    copies[m] = painted;
                }
                mats[i] = painted;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    // Cámara del fondo (cielo): la que tiene SkyboxSetter.
    static Camera FindBackgroundCamera(Transform under)
    {
        SkyboxSetter setter = under != null
            ? under.GetComponentInChildren<SkyboxSetter>(true)
            : FindAnyObjectByType<SkyboxSetter>();
        return setter != null ? setter.GetComponent<Camera>() : null;
    }

    // ---------- Despliegue (después del despegue) ----------

    public void Deploy()
    {
        if (p2Root == null || Deployed) return;
        Deployed = true;

        Transform s1 = P1.Ship.transform;
        unit = DefensaLaunch.MeasureShip(P1.Ship.gameObject);
        spawnPoint = s1.position;
        spawnRotation = s1.rotation;

        // La nave 2 vuela en formación a la derecha de la 1, un poco por detrás.
        Vector3 p = s1.position + s1.right * (2.5f * unit) - s1.forward * (1.5f * unit);
        P2.Ship.transform.SetPositionAndRotation(p, s1.rotation);
        p2Root.SetActive(true);

        if (P2.Body != null)
        {
            P2.Body.position = p;
            P2.Body.rotation = s1.rotation;
            P2.Body.linearVelocity = P1.Body != null ? P1.Body.linearVelocity : Vector3.zero;
            P2.Body.angularVelocity = Vector3.zero;
        }
        P2.Keys.StartCruise();   // igual que la nave 1, sale ya en crucero
        P2.Keys.ResetAim();

        SetSplitScreen();

        // Mira del jugador 2: la mueve con las flechas (como el cursor del ratón del jugador 1).
        if (P2.Ui != null && P2.Ui.MainCanvas != null)
            CoopAimReticle.Create(P2.Ui.MainCanvas, P2.Camera, P2.Keys, Player2Color);
    }

    void SetSplitScreen()
    {
        SetView(P1, LeftView);
        SetView(P2, RightView);
        DesktopMovementControls.MouseViewport = LeftView;
    }

    // Cámaras en su mitad de pantalla y HUD del shooter dibujado por su cámara (así ocupa sólo esa mitad).
    static void SetView(Player player, Rect rect)
    {
        player.Camera.rect = rect;
        if (player.Background != null) player.Background.rect = rect;
        if (player.Ui == null || player.Ui.MainCanvas == null) return;
        Canvas canvas = player.Ui.MainCanvas;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = player.Camera;
        canvas.planeDistance = player.Camera.nearClipPlane + 0.05f;

        // Barras del láser y misiles: escaladas para que quepan en media pantalla. La puntuación (arriba,
        // estirada a lo ancho) y los indicadores de objetivo se quedan como están.
        var elements = new List<Transform>();
        foreach (Transform child in canvas.transform)
        {
            if (child.name == "Score" || child.name.Contains("Lead Indicator")) continue;
            if (child.GetComponent<TargetIndicator>() != null || child.GetComponent<CoopHudFit>() != null) continue;
            elements.Add(child);
        }
        CoopHudFit.Wrap(canvas, elements.ToArray());
    }

    // ---------- Naves derribadas ----------

    public Player PlayerOf(ShipController ship)
    {
        if (P1 != null && P1.Ship == ship) return P1;
        if (P2 != null && P2.Ship == ship) return P2;
        return null;
    }

    public Player Partner(Player player) => player == P1 ? P2 : P1;

    public bool AnyAlive => (P1 != null && P1.Alive) || (P2 != null && P2.Alive);

    // Nave nueva junto al compañero (si está vivo) o en el punto de salida, con 3 s de protección.
    public void Respawn(Player player)
    {
        if (player == null || player.Alive) return;
        Player partner = Partner(player);
        Vector3 position;
        Quaternion rotation;
        Vector3 velocity = Vector3.zero;
        if (partner != null && partner.Alive)
        {
            Transform t = partner.Ship.transform;
            float side = player.Number == 1 ? -1f : 1f;
            position = t.position + t.right * (side * 2.5f * unit) - t.forward * (3f * unit);
            rotation = t.rotation;
            if (partner.Body != null) velocity = partner.Body.linearVelocity;
        }
        else
        {
            position = spawnPoint;
            rotation = spawnRotation;
        }

        player.Ship.transform.SetPositionAndRotation(position, rotation);
        player.Ship.gameObject.SetActive(true);   // ShipController.OnEnable repone el casco
        if (player.Body != null)
        {
            player.Body.position = position;
            player.Body.rotation = rotation;
            player.Body.linearVelocity = velocity;
            player.Body.angularVelocity = Vector3.zero;
        }
        if (player.Hull != null) player.Hull.InvulnerableUntil = Time.time + 3f;
        if (player.Keys != null)
        {
            player.Keys.StartCruise();
            player.Keys.ResetAim();
        }
        else ShipThrottle.StartCruise();
    }

    void OnDestroy()
    {
        DesktopMovementControls.MouseViewport = FullView;
    }
}
