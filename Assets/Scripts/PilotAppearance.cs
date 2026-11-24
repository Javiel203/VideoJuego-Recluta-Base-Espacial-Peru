using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Se activa sola en cada escena: si hay un cohete (Movment), lo tiñe con el color
// del piloto elegido y muestra la etiqueta "PILOTO: NOMBRE". No hace falta añadirla a ninguna escena.
public static class PilotAppearance
{
    const float TintStrength = 0.75f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!PilotData.HasPilot)
            return;

        Movment[] rockets = Object.FindObjectsByType<Movment>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (rockets.Length == 0)
            return; // escenas de menú

        Color pilotColor = PilotData.ColorFor(PilotData.Gender);
        foreach (Movment rocket in rockets)
            TintRocket(rocket.gameObject, pilotColor);

        CreateHud(PilotData.Name, pilotColor);
    }

    static void TintRocket(GameObject rocket, Color pilotColor)
    {
        Color tint = Color.Lerp(Color.white, pilotColor, TintStrength);
        var block = new MaterialPropertyBlock();

        foreach (Renderer r in rocket.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                continue;
            if (r.name.StartsWith(PeruLevelDecor.DecorPrefix))
                continue; // la bandera del cohete conserva sus colores

            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                int id = m.HasProperty(BaseColorId) ? BaseColorId
                       : m.HasProperty(ColorId) ? ColorId
                       : -1;
                if (id < 0) continue;

                r.GetPropertyBlock(block, i);
                block.SetColor(id, m.GetColor(id) * tint);
                r.SetPropertyBlock(block, i);
            }
        }
    }

    static void CreateHud(string pilotName, Color pilotColor)
    {
        var canvasGo = new GameObject("PilotHUD");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Banderita del Perú + "PILOTO: NOMBRE".
        RawImage flag = PeruUI.Flag(canvasGo.transform, new Vector2(0f, 1f), 44f);
        flag.rectTransform.pivot = new Vector2(0f, 1f);
        flag.rectTransform.anchoredPosition = new Vector2(30f, -30f);

        Text text = PeruUI.Label(canvasGo.transform, "PilotLabel", $"PILOTO: {pilotName.ToUpperInvariant()}",
            new Vector2(0f, 1f), new Vector2(800f, 60f), PeruTheme.HeadingFont, 34,
            Color.Lerp(Color.white, pilotColor, 0.35f), TextAnchor.MiddleLeft);
        text.rectTransform.pivot = new Vector2(0f, 1f);
        text.rectTransform.anchoredPosition = new Vector2(110f, -22f);

        var outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
    }
}
