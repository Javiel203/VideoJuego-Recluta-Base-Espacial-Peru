// Acelerador de crucero de la nave del jugador (Defensa espacial):
//  - Mientras se acelera (W, botón ACELERAR o voz "acelera") la nave empuja al 100 %.
//  - Al soltar, sigue empujando a MEDIA potencia (crucero).
//  - Sólo al retroceder (S, botón FRENAR o voz "frena" / "atrás") se apaga el crucero.
public static class ShipThrottle
{
    public const float CruiseFactor = 0.5f;

    public static bool Cruising { get; private set; }

    // raw > 0 acelerar, raw < 0 retroceder, 0 = nada pulsado. Devuelve el empuje final.
    public static float Apply(float raw)
    {
        if (raw > 0.01f)
        {
            Cruising = true;
            return raw;
        }
        if (raw < -0.01f)
        {
            Cruising = false;
            return raw;
        }
        return Cruising ? CruiseFactor : 0f;
    }

    public static void Reset() => Cruising = false;

    // La nave sale del hangar ya en crucero (DefensaLaunch).
    public static void StartCruise() => Cruising = true;
}
