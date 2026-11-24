using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla "MISIÓN FALLIDA": aparece al perder las 3 oportunidades.
//  - Prueba de destreza: REINTENTAR → Under con 3 oportunidades.
//  - Misión final (Defensa espacial): REINTENTAR → la misión desde cero.
// ELEGIR PRUEBA → pantalla de elección de prueba.
// Se construye por código, igual que VictoryScreen.
public class DefeatScreen : MonoBehaviour
{
    // Prueba de destreza (lo llama CollisionHandler).
    public static void Show()
    {
        string level = SceneManager.GetActiveScene().name.ToUpperInvariant();
        Show("PRUEBA DE DESTREZA  ·  " + Difficulty.CurrentName,
            $"Perdiste las {TrialProgress.DestrezaMaxLives} oportunidades en el nivel {level}.\n¡No te rindas, recluta! Vuelve a intentarlo desde el primer nivel.",
            TrialProgress.StartDestrezaLevels);
    }

    public static void Show(string mission, string message, UnityEngine.Events.UnityAction retry)
    {
        DefeatScreen existing = FindAnyObjectByType<DefeatScreen>();
        if (existing != null) Destroy(existing.gameObject);

        TrialNavigationHUD.Hide();
        Canvas canvas = PeruUI.CreateCanvas("DefeatScreen", 200);
        canvas.gameObject.AddComponent<DefeatScreen>().Build(canvas.transform, mission, message, retry);
    }

    void Build(Transform root, string mission, string message, UnityEngine.Events.UnityAction retryAction)
    {
        PeruUI.EnsureEventSystem();

        RectTransform bg = PeruUI.Rect(root, "Overlay", Vector2.zero, Vector2.zero);
        PeruUI.Stretch(bg);
        var bgImage = bg.gameObject.AddComponent<Image>();
        bgImage.color = new Color(PeruTheme.Night.r, PeruTheme.Night.g, PeruTheme.Night.b, 0.88f);

        PeruUI.ScreenStripes(root, 14f);

        RectTransform panel = PeruUI.Panel(root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(1400f, 820f));

        PeruUI.Emblem(panel, new Vector2(0.5f, 0.83f), 180f);

        PeruUI.Label(panel, "Mission", mission, new Vector2(0.5f, 0.67f), new Vector2(1300f, 50f),
            PeruTheme.HeadingFont, 34, PeruTheme.Red);

        Text title = PeruUI.Title(panel, "MISIÓN FALLIDA", new Vector2(0.5f, 0.56f), 84);
        title.rectTransform.sizeDelta = new Vector2(1300f, 120f);

        Text msg = PeruUI.Label(panel, "Message", message,
            new Vector2(0.5f, 0.38f), new Vector2(1180f, 170f),
            PeruTheme.BodyBoldFont, 40, PeruTheme.White, TextAnchor.MiddleCenter, wrap: true);
        PeruUI.AddSoftShadow(msg.gameObject);

        Button retry = PeruUI.Button(panel, "RetryButton", "Reintentar", new Vector2(0.33f, 0.14f), new Vector2(460f, 100f));
        retry.onClick.AddListener(retryAction);

        Button back = PeruUI.Button(panel, "ChooseTrialButton", "Elegir prueba", new Vector2(0.67f, 0.14f), new Vector2(460f, 100f));
        back.onClick.AddListener(TrialProgress.GoToSelection);

        EventSystem.current?.SetSelectedGameObject(retry.gameObject);
    }
}
