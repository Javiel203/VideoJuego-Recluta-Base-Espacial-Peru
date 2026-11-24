using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Genera la escena MainMenu (portada) de RECLUTA: Base Espacial del Perú.
// Uso: Tools > RECLUTA > Escenas > Portada (MainMenu)   (o Tools > RECLUTA > Reconstruir todo)
public static class MainMenuBuilder
{
    internal const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string TexturesFolder = "Assets/Textures";
    const string NebulaTexturePath = TexturesFolder + "/MenuSpace_Nebula.png";
    const string StarsTexturePath = TexturesFolder + "/MenuSpace_Stars.png";
    internal const string GameSceneName = StoryBuilder.SceneName;

    internal static Color BackgroundColor => PeruTheme.Night;

    [MenuItem("Tools/RECLUTA/Escenas/Portada (MainMenu)")]
    public static void CreateMainMenuScene()
    {
        if (!ReclutaBuilder.ConfirmReplace(ScenePath)) return;
        PeruAssets.Ensure();
        Build();
        SetBuildSceneOrder(ScenePath);
    }

    internal static void Build()
    {
        Scene scene = NewMenuScene();
        CreateCamera();
        CreateEventSystem(out EventSystem eventSystem);

        Canvas canvas = CreateCanvas();
        Transform root = canvas.transform;
        MainMenu menu = canvas.gameObject.AddComponent<MainMenu>();
        SetString(menu, "gameSceneName", GameSceneName);

        CreateSpaceBackground(root, regenerateTextures: !File.Exists(NebulaTexturePath));
        PeruUI.ScreenStripes(root, 14f);

        // Emblema + título + subtítulo con banderas.
        PeruUI.Emblem(root, new Vector2(0.5f, 0.79f), 250f);
        PeruUI.Title(root, PeruTheme.GameTitle, new Vector2(0.5f, 0.585f), 170);

        Text subtitle = PeruUI.Label(root, "Subtitle", PeruTheme.GameSubtitle, new Vector2(0.5f, 0.475f),
            new Vector2(1300f, 80f), PeruTheme.HeadingFont, 54, PeruTheme.White);
        var subOutline = subtitle.gameObject.AddComponent<Outline>();
        subOutline.effectColor = PeruTheme.RedDark;
        subOutline.effectDistance = new Vector2(2f, -2f);
        PeruUI.Flag(root, new Vector2(0.19f, 0.475f), 56f);
        PeruUI.Flag(root, new Vector2(0.81f, 0.475f), 56f);

        Button play = PeruUI.Button(root, "PlayButton", "Jugar", new Vector2(0.5f, 0.32f));
        Button exit = PeruUI.Button(root, "ExitButton", "Salir", new Vector2(0.5f, 0.19f));
        UnityEventTools.AddPersistentListener(play.onClick, menu.PlayGame);
        UnityEventTools.AddPersistentListener(exit.onClick, menu.ExitGame);
        SetVerticalNavigation(play, exit);
        eventSystem.firstSelectedGameObject = play.gameObject;

        PeruUI.Label(root, "Footer", "Programa de selección de pilotos espaciales", new Vector2(0.5f, 0.06f),
            new Vector2(1200f, 40f), PeruTheme.BodyFont, 26, PeruTheme.TextSoft);

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            Debug.LogError("No se pudo guardar la escena MainMenu.");
    }

    // ---------- Objetos de la escena (compartidos por todos los builders) ----------

    internal static Scene NewMenuScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = BackgroundColor;
        return scene;
    }

    // Fondo espacial: nebulosa + dos capas de estrellas con parallax.
    internal static void CreateSpaceBackground(Transform canvas, bool regenerateTextures)
    {
        Texture2D nebula = regenerateTextures ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(NebulaTexturePath);
        Texture2D stars = regenerateTextures ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(StarsTexturePath);
        if (nebula == null)
            nebula = CreateTextureAsset(NebulaTexturePath, GenerateNebula(1024, 1024, 1234), TextureWrapMode.Repeat);
        if (stars == null)
            stars = CreateTextureAsset(StarsTexturePath, GenerateStars(1024, 1024, 5678, 260, true), TextureWrapMode.Repeat);

        CreateBackgroundLayer(canvas, "Background_Nebula", nebula, Color.white,
            new Vector2(0.003f, 0.0015f), 1f);
        CreateBackgroundLayer(canvas, "Background_StarsFar", stars, new Color(1f, 1f, 1f, 0.6f),
            new Vector2(0.006f, 0.003f), 1.6f);
        CreateBackgroundLayer(canvas, "Background_StarsNear", stars, Color.white,
            new Vector2(0.015f, 0.007f), 0.8f);
    }

    internal static void SetString(Object target, string property, string value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void CreateCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BackgroundColor;
        go.AddComponent<AudioListener>();
        go.transform.position = new Vector3(0f, 0f, -10f);
    }

    internal static void CreateEventSystem(out EventSystem eventSystem)
    {
        var go = new GameObject("EventSystem");
        eventSystem = go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    internal static Canvas CreateCanvas() => PeruUI.CreateCanvas("MainMenuCanvas");

    static void CreateBackgroundLayer(Transform parent, string name, Texture2D texture, Color tint,
        Vector2 scrollSpeed, float tiling)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);

        var raw = go.AddComponent<RawImage>();
        raw.texture = texture;
        raw.color = tint;
        raw.raycastTarget = false;

        var scroll = go.AddComponent<StarfieldScroll>();
        var so = new SerializedObject(scroll);
        so.FindProperty("scrollSpeed").vector2Value = scrollSpeed;
        so.FindProperty("tiling").floatValue = tiling;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void SetVerticalNavigation(Button top, Button bottom)
    {
        var navTop = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = bottom, selectOnUp = bottom };
        var navBottom = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = top, selectOnDown = top };
        top.navigation = navTop;
        bottom.navigation = navBottom;
    }

    internal static void Stretch(RectTransform rt) => PeruUI.Stretch(rt);

    // ---------- Build Settings ----------

    // Coloca las escenas indicadas (si existen) al principio, en ese orden; el resto mantiene su orden.
    internal static void SetBuildSceneOrder(params string[] firstPaths)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        var first = new List<EditorBuildSettingsScene>();
        foreach (string path in firstPaths)
        {
            if (!File.Exists(path)) continue;
            scenes.RemoveAll(s => s.path == path);
            first.Add(new EditorBuildSettingsScene(path, true));
        }
        first.AddRange(scenes);
        EditorBuildSettings.scenes = first.ToArray();
        AssetDatabase.SaveAssets();
    }

    // ---------- Texturas procedurales del fondo ----------

    internal static Texture2D CreateTextureAsset(string path, Texture2D source, TextureWrapMode wrapMode)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, source.EncodeToPNG());
        Object.DestroyImmediate(source);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.wrapMode = wrapMode;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.sRGBTexture = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Nebulosa oscura azul/morada, sin costuras (tileable) y con estrellas tenues de fondo.
    static Texture2D GenerateNebula(int w, int h, int seed)
    {
        var rng = new System.Random(seed);
        float ox = (float)rng.NextDouble() * 1000f;
        float oy = (float)rng.NextDouble() * 1000f;

        var deep = new Color(0.01f, 0.01f, 0.05f);
        var blue = new Color(0.05f, 0.12f, 0.35f);
        var purple = new Color(0.35f, 0.08f, 0.4f);

        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float n1 = SeamlessFbm(x, y, w, h, 3f, ox, oy);
                float n2 = SeamlessFbm(x, y, w, h, 5f, ox + 300f, oy + 300f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((n1 - 0.4f) * 2.2f));
                float b = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((n2 - 0.5f) * 2.5f));
                Color c = Color.Lerp(deep, blue, a * 0.8f);
                c = Color.Lerp(c, purple, b * 0.55f);
                c.a = 1f;
                pixels[y * w + x] = c;
            }
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        DrawStars(tex, rng, 900, false, 0.5f);
        tex.Apply();
        return tex;
    }

    // Capa transparente con estrellas brillantes.
    static Texture2D GenerateStars(int w, int h, int seed, int count, bool withGlow)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var clear = new Color[w * h];
        for (int i = 0; i < clear.Length; i++) clear[i] = new Color(1f, 1f, 1f, 0f);
        tex.SetPixels(clear);
        DrawStars(tex, new System.Random(seed), count, withGlow, 1f);
        tex.Apply();
        return tex;
    }

    static void DrawStars(Texture2D tex, System.Random rng, int count, bool withGlow, float brightness)
    {
        int w = tex.width, h = tex.height;
        for (int i = 0; i < count; i++)
        {
            int cx = rng.Next(w);
            int cy = rng.Next(h);
            float intensity = (0.4f + 0.6f * (float)rng.NextDouble()) * brightness;
            float r = withGlow && rng.NextDouble() < 0.15 ? 2.5f + 2f * (float)rng.NextDouble() : 1.2f;

            float tint = (float)rng.NextDouble();
            Color starColor = tint < 0.2f ? new Color(0.7f, 0.8f, 1f)
                            : tint < 0.3f ? new Color(1f, 0.9f, 0.7f)
                            : Color.white;

            int rad = Mathf.CeilToInt(r * 2f);
            for (int dy = -rad; dy <= rad; dy++)
            {
                for (int dx = -rad; dx <= rad; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float falloff = Mathf.Exp(-(d * d) / (r * r * 0.5f)) * intensity;
                    if (falloff < 0.01f) continue;

                    int px = (cx + dx + w) % w; // envolver para que sea tileable
                    int py = (cy + dy + h) % h;
                    Color prev = tex.GetPixel(px, py);
                    Color add = starColor * falloff;
                    var result = new Color(
                        Mathf.Clamp01(prev.r * prev.a + add.r),
                        Mathf.Clamp01(prev.g * prev.a + add.g),
                        Mathf.Clamp01(prev.b * prev.a + add.b),
                        Mathf.Clamp01(prev.a + falloff));
                    // Para la capa transparente guardamos color sin premultiplicar.
                    if (result.a > 0f && prev.a < 1f)
                    {
                        result.r = Mathf.Clamp01(result.r / result.a);
                        result.g = Mathf.Clamp01(result.g / result.a);
                        result.b = Mathf.Clamp01(result.b / result.a);
                    }
                    tex.SetPixel(px, py, result);
                }
            }
        }
    }

    // Ruido fractal que se repite sin costuras en una textura de w x h.
    static float SeamlessFbm(int x, int y, int w, int h, float scale, float ox, float oy)
    {
        float u = (float)x / w, v = (float)y / h;
        float n00 = Fbm(u * scale + ox, v * scale + oy);
        float n10 = Fbm((u - 1f) * scale + ox, v * scale + oy);
        float n01 = Fbm(u * scale + ox, (v - 1f) * scale + oy);
        float n11 = Fbm((u - 1f) * scale + ox, (v - 1f) * scale + oy);
        return n00 * (1f - u) * (1f - v) + n10 * u * (1f - v) + n01 * (1f - u) * v + n11 * u * v;
    }

    static float Fbm(float x, float y)
    {
        float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
        for (int o = 0; o < 4; o++)
        {
            sum += Mathf.PerlinNoise(x * freq, y * freq) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return sum / norm;
    }
}
