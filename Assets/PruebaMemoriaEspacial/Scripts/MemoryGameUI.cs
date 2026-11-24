using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Interfaz de la Prueba de Memoria Espacial: instrucciones, marcador (nivel, oportunidades,
// estado), avisos de secuencia correcta/incorrecta y pantalla de derrota.
// La victoria usa la pantalla común VictoryScreen (Reintentar / Volver / otra prueba).
public class MemoryGameUI : MonoBehaviour
{
    [Header("Instrucciones")]
    [SerializeField] GameObject instructionsPanel;
    [SerializeField] Button playButton;

    [Header("Marcador")]
    [SerializeField] GameObject hud;
    [SerializeField] Text levelText;
    [SerializeField] Text statusText;
    [SerializeField] Text progressText;
    [SerializeField] Image[] lifeIcons;

    [Header("Aviso correcto / incorrecto")]
    [SerializeField] CanvasGroup feedbackGroup;
    [SerializeField] Image feedbackBackground;
    [SerializeField] Text feedbackText;

    [Header("Derrota")]
    [SerializeField] GameObject defeatPanel;
    [SerializeField] Text defeatDetail;
    [SerializeField] Button retryButton;

    static readonly Color LifeOn = new Color(0.851f, 0.063f, 0.137f);
    static readonly Color LifeOff = new Color(1f, 1f, 1f, 0.15f);
    static readonly Color GoodColor = new Color(0.1f, 0.55f, 0.3f, 0.92f);
    static readonly Color BadColor = new Color(0.7f, 0.05f, 0.1f, 0.92f);

    Coroutine feedbackRoutine;

    void Awake()
    {
        // El botón del marcador (durante el juego) lleva a la elección de prueba: "Elegir prueba".
        // Se ajusta aquí para que funcione aunque la escena se generara con el texto antiguo "Volver".
        if (hud == null) return;
        foreach (Button b in hud.GetComponentsInChildren<Button>(true))
        {
            Text label = b.GetComponentInChildren<Text>(true);
            if (label == null || label.text.Trim().ToUpperInvariant() != "VOLVER") continue;
            label.text = "ELEGIR PRUEBA";
            var rt = (RectTransform)b.transform;
            if (rt.sizeDelta.x < 330f) rt.sizeDelta = new Vector2(330f, rt.sizeDelta.y);
        }
    }

    public void ShowInstructions()
    {
        instructionsPanel.SetActive(true);
        hud.SetActive(false);
        defeatPanel.SetActive(false);
        HideFeedbackNow();
        Select(playButton);
    }

    public void ShowGame()
    {
        instructionsPanel.SetActive(false);
        defeatPanel.SetActive(false);
        hud.SetActive(true);
        HideFeedbackNow();
        EventSystem.current?.SetSelectedGameObject(null);   // que ESPACIO/ENTER no pulsen botones ocultos
    }

    public void HideAll()
    {
        instructionsPanel.SetActive(false);
        defeatPanel.SetActive(false);
        hud.SetActive(false);
        HideFeedbackNow();
    }

    public void SetLevel(int level, int maxLevel) => levelText.text = $"NIVEL {level} / {maxLevel}";

    public void SetLives(int lives)
    {
        for (int i = 0; i < lifeIcons.Length; i++)
            lifeIcons[i].color = i < lives ? LifeOn : LifeOff;
    }

    public void SetStatus(string status) => statusText.text = status;

    public void SetProgress(int done, int total) =>
        progressText.text = total > 0 ? $"{done} / {total}" : "";

    public void ShowFeedback(bool correct, string message, float duration = 1.1f)
    {
        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackText.text = message;
        feedbackBackground.color = correct ? GoodColor : BadColor;
        feedbackRoutine = StartCoroutine(FeedbackRoutine(duration));
    }

    IEnumerator FeedbackRoutine(float duration)
    {
        Transform t = feedbackGroup.transform;
        for (float e = 0f; e < 0.15f; e += Time.deltaTime)
        {
            float k = e / 0.15f;
            feedbackGroup.alpha = k;
            t.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, k);
            yield return null;
        }
        feedbackGroup.alpha = 1f;
        t.localScale = Vector3.one;
        yield return new WaitForSeconds(duration);
        for (float e = 0f; e < 0.25f; e += Time.deltaTime)
        {
            feedbackGroup.alpha = 1f - e / 0.25f;
            yield return null;
        }
        feedbackGroup.alpha = 0f;
        feedbackRoutine = null;
    }

    void HideFeedbackNow()
    {
        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackRoutine = null;
        if (feedbackGroup != null) feedbackGroup.alpha = 0f;
    }

    public void ShowDefeat(int levelReached)
    {
        hud.SetActive(false);
        HideFeedbackNow();
        defeatDetail.text = levelReached <= 1
            ? "No lograste superar el primer nivel. ¡Concéntrate y vuelve a intentarlo, recluta!"
            : $"Llegaste al nivel {levelReached}. ¡Inténtalo de nuevo, recluta!";
        defeatPanel.SetActive(true);
        Select(retryButton);
    }

    static void Select(Button b)
    {
        if (b != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(b.gameObject);
    }
}
