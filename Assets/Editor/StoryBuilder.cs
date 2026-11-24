using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Genera la escena Historia (introducción de la Base Espacial del Perú).
// Uso: Tools > RECLUTA > Escenas > Historia   (o Tools > RECLUTA > Reconstruir todo)
public static class StoryBuilder
{
    internal const string ScenePath = "Assets/Scenes/Historia.unity";
    internal const string SceneName = "Historia";

    [MenuItem("Tools/RECLUTA/Escenas/Historia")]
    public static void CreateStoryScene()
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

        Canvas canvas = PeruUI.CreateCanvas("StoryCanvas");
        Transform root = canvas.transform;
        MainMenuBuilder.CreateSpaceBackground(root, regenerateTextures: false);
        PeruUI.ScreenStripes(root, 12f);
        PeruUI.CornerBadge(root);

        PeruUI.Title(root, "LA MISIÓN", new Vector2(0.5f, 0.86f), 100);

        RectTransform panel = PeruUI.Panel(root, "StoryPanel", new Vector2(0.5f, 0.47f), new Vector2(1500f, 620f));

        // Bandera grande a la izquierda del panel.
        RawImage flag = PeruUI.Flag(panel, new Vector2(0f, 0.5f), 180f);
        flag.rectTransform.pivot = new Vector2(0f, 0.5f);
        flag.rectTransform.anchoredPosition = new Vector2(60f, 70f);
        PeruUI.Label(panel, "FlagCaption", "PERÚ", new Vector2(0f, 0.5f), new Vector2(270f, 50f),
            PeruTheme.HeadingFont, 36, PeruTheme.White).rectTransform.anchoredPosition = new Vector2(195f, -60f);

        // Texto de la historia (lo escribe Story.cs letra a letra).
        Text body = PeruUI.Label(panel, "StoryText", Story.StoryText, new Vector2(0.5f, 0.5f),
            new Vector2(1060f, 400f), PeruTheme.BodyFont, 38, PeruTheme.White, TextAnchor.UpperLeft, wrap: true);
        body.rectTransform.anchoredPosition = new Vector2(170f, 40f);
        PeruUI.AddSoftShadow(body.gameObject);

        Text finalLine = PeruUI.Label(panel, "FinalLine", Story.FinalLine, new Vector2(0.5f, 0f),
            new Vector2(1400f, 70f), PeruTheme.BodyBoldFont, 40, PeruTheme.RedLight, TextAnchor.MiddleCenter, wrap: true);
        finalLine.rectTransform.anchoredPosition = new Vector2(0f, 70f);
        var fo = finalLine.gameObject.AddComponent<Outline>();
        fo.effectColor = new Color(0f, 0f, 0f, 0.8f);
        fo.effectDistance = new Vector2(2f, -2f);

        Button back = PeruUI.Button(root, "BackButton", "Volver", new Vector2(0.35f, 0.08f));
        Button cont = PeruUI.Button(root, "ContinueButton", "Saltar", new Vector2(0.65f, 0.08f));

        var story = canvas.gameObject.AddComponent<Story>();
        var so = new SerializedObject(story);
        so.FindProperty("bodyText").objectReferenceValue = body;
        so.FindProperty("finalText").objectReferenceValue = finalLine;
        so.FindProperty("continueLabel").objectReferenceValue = cont.GetComponentInChildren<Text>();
        so.FindProperty("nextSceneName").stringValue = CharacterSelectBuilder.SceneName;
        so.FindProperty("menuSceneName").stringValue = "MainMenu";
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(cont.onClick, story.Continue);
        UnityEventTools.AddPersistentListener(back.onClick, story.Back);
        eventSystem.firstSelectedGameObject = cont.gameObject;

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            Debug.LogError("No se pudo guardar la escena Historia.");
    }
}
