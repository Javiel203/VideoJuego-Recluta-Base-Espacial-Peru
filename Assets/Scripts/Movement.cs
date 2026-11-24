using UnityEngine;
using UnityEngine.InputSystem;

public class Movment : MonoBehaviour
{
    [SerializeField] InputAction thrustAction;
    [SerializeField] InputAction rotationAction;
    [SerializeField] float ThrustForce = 150f;
    [SerializeField] float RotationForce = 10f;
    [SerializeField] ParticleSystem mainThrustersParticles;
    [SerializeField] ParticleSystem LeftThrustersParticles;
    [SerializeField] ParticleSystem RightThrustersParticles;
    [Tooltip("Volumen del propulsor cuando lo activa la VOZ (0..1). Más bajo = menos eco en el micrófono.")]
    [SerializeField, Range(0f, 1f)] float voiceThrustVolume = 0.4f;

    Rigidbody rb;
    AudioSource audioSource;
    float baseVolume = 1f;

    // Órdenes externas (voz): se mantienen activas hasta este instante.
    float externalThrustUntil;
    float externalRotationUntil;
    float externalRotation;   // -1 = izquierda, +1 = derecha

    void Awake()
    {
        // Control por voz (propulsión por sonido + giros).
        if (!TryGetComponent(out RocketVoice _)) gameObject.AddComponent<RocketVoice>();
        // Controles táctiles (móvil).
        if (!TryGetComponent(out RocketTouch _)) gameObject.AddComponent<RocketTouch>();
    }

    // Botones táctiles: se mantienen mientras el dedo está apoyado.
    bool touchThrust;
    float touchRotation;   // -1 = izquierda, +1 = derecha

    public void SetTouchInput(bool thrust, float rotation)
    {
        touchThrust = thrust;
        touchRotation = rotation;
    }

    // Empuja durante 'duration' segundos (propulsión por voz: se renueva mientras haya sonido).
    public void ThrustFor(float duration)
    {
        externalThrustUntil = Mathf.Max(externalThrustUntil, Time.time + duration);
    }

    // Gira durante 'duration' segundos. direction: -1 izquierda, +1 derecha.
    public void RotateFor(float direction, float duration)
    {
        externalRotation = Mathf.Sign(direction);
        externalRotationUntil = Time.time + duration;
    }

    // Cancela cualquier orden de voz activa (orden "alto" / "para").
    public void StopExternalInput()
    {
        externalThrustUntil = 0f;
        externalRotationUntil = 0f;
    }
    void OnEnable()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        baseVolume = audioSource.volume;
        thrustAction.Enable();
        rotationAction.Enable();
    }
    private void FixedUpdate()
    {
        ProcessThrust();
        ProcessRotation();
    }

    void ProcessThrust()
    {
        bool manualThrust = thrustAction.IsPressed() || touchThrust;
        if (manualThrust || Time.time < externalThrustUntil)
        {
            // Con voz el propulsor suena más bajo para que el micrófono no lo confunda con el jugador.
            audioSource.volume = manualThrust ? baseVolume : baseVolume * voiceThrustVolume;
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
            rb.AddRelativeForce(Vector3.up * ThrustForce * Time.deltaTime);
            if (!mainThrustersParticles.isPlaying)
            {

                mainThrustersParticles.Play();
            }
        }
        else
        {
            mainThrustersParticles.Stop();
            audioSource.Stop();
            audioSource.volume = baseVolume;
        }
    }

    // El AudioSource es compartido con los sonidos de choque / éxito (CollisionHandler).
    void OnDisable()
    {
        if (audioSource != null) audioSource.volume = baseVolume;
    }
    void ProcessRotation()
    {
        float InputRotation = rotationAction.ReadValue<float>();
        if (InputRotation == 0f) InputRotation = touchRotation;
        if (InputRotation == 0f && Time.time < externalRotationUntil)
        {
            InputRotation = externalRotation;
        }
        if (InputRotation < 0)
        {
            ApplyRotation(RotationForce);
            if (!LeftThrustersParticles.isPlaying)
                LeftThrustersParticles.Play();
        }
        else if (InputRotation > 0) 
        {
            ApplyRotation(-RotationForce);
            if (!RightThrustersParticles.isPlaying)
                RightThrustersParticles.Play();

        }
        else
        {
            LeftThrustersParticles.Stop();
            RightThrustersParticles.Stop();
        }
    }
    void ApplyRotation(float rotationThisFrame)
    {
        rb.freezeRotation = false; // freezing rotation so we can manually rotate
        transform.Rotate(Vector3.forward * rotationThisFrame * Time.fixedDeltaTime);
        rb.freezeRotation = true; // freezing rotation so we can manually rotate

    }
}
