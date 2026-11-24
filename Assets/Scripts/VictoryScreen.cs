using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum Trial { Destreza, Memoria, Defensa }

// Pantalla de prueba superada. La usan las tres pruebas:
//  - Si falta una prueba inicial: ofrece hacerla o volver a elegir prueba.
//  - Si ya están las dos iniciales: se desbloquea la misión final (Defensa espacial).
//  - Al superar la misión final: "¡Felicidades! Ya eres un astronauta especializado..." + Reiniciar juego.
// Se construye por código, así no hay que montarla a mano en la escena.
public class VictoryScreen : MonoBehaviour
{
    // Llamada sin parámetros: fin de la prueba de destreza (último nivel Over).
    public static void Show() => Show(Trial.Destreza);

    // retry: acción opcional para un botón REINTENTAR (la prueba de memoria lo usa).
    // backLabel: texto del botón que lleva a la elección de prueba.
    public static void Show(Trial trial, System.Action retry = null, string backLabel = "Elegir prueba")
    {
        bool wasUnlocked = TrialProgress.DefensaUnlocked;
        if (trial == Trial.Destreza) TrialProgress.CompleteDestreza();
        else if (trial == Trial.Memoria) TrialProgress.CompleteMemoria();
        else TrialProgress.CompleteDefensa();
        bool justUnlocked = !wasUnlocked && TrialProgress.DefensaUnlocked;

        VictoryScreen existing = FindAnyObjectByType<VictoryScreen>();
        if (existing != null) Destroy(existing.gameObject);

        Canvas canvas = PeruUI.CreateCanvas("VictoryScreen", 200);
        canvas.gameObject.AddComponent<VictoryScreen>().Build(canvas.transform, trial, retry, backLabel, justUnlocked);
    }

    void Build(Transform root, Trial trial, System.Action retry, string backLabel, bool justUnlocked)
    {
        PeruUI.EnsureEventSystem();

        // Fondo oscuro semitransparente.
        RectTransform bg = PeruUI.Rect(root, "Overlay", Vector2.zero, Vector2.zero);
        PeruUI.Stretch(bg);
        var bgImage = bg.gameObject.AddComponent<Image>();
        bgImage.color = new Color(PeruTheme.Night.r, PeruTheme.Night.g, PeruTheme.Night.b, 0.88f);

        PeruUI.ScreenStripes(root, 14f);

        RectTransform panel = PeruUI.Panel(root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(1400f, 900f));

        PeruUI.Emblem(panel, new Vector2(0.5f, 0.84f), 200f);
        PeruUI.Flag(panel, new Vector2(0.14f, 0.84f), 90f);
        PeruUI.Flag(panel, new Vector2(0.86f, 0.84f), 90f);

        string trialName = trial == Trial.Destreza ? "PRUEBA DE DESTREZA SUPERADA"
            : trial == Trial.Memoria ? "PRUEBA DE MEMORIA SUPERADA"
            : "MISIÓN FINAL CUMPLIDA";
        PeruUI.Label(panel, "Mission", trialName + "  ·  " + Difficulty.CurrentName, new Vector2(0.5f, 0.68f), new Vector2(1300f, 50f),
            PeruTheme.HeadingFont, 34, PeruTheme.Red);

        string pilot = PilotData.Name;
        // all: se superó la misión final (las tres pruebas). unlocked: están las dos iniciales.
        bool all = trial == Trial.Defensa;
        bool unlocked = TrialProgress.DefensaUnlocked;

        string title = all
            ? (string.IsNullOrWhiteSpace(pilot) ? "¡FELICIDADES!" : $"¡FELICIDADES, {pilot.ToUpperInvariant()}!")
            : "¡MUY BIEN, RECLUTA!";
        Text titleText = PeruUI.Title(panel, title, new Vector2(0.5f, 0.58f), 84);
        titleText.resizeTextForBestFit = true;
        titleText.resizeTextMinSize = 40;
        titleText.resizeTextMaxSize = 84;
        titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        titleText.rectTransform.sizeDelta = new Vector2(1300f, 120f);

        string message;
        if (all)
            message = "Ya eres un astronauta especializado y uno de los primeros pilotos espaciales del Perú.";
        else if (unlocked)
            message = justUnlocked
                ? "Superaste las dos pruebas iniciales. ¡Se desbloqueó la MISIÓN FINAL: Defensa espacial!"
                : TrialProgress.DefensaCompleted
                    ? "Prueba superada otra vez. Ya completaste todas las pruebas de la base."
                    : "Superaste las dos pruebas iniciales. Te espera la MISIÓN FINAL: Defensa espacial.";
        else
            message = trial == Trial.Destreza
                ? "Superaste la prueba de destreza. Te falta la prueba de memoria para desbloquear la misión final."
                : "Superaste la prueba de memoria. Te falta la prueba de destreza para desbloquear la misión final.";
        Text msg = PeruUI.Label(panel, "Message", message, new Vector2(0.5f, 0.42f), new Vector2(1180f, 160f),
            PeruTheme.BodyBoldFont, 42, PeruTheme.White, TextAnchor.MiddleCenter, wrap: true);
        PeruUI.AddSoftShadow(msg.gameObject);

        if (all)
            PeruUI.Label(panel, "Sub",
                $"Defendiste la Base Espacial del Perú: {TrialProgress.DefensaKillsToWin} naves enemigas derribadas (nivel {Difficulty.CurrentName}).",
                new Vector2(0.5f, 0.29f), new Vector2(1150f, 50f), PeruTheme.BodyFont, 30, PeruTheme.TextSoft,
                TextAnchor.MiddleCenter, wrap: true);

        // Botones (de izquierda a derecha).
        var buttons = new System.Collections.Generic.List<(string label, UnityEngine.Events.UnityAction action)>();
        if (!all)
        {
            if (unlocked) buttons.Add(("Misión final", TrialProgress.GoToDefensa));
            else if (trial == Trial.Destreza) buttons.Add(("Prueba de memoria", TrialProgress.GoToMemoria));
            else buttons.Add(("Prueba de destreza", TrialProgress.GoToDestreza));
        }
        if (retry != null) buttons.Add(("Reintentar", () => { Destroy(gameObject); retry(); }));
        buttons.Add((backLabel, TrialProgress.GoToSelection));
        if (all) buttons.Add(("Reiniciar juego", TrialProgress.RestartGame));

        // Panel de 1400 px: 2 botones de 460 px o 3 de 380 px, sin solaparse.
        float width = buttons.Count >= 3 ? 380f : 460f;
        float spacing = buttons.Count >= 3 ? 0.3f : 0.34f;
        Button first = null;
        for (int i = 0; i < buttons.Count; i++)
        {
            float x = 0.5f + (i - (buttons.Count - 1) * 0.5f) * spacing;
            Button b = PeruUI.Button(panel, "Button" + i, buttons[i].label, new Vector2(x, 0.13f),
                new Vector2(width, 100f));
            b.onClick.AddListener(buttons[i].action);
            if (first == null) first = b;
        }

        EventSystem.current?.SetSelectedGameObject(first.gameObject);
    }
}
