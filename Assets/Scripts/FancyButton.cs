using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Animación y sonido para botones: crece al pasar el ratón o al seleccionarlo con teclado,
// se encoge un poco al pulsarlo y hace un "clic" sintetizado (no necesita archivos de audio).
public class FancyButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler, ISubmitHandler, IPointerClickHandler
{
    [SerializeField] float hoverScale = 1.07f;
    [SerializeField] float pressedScale = 0.96f;
    [SerializeField] float speed = 14f;
    [SerializeField] bool playClickSound = true;

    bool hovered, selected, pressed;
    Selectable selectable;

    static AudioClip clickClip;
    static AudioSource sfxSource;

    void Awake() => selectable = GetComponent<Selectable>();

    void OnDisable()
    {
        hovered = selected = pressed = false;
        transform.localScale = Vector3.one;
    }

    void Update()
    {
        bool interactable = selectable == null || selectable.IsInteractable();
        float target = !interactable ? 1f : pressed ? pressedScale : (hovered || selected) ? hoverScale : 1f;
        float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
        transform.localScale = new Vector3(s, s, 1f);
    }

    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) => pressed = true;
    public void OnPointerUp(PointerEventData e) => pressed = false;
    public void OnSelect(BaseEventData e) => selected = true;
    public void OnDeselect(BaseEventData e) => selected = false;
    public void OnPointerClick(PointerEventData e) => Click();
    public void OnSubmit(BaseEventData e) => Click();

    void Click()
    {
        if (!playClickSound || (selectable != null && !selectable.IsInteractable())) return;
        PlayClick();
    }

    public static void PlayClick()
    {
        if (sfxSource == null)
        {
            var go = new GameObject("UI_Sfx");
            DontDestroyOnLoad(go);   // el sonido sigue aunque el botón cambie de escena
            sfxSource = go.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.volume = 0.5f;
        }
        if (clickClip == null) clickClip = CreateClickClip();
        sfxSource.PlayOneShot(clickClip);
    }

    // "Bip" corto ascendente con caída rápida.
    static AudioClip CreateClickClip()
    {
        const int rate = 44100;
        const float duration = 0.09f;
        int n = Mathf.CeilToInt(rate * duration);
        var data = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float freq = Mathf.Lerp(900f, 1500f, t);
            phase += 2.0 * Mathf.PI * freq / rate;
            float env = Mathf.Exp(-t * 6f) * Mathf.Clamp01(i / 60f);
            data[i] = (float)System.Math.Sin(phase) * env * 0.6f;
        }
        var clip = AudioClip.Create("UIClick", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
