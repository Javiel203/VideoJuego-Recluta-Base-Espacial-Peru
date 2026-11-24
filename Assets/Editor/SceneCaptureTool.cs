using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Toma capturas (1920x1080) de cada escena de la build tal como la ve su cámara,
// incluyendo la interfaz (los Canvas "Overlay" se pasan temporalmente a "Screen Space - Camera").
// Las imágenes se guardan en <proyecto>/Presentacion/capturas. Las escenas NO se guardan.
// Menú: Astro Rocket > Capturar escenas para la presentación.
public static class SceneCaptureTool
{
    const int Width = 1920, Height = 1080;

    [MenuItem("Astro Rocket/Capturar escenas para la presentación")]
    public static void CaptureAll()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Presentacion", "capturas");
        Directory.CreateDirectory(outDir);

        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (!s.enabled) continue;
            var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
            FillRuntimeTexts();

            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null) { Debug.LogWarning($"[Captura] {s.path}: sin cámara"); continue; }

            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = Mathf.Max(cam.nearClipPlane + 0.05f, 1f);
            }
            Canvas.ForceUpdateCanvases();

            string file = Path.Combine(outDir, scene.name + ".png");
            Render(cam, file);
            Debug.Log($"[Captura] {file}");

            ReportBrokenMaterials(scene.name);

            // Niveles de la nave: además, una vista general de todo el recorrido.
            Movment rocket = Object.FindAnyObjectByType<Movment>();
            if (rocket != null)
            {
                var wide = new GameObject("CapturaGeneral").AddComponent<Camera>();
                wide.CopyFrom(cam);
                Bounds b = LevelBounds(rocket);
                float vFov = wide.fieldOfView * Mathf.Deg2Rad;
                float hFov = 2f * Mathf.Atan(Mathf.Tan(vFov / 2f) * Width / Height);
                float dist = Mathf.Max(b.extents.y / Mathf.Tan(vFov / 2f), b.extents.x / Mathf.Tan(hFov / 2f)) * 1.1f;
                wide.transform.SetPositionAndRotation(new Vector3(b.center.x, b.center.y, -dist), Quaternion.identity);
                wide.farClipPlane = Mathf.Max(wide.farClipPlane, dist * 4f);
                string wideFile = Path.Combine(outDir, scene.name + "_general.png");
                Render(wide, wideFile);
                Object.DestroyImmediate(wide.gameObject);
                Debug.Log($"[Captura] {wideFile}");
            }
        }
    }

    // Recorrido del nivel: nave, plataformas y barreras (sin el suelo ni la decoración lejana).
    static Bounds LevelBounds(Movment rocket)
    {
        var b = new Bounds(rocket.transform.position, Vector3.one * 4f);
        foreach (GameObject root in rocket.gameObject.scene.GetRootGameObjects())
        {
            bool include = root.CompareTag("Finish") || root.CompareTag("Friendly") || root.name.Contains("StartPad")
                           || root.name == "Barreras";
            if (!include) continue;
            foreach (Collider c in root.GetComponentsInChildren<Collider>()) b.Encapsulate(c.bounds);
        }
        b.Expand(new Vector3(4f, 4f, 0f));
        return b;
    }

    // Objetos que se verían rosados: sin material o con un shader que este pipeline no puede dibujar.
    static void ReportBrokenMaterials(string sceneName)
    {
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            foreach (Material m in r.sharedMaterials)
            {
                string problem = m == null ? "sin material"
                    : m.shader == null || !m.shader.isSupported || m.shader.name.Contains("InternalError") ? "shader " + (m.shader ? m.shader.name : "null")
                    : null;
                if (problem == null) continue;
                string path = r.name;
                for (Transform t = r.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
                Debug.Log($"[Rosado] {sceneName}: {path} -> {problem} (material: {(m ? m.name + " " + AssetDatabase.GetAssetPath(m) : "-")}, tipo {r.GetType().Name})");
            }
        }
    }

    // Textos que en el juego se escriben al darle Play.
    static void FillRuntimeTexts()
    {
        var story = Object.FindAnyObjectByType<Story>();
        if (story != null)
        {
            var so = new SerializedObject(story);
            if (so.FindProperty("bodyText").objectReferenceValue is Text body) body.text = Story.StoryText;
            if (so.FindProperty("finalText").objectReferenceValue is Text fin) fin.text = Story.FinalLine;
            if (so.FindProperty("continueLabel").objectReferenceValue is Text lbl) lbl.text = "CONTINUAR";
        }
        var instr = Object.FindAnyObjectByType<Instructions>();
        if (instr != null && new SerializedObject(instr).FindProperty("voiceText").objectReferenceValue is Text voice)
            voice.text = string.Join("\n", VoiceCommands.HelpLines);
    }

    static void Render(Camera cam, string file)
    {
        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        RenderTexture prevTarget = cam.targetTexture, prevActive = RenderTexture.active;
        float prevAspect = cam.aspect;
        cam.targetTexture = rt;
        cam.aspect = (float)Width / Height;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());

        cam.targetTexture = prevTarget;
        cam.aspect = prevAspect;
        RenderTexture.active = prevActive;
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }
}
