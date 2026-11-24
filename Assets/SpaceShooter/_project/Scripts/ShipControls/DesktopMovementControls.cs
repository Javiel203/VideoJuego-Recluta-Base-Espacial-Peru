using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Ratón + teclado, combinados con la voz (ShipVoiceInput) y los controles táctiles (ShipTouchInput).
// Prioridad de la dirección: voz (si está apuntando o girando) → táctil (joystick) → ratón.
[Serializable]
public class DesktopMovementControls : MovementControlsBase
{
    [SerializeField] float _deadZoneRadius = 0.1f;
    float _rollAmount = 0;

    // Parte de la pantalla (normalizada) donde se ve la nave del jugador 1: la pantalla entera en individual,
    // la mitad izquierda en cooperativo (DefensaCoop). El ratón apunta respecto al centro de esa zona.
    public static Rect MouseViewport = new Rect(0f, 0f, 1f, 1f);

    static Vector2 ScreenCenter => new Vector2(
        (MouseViewport.x + MouseViewport.width * 0.5f) * Screen.width,
        (MouseViewport.y + MouseViewport.height * 0.5f) * Screen.height);
    static Vector2 HalfSize => new Vector2(MouseViewport.width * 0.5f * Screen.width, MouseViewport.height * 0.5f * Screen.height);

    public override float YawAmount
    {
        get
        {
            if (ShipVoiceInput.SteerActive) return ShipVoiceInput.Yaw;
            if (ShipTouchInput.Active) return ShipTouchInput.Yaw;
            if (Mouse.current == null) return 0f;   // sin ratón conectado
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            float yaw = Mathf.Clamp((mousePosition.x - ScreenCenter.x) / HalfSize.x, -1f, 1f);
            return Mathf.Abs(yaw) > _deadZoneRadius ? yaw : 0f;
        }
    }

    public override float PitchAmount
    {
        get
        {
            if (ShipVoiceInput.SteerActive) return ShipVoiceInput.Pitch;
            if (ShipTouchInput.Active) return ShipTouchInput.Pitch;
            if (Mouse.current == null) return 0f;
            Vector3 mousePosition = Mouse.current.position.ReadValue();
            float pitch = Mathf.Clamp((mousePosition.y - ScreenCenter.y) / HalfSize.y, -1f, 1f);
            return Mathf.Abs(pitch) > _deadZoneRadius ? pitch * -1: 0f;
        }

    }

    public override float RollAmount
    {
        get
        {
            float roll;
            if (Keyboard.current == null)
            {
                roll = 0f;
            }
            else if (Keyboard.current.qKey.isPressed)
            {
                roll =  1f;
            }
            else
            {
                roll = Keyboard.current.eKey.isPressed ? -1f : 0f;
            }

            _rollAmount = Mathf.Lerp(_rollAmount, roll, Time.deltaTime * 3f);
            if (ShipVoiceInput.SteerActive && Mathf.Approximately(roll, 0f)) return ShipVoiceInput.Roll;
            return _rollAmount;
        }

    }

    // Con crucero: al soltar el acelerador la nave sigue a media potencia hasta retroceder (ShipThrottle).
    public override float ThrustAmount => ShipThrottle.Apply(RawThrust);

    static float RawThrust
    {
        get
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) return 1f;
                if (Keyboard.current.sKey.isPressed) return -1f;
            }
            if (ShipTouchInput.Active && !Mathf.Approximately(ShipTouchInput.Thrust, 0f)) return ShipTouchInput.Thrust;
            return ShipVoiceInput.Thrust;
        }
    }
}
