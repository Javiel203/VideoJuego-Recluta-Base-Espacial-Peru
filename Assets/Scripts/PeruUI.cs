using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Piezas de interfaz con el estilo de RECLUTA (rojo y blanco).
// Las usan tanto los builders del Editor (escenas de menú) como las pantallas creadas en juego.
public static class PeruUI
{
    public static Canvas CreateCanvas(string name, int sortingOrder = 0)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.sizeDelta = size;
        return rt;
    }

    public static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    // ---------- Texto ----------

    public static Text Label(Transform parent, string name, string content, Vector2 anchor, Vector2 size,
        Font font, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool wrap = false)
    {
        RectTransform rt = Rect(parent, name, anchor, size);
        var text = rt.gameObject.AddComponent<Text>();
        text.text = content;
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Normal;   // las fuentes ya traen su grosor
        text.alignment = align;
        text.color = color;
        text.lineSpacing = 1.15f;
        text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    // Título grande: blanco con contorno rojo y sombra.
    public static Text Title(Transform parent, string content, Vector2 anchor, int fontSize)
    {
        Text t = Label(parent, "Title", content, anchor, new Vector2(1800f, fontSize * 1.5f),
            PeruTheme.TitleFont, fontSize, PeruTheme.White);
        AddTitleEffects(t.gameObject, fontSize >= 100 ? 5f : 3f);
        return t;
    }

    public static void AddTitleEffects(GameObject go, float thickness)
    {
        var outline = go.AddComponent<Outline>();
        outline.effectColor = PeruTheme.Red;
        outline.effectDistance = new Vector2(thickness, -thickness);
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = PeruTheme.Shadow;
        shadow.effectDistance = new Vector2(thickness * 2f, -thickness * 2f);
    }

    public static void AddSoftShadow(GameObject go)
    {
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = PeruTheme.Shadow;
        shadow.effectDistance = new Vector2(2f, -2f);
    }

    // ---------- Botón ----------

    // Botón redondeado: borde blanco, relleno rojo, texto Orbitron. Crece al pasar el ratón.
    public static Button Button(Transform parent, string name, string label, Vector2 anchor, Vector2? size = null)
    {
        RectTransform rt = Rect(parent, name, anchor, size ?? new Vector2(420f, 100f));

        // Borde blanco (fondo del botón).
        var border = rt.gameObject.AddComponent<Image>();
        border.sprite = PeruTheme.RoundedSprite;
        border.type = Image.Type.Sliced;
        border.color = PeruTheme.White;

        // Relleno rojo (el que cambia de color con el estado del botón).
        RectTransform fillRt = Rect(rt, "Fill", Vector2.zero, Vector2.zero);
        Stretch(fillRt, 5f);
        var fill = fillRt.gameObject.AddComponent<Image>();
        fill.sprite = PeruTheme.RoundedSprite;
        fill.type = Image.Type.Sliced;
        fill.color = Color.white;
        fill.raycastTarget = false;

        // Brillo superior sutil.
        RectTransform shineRt = Rect(fillRt, "Shine", Vector2.zero, Vector2.zero);
        shineRt.anchorMin = new Vector2(0f, 0.5f);
        shineRt.anchorMax = Vector2.one;
        shineRt.offsetMin = new Vector2(8f, 0f);
        shineRt.offsetMax = new Vector2(-8f, -6f);
        var shine = shineRt.gameObject.AddComponent<Image>();
        shine.sprite = PeruTheme.RoundedSprite;
        shine.type = Image.Type.Sliced;
        shine.color = new Color(1f, 1f, 1f, 0.12f);
        shine.raycastTarget = false;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        ColorBlock colors = button.colors;
        colors.normalColor = PeruTheme.Red;
        colors.highlightedColor = PeruTheme.RedLight;
        colors.selectedColor = PeruTheme.RedLight;
        colors.pressedColor = PeruTheme.RedDark;
        colors.disabledColor = new Color(0.35f, 0.35f, 0.4f, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Text text = Label(rt, "Text", label.ToUpperInvariant(), new Vector2(0.5f, 0.5f), Vector2.zero,
            PeruTheme.HeadingFont, 38, PeruTheme.White);
        Stretch((RectTransform)text.transform);
        AddSoftShadow(text.gameObject);

        rt.gameObject.AddComponent<FancyButton>();
        return button;
    }

    // ---------- Paneles y decoración ----------

    // Panel oscuro redondeado con una franja roja arriba.
    public static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 size)
    {
        RectTransform rt = Rect(parent, name, anchor, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = PeruTheme.RoundedSprite;
        img.type = Image.Type.Sliced;
        img.color = PeruTheme.Panel;
        img.raycastTarget = false;

        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.25f);
        outline.effectDistance = new Vector2(2f, -2f);

        RectTransform stripe = Rect(rt, "TopStripe", Vector2.zero, Vector2.zero);
        stripe.anchorMin = new Vector2(0f, 1f);
        stripe.anchorMax = new Vector2(1f, 1f);
        stripe.pivot = new Vector2(0.5f, 1f);
        stripe.offsetMin = new Vector2(30f, -10f);
        stripe.offsetMax = new Vector2(-30f, -4f);
        var s = stripe.gameObject.AddComponent<Image>();
        s.color = PeruTheme.Red;
        s.raycastTarget = false;
        return rt;
    }

    public static RawImage Flag(Transform parent, Vector2 anchor, float height)
    {
        RectTransform rt = Rect(parent, "PeruFlag", anchor, new Vector2(height * 1.5f, height));
        var raw = rt.gameObject.AddComponent<RawImage>();
        raw.texture = PeruTheme.Flag;
        raw.raycastTarget = false;
        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);
        return raw;
    }

    public static RawImage Emblem(Transform parent, Vector2 anchor, float size)
    {
        RectTransform rt = Rect(parent, "Emblem", anchor, new Vector2(size, size));
        var raw = rt.gameObject.AddComponent<RawImage>();
        raw.texture = PeruTheme.Emblem;
        raw.raycastTarget = false;
        return raw;
    }

    // Franjas rojo | blanco | rojo en el borde superior e inferior de la pantalla.
    public static void ScreenStripes(Transform parent, float thickness = 12f)
    {
        CreateStripe(parent, "TopStripes", top: true, thickness);
        CreateStripe(parent, "BottomStripes", top: false, thickness);
    }

    static void CreateStripe(Transform parent, string name, bool top, float thickness)
    {
        RectTransform root = Rect(parent, name, Vector2.zero, Vector2.zero);
        root.anchorMin = new Vector2(0f, top ? 1f : 0f);
        root.anchorMax = new Vector2(1f, top ? 1f : 0f);
        root.pivot = new Vector2(0.5f, top ? 1f : 0f);
        root.sizeDelta = new Vector2(0f, thickness);
        root.anchoredPosition = Vector2.zero;

        for (int i = 0; i < 3; i++)
        {
            RectTransform part = Rect(root, "Part" + i, Vector2.zero, Vector2.zero);
            part.anchorMin = new Vector2(i / 3f, 0f);
            part.anchorMax = new Vector2((i + 1) / 3f, 1f);
            part.offsetMin = part.offsetMax = Vector2.zero;
            var img = part.gameObject.AddComponent<Image>();
            img.color = i == 1 ? PeruTheme.White : PeruTheme.Red;
            img.raycastTarget = false;
        }
    }

    // Emblema pequeño + "BASE ESPACIAL DEL PERÚ" en una esquina (pantallas secundarias).
    public static void CornerBadge(Transform parent)
    {
        RawImage emblem = Emblem(parent, new Vector2(0f, 1f), 110f);
        var ert = emblem.rectTransform;
        ert.pivot = new Vector2(0f, 1f);
        ert.anchoredPosition = new Vector2(30f, -30f);

        Text t = Label(parent, "BadgeText", PeruTheme.GameTitle + "\n" + PeruTheme.GameSubtitle,
            new Vector2(0f, 1f), new Vector2(520f, 90f), PeruTheme.HeadingFont, 24, PeruTheme.White,
            TextAnchor.MiddleLeft);
        var trt = t.rectTransform;
        trt.pivot = new Vector2(0f, 1f);
        trt.anchoredPosition = new Vector2(152f, -40f);
        AddSoftShadow(t.gameObject);
    }
}
