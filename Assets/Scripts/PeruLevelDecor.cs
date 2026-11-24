using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Detalles peruanos dentro de los niveles. Se activa sola en cada escena con cohete (Movment):
//  - Mástil con la bandera del Perú ondeando en la plataforma de salida y en la de llegada.
//  - Bandera del Perú pintada en el costado del cohete.
// No tiene colisiones, así que no cambia la jugabilidad. No hace falta añadirla a ninguna escena.
public static class PeruLevelDecor
{
    public const string DecorPrefix = "PeruDecor";   // PilotAppearance no tiñe estos objetos

    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int CullId = Shader.PropertyToID("_Cull");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Movment rocket = Object.FindAnyObjectByType<Movment>(FindObjectsInactive.Include);
        if (rocket == null) return;   // escenas de menú

        Material template = FindTemplateMaterial(rocket);
        Bounds rocketBounds = RendererBounds(rocket.gameObject);
        float rocketHeight = Mathf.Max(rocketBounds.size.y, 1f);

        // Plataformas: la de salida (debajo del cohete) y las de llegada (tag Finish).
        var pads = new List<GameObject>();
        GameObject start = FindStartPad(rocket, rocketBounds);
        if (start != null) pads.Add(start);
        foreach (GameObject finish in GameObject.FindGameObjectsWithTag("Finish"))
            if (!pads.Contains(finish)) pads.Add(finish);

        // Centro del nivel: el mástil se pone en el lado exterior de cada plataforma.
        Vector3 center = Vector3.zero;
        foreach (GameObject p in pads) center += ColliderBounds(p).center;
        if (pads.Count > 0) center /= pads.Count;

        foreach (GameObject pad in pads)
            CreateFlagPole(pad, center, rocketHeight, template);

        AddRocketFlag(rocket.gameObject, rocketBounds, template);
    }

    // ---------- Mástil + bandera ----------

    static void CreateFlagPole(GameObject pad, Vector3 levelCenter, float rocketHeight, Material template)
    {
        Bounds b = ColliderBounds(pad);
        float side = b.center.x >= levelCenter.x ? 1f : -1f;
        if (Mathf.Approximately(b.center.x, levelCenter.x)) side = -1f;

        float poleHeight = rocketHeight * 1.6f;
        float flagWidth = poleHeight * 0.55f;
        float flagHeight = flagWidth / 1.5f;

        // Sobre la plataforma, cerca del borde exterior y hacia el fondo (detrás del cohete).
        var basePos = new Vector3(b.center.x + side * b.extents.x * 0.8f, b.max.y, b.center.z + b.extents.z * 0.55f);

        var root = new GameObject(DecorPrefix + "_FlagPole");
        root.transform.SetParent(pad.transform, true);
        root.transform.position = basePos;
        root.transform.rotation = Quaternion.identity;

        // Mástil (cilindro sin collider).
        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(pole.GetComponent<Collider>());
        pole.name = DecorPrefix + "_Pole";
        pole.transform.SetParent(root.transform, false);
        float poleRadius = poleHeight * 0.025f;
        pole.transform.localPosition = new Vector3(0f, poleHeight * 0.5f, 0f);
        pole.transform.localScale = new Vector3(poleRadius * 2f, poleHeight * 0.5f, poleRadius * 2f);
        pole.GetComponent<Renderer>().sharedMaterial = MakeMaterial(template, null, new Color(0.85f, 0.87f, 0.9f));

        // Remate dorado.
        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(cap.GetComponent<Collider>());
        cap.name = DecorPrefix + "_Cap";
        cap.transform.SetParent(root.transform, false);
        cap.transform.localPosition = new Vector3(0f, poleHeight, 0f);
        cap.transform.localScale = Vector3.one * poleRadius * 3.5f;
        cap.GetComponent<Renderer>().sharedMaterial = MakeMaterial(template, null, new Color(1f, 0.8f, 0.3f));

        // Bandera ondeando (apunta hacia fuera del nivel).
        var flag = new GameObject(DecorPrefix + "_Flag", typeof(MeshFilter), typeof(MeshRenderer));
        flag.transform.SetParent(root.transform, false);
        flag.transform.localPosition = new Vector3(poleRadius, poleHeight * 0.96f, 0f);
        flag.transform.localScale = new Vector3(side, 1f, 1f);
        flag.GetComponent<MeshFilter>().sharedMesh = FlagWave.BuildMesh(flagWidth, flagHeight);
        Material flagMat = MakeMaterial(template, PeruTheme.Flag, Color.white);
        if (flagMat.HasProperty(CullId)) flagMat.SetFloat(CullId, 0f);   // visible por ambas caras
        flag.GetComponent<MeshRenderer>().sharedMaterial = flagMat;
        flag.AddComponent<FlagWave>();
    }

    // ---------- Bandera en el cohete ----------

    static void AddRocketFlag(GameObject rocket, Bounds worldBounds, Material template)
    {
        Transform t = rocket.transform;
        float width = worldBounds.size.x * 0.42f;
        float height = width / 1.5f;

        // Cara que mira a la cámara (menor Z), un poco por encima del centro.
        var worldPos = new Vector3(worldBounds.center.x, worldBounds.center.y + worldBounds.extents.y * 0.1f,
            worldBounds.min.z - 0.02f);

        var decal = new GameObject(DecorPrefix + "_RocketFlag", typeof(MeshFilter), typeof(MeshRenderer));
        decal.transform.SetParent(t, true);
        decal.transform.position = worldPos;
        decal.transform.rotation = Quaternion.identity;
        Vector3 ls = t.lossyScale;
        decal.transform.localScale = new Vector3(1f / SafeScale(ls.x), 1f / SafeScale(ls.y), 1f / SafeScale(ls.z));

        decal.GetComponent<MeshFilter>().sharedMesh = BuildQuad(width, height);
        Material mat = MakeMaterial(template, PeruTheme.Flag, Color.white);
        decal.GetComponent<MeshRenderer>().sharedMaterial = mat;
        decal.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static float SafeScale(float v) => Mathf.Abs(v) < 1e-4f ? 1f : v;

    static Mesh BuildQuad(float w, float h)
    {
        var mesh = new Mesh { name = "PeruQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-w / 2, -h / 2, 0), new Vector3(w / 2, -h / 2, 0),
            new Vector3(-w / 2, h / 2, 0), new Vector3(w / 2, h / 2, 0)
        };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };   // cara hacia -Z
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------- Utilidades ----------

    static GameObject FindStartPad(Movment rocket, Bounds rocketBounds)
    {
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.transform.parent == null && go.name.Contains("StartPad"))
                return go;

        // Si no se llama StartPad: lo primero que haya justo debajo del cohete.
        var origin = new Vector3(rocketBounds.center.x, rocketBounds.min.y + 0.05f, rocketBounds.center.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 20f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
            if (!hit.transform.IsChildOf(rocket.transform))
                return hit.transform.root.gameObject;
        return null;
    }

    static Bounds ColliderBounds(GameObject go)
    {
        Collider[] cols = go.GetComponentsInChildren<Collider>();
        if (cols.Length == 0) return RendererBounds(go);
        Bounds b = cols[0].bounds;
        for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
        return b;
    }

    static Bounds RendererBounds(GameObject go)
    {
        bool has = false;
        var b = new Bounds(go.transform.position, Vector3.zero);
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // Material base: el del propio cohete, así seguro que el shader está incluido en la build.
    static Material FindTemplateMaterial(Movment rocket)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit != null) return new Material(lit);
        foreach (Renderer r in rocket.GetComponentsInChildren<Renderer>())
            if (!(r is ParticleSystemRenderer) && r.sharedMaterial != null)
                return r.sharedMaterial;
        return new Material(Shader.Find("Standard"));
    }

    static Material MakeMaterial(Material template, Texture texture, Color color)
    {
        var m = new Material(template) { name = DecorPrefix + "_Mat" };
        if (m.HasProperty(BaseMapId)) m.SetTexture(BaseMapId, texture);
        if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, color);
        if (m.HasProperty("_MainTex")) m.mainTexture = texture;
        if (m.HasProperty("_Color")) m.color = color;
        if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", null);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
        return m;
    }
}
