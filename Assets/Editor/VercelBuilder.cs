using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Genera la carpeta "Vercel" (junto a Assets) lista para subir a Vercel:
//   Vercel/index.html, Build/, TemplateData/, StreamingAssets/ (video) y vercel.json
// Compila en WebGL con compresión Gzip + "Decompression Fallback": así el juego carga en Vercel
// sin configurar cabeceras Content-Encoding (el propio loader descomprime si hace falta).
// Los ajustes de compresión del proyecto se restauran al terminar.
// Uso: Tools > RECLUTA > Compilar para Vercel (WebGL)
public static class VercelBuilder
{
    static string OutputDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Vercel"));

    [MenuItem("Tools/RECLUTA/Compilar para Vercel (WebGL)", priority = 40)]
    public static void BuildForVercel()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("Vercel", "No hay escenas activas en Build Settings.", "OK");
            return;
        }
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var prevCompression = PlayerSettings.WebGL.compressionFormat;
        bool prevFallback = PlayerSettings.WebGL.decompressionFallback;

        try
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            if (Directory.Exists(OutputDir)) Directory.Delete(OutputDir, true);

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog("Vercel", "La compilación WebGL falló. Revisa la Consola.", "OK");
                return;
            }

            File.WriteAllText(Path.Combine(OutputDir, "vercel.json"), VercelJson);

            long bytes = new DirectoryInfo(OutputDir).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            Debug.Log($"Build para Vercel lista en {OutputDir} ({bytes / (1024 * 1024)} MB).");
            EditorUtility.RevealInFinder(Path.Combine(OutputDir, "index.html"));
        }
        finally
        {
            PlayerSettings.WebGL.compressionFormat = prevCompression;
            PlayerSettings.WebGL.decompressionFallback = prevFallback;
            AssetDatabase.SaveAssets();
        }
    }

    // Caché larga para los archivos pesados y tipo MIME correcto para .wasm.
    const string VercelJson = @"{
  ""cleanUrls"": false,
  ""headers"": [
    {
      ""source"": ""/Build/(.*)"",
      ""headers"": [
        { ""key"": ""Cache-Control"", ""value"": ""public, max-age=3600"" }
      ]
    },
    {
      ""source"": ""/Build/(.*)\\.wasm"",
      ""headers"": [
        { ""key"": ""Content-Type"", ""value"": ""application/wasm"" }
      ]
    },
    {
      ""source"": ""/StreamingAssets/(.*)"",
      ""headers"": [
        { ""key"": ""Cache-Control"", ""value"": ""public, max-age=86400"" }
      ]
    }
  ]
}
";
}
