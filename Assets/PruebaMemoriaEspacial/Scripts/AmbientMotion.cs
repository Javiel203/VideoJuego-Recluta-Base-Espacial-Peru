using UnityEngine;

// Movimiento decorativo del ambiente: rotación continua y/o pulso de una luz.
public class AmbientMotion : MonoBehaviour
{
    [SerializeField] Vector3 rotationSpeed = new Vector3(0f, 20f, 0f);

    [Header("Pulso de luz (opcional)")]
    [SerializeField] Light pulseLight;
    [SerializeField] float pulseMin = 0.6f;
    [SerializeField] float pulseMax = 1.4f;
    [SerializeField] float pulseSpeed = 1.2f;

    float phase;

    void Awake() => phase = Random.value * 10f;

    void Update()
    {
        if (rotationSpeed != Vector3.zero)
            transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);

        if (pulseLight != null)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed + phase);
            pulseLight.intensity = Mathf.Lerp(pulseMin, pulseMax, k);
        }
    }
}
