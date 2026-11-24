using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;

// Migración del Space Shooter (Built-in, Unity 2022) a URP dentro de Astro Rocket (Unity 6).
//  - Convierte los materiales Standard → URP Lit (conversor oficial de URP) y las referencias a
//    materiales internos (Default-Material, Default-Particle...).
//  - Crea las plantillas de partículas URP que usa Detonator (Resources/Detonator/URP).
//  - Arregla materiales con shader inexistente (missileMat).
//  - Informa de materiales que seguirían viéndose rosados y saca capturas de comprobación.
// Menú: Astro Rocket > Defensa espacial > ...
public static class ShooterURPSetup
{
    const string Root = "Assets/SpaceShooter";
    const string TemplateFolder = Root + "/_project/Resources/Detonator/URP";
    const string ScenePath = "Assets/Scenes/DefensaEspacial.unity";

    [MenuItem("Astro Rocket/Defensa espacial/Convertir materiales a URP")]
    public static void Run()
    {
        // 1) Upgraders oficiales de URP (Standard → Lit, partículas estándar, etc.) sólo en Assets/SpaceShooter.
        //    (Converters.RunInBatchMode de URP 17.3 falla en batch: intenta crear un conversor 2D sin constructor.
        //     Las referencias a materiales internos Default-Material/Default-Particle se cambiaron en el YAML.)
        List<MaterialUpgrader> upgraders = MaterialUpgrader.FetchAllUpgradersForPipeline(
            typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset));
        var upgradable = new HashSet<string>(upgraders.Select(u => u.OldShaderPath));
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null || !upgradable.Contains(mat.shader.name)) continue;
            string before = mat.shader.name;
            MaterialUpgrader.Upgrade(mat, upgraders, MaterialUpgrader.UpgradeFlags.None);
            EditorUtility.SetDirty(mat);
            Debug.Log($"[URP] {path}: '{before}' → '{mat.shader.name}'");
        }

        // 2) Plantillas de partículas para Detonator.
        CreateParticleTemplate("DetonatorAdditive", additive: true);
        CreateParticleTemplate("DetonatorAlphaBlended", additive: false);

        // 3) Materiales con shader inexistente o Standard que el conversor no tocó.
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;
            string shaderName = mat.shader != null ? mat.shader.name : "";
            bool broken = mat.shader == null || shaderName.Contains("InternalError") || !mat.shader.isSupported;
            bool standard = shaderName == "Standard" || shaderName == "Standard (Specular setup)";
            if (!broken && !standard) continue;

            Texture main = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            mat.shader = lit;
            if (main != null) mat.SetTexture("_BaseMap", main);
            mat.SetColor("_BaseColor", color);
            BaseShaderGUI.SetMaterialKeywords(mat, LitGUI.SetMaterialKeywords);
            EditorUtility.SetDirty(mat);
            Debug.Log($"[URP] Material reparado a mano: {path} (antes '{shaderName}')");
        }

        AssetDatabase.SaveAssets();
        Report();
    }

    // En el proyecto original la nave del jugador no tenía DamageHandler: era invencible.
    // Para la misión final (3 oportunidades) se le añade uno, con la misma explosión que las naves enemigas.
    // La salud sale de su ShipData (ShipController la inicializa en OnEnable).
    [MenuItem("Astro Rocket/Defensa espacial/Dar salud a la nave del jugador")]
    public static void PreparePlayerShip()
    {
        const string playerPath = Root + "/_project/Prefabs/Ships/PlayerShip.prefab";
        const string enemyPath = Root + "/_project/Prefabs/Ships/EnemyShip.prefab";

        var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(enemyPath);
        Object explosion = enemy != null && enemy.TryGetComponent(out DamageHandler enemyHandler)
            ? new SerializedObject(enemyHandler).FindProperty("_explosionPrefab").objectReferenceValue
            : null;

        GameObject root = PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            if (!root.TryGetComponent(out DamageHandler handler))
                handler = root.AddComponent<DamageHandler>();
            var so = new SerializedObject(handler);
            so.FindProperty("_explosionPrefab").objectReferenceValue = explosion;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, playerPath);
            Debug.Log($"[DEFENSA] DamageHandler en PlayerShip (explosión: {(explosion ? explosion.name : "ninguna")})");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void CreateParticleTemplate(string name, bool additive)
    {
        Directory.CreateDirectory(TemplateFolder);
        string path = $"{TemplateFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (mat == null)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetFloat("_Surface", (float)BaseShaderGUI.SurfaceType.Transparent);
        mat.SetFloat("_Blend", (float)(additive ? BaseShaderGUI.BlendMode.Additive : BaseShaderGUI.BlendMode.Alpha));
        mat.SetColor("_BaseColor", Color.white);
        BaseShaderGUI.SetMaterialKeywords(mat, null, ParticleGUI.SetMaterialKeywords);
        EditorUtility.SetDirty(mat);
    }

    // Lista los materiales del shooter que no se pueden dibujar en URP.
    [MenuItem("Astro Rocket/Defensa espacial/Comprobar materiales")]
    public static void Report()
    {
        int bad = 0;
        var byShader = new Dictionary<string, int>();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;
            string shaderName = mat.shader != null ? mat.shader.name : "null";
            byShader[shaderName] = byShader.TryGetValue(shaderName, out int n) ? n + 1 : 1;
            bool broken = mat.shader == null || shaderName.Contains("InternalError") || !mat.shader.isSupported
                          || shaderName == "Standard" || shaderName == "Standard (Specular setup)";
            if (!broken) continue;
            bad++;
            Debug.Log($"[URP-PENDIENTE] {path} -> {shaderName}");
        }
        foreach (var kv in byShader.OrderByDescending(k => k.Value))
            Debug.Log($"[URP-SHADERS] {kv.Value} x {kv.Key}");
        Debug.Log($"[URP-RESUMEN] materiales con problemas: {bad}");
    }

    // ---------- Capturas de comprobación ----------

    // Línea de comandos: -executeMethod ShooterURPSetup.CaptureForReview -captureDir <carpeta>
    public static void CaptureForReview()
    {
        string dir = ArgValue("-captureDir") ?? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "Capturas");
        Directory.CreateDirectory(dir);

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (cam != null)
        {
            Render(cam, Path.Combine(dir, "Defensa_camara.png"));
        }

        // Renderers de la escena con problemas.
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (Material m in r.sharedMaterials)
            {
                bool broken = m == null || m.shader == null || !m.shader.isSupported || m.shader.name.Contains("InternalError")
                              || m.shader.name == "Standard" || m.shader.name.StartsWith("Legacy Shaders/Particles") == false && m.shader.name.StartsWith("Legacy");
                if (broken && !(r is ParticleSystemRenderer && m == null))
                    Debug.Log($"[ESCENA-ROSADO] {r.name} ({r.GetType().Name}) -> {(m ? m.name + " / " + m.shader.name : "sin material")}");
            }

        // Escaparate: cada prefab de nave y un asteroide, solos delante de una cámara propia (con el skybox).
        var light = new GameObject("LuzEscaparate").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.5f;
        light.transform.rotation = Quaternion.Euler(30f, -30f, 0f);
        string[] showcasePaths =
        {
            Root + "/_project/Prefabs/Ships/PlayerShip.prefab",
            Root + "/_project/Prefabs/Ships/EnemyShip.prefab",
            Root + "/_project/Prefabs/Asteroids/Rock1.prefab",
        };
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (root != light.gameObject) root.SetActive(false);
        foreach (string path in showcasePaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.Log($"[ESCAPARATE] no existe {path}"); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Bounds b = BoundsOf(go);
            float radius = Mathf.Max(b.extents.magnitude, 0.1f);
            var showCam = new GameObject("CamaraEscaparate").AddComponent<Camera>();
            showCam.fieldOfView = 40f;
            showCam.nearClipPlane = radius * 0.05f;
            showCam.farClipPlane = radius * 20f + 1000f;
            showCam.clearFlags = CameraClearFlags.Skybox;
            showCam.transform.position = b.center + new Vector3(-0.8f, 0.45f, -1f).normalized * radius * 3.2f;
            showCam.transform.LookAt(b.center);
            string file = Path.Combine(dir, "Defensa_" + Path.GetFileNameWithoutExtension(path) + ".png");
            Render(showCam, file);
            Object.DestroyImmediate(showCam.gameObject);
            Object.DestroyImmediate(go);
            Debug.Log($"[ESCAPARATE] {file}");
        }
        Debug.Log("[CAPTURA] listo: " + dir);
    }

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    static string ArgValue(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }

    static void Render(Camera cam, string file)
    {
        const int w = 1600, h = 900;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        RenderTexture prev = RenderTexture.active;
        cam.targetTexture = rt;
        cam.aspect = (float)w / h;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = prev;
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }
}
