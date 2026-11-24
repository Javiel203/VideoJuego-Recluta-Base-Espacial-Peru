using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Modo táctil (móvil / tablet). Se crea solo al arrancar y sobrevive entre escenas.
//  - Se activa solo en móviles (también en el navegador del móvil) o al tocar la pantalla.
//  - F6 lo activa / desactiva para probarlo en el PC o en el Editor (se recuerda).
//  - Cada juego muestra sus controles táctiles mientras Active es true (RocketTouch, DefensaTouch).
//  - En vertical pide girar el teléfono, y oculta los avisos "pulsa ESC" (en el móvil no hay teclado).
//  - En Windows desactiva "mantener pulsado = clic derecho" en la ventana del juego (retrasaba los toques).
//  - Panel de diagnóstico de toques: F9 o tocar con 3 dedos a la vez.
public class TouchMode : MonoBehaviour
{
    const string ForcedKey = "Touch_Forced";

    public static bool Active { get; private set; }
    public static event Action<bool> Changed;

    static TouchMode instance;
    GUIStyle rotateStyle, debugStyle;
    bool showDebug;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Active = false;
        Changed = null;
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        // Antes de cargar la escena: así los juegos ya saben si deben mostrar los controles táctiles.
        Active = Application.isMobilePlatform || PlayerPrefs.GetInt(ForcedKey, 0) == 1;
        var go = new GameObject("TouchMode");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<TouchMode>();
    }

    void Awake() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;
    void Start()
    {
        HideKeyboardHints();
        DisableWindowsPressAndHold();
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused) DisableWindowsPressAndHold();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HideKeyboardHints();

    void Update()
    {
        // Primer toque en la pantalla (portátil o tablet con pantalla táctil).
        if (!Active && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            Set(true);

        // Panel de diagnóstico: F9, o tres dedos que tocan a la vez.
        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame) showDebug = !showDebug;
        var fingers = TouchPointers.Current;
        if (!Active && TouchPointers.CountScreen + TouchPointers.CountEnhanced + TouchPointers.CountLegacy > 0)
            Set(true);   // cualquier fuente de toques activa el modo táctil
        if (fingers.Count == 3)
        {
            bool anyNew = false;
            foreach (var f in fingers) anyNew |= f.began;
            if (anyNew) showDebug = !showDebug;
        }

        if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame)
        {
            Set(!Active);
            PlayerPrefs.SetInt(ForcedKey, Active ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    static void Set(bool value)
    {
        if (Active == value) return;
        Active = value;
        if (instance != null) instance.HideKeyboardHints();
        Changed?.Invoke(value);
    }

    // Los textos "o pulsa ESC" no tienen sentido sin teclado.
    void HideKeyboardHints()
    {
        foreach (Text t in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string s = t.text.Trim().ToUpperInvariant();
            if (s == "O PULSA ESC") t.enabled = !Active;
        }
    }

    // ---------- Windows: sin "mantener pulsado = clic derecho" ----------
    // Windows espera ~1 s con el dedo quieto para decidir si es un clic derecho, y mientras tanto no
    // entrega el toque (por eso había que deslizar el dedo). Se desactiva para la ventana del juego.
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    const string TabletProperty = "MicrosoftTabletPenServiceProperty";
    const int DisablePressAndHold = 0x00000001, DisablePenTapFeedback = 0x00000008,
              DisablePenBarrelFeedback = 0x00000010, DisableFlicks = 0x00010000;

    [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SetPropW(IntPtr hWnd, string name, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern ushort GlobalAddAtomW(string name);
    [DllImport("kernel32.dll")] static extern ushort GlobalDeleteAtom(ushort atom);

    static void DisableWindowsPressAndHold()
    {
        try
        {
            IntPtr hwnd = GetActiveWindow();
            if (hwnd == IntPtr.Zero) return;
            ushort atom = GlobalAddAtomW(TabletProperty);
            SetPropW(hwnd, TabletProperty,
                new IntPtr(DisablePressAndHold | DisablePenTapFeedback | DisablePenBarrelFeedback | DisableFlicks));
            if (atom != 0) GlobalDeleteAtom(atom);
        }
        catch (Exception e)
        {
            Debug.Log("Táctil: no se pudo desactivar 'mantener pulsado' de Windows: " + e.Message);
        }
    }
#else
    static void DisableWindowsPressAndHold() { }
#endif

    void OnGUI()
    {
        if (showDebug) DrawDebug();
        DrawRotateNotice();
    }

    // ---------- Panel de diagnóstico ----------

    void DrawDebug()
    {
        if (debugStyle == null)
        {
            debugStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = false };
            debugStyle.normal.textColor = Color.white;
        }
        debugStyle.fontSize = Mathf.Max(14, Mathf.RoundToInt(Screen.height / 50f));

        var fingers = TouchPointers.Current;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("DIAGNÓSTICO TÁCTIL  (F9 o 3 dedos para cerrar)");
        sb.AppendLine($"Modo táctil: {(Active ? "SÍ" : "NO")}   ·   Pantalla táctil: {(Touchscreen.current != null ? "detectada" : "no detectada")}");
        sb.AppendLine($"Dedos vistos  ·  pantalla: {TouchPointers.CountScreen}   enhanced: {TouchPointers.CountEnhanced}   " +
                      $"antigua: {TouchPointers.CountLegacy}   ratón pulsado: {(TouchPointers.MousePressed ? "sí" : "no")}");
        sb.AppendLine($"Dedos usados por el juego: {fingers.Count}");
        foreach (var f in fingers)
            sb.AppendLine($"  dedo {f.id}  ({f.source})  x={f.position.x:0} y={f.position.y:0}{(f.began ? "  NUEVO" : "")}" +
                          $"{(TouchPointers.IsClaimed(f.id) ? "  [joystick]" : "")}");

        string text = sb.ToString();
        float h = debugStyle.CalcHeight(new GUIContent(text), Screen.width * 0.6f);
        Rect box = new Rect(10f, 10f, Screen.width * 0.6f + 20f, h + 20f);
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = old;
        GUI.Label(new Rect(20f, 20f, Screen.width * 0.6f, h), text, debugStyle);

        // Marca en cada dedo (GUI tiene el origen arriba; la pantalla, abajo).
        float size = Mathf.Max(24f, Screen.height / 25f);
        foreach (var f in fingers)
        {
            GUI.color = f.began ? Color.yellow : new Color(0.2f, 1f, 0.4f);
            GUI.DrawTexture(new Rect(f.position.x - size / 2f, Screen.height - f.position.y - size / 2f, size, size),
                Texture2D.whiteTexture);
        }
        GUI.color = old;
    }

    // En vertical los juegos no caben: se pide girar el teléfono.
    void DrawRotateNotice()
    {
        if (!Active || Screen.height <= Screen.width) return;
        if (rotateStyle == null)
        {
            rotateStyle = new GUIStyle(GUI.skin.label)
            {
                font = PeruTheme.HeadingFont,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };
            rotateStyle.normal.textColor = Color.white;
        }
        rotateStyle.fontSize = Mathf.RoundToInt(Screen.width / 14f);

        Color old = GUI.color;
        GUI.color = new Color(PeruTheme.Night.r, PeruTheme.Night.g, PeruTheme.Night.b, 0.96f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;
        GUI.Label(new Rect(Screen.width * 0.1f, 0, Screen.width * 0.8f, Screen.height),
            "GIRA TU TELÉFONO\n\npara jugar en horizontal", rotateStyle);
    }
}
