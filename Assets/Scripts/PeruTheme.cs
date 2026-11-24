using UnityEngine;

// Identidad visual de "RECLUTA: Base Espacial del Perú".
// Colores, fuentes y texturas (bandera, emblema ficticio, sprite de botón).
// Las texturas se cargan de Resources/Peru (las genera Tools > RECLUTA > Reconstruir todo)
// y, si no existen, se generan por código en tiempo de ejecución.
public static class PeruTheme
{
    public const string GameTitle = "RECLUTA";
    public const string GameSubtitle = "BASE ESPACIAL DEL PERÚ";
    public const string ProductName = "RECLUTA - Base Espacial del Perú";

    // ---------- Colores ----------
    public static readonly Color Red = new Color(0.851f, 0.063f, 0.137f);        // #D91023 rojo bandera
    public static readonly Color RedLight = new Color(0.95f, 0.25f, 0.3f);
    public static readonly Color RedDark = new Color(0.45f, 0.02f, 0.07f);
    public static readonly Color White = Color.white;
    public static readonly Color Night = new Color(0.015f, 0.02f, 0.06f);        // fondo espacial
    public static readonly Color Panel = new Color(0.05f, 0.06f, 0.14f, 0.9f);
    public static readonly Color PanelLight = new Color(0.1f, 0.12f, 0.24f, 0.95f);
    public static readonly Color TextSoft = new Color(0.82f, 0.86f, 0.95f);
    public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.75f);

    // ---------- Fuentes (Resources/Fonts, licencia SIL OFL) ----------
    static Font titleFont, headingFont, bodyFont, bodyBoldFont;

    public static Font TitleFont => titleFont != null ? titleFont : (titleFont = LoadFont("Orbitron-Black"));
    public static Font HeadingFont => headingFont != null ? headingFont : (headingFont = LoadFont("Orbitron-Bold"));
    public static Font BodyFont => bodyFont != null ? bodyFont : (bodyFont = LoadFont("Exo2-Medium"));
    public static Font BodyBoldFont => bodyBoldFont != null ? bodyBoldFont : (bodyBoldFont = LoadFont("Exo2-Bold"));

    static Font LoadFont(string name)
    {
        Font f = Resources.Load<Font>("Fonts/" + name);
        return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    // ---------- Texturas ----------
    public const string ResourcesFolder = "Peru";
    public const int RoundedBorder = 24;

    static Texture2D flag, emblem;
    static Sprite rounded;

    public static Texture2D Flag => flag != null ? flag : (flag = Load("Flag") ?? GenerateFlag(300, 200));
    public static Texture2D Emblem => emblem != null ? emblem : (emblem = Load("Emblem") ?? GenerateEmblem(512));

    public static Sprite RoundedSprite
    {
        get
        {
            if (rounded != null) return rounded;
            rounded = Resources.Load<Sprite>(ResourcesFolder + "/Rounded");
            if (rounded == null)
            {
                Texture2D tex = GenerateRounded(64, 22);
                rounded = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, Vector4.one * RoundedBorder);
            }
            return rounded;
        }
    }

    // Olvida lo cargado (lo usa el Editor tras regenerar los PNG).
    public static void ClearCache()
    {
        flag = emblem = null;
        rounded = null;
        titleFont = headingFont = bodyFont = bodyBoldFont = null;
    }

    static Texture2D Load(string name)
    {
        Texture2D t = Resources.Load<Texture2D>(ResourcesFolder + "/" + name);
        return t != null ? t : null;
    }

    // Bandera del Perú (bandera civil, sin escudo): rojo | blanco | rojo en vertical.
    public static Texture2D GenerateFlag(int w, int h)
    {
        var tex = NewTexture(w, h);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                px[y * w + x] = (u < 1f / 3f || u > 2f / 3f) ? Red : White;
            }
        tex.SetPixels(px);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    // Rectángulo redondeado blanco (se tiñe con Image.color). Para botones y paneles 9-slice.
    public static Texture2D GenerateRounded(int size, float radius)
    {
        var tex = NewTexture(size, size);
        var px = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                float d = RoundBox(p, new Vector2(half - 1f, half - 1f), radius);
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
            }
        tex.SetPixels(px);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    // Emblema ficticio de la base: aro rojo y blanco, disco nocturno con órbita,
    // cohete blanco con ventanilla roja, estrella, y franja inferior con los colores patrios.
    public static Texture2D GenerateEmblem(int size)
    {
        var tex = NewTexture(size, size);
        var px = new Color[size * size];
        float s = size / 512f;   // todo está diseñado a 512 px
        var c = new Vector2(256f, 256f);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / s, (y + 0.5f) / s);
                float r = (p - c).magnitude;
                Color col = Color.clear;

                // Aro exterior rojo, filete blanco, disco nocturno.
                col = Over(col, Red, Fill(r - 250f, s));
                col = Over(col, White, Fill(r - 226f, s));
                col = Over(col, Red, Fill(r - 214f, s));
                col = Over(col, Night, Fill(r - 200f, s));

                // Franja inferior del disco con rojo | blanco | rojo.
                float band = Mathf.Max(r - 200f, p.y - 150f);
                if (band < 1f)
                {
                    Color bandCol = (p.x < 256f - 60f || p.x > 256f + 60f) ? Red : White;
                    col = Over(col, bandCol, Fill(band, s));
                }

                // Órbita (elipse inclinada).
                Vector2 q = Rotate(p - c, -20f);
                float ell = Mathf.Abs(new Vector2(q.x / 175f, q.y / 62f).magnitude - 1f) * 62f - 5f;
                col = Over(col, new Color(1f, 1f, 1f, 0.85f), Fill(ell, s) * (r < 200f ? 1f : 0f));

                // Cohete (inclinado 30°).
                Vector2 rp = Rotate(p - new Vector2(256f, 270f), 30f);
                float body = new Vector2(rp.x / 38f, rp.y / 105f).magnitude * 38f - 38f;
                float finL = RoundBox(Rotate(rp - new Vector2(-30f, -72f), -20f), new Vector2(13f, 32f), 6f);
                float finR = RoundBox(Rotate(rp - new Vector2(30f, -72f), 20f), new Vector2(13f, 32f), 6f);
                float rocket = Mathf.Min(body, Mathf.Min(finL, finR));
                float flame = new Vector2(rp.x / 20f, (rp.y + 120f) / 34f).magnitude * 20f - 20f;
                col = Over(col, new Color(1f, 0.55f, 0.15f), Fill(flame, s));
                col = Over(col, White, Fill(rocket, s));
                float window = (rp - new Vector2(0f, 25f)).magnitude - 17f;
                col = Over(col, Red, Fill(window, s));

                // Estrella blanca de cinco puntas.
                float star = Star(p - new Vector2(372f, 345f), 34f, 13f);
                col = Over(col, White, Fill(star, s));

                px[y * size + x] = col;
            }
        tex.SetPixels(px);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    // ---------- utilidades de dibujo ----------

    static Texture2D NewTexture(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false);

    static float Fill(float dist, float scale) => Mathf.Clamp01(0.5f - dist * scale);

    static Color Over(Color dst, Color src, float a)
    {
        a *= src.a;
        if (a <= 0f) return dst;
        float outA = a + dst.a * (1f - a);
        Color rgb = (src * a + dst * dst.a * (1f - a)) / Mathf.Max(outA, 1e-5f);
        rgb.a = outA;
        return rgb;
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }

    static float RoundBox(Vector2 p, Vector2 half, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(radius, radius);
        Vector2 outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
        return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }

    // Distancia aproximada a una estrella de 5 puntas (negativo = dentro).
    static float Star(Vector2 p, float outer, float inner)
    {
        float angle = Mathf.Atan2(p.x, p.y);          // 0 = punta hacia arriba
        float sector = Mathf.PI * 2f / 5f;
        float a = Mathf.Repeat(angle + sector * 0.5f, sector) - sector * 0.5f;
        float t = Mathf.Abs(a) / (sector * 0.5f);     // 0 en la punta, 1 en el valle
        float radius = inner + (outer - inner) * (1f - t) * (1f - t);   // lados cóncavos
        return p.magnitude - radius;
    }
}
