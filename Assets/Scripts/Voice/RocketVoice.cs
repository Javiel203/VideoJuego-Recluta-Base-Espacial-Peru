using UnityEngine;

// Voz en la PRUEBA DE DESTREZA (cohete). Movment lo añade solo.
//  - Propulsión por SONIDO: mientras el micrófono capte la voz del jugador ("suuuube", "aaaa"…),
//    cada instante con sonido da 0,25 s de propulsión; en silencio el propulsor se apaga.
//  - "izquierda" / "derecha": giro de unos segundos. "alto" / "para": detiene el giro.
[RequireComponent(typeof(Movment))]
public class RocketVoice : MonoBehaviour
{
    [Tooltip("Segundos de propulsión que da cada instante con sonido.")]
    [SerializeField] float thrustPerSound = 0.25f;
    [Tooltip("Segundos que dura el giro tras decir \"izquierda\" o \"derecha\".")]
    [SerializeField] float rotationDuration = 0.6f;

    Movment ship;
    VoiceContext context;

    void Awake()
    {
        ship = GetComponent<Movment>();
        context = new VoiceContext(
            "VOZ:  haz sonido (\"suuuube\") = propulsor   ·   IZQUIERDA   ·   DERECHA   ·   ALTO")
        { UsesMicLevel = true }
            .On(() => Rotate(-1f), "izquierda")
            .On(() => Rotate(1f), "derecha")
            .On(Stop, "alto", "para");
    }

    void OnEnable() => VoiceCommands.Push(context);
    void OnDisable() => VoiceCommands.Pop(context);

    void Update()
    {
        if (ship.enabled && VoiceCommands.SoundActive)
            ship.ThrustFor(thrustPerSound);   // se renueva mientras siga el sonido
    }

    void Rotate(float direction)
    {
        if (ship.enabled) ship.RotateFor(direction, rotationDuration);
    }

    void Stop()
    {
        if (ship.enabled) ship.StopExternalInput();
    }
}
