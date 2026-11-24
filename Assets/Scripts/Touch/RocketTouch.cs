using UnityEngine;

// Controles táctiles de la PRUEBA DE DESTREZA (cohete). Movment lo añade solo.
//   Abajo a la izquierda: IZQUIERDA / DERECHA (girar).   Abajo a la derecha: PROPULSOR.
// Se mantienen mientras el dedo está apoyado; se pueden usar a la vez (girar y propulsar).
[RequireComponent(typeof(Movment))]
public class RocketTouch : MonoBehaviour
{
    const float HudSpace = 250f;   // altura que ocupan los botones (para subir el indicador de voz)

    Movment ship;
    Canvas canvas;
    TouchHoldButton left, right, thrust;

    void Awake() => ship = GetComponent<Movment>();

    void Start()
    {
        canvas = TouchUI.CreateCanvas("RocketTouch", transform);
        canvas.transform.SetParent(null, false);   // que no herede la rotación de la nave
        Transform root = canvas.transform;

        left = TouchUI.HoldButton(root, "Izquierda", "IZQUIERDA", new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(250f, 190f), 26);
        right = TouchUI.HoldButton(root, "Derecha", "DERECHA", new Vector2(0f, 0f), new Vector2(310f, 40f), new Vector2(250f, 190f), 26);
        thrust = TouchUI.HoldButton(root, "Propulsor", "PROPULSOR", new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(330f, 220f), 30);

        TouchMode.Changed += Show;
        Show(TouchMode.Active);
    }

    void OnDestroy()
    {
        TouchMode.Changed -= Show;
        if (canvas != null) Destroy(canvas.gameObject);
        VoiceCommands.HudBottomOffset = 0f;
    }

    void Show(bool visible)
    {
        if (canvas != null) canvas.gameObject.SetActive(visible);
        VoiceCommands.HudBottomOffset = visible ? HudSpace : 0f;
        if (!visible && ship != null) ship.SetTouchInput(false, 0f);
    }

    void Update()
    {
        if (canvas == null || !canvas.gameObject.activeSelf) return;

        // Al aterrizar o chocar la nave se desactiva: los botones dejan de actuar y se ocultan
        // (para no tapar las pantallas de victoria o derrota).
        bool playing = ship.enabled;
        if (left.gameObject.activeSelf != playing)
        {
            left.gameObject.SetActive(playing);
            right.gameObject.SetActive(playing);
            thrust.gameObject.SetActive(playing);
        }

        float rotation = (right.IsHeld ? 1f : 0f) - (left.IsHeld ? 1f : 0f);
        ship.SetTouchInput(playing && thrust.IsHeld, playing ? rotation : 0f);
    }
}
