using UnityEngine;
using UnityEngine.UI;

// Controles táctiles de la MISIÓN FINAL (Defensa espacial). DefensaMission lo añade solo.
//   Izquierda: JOYSTICK (apuntar) y, encima, ACELERAR / FRENAR (mantener).
//   Derecha:   FUEGO (mantener; o un toque corto = disparo continuo, otro toque lo para), MISIL y OBJETIVO (asistencia de apuntado; si ya apunta, pasa al SIGUIENTE).
//   Arriba a la izquierda: SALIR (elegir prueba), porque en el móvil no hay tecla ESC.
// Mover el joystick quita la asistencia de apuntado (igual que mover el ratón en el PC).
public class DefensaTouch : MonoBehaviour
{
    const float HudSpace = 560f;
    const float TapTime = 0.25f;   // un toque más corto que esto en FUEGO activa / desactiva el disparo continuo   // altura de los controles de la izquierda (para subir el indicador de voz)

    Canvas canvas;
    GameObject controls;
    TouchJoystick joystick;
    TouchHoldButton accelerate, brake, fire, missile, target, exit;
    Text targetLabel;
    DefensaVoice assist;

    bool fireWasHeld, autoFire;
    float firePressTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => ShipTouchInput.Reset();

    void Start()
    {
        assist = GetComponent<DefensaVoice>();
        canvas = TouchUI.CreateCanvas("DefensaTouch", transform);
        controls = canvas.gameObject;
        Transform root = canvas.transform;

        joystick = TouchUI.Joystick(root, "Joystick", new Vector2(0f, 0f), new Vector2(60f, 60f), 300f);
        accelerate = TouchUI.HoldButton(root, "Acelerar", "ACELERAR", new Vector2(0f, 0f), new Vector2(40f, 440f), new Vector2(200f, 100f), 22);
        brake = TouchUI.HoldButton(root, "Frenar", "FRENAR", new Vector2(0f, 0f), new Vector2(260f, 440f), new Vector2(160f, 100f), 22);

        fire = TouchUI.HoldButton(root, "Fuego", "FUEGO", new Vector2(1f, 0f), new Vector2(-50f, 60f), new Vector2(260f, 200f), 34);
        missile = TouchUI.HoldButton(root, "Misil", "MISIL", new Vector2(1f, 0f), new Vector2(-340f, 60f), new Vector2(190f, 140f), 26);
        target = TouchUI.HoldButton(root, "Objetivo", "OBJETIVO", new Vector2(1f, 0f), new Vector2(-50f, 290f), new Vector2(260f, 110f), 24);
        targetLabel = target.GetComponentInChildren<Text>();

        exit = TouchUI.HoldButton(root, "Salir", "SALIR", new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(200f, 80f), 22);

        TouchMode.Changed += Show;
        Show(TouchMode.Active);
    }

    void OnDestroy()
    {
        TouchMode.Changed -= Show;
        ShipTouchInput.Reset();
        VoiceCommands.HudBottomOffset = 0f;
    }

    void Show(bool visible)
    {
        if (controls != null) controls.SetActive(visible);
        VoiceCommands.HudBottomOffset = visible ? HudSpace : 0f;
        if (!visible) ShipTouchInput.Reset();
    }

    void Update()
    {
        ShipTouchInput.Active = TouchMode.Active;
        if (controls == null || !controls.activeSelf) return;

        // En la sesión informativa y en las pantallas finales el tiempo está parado: sin controles.
        bool playing = Time.timeScale > 0f && !DefensaLaunch.InProgress;   // sin controles durante el despegue
        foreach (Transform child in canvas.transform)
            if (child.gameObject.activeSelf != playing) child.gameObject.SetActive(playing);
        if (!playing)
        {
            ShipTouchInput.Yaw = ShipTouchInput.Pitch = ShipTouchInput.Thrust = 0f;
            ShipTouchInput.Primary = ShipTouchInput.Secondary = false;
            autoFire = fireWasHeld = false;
            return;
        }

        // Apuntar: mismo convenio que el ratón (empujar arriba = morro arriba).
        Vector2 stick = joystick.Value;
        if (joystick.IsHeld && stick.sqrMagnitude > 0.04f)
            ShipVoiceInput.SteerActive = false;   // el joystick manda: se quita la asistencia de apuntado
        ShipTouchInput.Yaw = stick.x;
        ShipTouchInput.Pitch = -stick.y;

        ShipTouchInput.Thrust = accelerate.IsHeld ? 1f : brake.IsHeld ? -1f : 0f;
        ShipTouchInput.Primary = UpdateFire();
        ShipTouchInput.Secondary = missile.IsHeld;

        if (target.ConsumePress() && assist != null)
            assist.AimAtTarget(assist.AutoAiming);   // primer toque: apunta; si ya apuntaba, siguiente enemigo
        if (targetLabel != null)
            targetLabel.text = assist != null && assist.AutoAiming ? "SIGUIENTE" : "OBJETIVO";

        if (exit.ConsumePress()) TrialProgress.GoToSelection();
    }

    // FUEGO: dispara mientras se mantiene. Un toque corto activa el disparo continuo (para poder
    // apuntar con un solo dedo); otro toque corto lo detiene.
    bool UpdateFire()
    {
        bool held = fire.IsHeld;
        if (held && !fireWasHeld) firePressTime = Time.unscaledTime;
        if (!held && fireWasHeld && Time.unscaledTime - firePressTime < TapTime) autoFire = !autoFire;
        fireWasHeld = held;

        if (fire.Label != null) fire.Label.text = autoFire ? "DISPARANDO" : "FUEGO";
        return held || autoFire;
    }
}
