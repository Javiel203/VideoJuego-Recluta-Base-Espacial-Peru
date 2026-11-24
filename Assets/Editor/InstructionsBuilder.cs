using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Genera la escena Instructions (Prueba de destreza: cómo jugar con teclado y órdenes de voz).
// Uso: Tools > RECLUTA > Escenas > Instrucciones   (o Tools > RECLUTA > Reconstruir todo)
public static class InstructionsBuilder
{
    internal const string ScenePath = "Assets/Scenes/Instructions.unity";
    internal const string SceneName = "Instructions";

    [MenuItem("Tools/RECLUTA/Escenas/Instrucciones")]
    public static void CreateInstructionsScene()
    {
        if (!ReclutaBuilder.ConfirmReplace(ScenePath)) return;
        PeruAssets.Ensure();
        Build();
        ReclutaBuilder.SetupGameFlow();
    }

    internal static void Build()
    {
        Scene scene = MainMenuBuilder.NewMenuScene();
        MainMenuBuilder.CreateCamera();
        MainMenuBuilder.CreateEventSystem(out EventSystem eventSystem);

        Canvas canvas = PeruUI.CreateCanvas("InstructionsCanvas");
        Transform root = canvas.transform;
        MainMenuBuilder.CreateSpaceBackground(root, regenerateTextures: false);
        PeruUI.ScreenStripes(root, 12f);
        PeruUI.CornerBadge(root);

        PeruUI.Title(root, "PRUEBA DE DESTREZA", new Vector2(0.5f, 0.86f), 62);

        PeruUI.Label(root, "Goal",
            "Cómo jugar: pilota el cohete desde la plataforma de salida hasta la de llegada sin chocar.",
            new Vector2(0.5f, 0.765f), new Vector2(1700f, 60f), PeruTheme.BodyFont, 36, PeruTheme.White);

        // Panel izquierdo: teclado.
        RectTransform keys = PeruUI.Panel(root, "KeyboardPanel", new Vector2(0.27f, 0.47f), new Vector2(820f, 470f));
        PeruUI.Label(keys, "Header", "TECLADO", new Vector2(0.5f, 0.86f), new Vector2(760f, 70f),
            PeruTheme.HeadingFont, 46, PeruTheme.White);
        PeruUI.Label(keys, "Body",
            "ESPACIO  o  FLECHA ARRIBA\n      enciende el propulsor (subir)\n" +
            "FLECHA IZQUIERDA\n      girar a la izquierda\n" +
            "FLECHA DERECHA\n      girar a la derecha",
            new Vector2(0.5f, 0.42f), new Vector2(720f, 330f), PeruTheme.BodyBoldFont, 32, PeruTheme.TextSoft,
            TextAnchor.MiddleLeft);

        // Panel derecho: voz (el texto lo rellena Instructions.cs desde VoiceCommands).
        RectTransform voice = PeruUI.Panel(root, "VoicePanel", new Vector2(0.73f, 0.47f), new Vector2(820f, 470f));
        PeruUI.Label(voice, "Header", "ÓRDENES DE VOZ", new Vector2(0.5f, 0.86f), new Vector2(760f, 70f),
            PeruTheme.HeadingFont, 46, PeruTheme.White);
        Text voiceBody = PeruUI.Label(voice, "Body", string.Join("\n", VoiceCommands.HelpLines),
            new Vector2(0.5f, 0.5f), new Vector2(740f, 260f), PeruTheme.BodyBoldFont, 29, PeruTheme.TextSoft,
            TextAnchor.MiddleLeft);
        PeruUI.Label(voice, "Note", "Habla claro y en español. Cada orden dura un momento: repítela si hace falta.",
            new Vector2(0.5f, 0.11f), new Vector2(740f, 70f), PeruTheme.BodyFont, 24, PeruTheme.RedLight,
            TextAnchor.MiddleCenter, wrap: true);

        PeruUI.Label(root, "Route", "MISIONES:  UNDER  -  THROUGH  -  OVER",
            new Vector2(0.5f, 0.19f), new Vector2(1600f, 60f), PeruTheme.HeadingFont, 36, PeruTheme.White);

        Button back = PeruUI.Button(root, "BackButton", "Elegir prueba", new Vector2(0.35f, 0.08f));
        Button cont = PeruUI.Button(root, "ContinueButton", "Despegar", new Vector2(0.65f, 0.08f));

        var logic = canvas.gameObject.AddComponent<Instructions>();
        var so = new SerializedObject(logic);
        so.FindProperty("voiceText").objectReferenceValue = voiceBody;
        so.FindProperty("nextSceneName").stringValue = TrialProgress.FirstLevelScene;
        so.FindProperty("menuSceneName").stringValue = CharacterSelectBuilder.SceneName;
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(cont.onClick, logic.Continue);
        UnityEventTools.AddPersistentListener(back.onClick, logic.Back);
        eventSystem.firstSelectedGameObject = cont.gameObject;

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            Debug.LogError("No se pudo guardar la escena Instructions.");
    }
}
