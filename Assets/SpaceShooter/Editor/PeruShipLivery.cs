using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Diseños peruanos pequeños en la nave del jugador (misión final):
//  - Banderitas del Perú sobre las alas.
//  - Franjas rojo-blanco-rojo finas sobre los motores.
//  - Emblema de RECLUTA sobre el fuselaje.
//  - Banderita en el tablero de la cabina (se ve desde la cámara 1).
// Son calcomanías planas (quads sin collider) pegadas a la superficie del modelo: el punto se busca
// lanzando un rayo contra la malla de cada pieza. Se pueden regenerar sin duplicarse.
// Menú: Astro Rocket > Defensa espacial > Diseños peruanos en la nave
public static class PeruShipLivery
{
    const string PlayerPath = "Assets/SpaceShooter/_project/Prefabs/Ships/PlayerShip.prefab";
    const string MatFolder = "Assets/SpaceShooter/_project/Materials/Peru";
    const string RootName = "DisenoPeruano";

    [MenuItem("Astro Rocket/Defensa espacial/Diseños peruanos en la nave")]
    public static void Apply()
    {
        Material flag = CreateMaterial("PeruFlag", LoadTexture("Assets/Resources/Peru/Flag.png"), alphaClip: false);
        Material emblem = CreateMaterial("PeruEmblem", LoadTexture("Assets/Resources/Peru/Emblem.png"), alphaClip: true);
        Material stripe = CreateMaterial("PeruStripe", CreateStripeTexture(), alphaClip: false);

        bool hasEye = TryGetCockpitEye(out Vector3 eyeLocalPos, out Quaternion eyeLocalRot, out float eyeFov);

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            Transform old = root.transform.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var hull = HullRenderers(root);
            var livery = new GameObject(RootName).transform;
            livery.SetParent(root.transform, false);

            Transform ship = root.transform;
            Vector3 up = ship.up, forward = ship.forward;

            // Alas: banderita en cada ala principal.
            foreach (Renderer wing in hull.Where(r => r.name.StartsWith("Wing2")))
                PlaceOnTop(livery, wing, hull, flag, "BanderaAla_" + wing.name, widthFraction: 0.32f, aspect: 2f / 3f,
                    up, forward, alongForward: false);

            // Motores: franja tricolor fina a lo largo.
            foreach (Renderer engine in hull.Where(r => r.name.StartsWith("Engine")))
                PlaceOnTop(livery, engine, hull, stripe, "FranjaMotor_" + engine.name, widthFraction: 0.45f, aspect: 0.22f,
                    up, forward, alongForward: true);

            // Fuselaje: emblema.
            Renderer body = hull.FirstOrDefault(r => r.name == "MainBody2") ?? hull.FirstOrDefault(r => r.name.StartsWith("MainBody"));
            if (body != null)
                PlaceOnTop(livery, body, hull, emblem, "EmblemaFuselaje", widthFraction: 0.22f, aspect: 1f, up, forward,
                    alongForward: false, forwardOffset: -0.18f);

            // Cabina: banderita en el tablero, a la izquierda de las pantallas.
            if (hasEye)
                PlaceInCockpit(livery, root, hull, flag, eyeLocalPos, eyeLocalRot, eyeFov);
            else
                Debug.LogWarning("[LIBREA] No se encontró la cámara de la escena para la cabina");

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            Debug.Log("[LIBREA] Diseños peruanos aplicados: " + string.Join(", ", livery.Cast<Transform>().Select(t => t.name)));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Piezas visibles de la nave (sin calcomanías, partículas ni interfaz).
    static List<Renderer> HullRenderers(GameObject root) =>
        root.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null)
            .Cast<Renderer>().ToList();

    static void PlaceOnTop(Transform parent, Renderer part, List<Renderer> hull, Material mat, string name,
        float widthFraction, float aspect, Vector3 up, Vector3 forward, bool alongForward, float forwardOffset = 0f)
    {
        Bounds b = part.bounds;
        Vector3 right = Vector3.Cross(up, forward);
        // Tamaño respecto a la pieza: ancho (envergadura) o largo, según la orientación.
        float spanAcross = Mathf.Abs(Vector3.Dot(b.size, right));
        float spanAlong = Mathf.Abs(Vector3.Dot(b.size, forward));
        float width = (alongForward ? spanAlong : spanAcross) * widthFraction;
        float height = width * aspect;

        Vector3 target = b.center + forward * (spanAlong * forwardOffset);
        Vector3 origin = target + up * (b.size.magnitude + 1f);
        if (!RaycastMeshes(new Ray(origin, -up), new[] { part }, out Vector3 point, out Vector3 normal))
        {
            Debug.LogWarning($"[LIBREA] Sin superficie para {name}");
            return;
        }
        if (Vector3.Dot(normal, up) < 0f) normal = -normal;

        // Si otra pieza la tapa desde arriba (p. ej. un ala bajo el motor), no se pone: no se vería.
        if (RaycastMeshes(new Ray(origin, -up), hull, out Vector3 firstHit, out _) &&
            Vector3.Distance(firstHit, point) > b.size.magnitude * 0.02f)
        {
            Debug.Log($"[LIBREA] {name} omitida: queda tapada por otra pieza");
            return;
        }

        // El eje vertical del quad va hacia delante: las bandas (rojo | blanco | rojo) quedan a lo largo de la nave.
        // Bandera: ancho a través de la pieza. Franja de motor: fina y larga.
        var quad = MakeQuad(parent, name, mat);
        quad.position = point + normal * Mathf.Max(0.002f, width * 0.01f);
        quad.rotation = Quaternion.LookRotation(-normal, ProjectOnPlane(forward, normal));
        quad.localScale = alongForward ? new Vector3(height, width, 1f) : new Vector3(width, height, 1f);
    }

    // Punto de pantalla (0..1) donde va la banderita del tablero: a la izquierda de los monitores.
    static readonly Vector2 DashboardViewport = new Vector2(0.30f, 0.235f);   // panel lateral izquierdo, de cara al piloto

    // Pose de la Main Camera de la escena (vista de cabina) relativa a la nave del jugador.
    static bool TryGetCockpitEye(out Vector3 localPos, out Quaternion localRot, out float fov)
    {
        localPos = Vector3.zero; localRot = Quaternion.identity; fov = 60f;
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/DefensaEspacial.unity");
        Camera cam = Camera.main;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (cam == null || player == null) return false;
        Transform ship = player.transform;
        localPos = ship.InverseTransformPoint(cam.transform.position);
        localRot = Quaternion.Inverse(ship.rotation) * cam.transform.rotation;
        fov = cam.fieldOfView;
        return true;
    }

    static Ray ViewportRay(Vector3 eyePos, Quaternion eyeRot, float fov, Vector2 viewport)
    {
        float tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        var local = new Vector3((viewport.x * 2f - 1f) * tan * 16f / 9f, (viewport.y * 2f - 1f) * tan, 1f);
        return new Ray(eyePos, (eyeRot * local).normalized);
    }

    static void PlaceInCockpit(Transform parent, GameObject root, List<Renderer> hull, Material flag,
        Vector3 eyeLocalPos, Quaternion eyeLocalRot, float fov)
    {
        Transform ship = root.transform;
        Vector3 eye = ship.TransformPoint(eyeLocalPos);
        Quaternion eyeRot = ship.rotation * eyeLocalRot;
        Ray ray = ViewportRay(eye, eyeRot, fov, DashboardViewport);
        // El ojo está pegado al respaldo: se ignoran el asiento y el cristal, y el rayo sale un poco adelantado.
        ray.origin += ray.direction * 0.05f;
        var visible = hull.Where(r => !r.name.Contains("Seat") && !r.name.Contains("Glass"));
        if (!RaycastMeshes(ray, visible, out Vector3 point, out Vector3 normal))
        {
            Debug.LogWarning("[LIBREA] Sin superficie en el tablero");
            return;
        }
        if (Vector3.Dot(normal, eye - point) < 0f) normal = -normal;
        float width = Vector3.Distance(eye, point) * 0.09f;   // unos 5° de la vista
        Debug.Log($"[LIBREA] Tablero: ojo local {eyeLocalPos}, impacto en '{lastHitName}' a {Vector3.Distance(eye, point):F3} m, ancho {width:F3}");
        var quad = MakeQuad(parent, "BanderaTablero", flag);
        quad.position = point + normal * Mathf.Max(0.001f, width * 0.01f);
        // Derecha para quien mira desde el asiento.
        quad.rotation = Quaternion.LookRotation(-normal, ProjectOnPlane(eyeRot * Vector3.up, normal));
        quad.localScale = new Vector3(width, width * 2f / 3f, 1f);
    }

    static Vector3 ProjectOnPlane(Vector3 v, Vector3 n)
    {
        Vector3 p = Vector3.ProjectOnPlane(v, n);
        return p.sqrMagnitude < 1e-6f ? Vector3.ProjectOnPlane(Vector3.forward, n) : p.normalized;
    }

    static Transform MakeQuad(Transform parent, string name, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, true);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    // Rayo contra las mallas (en espacio de mundo) de las piezas indicadas; devuelve el impacto más cercano.
    static string lastHitName;

    static bool RaycastMeshes(Ray ray, IEnumerable<Renderer> parts, out Vector3 point, out Vector3 normal)
    {
        float best = float.MaxValue;
        point = normal = Vector3.zero;
        lastHitName = null;
        foreach (Renderer r in parts)
        {
            Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
            if (mesh == null || !mesh.isReadable && !Application.isEditor) continue;
            Matrix4x4 m = r.transform.localToWorldMatrix;
            Vector3[] v = mesh.vertices;
            int[] tris = mesh.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = m.MultiplyPoint3x4(v[tris[i]]), b = m.MultiplyPoint3x4(v[tris[i + 1]]), c = m.MultiplyPoint3x4(v[tris[i + 2]]);
                if (!RayTriangle(ray, a, b, c, out float t) || t >= best) continue;
                best = t;
                lastHitName = r.name;
                point = ray.origin + ray.direction * t;
                normal = Vector3.Cross(b - a, c - a).normalized;
            }
        }
        return best < float.MaxValue;
    }

    static bool RayTriangle(Ray ray, Vector3 a, Vector3 b, Vector3 c, out float t)
    {
        t = 0f;
        Vector3 e1 = b - a, e2 = c - a, p = Vector3.Cross(ray.direction, e2);
        float det = Vector3.Dot(e1, p);
        if (Mathf.Abs(det) < 1e-9f) return false;
        float inv = 1f / det;
        Vector3 s = ray.origin - a;
        float u = Vector3.Dot(s, p) * inv;
        if (u < 0f || u > 1f) return false;
        Vector3 q = Vector3.Cross(s, e1);
        float w = Vector3.Dot(ray.direction, q) * inv;
        if (w < 0f || u + w > 1f) return false;
        t = Vector3.Dot(e2, q) * inv;
        return t > 0f;
    }

    // ---------- Materiales ----------

    static Texture2D LoadTexture(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

    // Franja rojo | blanco | rojo (bandas a lo largo).
    static Texture2D CreateStripeTexture()
    {
        string path = MatFolder + "/PeruStripe.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        Directory.CreateDirectory(MatFolder);
        var tex = new Texture2D(48, 4, TextureFormat.RGBA32, false);
        for (int x = 0; x < 48; x++)
            for (int y = 0; y < 4; y++)
                tex.SetPixel(x, y, x >= 16 && x < 32 ? Color.white : new Color(0.851f, 0.063f, 0.137f));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material CreateMaterial(string name, Texture2D tex, bool alphaClip)
    {
        Directory.CreateDirectory(MatFolder);
        string path = $"{MatFolder}/{name}.mat";
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(lit);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = lit;
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.35f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Cull", 0f);                     // visible por ambas caras
        mat.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
        mat.SetFloat("_Cutoff", 0.5f);
        // Un poco de emisión para que se distingan en el espacio oscuro.
        mat.SetTexture("_EmissionMap", tex);
        mat.SetColor("_EmissionColor", new Color(0.25f, 0.25f, 0.25f));
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        BaseShaderGUI.SetMaterialKeywords(mat, UnityEditor.Rendering.Universal.ShaderGUI.LitGUI.SetMaterialKeywords);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // ---------- Capturas de comprobación ----------

    // -executeMethod PeruShipLivery.CaptureShip -captureDir <carpeta>
    public static void CaptureShip()
    {
        string dir = null;
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-captureDir") dir = args[i + 1];
        dir ??= Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "Capturas");
        Directory.CreateDirectory(dir);

        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/DefensaEspacial.unity");
        if (Camera.main != null) Render(Camera.main, Path.Combine(dir, "Nave_cabina.png"));
        foreach (GameObject g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            g.SetActive(false);
        var light = new GameObject("Luz").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.6f;
        light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        var ship = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Bounds b = new Bounds(ship.transform.position, Vector3.zero);
        foreach (Renderer r in ship.GetComponentsInChildren<MeshRenderer>()) b.Encapsulate(r.bounds);
        float radius = b.extents.magnitude;

        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.fieldOfView = 40f;
        cam.nearClipPlane = radius * 0.01f;
        cam.farClipPlane = radius * 50f + 2000f;
        cam.clearFlags = CameraClearFlags.Skybox;

        (string name, Vector3 dir)[] views =
        {
            ("Nave_arriba_atras", new Vector3(0f, 0.9f, -1f)),
            ("Nave_lado", new Vector3(-1f, 0.35f, 0.15f)),
            ("Nave_arriba", new Vector3(0.01f, 1f, -0.25f)),
        };
        foreach (var v in views)
        {
            cam.transform.position = b.center + v.dir.normalized * radius * 2.6f;
            cam.transform.LookAt(b.center, ship.transform.up);
            Render(cam, Path.Combine(dir, v.name + ".png"));
        }

        Debug.Log("[LIBREA] capturas en " + dir);
    }

    static void Render(Camera cam, string file)
    {
        const int w = 1600, h = 900;
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        cam.aspect = (float)w / h;
        cam.Render();
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());
        RenderTexture.active = prev;
        cam.targetTexture = null;
        Object.DestroyImmediate(tex);
        rt.Release();
    }
}
