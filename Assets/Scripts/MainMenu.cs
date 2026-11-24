using UnityEngine;
using UnityEngine.SceneManagement;

// Lógica de los botones del menú principal (portada).
public class MainMenu : MonoBehaviour
{
    [Tooltip("Nombre de la escena que se carga al pulsar Play (debe estar en Build Settings).")]
    [SerializeField] string gameSceneName = "Historia";

    public void PlayGame()
    {
        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError($"La escena '{gameSceneName}' no está en Build Settings.");
            return;
        }
        SceneManager.LoadScene(gameSceneName);
    }

    public void ExitGame()
    {
        Debug.Log("Quitting the application");
#if UNITY_EDITOR
        // Application.Quit() no hace nada dentro del Editor: detenemos el Play Mode.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
