using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Prueba de Memoria Espacial (estilo "Simón dice" con 3 cristales).
//  - Los cristales se iluminan en una secuencia aleatoria; el jugador la repite con el ratón.
//  - Acierto: sube de nivel (la secuencia crece en 1 y se reproduce más rápido).
//  - Fallo: pierde una oportunidad y se repite la misma secuencia. 3 oportunidades.
//  - Superar el último nivel completa la prueba (TrialProgress.CompleteMemoria).
public class MemoryGame : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] Crystal[] crystals;
    [SerializeField] MemoryGameUI ui;
    [SerializeField] AudioSource audioSource;
    [SerializeField] Transform cameraRig;

    [Header("Reglas")]
    [Tooltip("Niveles a superar para aprobar la prueba.")]
    [SerializeField] int levelsToWin = 10;
    [SerializeField] int startLives = 3;
    [Tooltip("Cristales de la secuencia en el nivel 1. Cada nivel añade uno.")]
    [SerializeField] int startSequenceLength = 2;

    [Header("Dificultad (nivel 1 → último nivel)")]
    [SerializeField] float flashDurationStart = 0.65f;
    [SerializeField] float flashDurationEnd = 0.28f;
    [SerializeField] float gapStart = 0.35f;
    [SerializeField] float gapEnd = 0.1f;

    [Header("Navegación")]
    [Tooltip("Escena a la que lleva VOLVER (pantalla de elección de prueba).")]
    [SerializeField] string backSceneName = TrialProgress.SelectScene;

    readonly List<int> sequence = new List<int>();
    int level;
    int lives;
    int inputIndex;
    bool acceptingInput;
    bool playing;
    Coroutine routine;

    public int Level => level;
    public int Lives => lives;

    void Start()
    {
        // Niveles según la dificultad elegida: Fácil 3, Intermedio 6, Avanzado 10.
        levelsToWin = Difficulty.MemoryLevels(Difficulty.Current);
        // Las instrucciones de la escena dicen "nivel 10": se ajustan a la dificultad.
        foreach (UnityEngine.UI.Text t in FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.text.Contains("<b>nivel 10</b>"))
                t.text = t.text.Replace("<b>nivel 10</b>", $"<b>nivel {levelsToWin}</b> ({Difficulty.CurrentName})");

        foreach (Crystal c in crystals)
        {
            c.Clicked += OnCrystalClicked;
            c.Interactable = false;
        }
        if (!TryGetComponent(out MemoryVoice _)) gameObject.AddComponent<MemoryVoice>();   // cristales por voz
        ui.ShowInstructions();
    }

    void OnDestroy()
    {
        foreach (Crystal c in crystals)
            if (c != null) c.Clicked -= OnCrystalClicked;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Back();
    }

    // ---------- Botones ----------

    // JUGAR y REINTENTAR.
    public void Play()
    {
        if (routine != null) StopCoroutine(routine);
        level = 1;
        lives = startLives;
        sequence.Clear();
        for (int i = 0; i < startSequenceLength; i++) AddRandomStep();
        playing = true;

        ui.ShowGame();
        ui.SetLives(lives);
        routine = StartCoroutine(PlayRound(0.6f));
    }

    // VOLVER: a la elección de prueba.
    public void Back()
    {
        SceneManager.LoadScene(backSceneName);
    }

    // ---------- Rondas ----------

    void AddRandomStep()
    {
        int next = Random.Range(0, crystals.Length);
        // Evita tres cristales iguales seguidos (se vuelve confuso).
        int n = sequence.Count;
        if (n >= 2 && sequence[n - 1] == next && sequence[n - 2] == next)
            next = (next + Random.Range(1, crystals.Length)) % crystals.Length;
        sequence.Add(next);
    }

    // Velocidad: siempre en la escala de 10 niveles, así en Fácil el último nivel (3) sigue siendo lento.
    float SpeedFactor => Mathf.Clamp01((level - 1f) / (global::Difficulty.MemorySpeedScaleLevels - 1f));

    IEnumerator PlayRound(float delay)
    {
        SetCrystalsInteractable(false);
        acceptingInput = false;
        ui.SetLevel(level, levelsToWin);
        ui.SetStatus("OBSERVA LA SECUENCIA");
        ui.SetProgress(0, 0);

        yield return new WaitForSeconds(delay);

        float flash = Mathf.Lerp(flashDurationStart, flashDurationEnd, SpeedFactor);
        float gap = Mathf.Lerp(gapStart, gapEnd, SpeedFactor);
        foreach (int step in sequence)
        {
            LightCrystal(step, flash);
            yield return new WaitForSeconds(flash + gap);
        }

        inputIndex = 0;
        acceptingInput = true;
        SetCrystalsInteractable(true);
        ui.SetStatus("TU TURNO: REPITE LA SECUENCIA");
        ui.SetProgress(0, sequence.Count);
        routine = null;
    }

    // Elegir un cristal sin ratón (voz): 0 = azul, 1 = verde, 2 = naranja.
    public void SelectCrystal(int index)
    {
        if (index < 0 || index >= crystals.Length) return;
        OnCrystalClicked(crystals[index]);
    }

    void OnCrystalClicked(Crystal crystal)
    {
        if (!acceptingInput || !playing) return;

        LightCrystal(crystal.Index, 0.3f);

        if (crystal.Index == sequence[inputIndex])
        {
            inputIndex++;
            ui.SetProgress(inputIndex, sequence.Count);
            if (inputIndex >= sequence.Count)
                routine = StartCoroutine(RoundWon());
        }
        else
        {
            routine = StartCoroutine(RoundLost(sequence[inputIndex]));
        }
    }

    IEnumerator RoundWon()
    {
        acceptingInput = false;
        SetCrystalsInteractable(false);
        yield return new WaitForSeconds(0.35f);

        if (level >= levelsToWin)
        {
            yield return Victory();
            yield break;
        }

        audioSource.PlayOneShot(MemoryAudio.Success);
        ui.ShowFeedback(true, "¡SECUENCIA CORRECTA!");
        level++;
        AddRandomStep();
        routine = StartCoroutine(PlayRound(1.3f));
    }

    IEnumerator RoundLost(int expected)
    {
        acceptingInput = false;
        SetCrystalsInteractable(false);
        lives--;
        ui.SetLives(lives);
        audioSource.PlayOneShot(MemoryAudio.Error);
        StartCoroutine(Shake(0.35f, 0.08f));

        string hint = $"Tocaba el cristal {crystals[expected].DisplayName.ToUpperInvariant()}";
        ui.ShowFeedback(false, lives > 0
            ? $"SECUENCIA INCORRECTA\n<size=30>{hint}. Te quedan {lives} {(lives == 1 ? "oportunidad" : "oportunidades")}</size>"
            : "SECUENCIA INCORRECTA", 1.6f);

        // Muestra cuál era el cristal correcto.
        yield return new WaitForSeconds(0.5f);
        crystals[expected].Flash(0.6f);

        if (lives <= 0)
        {
            yield return new WaitForSeconds(1.4f);
            playing = false;
            audioSource.PlayOneShot(MemoryAudio.Defeat);
            ui.ShowDefeat(level);
            yield break;
        }

        // Se repite la misma secuencia.
        routine = StartCoroutine(PlayRound(1.6f));
    }

    IEnumerator Victory()
    {
        playing = false;
        audioSource.PlayOneShot(MemoryAudio.Victory);
        // Celebración: los tres cristales se encienden en cascada.
        for (int r = 0; r < 2; r++)
            foreach (Crystal c in crystals)
            {
                c.Flash(0.35f);
                yield return new WaitForSeconds(0.15f);
            }
        yield return new WaitForSeconds(0.5f);
        ui.HideAll();
        VictoryScreen.Show(Trial.Memoria, Play, "Volver");
    }

    // ---------- Utilidades ----------

    void LightCrystal(int index, float duration)
    {
        crystals[index].Flash(duration);
        audioSource.PlayOneShot(MemoryAudio.Crystal(index));
    }

    void SetCrystalsInteractable(bool value)
    {
        foreach (Crystal c in crystals) c.Interactable = value;
    }

    IEnumerator Shake(float duration, float strength)
    {
        if (cameraRig == null) yield break;
        Vector3 origin = cameraRig.localPosition;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = 1f - t / duration;
            cameraRig.localPosition = origin + (Vector3)(Random.insideUnitCircle * strength * k);
            yield return null;
        }
        cameraRig.localPosition = origin;
    }
}
