using UnityEngine;
using UnityEngine.UI;

// Desplaza lentamente una textura de estrellas en un RawImage para dar sensación de movimiento.
[RequireComponent(typeof(RawImage))]
public class StarfieldScroll : MonoBehaviour
{
    [SerializeField] Vector2 scrollSpeed = new Vector2(0.01f, 0.005f);
    [Tooltip("Cuántas veces se repite la textura en vertical (más = estrellas más pequeñas).")]
    [SerializeField] float tiling = 1f;

    RawImage rawImage;
    RectTransform rectTransform;

    void Awake()
    {
        rawImage = GetComponent<RawImage>();
        rectTransform = (RectTransform)transform;
    }

    void Update()
    {
        Rect uv = rawImage.uvRect;

        // Mantener la proporción de la textura (cuadrada) sin estirarla.
        Vector2 size = rectTransform.rect.size;
        float aspect = size.y > 0f ? size.x / size.y : 1f;
        uv.height = tiling;
        uv.width = tiling * aspect;

        uv.position += scrollSpeed * Time.unscaledDeltaTime;
        uv.x = Mathf.Repeat(uv.x, 1f);
        uv.y = Mathf.Repeat(uv.y, 1f);

        rawImage.uvRect = uv;
    }
}
