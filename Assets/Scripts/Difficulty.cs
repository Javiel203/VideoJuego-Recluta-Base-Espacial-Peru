using UnityEngine;

public enum DifficultyLevel
{
    Facil = 0,
    Intermedio = 1,
    Avanzado = 2
}

// Dificultad elegida en CharacterSelect (se guarda con PlayerPrefs) y reglas de cada prueba.
// Para ajustar el juego basta con cambiar los números de esta tabla.
public static class Difficulty
{
    const string Key = "Difficulty";
    public const DifficultyLevel Default = DifficultyLevel.Facil;

    public static DifficultyLevel Current
    {
        get
        {
            int v = PlayerPrefs.GetInt(Key, (int)Default);
            return v >= 0 && v <= 2 ? (DifficultyLevel)v : Default;
        }
        set
        {
            PlayerPrefs.SetInt(Key, (int)value);
            PlayerPrefs.Save();
        }
    }

    public static string Name(DifficultyLevel d) =>
        d == DifficultyLevel.Facil ? "FÁCIL" : d == DifficultyLevel.Intermedio ? "INTERMEDIO" : "AVANZADO";

    public static string CurrentName => Name(Current);

    // ---------- Prueba de memoria ----------
    // Niveles a superar.
    public static int MemoryLevels(DifficultyLevel d) => d == DifficultyLevel.Facil ? 3 : d == DifficultyLevel.Intermedio ? 6 : 10;
    // La velocidad de los destellos sigue siempre la escala de 10 niveles (en Fácil el nivel 3 sigue siendo lento).
    public const int MemorySpeedScaleLevels = 10;

    // ---------- Prueba de destreza ----------
    // Niveles de la nave que hay que pasar (en orden: Under, Through, Over).
    public static readonly string[] DestrezaLevelScenes = { "Under", "Through", "Over" };
    public static int DestrezaLevelCount(DifficultyLevel d) => d == DifficultyLevel.Facil ? 1 : d == DifficultyLevel.Intermedio ? 2 : 3;
    public static string DestrezaFinalScene(DifficultyLevel d) => DestrezaLevelScenes[DestrezaLevelCount(d) - 1];

    // ---------- Misión final: Defensa espacial ----------
    public static int DefensaKillsToWin(DifficultyLevel d) => d == DifficultyLevel.Facil ? 2 : d == DifficultyLevel.Intermedio ? 3 : 5;
    // Multiplicador del daño que recibe la nave del jugador.
    public static float DefensaDamageTaken(DifficultyLevel d) => d == DifficultyLevel.Facil ? 0.05f : d == DifficultyLevel.Intermedio ? 0.5f : 1f;

    // Lista legible de las misiones de destreza: "UNDER  -  THROUGH".
    public static string DestrezaRoute(DifficultyLevel d)
    {
        int n = DestrezaLevelCount(d);
        string route = "";
        for (int i = 0; i < n; i++)
            route += (i > 0 ? "  -  " : "") + DestrezaLevelScenes[i].ToUpperInvariant();
        return route;
    }
}
