using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Genera la escena CharacterSelect (nombre + elección de piloto Hombre/Mujer).
// Uso: Tools > RECLUTA > Escenas > Selección de piloto   (o Tools > RECLUTA > Reconstruir todo)
public static class CharacterSelectBuilder
{
    internal const string ScenePath = "Assets/Scenes/CharacterSelect.unity";
    internal const string SceneName = TrialProgress.SelectScene;
    const string MaleTexturePath = "Assets/Textures/Pilot_Male.png";
    const string FemaleTexturePath = "Assets/Textures/Pilot_Female.png";

    [MenuItem("Tools/RECLUTA/Escenas/Selección de piloto")]
    public static void CreateCharacterSelectScene()
    {
        if (!ReclutaBuilder.ConfirmReplace(ScenePath)) return;
        PeruAssets.Ensure();
        Build();
        ReclutaBuilder.SetupGameFlow();
    }

    internal static void Build()
    {
        Texture2D maleTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MaleTexturePath);
        if (maleTex == null)
            maleTex = MainMenuBuilder.CreateTextureAsset(MaleTexturePath, GenerateSilhouette(female: false),
                TextureWrapMode.Clamp);
        Texture2D femaleTex = AssetDatabase.LoadAssetAtPath<Texture2D>(FemaleTexturePath);
        if (femaleTex == null)
            femaleTex = MainMenuBuilder.CreateTextureAsset(FemaleTexturePath, GenerateSilhouette(female: true),
                TextureWrapMode.Clamp);

        Scene scene = MainMenuBuilder.NewMenuScene();
        MainMenuBuilder.CreateCamera();
        MainMenuBuilder.CreateEventSystem(out EventSystem eventSystem);

        Canvas canvas = PeruUI.CreateCanvas("CharacterSelectCanvas");
        Transform root = canvas.transform;
        MainMenuBuilder.CreateSpaceBackground(root, regenerateTextures: false);
        PeruUI.ScreenStripes(root, 12f);
        PeruUI.CornerBadge(root);

        PeruUI.Title(root, "ELIGE TU PILOTO", new Vector2(0.5f, 0.905f), 68);

        InputField nameInput = CreateNameInput(root, new Vector2(0.5f, 0.8f));

        // Dificultad: FÁCIL · INTERMEDIO · AVANZADO (debajo del nombre).
        Text diffLabel = PeruUI.Label(root, "DifficultyLabel", "DIFICULTAD", new Vector2(0.215f, 0.7f),
            new Vector2(240f, 50f), PeruTheme.HeadingFont, 26, PeruTheme.TextSoft, TextAnchor.MiddleRight);
        PeruUI.AddSoftShadow(diffLabel.gameObject);
        string[] diffNames = { "Fácil", "Intermedio", "Avanzado" };
        var diffButtons = new Button[diffNames.Length];
        for (int i = 0; i < diffNames.Length; i++)
        {
            diffButtons[i] = PeruUI.Button(root, "Difficulty" + i, diffNames[i],
                new Vector2(0.355f + i * 0.145f, 0.7f), new Vector2(240f, 64f));
            diffButtons[i].GetComponentInChildren<Text>().fontSize = 28;
        }

        Card male = CreateCard(root, "MaleCard", "HOMBRE", maleTex, new Vector2(0.4f, 0.5f));
        Card female = CreateCard(root, "FemaleCard", "MUJER", femaleTex, new Vector2(0.6f, 0.5f));

        Text hint = PeruUI.Label(root, "Hint", "", new Vector2(0.5f, 0.31f), new Vector2(1400f, 50f),
            PeruTheme.BodyBoldFont, 36, PeruTheme.TextSoft);

        // Las dos pruebas del reclutamiento + la misión final (se desbloquea al superar las dos).
        Button destreza = CreateTrialButton(root, "DestrezaButton", "Prueba de destreza",
            "Pilota la nave: Under, Through y Over", new Vector2(0.2f, 0.235f), out Text destrezaStatus);
        Button memoria = CreateTrialButton(root, "MemoriaButton", "Prueba de memoria",
            "Repite la secuencia de los cristales", new Vector2(0.5f, 0.235f), out Text memoriaStatus);
        Button defensa = CreateTrialButton(root, "DefensaButton", "Misión final",
            "Defensa espacial: derriba 5 naves", new Vector2(0.8f, 0.235f), out Text defensaStatus);

        Button back = PeruUI.Button(root, "BackButton", "Volver", new Vector2(0.5f, 0.065f), new Vector2(300f, 80f));
        back.GetComponentInChildren<Text>().fontSize = 30;

        // Lógica de la pantalla.
        var select = canvas.gameObject.AddComponent<CharacterSelect>();
        var so = new SerializedObject(select);
        so.FindProperty("nameInput").objectReferenceValue = nameInput;
        so.FindProperty("maleCard").objectReferenceValue = male.rect;
        so.FindProperty("maleBorder").objectReferenceValue = male.border;
        so.FindProperty("maleSilhouette").objectReferenceValue = male.silhouette;
        so.FindProperty("femaleCard").objectReferenceValue = female.rect;
        so.FindProperty("femaleBorder").objectReferenceValue = female.border;
        so.FindProperty("femaleSilhouette").objectReferenceValue = female.silhouette;
        so.FindProperty("destrezaButton").objectReferenceValue = destreza;
        so.FindProperty("memoriaButton").objectReferenceValue = memoria;
        so.FindProperty("defensaButton").objectReferenceValue = defensa;
        so.FindProperty("destrezaStatus").objectReferenceValue = destrezaStatus;
        so.FindProperty("memoriaStatus").objectReferenceValue = memoriaStatus;
        so.FindProperty("defensaStatus").objectReferenceValue = defensaStatus;
        so.FindProperty("hintText").objectReferenceValue = hint;
        SerializedProperty diffProp = so.FindProperty("difficultyButtons");
        diffProp.arraySize = diffButtons.Length;
        for (int i = 0; i < diffButtons.Length; i++)
            diffProp.GetArrayElementAtIndex(i).objectReferenceValue = diffButtons[i];
        so.FindProperty("menuSceneName").stringValue = StoryBuilder.SceneName;
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(diffButtons[0].onClick, select.SelectFacil);
        UnityEventTools.AddPersistentListener(diffButtons[1].onClick, select.SelectIntermedio);
        UnityEventTools.AddPersistentListener(diffButtons[2].onClick, select.SelectAvanzado);
        UnityEventTools.AddPersistentListener(male.button.onClick, select.SelectMale);
        UnityEventTools.AddPersistentListener(female.button.onClick, select.SelectFemale);
        UnityEventTools.AddPersistentListener(destreza.onClick, select.PlayDestreza);
        UnityEventTools.AddPersistentListener(memoria.onClick, select.PlayMemoria);
        UnityEventTools.AddPersistentListener(defensa.onClick, select.PlayDefensa);
        UnityEventTools.AddPersistentListener(back.onClick, select.Back);

        eventSystem.firstSelectedGameObject = nameInput.gameObject;

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            Debug.LogError("No se pudo guardar la escena CharacterSelect.");
    }

    // ---------- UI ----------

    struct Card
    {
        public RectTransform rect;
        public Button button;
        public Image border;
        public RawImage silhouette;
    }

    static Card CreateCard(Transform parent, string name, string label, Texture2D silhouetteTex, Vector2 anchor)
    {
        Sprite uiSprite = PeruTheme.RoundedSprite;

        // Borde (el color lo cambia CharacterSelect al seleccionar).
        RectTransform rt = PeruUI.Rect(parent, name, anchor, new Vector2(280f, 350f));
        var border = rt.gameObject.AddComponent<Image>();
        border.sprite = uiSprite;
        border.type = Image.Type.Sliced;
        border.color = new Color(1f, 1f, 1f, 0.15f);

        // Panel interior.
        RectTransform innerRt = PeruUI.Rect(rt, "Inner", Vector2.zero, Vector2.zero);
        PeruUI.Stretch(innerRt, 6f);
        var inner = innerRt.gameObject.AddComponent<Image>();
        inner.sprite = uiSprite;
        inner.type = Image.Type.Sliced;
        inner.color = PeruTheme.PanelLight;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = inner;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.highlightedColor = Color.white;
        colors.selectedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.pressedColor = new Color(1f, 0.75f, 0.75f, 1f);
        colors.fadeDuration = 0.1f;
        button.colors = colors;

        // Franja roja | blanca | roja en la parte de abajo de la tarjeta.
        RectTransform band = PeruUI.Rect(innerRt, "Band", Vector2.zero, Vector2.zero);
        band.anchorMin = new Vector2(0f, 0f);
        band.anchorMax = new Vector2(1f, 0f);
        band.pivot = new Vector2(0.5f, 0f);
        band.offsetMin = new Vector2(18f, 100f);
        band.offsetMax = new Vector2(-18f, 106f);
        for (int i = 0; i < 3; i++)
        {
            RectTransform part = PeruUI.Rect(band, "Part" + i, Vector2.zero, Vector2.zero);
            part.anchorMin = new Vector2(i / 3f, 0f);
            part.anchorMax = new Vector2((i + 1) / 3f, 1f);
            part.offsetMin = part.offsetMax = Vector2.zero;
            var img = part.gameObject.AddComponent<Image>();
            img.color = i == 1 ? PeruTheme.White : PeruTheme.Red;
            img.raycastTarget = false;
        }

        // Silueta.
        RectTransform silRt = PeruUI.Rect(rt, "Silhouette", new Vector2(0.5f, 1f), new Vector2(220f, 220f));
        silRt.pivot = new Vector2(0.5f, 1f);
        silRt.anchoredPosition = new Vector2(0f, -20f);
        var silhouette = silRt.gameObject.AddComponent<RawImage>();
        silhouette.texture = silhouetteTex;
        silhouette.raycastTarget = false;

        // Etiqueta.
        Text text = PeruUI.Label(rt, "Label", label, new Vector2(0.5f, 0f), new Vector2(260f, 90f),
            PeruTheme.HeadingFont, 36, PeruTheme.White);
        text.rectTransform.pivot = new Vector2(0.5f, 0f);
        text.rectTransform.anchoredPosition = new Vector2(0f, 8f);
        PeruUI.AddSoftShadow(text.gameObject);

        rt.gameObject.AddComponent<FancyButton>();
        return new Card { rect = rt, button = button, border = border, silhouette = silhouette };
    }

    // Botón grande de prueba: título + descripción dentro, y estado (PENDIENTE/COMPLETADA) debajo.
    static Button CreateTrialButton(Transform parent, string name, string title, string description,
        Vector2 anchor, out Text status)
    {
        Button button = PeruUI.Button(parent, name, title, anchor, new Vector2(540f, 120f));
        Text label = button.GetComponentInChildren<Text>();
        label.fontSize = 34;
        RectTransform lrt = label.rectTransform;
        lrt.offsetMin = new Vector2(10f, 40f);
        lrt.offsetMax = new Vector2(-10f, -8f);

        Text desc = PeruUI.Label(button.transform, "Description", description, new Vector2(0.5f, 0f),
            new Vector2(520f, 44f), PeruTheme.BodyFont, 24, new Color(1f, 1f, 1f, 0.9f));
        desc.rectTransform.pivot = new Vector2(0.5f, 0f);
        desc.rectTransform.anchoredPosition = new Vector2(0f, 12f);

        status = PeruUI.Label(parent, name + "Status", "PENDIENTE", anchor, new Vector2(540f, 40f),
            PeruTheme.HeadingFont, 24, new Color(1f, 1f, 1f, 0.55f));
        status.rectTransform.anchoredPosition = new Vector2(0f, -88f);
        return button;
    }

    static InputField CreateNameInput(Transform parent, Vector2 anchor)
    {
        RectTransform rt = PeruUI.Rect(parent, "NameInput", anchor, new Vector2(640f, 88f));

        var frame = rt.gameObject.AddComponent<Image>();
        frame.sprite = PeruTheme.RoundedSprite;
        frame.type = Image.Type.Sliced;
        frame.color = PeruTheme.White;

        RectTransform bgRt = PeruUI.Rect(rt, "Background", Vector2.zero, Vector2.zero);
        PeruUI.Stretch(bgRt, 4f);
        var bg = bgRt.gameObject.AddComponent<Image>();
        bg.sprite = PeruTheme.RoundedSprite;
        bg.type = Image.Type.Sliced;
        bg.color = PeruTheme.Night;
        bg.raycastTarget = false;

        Text placeholder = CreateInputText(rt, "Placeholder", "Nombre del piloto...",
            new Color(0.75f, 0.78f, 0.9f, 0.6f), PeruTheme.BodyFont);
        Text text = CreateInputText(rt, "Text", "", Color.white, PeruTheme.BodyBoldFont);
        text.supportRichText = false;

        var input = rt.gameObject.AddComponent<InputField>();
        input.targetGraphic = frame;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.characterLimit = PilotData.MaxNameLength;
        input.lineType = InputField.LineType.SingleLine;
        input.contentType = InputField.ContentType.Standard;
        input.caretWidth = 3;
        input.customCaretColor = true;
        input.caretColor = PeruTheme.Red;
        input.selectionColor = new Color(0.85f, 0.06f, 0.14f, 0.45f);
        ColorBlock colors = input.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.55f);
        colors.highlightedColor = Color.white;
        colors.selectedColor = PeruTheme.Red;
        input.colors = colors;
        return input;
    }

    static Text CreateInputText(Transform parent, string name, string content, Color color, Font font)
    {
        Text text = PeruUI.Label(parent, name, content, new Vector2(0.5f, 0.5f), Vector2.zero, font, 40, color);
        RectTransform rt = text.rectTransform;
        PeruUI.Stretch(rt);
        rt.offsetMin = new Vector2(24f, 8f);
        rt.offsetMax = new Vector2(-24f, -8f);
        return text;
    }

    // ---------- Siluetas procedurales (blancas con alfa; el color lo pone la UI) ----------

    const int SilhouetteSize = 512;

    static Texture2D GenerateSilhouette(bool female)
    {
        int s = SilhouetteSize;
        var pixels = new Color[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = female ? FemaleShape(p) : MaleShape(p);
                float a = Mathf.Clamp01(0.5f - d); // borde suavizado de 1 px
                pixels[y * s + x] = new Color(1f, 1f, 1f, a);
            }
        }
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // Distancias con signo (negativo = dentro). El eje Y crece hacia arriba.
    static float MaleShape(Vector2 p)
    {
        float head = Circle(p, new Vector2(256f, 330f), 78f);
        float hair = Mathf.Max(Circle(p, new Vector2(256f, 342f), 84f), 335f - p.y); // pelo corto arriba
        float neck = Box(p, new Vector2(256f, 235f), new Vector2(30f, 40f), 8f);
        float torso = Box(p, new Vector2(256f, 70f), new Vector2(175f, 140f), 80f);
        return Mathf.Min(Mathf.Min(head, hair), Mathf.Min(neck, torso));
    }

    static float FemaleShape(Vector2 p)
    {
        float head = Circle(p, new Vector2(256f, 335f), 72f);
        float hairTop = Circle(p, new Vector2(256f, 348f), 84f);
        float hairLong = Box(p, new Vector2(256f, 265f), new Vector2(92f, 95f), 40f); // melena hasta los hombros
        float neck = Box(p, new Vector2(256f, 235f), new Vector2(26f, 40f), 8f);
        float torso = Box(p, new Vector2(256f, 65f), new Vector2(150f, 140f), 90f);
        return Mathf.Min(Mathf.Min(Mathf.Min(head, hairTop), hairLong), Mathf.Min(neck, torso));
    }

    static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

    static float Box(Vector2 p, Vector2 c, Vector2 half, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(radius, radius);
        Vector2 outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
        return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }
}
