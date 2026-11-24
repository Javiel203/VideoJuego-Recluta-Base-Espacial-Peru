using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Al compilar el juego, copia el video de la misión final (Assets/Video/videoFinal.mp4) a
// StreamingAssets/Video para que viaje con la build (WebGL sólo reproduce videos por URL).
// Después de compilar borra la copia, así el proyecto no guarda el video dos veces.
public class IntroVideoBuildStep : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    const string Source = "Assets/Video/" + DefensaIntroVideo.FileName;
    const string TargetFolder = "Assets/StreamingAssets/Video";
    static string Target => TargetFolder + "/" + DefensaIntroVideo.FileName;

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!File.Exists(Source))
        {
            Debug.LogWarning($"Video de introducción: no existe {Source}; la misión final empezará sin video.");
            return;
        }
        Directory.CreateDirectory(TargetFolder);
        File.Copy(Source, Target, true);
        AssetDatabase.Refresh();
        long mb = new FileInfo(Source).Length / (1024 * 1024);
        Debug.Log($"Video de introducción copiado a StreamingAssets ({mb} MB).");
    }

    public void OnPostprocessBuild(BuildReport report) => Cleanup();

    // Si una build falló a medias, la copia se borra al abrir el proyecto.
    [InitializeOnLoadMethod]
    static void CleanupOnLoad()
    {
        if (!BuildPipeline.isBuildingPlayer) EditorApplication.delayCall += Cleanup;
    }

    static void Cleanup()
    {
        if (!File.Exists(Target)) return;
        AssetDatabase.DeleteAsset(Target);
        if (Directory.Exists(TargetFolder) && Directory.GetFileSystemEntries(TargetFolder).Length == 0)
            AssetDatabase.DeleteAsset(TargetFolder);
        if (Directory.Exists("Assets/StreamingAssets") && Directory.GetFileSystemEntries("Assets/StreamingAssets").Length == 0)
            AssetDatabase.DeleteAsset("Assets/StreamingAssets");
    }
}
