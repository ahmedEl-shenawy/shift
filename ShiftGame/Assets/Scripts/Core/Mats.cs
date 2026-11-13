using UnityEngine;

/// <summary>Runtime material factory. Base materials live in Resources so their shaders/variants ship in builds.</summary>
public static class Mats
{
    static Material lit, trans, particle;
    static Texture2D softDot;

    static Material Base(ref Material cache, string res, string fallbackShader)
    {
        if (cache == null)
        {
            cache = Resources.Load<Material>(res);
            if (cache == null) cache = new Material(Shader.Find(fallbackShader));
        }
        return cache;
    }

    public static Material Lit(Color c, float emission = 0f, float smoothness = 0.3f)
    {
        var m = new Material(Base(ref lit, "Mat_Lit", "Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetColor("_EmissionColor", c * emission);
        m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    public static Material Transparent(Color c, float alpha, float emission = 0f)
    {
        var m = new Material(Base(ref trans, "Mat_Transparent", "Universal Render Pipeline/Lit"));
        c.a = alpha;
        m.SetColor("_BaseColor", c);
        m.SetColor("_EmissionColor", new Color(c.r, c.g, c.b) * emission);
        m.SetFloat("_Smoothness", 0.6f);
        return m;
    }

    public static Material Particle(Color c)
    {
        var m = new Material(Base(ref particle, "Mat_Particle", "Universal Render Pipeline/Particles/Unlit"));
        m.SetColor("_BaseColor", c);
        m.SetTexture("_BaseMap", SoftDot);
        return m;
    }

    public static void SetColor(Material m, Color c, float emission)
    {
        m.SetColor("_BaseColor", c);
        m.SetColor("_EmissionColor", new Color(c.r, c.g, c.b) * emission);
    }

    public static Texture2D SoftDot
    {
        get
        {
            if (softDot != null) return softDot;
            const int s = 64;
            softDot = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2 - 1, dy = (y + 0.5f) / s * 2 - 1;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1 - d);
                a = a * a;
                px[y * s + x] = new Color(1, 1, 1, a);
            }
            softDot.SetPixels(px);
            softDot.Apply();
            return softDot;
        }
    }
}
