// Órdenes de voz para la nave del jugador en la Defensa espacial (las escribe DefensaVoice).
// DesktopMovementControls y DesktopWeaponControls las combinan con el ratón y el teclado:
//  - Dirección: manda la voz mientras SteerActive; si el jugador mueve el ratón, vuelve el ratón.
//  - Empuje: W/S tienen prioridad; si no se pulsan, vale el de la voz.
//  - Armas: disparan con el clic O con la voz.
public static class ShipVoiceInput
{
    public static bool SteerActive;
    public static float Yaw, Pitch, Roll, Thrust;
    public static bool Primary, Secondary;

    public static void Reset()
    {
        SteerActive = false;
        Yaw = Pitch = Roll = Thrust = 0f;
        Primary = Secondary = false;
    }
}
