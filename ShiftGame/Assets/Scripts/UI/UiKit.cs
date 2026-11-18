using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Procedural UI toolkit: anti-aliased sprites drawn from signed distance functions (rounded panels and icons),
/// plus small factory helpers for building uGUI hierarchies in code.
/// </summary>
public static class UiKit
{
    public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
    static Font font;

    static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { sprites.Clear(); font = null; }

    // ------------------------------------------------------------------ colours

    public static readonly Color Glass = new Color(0.06f, 0.05f, 0.13f, 0.68f);
    public static readonly Color GlassLight = new Color(1f, 1f, 1f, 0.09f);
    public static readonly Color Ink = new Color(0.07f, 0.06f, 0.14f, 1f);
    public static readonly Color Soft = new Color(1f, 1f, 1f, 0.78f);

    public static Color Accent => Color.Lerp(new Color(1f, 0.70f, 0.32f), new Color(0.45f, 0.75f, 1f), PhaseState.Smooth);

    // candy palette (joyful UI)
    public static readonly Color Cream = new Color(1.00f, 0.975f, 0.93f);
    public static readonly Color CreamDeep = new Color(0.93f, 0.89f, 0.84f);
    public static readonly Color InkSoft = new Color(0.20f, 0.15f, 0.32f);
    public static readonly Color Sunny = new Color(1.00f, 0.72f, 0.22f);
    public static readonly Color Peach = new Color(1.00f, 0.55f, 0.38f);
    public static readonly Color Berry = new Color(1.00f, 0.40f, 0.60f);
    public static readonly Color Grape = new Color(0.62f, 0.44f, 1.00f);
    public static readonly Color Sky = new Color(0.33f, 0.66f, 1.00f);
    public static readonly Color Mint = new Color(0.30f, 0.84f, 0.62f);
    public static readonly Color Slate = new Color(0.52f, 0.52f, 0.68f);
    public static readonly Color[] Confetti = { Sunny, Peach, Berry, Grape, Sky, Mint, Color.white };

    public static Color Darker(Color c, float f = 0.72f) => new Color(c.r * f, c.g * f, c.b * f, c.a);
    public static Color Lighter(Color c, float f = 0.25f) => Color.Lerp(c, Color.white, f);

    // ------------------------------------------------------------------ SDF helpers

    static float Circle(Vector2 p, float r) => p.magnitude - r;

    static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - r;
    }

    static float Box(Vector2 p, Vector2 b, float r)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - b + new Vector2(r, r);
        return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - r;
    }

    static float Poly(Vector2 p, params Vector2[] v)
    {
        float area = 0;
        for (int i = 0; i < v.Length; i++) { var a = v[i]; var b = v[(i + 1) % v.Length]; area += a.x * b.y - b.x * a.y; }
        float sign = area > 0 ? 1 : -1;
        float d = float.MinValue;
        for (int i = 0; i < v.Length; i++)
        {
            var a = v[i]; var b = v[(i + 1) % v.Length];
            var e = (b - a).normalized;
            var n = new Vector2(e.y, -e.x) * sign;
            d = Mathf.Max(d, Vector2.Dot(p - a, n));
        }
        return d;
    }

    static Sprite Make(string key, int res, Func<Vector2, float, float> sdf, int border = 0)
    {
        if (sprites.TryGetValue(key, out var s) && s != null) return s;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = key
        };
        var px = new Color32[res * res];
        float pix = 2f / res;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            var p = new Vector2((x + 0.5f) / res * 2 - 1, (y + 0.5f) / res * 2 - 1);
            float d = sdf(p, pix);
            byte a = (byte)(Mathf.Clamp01(0.5f - d / pix) * 255);
            px[y * res + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(px);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
        s.name = key;
        sprites[key] = s;
        return s;
    }

    // ------------------------------------------------------------------ sprites

    /// <summary>9-sliced rounded rectangle (corner radius ~30 units at multiplier 1).</summary>
    public static Sprite Rounded => Make("rounded", 96, (p, px) => Box(p, Vector2.one * (1 - px), 0.62f), 34);

    /// <summary>9-sliced rounded outline.</summary>
    public static Sprite RoundedOutline => Make("roundedOutline", 96, (p, px) => Mathf.Abs(Box(p, Vector2.one * (1 - px * 3), 0.6f)) - px * 1.6f, 34);

    public static Sprite Dot => Make("dot", 96, (p, px) => Circle(p, 1 - px));

    public static Sprite SoftGlow => Make("glow", 96, (p, px) => p.magnitude * 1.4f - 0.9f);

    public static Sprite Sun => Make("sun", 128, (p, px) =>
    {
        float d = Circle(p, 0.36f);
        for (int k = 0; k < 8; k++)
        {
            float a = k * Mathf.PI / 4f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            d = Mathf.Min(d, Capsule(p, dir * 0.6f, dir * 0.84f, 0.075f));
        }
        return d;
    });

    public static Sprite Moon => Make("moon", 128, (p, px) => Mathf.Max(Circle(p, 0.74f), -Circle(p - new Vector2(0.38f, 0.30f), 0.62f)));

    public static Sprite Diamond => Make("diamond", 96, (p, px) =>
        Poly(p, new Vector2(0, 0.92f), new Vector2(0.62f, 0.05f), new Vector2(0, -0.92f), new Vector2(-0.62f, 0.05f)));

    public static Sprite DiamondOutline => Make("diamondOutline", 96, (p, px) =>
        Mathf.Abs(Poly(p, new Vector2(0, 0.86f), new Vector2(0.56f, 0.05f), new Vector2(0, -0.86f), new Vector2(-0.56f, 0.05f))) - 0.07f);

    public static Sprite Restart => Make("restart", 128, (p, px) =>
    {
        const float R = 0.56f, T = 0.12f, gap = 0.75f;
        float len = p.magnitude;
        float ring = Mathf.Abs(len - R) - T;
        float ang = Mathf.Atan2(p.y, p.x);            // gap centred on the right-hand side
        float w = (Mathf.Abs(ang) - gap * 0.5f) * len;   // negative inside the gap wedge
        float d = w < 0 ? Mathf.Max(ring, -w) : ring;
        // arrow head at the upper end of the gap, pointing clockwise (down)
        float ea = gap * 0.5f;
        var e = new Vector2(Mathf.Cos(ea), Mathf.Sin(ea)) * R;
        var radial = e.normalized;
        var tangent = new Vector2(radial.y, -radial.x);
        var tip = e + tangent * 0.34f;
        d = Mathf.Min(d, Poly(p, tip, e + radial * 0.3f - tangent * 0.02f, e - radial * 0.3f - tangent * 0.02f));
        return d;
    });

    /// <summary>Chunky rounded five-point star.</summary>
    public static Sprite Star => Make("star", 128, (p, px) => StarSdf(new Vector2(p.x, p.y + 0.06f), 0.92f, 5, 2.4f) - 0.06f);

    public static Sprite StarOutline => Make("starOutline", 128, (p, px) => Mathf.Abs(StarSdf(new Vector2(p.x, p.y + 0.06f), 0.86f, 5, 2.4f) - 0.04f) - 0.07f);

    /// <summary>Four-point twinkle.</summary>
    public static Sprite Sparkle => Make("sparkle", 96, (p, px) =>
        Mathf.Min(Poly(p, new Vector2(0, 0.95f), new Vector2(0.17f, 0), new Vector2(0, -0.95f), new Vector2(-0.17f, 0)),
                  Poly(p, new Vector2(0.95f, 0), new Vector2(0, 0.17f), new Vector2(-0.95f, 0), new Vector2(0, -0.17f))));

    public static Sprite Heart => Make("heart", 128, (p, px) =>
        Mathf.Min(Mathf.Min(Circle(p - new Vector2(-0.36f, 0.28f), 0.42f), Circle(p - new Vector2(0.36f, 0.28f), 0.42f)),
                  Poly(p, new Vector2(-0.76f, 0.12f), new Vector2(0.76f, 0.12f), new Vector2(0f, -0.84f))));

    public static Sprite HeartOutline => Make("heartOutline", 128, (p, px) =>
        Mathf.Abs(Mathf.Min(Mathf.Min(Circle(p - new Vector2(-0.33f, 0.26f), 0.36f), Circle(p - new Vector2(0.33f, 0.26f), 0.36f)),
                  Poly(p, new Vector2(-0.67f, 0.12f), new Vector2(0.67f, 0.12f), new Vector2(0f, -0.74f)))) - 0.07f);

    public static Sprite Pill => Make("pill", 64, (p, px) => Box(p, new Vector2(1 - px, 0.5f), 0.45f));

    static float StarSdf(Vector2 p, float r, int n, float m)
    {
        float an = Mathf.PI / n, en = Mathf.PI / m;
        var acs = new Vector2(Mathf.Cos(an), Mathf.Sin(an));
        var ecs = new Vector2(Mathf.Cos(en), Mathf.Sin(en));
        float a = Mathf.Atan2(p.x, p.y);
        float bn = a - 2 * an * Mathf.Floor(a / (2 * an)) - an;
        p = p.magnitude * new Vector2(Mathf.Cos(bn), Mathf.Abs(Mathf.Sin(bn)));
        p -= r * acs;
        p += ecs * Mathf.Clamp(-Vector2.Dot(p, ecs), 0, r * acs.y / ecs.y);
        return p.magnitude * Mathf.Sign(p.x);
    }

    public static Sprite Pause => Make("pause", 96, (p, px) =>
        Mathf.Min(Box(p - new Vector2(-0.3f, 0), new Vector2(0.16f, 0.62f), 0.08f), Box(p - new Vector2(0.3f, 0), new Vector2(0.16f, 0.62f), 0.08f)));

    public static Sprite Play => Make("play", 96, (p, px) =>
        Poly(p, new Vector2(-0.45f, 0.72f), new Vector2(0.72f, 0f), new Vector2(-0.45f, -0.72f)));

    public static Sprite Clock => Make("clock", 96, (p, px) =>
        Mathf.Min(Mathf.Abs(Circle(p, 0.72f)) - 0.1f,
            Mathf.Min(Capsule(p, Vector2.zero, new Vector2(0, 0.42f), 0.08f), Capsule(p, Vector2.zero, new Vector2(0.3f, -0.12f), 0.08f))));

    public static Sprite Cross => Make("cross", 96, (p, px) =>
        Mathf.Min(Capsule(p, new Vector2(-0.52f, -0.52f), new Vector2(0.52f, 0.52f), 0.14f),
                  Capsule(p, new Vector2(-0.52f, 0.52f), new Vector2(0.52f, -0.52f), 0.14f)));

    public static Sprite ShiftArrow => Make("shiftArrow", 96, (p, px) =>
        Mathf.Min(Poly(p, new Vector2(0, 0.88f), new Vector2(0.78f, 0.06f), new Vector2(-0.78f, 0.06f)),
                  Box(p - new Vector2(0, -0.38f), new Vector2(0.3f, 0.46f), 0.04f)));

    public static Sprite ChevronLeft => Make("chevL", 96, (p, px) =>
        Mathf.Min(Capsule(p, new Vector2(0.25f, 0.58f), new Vector2(-0.3f, 0f), 0.13f), Capsule(p, new Vector2(-0.3f, 0f), new Vector2(0.25f, -0.58f), 0.13f)));

    public static Sprite ChevronRight => Make("chevR", 96, (p, px) =>
        Mathf.Min(Capsule(p, new Vector2(-0.25f, 0.58f), new Vector2(0.3f, 0f), 0.13f), Capsule(p, new Vector2(0.3f, 0f), new Vector2(-0.25f, -0.58f), 0.13f)));

    public static Sprite Home => Make("home", 96, (p, px) =>
        Mathf.Min(Poly(p, new Vector2(0, 0.85f), new Vector2(0.85f, 0.05f), new Vector2(-0.85f, 0.05f)),
                  Box(p - new Vector2(0, -0.4f), new Vector2(0.56f, 0.46f), 0.06f)));

    public static Sprite Flag => Make("flag", 96, (p, px) =>
        Mathf.Min(Capsule(p, new Vector2(-0.55f, -0.85f), new Vector2(-0.55f, 0.8f), 0.09f),
                  Poly(p, new Vector2(-0.5f, 0.8f), new Vector2(0.7f, 0.45f), new Vector2(-0.5f, 0.1f))));

    public static Sprite Exit => Make("exit", 96, (p, px) =>
        Mathf.Min(Mathf.Max(Box(p - new Vector2(-0.25f, 0), new Vector2(0.5f, 0.8f), 0.08f), -Box(p - new Vector2(-0.25f, 0), new Vector2(0.32f, 0.62f), 0.02f)),
                  Mathf.Min(Capsule(p, new Vector2(0.05f, 0), new Vector2(0.75f, 0), 0.09f),
                            Poly(p, new Vector2(0.9f, 0), new Vector2(0.55f, 0.32f), new Vector2(0.55f, -0.32f)))));

    // ------------------------------------------------------------------ factory

    public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Stretch(string name, Transform parent)
    {
        var rt = Rect(name, parent, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    public static Image Img(Transform parent, string name, Sprite sprite, Color c, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, float cornerScale = 1f, bool raycast = false)
    {
        var rt = Rect(name, parent, anchor, pivot, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = c;
        img.raycastTarget = raycast;
        if (sprite != null && sprite.border != Vector4.zero)
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = cornerScale;
        }
        else img.preserveAspect = true;
        return img;
    }

    public static Image Fill(Transform parent, string name, Color c, bool raycast = false)
    {
        var rt = Stretch(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = raycast;
        return img;
    }

    public static Text Txt(Transform parent, string s, int size, Color c, TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box,
        FontStyle style = FontStyle.Bold, float shadow = 0.45f)
    {
        var rt = Rect("Text", parent, anchor, pivot, pos, box);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = c;
        t.alignment = align;
        t.text = s;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        if (shadow > 0)
        {
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, shadow);
            sh.effectDistance = new Vector2(2, -3);
        }
        return t;
    }

    /// <summary>Spreads letters apart for small-caps style labels.</summary>
    public static string Spaced(string s) => string.Join(" ", s.ToUpperInvariant().ToCharArray());

    public static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1;
        return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
    }
}
