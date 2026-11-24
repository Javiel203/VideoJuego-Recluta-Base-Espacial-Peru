using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Finger = UnityEngine.InputSystem.EnhancedTouch.Touch;
using FingerPhase = UnityEngine.InputSystem.TouchPhase;

// Piezas de los controles táctiles: botón de mantener pulsado y joystick virtual.
// No usan el componente Button, así el sistema de voz no los confunde con botones de menú.
// No dependen del EventSystem: leen los dedos con TouchPointers y comprueban si caen dentro de cada control.
public static class TouchUI
{
    public const int CanvasOrder = 140;   // encima del juego, debajo de Victoria / Derrota (200)

    static readonly Color Fill = new Color(0.85f, 0.06f, 0.14f, 0.55f);
    static readonly Color FillPressed = new Color(1f, 0.25f, 0.3f, 0.85f);
    static readonly Color Border = new Color(1f, 1f, 1f, 0.75f);

    public static Canvas CreateCanvas(string name, Transform owner)
    {
        PeruUI.EnsureEventSystem();
        Canvas canvas = PeruUI.CreateCanvas(name, CanvasOrder);
        canvas.transform.SetParent(owner, false);
        return canvas;
    }

    // Botón redondeado anclado a una esquina (anchor) y desplazado 'position' px (referencia 1920x1080).
    public static TouchHoldButton HoldButton(Transform parent, string name, string label, Vector2 anchor,
        Vector2 position, Vector2 size, int fontSize = 30)
    {
        RectTransform rt = PeruUI.Rect(parent, name, anchor, size);
        rt.pivot = anchor;
        rt.anchoredPosition = position;

        var border = rt.gameObject.AddComponent<Image>();
        border.sprite = PeruTheme.RoundedSprite;
        border.type = Image.Type.Sliced;
        border.color = Border;

        RectTransform fillRt = PeruUI.Rect(rt, "Fill", Vector2.zero, Vector2.zero);
        PeruUI.Stretch(fillRt, 4f);
        var fill = fillRt.gameObject.AddComponent<Image>();
        fill.sprite = PeruTheme.RoundedSprite;
        fill.type = Image.Type.Sliced;
        fill.color = Fill;
        fill.raycastTarget = false;

        Text text = PeruUI.Label(rt, "Label", label, new Vector2(0.5f, 0.5f), size - new Vector2(16f, 8f),
            PeruTheme.HeadingFont, fontSize, Color.white, TextAnchor.MiddleCenter, wrap: true);
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;   // textos largos ("DISPARANDO") caben en el botón
        text.resizeTextMinSize = 14;
        text.resizeTextMaxSize = fontSize;
        PeruUI.AddSoftShadow(text.gameObject);

        var button = rt.gameObject.AddComponent<TouchHoldButton>();
        button.Init(fill, Fill, FillPressed, text);
        return button;
    }

    public static TouchJoystick Joystick(Transform parent, string name, Vector2 anchor, Vector2 position, float diameter)
    {
        RectTransform rt = PeruUI.Rect(parent, name, anchor, new Vector2(diameter, diameter));
        rt.pivot = anchor;
        rt.anchoredPosition = position;

        var bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = PeruTheme.RoundedSprite;
        bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = 0.05f;   // esquinas muy redondeadas: casi un círculo
        bg.color = new Color(0f, 0f, 0f, 0.35f);
        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = Border;
        outline.effectDistance = new Vector2(3f, -3f);

        float knobSize = diameter * 0.42f;
        RectTransform knob = PeruUI.Rect(rt, "Knob", new Vector2(0.5f, 0.5f), new Vector2(knobSize, knobSize));
        var knobImage = knob.gameObject.AddComponent<Image>();
        knobImage.sprite = PeruTheme.RoundedSprite;
        knobImage.type = Image.Type.Sliced;
        knobImage.pixelsPerUnitMultiplier = 0.05f;
        knobImage.color = FillPressed;
        knobImage.raycastTarget = false;

        var joystick = rt.gameObject.AddComponent<TouchJoystick>();
        joystick.Init(knob, diameter * 0.5f);
        return joystick;
    }
}

// Dedos apoyados en la pantalla en este frame. Se combinan varias fuentes porque cada plataforma
// (pantalla táctil de Windows, móvil, navegador) informa los toques de forma distinta:
//  1. Estado directo de la pantalla táctil (Input System): ve los dedos quietos.
//  2. Enhanced Touch: no pierde los toques muy cortos.
//  3. API de toques antigua de Unity (respaldo).
//  4. Ratón: sólo si no hay ningún toque (PC con F6, o Windows convirtiendo el toque en ratón).
// Un dedo es "nuevo" (began) en el primer frame en que aparece, venga de la fuente que venga.
public static class TouchPointers
{
    public struct Pointer
    {
        public int id;
        public Vector2 position;   // píxeles de pantalla (origen abajo a la izquierda)
        public bool began;         // apareció en este frame
        public string source;
    }

    const int MouseId = -1;
    const int LegacyIdOffset = 100000;
    const float SamePointPixels = 40f;   // dos fuentes que informan el mismo dedo
    const float MouseAfterTouchDelay = 0.5f;

    static readonly List<Pointer> pointers = new List<Pointer>();
    static readonly List<TouchJoystick> joysticks = new List<TouchJoystick>();
    static readonly HashSet<int> claimed = new HashSet<int>();
    static HashSet<int> previousIds = new HashSet<int>();
    static HashSet<int> currentIds = new HashSet<int>();
    static int frame = -1;
    static float lastTouchTime = -10f;

    // Para el panel de diagnóstico: cuántos dedos ve cada fuente.
    public static int CountScreen { get; private set; }
    public static int CountEnhanced { get; private set; }
    public static int CountLegacy { get; private set; }
    public static bool MousePressed { get; private set; }

    public static List<Pointer> Current
    {
        get
        {
            if (frame != Time.frameCount) Refresh();
            return pointers;
        }
    }

    // Dedo que ya maneja un joystick: los botones lo ignoran aunque pase por encima.
    public static bool IsClaimed(int id) => claimed.Contains(id);

    internal static void Register(TouchJoystick j)
    {
        if (!joysticks.Contains(j)) joysticks.Add(j);
    }

    internal static void Unregister(TouchJoystick j) => joysticks.Remove(j);
    internal static void Claim(int id) => claimed.Add(id);
    internal static void Release(int id) => claimed.Remove(id);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        pointers.Clear();
        joysticks.Clear();
        claimed.Clear();
        previousIds.Clear();
        currentIds.Clear();
        frame = -1;
        lastTouchTime = -10f;
    }

    static void Refresh()
    {
        frame = Time.frameCount;
        pointers.Clear();
        CountScreen = CountEnhanced = CountLegacy = 0;
        MousePressed = false;

        ReadTouchscreen();
        ReadEnhancedTouch();
        ReadLegacyTouches();
        if (pointers.Count > 0) lastTouchTime = Time.unscaledTime;
        ReadMouse();

        // "Nuevo" = no estaba en el frame anterior.
        currentIds.Clear();
        for (int i = 0; i < pointers.Count; i++)
        {
            Pointer p = pointers[i];
            p.began = !previousIds.Contains(p.id);
            pointers[i] = p;
            currentIds.Add(p.id);
        }
        (previousIds, currentIds) = (currentIds, previousIds);

        // Dedos que ya no están: dejan de pertenecer a un joystick.
        claimed.RemoveWhere(id => !previousIds.Contains(id));

        // Primero los joysticks (se quedan con su dedo), después los botones.
        for (int i = joysticks.Count - 1; i >= 0; i--)
        {
            if (joysticks[i] == null) joysticks.RemoveAt(i);
            else joysticks[i].Process(pointers);
        }
    }

    static void ReadTouchscreen()
    {
        Touchscreen screen = Touchscreen.current;
        if (screen == null) return;
        foreach (TouchControl t in screen.touches)
        {
            if (!t.press.isPressed) continue;
            int id = t.touchId.ReadValue();
            if (id == 0) continue;   // ranura vacía
            Add(id, t.position.ReadValue(), "pantalla");
            CountScreen++;
        }
    }

    static void ReadEnhancedTouch()
    {
        if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
        foreach (Finger t in Finger.activeTouches)
        {
            FingerPhase phase = t.phase;
            if (phase == FingerPhase.Ended || phase == FingerPhase.Canceled) continue;
            CountEnhanced++;
            if (!HasId(t.touchId)) Add(t.touchId, t.screenPosition, "enhanced");
        }
    }

    static void ReadLegacyTouches()
    {
        try
        {
            int count = UnityEngine.Input.touchCount;
            for (int i = 0; i < count; i++)
            {
                UnityEngine.Touch t = UnityEngine.Input.GetTouch(i);
                if (t.phase == UnityEngine.TouchPhase.Ended || t.phase == UnityEngine.TouchPhase.Canceled) continue;
                CountLegacy++;
                if (!HasNear(t.position)) Add(LegacyIdOffset + t.fingerId, t.position, "antigua");
            }
        }
        catch (InvalidOperationException)
        {
            // El proyecto usa sólo el Input System nuevo: no hay API antigua.
        }
    }

    static void ReadMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        bool down = mouse.leftButton.isPressed || mouse.leftButton.wasPressedThisFrame;
        MousePressed = down;
        if (!down) return;
        // Si hay toques, el ratón suele ser el mismo dedo "convertido" por el sistema: se ignora.
        if (pointers.Count > 0 || Time.unscaledTime - lastTouchTime < MouseAfterTouchDelay) return;
        Add(MouseId, mouse.position.ReadValue(), "ratón");
    }

    static void Add(int id, Vector2 position, string source) =>
        pointers.Add(new Pointer { id = id, position = position, source = source });

    static bool HasId(int id)
    {
        foreach (Pointer p in pointers)
            if (p.id == id) return true;
        return false;
    }

    static bool HasNear(Vector2 position)
    {
        foreach (Pointer p in pointers)
            if ((p.position - position).sqrMagnitude < SamePointPixels * SamePointPixels) return true;
        return false;
    }

    // ¿El punto de pantalla cae dentro del control? 'margin' agranda la zona (0.15 = 15 % más por lado).
    public static bool Hits(RectTransform rt, Vector2 screenPosition, float margin)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPosition, null, out Vector2 local))
            return false;
        Rect r = rt.rect;
        float mx = r.width * margin, my = r.height * margin;
        return local.x >= r.xMin - mx && local.x <= r.xMax + mx && local.y >= r.yMin - my && local.y <= r.yMax + my;
    }
}

// Botón que cuenta mientras haya un dedo encima (propulsor, fuego…). Reacciona en cuanto se toca.
//  - El dedo que lo toca queda "capturado": aunque se deslice un poco fuera del dibujo, sigue pulsado.
//  - Un dedo que llega deslizándose también lo pulsa (de IZQUIERDA a DERECHA sin levantarlo).
public class TouchHoldButton : MonoBehaviour
{
    const float HitMargin = 0.12f;       // zona para empezar a pulsar
    const float CaptureMargin = 0.5f;    // zona para seguir pulsado con un dedo capturado

    Image fill;
    Color idle, pressed;
    bool held, pendingPress;
    int polledFrame = -1;
    readonly HashSet<int> captured = new HashSet<int>();
    readonly List<int> stale = new List<int>();

    public Text Label { get; private set; }

    public bool IsHeld
    {
        get
        {
            Poll();
            return held;
        }
    }

    // true una sola vez por cada toque (para botones de una vez: objetivo, salir).
    public bool ConsumePress()
    {
        Poll();
        bool p = pendingPress;
        pendingPress = false;
        return p;
    }

    public void Init(Image fillImage, Color idleColor, Color pressedColor, Text label)
    {
        fill = fillImage;
        idle = idleColor;
        pressed = pressedColor;
        Label = label;
    }

    void Update() => Poll();   // registra los toques aunque nadie pregunte en este frame

    void Poll()
    {
        if (polledFrame == Time.frameCount) return;
        polledFrame = Time.frameCount;

        bool wasHeld = held;
        held = false;
        if (isActiveAndEnabled)
        {
            var rt = (RectTransform)transform;
            List<TouchPointers.Pointer> pointers = TouchPointers.Current;

            foreach (TouchPointers.Pointer p in pointers)
            {
                if (TouchPointers.IsClaimed(p.id)) continue;
                bool inside = TouchPointers.Hits(rt, p.position, HitMargin) ||
                              (captured.Contains(p.id) && TouchPointers.Hits(rt, p.position, CaptureMargin));
                if (inside)
                {
                    held = true;
                    captured.Add(p.id);
                    if (p.began) pendingPress = true;
                }
                else
                {
                    captured.Remove(p.id);
                }
            }

            // Olvida los dedos que ya se levantaron.
            stale.Clear();
            foreach (int id in captured)
            {
                bool present = false;
                foreach (TouchPointers.Pointer p in pointers)
                    if (p.id == id) { present = true; break; }
                if (!present) stale.Add(id);
            }
            foreach (int id in stale) captured.Remove(id);
        }
        if (held != wasHeld && fill != null) fill.color = held ? pressed : idle;
    }

    void OnDisable()
    {
        held = pendingPress = false;
        captured.Clear();
        if (fill != null) fill.color = idle;
    }
}

// Joystick virtual: Value va de (-1,-1) a (1,1); vuelve al centro al soltar.
// Se queda con el dedo que lo tocó aunque luego salga del círculo; ese dedo no pulsa otros botones.
public class TouchJoystick : MonoBehaviour
{
    const float HitMargin = 0.1f;
    const int None = int.MinValue;

    RectTransform knob;
    float radius;
    int fingerId = None;

    public Vector2 Value { get; private set; }
    public bool IsHeld => fingerId != None;

    public void Init(RectTransform knobRect, float radiusPx)
    {
        knob = knobRect;
        radius = radiusPx;
    }

    void OnEnable() => TouchPointers.Register(this);

    void OnDisable()
    {
        TouchPointers.Unregister(this);
        ResetStick();
    }

    // Asegura que los dedos se procesen cada frame aunque nadie más pregunte.
    void Update()
    {
        if (TouchPointers.Current == null) ResetStick();
    }

    internal void Process(List<TouchPointers.Pointer> pointers)
    {
        if (!isActiveAndEnabled) return;
        var rt = (RectTransform)transform;

        if (IsHeld)
        {
            foreach (TouchPointers.Pointer p in pointers)
            {
                if (p.id != fingerId) continue;
                Move(rt, p.position);
                return;
            }
            ResetStick();   // el dedo se levantó
            return;
        }

        // Lo toma un dedo nuevo que cae dentro del círculo (con margen).
        foreach (TouchPointers.Pointer p in pointers)
        {
            if (!p.began || TouchPointers.IsClaimed(p.id) || !TouchPointers.Hits(rt, p.position, HitMargin)) continue;
            fingerId = p.id;
            TouchPointers.Claim(p.id);
            Move(rt, p.position);
            return;
        }
    }

    void Move(RectTransform rt, Vector2 screenPosition)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPosition, null, out Vector2 local))
            return;
        Vector2 offset = Vector2.ClampMagnitude(local - rt.rect.center, radius);
        if (knob != null) knob.anchoredPosition = offset;
        Vector2 v = offset / radius;
        Value = v.magnitude < 0.12f ? Vector2.zero : v;   // zona muerta
    }

    void ResetStick()
    {
        if (fingerId != None) TouchPointers.Release(fingerId);
        fingerId = None;
        Value = Vector2.zero;
        if (knob != null) knob.anchoredPosition = Vector2.zero;
    }
}
