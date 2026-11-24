using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Resplandor (Bloom) suave para la MISIÓN FINAL: hace brillar los motores y ventanas de la nave nodriza,
// las luces del hangar y los disparos. Sólo afecta a lo que brilla con intensidad (umbral alto), así que
// el resto de la imagen no cambia. Se crea al cargar la misión (DefensaMission) y desaparece con la escena.
public static class DefensaBloom
{
    public static void Enable(Transform owner)
    {
        // La cámara principal necesita el post-procesado activado para mostrar el Bloom.
        Camera cam = Camera.main;
        if (cam != null && cam.TryGetComponent(out UniversalAdditionalCameraData data))
            data.renderPostProcessing = true;
        else if (cam != null)
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        var go = new GameObject("BloomMision");
        go.transform.SetParent(owner, false);
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1f);      // sólo lo más brillante (emisión por encima de 1)
        bloom.intensity.Override(0.85f);
        bloom.scatter.Override(0.65f);
        bloom.highQualityFiltering.Override(false);   // más ligero para web y móvil
        volume.sharedProfile = profile;
    }
}
