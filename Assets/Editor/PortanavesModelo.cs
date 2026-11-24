using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// NAVE NODRIZA con el modelo CF-782 (Assets/Models/Nave_Nodriza/CF782.glb, o el .obj como respaldo).
// El modelo no tiene hangar: se le añade una BAHÍA DE LANZAMIENTO bajo el casco, abierta hacia la proa.
// La nave del jugador sale hacia delante por debajo de la nodriza.
//
// Medidas en "largos de nave" (U), con la boca de la bahía en el origen y la salida hacia +Z:
//   nodriza de 24 U de largo · bahía de 2,6 x 2 x 6 U · túnel interior de 2 x 1 x 5 U.
// Los materiales del modelo se reemplazan por materiales URP propios (mismos colores, con un leve brillo
// para que se vea en el espacio oscuro). Así no dependen de los shaders del importador glTF en las builds.
public static partial class PortanavesBuilder
{
    const string ModelGlb = "Assets/Models/Nave_Nodriza/CF782.glb";
    const string ModelObj = "Assets/Models/Nave_Nodriza/CF782.obj";
    const string ModelMatFolder = "Assets/Defensa/Portanaves/Materials/CF782";

    const float CarrierLengthU = 24f;   // largo de la nodriza
    const float NoseAheadU = 6.3f;      // la proa sobresale por delante de la bahía
    const float ModelBottomU = 0.25f;   // altura de lo más bajo del modelo (propulsores) sobre la boca

    static GameObject LoadModel()
    {
        var glb = AssetDatabase.LoadAssetAtPath<GameObject>(ModelGlb);
        if (glb != null && glb.GetComponentsInChildren<Renderer>(true).Length > 0) return glb;
        return AssetDatabase.LoadAssetAtPath<GameObject>(ModelObj);
    }

    static void BuildFromModel(GameObject modelAsset)
    {
        CreateMaterials();
        var root = new GameObject("Portanaves");

        // ---------- Modelo ----------
        var model = (GameObject)Object.Instantiate(modelAsset);
        model.name = "Modelo_CF782";
        model.transform.SetParent(root.transform, false);
        ApplyModelMaterials(model);
        FitModel(model.transform);
        AddModelColliders(model);

        // ---------- Bahía bajo el casco ----------
        BuildVentralBay(root.transform);
        BuildBayMarkings(root.transform);
        BuildShotAnchors(root.transform);

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("Nave nodriza (modelo CF-782) creada en " + PrefabPath);
    }

    // Escala el modelo a 24 U y lo coloca: centrado, proa 6,3 U por delante de la boca y la parte
    // más baja justo encima de la bahía (que queda pegada bajo el casco).
    static void FitModel(Transform model)
    {
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        model.localScale = Vector3.one;
        Bounds b = RendererBounds(model);
        float k = CarrierLengthU / Mathf.Max(0.01f, b.size.z);
        model.localScale = Vector3.one * k;

        b = RendererBounds(model);
        model.localPosition += new Vector3(-b.center.x, ModelBottomU - b.min.y, NoseAheadU - b.max.z);
    }

    static Bounds RendererBounds(Transform t)
    {
        Renderer[] rs = t.GetComponentsInChildren<Renderer>(true);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // ---------- Materiales del modelo ----------

    static void ApplyModelMaterials(GameObject model)
    {
        Directory.CreateDirectory(ModelMatFolder);
        var mats = new Dictionary<string, Material>
        {
            ["armor"] = ModelMat("armor", new Color(0.643f, 0.659f, 0.667f), 0.35f, 0.5f, 0.2f),
            ["light"] = ModelMat("light", new Color(0.784f, 0.792f, 0.776f), 0.3f, 0.5f, 0.22f),
            ["dark"] = ModelMat("dark", new Color(0.169f, 0.192f, 0.22f), 0.35f, 0.45f, 0.12f),
            ["steel"] = ModelMat("steel", new Color(0.337f, 0.365f, 0.388f), 0.5f, 0.5f, 0.15f),
            ["red"] = ModelMat("red", new Color(0.588f, 0.145f, 0.114f), 0.25f, 0.5f, 0.25f),
            ["gold"] = ModelMat("gold", new Color(0.725f, 0.529f, 0.212f), 0.6f, 0.55f, 0.2f),
            ["glass"] = ModelMat("glass", new Color(0.09f, 0.18f, 0.231f), 0.1f, 0.9f, 0.25f),
            ["lamp"] = ModelGlow("lamp", new Color(1f, 0.855f, 0.557f), new Color(1f, 0.55f, 0.16f) * 2.6f),
            ["blue"] = ModelGlow("blue", new Color(0.333f, 0.69f, 1f), new Color(0.15f, 0.55f, 1f) * 3.2f),
        };

        Transform glowGroup = null;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            var shared = r.sharedMaterials;
            for (int i = 0; i < shared.Length; i++)
            {
                string key = MaterialKey(shared[i] != null ? shared[i].name : r.gameObject.name, r.gameObject.name);
                if (key != null) shared[i] = mats[key];
            }
            r.sharedMaterials = shared;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Los motores (material "blue") van al grupo EngineGlow: DefensaLaunch los hace pulsar.
            if (MaterialKey(r.gameObject.name, r.gameObject.name) == "blue" ||
                (shared.Length == 1 && shared[0] == mats["blue"]))
            {
                if (glowGroup == null)
                {
                    glowGroup = new GameObject("EngineGlow").transform;
                    glowGroup.SetParent(model.transform, false);
                }
                r.transform.SetParent(glowGroup, true);
            }
        }
    }

    // "armor", "armor (Instance)", "Mat_armor"… → "armor".
    static string MaterialKey(string name, string fallback)
    {
        string[] keys = { "armor", "light", "dark", "steel", "red", "gold", "glass", "lamp", "blue" };
        foreach (string source in new[] { name, fallback })
        {
            string n = (source ?? "").ToLowerInvariant();
            foreach (string k in keys)
                if (n == k || n.StartsWith(k + " ") || n.StartsWith(k + "_") || n.EndsWith("_" + k) || n.StartsWith(k + "("))
                    return k;
        }
        return null;
    }

    static Material ModelMat(string name, Color color, float metallic, float smoothness, float glow) =>
        LitAt(ModelMatFolder, name, color, metallic, smoothness, color * glow);

    static Material ModelGlow(string name, Color color, Color emission) =>
        LitAt(ModelMatFolder, name, color, 0f, 0.5f, emission);

    static Material LitAt(string folder, string name, Color baseColor, float metallic, float smoothness, Color emission)
    {
        string path = folder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        m.SetColor("_BaseColor", baseColor);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", emission);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (isNew) AssetDatabase.CreateAsset(m, path);
        else EditorUtility.SetDirty(m);
        return m;
    }

    // Colisión con la forma real del casco (la nave del jugador no recibe daño al chocar).
    static void AddModelColliders(GameObject model)
    {
        foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || (mf.transform.parent != null && mf.transform.parent.name == "EngineGlow")) continue;
            if (mf.GetComponent<Collider>() != null) continue;
            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }
    }

    // ---------- Bahía de lanzamiento ----------
    // Túnel interior: x -1..1 · y -0,5..0,5 · z -5..0 (boca en el origen, abierta hacia +Z).
    static void BuildVentralBay(Transform root)
    {
        Material armor = AssetDatabase.LoadAssetAtPath<Material>(ModelMatFolder + "/armor.mat");
        Material dark = AssetDatabase.LoadAssetAtPath<Material>(ModelMatFolder + "/dark.mat");
        Material steel = AssetDatabase.LoadAssetAtPath<Material>(ModelMatFolder + "/steel.mat");

        Transform bay = Group(root, "Hangar");

        // Casco de la bahía (sólido).
        Box(bay, "ParedIzquierda", new Vector3(-1.15f, 0f, -3f), new Vector3(0.3f, 1f, 6f), armor);
        Box(bay, "ParedDerecha", new Vector3(1.15f, 0f, -3f), new Vector3(0.3f, 1f, 6f), armor);
        Box(bay, "Techo", new Vector3(0f, 0.85f, -3f), new Vector3(2.6f, 0.7f, 6f), armor);
        Box(bay, "Piso", new Vector3(0f, -0.8f, -3f), new Vector3(2.6f, 0.6f, 6f), armor);
        Box(bay, "Fondo", new Vector3(0f, 0f, -5.5f), new Vector3(2f, 1f, 1f), armor);
        Box(bay, "Quilla", new Vector3(0f, -1.18f, -3.4f), new Vector3(0.7f, 0.18f, 4.6f), dark);
        Box(bay, "CantoInferior", new Vector3(0f, -1.06f, -0.2f), new Vector3(2.4f, 0.12f, 0.4f), steel, false);
        Box(bay, "CantoSuperior", new Vector3(0f, 1.15f, -0.2f), new Vector3(2.4f, 0.12f, 0.4f), steel, false);

        // Interior oscuro.
        Box(bay, "SueloInterior", new Vector3(0f, -0.49f, -2.5f), new Vector3(1.98f, 0.02f, 5f), interior, false);
        Box(bay, "TechoInterior", new Vector3(0f, 0.49f, -2.5f), new Vector3(1.98f, 0.02f, 5f), interior, false);
        Box(bay, "MuroInteriorIzq", new Vector3(-0.99f, 0f, -2.5f), new Vector3(0.02f, 0.98f, 5f), interior, false);
        Box(bay, "MuroInteriorDer", new Vector3(0.99f, 0f, -2.5f), new Vector3(0.02f, 0.98f, 5f), interior, false);
        Box(bay, "MuroInteriorFondo", new Vector3(0f, 0f, -4.98f), new Vector3(1.98f, 0.98f, 0.02f), interior, false);
        Box(bay, "FranjaFondo", new Vector3(0f, 0.15f, -4.96f), new Vector3(1.8f, 0.15f, 0.02f), redPaint, false);

        // Luces de pista (se encienden en cadena hacia la salida) y del techo.
        Transform runway = Group(bay, "RunwayLights");
        int n = 0;
        for (float z = -4.6f; z <= -0.2f; z += 0.5f, n++)
        {
            Box(runway, $"Luz{n:00}I", new Vector3(-0.6f, -0.475f, z), new Vector3(0.08f, 0.02f, 0.16f), glowWhite, false);
            Box(runway, $"Luz{n:00}D", new Vector3(0.6f, -0.475f, z), new Vector3(0.08f, 0.02f, 0.16f), glowWhite, false);
        }
        for (int i = 0; i < 3; i++)   // la primera queda delante de la cámara del plano interior
            Box(bay, "LuzTecho" + i, new Vector3(0f, 0.47f, -3.2f + i * 1.2f), new Vector3(1.2f, 0.02f, 0.08f), glowWhite, false);

        // Marco luminoso de la boca.
        Box(bay, "MarcoArriba", new Vector3(0f, 0.53f, 0.02f), new Vector3(2.12f, 0.06f, 0.05f), glowWhite, false);
        Box(bay, "MarcoAbajo", new Vector3(0f, -0.53f, 0.02f), new Vector3(2.12f, 0.06f, 0.05f), glowWhite, false);
        Box(bay, "MarcoIzquierda", new Vector3(-1.03f, 0f, 0.02f), new Vector3(0.06f, 1.12f, 0.05f), glowWhite, false);
        Box(bay, "MarcoDerecha", new Vector3(1.03f, 0f, 0.02f), new Vector3(0.06f, 1.12f, 0.05f), glowWhite, false);

        // Luces de alerta.
        Transform alerts = Group(bay, "AlertLights");
        Box(alerts, "AlertaExtIzq", new Vector3(-1.15f, 0.32f, 0.05f), new Vector3(0.2f, 0.2f, 0.08f), glowRed, false);
        Box(alerts, "AlertaExtDer", new Vector3(1.15f, 0.32f, 0.05f), new Vector3(0.2f, 0.2f, 0.08f), glowRed, false);
        Box(alerts, "AlertaIntIzq", new Vector3(-0.96f, 0.32f, -0.6f), new Vector3(0.06f, 0.14f, 0.24f), glowRed, false);
        Box(alerts, "AlertaIntDer", new Vector3(0.96f, 0.32f, -0.6f), new Vector3(0.06f, 0.14f, 0.24f), glowRed, false);
        Box(alerts, "AlertaFondo", new Vector3(0f, 0.32f, -4.95f), new Vector3(0.4f, 0.1f, 0.05f), glowRed, false);

        // Compuertas: arriba se esconde en el techo y abajo en el piso.
        // DoorTopOpen / DoorBottomOpen marcan la posición abierta (las lee DefensaLaunch).
        Transform top = Box(bay, "DoorTop", new Vector3(0f, 0.25f, -0.1f), new Vector3(2f, 0.5f, 0.08f), door, false);
        Box(top, "FranjaRoja", new Vector3(0f, -0.4f, 0.6f), new Vector3(1f, 0.12f, 0.5f), redPaint, false);
        Transform bottom = Box(bay, "DoorBottom", new Vector3(0f, -0.25f, -0.1f), new Vector3(2f, 0.5f, 0.08f), door, false);
        Box(bottom, "FranjaRoja", new Vector3(0f, 0.4f, 0.6f), new Vector3(1f, 0.12f, 0.5f), redPaint, false);
        Marker(bay, "DoorTopOpen", new Vector3(0f, 0.8f, -0.1f));
        Marker(bay, "DoorBottomOpen", new Vector3(0f, -0.8f, -0.1f));
    }

    // Bandera y nombre en los costados de la bahía, y bandera sobre la boca.
    static void BuildBayMarkings(Transform root)
    {
        Transform marks = Group(root, "Insignias");
        for (int side = -1; side <= 1; side += 2)
        {
            string s = side < 0 ? "Izq" : "Der";
            float x = 1.31f * side;
            Box(marks, "Franja" + s, new Vector3(x, -0.62f, -3f), new Vector3(0.02f, 0.1f, 6f), redPaint, false);

            Transform flag = Group(marks, "Bandera" + s);
            flag.localPosition = new Vector3(x, 0.15f, -0.9f);
            flag.localRotation = Quaternion.Euler(0f, side < 0 ? 90f : -90f, 0f);
            Box(flag, "Rojo1", new Vector3(-0.3f, 0f, 0f), new Vector3(0.3f, 0.55f, 0.02f), redPaint, false);
            Box(flag, "Blanco", Vector3.zero, new Vector3(0.3f, 0.55f, 0.02f), glowWhite, false);
            Box(flag, "Rojo2", new Vector3(0.3f, 0f, 0f), new Vector3(0.3f, 0.55f, 0.02f), redPaint, false);

            WorldText(marks, "Nombre" + s, "BASE ESPACIAL DEL PERÚ", new Vector3(x * 1.005f, 0.15f, -3.6f),
                Quaternion.Euler(0f, side < 0 ? 90f : -90f, 0f), new Vector2(380f, 50f), 22);
        }

        Transform front = Group(marks, "BanderaFrontal");
        front.localPosition = new Vector3(0f, 0.85f, 0.02f);
        Box(front, "Rojo1", new Vector3(-0.3f, 0f, 0f), new Vector3(0.3f, 0.4f, 0.02f), redPaint, false);
        Box(front, "Blanco", Vector3.zero, new Vector3(0.3f, 0.4f, 0.02f), glowWhite, false);
        Box(front, "Rojo2", new Vector3(0.3f, 0f, 0f), new Vector3(0.3f, 0.4f, 0.02f), redPaint, false);
    }

    static void WorldText(Transform parent, string name, string content, Vector3 localPos, Quaternion rot,
        Vector2 sizePx, int fontSize)
    {
        var canvasGo = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(parent, false);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)canvasGo.transform;
        rt.sizeDelta = sizePx;
        rt.localScale = Vector3.one * 0.01f;
        rt.localPosition = localPos;
        rt.localRotation = rot;

        var textGo = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(canvasGo.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<Text>();
        text.text = content;
        text.font = PeruTheme.HeadingFont;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.75f, 0.06f, 0.12f);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.raycastTarget = false;
    }

    // Planos de cámara de DefensaLaunch para esta nodriza (posiciones en U, relativas a la boca).
    static void BuildShotAnchors(Transform root)
    {
        Transform shots = Group(root, "Planos");
        Marker(shots, "CamA_Desde", new Vector3(-8f, -3.2f, 12.5f));   // general de tres cuartos, desde abajo y delante
        Marker(shots, "CamA_Hasta", new Vector3(-6.8f, -2.6f, 10.5f));
        Marker(shots, "CamA_Mira", new Vector3(0f, 1.2f, -4f));
        Marker(shots, "CamB_Desde", new Vector3(0f, 0.16f, -4.65f));  // dentro de la bahía, detrás de la nave
        Marker(shots, "CamB_Mira", new Vector3(0f, 0.05f, 3f));
        Marker(shots, "CamC_Desde", new Vector3(3.5f, -1.1f, 4.5f));  // lateral: la nave sale bajo la proa
    }

    static Transform Marker(Transform parent, string name, Vector3 localPos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        return t;
    }
}
