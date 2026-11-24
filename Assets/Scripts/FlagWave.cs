using UnityEngine;

// Malla de bandera que ondea: el borde pegado al mástil queda fijo y la ola crece hacia la punta.
[RequireComponent(typeof(MeshFilter))]
public class FlagWave : MonoBehaviour
{
    [SerializeField] float amplitude = 0.12f;
    [SerializeField] float frequency = 2.2f;
    [SerializeField] float waveSpeed = 3.5f;

    Mesh mesh;
    Vector3[] baseVertices;
    Vector3[] vertices;
    float width;
    float phaseOffset;

    // Crea una malla plana de width x height subdividida, anclada en x = 0 (lado del mástil).
    public static Mesh BuildMesh(float width, float height, int segX = 24, int segY = 10)
    {
        var verts = new Vector3[(segX + 1) * (segY + 1)];
        var uvs = new Vector2[verts.Length];
        var tris = new int[segX * segY * 6];
        for (int y = 0; y <= segY; y++)
            for (int x = 0; x <= segX; x++)
            {
                int i = y * (segX + 1) + x;
                float u = x / (float)segX, v = y / (float)segY;
                verts[i] = new Vector3(u * width, v * height - height, 0f);   // cuelga desde arriba
                uvs[i] = new Vector2(u, v);
            }
        int t = 0;
        for (int y = 0; y < segY; y++)
            for (int x = 0; x < segX; x++)
            {
                int i = y * (segX + 1) + x;
                // Orden para que la cara mire hacia -Z (hacia la cámara).
                tris[t++] = i; tris[t++] = i + segX + 1; tris[t++] = i + 1;
                tris[t++] = i + 1; tris[t++] = i + segX + 1; tris[t++] = i + segX + 2;
            }
        var mesh = new Mesh { name = "PeruFlagMesh" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        baseVertices = mesh.vertices;
        vertices = new Vector3[baseVertices.Length];
        foreach (Vector3 v in baseVertices) width = Mathf.Max(width, v.x);
        phaseOffset = Random.value * 10f;
    }

    void Update()
    {
        if (width <= 0f) return;
        float time = Time.time * waveSpeed + phaseOffset;
        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 v = baseVertices[i];
            float k = v.x / width;                       // 0 en el mástil, 1 en la punta
            float wave = Mathf.Sin(v.x * frequency * 2f - time) * amplitude * k;
            float droop = Mathf.Sin(time * 0.5f + v.x) * amplitude * 0.25f * k;
            vertices[i] = new Vector3(v.x, v.y + droop, v.z + wave);
        }
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
    }
}
