using UnityEngine;
using UnityEngine.InputSystem;

// Controles SOLO DE TECLADO del jugador 2 en la Defensa espacial cooperativa (pantalla dividida).
// Están en la parte derecha del teclado para no chocar con los del jugador 1 (ratón + W/S/Q/E):
//   Flechas       mueven la MIRA; la nariz de la nave gira hacia ella (igual que el ratón del jugador 1:
//                 cuanto más lejos del centro, más rápido gira). La mira se queda donde se deja.
//   Espacio       apunta solo a la nave enemiga más cercana y la sigue (otra vez: la siguiente).
//                 Tocar una flecha devuelve el control manual.
//   I / K         acelerar / frenar (como W/S: al soltar I sigue a media velocidad)
//   J / L         rodar a la izquierda / derecha
//   U o Enter     láser (mantener) · O o Shift der. misil (CoopKeyboardWeapons)
//   8 / 9 / 0     cámara (cabina, seguimiento, enemigo) — la cambia CameraManager
public class CoopKeyboardMovement : MovementControlsBase
{
    public const string Help =
        "FLECHAS  —  mover la mira (la nave gira hacia ella)\n" +
        "ESPACIO  —  apuntar solo al enemigo (otra vez: siguiente)\n" +
        "I  —  acelerar · K  —  frenar\n" +
        "J / L  —  rodar a la izquierda / derecha";

    public const float CruiseFactor = 0.5f;   // igual que ShipThrottle (jugador 1)

    [Tooltip("Velocidad de la mira con las flechas (fracción de media pantalla por segundo).")]
    [SerializeField] float aimSpeed = 1.4f;
    [Tooltip("Zona muerta en el centro, como la del ratón del jugador 1.")]
    [SerializeField] float deadZone = 0.1f;

    [Header("Asistencia de apuntado (Espacio) — mismos valores que la tecla T del jugador 1")]
    [SerializeField] float aimFullTurnAngle = 20f;
    [SerializeField] float aimDamping = 0.6f;
    [SerializeField] float retargetInterval = 0.5f;

    Transform ship;
    Rigidbody body;
    Vector2 aim;              // mira: -1..1 en cada eje respecto al centro de la mitad derecha
    float roll;
    bool cruising;
    Transform target;
    float nextRetarget;
    float yaw, pitch, autoRoll;
    int lastFrame = -1;

    static Keyboard Kb => Keyboard.current;

    // Para la mira en pantalla (CoopAimReticle).
    public Vector2 AimPoint => aim;
    public bool AutoAiming { get; private set; }
    public Transform Target => AutoAiming ? target : null;

    public void Init(Transform shipTransform, Rigidbody shipBody)
    {
        ship = shipTransform;
        body = shipBody;
    }

    // Nave nueva: mira al centro y sin asistencia.
    public void ResetAim()
    {
        aim = Vector2.zero;
        AutoAiming = false;
        target = null;
    }

    void Update() => Step();

    // Se calcula una vez por frame (lo piden ShipController y la animación de la cabina).
    void Step()
    {
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;

        Vector2 arrows = Vector2.zero;
        if (Kb != null)
        {
            arrows.x = (Kb.rightArrowKey.isPressed ? 1f : 0f) - (Kb.leftArrowKey.isPressed ? 1f : 0f);
            arrows.y = (Kb.upArrowKey.isPressed ? 1f : 0f) - (Kb.downArrowKey.isPressed ? 1f : 0f);
            if (Kb.spaceKey.wasPressedThisFrame && Time.timeScale > 0f && !DefensaLaunch.InProgress)
                AimAtTarget(AutoAiming);
        }

        if (arrows != Vector2.zero)
        {
            if (AutoAiming)
            {
                // Como mover el ratón: se deja la asistencia y la mira vuelve al centro para no dar un tirón.
                AutoAiming = false;
                target = null;
                aim = Vector2.zero;
            }
            aim += arrows * (aimSpeed * Time.deltaTime);   // en pausa no se mueve
            aim.x = Mathf.Clamp(aim.x, -1f, 1f);
            aim.y = Mathf.Clamp(aim.y, -1f, 1f);
        }

        if (AutoAiming)
        {
            if ((target == null || !target.gameObject.activeInHierarchy) && Time.time >= nextRetarget)
            {
                nextRetarget = Time.time + retargetInterval;
                target = FindTarget(null);
            }
            SteerTowards(target != null && target.gameObject.activeInHierarchy ? target : null);
            return;
        }

        // Igual que el ratón del jugador 1: la mira fuera de la zona muerta hace girar la nave.
        yaw = Mathf.Abs(aim.x) > deadZone ? aim.x : 0f;
        pitch = Mathf.Abs(aim.y) > deadZone ? -aim.y : 0f;   // pitch negativo = morro arriba
    }

    public void AimAtTarget(bool next)
    {
        AutoAiming = true;
        target = FindTarget(next ? target : null);
        nextRetarget = Time.time + retargetInterval;
    }

    void SteerTowards(Transform t)
    {
        if (t == null || ship == null)
        {
            yaw = pitch = autoRoll = 0f;
            return;
        }
        Vector3 local = Quaternion.Inverse(ship.rotation) * (t.position - ship.position);
        float yawError = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;                                 // + = derecha
        float pitchError = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg; // + = arriba
        Vector3 spin = body != null ? Quaternion.Inverse(ship.rotation) * body.angularVelocity : Vector3.zero;
        yaw = Mathf.Clamp(yawError / aimFullTurnAngle - spin.y * aimDamping, -1f, 1f);
        pitch = Mathf.Clamp(-pitchError / aimFullTurnAngle - spin.x * aimDamping, -1f, 1f);
        autoRoll = Mathf.Clamp(-spin.z * aimDamping, -1f, 1f);
    }

    // Enemigo activo más cercano a la nave 2 (si 'exclude' no es null, el más cercano que no sea ese).
    Transform FindTarget(Transform exclude)
    {
        if (ship == null) return null;
        Transform best = null;
        float bestDistance = float.MaxValue;
        foreach (EnemyShipController enemy in FindObjectsByType<EnemyShipController>(FindObjectsSortMode.None))
        {
            if (!enemy.gameObject.activeInHierarchy || enemy.transform == exclude) continue;
            float d = (enemy.transform.position - ship.position).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = enemy.transform;
            }
        }
        return best != null ? best : exclude;   // si sólo queda uno, se sigue con él
    }

    public override float YawAmount { get { Step(); return yaw; } }

    public override float PitchAmount { get { Step(); return pitch; } }

    public override float RollAmount
    {
        get
        {
            Step();
            float input = 0f;
            if (Kb != null)
            {
                if (Kb.jKey.isPressed) input = 1f;
                else if (Kb.lKey.isPressed) input = -1f;
            }
            roll = Mathf.Lerp(roll, input, Time.deltaTime * 3f);
            // Con la asistencia y sin J/L, amortigua el giro sobre sí misma (como la tecla T).
            if (AutoAiming && Mathf.Approximately(input, 0f)) return autoRoll;
            return roll;
        }
    }

    // Crucero propio (el del jugador 1 es estático en ShipThrottle).
    public override float ThrustAmount
    {
        get
        {
            float raw = 0f;
            if (Kb != null)
            {
                if (Kb.iKey.isPressed) raw = 1f;
                else if (Kb.kKey.isPressed) raw = -1f;
            }
            if (raw > 0.01f) { cruising = true; return raw; }
            if (raw < -0.01f) { cruising = false; return raw; }
            return cruising ? CruiseFactor : 0f;
        }
    }

    public void StartCruise() => cruising = true;
    public void ResetCruise() => cruising = false;
}
