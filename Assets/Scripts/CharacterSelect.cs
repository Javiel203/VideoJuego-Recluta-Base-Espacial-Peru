using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla de selección de piloto (nombre + Hombre/Mujer) y de la prueba a jugar:
// PRUEBA DE DESTREZA (nave), PRUEBA DE MEMORIA (cristales) o MISIÓN FINAL (Defensa espacial,
// bloqueada hasta superar las dos primeras).
public class CharacterSelect : MonoBehaviour
{
    [Header("Nombre")]
    [SerializeField] InputField nameInput;

    [Header("Tarjeta Hombre")]
    [SerializeField] RectTransform maleCard;
    [SerializeField] Image maleBorder;
    [SerializeField] Graphic maleSilhouette;

    [Header("Tarjeta Mujer")]
    [SerializeField] RectTransform femaleCard;
    [SerializeField] Image femaleBorder;
    [SerializeField] Graphic femaleSilhouette;

    [Header("Dificultad (Fácil, Intermedio, Avanzado)")]
    [SerializeField] Button[] difficultyButtons;

    [Header("Pruebas")]
    [SerializeField] Button destrezaButton;
    [SerializeField] Button memoriaButton;
    [SerializeField] Button defensaButton;
    [SerializeField] Text destrezaStatus;
    [SerializeField] Text memoriaStatus;
    [SerializeField] Text defensaStatus;

    [Header("Navegación")]
    [SerializeField] Text hintText;
    [SerializeField] string menuSceneName = "Historia";

    static readonly Color UnselectedBorder = new Color(1f, 1f, 1f, 0.15f);
    static readonly Color UnselectedSilhouette = new Color(0.55f, 0.6f, 0.72f);
    const float SelectedScale = 1.06f;

    PilotGender selected;

    string CleanName => nameInput != null ? nameInput.text.Trim() : "";
    bool IsValid => CleanName.Length > 0 && selected != PilotGender.None;

    void Start()
    {
        nameInput.characterLimit = PilotData.MaxNameLength;
        nameInput.text = PilotData.Name;          // recordar el último piloto
        selected = PilotData.Gender;
        nameInput.onValueChanged.AddListener(_ => Refresh());
        if (!TryGetComponent(out CharacterSelectVoice _)) gameObject.AddComponent<CharacterSelectVoice>();   // voz
        Refresh();
    }

    // ---------- Nombre por voz (CharacterSelectVoice) ----------

    bool dictating;

    public string PilotName => nameInput != null ? nameInput.text : "";

    public void SetPilotName(string pilotName)
    {
        pilotName = (pilotName ?? "").Trim();
        if (pilotName.Length > PilotData.MaxNameLength) pilotName = pilotName.Substring(0, PilotData.MaxNameLength).TrimEnd();
        nameInput.text = pilotName;   // onValueChanged → Refresh
    }

    public void ClearName() => SetPilotName("");

    public void SetDictating(bool value)
    {
        dictating = value;
        Refresh();
    }

    public void SelectFacil() => SelectDifficulty(DifficultyLevel.Facil);
    public void SelectIntermedio() => SelectDifficulty(DifficultyLevel.Intermedio);
    public void SelectAvanzado() => SelectDifficulty(DifficultyLevel.Avanzado);

    // El progreso se guarda por dificultad: al cambiarla cambian los estados de las pruebas.
    void SelectDifficulty(DifficultyLevel level)
    {
        Difficulty.Current = level;
        Refresh();
    }

    public void SelectMale() => Select(PilotGender.Male);
    public void SelectFemale() => Select(PilotGender.Female);

    void Select(PilotGender gender)
    {
        selected = gender;
        Refresh();
    }

    public void PlayDestreza() => StartTrial(TrialProgress.DestrezaScene);
    public void PlayMemoria() => StartTrial(TrialProgress.MemoriaScene);

    public void PlayDefensa()
    {
        if (!TrialProgress.DefensaUnlocked) return;
        if (!SavePilot()) return;
        TrialProgress.StartDefensa();
    }

    void StartTrial(string sceneName)
    {
        if (!SavePilot()) return;
        SceneManager.LoadScene(sceneName);
    }

    bool SavePilot()
    {
        if (!IsValid)
        {
            Refresh();
            return false;
        }
        PilotData.Save(CleanName, selected);
        return true;
    }

    public void Back()
    {
        SceneManager.LoadScene(menuSceneName);
    }

    void Refresh()
    {
        bool hasName = CleanName.Length > 0;
        bool hasGender = selected != PilotGender.None;

        destrezaButton.interactable = hasName && hasGender;
        memoriaButton.interactable = hasName && hasGender;
        if (defensaButton != null)
            defensaButton.interactable = hasName && hasGender && TrialProgress.DefensaUnlocked;
        SetStatus(destrezaStatus, TrialProgress.DestrezaCompleted);
        SetStatus(memoriaStatus, TrialProgress.MemoriaCompleted);
        if (defensaStatus != null && !TrialProgress.DefensaUnlocked)
        {
            defensaStatus.text = "BLOQUEADA: SUPERA LAS DOS PRUEBAS";
            defensaStatus.color = new Color(1f, 0.55f, 0.55f, 0.85f);
        }
        else
        {
            SetStatus(defensaStatus, TrialProgress.DefensaCompleted);
        }

        if (dictating) hintText.text = "Deletrea tu nombre letra por letra y di \"FINALIZAR\" al terminar";
        else if (!hasName && !hasGender) hintText.text = "Escribe tu nombre (o di \"NOMBRE\" y deletréalo) y elige un piloto";
        else if (!hasName) hintText.text = "Escribe el nombre de tu piloto o di \"NOMBRE\" para deletrearlo";
        else if (!hasGender) hintText.text = "Elige tu piloto";
        else if (TrialProgress.AllCompleted) hintText.text = $"¡{CleanName}, superaste todas las pruebas! Puedes repetirlas.";
        else if (TrialProgress.DefensaUnlocked) hintText.text = $"¡{CleanName}, se desbloqueó la misión final!";
        else hintText.text = $"{CleanName}, elige tu prueba:";

        RefreshDifficulty();

        UpdateCard(maleCard, maleBorder, maleSilhouette, selected == PilotGender.Male, PilotData.MaleColor);
        UpdateCard(femaleCard, femaleBorder, femaleSilhouette, selected == PilotGender.Female, PilotData.FemaleColor);
    }

    static readonly Color DifficultyIdle = new Color(0.32f, 0.12f, 0.16f);
    static readonly Color DifficultyIdleBorder = new Color(1f, 1f, 1f, 0.25f);

    void RefreshDifficulty()
    {
        DifficultyLevel current = Difficulty.Current;
        if (difficultyButtons != null)
        {
            for (int i = 0; i < difficultyButtons.Length; i++)
            {
                Button b = difficultyButtons[i];
                if (b == null) continue;
                bool on = (int)current == i;
                ColorBlock colors = b.colors;
                colors.normalColor = on ? PeruTheme.Red : DifficultyIdle;
                colors.selectedColor = on ? PeruTheme.RedLight : DifficultyIdle;
                b.colors = colors;
                if (b.TryGetComponent(out Image border)) border.color = on ? PeruTheme.White : DifficultyIdleBorder;
                b.transform.localScale = Vector3.one * (on ? 1.06f : 1f);
            }
        }

        // Descripciones de las pruebas según la dificultad.
        SetDescription(destrezaButton, "Pilota la nave: " + Route(current));
        SetDescription(memoriaButton, $"Repite la secuencia: {Difficulty.MemoryLevels(current)} niveles");
        SetDescription(defensaButton, $"Defensa espacial: derriba {Difficulty.DefensaKillsToWin(current)} naves");
    }

    static string Route(DifficultyLevel d)
    {
        int n = Difficulty.DestrezaLevelCount(d);
        return n == 1 ? "Under" : n == 2 ? "Under y Through" : "Under, Through y Over";
    }

    static void SetDescription(Button button, string text)
    {
        if (button == null) return;
        Transform t = button.transform.Find("Description");
        if (t != null && t.TryGetComponent(out Text label)) label.text = text;
    }

    static void SetStatus(Text label, bool completed)
    {
        if (label == null) return;
        label.text = completed ? "COMPLETADA" : "PENDIENTE";
        label.color = completed ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.55f);
    }

    static void UpdateCard(RectTransform card, Image border, Graphic silhouette, bool isSelected, Color color)
    {
        border.color = isSelected ? color : UnselectedBorder;
        silhouette.color = isSelected ? Color.Lerp(Color.white, color, 0.5f) : UnselectedSilhouette;
        card.localScale = Vector3.one * (isSelected ? SelectedScale : 1f);
    }
}
