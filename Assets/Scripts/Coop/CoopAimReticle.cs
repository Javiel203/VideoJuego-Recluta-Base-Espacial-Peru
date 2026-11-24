using UnityEngine;
using UnityEngine.UI;

// Mira del jugador 2 (cooperativo): muestra en su mitad de pantalla dónde está la mira que mueve con las
// flechas (la nariz de la nave gira hacia ella). Con la asistencia (Espacio) se pone roja, dice OBJETIVO
// y se coloca sobre la nave enemiga que está siguiendo.
public class CoopAimReticle : MonoBehaviour
{
    const float Size = 96f, Thickness = 6f, Gap = 16f;
    // Azul claro y brillante: se distingue del fondo espacial y de la mira roja de la cabina.
    static readonly Color ManualColor = new Color(0.45f, 0.85f, 1f);

    CoopKeyboardMovement keys;
    Camera view;
    RectTransform canvasRect, rect;
    Image[] parts;
    Text label;
    Color manualColor;
    static readonly Color AutoColor = new Color(1f, 0.25f, 0.25f);

    // Crea la mira dentro del HUD del shooter del jugador 2 (canvas dibujado por su cámara).
    public static CoopAimReticle Create(Canvas canvas, Camera camera, CoopKeyboardMovement keys, Color color)
    {
        var go = new GameObject("MiraJugador2", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(Size, Size);
        var reticle = go.AddComponent<CoopAimReticle>();
        reticle.keys = keys;
        reticle.view = camera;
        reticle.canvasRect = (RectTransform)canvas.transform;
        reticle.rect = rt;
        reticle.manualColor = color.maxColorComponent > 0f ? Color.Lerp(color, ManualColor, 0.6f) : ManualColor;
        reticle.Build();
        return reticle;
    }

    void Build()
    {
        // Cruz con hueco en el centro + punto central.
        float arm = (Size - Gap) * 0.5f;
        parts = new[]
        {
            Bar(new Vector2(0f, (Gap + arm) * 0.5f), new Vector2(Thickness, arm)),
            Bar(new Vector2(0f, -(Gap + arm) * 0.5f), new Vector2(Thickness, arm)),
            Bar(new Vector2((Gap + arm) * 0.5f, 0f), new Vector2(arm, Thickness)),
            Bar(new Vector2(-(Gap + arm) * 0.5f, 0f), new Vector2(arm, Thickness)),
            Bar(Vector2.zero, new Vector2(Thickness + 2f, Thickness + 2f)),
        };
        foreach (Image part in parts)
        {
            var outline = part.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        label = PeruUI.Label(rect, "Objetivo", "OBJETIVO", new Vector2(0.5f, 0f), new Vector2(220f, 30f),
            PeruTheme.HeadingFont, 18, AutoColor);
        label.rectTransform.pivot = new Vector2(0.5f, 1f);
        label.rectTransform.anchoredPosition = new Vector2(0f, -6f);
        label.raycastTarget = false;
        PeruUI.AddSoftShadow(label.gameObject);
    }

    Image Bar(Vector2 position, Vector2 size)
    {
        var go = new GameObject("Trazo", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(rect, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    void LateUpdate()
    {
        if (keys == null || canvasRect == null) return;
        Rect area = canvasRect.rect;
        Vector2 half = area.size * 0.5f;

        Transform target = keys.Target;
        bool auto = keys.AutoAiming;
        Vector2 position = new Vector2(keys.AimPoint.x * half.x, keys.AimPoint.y * half.y);
        if (auto && target != null && view != null)
        {
            // Sobre la nave enemiga (si está delante); si no, en el centro.
            Vector3 vp = view.WorldToViewportPoint(target.position);
            position = vp.z > 0f
                ? new Vector2(Mathf.Clamp(vp.x - 0.5f, -0.5f, 0.5f) * area.width, Mathf.Clamp(vp.y - 0.5f, -0.5f, 0.5f) * area.height)
                : Vector2.zero;
        }
        rect.localPosition = new Vector3(position.x, position.y, 0f);

        Color c = auto ? AutoColor : manualColor;
        foreach (Image part in parts) part.color = c;
        label.enabled = auto;
        rect.localScale = Vector3.one * (auto ? 1.2f : 1f);
    }
}
