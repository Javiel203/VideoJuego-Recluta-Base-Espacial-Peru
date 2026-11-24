using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Escena "Historia": el texto aparece letra a letra (efecto máquina de escribir).
// El botón principal primero dice SALTAR (muestra todo) y luego CONTINUAR.
public class Story : MonoBehaviour
{
    public const string StoryText =
        "El Perú ha inaugurado su primera base espacial y está buscando nuevos pilotos.\n\n" +
        "Para seleccionar a sus nuevos pilotos, la base ha organizado una prueba de entrenamiento espacial.\n\n" +
        "Tu misión: demostrar tus habilidades, superar la prueba y convertirte en uno de los primeros " +
        "pilotos espaciales del Perú.";

    public const string FinalLine = "¡El futuro de la exploración espacial peruana está en tus manos!";

    [SerializeField] Text bodyText;
    [SerializeField] Text finalText;
    [SerializeField] Text continueLabel;
    [SerializeField] float charactersPerSecond = 45f;
    [SerializeField] string nextSceneName = "CharacterSelect";
    [SerializeField] string menuSceneName = "MainMenu";

    bool finished;
    Coroutine typing;

    void Start()
    {
        bodyText.text = "";
        finalText.text = "";
        SetLabel("Saltar");
        typing = StartCoroutine(TypeAll());
    }

    IEnumerator TypeAll()
    {
        yield return Type(bodyText, StoryText);
        yield return new WaitForSeconds(0.4f);
        yield return Type(finalText, FinalLine);
        Finish();
    }

    IEnumerator Type(Text target, string content)
    {
        float shown = 0f;
        int lastPause = -1;
        while (shown < content.Length)
        {
            shown += charactersPerSecond * Time.deltaTime;
            int count = Mathf.Min(content.Length, Mathf.FloorToInt(shown));
            if (count > target.text.Length && !char.IsWhiteSpace(content[count - 1])) KeySound.Play(0.4f);   // teclado
            target.text = content.Substring(0, count);
            // Pequeña pausa al final de cada frase (una sola vez por signo).
            int last = count - 1;
            if (last > lastPause && last < content.Length - 1 && (content[last] == '.' || content[last] == ':'))
            {
                lastPause = last;
                shown = count;
                yield return new WaitForSeconds(0.25f);
            }
            yield return null;
        }
        target.text = content;
    }

    void Finish()
    {
        if (typing != null) StopCoroutine(typing);
        bodyText.text = StoryText;
        finalText.text = FinalLine;
        finished = true;
        SetLabel("Continuar");
    }

    void SetLabel(string label)
    {
        if (continueLabel != null) continueLabel.text = label.ToUpperInvariant();
    }

    // Primera pulsación: muestra todo el texto. Segunda: pasa a las instrucciones.
    public void Continue()
    {
        if (!finished)
        {
            Finish();
            return;
        }
        SceneManager.LoadScene(nextSceneName);
    }

    public void Back() => SceneManager.LoadScene(menuSceneName);
}
