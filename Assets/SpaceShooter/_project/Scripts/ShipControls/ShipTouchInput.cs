// Controles táctiles de la nave del jugador en la Defensa espacial (los escribe DefensaTouch).
// Mientras Active es true (modo táctil), el ratón se ignora: en el móvil los toques también
// llegan como "ratón" y harían girar o disparar la nave sin querer.
public static class ShipTouchInput
{
    public static bool Active;
    public static float Yaw, Pitch, Thrust;
    public static bool Primary, Secondary;

    public static void Reset()
    {
        Active = false;
        Yaw = Pitch = Thrust = 0f;
        Primary = Secondary = false;
    }
}
