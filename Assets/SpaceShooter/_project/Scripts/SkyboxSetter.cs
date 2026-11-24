using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Skybox))]
public class SkyboxSetter : MonoBehaviour
{
    [SerializeField] List<Material> _skyboxMaterials;

    Skybox _skybox;

    void Awake()
    {
        _skybox = GetComponent<Skybox>();
    }

    void OnEnable()
    {
        ChangeSkybox(0);
    }

    void ChangeSkybox(int skyBox)
    {
        if (skyBox < 0 || skyBox >= _skyboxMaterials.Count) return;   // antes "<=": se salía de la lista
        Material material = _skyboxMaterials[skyBox];
        if (_skybox != null) _skybox.material = material;
        // URP ignora el componente Skybox de la cámara: el cielo se toma de RenderSettings.
        RenderSettings.skybox = material;
        DynamicGI.UpdateEnvironment();
    }
}
