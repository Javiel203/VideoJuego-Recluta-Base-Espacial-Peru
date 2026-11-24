using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Corrige los modelos de "GameDevTV Assets/Models" que se ven rosados.
// Causa: el remapeo de materiales del importador FBX apuntaba a materiales internos de OTROS FBX
// (cadenas como Crate -> Light_Glow_13 -> ...) y algunos de esos destinos ya no existen.
// Solución: el material de paleta "Colour_Palette_Sci_Fi" se apunta siempre al .mat real del paquete,
// y cualquier remapeo que quede vacío también. Los remapeos que funcionan (emisión, rugosidad) no se tocan.
// Menú: Astro Rocket > Corregir materiales rosados.
public static class FixMissingMaterials
{
    const string ModelsFolder = "Assets/GameDevTV Assets/Models";
    const string PaletteMaterialPath = "Assets/GameDevTV Assets/Materials and Textures/Colour_Palette_Sci Fi.mat";
    const string PaletteName = "Colour_Palette_Sci_Fi";

    [MenuItem("Astro Rocket/Corregir materiales rosados")]
    public static void Fix()
    {
        var palette = AssetDatabase.LoadAssetAtPath<Material>(PaletteMaterialPath);
        if (palette == null) { Debug.LogError("[Materiales] No se encontró " + PaletteMaterialPath); return; }

        int changedModels = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;

            var changes = new List<string>();
            foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, Object> kv in importer.GetExternalObjectMap())
            {
                if (kv.Key.type != typeof(Material)) continue;
                Object target = kv.Value;
                string targetPath = target != null ? AssetDatabase.GetAssetPath(target) : "";
                bool missing = target == null;
                bool chained = !missing && AssetDatabase.IsSubAsset(target) &&
                               targetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
                if (!missing && !chained && !(kv.Key.name == PaletteName && target != palette)) continue;

                // Paleta base o destino vacío -> .mat de la paleta.
                // Otro material encadenado (emisión, rugosidad) que sí carga -> copia estable en un .mat propio.
                Material replacement = missing || kv.Key.name == PaletteName ? palette : StableCopy((Material)target);
                importer.AddRemap(kv.Key, replacement);
                changes.Add($"{kv.Key.name}: {(missing ? "vacío" : targetPath)} -> {AssetDatabase.GetAssetPath(replacement)}");
            }

            if (changes.Count == 0) continue;
            importer.SaveAndReimport();
            changedModels++;
            Debug.Log($"[Materiales] {path}\n  " + string.Join("\n  ", changes));
        }

        // Comprobación: ningún modelo debe quedar con huecos de material.
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    if (m == null) Debug.LogWarning($"[Materiales] Sigue vacío: {path} / {r.name}");
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Materiales] Modelos corregidos: {changedModels}");
    }

    const string StableFolder = "Assets/GameDevTV Assets/Materials and Textures/Remapeados";

    static Material StableCopy(Material source)
    {
        if (!AssetDatabase.IsValidFolder(StableFolder))
            AssetDatabase.CreateFolder("Assets/GameDevTV Assets/Materials and Textures", "Remapeados");
        string path = $"{StableFolder}/{source.name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        AssetDatabase.CreateAsset(new Material(source) { name = source.name }, path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }
}
