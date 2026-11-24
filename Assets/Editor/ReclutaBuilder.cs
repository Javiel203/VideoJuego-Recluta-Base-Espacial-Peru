using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Deja todo el juego listo de una vez:
//  - Nombre del producto "RECLUTA - Base Espacial del Perú"
//  - Bandera, emblema y sprite de botón (Assets/Resources/Peru)
//  - Escenas de menú con el estilo peruano: MainMenu, Historia, CharacterSelect, Instructions
//  - Escena 3D de la prueba de memoria: PruebaMemoriaEspacial
//  - Orden de Build Settings: MainMenu, Historia, CharacterSelect, Instructions, Under, Through, Over,
//    PruebaMemoriaEspacial, DefensaEspacial (misión final: shooter 3D)
//  - Permiso de micrófono para las órdenes de voz
// Uso: Tools > RECLUTA > Reconstruir todo
public static class ReclutaBuilder
{
    const string UnderPath = "Assets/Scenes/Under.unity";
    const string ThroughPath = "Assets/Scenes/Through.unity";
    const string OverPath = "Assets/Scenes/Over.unity";
    const string DefensaPath = "Assets/Scenes/DefensaEspacial.unity";

    static bool batch;

    [MenuItem("Tools/RECLUTA/Reconstruir todo (recomendado)", priority = 0)]
    public static void RebuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        if (!EditorUtility.DisplayDialog("RECLUTA",
                "Se van a regenerar las escenas MainMenu, Historia, CharacterSelect, Instructions y " +
                "PruebaMemoriaEspacial " +
                "con el nuevo estilo. Los niveles (Under, Through, Over) no se modifican.\n\n¿Continuar?",
                "Reconstruir", "Cancelar"))
            return;

        batch = true;
        try
        {
            PlayerSettings.productName = PeruTheme.ProductName;
            PeruAssets.Ensure(force: true);

            MainMenuBuilder.Build();
            StoryBuilder.Build();
            InstructionsBuilder.Build();
            CharacterSelectBuilder.Build();
            PruebaMemoriaBuilder.Build();
            PortanavesBuilder.Build();   // nave nodriza de la misión final

            SetupGameFlow();
            EditorSceneManager.OpenScene(MainMenuBuilder.ScenePath);
            Debug.Log("RECLUTA listo: MainMenu → Historia → CharacterSelect → (Destreza: Instructions → Under → Through → Over) " +
                      "| (Memoria: PruebaMemoriaEspacial)");
        }
        finally
        {
            batch = false;
        }
    }

    // Pregunta antes de reemplazar una escena (no pregunta durante "Reconstruir todo").
    internal static bool ConfirmReplace(string scenePath)
    {
        if (batch) return true;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        return !File.Exists(scenePath) ||
               EditorUtility.DisplayDialog("RECLUTA", $"{scenePath} ya existe. ¿Quieres reemplazarla?",
                   "Reemplazar", "Cancelar");
    }

    // Orden de Build Settings + destinos de los botones + permiso de micrófono.
    [MenuItem("Tools/RECLUTA/Solo ajustar orden de escenas", priority = 20)]
    public static void SetupGameFlow()
    {
        // Under → Through → Over deben ir seguidos (los niveles avanzan por índice).
        // La prueba de memoria va al final: Over se reconoce como último nivel por su nombre.
        MainMenuBuilder.SetBuildSceneOrder(MainMenuBuilder.ScenePath, StoryBuilder.ScenePath,
            CharacterSelectBuilder.ScenePath, InstructionsBuilder.ScenePath, UnderPath, ThroughPath, OverPath,
            PruebaMemoriaBuilder.ScenePath, DefensaPath);

        SetSceneString<MainMenu>(MainMenuBuilder.ScenePath, "gameSceneName", StoryBuilder.SceneName);

        if (string.IsNullOrEmpty(PlayerSettings.iOS.microphoneUsageDescription))
            PlayerSettings.iOS.microphoneUsageDescription = "El juego usa el micrófono para las órdenes de voz.";
        PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.Microphone, true);
        AssetDatabase.SaveAssets();
    }

    // Para línea de comandos (-executeMethod): regenera la pantalla de selección y el orden de escenas.
    public static void BatchRebuildCharacterSelect()
    {
        CharacterSelectBuilder.Build();
        SetupGameFlow();
    }

    static void SetSceneString<T>(string path, string property, string value) where T : Component
    {
        if (!File.Exists(path)) return;
        Scene s = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        bool found = false;
        foreach (GameObject go in s.GetRootGameObjects())
            foreach (T c in go.GetComponentsInChildren<T>(true))
            {
                MainMenuBuilder.SetString(c, property, value);
                found = true;
            }
        if (found)
        {
            EditorSceneManager.MarkSceneDirty(s);
            EditorSceneManager.SaveScene(s);
        }
        if (SceneManager.sceneCount > 1)
            EditorSceneManager.CloseScene(s, true);
    }
}
