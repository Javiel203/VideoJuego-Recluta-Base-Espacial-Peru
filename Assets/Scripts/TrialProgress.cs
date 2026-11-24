using UnityEngine;
using UnityEngine.SceneManagement;

// Las pruebas del reclutamiento y qué escenas usan.
//  - Destreza (nave) y Memoria (cristales): las dos pruebas iniciales.
//  - Defensa espacial (shooter 3D): misión final, se desbloquea al superar las dos iniciales.
// Las pruebas superadas se guardan con PlayerPrefs, por separado para cada dificultad (Difficulty.cs);
// "Reiniciar juego" borra el progreso de la dificultad actual.
public static class TrialProgress
{
    public const string MenuScene = "MainMenu";
    public const string SelectScene = "CharacterSelect";          // aquí se elige la prueba
    public const string DestrezaScene = "Instructions";           // entrada a la prueba de destreza
    public const string FirstLevelScene = "Under";
    // Último nivel de la nave según la dificultad: Under (Fácil), Through (Intermedio), Over (Avanzado).
    public static string FinalLevelScene => Difficulty.DestrezaFinalScene(Difficulty.Current);
    public const string MemoriaScene = "PruebaMemoriaEspacial";
    public const string DefensaScene = "DefensaEspacial";         // misión final (shooter 3D)

    const string DestrezaKey = "Trial_Destreza";
    const string MemoriaKey = "Trial_Memoria";
    const string DefensaKey = "Trial_Defensa";

    // Clave con la dificultad: "Trial_Destreza_0" (Fácil), "_1" (Intermedio), "_2" (Avanzado).
    static string K(string key) => K(key, Difficulty.Current);
    static string K(string key, DifficultyLevel d) => key + "_" + (int)d;

    public static bool DestrezaCompleted => PlayerPrefs.GetInt(K(DestrezaKey), 0) == 1;
    public static bool MemoriaCompleted => PlayerPrefs.GetInt(K(MemoriaKey), 0) == 1;
    public static bool DefensaCompleted => PlayerPrefs.GetInt(K(DefensaKey), 0) == 1;

    // Las dos pruebas iniciales: desbloquean la misión final.
    public static bool InitialTrialsCompleted => DestrezaCompleted && MemoriaCompleted;
    public static bool DefensaUnlocked => InitialTrialsCompleted;
    public static bool AllCompleted => InitialTrialsCompleted && DefensaCompleted;

    public static void CompleteDestreza() => Save(DestrezaKey);
    public static void CompleteMemoria() => Save(MemoriaKey);
    public static void CompleteDefensa() => Save(DefensaKey);

    static void Save(string key)
    {
        PlayerPrefs.SetInt(K(key), 1);
        PlayerPrefs.Save();
    }

    // ---------- Oportunidades de la prueba de destreza (para Under + Through + Over) ----------
    public const int DestrezaMaxLives = 3;
    public static int DestrezaLives { get; private set; } = DestrezaMaxLives;

    public static void ResetDestrezaLives() => DestrezaLives = DestrezaMaxLives;

    // Resta una oportunidad y devuelve las que quedan.
    public static int LoseDestrezaLife()
    {
        if (DestrezaLives > 0) DestrezaLives--;
        return DestrezaLives;
    }

    // ---------- Misión final: Defensa espacial ----------
    public const int DefensaMaxLives = 3;
    public static int DefensaKillsToWin => Difficulty.DefensaKillsToWin(Difficulty.Current);
    public static int DefensaLives { get; private set; } = DefensaMaxLives;
    public static int DefensaKills { get; private set; }
    // true sólo al entrar a la misión (muestra la sesión informativa); false al recargar tras perder una nave.
    public static bool DefensaFreshStart { get; private set; } = true;

    public static int LoseDefensaLife()
    {
        if (DefensaLives > 0) DefensaLives--;
        DefensaFreshStart = false;
        return DefensaLives;
    }

    public static int AddDefensaKill() => ++DefensaKills;

    public static void StartDefensa()
    {
        DefensaLives = DefensaMaxLives;
        DefensaKills = 0;
        DefensaFreshStart = true;
        SceneManager.LoadScene(DefensaScene);
    }

    public static void Reset()
    {
        PlayerPrefs.DeleteKey(K(DestrezaKey));
        PlayerPrefs.DeleteKey(K(MemoriaKey));
        PlayerPrefs.DeleteKey(K(DefensaKey));
        PlayerPrefs.Save();
        ResetDestrezaLives();
        DefensaLives = DefensaMaxLives;
        DefensaKills = 0;
        DefensaFreshStart = true;
    }

    // Empieza la prueba de destreza desde el primer nivel con todas las oportunidades.
    public static void StartDestrezaLevels()
    {
        ResetDestrezaLives();
        SceneManager.LoadScene(FirstLevelScene);
    }

    public static void GoToSelection() => SceneManager.LoadScene(SelectScene);
    public static void GoToDestreza() => SceneManager.LoadScene(DestrezaScene);
    public static void GoToMemoria() => SceneManager.LoadScene(MemoriaScene);
    public static void GoToDefensa() => StartDefensa();

    public static void RestartGame()
    {
        Reset();
        SceneManager.LoadScene(MenuScene);
    }
}
