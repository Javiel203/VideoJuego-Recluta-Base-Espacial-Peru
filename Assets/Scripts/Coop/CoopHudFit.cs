using UnityEngine;

// Cooperativo: el HUD del shooter (barras del láser y misiles) está diseñado para una pantalla de 1920 x 1080.
// En media pantalla se saldría por los lados, así que se mete en este contenedor de 1920 x 1080 pegado
// abajo y se escala para que entre a lo ancho de su mitad.
[RequireComponent(typeof(RectTransform))]
public class CoopHudFit : MonoBehaviour
{
    public const float ReferenceWidth = 1920f, ReferenceHeight = 1080f;

    RectTransform rect, parent;

    // Crea el contenedor dentro del canvas y mueve allí los elementos indicados.
    public static CoopHudFit Wrap(Canvas canvas, params Transform[] elements)
    {
        var go = new GameObject("HUD_MediaPantalla", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);
        rt.anchoredPosition = Vector2.zero;
        rt.SetAsFirstSibling();   // debajo de los indicadores de objetivo
        foreach (Transform t in elements)
            if (t != null) t.SetParent(rt, false);
        var fit = go.AddComponent<CoopHudFit>();
        fit.LateUpdate();
        return fit;
    }

    void Awake()
    {
        rect = (RectTransform)transform;
    }

    void LateUpdate()
    {
        if (rect == null) rect = (RectTransform)transform;
        if (parent == null) parent = rect.parent as RectTransform;
        if (parent == null) return;
        Rect area = parent.rect;
        float k = Mathf.Min(area.width / ReferenceWidth, area.height / ReferenceHeight);
        if (k > 0f) rect.localScale = new Vector3(k, k, 1f);
    }
}
