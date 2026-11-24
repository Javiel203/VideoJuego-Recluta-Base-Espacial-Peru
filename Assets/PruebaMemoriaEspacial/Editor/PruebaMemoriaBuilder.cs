using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Genera la escena 3D PruebaMemoriaEspacial y todos sus recursos:
//   Assets/PruebaMemoriaEspacial/  Meshes, Textures, Materials, Prefabs, Settings
//   Assets/Scenes/PruebaMemoriaEspacial.unity
// Uso: Tools > RECLUTA > Escenas > Prueba Memoria Espacial   (o Tools > RECLUTA > Reconstruir todo)
public static class PruebaMemoriaBuilder
{
    internal const string ScenePath = "Assets/Scenes/" + TrialProgress.MemoriaScene + ".unity";

    const string Root = "Assets/PruebaMemoriaEspacial";
    const string MeshFolder = Root + "/Meshes";
    const string TexFolder = Root + "/Textures";
    const string MatFolder = Root + "/Materials";
    const string PrefabFolder = Root + "/Prefabs";
    const string SettingsFolder = Root + "/Settings";

    // Skybox espacial que usan los niveles de la nave (configuración gráfica existente).
    const string LevelSkyboxGuid = "d535d208730dd0e469d698bcbff1b900";

    static readonly Color Cyan = new Color(0.25f, 0.75f, 1f);

    struct CrystalDef
    {
        public string id, name;
        public Color color;
        public float x;
    }

    static readonly CrystalDef[] Crystals =
    {
        new CrystalDef { id = "Azul",    name = "Azul",    color = new Color(0.15f, 0.5f, 1f),   x = -2.7f },
        new CrystalDef { id = "Verde",   name = "Verde",   color = new Color(0.15f, 1f, 0.4f),   x = 0f },
        new CrystalDef { id = "Naranja", name = "Naranja", color = new Color(1f, 0.5f, 0.08f),   x = 2.7f },
    };

    // Materiales compartidos de la escena.
    static Material metalMat, darkMetalMat, floorMat, panelMat, stripMat, redMat, whiteMat;
    static Material particleMat, emblemMat, flagMat, floorRingDarkMat;
    static Mesh crystalMesh;

    [MenuItem("Tools/RECLUTA/Escenas/Prueba Memoria Espacial")]
    public static void CreateScene()
    {
        if (!ReclutaBuilder.ConfirmReplace(ScenePath)) return;
        PeruAssets.Ensure();
        Build();
        ReclutaBuilder.SetupGameFlow();
        EditorSceneManager.OpenScene(ScenePath);
        Debug.Log($"Escena {ScenePath} creada. Pulsa Play para probar la Prueba de Memoria Espacial.");
    }

    internal static void Build()
    {
        foreach (string f in new[] { Root, MeshFolder, TexFolder, MatFolder, PrefabFolder, SettingsFolder })
            Directory.CreateDirectory(f);

        CreateSharedAssets();
        VolumeProfile profile = CreateVolumeProfile();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupEnvironment();

        Transform cameraRig = CreateCamera();
        CreateLights();
        CreatePostProcess(profile);
        CreateRoom();
        CreateDust();

        var crystals = new Crystal[Crystals.Length];
        for (int i = 0; i < Crystals.Length; i++)
            crystals[i] = CreatePedestal(i, Crystals[i]);

        MemoryGameUI ui = CreateUI(out Button play, out Button instrBack, out Button retry, out Button defeatBack,
            out Button hudBack);

        // Lógica del juego.
        var gameGo = new GameObject("MemoryGame");
        var audio = gameGo.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        audio.volume = 0.8f;
        var game = gameGo.AddComponent<MemoryGame>();
        var so = new SerializedObject(game);
        SetArray(so.FindProperty("crystals"), crystals);
        so.FindProperty("ui").objectReferenceValue = ui;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.FindProperty("cameraRig").objectReferenceValue = cameraRig;
        so.FindProperty("backSceneName").stringValue = TrialProgress.SelectScene;
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(play.onClick, game.Play);
        UnityEventTools.AddPersistentListener(retry.onClick, game.Play);
        UnityEventTools.AddPersistentListener(instrBack.onClick, game.Back);
        UnityEventTools.AddPersistentListener(defeatBack.onClick, game.Back);
        UnityEventTools.AddPersistentListener(hudBack.onClick, game.Back);

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            Debug.LogError("No se pudo guardar la escena PruebaMemoriaEspacial.");
        AssetDatabase.SaveAssets();
    }

    // =====================================================================
    // Recursos: texturas, malla, materiales, perfil de postprocesado
    // =====================================================================

    static void CreateSharedAssets()
    {
        Texture2D grid = SaveTexture(TexFolder + "/FloorGrid.png", GenerateGrid(256), TextureWrapMode.Repeat, true);
        Texture2D dot = SaveTexture(TexFolder + "/SoftDot.png", GenerateDot(64), TextureWrapMode.Clamp, false);
        crystalMesh = SaveAsset(GenerateCrystalMesh(), MeshFolder + "/CrystalMesh.asset");

        metalMat = LitMaterial("Metal", new Color(0.62f, 0.66f, 0.72f), 0.9f, 0.78f, Color.black);
        darkMetalMat = LitMaterial("MetalOscuro", new Color(0.09f, 0.1f, 0.13f), 0.75f, 0.6f, Color.black);
        panelMat = LitMaterial("PanelPared", new Color(0.06f, 0.07f, 0.11f), 0.6f, 0.45f, Color.black);
        stripMat = LitMaterial("FranjaLuz", Color.black, 0f, 0.5f, Cyan * 2.2f);
        redMat = LitMaterial("FranjaRoja", PeruTheme.Red, 0f, 0.5f, PeruTheme.Red * 1.6f);
        whiteMat = LitMaterial("FranjaBlanca", Color.white, 0f, 0.5f, Color.white * 1.2f);
        floorMat = LitMaterial("SueloRejilla", new Color(0.35f, 0.4f, 0.5f), 0.55f, 0.7f, Cyan * 1.4f,
            grid, grid, new Vector2(20f, 20f));
        floorRingDarkMat = LitMaterial("SueloAnilloInterior", new Color(0.03f, 0.04f, 0.07f), 0.6f, 0.8f, Color.black);

        particleMat = ParticleMaterial("ParticulaBrillo", dot);
        emblemMat = UnlitMaterial("EmblemaBase", PeruTheme.Emblem, alphaClip: true, tint: new Color(0.95f, 0.95f, 0.95f));
        flagMat = UnlitMaterial("BanderaPeru", PeruTheme.Flag, alphaClip: false, tint: new Color(0.9f, 0.9f, 0.9f));
    }

    static VolumeProfile CreateVolumeProfile()
    {
        string path = SettingsFolder + "/MemoriaVolumeProfile.asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        Bloom bloom = profile.Add<Bloom>();
        bloom.intensity.Override(1.1f);
        bloom.threshold.Override(0.9f);
        bloom.scatter.Override(0.65f);
        bloom.highQualityFiltering.Override(false);   // ligero para WebGL

        Vignette vignette = profile.Add<Vignette>();
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.45f);

        Tonemapping tone = profile.Add<Tonemapping>();
        tone.mode.Override(TonemappingMode.Neutral);

        foreach (VolumeComponent c in profile.components)
        {
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile);
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    static Material LitMaterial(string name, Color baseColor, float metallic, float smoothness, Color emission,
        Texture baseMap = null, Texture emissionMap = null, Vector2? tiling = null)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        m.SetColor("_BaseColor", baseColor);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        if (baseMap != null) m.SetTexture("_BaseMap", baseMap);
        if (tiling.HasValue) m.SetTextureScale("_BaseMap", tiling.Value);
        if (emission.maxColorComponent > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            if (emissionMap != null) m.SetTexture("_EmissionMap", emissionMap);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        return SaveAsset(m, MatFolder + "/" + name + ".mat");
    }

    // Material de cristal: casi negro de base, todo el color sale de la emisión (la anima Crystal.cs).
    static Material CrystalMaterial(string id, Color color)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Cristal" + id };
        m.SetColor("_BaseColor", Color.Lerp(Color.black, color, 0.35f));
        m.SetFloat("_Metallic", 0.2f);
        m.SetFloat("_Smoothness", 0.95f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", color * 0.6f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return SaveAsset(m, MatFolder + "/Cristal" + id + ".mat");
    }

    static Material GlowMaterial(string name, Color color, float intensity) =>
        LitMaterial(name, Color.black, 0f, 0.4f, color * intensity);

    static Material UnlitMaterial(string name, Texture tex, bool alphaClip, Color tint)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", tint);
        if (alphaClip)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.SetOverrideTag("RenderType", "TransparentCutout");
        }
        return SaveAsset(m, MatFolder + "/" + name + ".mat");
    }

    // Partículas aditivas (brillan sin oscurecer).
    static Material ParticleMaterial(string name, Texture tex)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f);   // transparente
        m.SetFloat("_Blend", 2f);     // aditivo
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        return SaveAsset(m, MatFolder + "/" + name + ".mat");
    }

    static T SaveAsset<T>(T asset, string path) where T : Object
    {
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static Texture2D SaveTexture(string path, Texture2D tex, TextureWrapMode wrap, bool mipmaps)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.wrapMode = wrap;
        importer.mipmapEnabled = mipmaps;
        importer.anisoLevel = mipmaps ? 8 : 1;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Rejilla luminosa del suelo (también se usa como mapa de emisión).
    static Texture2D GenerateGrid(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Min(x, size - 1 - x), dy = Mathf.Min(y, size - 1 - y);
                float d = Mathf.Min(dx, dy);
                float line = Mathf.Clamp01(1.5f - d) + 0.25f * Mathf.Clamp01(5f - d) * 0.5f;
                // Cruz tenue en el centro de cada baldosa.
                float c = Mathf.Min(Mathf.Abs(x - size / 2f), Mathf.Abs(y - size / 2f));
                line += 0.12f * Mathf.Clamp01(1f - c);
                float v = Mathf.Clamp01(line);
                px[y * size + x] = new Color(v * 0.6f + 0.04f, v * 0.85f + 0.05f, v + 0.08f, 1f) * (0.15f + 0.85f * v);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static Texture2D GenerateDot(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        float h = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2(x + 0.5f - h, y + 0.5f - h).magnitude / h;
                float a = Mathf.Clamp01(1f - r);
                px[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // Cristal facetado: prisma hexagonal con punta arriba y abajo (caras planas).
    static Mesh GenerateCrystalMesh()
    {
        const int sides = 6;
        const float radius = 0.42f, prismTop = 0.32f, prismBottom = -0.32f, tipTop = 0.98f, tipBottom = -0.78f;
        var verts = new List<Vector3>();
        var tris = new List<int>();

        Vector3 Ring(int i, float y)
        {
            float a = (i % sides) / (float)sides * Mathf.PI * 2f;
            // Facetas un poco irregulares para que parezca tallado.
            float r = radius * (i % 2 == 0 ? 1f : 0.9f);
            return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        }

        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            Vector3 centroid = (a + b + c) / 3f;
            if (Vector3.Dot(n, centroid) < 0f) { Vector3 t = b; b = c; c = t; }   // normal hacia fuera
            int s = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
        }

        var top = new Vector3(0f, tipTop, 0f);
        var bottom = new Vector3(0f, tipBottom, 0f);
        for (int i = 0; i < sides; i++)
        {
            Vector3 b0 = Ring(i, prismBottom), b1 = Ring(i + 1, prismBottom);
            Vector3 t0 = Ring(i, prismTop), t1 = Ring(i + 1, prismTop);
            Tri(b0, t0, t1);
            Tri(b0, t1, b1);
            Tri(t0, top, t1);
            Tri(b0, b1, bottom);
        }

        var mesh = new Mesh { name = "CrystalMesh" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    // =====================================================================
    // Escena
    // =====================================================================

    static void SetupEnvironment()
    {
        string skyPath = AssetDatabase.GUIDToAssetPath(LevelSkyboxGuid);
        Material sky = string.IsNullOrEmpty(skyPath) ? null : AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.13f, 0.15f, 0.24f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.022f;
        RenderSettings.fogColor = new Color(0.03f, 0.05f, 0.1f);
    }

    static Transform CreateCamera()
    {
        var rig = new GameObject("CameraRig");
        rig.transform.position = new Vector3(0f, 3.1f, -7.4f);
        rig.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.45f, 0.4f) - rig.transform.position);

        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.transform.SetParent(rig.transform, false);
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 200f;
        cam.allowHDR = true;
        cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        cam.backgroundColor = PeruTheme.Night;
        go.AddComponent<AudioListener>();
        go.AddComponent<PhysicsRaycaster>();   // clic en los cristales vía EventSystem

        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        return rig.transform;
    }

    static void CreateLights()
    {
        var root = new GameObject("Luces").transform;

        Light sun = NewLight(root, "LuzAmbiente", LightType.Directional, new Color(0.65f, 0.72f, 1f), 0.45f);
        sun.transform.rotation = Quaternion.Euler(48f, -25f, 0f);
        sun.shadows = LightShadows.Soft;

        Light key = NewLight(root, "FocoCentral", LightType.Spot, new Color(0.85f, 0.92f, 1f), 3.5f);
        key.transform.position = new Vector3(0f, 7.5f, -3.5f);
        key.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.8f, 0f) - key.transform.position);
        key.range = 18f;
        key.spotAngle = 80f;
        key.innerSpotAngle = 50f;
        key.shadows = LightShadows.Soft;

        for (int s = -1; s <= 1; s += 2)
        {
            Light rim = NewLight(root, s < 0 ? "LuzLateralIzq" : "LuzLateralDer", LightType.Point, Cyan, 1.8f);
            rim.transform.position = new Vector3(5.5f * s, 2.5f, 4.5f);
            rim.range = 9f;
            AddPulse(rim.gameObject, rim, 1.2f, 2.4f, 0.9f);
        }

        Light red = NewLight(root, "LuzEmblema", LightType.Point, PeruTheme.Red, 1.6f);
        red.transform.position = new Vector3(0f, 4.2f, 7.2f);
        red.range = 6f;
        AddPulse(red.gameObject, red, 1f, 2.2f, 0.6f);
    }

    static Light NewLight(Transform parent, string name, LightType type, Color color, float intensity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var l = go.AddComponent<Light>();
        l.type = type;
        l.color = color;
        l.intensity = intensity;
        return l;
    }

    static void AddPulse(GameObject go, Light light, float min, float max, float speed)
    {
        var motion = go.AddComponent<AmbientMotion>();
        var so = new SerializedObject(motion);
        so.FindProperty("rotationSpeed").vector3Value = Vector3.zero;
        so.FindProperty("pulseLight").objectReferenceValue = light;
        so.FindProperty("pulseMin").floatValue = min;
        so.FindProperty("pulseMax").floatValue = max;
        so.FindProperty("pulseSpeed").floatValue = speed;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreatePostProcess(VolumeProfile profile)
    {
        var go = new GameObject("PostProcesado");
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
    }

    // Sala de entrenamiento: suelo con rejilla y pared curva de paneles con franjas de luz.
    static void CreateRoom()
    {
        var room = new GameObject("SalaEntrenamiento").transform;

        GameObject floor = Prim(PrimitiveType.Plane, room, "Suelo", Vector3.zero, Vector3.one * 4f, floorMat);
        floor.GetComponent<Renderer>().receiveShadows = true;

        // Pared curva (arco de paneles alrededor del área de juego).
        var center = new Vector3(0f, 0f, 1f);
        const float radius = 8f;
        for (int i = -4; i <= 4; i++)
        {
            float angle = i * 17.5f;
            float rad = angle * Mathf.Deg2Rad;
            var panelRoot = new GameObject("Panel" + (i + 4)).transform;
            panelRoot.SetParent(room, false);
            panelRoot.position = center + new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
            panelRoot.rotation = Quaternion.Euler(0f, angle, 0f);

            Prim(PrimitiveType.Cube, panelRoot, "Cuerpo", new Vector3(0f, 3f, 0f), new Vector3(2.5f, 6f, 0.3f), panelMat);
            Prim(PrimitiveType.Cube, panelRoot, "FranjaInferior", new Vector3(0f, 0.9f, -0.17f),
                new Vector3(2.2f, 0.05f, 0.04f), stripMat);
            Prim(PrimitiveType.Cube, panelRoot, "FranjaSuperior", new Vector3(0f, 5.3f, -0.17f),
                new Vector3(2.2f, 0.05f, 0.04f), stripMat);
            Prim(PrimitiveType.Cube, panelRoot, "Junta", new Vector3(1.24f, 3f, -0.17f),
                new Vector3(0.04f, 5.8f, 0.04f), darkMetalMat);

            if (i == 0)
            {
                // Emblema de la Base Espacial del Perú + franja rojo | blanco | rojo.
                Prim(PrimitiveType.Quad, panelRoot, "Emblema", new Vector3(0f, 3.9f, -0.17f),
                    new Vector3(2.1f, 2.1f, 1f), emblemMat);
                for (int k = -1; k <= 1; k++)
                    Prim(PrimitiveType.Cube, panelRoot, "FranjaPeru" + (k + 1), new Vector3(k * 0.5f, 2.55f, -0.17f),
                        new Vector3(0.5f, 0.1f, 0.04f), k == 0 ? whiteMat : redMat);
            }
            else if (Mathf.Abs(i) == 1)
            {
                Prim(PrimitiveType.Quad, panelRoot, "Bandera", new Vector3(0f, 3.9f, -0.17f),
                    new Vector3(1.5f, 1f, 1f), flagMat);
            }
        }

        // Anillos luminosos en el suelo bajo cada pedestal.
        foreach (CrystalDef def in Crystals)
        {
            Material ring = GlowMaterial("AnilloSuelo" + def.id, def.color, 1.2f);
            Prim(PrimitiveType.Cylinder, room, "AnilloSuelo" + def.id, new Vector3(def.x, 0.005f, 0f),
                new Vector3(2.5f, 0.005f, 2.5f), ring);
            Prim(PrimitiveType.Cylinder, room, "AnilloSueloInterior" + def.id, new Vector3(def.x, 0.012f, 0f),
                new Vector3(2.3f, 0.005f, 2.3f), floorRingDarkMat);
        }

        // Línea de luz que une los pedestales.
        Prim(PrimitiveType.Cube, room, "LineaCentral", new Vector3(0f, 0.01f, 0f), new Vector3(5.4f, 0.01f, 0.05f), stripMat);
    }

    static void CreateDust()
    {
        var go = new GameObject("PolvoEstelar");
        go.transform.position = new Vector3(0f, 3.2f, 2f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.startColor = new Color(0.6f, 0.85f, 1f, 0.55f);
        main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 25f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(16f, 6.5f, 10f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.15f;
        noise.frequency = 0.25f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeInOut(Color.white);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = particleMat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
    }

    static ParticleSystem.MinMaxGradient FadeInOut(Color c)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f),
                    new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    // =====================================================================
    // Pedestal + cristal (se guarda como prefab)
    // =====================================================================

    static Crystal CreatePedestal(int index, CrystalDef def)
    {
        Material crystalMat = CrystalMaterial(def.id, def.color);
        Material rimMat = GlowMaterial("BordeLuz" + def.id, def.color, 1.5f);

        var root = new GameObject("Pedestal" + def.id);
        root.transform.position = new Vector3(def.x, 0f, 0f);
        Transform t = root.transform;

        // Plataforma metálica escalonada.
        Prim(PrimitiveType.Cylinder, t, "Base", new Vector3(0f, 0.12f, 0f), new Vector3(1.8f, 0.12f, 1.8f), darkMetalMat);
        Prim(PrimitiveType.Cylinder, t, "BaseBorde", new Vector3(0f, 0.25f, 0f), new Vector3(1.6f, 0.02f, 1.6f), metalMat);
        Prim(PrimitiveType.Cylinder, t, "Columna", new Vector3(0f, 0.6f, 0f), new Vector3(1.05f, 0.35f, 1.05f), metalMat);
        for (int k = 0; k < 4; k++)
        {
            float a = k * 90f + 45f;
            var p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.6f, 0.53f);
            GameObject fin = Prim(PrimitiveType.Cube, t, "Refuerzo" + k, p, new Vector3(0.12f, 0.6f, 0.12f), darkMetalMat);
            fin.transform.localRotation = Quaternion.Euler(0f, a, 0f);
        }
        GameObject rim = Prim(PrimitiveType.Cylinder, t, "BordeLuminoso", new Vector3(0f, 0.97f, 0f),
            new Vector3(1.42f, 0.02f, 1.42f), rimMat);
        Prim(PrimitiveType.Cylinder, t, "Plataforma", new Vector3(0f, 0.99f, 0f), new Vector3(1.32f, 0.03f, 1.32f), metalMat);

        // Anillo flotante que gira alrededor del cristal.
        var ringPivot = new GameObject("AnilloOrbital").transform;
        ringPivot.SetParent(t, false);
        ringPivot.localPosition = new Vector3(0f, 1.25f, 0f);
        for (int k = 0; k < 12; k++)
        {
            float a = k * 30f * Mathf.Deg2Rad;
            GameObject seg = Prim(PrimitiveType.Cube, ringPivot, "Segmento" + k,
                new Vector3(Mathf.Cos(a) * 0.62f, 0f, Mathf.Sin(a) * 0.62f), new Vector3(0.18f, 0.025f, 0.04f), rimMat);
            seg.transform.localRotation = Quaternion.Euler(0f, -k * 30f + 90f, 0f);
        }
        var spin = ringPivot.gameObject.AddComponent<AmbientMotion>();
        var spinSo = new SerializedObject(spin);
        spinSo.FindProperty("rotationSpeed").vector3Value = new Vector3(0f, index % 2 == 0 ? 35f : -35f, 0f);
        spinSo.ApplyModifiedPropertiesWithoutUndo();

        // Cristal (MeshCollider convexo para recibir el clic).
        var crystalGo = new GameObject("Cristal" + def.id, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        crystalGo.transform.SetParent(t, false);
        crystalGo.transform.localPosition = new Vector3(0f, 1.95f, 0f);
        crystalGo.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
        crystalGo.GetComponent<MeshFilter>().sharedMesh = crystalMesh;
        var crystalRenderer = crystalGo.GetComponent<MeshRenderer>();
        crystalRenderer.sharedMaterial = crystalMat;
        var mc = crystalGo.GetComponent<MeshCollider>();
        mc.sharedMesh = crystalMesh;
        mc.convex = true;

        // Luz propia del cristal.
        Light glow = NewLight(t, "LuzCristal", LightType.Point, def.color, 0.6f);
        glow.transform.localPosition = new Vector3(0f, 1.9f, -0.3f);
        glow.range = 4.5f;

        // Chispas (brillo ambiental + ráfaga al iluminarse).
        ParticleSystem sparks = CreateSparks(t, def.color);

        var crystal = crystalGo.AddComponent<Crystal>();
        var so = new SerializedObject(crystal);
        so.FindProperty("index").intValue = index;
        so.FindProperty("displayName").stringValue = def.name;
        so.FindProperty("glowColor").colorValue = def.color;
        so.FindProperty("crystalRenderer").objectReferenceValue = crystalRenderer;
        so.FindProperty("rimRenderer").objectReferenceValue = rim.GetComponent<Renderer>();
        so.FindProperty("glowLight").objectReferenceValue = glow;
        so.FindProperty("sparks").objectReferenceValue = sparks;
        so.ApplyModifiedPropertiesWithoutUndo();

        string prefabPath = PrefabFolder + "/CrystalPedestal_" + def.id + ".prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
        return crystal;
    }

    static ParticleSystem CreateSparks(Transform parent, Color color)
    {
        var go = new GameObject("Chispas");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 1.95f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        main.startColor = Color.Lerp(color, Color.white, 0.35f);
        main.gravityModifier = -0.15f;
        main.maxParticles = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 4f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeInOut(Color.white);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = particleMat;
        return ps;
    }

    static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 localScale,
        Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());   // sólo los cristales reciben clics
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static void SetArray(SerializedProperty prop, Object[] values)
    {
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    // =====================================================================
    // Interfaz
    // =====================================================================

    static MemoryGameUI CreateUI(out Button play, out Button instrBack, out Button retry, out Button defeatBack,
        out Button hudBack)
    {
        MainMenuBuilder.CreateEventSystem(out EventSystem eventSystem);
        Canvas canvas = PeruUI.CreateCanvas("MemoriaCanvas", 10);
        Transform root = canvas.transform;
        var ui = canvas.gameObject.AddComponent<MemoryGameUI>();

        // ---------- Marcador ----------
        RectTransform hud = FullScreen(root, "Marcador", null);

        RectTransform bar = PeruUI.Rect(hud, "BarraSuperior", new Vector2(0.5f, 1f), Vector2.zero);
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.sizeDelta = new Vector2(0f, 130f);
        var barImg = bar.gameObject.AddComponent<Image>();
        barImg.color = new Color(PeruTheme.Night.r, PeruTheme.Night.g, PeruTheme.Night.b, 0.7f);
        barImg.raycastTarget = false;
        PeruUI.ScreenStripes(bar, 6f);

        Text level = PeruUI.Label(hud, "Nivel", "NIVEL 1 / 10", new Vector2(0f, 1f), new Vector2(520f, 70f),
            PeruTheme.TitleFont, 46, PeruTheme.White, TextAnchor.MiddleLeft);
        level.rectTransform.pivot = new Vector2(0f, 1f);
        level.rectTransform.anchoredPosition = new Vector2(40f, -28f);
        PeruUI.AddTitleEffects(level.gameObject, 2f);

        Text status = PeruUI.Label(hud, "Estado", "OBSERVA LA SECUENCIA", new Vector2(0.5f, 1f), new Vector2(900f, 56f),
            PeruTheme.HeadingFont, 34, PeruTheme.White);
        status.rectTransform.pivot = new Vector2(0.5f, 1f);
        status.rectTransform.anchoredPosition = new Vector2(0f, -22f);
        PeruUI.AddSoftShadow(status.gameObject);

        Text progress = PeruUI.Label(hud, "Progreso", "", new Vector2(0.5f, 1f), new Vector2(400f, 44f),
            PeruTheme.BodyBoldFont, 32, PeruTheme.TextSoft);
        progress.rectTransform.pivot = new Vector2(0.5f, 1f);
        progress.rectTransform.anchoredPosition = new Vector2(0f, -78f);

        Text livesLabel = PeruUI.Label(hud, "OportunidadesTexto", "OPORTUNIDADES", new Vector2(1f, 1f),
            new Vector2(300f, 36f), PeruTheme.HeadingFont, 22, PeruTheme.TextSoft, TextAnchor.MiddleRight);
        livesLabel.rectTransform.pivot = new Vector2(1f, 1f);
        livesLabel.rectTransform.anchoredPosition = new Vector2(-40f, -22f);

        var lifeIcons = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            RectTransform icon = PeruUI.Rect(hud, "Oportunidad" + (i + 1), new Vector2(1f, 1f), new Vector2(46f, 46f));
            icon.pivot = new Vector2(1f, 1f);
            icon.anchoredPosition = new Vector2(-40f - (2 - i) * 60f, -64f);
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = PeruTheme.RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = PeruTheme.Red;
            img.raycastTarget = false;
            var outline = icon.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            lifeIcons[i] = img;
        }

        hudBack = PeruUI.Button(hud, "ElegirPruebaBoton", "Elegir prueba", new Vector2(0f, 0f), new Vector2(330f, 76f));
        var hbrt = (RectTransform)hudBack.transform;
        hbrt.pivot = Vector2.zero;
        hbrt.anchoredPosition = new Vector2(30f, 30f);
        hudBack.GetComponentInChildren<Text>().fontSize = 28;
        hudBack.navigation = new Navigation { mode = Navigation.Mode.None };
        Text esc = PeruUI.Label(hud, "EscTexto", "o pulsa ESC", new Vector2(0f, 0f), new Vector2(330f, 30f),
            PeruTheme.BodyFont, 22, PeruTheme.TextSoft);
        esc.rectTransform.pivot = Vector2.zero;
        esc.rectTransform.anchoredPosition = new Vector2(30f, 108f);

        PeruUI.Label(hud, "Ayuda", "Haz clic en los cristales en el mismo orden en que se iluminaron",
            new Vector2(0.5f, 0f), new Vector2(1200f, 40f), PeruTheme.BodyFont, 26, PeruTheme.TextSoft)
            .rectTransform.anchoredPosition = new Vector2(0f, 40f);

        // ---------- Aviso correcto / incorrecto ----------
        RectTransform fb = PeruUI.Rect(root, "Aviso", new Vector2(0.5f, 0.24f), new Vector2(1000f, 140f));
        var fbGroup = fb.gameObject.AddComponent<CanvasGroup>();
        fbGroup.alpha = 0f;
        fbGroup.blocksRaycasts = false;
        fbGroup.interactable = false;
        var fbBg = fb.gameObject.AddComponent<Image>();
        fbBg.sprite = PeruTheme.RoundedSprite;
        fbBg.type = Image.Type.Sliced;
        fbBg.color = new Color(0.1f, 0.55f, 0.3f, 0.92f);
        fbBg.raycastTarget = false;
        var fbOutline = fb.gameObject.AddComponent<Outline>();
        fbOutline.effectColor = new Color(1f, 1f, 1f, 0.8f);
        fbOutline.effectDistance = new Vector2(3f, -3f);
        Text fbText = PeruUI.Label(fb, "Texto", "¡SECUENCIA CORRECTA!", new Vector2(0.5f, 0.5f), new Vector2(960f, 130f),
            PeruTheme.HeadingFont, 44, PeruTheme.White);
        PeruUI.AddSoftShadow(fbText.gameObject);

        // ---------- Instrucciones ----------
        RectTransform instr = FullScreen(root, "Instrucciones", new Color(0.01f, 0.015f, 0.04f, 0.6f));
        RectTransform ip = PeruUI.Panel(instr, "Panel", new Vector2(0.5f, 0.5f), new Vector2(1360f, 820f));
        PeruUI.Emblem(ip, new Vector2(0.5f, 0.9f), 120f);
        Text ititle = PeruUI.Label(ip, "Titulo", "PRUEBA DE MEMORIA ESPACIAL", new Vector2(0.5f, 0.76f),
            new Vector2(1300f, 90f), PeruTheme.TitleFont, 58, PeruTheme.White);
        PeruUI.AddTitleEffects(ititle.gameObject, 3f);
        PeruUI.Label(ip, "Texto",
            "1.  Observa cómo se iluminan los cristales <color=#4DA6FF>AZUL</color>, " +
            "<color=#4DFF88>VERDE</color> y <color=#FF9A33>NARANJA</color>. Cada uno tiene su propio sonido.\n" +
            "2.  Repite la secuencia haciendo clic en los cristales, en el mismo orden.\n" +
            "3.  Si aciertas, pasas al siguiente nivel: la secuencia es más larga y más rápida.\n" +
            "4.  Si te equivocas, pierdes una oportunidad. Tienes <color=#FF5A66>3 oportunidades</color>.\n" +
            "5.  Supera el <b>nivel 10</b> para aprobar la prueba.",
            new Vector2(0.5f, 0.43f), new Vector2(1180f, 380f), PeruTheme.BodyFont, 34, PeruTheme.White,
            TextAnchor.MiddleLeft, wrap: true);
        instrBack = PeruUI.Button(ip, "VolverBoton", "Volver", new Vector2(0.3f, 0.1f));
        play = PeruUI.Button(ip, "JugarBoton", "Jugar", new Vector2(0.7f, 0.1f));
        MainMenuBuilder.SetVerticalNavigation(play, instrBack);
        eventSystem.firstSelectedGameObject = play.gameObject;

        // ---------- Derrota ----------
        RectTransform defeat = FullScreen(root, "Derrota", new Color(0.01f, 0.015f, 0.04f, 0.85f));
        PeruUI.ScreenStripes(defeat, 14f);
        RectTransform dp = PeruUI.Panel(defeat, "Panel", new Vector2(0.5f, 0.5f), new Vector2(1240f, 720f));
        PeruUI.Emblem(dp, new Vector2(0.5f, 0.84f), 150f);
        PeruUI.Label(dp, "Mision", "PRUEBA DE MEMORIA", new Vector2(0.5f, 0.68f), new Vector2(1100f, 50f),
            PeruTheme.HeadingFont, 32, PeruTheme.Red);
        Text dtitle = PeruUI.Label(dp, "Titulo", "PRUEBA NO SUPERADA", new Vector2(0.5f, 0.56f), new Vector2(1200f, 100f),
            PeruTheme.TitleFont, 70, PeruTheme.White);
        PeruUI.AddTitleEffects(dtitle.gameObject, 3f);
        Text detail = PeruUI.Label(dp, "Detalle", "Llegaste al nivel 1.", new Vector2(0.5f, 0.38f),
            new Vector2(1050f, 120f), PeruTheme.BodyBoldFont, 38, PeruTheme.TextSoft, TextAnchor.MiddleCenter, wrap: true);
        retry = PeruUI.Button(dp, "ReintentarBoton", "Reintentar", new Vector2(0.3f, 0.14f));
        defeatBack = PeruUI.Button(dp, "VolverBoton", "Volver", new Vector2(0.7f, 0.14f));
        MainMenuBuilder.SetVerticalNavigation(retry, defeatBack);

        // Estado inicial: sólo instrucciones (MemoryGameUI lo vuelve a fijar al empezar).
        hud.gameObject.SetActive(false);
        defeat.gameObject.SetActive(false);

        var so = new SerializedObject(ui);
        so.FindProperty("instructionsPanel").objectReferenceValue = instr.gameObject;
        so.FindProperty("playButton").objectReferenceValue = play;
        so.FindProperty("hud").objectReferenceValue = hud.gameObject;
        so.FindProperty("levelText").objectReferenceValue = level;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("progressText").objectReferenceValue = progress;
        SetArray(so.FindProperty("lifeIcons"), lifeIcons);
        so.FindProperty("feedbackGroup").objectReferenceValue = fbGroup;
        so.FindProperty("feedbackBackground").objectReferenceValue = fbBg;
        so.FindProperty("feedbackText").objectReferenceValue = fbText;
        so.FindProperty("defeatPanel").objectReferenceValue = defeat.gameObject;
        so.FindProperty("defeatDetail").objectReferenceValue = detail;
        so.FindProperty("retryButton").objectReferenceValue = retry;
        so.ApplyModifiedPropertiesWithoutUndo();
        return ui;
    }

    // Capa a pantalla completa; con color bloquea los clics a los cristales (paneles modales).
    static RectTransform FullScreen(Transform parent, string name, Color? overlay)
    {
        RectTransform rt = PeruUI.Rect(parent, name, Vector2.zero, Vector2.zero);
        PeruUI.Stretch(rt);
        if (overlay.HasValue)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = overlay.Value;
        }
        return rt;
    }
}
