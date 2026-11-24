using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Genera la NAVE NODRIZA de la misión final (Assets/Resources/Defensa/Portanaves.prefab).
// La usa DefensaLaunch: la nave del jugador sale de su hangar al empezar la misión.
//
// Medidas en "largos de nave" (1 = largo de la nave del jugador; DefensaLaunch la escala en el juego):
//   casco 22 x 4 x 6 · hangar en el costado (+Z), túnel de 3,2 x 1,8 x 5 · boca del hangar en el origen.
// Luces = materiales que brillan (sin luces reales) para que vaya fluido en web y móvil.
// El casco también brilla un poco: en el espacio de la misión casi no hay luz y se vería negro.
// Nombres que usa DefensaLaunch: DoorTop, DoorBottom, RunwayLights, AlertLights, EngineGlow.
//
// Hay dos versiones (las dos guardan el mismo prefab):
//  - Build(): el modelo CF-782 (Assets/Models/Nave_Nodriza) con una BAHÍA DE LANZAMIENTO bajo el casco
//    (PortanavesModelo.cs). Es la que se usa.
//  - BuildSimple(): la nodriza hecha con bloques y hangar en el costado (respaldo si falta el modelo).
public static partial class PortanavesBuilder
{
    public const string PrefabPath = "Assets/Resources/Defensa/Portanaves.prefab";
    const string MatFolder = "Assets/Defensa/Portanaves/Materials";

    static Material hullWhite, hullGray, hullDark, interior, redPaint, door;
    static Material glowWhite, glowRed, glowCyan, window;

    [MenuItem("Tools/RECLUTA/Misión final: nave nodriza (modelo CF-782)")]
    public static void Build()
    {
        GameObject model = LoadModel();
        if (model == null)
        {
            Debug.LogWarning("No se encontró el modelo CF-782 en Assets/Models/Nave_Nodriza: se crea la nodriza simple.");
            BuildSimple();
            return;
        }
        BuildFromModel(model);
    }

    [MenuItem("Tools/RECLUTA/Misión final: nave nodriza (versión simple)")]
    public static void BuildSimple()
    {
        CreateMaterials();

        var root = new GameObject("Portanaves");
        BuildHull(root.transform);
        BuildHangar(root.transform);
        BuildBow(root.transform);
        BuildEngines(root.transform);
        BuildBridge(root.transform);
        BuildTurrets(root.transform);
        BuildMarkings(root.transform);

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("Nave nodriza creada en " + PrefabPath);
    }

    // ---------- Partes ----------

    static void BuildHull(Transform root)
    {
        Transform hull = Group(root, "Casco");
        // Bloques alrededor del túnel del hangar (x -1,6..1,6 · y -0,9..0,9 · z -5..0).
        Box(hull, "CascoIzquierdo", new Vector3(-6.3f, 0f, -3f), new Vector3(9.4f, 4f, 6f), hullWhite);
        Box(hull, "CascoDerecho", new Vector3(6.3f, 0f, -3f), new Vector3(9.4f, 4f, 6f), hullWhite);
        Box(hull, "SobreHangar", new Vector3(0f, 1.45f, -3f), new Vector3(3.2f, 1.1f, 6f), hullWhite);
        Box(hull, "BajoHangar", new Vector3(0f, -1.45f, -3f), new Vector3(3.2f, 1.1f, 6f), hullWhite);
        Box(hull, "FondoHangar", new Vector3(0f, 0f, -5.5f), new Vector3(3.2f, 1.8f, 1f), hullWhite);

        // Cubierta superior e inferior más oscuras (volumen) y paneles.
        Box(hull, "Cubierta", new Vector3(0f, 2.06f, -3f), new Vector3(21.6f, 0.12f, 5.6f), hullGray, false);
        Box(hull, "Quilla", new Vector3(0f, -2.25f, -3f), new Vector3(18f, 0.5f, 3.6f), hullDark);
        float[] panelX = { -9f, -3.5f, 1.5f, 4.5f, 8f };
        for (int i = 0; i < panelX.Length; i++)
            Box(hull, "Panel" + i, new Vector3(panelX[i], 2.2f, i % 2 == 0 ? -1.6f : -4.4f),
                new Vector3(1.6f + (i % 3) * 0.5f, 0.2f, 1.2f), hullDark, false);
    }

    static void BuildHangar(Transform root)
    {
        Transform hangar = Group(root, "Hangar");

        // Interior oscuro (forro de paredes, techo y suelo) sin colisión.
        Box(hangar, "Suelo", new Vector3(0f, -0.88f, -2.5f), new Vector3(3.16f, 0.04f, 5f), interior, false);
        Box(hangar, "Techo", new Vector3(0f, 0.88f, -2.5f), new Vector3(3.16f, 0.04f, 5f), interior, false);
        Box(hangar, "ParedIzquierda", new Vector3(-1.58f, 0f, -2.5f), new Vector3(0.04f, 1.76f, 5f), interior, false);
        Box(hangar, "ParedDerecha", new Vector3(1.58f, 0f, -2.5f), new Vector3(0.04f, 1.76f, 5f), interior, false);
        Box(hangar, "ParedFondo", new Vector3(0f, 0f, -4.98f), new Vector3(3.16f, 1.76f, 0.04f), interior, false);
        // Franja roja al fondo y en el techo.
        Box(hangar, "FranjaFondo", new Vector3(0f, 0.2f, -4.95f), new Vector3(3f, 0.25f, 0.03f), redPaint, false);

        // Luces de pista: dos filas que se encienden en cadena hacia la salida.
        Transform runway = Group(hangar, "RunwayLights");
        int n = 0;
        for (float z = -4.6f; z <= -0.2f; z += 0.5f, n++)
        {
            Box(runway, $"Luz{n:00}I", new Vector3(-0.9f, -0.85f, z), new Vector3(0.12f, 0.03f, 0.22f), glowWhite, false);
            Box(runway, $"Luz{n:00}D", new Vector3(0.9f, -0.85f, z), new Vector3(0.12f, 0.03f, 0.22f), glowWhite, false);
        }
        // Luces del techo (fijas).
        for (int i = 0; i < 4; i++)
            Box(hangar, "LuzTecho" + i, new Vector3(0f, 0.85f, -4.2f + i * 1.2f), new Vector3(1.6f, 0.03f, 0.12f), glowWhite, false);

        // Marco luminoso de la boca del hangar.
        Box(hangar, "MarcoArriba", new Vector3(0f, 0.95f, 0.03f), new Vector3(3.4f, 0.08f, 0.06f), glowWhite, false);
        Box(hangar, "MarcoAbajo", new Vector3(0f, -0.95f, 0.03f), new Vector3(3.4f, 0.08f, 0.06f), glowWhite, false);
        Box(hangar, "MarcoIzquierda", new Vector3(-1.66f, 0f, 0.03f), new Vector3(0.08f, 1.98f, 0.06f), glowWhite, false);
        Box(hangar, "MarcoDerecha", new Vector3(1.66f, 0f, 0.03f), new Vector3(0.08f, 1.98f, 0.06f), glowWhite, false);

        // Luces de alerta (parpadean mientras se abren las compuertas).
        Transform alerts = Group(hangar, "AlertLights");
        Box(alerts, "AlertaExtIzq", new Vector3(-2.1f, 1.25f, 0.05f), new Vector3(0.3f, 0.3f, 0.1f), glowRed, false);
        Box(alerts, "AlertaExtDer", new Vector3(2.1f, 1.25f, 0.05f), new Vector3(0.3f, 0.3f, 0.1f), glowRed, false);
        Box(alerts, "AlertaIntIzq", new Vector3(-1.5f, 0.7f, -0.6f), new Vector3(0.1f, 0.2f, 0.3f), glowRed, false);
        Box(alerts, "AlertaIntDer", new Vector3(1.5f, 0.7f, -0.6f), new Vector3(0.1f, 0.2f, 0.3f), glowRed, false);
        Box(alerts, "AlertaFondo", new Vector3(0f, 0.6f, -4.9f), new Vector3(0.5f, 0.15f, 0.08f), glowRed, false);

        // Compuertas: cerradas tapan la boca; DefensaLaunch las abre hacia arriba y hacia abajo (1 unidad).
        Transform top = Box(hangar, "DoorTop", new Vector3(0f, 0.45f, -0.1f), new Vector3(3.2f, 0.9f, 0.1f), door, false);
        Box(top, "FranjaRoja", new Vector3(0f, -0.35f, 0.6f), new Vector3(1f, 0.12f, 0.4f), redPaint, false);
        Transform bottom = Box(hangar, "DoorBottom", new Vector3(0f, -0.45f, -0.1f), new Vector3(3.2f, 0.9f, 0.1f), door, false);
        Box(bottom, "FranjaRoja", new Vector3(0f, 0.35f, 0.6f), new Vector3(1f, 0.12f, 0.4f), redPaint, false);
    }

    static void BuildBow(Transform root)
    {
        Transform bow = Group(root, "Proa");
        Transform tip = Box(bow, "Punta", new Vector3(11f, 0f, -3f), new Vector3(4.2f, 3.6f, 4.2f), hullWhite);
        tip.localRotation = Quaternion.Euler(0f, 45f, 0f);
        Transform band = Box(bow, "PuntaFranja", new Vector3(11f, -1.2f, -3f), new Vector3(4.26f, 0.35f, 4.26f), redPaint, false);
        band.localRotation = Quaternion.Euler(0f, 45f, 0f);
        Box(bow, "Sensor", new Vector3(13.6f, 0.4f, -3f), new Vector3(0.6f, 0.3f, 0.3f), glowCyan, false);
    }

    static void BuildEngines(Transform root)
    {
        Transform engines = Group(root, "Motores");
        Transform glow = Group(engines, "EngineGlow");
        Vector2[] spots = { new Vector2(1f, -1.5f), new Vector2(-1f, -1.5f), new Vector2(1f, -4.5f), new Vector2(-1f, -4.5f) };
        for (int i = 0; i < spots.Length; i++)
        {
            Vector3 c = new Vector3(-11.7f, spots[i].x, spots[i].y);
            Transform body = Cylinder(engines, "Tobera" + i, c, 0.75f, 1.4f, hullGray);
            body.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Transform ring = Cylinder(engines, "Aro" + i, c + new Vector3(-0.5f, 0f, 0f), 0.85f, 0.2f, redPaint, false);
            ring.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Transform disc = Cylinder(glow, "Brillo" + i, c + new Vector3(-0.72f, 0f, 0f), 0.6f, 0.05f, glowCyan, false);
            disc.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
    }

    static void BuildBridge(Transform root)
    {
        Transform bridge = Group(root, "Puente");
        Box(bridge, "Base", new Vector3(-6f, 2.8f, -3f), new Vector3(3f, 1.6f, 2.4f), hullWhite);
        Box(bridge, "Cabina", new Vector3(-6f, 3.95f, -3f), new Vector3(2.2f, 0.8f, 3f), hullGray);
        Box(bridge, "Ventanas", new Vector3(-6f, 4f, -3f), new Vector3(2.25f, 0.2f, 3.05f), window, false);
        Box(bridge, "FranjaRoja", new Vector3(-6f, 3.3f, -3f), new Vector3(3.05f, 0.25f, 2.45f), redPaint, false);
        Cylinder(bridge, "Antena", new Vector3(-6.6f, 5.2f, -3f), 0.06f, 2f, hullGray, false);
        Box(bridge, "LuzAntena", new Vector3(-6.6f, 6.25f, -3f), new Vector3(0.18f, 0.18f, 0.18f), glowRed, false);
        Transform dish = Cylinder(bridge, "Radar", new Vector3(-5.2f, 4.7f, -3f), 0.6f, 0.08f, hullWhite, false);
        dish.localRotation = Quaternion.Euler(0f, 0f, 30f);
        Cylinder(bridge, "RadarMastil", new Vector3(-5.2f, 4.45f, -3f), 0.05f, 0.4f, hullGray, false);
    }

    static void BuildTurrets(Transform root)
    {
        Transform turrets = Group(root, "Torretas");
        float[] xs = { 2.5f, 6f, 9f };
        for (int i = 0; i < xs.Length; i++)
        {
            Transform t = Group(turrets, "Torreta" + i);
            t.localPosition = new Vector3(xs[i], 2.1f, i == 1 ? -4.2f : -1.8f);
            Cylinder(t, "Base", new Vector3(0f, 0.15f, 0f), 0.5f, 0.3f, hullGray, false);
            Box(t, "Cuerpo", new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.35f, 0.8f), hullWhite, false);
            for (int s = -1; s <= 1; s += 2)
            {
                Transform barrel = Cylinder(t, "Canon" + (s < 0 ? "I" : "D"), new Vector3(0.15f * s, 0.48f, 0.75f), 0.05f, 0.8f, hullDark, false);
                barrel.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }
    }

    // Franjas, bandera y nombre en el costado del hangar (+Z), que es el que se ve al despegar.
    static void BuildMarkings(Transform root)
    {
        Transform marks = Group(root, "Insignias");
        Box(marks, "FranjaRojaIzq", new Vector3(-6.45f, -1.35f, 0.02f), new Vector3(9.1f, 0.3f, 0.04f), redPaint, false);
        Box(marks, "FranjaRojaDer", new Vector3(6.45f, -1.35f, 0.02f), new Vector3(9.1f, 0.3f, 0.04f), redPaint, false);
        Box(marks, "FranjaOscuraIzq", new Vector3(-6.45f, 1.6f, 0.02f), new Vector3(9.1f, 0.12f, 0.04f), hullDark, false);
        Box(marks, "FranjaOscuraDer", new Vector3(6.45f, 1.6f, 0.02f), new Vector3(9.1f, 0.12f, 0.04f), hullDark, false);

        // Bandera del Perú (rojo-blanco-rojo).
        Transform flag = Group(marks, "Bandera");
        flag.localPosition = new Vector3(-6.3f, 0.35f, 0.03f);
        Box(flag, "Rojo1", new Vector3(-0.8f, 0f, 0f), new Vector3(0.8f, 1.6f, 0.03f), redPaint, false);
        Box(flag, "Blanco", new Vector3(0f, 0f, 0f), new Vector3(0.8f, 1.6f, 0.03f), glowWhite, false);
        Box(flag, "Rojo2", new Vector3(0.8f, 0f, 0f), new Vector3(0.8f, 1.6f, 0.03f), redPaint, false);

        // Nombre en el casco: canvas en el mundo mirando hacia +Z.
        var canvasGo = new GameObject("Nombre", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(marks, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)canvasGo.transform;
        rt.sizeDelta = new Vector2(900f, 120f);
        rt.localScale = Vector3.one * 0.01f;
        rt.localPosition = new Vector3(6.3f, 0.35f, 0.05f);
        rt.localRotation = Quaternion.Euler(0f, 180f, 0f);   // el frente del canvas mira hacia +Z

        var textGo = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(canvasGo.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<Text>();
        text.text = "BASE ESPACIAL DEL PERÚ";
        text.font = PeruTheme.HeadingFont;
        text.fontSize = 44;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.75f, 0.06f, 0.12f);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.raycastTarget = false;
    }

    // ---------- Utilidades ----------

    static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static Transform Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool collider = true) =>
        Primitive(PrimitiveType.Cube, parent, name, center, size, mat, collider);

    // Cilindro de Unity: eje Y, alto 2 por defecto.
    static Transform Cylinder(Transform parent, string name, Vector3 center, float radius, float length, Material mat,
        bool collider = true) =>
        Primitive(PrimitiveType.Cylinder, parent, name, center, new Vector3(radius * 2f, length * 0.5f, radius * 2f), mat, collider);

    static Transform Primitive(PrimitiveType type, Transform parent, string name, Vector3 center, Vector3 scale,
        Material mat, bool collider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localScale = scale;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go.transform;
    }

    // ---------- Materiales ----------

    static void CreateMaterials()
    {
        Directory.CreateDirectory(MatFolder);
        hullWhite = Lit("CascoBlanco", new Color(0.82f, 0.84f, 0.88f), 0.35f, 0.55f, new Color(0.26f, 0.27f, 0.3f));
        hullGray = Lit("CascoGris", new Color(0.42f, 0.45f, 0.52f), 0.5f, 0.5f, new Color(0.11f, 0.12f, 0.14f));
        hullDark = Lit("CascoOscuro", new Color(0.11f, 0.12f, 0.15f), 0.4f, 0.4f, new Color(0.03f, 0.03f, 0.04f));
        interior = Lit("HangarInterior", new Color(0.18f, 0.2f, 0.24f), 0.3f, 0.35f, new Color(0.09f, 0.1f, 0.12f));
        redPaint = Lit("PinturaRoja", new Color(0.75f, 0.06f, 0.12f), 0.2f, 0.5f, new Color(0.3f, 0.02f, 0.05f));
        door = Lit("Compuerta", new Color(0.34f, 0.36f, 0.42f), 0.55f, 0.45f, new Color(0.1f, 0.11f, 0.13f));
        glowWhite = Lit("LuzBlanca", Color.white, 0f, 0.5f, new Color(2.2f, 2.2f, 2.3f));
        glowRed = Lit("LuzAlerta", new Color(1f, 0.15f, 0.15f), 0f, 0.5f, new Color(3f, 0.25f, 0.25f));
        glowCyan = Lit("LuzMotor", new Color(0.4f, 0.85f, 1f), 0f, 0.5f, new Color(1.2f, 3f, 4f));
        window = Lit("Ventanas", new Color(0.3f, 0.6f, 0.8f), 0.1f, 0.9f, new Color(0.5f, 1.1f, 1.5f));
    }

    static Material Lit(string name, Color baseColor, float metallic, float smoothness, Color emission)
    {
        string path = MatFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        m.SetColor("_BaseColor", baseColor);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        if (emission.maxColorComponent > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }
        if (isNew) AssetDatabase.CreateAsset(m, path);
        else EditorUtility.SetDirty(m);
        return m;
    }
}
