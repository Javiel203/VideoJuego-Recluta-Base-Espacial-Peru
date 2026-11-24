using UnityEngine;
using UnityEngine.InputSystem;

// Voz en la MISIÓN FINAL (Defensa espacial). DefensaMission lo añade solo.
// Apuntar con la voz a una nave que se mueve es casi imposible, así que hay ASISTENCIA DE APUNTADO:
//   "objetivo"  → la nave gira sola hacia el enemigo más cercano y lo sigue (si cae, pasa al siguiente).
//                 Con teclado: T (otra vez T = siguiente enemigo).
//   "siguiente" → cambia al siguiente enemigo.
//   "fuego"     → ráfaga de láser de unos segundos.   "misil" → lanza un misil al objetivo fijado.
//   "acelera" → empuje al 100 % unos segundos y luego sigue a media potencia (crucero, ShipThrottle).
//   "frena" / "atrás" → retrocede un momento y apaga el crucero.
//   "izquierda" / "derecha" / "arriba" / "abajo" → giro manual corto (desactiva el apuntado).
//   "recto" → deja de girar.   "alto" → para todo (empuje, giro, disparo y apuntado).
// El ratón y el teclado siguen funcionando: mover el ratón devuelve el control de dirección al ratón.
public class DefensaVoice : MonoBehaviour
{
    [Header("Giro manual")]
    [SerializeField] float turnDuration = 0.7f;
    [SerializeField] float turnStrength = 0.8f;

    [Header("Empuje")]
    [Tooltip("Segundos a plena potencia tras decir \"acelera\" (después sigue a media potencia).")]
    [SerializeField] float accelerateDuration = 1.5f;
    [Tooltip("Segundos de retroceso tras decir \"frena\" o \"atrás\" (después se queda parada).")]
    [SerializeField] float brakeDuration = 0.8f;

    [Header("Disparo")]
    [SerializeField] float fireDuration = 2.5f;
    [SerializeField] float missileHold = 0.3f;

    [Header("Asistencia de apuntado")]
    [Tooltip("Grados de error para girar al máximo: más bajo = gira más fuerte.")]
    [SerializeField] float aimFullTurnAngle = 20f;
    [Tooltip("Frena el giro para no pasarse del objetivo.")]
    [SerializeField] float aimDamping = 0.6f;
    [SerializeField] float retargetInterval = 0.5f;

    VoiceContext context;
    Transform player;
    Rigidbody playerBody;

    bool autoAim;
    Transform target;
    float nextRetarget;

    float manualYaw, manualPitch, manualUntil;
    float fireUntil, missileUntil;
    float thrustValue, thrustUntil;
    float mouseTravel;                       // píxeles que se movió el ratón desde la última orden
    const float MouseReleaseTravel = 60f;    // con más movimiento, el ratón vuelve a mandar

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        ShipVoiceInput.Reset();
        ShipThrottle.Reset();
    }

    void Awake()
    {
        ShipVoiceInput.Reset();
        ShipThrottle.Reset();   // cada nave nueva empieza parada
        context = new VoiceContext(
            "VOZ:  OBJETIVO (apunta solo)  ·  SIGUIENTE  ·  FUEGO  ·  MISIL  ·  ACELERA (luego sigue a media velocidad)  ·  " +
            "FRENA o ATRÁS (se detiene)  ·  " +
            "IZQUIERDA  ·  DERECHA  ·  ARRIBA  ·  ABAJO  ·  RECTO  ·  ALTO")
            .On(() => SetThrust(1f, accelerateDuration), "acelera", "adelante", "avanza")
            .On(() => SetThrust(-1f, brakeDuration), "frena", "despacio", "atrás", "reversa", "retrocede")
            .On(() => Turn(-1f, 0f), "izquierda")
            .On(() => Turn(1f, 0f), "derecha")
            .On(() => Turn(0f, -1f), "arriba", "sube")
            .On(() => Turn(0f, 1f), "abajo", "baja")
            .On(Straight, "recto", "centro")
            .On(() => AimAtTarget(false), "objetivo", "apunta", "enemigo")
            .On(() => AimAtTarget(true), "siguiente", "otro")
            .On(() => { if (Playing) fireUntil = Time.time + fireDuration; }, "fuego", "dispara", "disparo")
            .On(() => { if (Playing) missileUntil = Time.time + missileHold; }, "misil", "misiles")
            .On(StopAll, "alto", "para");
    }

    void OnEnable() => VoiceCommands.Push(context);

    void OnDisable()
    {
        VoiceCommands.Pop(context);
        ShipVoiceInput.Reset();
    }

    void Start()
    {
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go == null) return;
        player = go.transform;
        playerBody = go.GetComponent<Rigidbody>();
    }

    // En la sesión informativa y en las pantallas finales el tiempo está parado.
    static bool Playing => Time.timeScale > 0f && !DefensaLaunch.InProgress;   // en el despegue manda la animación

    // ---------- Órdenes ----------

    // Pulso de empuje: acelerar (luego crucero a media potencia) o retroceder (apaga el crucero).
    void SetThrust(float value, float duration)
    {
        if (!Playing) return;
        thrustValue = value;
        thrustUntil = Time.time + duration;
    }

    void Turn(float yaw, float pitch)
    {
        if (!Playing) return;
        mouseTravel = 0f;
        autoAim = false;
        manualYaw = yaw * turnStrength;
        manualPitch = pitch * turnStrength;
        manualUntil = Time.time + turnDuration;
        ShipVoiceInput.SteerActive = true;
    }

    void Straight()
    {
        autoAim = false;
        manualUntil = 0f;
    }

    // true mientras la asistencia de apuntado está siguiendo a un enemigo.
    public bool AutoAiming => autoAim && ShipVoiceInput.SteerActive;

    // Asistencia de apuntado (voz "objetivo" / "siguiente" y botón táctil OBJETIVO).
    public void AimAtTarget(bool next)
    {
        if (!Playing) return;
        mouseTravel = 0f;
        autoAim = true;
        manualUntil = 0f;
        ShipVoiceInput.SteerActive = true;
        target = FindTarget(next ? target : null);
    }

    void StopAll()
    {
        autoAim = false;
        manualUntil = 0f;
        fireUntil = missileUntil = 0f;
        thrustUntil = 0f;
        ShipVoiceInput.Thrust = 0f;
        ShipThrottle.Reset();   // "alto": también apaga el crucero
    }

    // ---------- Cada frame ----------

    void Update()
    {
        if (player == null || !player.gameObject.activeInHierarchy || !Playing)
        {
            ShipVoiceInput.Primary = ShipVoiceInput.Secondary = false;
            ShipVoiceInput.Thrust = 0f;
            return;
        }

        // Teclado: T apunta solo a la nave enemiga más cercana; otra vez T pasa a la siguiente.
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            AimAtTarget(AutoAiming);

        ReleaseSteeringIfMouseMoves();

        ShipVoiceInput.Thrust = Time.time < thrustUntil ? thrustValue : 0f;
        ShipVoiceInput.Primary = Time.time < fireUntil;
        ShipVoiceInput.Secondary = Time.time < missileUntil;

        if (!ShipVoiceInput.SteerActive) return;

        if (autoAim)
        {
            // Objetivo derribado: se busca otro (cada medio segundo, por si aún no hay ninguno).
            if ((target == null || !target.gameObject.activeInHierarchy) && Time.time >= nextRetarget)
            {
                nextRetarget = Time.time + retargetInterval;
                target = FindTarget(null);
            }
            SteerTowards(target != null && target.gameObject.activeInHierarchy ? target : null);
        }
        else if (Time.time < manualUntil)
        {
            ShipVoiceInput.Yaw = manualYaw;
            ShipVoiceInput.Pitch = manualPitch;
            ShipVoiceInput.Roll = 0f;
        }
        else
        {
            // Sin orden de giro: amortigua el giro que quede para que la nave no siga rodando.
            Vector3 spin = LocalAngularVelocity();
            ShipVoiceInput.Yaw = Mathf.Clamp(-spin.y * aimDamping, -1f, 1f);
            ShipVoiceInput.Pitch = Mathf.Clamp(-spin.x * aimDamping, -1f, 1f);
            ShipVoiceInput.Roll = Mathf.Clamp(-spin.z * aimDamping, -1f, 1f);
        }
    }

    void ReleaseSteeringIfMouseMoves()
    {
        // En el móvil los toques también mueven el "ratón": ahí sólo cuenta el joystick (DefensaTouch).
        if (!ShipVoiceInput.SteerActive || Mouse.current == null || TouchMode.Active) return;
        // Hace falta mover el ratón de verdad (no un temblor de la mano) para recuperar el control.
        mouseTravel += Mouse.current.delta.ReadValue().magnitude;
        if (mouseTravel > MouseReleaseTravel)
        {
            ShipVoiceInput.SteerActive = false;
            autoAim = false;
            manualUntil = 0f;
        }
    }

    void SteerTowards(Transform t)
    {
        if (t == null)
        {
            ShipVoiceInput.Yaw = ShipVoiceInput.Pitch = ShipVoiceInput.Roll = 0f;
            return;
        }

        Vector3 local = Quaternion.Inverse(player.rotation) * (t.position - player.position);
        float yawError = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;                                 // + = derecha
        float pitchError = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg; // + = arriba
        Vector3 spin = LocalAngularVelocity();

        // Mismo convenio que el ratón: Pitch negativo = morro arriba.
        ShipVoiceInput.Yaw = Mathf.Clamp(yawError / aimFullTurnAngle - spin.y * aimDamping, -1f, 1f);
        ShipVoiceInput.Pitch = Mathf.Clamp(-pitchError / aimFullTurnAngle - spin.x * aimDamping, -1f, 1f);
        ShipVoiceInput.Roll = Mathf.Clamp(-spin.z * aimDamping, -1f, 1f);
    }

    Vector3 LocalAngularVelocity() =>
        playerBody != null ? Quaternion.Inverse(player.rotation) * playerBody.angularVelocity : Vector3.zero;

    // Enemigo activo más cercano (si 'exclude' no es null, el más cercano que no sea ese).
    Transform FindTarget(Transform exclude)
    {
        if (player == null) return null;
        Transform best = null;
        float bestDistance = float.MaxValue;
        foreach (EnemyShipController enemy in FindObjectsByType<EnemyShipController>(FindObjectsSortMode.None))
        {
            if (!enemy.gameObject.activeInHierarchy || enemy.transform == exclude) continue;
            float d = (enemy.transform.position - player.position).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = enemy.transform;
            }
        }
        return best != null ? best : exclude;   // si sólo queda uno, se sigue con él
    }
}
