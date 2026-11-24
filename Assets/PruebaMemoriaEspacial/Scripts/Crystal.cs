using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// Cristal luminoso de la Prueba de Memoria. Flota y gira; se ilumina al mostrar la secuencia
// o al hacer clic. El clic llega por el EventSystem (PhysicsRaycaster en la cámara + MeshCollider).
public class Crystal : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("0 = azul, 1 = verde, 2 = naranja. También elige su sonido.")]
    [SerializeField] int index;
    [SerializeField] string displayName = "Azul";
    [ColorUsage(false, true)]
    [SerializeField] Color glowColor = new Color(0.2f, 0.6f, 1f);

    [Header("Referencias")]
    [SerializeField] Renderer crystalRenderer;
    [SerializeField] Renderer rimRenderer;
    [SerializeField] Light glowLight;
    [SerializeField] ParticleSystem sparks;

    [Header("Brillo")]
    [SerializeField] float idleEmission = 0.6f;
    [SerializeField] float hoverEmission = 1.6f;
    [SerializeField] float litEmission = 7f;
    [SerializeField] float idleLight = 0.6f;
    [SerializeField] float litLight = 9f;

    [Header("Animación")]
    [SerializeField] float spinSpeed = 25f;
    [SerializeField] float bobHeight = 0.08f;
    [SerializeField] float bobSpeed = 1.6f;
    [SerializeField] float litScale = 1.18f;

    public int Index => index;
    public string DisplayName => displayName;
    public Color GlowColor => glowColor;

    // Lo escucha MemoryGame.
    public event System.Action<Crystal> Clicked;

    public bool Interactable { get; set; }

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    MaterialPropertyBlock block;
    Vector3 basePosition;
    Vector3 baseScale;
    float lit;          // 0..1 brillo actual de "encendido"
    float hover;        // 0..1
    bool hovered;
    Coroutine flashRoutine;
    float bobPhase;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        basePosition = transform.localPosition;
        baseScale = transform.localScale;
        bobPhase = index * 1.3f;
        ApplyVisuals();
    }

    void OnDisable()
    {
        lit = 0f;
        hovered = false;
    }

    void Update()
    {
        float targetHover = hovered && Interactable ? 1f : 0f;
        hover = Mathf.MoveTowards(hover, targetHover, Time.deltaTime * 6f);

        float spin = spinSpeed * (1f + lit * 4f);
        transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
        float bob = Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobHeight;
        transform.localPosition = basePosition + new Vector3(0f, bob, 0f);
        float scale = 1f + (litScale - 1f) * lit + 0.04f * hover;
        transform.localScale = baseScale * scale;

        ApplyVisuals();
    }

    // Enciende el cristal durante 'duration' segundos (con subida y bajada suaves).
    public void Flash(float duration)
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(duration));
        if (sparks != null) sparks.Emit(24);
    }

    IEnumerator FlashRoutine(float duration)
    {
        float rise = Mathf.Min(0.06f, duration * 0.25f);
        float fall = Mathf.Min(0.18f, duration * 0.4f);
        float hold = Mathf.Max(0f, duration - rise - fall);

        for (float t = 0f; t < rise; t += Time.deltaTime) { lit = t / rise; yield return null; }
        lit = 1f;
        yield return new WaitForSeconds(hold);
        for (float t = 0f; t < fall; t += Time.deltaTime) { lit = 1f - t / fall; yield return null; }
        lit = 0f;
        flashRoutine = null;
    }

    void ApplyVisuals()
    {
        float emission = Mathf.Lerp(idleEmission, hoverEmission, hover);
        emission = Mathf.Lerp(emission, litEmission, lit);
        SetEmission(crystalRenderer, glowColor * emission);
        SetEmission(rimRenderer, glowColor * (1.5f + lit * 5f));
        if (glowLight != null) glowLight.intensity = Mathf.Lerp(idleLight, litLight, lit) + hover * 0.8f;
    }

    void SetEmission(Renderer r, Color c)
    {
        if (r == null) return;
        r.GetPropertyBlock(block);
        block.SetColor(EmissionId, c);
        r.SetPropertyBlock(block);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!Interactable || eventData.button != PointerEventData.InputButton.Left) return;
        Clicked?.Invoke(this);
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;
}
