using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Construye barreras visibles en los niveles Under y Through para que la nave
// no pueda esquivar la prueba pasando por encima de los obstáculos:
//  - Techo sobre todo el nivel.
//  - Paredes laterales (de suelo a techo) detrás de cada plataforma.
//  - Columna desde el obstáculo hasta el techo (y hasta el suelo en Through, si queda hueco).
// Todas tienen collider y no llevan tag, así que tocarlas cuenta como choque.
// Menú: Astro Rocket > Construir barreras (Under y Through). Se puede repetir: reemplaza las anteriores.
public static class TrialBarriersBuilder
{
    const string RootName = "Barreras";
    const string MaterialPath = "Assets/Materials/Barrera.mat";
    const string ObstacleKeyword = "Tank";

    const float CeilingClearance = 6f;   // espacio libre entre el obstáculo más alto y el techo
    const float SideMargin = 4f;         // distancia de las paredes laterales a las plataformas
    const float Thickness = 1.5f;        // grosor de techo y paredes
    const float Depth = 6f;              // profundidad en Z (la nave vuela en Z = 0)
    const float ColumnWidth = 3f;        // ancho de las columnas sobre/bajo los obstáculos

    [MenuItem("Astro Rocket/Construir barreras (Under y Through)")]
    public static void BuildAll()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = EditorSceneManager.GetActiveScene().path;

        Build("Assets/Scenes/Under.unity", blockBelowLowest: false);
        Build("Assets/Scenes/Through.unity", blockBelowLowest: true);

        if (!Application.isBatchMode && !string.IsNullOrEmpty(previous))
            EditorSceneManager.OpenScene(previous);
    }

    static void Build(string scenePath, bool blockBelowLowest)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == RootName) Object.DestroyImmediate(root);

        // --- Referencias del nivel ---
        GameObject ground = null, startPad = null, finishPad = null;
        var obstacles = new List<Bounds>();
        foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            GameObject go = c.gameObject;
            if (go.GetComponentInParent<Movment>() != null) continue;
            if (go.name == "Ground") ground = go;
            else if (go.name.Contains(ObstacleKeyword)) obstacles.Add(c.bounds);
        }
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.CompareTag("Finish")) finishPad = root;
            else if (startPad == null && (root.CompareTag("Friendly") || root.name.Contains("StartPad"))) startPad = root;
        }

        if (ground == null || startPad == null || finishPad == null || obstacles.Count == 0)
        {
            Debug.LogError($"[Barreras] {scenePath}: no se encontró suelo, plataformas u obstáculos. No se cambió nada.");
            return;
        }

        float groundTop = ground.GetComponent<Collider>().bounds.max.y;
        Bounds startB = Bounds(startPad), finishB = Bounds(finishPad);
        float minX = Mathf.Min(startB.min.x, finishB.min.x) - SideMargin;
        float maxX = Mathf.Max(startB.max.x, finishB.max.x) + SideMargin;

        float highest = float.MinValue;
        Bounds top = obstacles[0], lowest = obstacles[0];
        foreach (Bounds b in obstacles)
        {
            if (b.max.y > highest) { highest = b.max.y; top = b; }
            if (b.min.y < lowest.min.y) lowest = b;
        }
        float ceilingBottom = highest + CeilingClearance;

        // --- Barreras ---
        Material mat = GetMaterial();
        var parent = new GameObject(RootName).transform;

        // Techo
        MakeBox(parent, "Techo", mat,
            new Vector3((minX + maxX) / 2f, ceilingBottom + Thickness / 2f, 0f),
            new Vector3(maxX - minX + Thickness * 2f, Thickness, Depth));

        // Paredes laterales (de suelo a techo)
        float wallH = ceilingBottom - groundTop;
        float wallY = groundTop + wallH / 2f;
        MakeBox(parent, "Pared Izquierda", mat, new Vector3(minX - Thickness / 2f, wallY, 0f), new Vector3(Thickness, wallH, Depth));
        MakeBox(parent, "Pared Derecha", mat, new Vector3(maxX + Thickness / 2f, wallY, 0f), new Vector3(Thickness, wallH, Depth));

        // Columna desde el obstáculo más alto hasta el techo: impide pasar por encima.
        // Se mete un poco dentro del obstáculo para que no quede rendija.
        float colBottom = top.center.y;
        MakeBox(parent, "Columna Superior", mat,
            new Vector3(top.center.x, (colBottom + ceilingBottom) / 2f, 0f),
            new Vector3(ColumnWidth, ceilingBottom - colBottom, Depth));

        // Through: si queda hueco entre el suelo y el obstáculo de abajo, también se tapa.
        if (blockBelowLowest && lowest.min.y > groundTop + 0.05f)
        {
            float colTop = lowest.center.y;
            MakeBox(parent, "Columna Inferior", mat,
                new Vector3(lowest.center.x, (groundTop + colTop) / 2f, 0f),
                new Vector3(ColumnWidth, colTop - groundTop, Depth));
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Barreras] {scenePath}: suelo={groundTop:F2} techo={ceilingBottom:F2} x=[{minX:F2},{maxX:F2}] " +
                  $"obstáculo superior y=[{top.min.y:F2},{top.max.y:F2}] inferior y=[{lowest.min.y:F2},{lowest.max.y:F2}]");
    }

    static Bounds Bounds(GameObject go)
    {
        Collider[] cols = go.GetComponentsInChildren<Collider>();
        Bounds b = cols.Length > 0 ? cols[0].bounds : new Bounds(go.transform.position, Vector3.zero);
        for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
        return b;
    }

    static void MakeBox(Transform parent, string name, Material mat, Vector3 center, Vector3 size)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = center;
        box.transform.localScale = size;
        var r = box.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;   // que el techo no oscurezca el nivel
        GameObjectUtility.SetStaticEditorFlags(box, StaticEditorFlags.BatchingStatic);
    }

    static Material GetMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat != null) return mat;

        mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Barrera" };
        mat.SetColor("_BaseColor", new Color(0.55f, 0.42f, 0.32f));      // piedra andina
        mat.SetFloat("_Smoothness", 0.15f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.12f, 0.05f, 0.02f));  // leve brillo para que se vea en zonas oscuras
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        AssetDatabase.CreateAsset(mat, MaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
