using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// En los niveles de la nave (escenas con Movment), arriba a la derecha:
//  - Botón "ELEGIR PRUEBA" (y tecla Esc) para volver a la pantalla de elección de prueba.
//  - Las 3 oportunidades de la prueba de destreza.
//  - Aviso "¡Nave dañada!" al chocar.
// Se activa sola, sin tocar las escenas de los niveles.
public class TrialNavigationHUD : MonoBehaviour
{
    static TrialNavigationHUD instance;
    static string pendingNotice;           // aviso que se vuelve a mostrar tras recargar el nivel

    static readonly Color LifeOn = new Color(0.851f, 0.063f, 0.137f);
    static readonly Color LifeOff = new Color(1f, 1f, 1f, 0.15f);
    static readonly Color DamageColor = new Color(0.7f, 0.05f, 0.1f, 0.92f);

    Image[] lifeIcons;
    CanvasGroup noticeGroup;
    Text noticeText;
    Coroutine noticeRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        pendingNotice = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindAnyObjectByType<Movment>(FindObjectsInactive.Include) == null) return;   // sólo niveles

        PeruUI.EnsureEventSystem();
        Canvas canvas = PeruUI.CreateCanvas("TrialNavigationHUD", 150);
        instance = canvas.gameObject.AddComponent<TrialNavigationHUD>();
        instance.Build(canvas.transform);

        if (!string.IsNullOrEmpty(pendingNotice))
        {
            instance.ShowNotice(pendingNotice, 1.8f);
            pendingNotice = null;
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Build(Transform root)
    {
        Button button = PeruUI.Button(root, "ChooseTrialButton", "Elegir prueba",
            new Vector2(1f, 1f), new Vector2(330f, 74f));
        var rt = (RectTransform)button.transform;
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-28f, -24f);
        button.GetComponentInChildren<Text>().fontSize = 28;
        // Sin navegación: así ESPACIO (propulsor) nunca "pulsa" este botón.
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(TrialProgress.GoToSelection);

        Text hint = PeruUI.Label(root, "EscHint", "o pulsa ESC", new Vector2(1f, 1f),
            new Vector2(330f, 30f), PeruTheme.BodyFont, 22, PeruTheme.TextSoft);
        hint.rectTransform.pivot = new Vector2(1f, 1f);
        hint.rectTransform.anchoredPosition = new Vector2(-28f, -102f);
        PeruUI.AddSoftShadow(hint.gameObject);

        // Oportunidades: tres indicadores a la izquierda del botón.
        lifeIcons = new Image[TrialProgress.DestrezaMaxLives];
        for (int i = 0; i < lifeIcons.Length; i++)
        {
            RectTransform icon = PeruUI.Rect(root, "Oportunidad" + (i + 1), new Vector2(1f, 1f), new Vector2(46f, 46f));
            icon.pivot = new Vector2(1f, 1f);
            icon.anchoredPosition = new Vector2(-386f - (lifeIcons.Length - 1 - i) * 60f, -30f);
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = PeruTheme.RoundedSprite;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            var outline = icon.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            lifeIcons[i] = img;
        }
        Text livesLabel = PeruUI.Label(root, "OportunidadesTexto", "OPORTUNIDADES", new Vector2(1f, 1f),
            new Vector2(240f, 30f), PeruTheme.HeadingFont, 18, PeruTheme.TextSoft, TextAnchor.MiddleRight);
        livesLabel.rectTransform.pivot = new Vector2(1f, 1f);
        livesLabel.rectTransform.anchoredPosition = new Vector2(-386f, -84f);
        PeruUI.AddSoftShadow(livesLabel.gameObject);
        RefreshLives();

        // Aviso de daño (centrado, oculto hasta que se choca).
        RectTransform notice = PeruUI.Rect(root, "AvisoDanio", new Vector2(0.5f, 0.66f), new Vector2(900f, 150f));
        var bg = notice.gameObject.AddComponent<Image>();
        bg.sprite = PeruTheme.RoundedSprite;
        bg.type = Image.Type.Sliced;
        bg.color = DamageColor;
        bg.raycastTarget = false;
        var bgOutline = notice.gameObject.AddComponent<Outline>();
        bgOutline.effectColor = new Color(1f, 1f, 1f, 0.7f);
        bgOutline.effectDistance = new Vector2(3f, -3f);
        noticeGroup = notice.gameObject.AddComponent<CanvasGroup>();
        noticeGroup.alpha = 0f;
        noticeGroup.blocksRaycasts = false;
        noticeGroup.interactable = false;
        noticeText = PeruUI.Label(notice, "Texto", "", new Vector2(0.5f, 0.5f), new Vector2(860f, 140f),
            PeruTheme.HeadingFont, 46, PeruTheme.White, TextAnchor.MiddleCenter, wrap: true);
        noticeText.supportRichText = true;
        noticeText.raycastTarget = false;
        PeruUI.AddSoftShadow(noticeText.gameObject);
    }

    void RefreshLives()
    {
        int lives = TrialProgress.DestrezaLives;
        for (int i = 0; i < lifeIcons.Length; i++)
            lifeIcons[i].color = i < lives ? LifeOn : LifeOff;
    }

    void ShowNotice(string message, float duration)
    {
        if (noticeRoutine != null) StopCoroutine(noticeRoutine);
        noticeText.text = message;
        noticeRoutine = StartCoroutine(NoticeRoutine(duration));
    }

    IEnumerator NoticeRoutine(float duration)
    {
        Transform t = noticeGroup.transform;
        for (float e = 0f; e < 0.15f; e += Time.unscaledDeltaTime)
        {
            float k = e / 0.15f;
            noticeGroup.alpha = k;
            t.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, k);
            yield return null;
        }
        noticeGroup.alpha = 1f;
        t.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(duration);
        for (float e = 0f; e < 0.3f; e += Time.unscaledDeltaTime)
        {
            noticeGroup.alpha = 1f - e / 0.3f;
            yield return null;
        }
        noticeGroup.alpha = 0f;
        noticeRoutine = null;
    }

    // Lo llama CollisionHandler al chocar (después de restar la oportunidad).
    public static void NotifyCrash(int livesLeft)
    {
        string message = livesLeft > 0
            ? $"¡NAVE DAÑADA!\n<size=32>Te {(livesLeft == 1 ? "queda" : "quedan")} {livesLeft} {(livesLeft == 1 ? "oportunidad" : "oportunidades")}</size>"
            : "¡NAVE DESTRUIDA!\n<size=32>Sin oportunidades</size>";

        // Se vuelve a mostrar en el nivel recargado para que dé tiempo a leerlo.
        if (livesLeft > 0) pendingNotice = message;

        if (instance == null) return;
        instance.RefreshLives();
        instance.ShowNotice(message, 0.9f);
    }

    // Oculta el HUD (lo usa la pantalla de derrota).
    public static void Hide()
    {
        if (instance != null) instance.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TrialProgress.GoToSelection();
    }
}
