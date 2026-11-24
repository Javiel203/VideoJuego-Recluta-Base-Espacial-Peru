using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla "Cómo jugar" de la PRUEBA DE DESTREZA: controles de teclado y órdenes de voz.
// Continuar → primer nivel (Under). Volver → elegir prueba (CharacterSelect).
public class Instructions : MonoBehaviour
{
    [SerializeField] Text voiceText;
    [SerializeField] string nextSceneName = "Under";
    [SerializeField] string menuSceneName = "CharacterSelect";

    void Start()
    {
        // Las órdenes de voz se leen de VoiceCommands para que siempre coincidan.
        if (voiceText != null)
            voiceText.text = string.Join("\n", VoiceCommands.HelpLines);

        // Misiones según la dificultad: Fácil sólo Under, Intermedio Under + Through, Avanzado las tres.
        GameObject route = GameObject.Find("Route");
        if (route != null && route.TryGetComponent(out Text routeText))
            routeText.text = $"MISIONES:  {Difficulty.DestrezaRoute(Difficulty.Current)}     ·     NIVEL {Difficulty.CurrentName}";
    }

    // Despegar: la prueba empieza con todas las oportunidades.
    public void Continue()
    {
        TrialProgress.ResetDestrezaLives();
        SceneManager.LoadScene(nextSceneName);
    }
    public void Back() => SceneManager.LoadScene(menuSceneName);
}
