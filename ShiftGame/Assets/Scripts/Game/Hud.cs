using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// All UI, built at runtime in a bright "candy" style: cream cards with coloured ribbons, 3D buttons, confetti,
/// sparkles, springy animations. HUD (level card, day/night switch, shard pips, timer, on-screen buttons, controls,
/// toasts, level banner, boss bar) and screens (title, pause, level complete with star rating, confirm dialog).
/// Works with mouse, keyboard and gamepad. Everything animates on unscaled time.
/// </summary>
public class Hud : MonoBehaviour
{
    public GameManager gm;

    Transform root;
    RectTransform fx;
    public Image flash, fade;

    // ---- HUD
    GameObject hudRoot;
    RectTransform levelCard, levelBody, levelNumBadge;
    Text levelNum, levelBadge, levelName, hint;
    Image levelNumBg;
    RectTransform switchRoot, knob;
    Image switchBody, switchLip, knobImg, knobLip, knobSun, knobMoon;
    Image[] clouds, nightStars;
    Text dayLabel, nightLabel;
    Phase lastPhase;
    float knobPunch;
    RectTransform shardPill;
    readonly List<Image> pips = new List<Image>();
    readonly List<float> pipPunch = new List<float>();
    int lastShardsGot = -1;
    Text shardLabel;
    RectTransform statsPill;
    Text timeText, deathText;
    float deathShake;
    RectTransform portalChip;
    CanvasGroup controls;
    Menu hudButtons;
    float hudTime, hintTime;
    bool portalOpen;

    // ---- banner
    RectTransform banner;
    Image bannerBody, bannerLip, bannerChipBg;
    Text bannerChip, bannerTitle;
    float bannerT = 99f;
    public static float BannerHold = 1.5f;   // tweakable (screenshots)
    bool bannerBoss;

    // ---- boss bar
    RectTransform bossBar;
    Text bossName;
    readonly List<Image> bossSegs = new List<Image>();
    int bossHp, bossMax;
    float bossPunch;

    // ---- toast
    RectTransform toast;
    Image toastBody, toastLip;
    Text toastText;
    CanvasGroup toastGroup;
    float toastT = 99f;
    Color toastColor;

    // ---- title
    GameObject titleRoot;
    readonly List<Text> logo = new List<Text>();
    readonly List<Shadow> logoShadows = new List<Shadow>();
    RectTransform orbitSun, orbitMoon;
    Text tagline;
    RectTransform bestChip;
    Text bestText;
    Menu titleMenu;
    MenuButton continueBtn, selectorBtn, newGameBtn, quitBtn;
    readonly List<Image> selectorStars = new List<Image>();
    float titleT, sparkleTimer;

    // ---- pause
    GameObject pauseRoot;
    RectTransform pauseCard;
    Menu pauseMenu;
    Text pauseSub;
    float pauseT;

    // ---- complete
    GameObject completeRoot;
    Menu completeMenu;
    RectTransform completeCard;
    Image completeRibbonBody, completeRibbonLip;
    Text completeHeader, completeSub;
    readonly Text[] statValues = new Text[3];
    readonly Text[] statLabels = new Text[3];
    readonly Image[] starImgs = new Image[3];
    readonly Text[] starNotes = new Text[3];
    readonly bool[] starPopped = new bool[3];
    float[] statTarget = new float[3];
    bool statIsTime;
    readonly bool[] starEarned = new bool[3];
    MenuButton nextBtn;
    float completeT;

    // ---- confirm
    GameObject confirmRoot;
    RectTransform confirmCard;
    Menu confirmMenu;
    Text confirmText;
    MenuButton confirmYes;
    Action onConfirmYes, onConfirmNo;
    Menu confirmReturn;
    float confirmT;

    float flashA;
    Color flashColor;

    static readonly Color DimColor = new Color(0.16f, 0.06f, 0.26f, 0.5f);
    static readonly Vector2 C = new Vector2(0.5f, 0.5f);

    // =================================================================== particles

    class Bit
    {
        public RectTransform rt; public Image img; public Vector2 vel; public float rot, rotVel, life, maxLife, gravity, size, seed;
        public bool twinkle;
    }
    readonly List<Bit> bits = new List<Bit>();
    readonly Stack<Bit> spare = new Stack<Bit>();

    Bit SpawnBit(Sprite s, Color c, Vector2 pos, Vector2 vel, float size, float life, float gravity, bool twinkle = false)
    {
        Bit b;
        if (spare.Count > 0) { b = spare.Pop(); b.rt.gameObject.SetActive(true); }
        else
        {
            var img = UiKit.Img(fx, "Bit", s, c, C, C, Vector2.zero, Vector2.one * size);
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            b = new Bit { rt = img.rectTransform, img = img };
        }
        b.img.sprite = s;
        b.img.color = c;
        b.rt.anchoredPosition = pos;
        b.rt.sizeDelta = s == UiKit.Rounded ? new Vector2(size, size * 0.55f) : Vector2.one * size;
        b.vel = vel; b.size = size; b.life = b.maxLife = life; b.gravity = gravity; b.twinkle = twinkle;
        b.rot = UnityEngine.Random.Range(0f, 360f);
        b.rotVel = twinkle ? UnityEngine.Random.Range(-60f, 60f) : UnityEngine.Random.Range(-540f, 540f);
        b.seed = UnityEngine.Random.value * 10f;
        bits.Add(b);
        return b;
    }

    /// <summary>Radial burst of sparkles/stars/confetti at a canvas position.</summary>
    void Burst(Vector2 pos, int n, float speed, float size, Color[] colors, Sprite[] shapes = null, float gravity = 900f)
    {
        shapes = shapes ?? new[] { UiKit.Sparkle, UiKit.Star, UiKit.Diamond };
        for (int i = 0; i < n; i++)
        {
            float a = UnityEngine.Random.value * Mathf.PI * 2;
            var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * UnityEngine.Random.Range(0.4f, 1f) + Vector2.up * speed * 0.35f;
            SpawnBit(shapes[UnityEngine.Random.Range(0, shapes.Length)], colors[UnityEngine.Random.Range(0, colors.Length)], pos, v,
                size * UnityEngine.Random.Range(0.6f, 1.25f), UnityEngine.Random.Range(0.6f, 1.1f), gravity);
        }
    }

    /// <summary>Confetti shower falling from the top of the screen.</summary>
    void ConfettiRain(int n)
    {
        var shapes = new[] { UiKit.Rounded, UiKit.Rounded, UiKit.Star, UiKit.Diamond, UiKit.Dot };
        for (int i = 0; i < n; i++)
        {
            var pos = new Vector2(UnityEngine.Random.Range(-980f, 980f), 600f + UnityEngine.Random.Range(0f, 300f));
            var v = new Vector2(UnityEngine.Random.Range(-120f, 120f), UnityEngine.Random.Range(-250f, -60f));
            SpawnBit(shapes[UnityEngine.Random.Range(0, shapes.Length)], UiKit.Confetti[UnityEngine.Random.Range(0, UiKit.Confetti.Length - 1)],
                pos, v, UnityEngine.Random.Range(16f, 30f), UnityEngine.Random.Range(2.4f, 3.6f), 260f);
        }
    }

    void UpdateBits(float dt)
    {
        for (int i = bits.Count - 1; i >= 0; i--)
        {
            var b = bits[i];
            b.life -= dt;
            if (b.life <= 0)
            {
                b.rt.gameObject.SetActive(false);
                spare.Push(b);
                bits.RemoveAt(i);
                continue;
            }
            b.vel.y -= b.gravity * dt;
            b.vel *= 1f - Mathf.Min(1f, 1.2f * dt);
            var p = b.rt.anchoredPosition + b.vel * dt;
            if (b.gravity > 0 && b.gravity < 400) p.x += Mathf.Sin(Time.unscaledTime * 5f + b.seed) * 60f * dt;  // flutter
            b.rt.anchoredPosition = p;
            b.rot += b.rotVel * dt;
            b.rt.localRotation = Quaternion.Euler(0, 0, b.rot);
            float t = 1f - b.life / b.maxLife;
            float s = b.twinkle ? Mathf.Sin(t * Mathf.PI) : (t < 0.1f ? t / 0.1f : 1f);
            b.rt.localScale = new Vector3(s, s, 1);
            var c = b.img.color;
            c.a = b.twinkle ? 0.9f : Mathf.Clamp01(b.life / 0.35f);
            b.img.color = c;
        }
    }

    Vector2 FxPos(RectTransform rt)
    {
        var w = rt.TransformPoint(rt.rect.center);
        return fx.InverseTransformPoint(w);
    }

    // =================================================================== build helpers

    GameObject Layer(string name) => UiKit.Stretch(name, root).gameObject;

    /// <summary>Cream card with a chunky darker "lip" under it. Returns the card body.</summary>
    RectTransform CandyCard(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, float corner = 1.0f)
    {
        var holder = UiKit.Rect(name, parent, anchor, pivot, pos, size);
        UiKit.Img(holder, "Shadow", UiKit.Rounded, new Color(0.1f, 0.03f, 0.2f, 0.18f), C, C, new Vector2(0, -16), size + new Vector2(10, 6), corner);
        UiKit.Img(holder, "Lip", UiKit.Rounded, UiKit.CreamDeep * 0.92f, C, C, new Vector2(0, -8), size, corner);
        var body = UiKit.Img(holder, "Body", UiKit.Rounded, UiKit.Cream, C, C, Vector2.zero, size, corner);
        UiKit.Img(body.transform, "Inner", UiKit.RoundedOutline, new Color(1, 1, 1, 0.9f), C, C, Vector2.zero, size - new Vector2(14, 14), corner * 1.3f);
        return holder;
    }

    /// <summary>Coloured ribbon with a lip and outlined white text.</summary>
    (Image body, Image lip, Text text) Ribbon(Transform parent, string s, Color col, Vector2 pos, Vector2 size, int fontSize, float tilt = -2f)
    {
        var holder = UiKit.Rect("Ribbon", parent, C, C, pos, size);
        holder.localRotation = Quaternion.Euler(0, 0, tilt);
        var lip = UiKit.Img(holder, "Lip", UiKit.Rounded, UiKit.Darker(col), C, C, new Vector2(0, -8), size, 1.3f);
        var body = UiKit.Img(holder, "Body", UiKit.Rounded, col, C, C, Vector2.zero, size, 1.3f);
        UiKit.Img(body.transform, "Gloss", UiKit.Rounded, new Color(1, 1, 1, 0.25f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(size.x - 30, size.y * 0.34f), 2.5f);
        var text = OutlinedText(body.transform, s, fontSize, Color.white, UiKit.Darker(col, 0.5f), Vector2.zero, new Vector2(size.x - 40, size.y));
        return (body, lip, text);
    }

    Text OutlinedText(Transform parent, string s, int size, Color c, Color outline, Vector2 pos, Vector2 box, TextAnchor align = TextAnchor.MiddleCenter, float thick = 2.5f)
    {
        var t = UiKit.Txt(parent, s, size, c, align, C, C, pos, box, FontStyle.Bold, 0);
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = outline;
        o.effectDistance = new Vector2(thick, -thick);
        var sh = t.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0, 0, 0, 0.2f);
        sh.effectDistance = new Vector2(0, -4);
        return t;
    }

    /// <summary>Small candy pill (lip + body) used for HUD chips.</summary>
    RectTransform Chip(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color col, out Image body)
    {
        var holder = UiKit.Rect(name, parent, anchor, pivot, pos, size);
        UiKit.Img(holder, "Lip", UiKit.Rounded, UiKit.Darker(col, col == UiKit.Cream ? 0.85f : 0.7f), C, C, new Vector2(0, -6), size, 1.1f);
        body = UiKit.Img(holder, "Body", UiKit.Rounded, col, C, C, Vector2.zero, size, 1.1f);
        return holder;
    }

    static void Pin(Transform tr, Vector2 anchor, Vector2 pos)
    {
        var rt = (RectTransform)tr;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
    }

    // =================================================================== build

    public void Build()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        root = canvasGo.transform;

        BuildHud();
        BuildBanner();
        BuildToast();
        BuildTitle();
        BuildPause();
        BuildComplete();
        BuildConfirm();

        fx = UiKit.Stretch("FX", root);
        flash = UiKit.Fill(root, "Flash", new Color(1, 1, 1, 0));
        fade = UiKit.Fill(root, "Fade", new Color(0.08f, 0.04f, 0.14f, 1));

        hudRoot.SetActive(false);
        titleRoot.SetActive(false);
        pauseRoot.SetActive(false);
        completeRoot.SetActive(false);
        confirmRoot.SetActive(false);
        banner.gameObject.SetActive(false);
    }

    void BuildHud()
    {
        hudRoot = Layer("HUD");
        var t = hudRoot.transform;
        var TL = new Vector2(0, 1);
        var TR = new Vector2(1, 1);

        // ---- level card (top-left)
        levelCard = CandyCard(t, "LevelCard", TL, TL, new Vector2(30, -26), new Vector2(560, 108), 1.1f);
        levelBody = levelCard;
        var numBg = Chip(levelCard, "Num", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 2), new Vector2(84, 80), UiKit.Sunny, out levelNumBg);
        levelNumBadge = numBg;
        levelNum = OutlinedText(numBg, "1", 44, Color.white, UiKit.Darker(UiKit.Sunny, 0.5f), Vector2.zero, new Vector2(84, 70));
        levelBadge = UiKit.Txt(levelCard, "", 20, UiKit.Grape, TextAnchor.UpperLeft, TL, TL, new Vector2(114, -16), new Vector2(560, 28), FontStyle.Bold, 0);
        levelName = UiKit.Txt(levelCard, "", 40, UiKit.InkSoft, TextAnchor.UpperLeft, TL, TL, new Vector2(112, -42), new Vector2(900, 52), FontStyle.Bold, 0);
        levelName.horizontalOverflow = HorizontalWrapMode.Overflow;
        hint = OutlinedText(t, "", 25, Color.white, new Color(0.15f, 0.08f, 0.25f, 0.85f), Vector2.zero, new Vector2(860, 90), TextAnchor.UpperLeft, 2f);
        Pin(hint.transform, TL, Vector2.zero);
        hint.rectTransform.pivot = TL;
        hint.rectTransform.anchoredPosition = new Vector2(36, -150);

        // ---- top-right: [day/night switch] [RESTART] [MENU]
        hudButtons = UiKit.Rect("HudButtons", t, TR, TR, Vector2.zero, Vector2.zero).gameObject.AddComponent<Menu>();
        hudButtons.pointerOnly = true;
        hudButtons.selected = -1;
        var menuBtn = hudButtons.AddButton(t, "MENU", Vector2.zero, new Vector2(160, 64), UiKit.Pause, () => gm.UiPause(), false, UiKit.Grape);
        menuBtn.basePos = new Vector2(-30 - 80, -26 - 32); Pin(menuBtn.transform, TR, menuBtn.basePos);
        var restartBtn = hudButtons.AddButton(t, "RESTART", Vector2.zero, new Vector2(204, 64), UiKit.Restart, () => gm.UiAskRestartFromOne(), false, UiKit.Berry);
        restartBtn.basePos = new Vector2(-30 - 160 - 14 - 102, -26 - 32); Pin(restartBtn.transform, TR, restartBtn.basePos);

        switchRoot = UiKit.Rect("PhaseSwitch", t, TR, TR, new Vector2(-30 - 160 - 14 - 204 - 16, -26), new Vector2(240, 64));
        switchLip = UiKit.Img(switchRoot, "Lip", UiKit.Rounded, Color.white, C, C, new Vector2(0, -6), new Vector2(240, 64), 1.1f);
        switchBody = UiKit.Img(switchRoot, "Body", UiKit.Rounded, Color.white, C, C, Vector2.zero, new Vector2(240, 64), 1.1f);
        clouds = new[]
        {
            UiKit.Img(switchRoot, "Cloud", UiKit.Pill, Color.white, C, C, new Vector2(-10, 22), new Vector2(34, 12)),
            UiKit.Img(switchRoot, "Cloud", UiKit.Pill, Color.white, C, C, new Vector2(98, -22), new Vector2(26, 10)),
        };
        nightStars = new[]
        {
            UiKit.Img(switchRoot, "Star", UiKit.Sparkle, Color.white, C, C, new Vector2(-100, 21), new Vector2(12, 12)),
            UiKit.Img(switchRoot, "Star", UiKit.Sparkle, Color.white, C, C, new Vector2(10, 22), new Vector2(10, 10)),
            UiKit.Img(switchRoot, "Star", UiKit.Sparkle, Color.white, C, C, new Vector2(-96, -21), new Vector2(9, 9)),
        };
        dayLabel = OutlinedText(switchRoot, "DAY", 26, Color.white, new Color(0.15f, 0.35f, 0.65f), new Vector2(40, 0), new Vector2(150, 40), TextAnchor.MiddleCenter, 2f);
        nightLabel = OutlinedText(switchRoot, "NIGHT", 26, Color.white, new Color(0.08f, 0.06f, 0.25f), new Vector2(-40, 0), new Vector2(150, 40), TextAnchor.MiddleCenter, 2f);
        knobLip = UiKit.Img(switchRoot, "KnobLip", UiKit.Dot, Color.white, C, C, Vector2.zero, new Vector2(54, 54));
        knobImg = UiKit.Img(switchRoot, "Knob", UiKit.Dot, Color.white, C, C, Vector2.zero, new Vector2(54, 54));
        knob = knobImg.rectTransform;
        knobSun = UiKit.Img(knob, "Sun", UiKit.Sun, Color.white, C, C, Vector2.zero, new Vector2(40, 40));
        knobMoon = UiKit.Img(knob, "Moon", UiKit.Moon, Color.white, C, C, Vector2.zero, new Vector2(34, 34));

        // ---- shards pill
        shardPill = Chip(t, "Shards", TR, TR, new Vector2(-30, -108), new Vector2(300, 54), UiKit.Cream, out _);
        shardLabel = UiKit.Txt(shardPill, "SHARDS", 20, UiKit.Grape, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(140, 40), FontStyle.Bold, 0);
        for (int i = 0; i < 12; i++)
        {
            pips.Add(UiKit.Img(shardPill, "Pip", UiKit.Diamond, Color.white, new Vector2(1, 0.5f), C, Vector2.zero, new Vector2(26, 34)));
            pipPunch.Add(0);
        }

        // ---- stats pill
        statsPill = Chip(t, "Stats", TR, TR, new Vector2(-30, -176), new Vector2(300, 50), UiKit.Cream, out _);
        UiKit.Img(statsPill, "Clock", UiKit.Clock, UiKit.Sky, new Vector2(0, 0.5f), C, new Vector2(32, 0), new Vector2(28, 28));
        timeText = UiKit.Txt(statsPill, "00:00.0", 26, UiKit.InkSoft, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(54, 0), new Vector2(130, 40), FontStyle.Bold, 0);
        UiKit.Img(statsPill, "Cross", UiKit.Cross, UiKit.Berry, new Vector2(0, 0.5f), C, new Vector2(206, 0), new Vector2(22, 22));
        deathText = UiKit.Txt(statsPill, "0", 26, UiKit.InkSoft, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(226, 0), new Vector2(70, 40), FontStyle.Bold, 0);

        // ---- portal chip
        portalChip = Chip(t, "PortalChip", TR, TR, new Vector2(-30, -238), new Vector2(250, 50), UiKit.Mint, out _);
        OutlinedText(portalChip, "PORTAL OPEN!", 24, Color.white, UiKit.Darker(UiKit.Mint, 0.45f), Vector2.zero, new Vector2(250, 46));

        // ---- boss bar (top centre)
        bossBar = CandyCard(t, "BossBar", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(600, 84), 1.1f);
        bossName = UiKit.Txt(bossBar, "", 22, UiKit.Grape, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(540, 32), FontStyle.Bold, 0);
        bossBar.gameObject.SetActive(false);

        // ---- controls bar (bottom)
        var bar = UiKit.Rect("Controls", t, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(1600, 54));
        controls = bar.gameObject.AddComponent<CanvasGroup>();
        controls.blocksRaycasts = false;
        BuildKeyRow(bar, new[] { ("WASD", "MOVE"), ("SPACE", "JUMP"), ("SHIFT", "FLIP WORLD"), ("R", "RETRY"), ("ESC", "MENU") });
    }

    void BuildKeyRow(RectTransform bar, (string key, string label)[] keys)
    {
        var parts = new List<(RectTransform rt, float w)>();
        float total = 0, gap = 34;
        foreach (var (key, label) in keys)
        {
            var group = UiKit.Rect("Key " + key, bar, C, new Vector2(0, 0.5f), Vector2.zero, new Vector2(10, 54));
            var probe = UiKit.Txt(group, key, 22, UiKit.InkSoft, TextAnchor.MiddleCenter, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(10, 40), FontStyle.Bold, 0);
            float capW = Mathf.Max(48, probe.preferredWidth + 28);
            var cap = Chip(group, "Cap", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 3), new Vector2(capW, 40), UiKit.Cream, out _);
            cap.SetAsFirstSibling();
            probe.rectTransform.sizeDelta = new Vector2(capW, 40);
            probe.rectTransform.anchoredPosition = new Vector2(0, 3);
            var lab = OutlinedText(group, label, 22, Color.white, new Color(0.15f, 0.08f, 0.25f, 0.85f), Vector2.zero, new Vector2(400, 40), TextAnchor.MiddleLeft, 2f);
            var lrt = lab.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0, 0.5f);
            lrt.pivot = new Vector2(0, 0.5f);
            lrt.anchoredPosition = new Vector2(capW + 12, 0);
            float w = capW + 12 + lab.preferredWidth;
            lrt.sizeDelta = new Vector2(lab.preferredWidth + 6, 40);
            parts.Add((group, w));
            total += w;
        }
        total += gap * (parts.Count - 1);
        float x = -total / 2;
        foreach (var (rt, w) in parts) { rt.anchoredPosition = new Vector2(x, 0); x += w + gap; }
    }

    void BuildBanner()
    {
        banner = UiKit.Rect("Banner", root, C, C, new Vector2(0, 120), new Vector2(1000, 220));
        var cg = banner.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        var chip = Chip(banner, "Chip", C, C, new Vector2(0, 74), new Vector2(260, 54), UiKit.Cream, out bannerChipBg);
        bannerChip = UiKit.Txt(chip, "", 26, UiKit.Grape, TextAnchor.MiddleCenter, C, C, Vector2.zero, new Vector2(260, 50), FontStyle.Bold, 0);
        bannerLip = UiKit.Img(banner, "Lip", UiKit.Rounded, Color.white, C, C, new Vector2(0, -18), new Vector2(940, 130), 0.9f);
        bannerBody = UiKit.Img(banner, "Body", UiKit.Rounded, Color.white, C, C, new Vector2(0, -8), new Vector2(940, 130), 0.9f);
        UiKit.Img(bannerBody.transform, "Gloss", UiKit.Rounded, new Color(1, 1, 1, 0.25f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(900, 42), 2.5f);
        chip.SetAsLastSibling();
        bannerTitle = OutlinedText(bannerBody.transform, "", 64, Color.white, Color.black, Vector2.zero, new Vector2(920, 110), TextAnchor.MiddleCenter, 3.5f);
    }

    void BuildToast()
    {
        toast = UiKit.Rect("Toast", root, new Vector2(0.5f, 1), C, new Vector2(0, -150), new Vector2(420, 66));
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.blocksRaycasts = false;
        toastLip = UiKit.Img(toast, "Lip", UiKit.Rounded, Color.white, C, C, new Vector2(0, -6), new Vector2(420, 66), 1.1f);
        toastBody = UiKit.Img(toast, "Body", UiKit.Rounded, Color.white, C, C, Vector2.zero, new Vector2(420, 66), 1.1f);
        toastText = OutlinedText(toast, "", 30, Color.white, Color.black, Vector2.zero, new Vector2(900, 60), TextAnchor.MiddleCenter, 2.5f);
        toastGroup.alpha = 0;
    }

    void BuildTitle()
    {
        titleRoot = Layer("Title");
        var t = titleRoot.transform;

        orbitSun = UiKit.Img(t, "OrbitSun", UiKit.Sun, UiKit.Sunny, C, C, Vector2.zero, new Vector2(96, 96)).rectTransform;
        orbitMoon = UiKit.Img(t, "OrbitMoon", UiKit.Moon, new Color(0.85f, 0.9f, 1f), C, C, Vector2.zero, new Vector2(80, 80)).rectTransform;

        string word = "SHIFT";
        for (int i = 0; i < word.Length; i++)
        {
            var letter = UiKit.Txt(t, word[i].ToString(), 220, Color.white, TextAnchor.MiddleCenter, C, C, new Vector2((i - 2) * 162, 300), new Vector2(230, 270), FontStyle.Bold, 0);
            var sh = letter.gameObject.AddComponent<Shadow>();
            sh.effectDistance = new Vector2(0, -16);
            var o = letter.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.16f, 0.08f, 0.26f, 1f);
            o.effectDistance = new Vector2(6, -6);
            logoShadows.Add(sh);
            logo.Add(letter);
        }
        tagline = OutlinedText(t, "Day and night are one key apart.", 38, Color.white, new Color(0.18f, 0.08f, 0.3f), new Vector2(0, 150), new Vector2(1400, 56), TextAnchor.MiddleCenter, 3f);

        titleMenu = UiKit.Rect("TitleMenu", t, C, C, Vector2.zero, Vector2.zero).gameObject.AddComponent<Menu>();
        var size = new Vector2(620, 88);
        continueBtn = titleMenu.AddButton(t, "PLAY", new Vector2(0, 30), size, UiKit.Play, () => gm.UiContinue(), true, UiKit.Sunny);
        selectorBtn = titleMenu.AddSelector(t, new Vector2(0, -78), size, () => { gm.UiSelectLevel(-1); RefreshTitle(); }, () => { gm.UiSelectLevel(1); RefreshTitle(); }, () => gm.UiPlaySelected(), UiKit.Sky);
        for (int i = 0; i < 3; i++)
            selectorStars.Add(UiKit.Img(selectorBtn.face, "Star", UiKit.Star, Color.white, C, C, new Vector2(150 + i * 30, 14), new Vector2(28, 28)));
        newGameBtn = titleMenu.AddButton(t, "NEW GAME  -  FROM LEVEL 1", new Vector2(0, -186), size, UiKit.Restart, () => gm.UiNewGame(), false, UiKit.Berry);
        quitBtn = titleMenu.AddButton(t, "QUIT", new Vector2(0, -294), size, UiKit.Exit, () => gm.UiQuit(), false, UiKit.Slate);
#if UNITY_WEBGL
        quitBtn.gameObject.SetActive(false);
#endif

        bestChip = Chip(t, "Best", new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -26), new Vector2(300, 56), UiKit.Sunny, out _);
        UiKit.Img(bestChip, "Star", UiKit.Star, Color.white, new Vector2(0, 0.5f), C, new Vector2(34, 2), new Vector2(34, 34));
        bestText = OutlinedText(bestChip, "", 26, Color.white, UiKit.Darker(UiKit.Sunny, 0.5f), new Vector2(18, 0), new Vector2(240, 50));
        var credit = OutlinedText(t, "GITHUB GAME OFF 2026  -  THEME: SHIFT", 20, Color.white, new Color(0.15f, 0.08f, 0.25f, 0.8f), Vector2.zero, new Vector2(700, 40), TextAnchor.UpperRight, 2f);
        credit.rectTransform.anchorMin = credit.rectTransform.anchorMax = credit.rectTransform.pivot = new Vector2(1, 1);
        credit.rectTransform.anchoredPosition = new Vector2(-34, -26);
        var keys = UiKit.Rect("TitleKeys", t, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(1200, 54));
        BuildKeyRow(keys, new[] { ("SHIFT", "FLIP THE WORLD"), ("W / S", "CHOOSE"), ("A / D", "CHANGE LEVEL"), ("ENTER", "SELECT") });
    }

    void BuildPause()
    {
        pauseRoot = Layer("Pause");
        var t = pauseRoot.transform;
        UiKit.Fill(t, "Dim", DimColor, true);
        pauseCard = CandyCard(t, "Card", C, C, new Vector2(0, -10), new Vector2(680, 640), 0.8f);
        Ribbon(pauseCard, "PAUSED", UiKit.Grape, new Vector2(0, 300), new Vector2(460, 104), 58);
        pauseSub = UiKit.Txt(pauseCard, "", 26, UiKit.Grape, TextAnchor.MiddleCenter, C, C, new Vector2(0, 208), new Vector2(640, 40), FontStyle.Bold, 0);

        pauseMenu = UiKit.Rect("PauseMenu", t, C, C, Vector2.zero, Vector2.zero).gameObject.AddComponent<Menu>();
        var size = new Vector2(540, 80);
        pauseMenu.AddButton(pauseCard, "RESUME", new Vector2(0, 112), size, UiKit.Play, () => gm.UiResume(), true, UiKit.Mint);
        pauseMenu.AddButton(pauseCard, "RESTART LEVEL", new Vector2(0, 14), size, UiKit.Restart, () => gm.UiRestartLevel(), false, UiKit.Sky);
        pauseMenu.AddButton(pauseCard, "RESTART FROM LEVEL 1", new Vector2(0, -84), size, UiKit.Flag, () => gm.UiAskRestartFromOne(), false, UiKit.Berry);
        pauseMenu.AddButton(pauseCard, "MAIN MENU", new Vector2(0, -182), size, UiKit.Home, () => gm.UiMainMenu(), false, UiKit.Slate);
        pauseMenu.onBack = () => gm.UiResume();
    }

    void BuildComplete()
    {
        completeRoot = Layer("Complete");
        var t = completeRoot.transform;
        UiKit.Fill(t, "Dim", new Color(DimColor.r, DimColor.g, DimColor.b, 0.38f), true);
        completeCard = CandyCard(t, "Card", C, C, new Vector2(0, -24), new Vector2(760, 820), 0.8f);
        var rib = Ribbon(completeCard, "", UiKit.Sunny, new Vector2(0, 392), new Vector2(720, 112), 50);
        completeRibbonBody = rib.body; completeRibbonLip = rib.lip; completeHeader = rib.text;
        completeSub = UiKit.Txt(completeCard, "", 26, UiKit.Grape, TextAnchor.MiddleCenter, C, C, new Vector2(0, 300), new Vector2(700, 40), FontStyle.Bold, 0);

        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 150f;
            float y = i == 1 ? 196f : 178f;
            float sz = i == 1 ? 124f : 104f;
            UiKit.Img(completeCard, "StarSlot", UiKit.Star, new Color(0.2f, 0.12f, 0.3f, 0.12f), C, C, new Vector2(x, y - 4), new Vector2(sz, sz));
            starImgs[i] = UiKit.Img(completeCard, "Star", UiKit.Star, UiKit.Sunny, C, C, new Vector2(x, y), new Vector2(sz, sz));
            starNotes[i] = UiKit.Txt(completeCard, "", 17, UiKit.InkSoft, TextAnchor.MiddleCenter, C, C, new Vector2(x, y - sz * 0.5f - 18), new Vector2(170, 26), FontStyle.Bold, 0);
        }

        var icons = new[] { UiKit.Clock, UiKit.ShiftArrow, UiKit.Cross };
        var iconCols = new[] { UiKit.Sky, UiKit.Sunny, UiKit.Berry };
        var names = new[] { "TIME", "SHIFTS", "DEATHS" };
        for (int i = 0; i < 3; i++)
        {
            float y = 46 - i * 62;
            var row = UiKit.Img(completeCard, "Row", UiKit.Rounded, UiKit.CreamDeep, C, C, new Vector2(0, y), new Vector2(580, 52), 1.6f);
            UiKit.Img(row.transform, "Icon", icons[i], iconCols[i], new Vector2(0, 0.5f), C, new Vector2(36, 0), new Vector2(30, 30));
            statLabels[i] = UiKit.Txt(row.transform, names[i], 22, UiKit.Grape, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(66, 0), new Vector2(260, 40), FontStyle.Bold, 0);
            statValues[i] = UiKit.Txt(row.transform, "", 30, UiKit.InkSoft, TextAnchor.MiddleRight, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-26, 0), new Vector2(300, 44), FontStyle.Bold, 0);
        }

        completeMenu = UiKit.Rect("CompleteMenu", t, C, C, Vector2.zero, Vector2.zero).gameObject.AddComponent<Menu>();
        var size = new Vector2(540, 80);
        nextBtn = completeMenu.AddButton(completeCard, "NEXT LEVEL", new Vector2(0, -170), size, UiKit.Play, () => gm.UiNext(), true, UiKit.Mint);
        completeMenu.AddButton(completeCard, "PLAY AGAIN", new Vector2(0, -264), size, UiKit.Restart, () => gm.UiReplayCompleted(), false, UiKit.Sky);
        completeMenu.AddButton(completeCard, "MAIN MENU", new Vector2(0, -358), size, UiKit.Home, () => gm.UiMainMenu(), false, UiKit.Slate);
    }

    void BuildConfirm()
    {
        confirmRoot = Layer("Confirm");
        var t = confirmRoot.transform;
        UiKit.Fill(t, "Dim", new Color(DimColor.r, DimColor.g, DimColor.b, 0.62f), true);
        confirmCard = CandyCard(t, "Card", C, C, Vector2.zero, new Vector2(740, 450), 0.9f);
        var badge = Chip(confirmCard, "Badge", C, C, new Vector2(0, 200), new Vector2(110, 110), UiKit.Berry, out _);
        UiKit.Img(badge, "Icon", UiKit.Flag, Color.white, C, C, new Vector2(4, 2), new Vector2(56, 56));
        confirmText = UiKit.Txt(confirmCard, "", 32, UiKit.InkSoft, TextAnchor.MiddleCenter, C, C, new Vector2(0, 70), new Vector2(660, 120), FontStyle.Bold, 0);
        confirmMenu = UiKit.Rect("ConfirmMenu", t, C, C, Vector2.zero, Vector2.zero).gameObject.AddComponent<Menu>();
        var size = new Vector2(500, 78);
        confirmYes = confirmMenu.AddButton(confirmCard, "YES", new Vector2(0, -56), size, UiKit.Restart, () => CloseConfirm(true), true, UiKit.Berry);
        confirmMenu.AddButton(confirmCard, "CANCEL", new Vector2(0, -150), size, UiKit.Cross, () => CloseConfirm(false), false, UiKit.Slate);
        confirmMenu.onBack = () => CloseConfirm(false);
    }

    // =================================================================== public API

    public void HideAll()
    {
        hudRoot.SetActive(false);
        titleRoot.SetActive(false);
        pauseRoot.SetActive(false);
        completeRoot.SetActive(false);
        confirmRoot.SetActive(false);
        banner.gameObject.SetActive(false);
        toastT = 99f;
    }

    public void ShowTitle()
    {
        HideAll();
        titleRoot.SetActive(true);
        titleT = 0;
        RefreshTitle();
        titleMenu.Open(0);
        if (AudioFx.I != null) AudioFx.I.Play(AudioFx.I.whoosh, 0.6f, 0f);
    }

    public void RefreshTitle()
    {
        int current = gm.CurrentLevel, unlocked = gm.Unlocked, sel = gm.SelectedLevel;
        bool hasProgress = current > 0 || unlocked > 1;
        continueBtn.label.text = hasProgress ? $"CONTINUE  -  LEVEL {current + 1}" : "PLAY";
        newGameBtn.gameObject.SetActive(hasProgress);
        selectorBtn.gameObject.SetActive(unlocked > 1);
        var def = Levels.Get(sel);
        selectorBtn.label.text = $"LEVEL {sel + 1}";
        selectorBtn.sub.text = def.boss != null ? $"{def.name.ToUpper()}  -  BOSS" : def.generated ? $"{def.name.ToUpper()}  -  ENDLESS" : def.name.ToUpper();
        selectorBtn.onLeft = sel > 0 ? (Action)(() => { gm.UiSelectLevel(-1); RefreshTitle(); }) : null;
        selectorBtn.onRight = sel < unlocked - 1 ? (Action)(() => { gm.UiSelectLevel(1); RefreshTitle(); }) : null;
        int stars = gm.BestStars(sel);
        for (int i = 0; i < 3; i++)
        {
            selectorStars[i].sprite = i < stars ? UiKit.Star : UiKit.StarOutline;
            selectorStars[i].color = i < stars ? new Color(1f, 0.9f, 0.35f) : new Color(1, 1, 1, 0.55f);
        }
        bestChip.gameObject.SetActive(unlocked > 1);
        bestText.text = $"BEST: LEVEL {unlocked}";
    }

    public void ShowHud(int index, LevelDef def)
    {
        HideAll();
        hudRoot.SetActive(true);
        bossBar.gameObject.SetActive(false);
        shardPill.gameObject.SetActive(def.boss == null);
        levelNum.text = (index + 1).ToString();
        levelNum.fontSize = index + 1 >= 100 ? 32 : 44;
        levelBadge.text = def.boss != null
            ? UiKit.Spaced("BOSS FIGHT")
            : def.generated
            ? UiKit.Spaced("ENDLESS") + "   -   " + UiKit.Spaced(Themes.Get(def.theme).name)
            : UiKit.Spaced($"LEVEL {index + 1} OF {Levels.HandmadeCount}");
        levelName.text = def.name.ToUpper();
        var size = new Vector2(Mathf.Max(440, levelName.preferredWidth + 140, levelBadge.preferredWidth + 140), 108);
        foreach (RectTransform child in levelCard)
            if (child.name == "Shadow" || child.name == "Lip" || child.name == "Body")
                child.sizeDelta = child.name == "Shadow" ? size + new Vector2(10, 6) : size;
        levelCard.Find("Body/Inner").GetComponent<RectTransform>().sizeDelta = size - new Vector2(14, 14);
        levelCard.sizeDelta = size;
        hint.text = def.hint;
        hudTime = 0;
        hintTime = 0;
        portalOpen = false;
        lastShardsGot = -1;
        lastPhase = PhaseState.Current;
        Banner(index, def);
    }

    void Banner(int index, LevelDef def)
    {
        bannerBoss = def.boss != null;
        var col = bannerBoss ? UiKit.Berry : UiKit.Accent;
        if (!bannerBoss && def.generated) col = UiKit.Confetti[index % 6];
        bannerBody.color = col;
        bannerLip.color = UiKit.Darker(col);
        bannerTitle.text = bannerBoss ? "BOSS FIGHT!" : def.name.ToUpper();
        bannerTitle.GetComponent<Outline>().effectColor = UiKit.Darker(col, 0.45f);
        bannerChip.text = bannerBoss ? def.name.ToUpper() : $"LEVEL {index + 1}";
        banner.gameObject.SetActive(true);
        banner.anchoredPosition = new Vector2(-1500f, 120f);
        bannerT = -0.45f;   // wait for the screen fade to clear
    }

    public void SetShards(int got, int total)
    {
        int n = Mathf.Min(total, pips.Count);
        float pipW = 32;
        float width = 140 + Mathf.Max(n, 1) * pipW + 6;
        shardLabel.text = total == 0 ? "NO SHARDS" : "SHARDS";
        var w = new Vector2(total == 0 ? 230 : width, 54);
        shardPill.sizeDelta = w;
        foreach (RectTransform child in shardPill) if (child.name == "Lip" || child.name == "Body") child.sizeDelta = w;
        for (int i = 0; i < pips.Count; i++)
        {
            var p = pips[i];
            bool on = i < n;
            p.gameObject.SetActive(on);
            if (!on) continue;
            int fromRight = n - 1 - i;
            p.rectTransform.anchoredPosition = new Vector2(-24 - fromRight * pipW, 0);
            bool filled = i < got;
            p.sprite = filled ? UiKit.Diamond : UiKit.DiamondOutline;
            p.color = filled ? UiKit.Sunny : new Color(0.2f, 0.12f, 0.3f, 0.3f);
            if (filled && lastShardsGot >= 0 && i >= lastShardsGot && i < got)
            {
                pipPunch[i] = 1f;
                Burst(FxPos(p.rectTransform), 10, 420f, 20f, new[] { UiKit.Sunny, Color.white, UiKit.Peach });
            }
        }
        bool open = got >= total;
        if (open && !portalOpen && hudTime > 0.5f) Toast(total == 0 ? "PORTAL OPEN!" : "ALL SHARDS!  PORTAL OPEN!", UiKit.Mint);
        portalOpen = open;
        lastShardsGot = got;
    }

    public void ShowBoss(string name, int maxHp, int tier)
    {
        bossBar.gameObject.SetActive(true);
        bossName.text = UiKit.Spaced(name) + "     " + UiKit.Spaced($"TIER {tier}");
        foreach (var seg in bossSegs) Destroy(seg.transform.parent.gameObject);
        bossSegs.Clear();
        bossMax = maxHp;
        float gap = 8, w = (500 - gap * (maxHp - 1)) / maxHp;
        for (int i = 0; i < maxHp; i++)
        {
            var holder = UiKit.Rect("Seg", bossBar, new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(-250 + i * (w + gap), 14), new Vector2(w, 22));
            UiKit.Img(holder, "Lip", UiKit.Rounded, UiKit.Darker(UiKit.Berry), C, C, new Vector2(0, -4), new Vector2(w, 22), 3f);
            bossSegs.Add(UiKit.Img(holder, "Seg", UiKit.Rounded, UiKit.Berry, C, C, Vector2.zero, new Vector2(w, 22), 3f));
        }
        SetBossHp(maxHp);
        bossPunch = 0;
    }

    public void SetBossHp(int hp)
    {
        if (hp < bossHp)
        {
            bossPunch = 1;
            if (hp >= 0 && hp < bossSegs.Count)
                Burst(FxPos(bossSegs[hp].rectTransform), 14, 500f, 22f, new[] { UiKit.Berry, UiKit.Grape, Color.white });
        }
        bossHp = hp;
    }

    /// <summary>Opens the portal chip without shards (used when a boss is defeated) - with a party.</summary>
    public void MarkPortalOpen(string toastMsg)
    {
        portalOpen = true;
        Toast(toastMsg, UiKit.Mint);
        ConfettiRain(140);
        if (AudioFx.I != null) AudioFx.I.Play(AudioFx.I.cheer, 0.8f, 0f);
    }

    public void SetStats(float time, int deaths)
    {
        int m = (int)(time / 60);
        float s = time - m * 60;
        timeText.text = $"{m:00}:{s:00.0}";
        deathText.text = deaths.ToString();
    }

    public void OnDeath() => deathShake = 1f;

    public void Toast(string msg) => Toast(msg, UiKit.Accent);

    public void Toast(string msg, Color col)
    {
        toastText.text = msg;
        float w = toastText.preferredWidth + 90;
        toast.sizeDelta = new Vector2(w, 66);
        toastBody.rectTransform.sizeDelta = new Vector2(w, 66);
        toastLip.rectTransform.sizeDelta = new Vector2(w, 66);
        toastColor = col;
        toastBody.color = col;
        toastLip.color = UiKit.Darker(col);
        toastText.GetComponent<Outline>().effectColor = UiKit.Darker(col, 0.45f);
        toastT = 0;
        var basePos = FxPos(toast);
        Burst(basePos + new Vector2(-w * 0.5f, 0), 6, 320f, 18f, new[] { col, Color.white }, null, 500f);
        Burst(basePos + new Vector2(w * 0.5f, 0), 6, 320f, 18f, new[] { col, Color.white }, null, 500f);
    }

    public void ShowPause(int index, LevelDef def)
    {
        pauseRoot.SetActive(true);
        pauseSub.text = $"LEVEL {index + 1}  -  {def.name.ToUpper()}";
        pauseT = 0;
        pauseMenu.Open(0);
    }

    public void HidePause() => pauseRoot.SetActive(false);

    /// <summary>Level complete card with a 1-3 star rating, counting-up stats and confetti.</summary>
    public void ShowComplete(string header, string sub, float time, int shifts, int deaths, string nextLabel, bool totals,
        bool[] earned, string[] notes, bool boss)
    {
        completeRoot.SetActive(true);
        completeHeader.text = header;
        completeSub.text = sub;
        var col = boss ? UiKit.Grape : UiKit.Sunny;
        completeRibbonBody.color = col;
        completeRibbonLip.color = UiKit.Darker(col);
        completeHeader.GetComponent<Outline>().effectColor = UiKit.Darker(col, 0.45f);
        statTarget = new[] { time, shifts, (float)deaths };
        statIsTime = true;
        statLabels[0].text = totals ? "TOTAL TIME" : "TIME";
        statLabels[1].text = totals ? "TOTAL SHIFTS" : "SHIFTS";
        statLabels[2].text = totals ? "TOTAL DEATHS" : "DEATHS";
        for (int i = 0; i < 3; i++) statValues[i].text = i == 0 ? "00:00.0" : "0";
        for (int i = 0; i < 3; i++)
        {
            starEarned[i] = earned != null && i < earned.Length && earned[i];
            starPopped[i] = false;
            starImgs[i].rectTransform.localScale = Vector3.zero;
            starNotes[i].text = notes != null && i < notes.Length ? notes[i] : "";
            starNotes[i].color = new Color(UiKit.InkSoft.r, UiKit.InkSoft.g, UiKit.InkSoft.b, starEarned[i] ? 0.9f : 0.35f);
        }
        nextBtn.label.text = nextLabel;
        completeT = 0;
        completeMenu.Open(0);
        ConfettiRain(boss ? 160 : 110);
        if (AudioFx.I != null) AudioFx.I.Play(AudioFx.I.cheer, 0.7f, 0f);
    }

    public bool ConfirmOpen => confirmRoot.activeSelf;

    public void Confirm(string message, string yesLabel, Action onYes, Action onNo)
    {
        confirmReturn = null;
        foreach (var m in new[] { titleMenu, pauseMenu, completeMenu })
            if (m.gameObject.activeInHierarchy && !m.blocked) { confirmReturn = m; m.blocked = true; }
        confirmText.text = message;
        confirmYes.label.text = yesLabel;
        onConfirmYes = onYes;
        onConfirmNo = onNo;
        confirmRoot.SetActive(true);
        confirmT = 0;
        confirmMenu.Open(1);   // default to CANCEL so a stray Enter never wipes progress
    }

    void CloseConfirm(bool yes)
    {
        confirmRoot.SetActive(false);
        if (confirmReturn != null) confirmReturn.blocked = false;
        var cb = yes ? onConfirmYes : onConfirmNo;
        onConfirmYes = onConfirmNo = null;
        cb?.Invoke();
    }

    public void Flash(Color c, float a)
    {
        flashColor = c;
        flashA = Mathf.Max(flashA, a);
    }

    // =================================================================== animation

    static float PopIn(float t, float dur = 0.4f) => Mathf.LerpUnclamped(0.6f, 1f, UiKit.EaseOutBack(Mathf.Clamp01(t / dur)));

    static float Bounce(float t)
    {
        if (t < 1 / 2.75f) return 7.5625f * t * t;
        if (t < 2 / 2.75f) { t -= 1.5f / 2.75f; return 7.5625f * t * t + 0.75f; }
        if (t < 2.5 / 2.75) { t -= 2.25f / 2.75f; return 7.5625f * t * t + 0.9375f; }
        t -= 2.625f / 2.75f; return 7.5625f * t * t + 0.984375f;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float time = Time.unscaledTime;
        float b = PhaseState.Smooth;
        var accent = UiKit.Accent;

        flashA = Mathf.MoveTowards(flashA, 0, dt * 2.2f);
        flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashA);

        if (hudRoot.activeSelf) AnimateHud(dt, time, b, accent);
        AnimateBanner(dt);
        AnimateToast(dt, time);
        if (titleRoot.activeSelf) AnimateTitle(dt, time, b);
        if (pauseRoot.activeSelf)
        {
            pauseT += dt;
            float s = PopIn(pauseT);
            pauseCard.localScale = new Vector3(s, s, 1);
        }
        if (confirmRoot.activeSelf)
        {
            confirmT += dt;
            float s = PopIn(confirmT, 0.3f);
            confirmCard.localScale = new Vector3(s, s, 1);
        }
        if (completeRoot.activeSelf) AnimateComplete(dt, time);
        UpdateBits(dt);
    }

    void AnimateHud(float dt, float time, float b, Color accent)
    {
        hudTime += Time.deltaTime;
        hintTime += dt;
        bool covered = pauseRoot.activeSelf || completeRoot.activeSelf;
        float ha = covered ? 0f : Mathf.Lerp(1f, 0.65f, Mathf.Clamp01((hintTime - 8f) / 2f));
        hint.color = new Color(1, 1, 1, ha);
        hint.GetComponent<Outline>().effectColor = new Color(0.15f, 0.08f, 0.25f, 0.85f * ha);
        controls.alpha = Mathf.Lerp(1f, 0.4f, Mathf.Clamp01((hudTime - 10f) / 1.5f));

        // level card slides in
        float cardIn = UiKit.EaseOutBack(Mathf.Clamp01(hintTime / 0.5f));
        levelCard.anchoredPosition = new Vector2(Mathf.LerpUnclamped(-620f, 30f, cardIn), -26);
        levelNumBg.color = accent;
        levelNum.GetComponent<Outline>().effectColor = UiKit.Darker(accent, 0.5f);
        levelNumBadge.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 2f) * 4f);

        // day / night switch
        if (PhaseState.Current != lastPhase)
        {
            lastPhase = PhaseState.Current;
            knobPunch = 1f;
            var c = lastPhase == Phase.Day ? new[] { UiKit.Sunny, Color.white, UiKit.Peach } : new[] { UiKit.Sky, Color.white, UiKit.Grape };
            Burst(FxPos(knob), 10, 380f, 18f, c, new[] { UiKit.Sparkle, UiKit.Star }, 600f);
        }
        knobPunch = Mathf.MoveTowards(knobPunch, 0, dt * 3f);
        var dayTrack = new Color(0.55f, 0.80f, 1f);
        var nightTrack = new Color(0.17f, 0.14f, 0.40f);
        var track = Color.Lerp(dayTrack, nightTrack, b);
        switchBody.color = track;
        switchLip.color = UiKit.Darker(track, 0.7f);
        float kx = Mathf.Lerp(-120 + 38, 120 - 38, b);
        float squash = Mathf.Sin(knobPunch * Mathf.PI) * 0.25f;
        knob.anchoredPosition = new Vector2(kx, 2);
        knob.localScale = new Vector3(1 + squash, 1 - squash, 1);
        knobLip.rectTransform.anchoredPosition = new Vector2(kx, -3);
        var knobCol = Color.Lerp(UiKit.Sunny, new Color(0.93f, 0.93f, 1f), b);
        knobImg.color = knobCol;
        knobLip.color = UiKit.Darker(knobCol, 0.7f);
        knobSun.color = new Color(1f, 1f, 1f, 1 - b);
        knobMoon.color = new Color(0.45f, 0.45f, 0.75f, b);
        knobSun.rectTransform.localRotation = Quaternion.Euler(0, 0, time * 40f);
        dayLabel.color = new Color(1, 1, 1, 1 - b);
        nightLabel.color = new Color(1, 1, 1, b);
        dayLabel.GetComponent<Outline>().effectColor = new Color(0.15f, 0.35f, 0.65f, 1 - b);
        nightLabel.GetComponent<Outline>().effectColor = new Color(0.08f, 0.06f, 0.25f, b);
        for (int i = 0; i < clouds.Length; i++)
        {
            clouds[i].color = new Color(1, 1, 1, 0.85f * (1 - b));
            clouds[i].rectTransform.anchoredPosition = new Vector2((i == 0 ? -10 : 98) + Mathf.Sin(time * 0.8f + i) * 5f, i == 0 ? 22 : -22);
        }
        for (int i = 0; i < nightStars.Length; i++)
        {
            float tw = 0.5f + 0.5f * Mathf.Sin(time * 4f + i * 2.1f);
            nightStars[i].color = new Color(1, 1, 0.85f, b * (0.4f + 0.6f * tw));
        }

        // shard pips spring
        for (int i = 0; i < pips.Count; i++)
        {
            if (!pips[i].gameObject.activeSelf) continue;
            pipPunch[i] = Mathf.MoveTowards(pipPunch[i], 0, dt * 2.5f);
            float s = 1f + Mathf.Sin(pipPunch[i] * Mathf.PI) * 0.6f + 0.04f * Mathf.Sin(time * 3f + i);
            pips[i].rectTransform.localScale = new Vector3(s, s, 1);
            pips[i].rectTransform.localRotation = Quaternion.Euler(0, 0, pipPunch[i] * 360f);
        }

        // death counter shake
        deathShake = Mathf.MoveTowards(deathShake, 0, dt * 2f);
        statsPill.anchoredPosition = new Vector2(-30 + Mathf.Sin(time * 60f) * 10f * deathShake, -176);
        deathText.color = Color.Lerp(UiKit.InkSoft, UiKit.Berry, deathShake);

        portalChip.gameObject.SetActive(portalOpen);
        if (portalOpen)
        {
            float pulse = 1f + 0.05f * Mathf.Sin(time * 6f);
            portalChip.localScale = new Vector3(pulse, pulse, 1);
            portalChip.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 3f) * 2.5f);
        }

        if (bossBar.gameObject.activeSelf)
        {
            bossPunch = Mathf.MoveTowards(bossPunch, 0, dt * 2.5f);
            bossName.color = Color.Lerp(UiKit.Grape, UiKit.Berry, bossPunch);
            for (int i = 0; i < bossSegs.Count; i++)
            {
                bool alive = i < bossHp;
                var c = alive ? Color.Lerp(UiKit.Berry, UiKit.Grape, 0.5f + 0.5f * Mathf.Sin(time * 3f + i * 0.8f)) : new Color(0.2f, 0.12f, 0.3f, 0.15f);
                bossSegs[i].color = c;
                bossSegs[i].transform.parent.Find("Lip").GetComponent<Image>().color = alive ? UiKit.Darker(c) : Color.clear;
            }
            float s = 1f + bossPunch * 0.08f;
            bossBar.localScale = new Vector3(s, s, 1);
            bossBar.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 40f) * 3f * bossPunch);
            if (bossHp <= 0 && bossPunch <= 0) bossBar.gameObject.SetActive(false);
        }
    }

    void AnimateBanner(float dt)
    {
        if (!banner.gameObject.activeSelf) return;
        float prevT = bannerT;
        bannerT += Mathf.Min(dt, 0.05f);
        if (bannerT < 0) return;
        if (prevT < 0 && AudioFx.I != null) AudioFx.I.Play(AudioFx.I.whoosh, 0.7f, 0.05f);
        const float inT = 0.5f, outT = 0.35f;
        float hold = BannerHold;
        float x;
        if (bannerT < inT) x = Mathf.LerpUnclamped(-1500f, 0f, UiKit.EaseOutBack(bannerT / inT));
        else if (bannerT < inT + hold) x = 0;
        else if (bannerT < inT + hold + outT) { float k = (bannerT - inT - hold) / outT; x = Mathf.Lerp(0, 1600f, k * k); }
        else { banner.gameObject.SetActive(false); return; }
        float shake = bannerBoss && bannerT > inT && bannerT < inT + hold ? Mathf.Sin(bannerT * 70f) * 6f : 0f;
        banner.anchoredPosition = new Vector2(x + shake, 120 + Mathf.Sin(bannerT * 4f) * 4f);
        banner.localRotation = Quaternion.Euler(0, 0, -3f + Mathf.Sin(bannerT * 3f) * 1.5f);
        if (prevT < inT && bannerT >= inT)
            Burst(FxPos(banner), bannerBoss ? 26 : 18, 700f, 26f, bannerBoss ? new[] { UiKit.Berry, UiKit.Grape, Color.white } : UiKit.Confetti);
    }

    void AnimateToast(float dt, float time)
    {
        toastT += dt;
        const float hold = 1.9f;
        if (toastT < hold + 0.4f)
        {
            float a = toastT < 0.12f ? toastT / 0.12f : Mathf.Clamp01((hold + 0.4f - toastT) / 0.4f);
            toastGroup.alpha = a;
            float s = toastT < 0.35f ? Mathf.LerpUnclamped(0.5f, 1f, UiKit.EaseOutBack(toastT / 0.35f)) : 1f + 0.02f * Mathf.Sin(time * 6f);
            toast.localScale = new Vector3(s, s, 1);
            toast.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(toastT * 18f) * 8f * Mathf.Exp(-toastT * 4f));
        }
        else toastGroup.alpha = 0;
    }

    void AnimateTitle(float dt, float time, float b)
    {
        titleT += dt;
        var dayCols = new[] { UiKit.Sunny, UiKit.Peach, UiKit.Berry, UiKit.Grape, UiKit.Sky };
        var nightCols = new[] { UiKit.Sky, UiKit.Mint, UiKit.Grape, UiKit.Berry, UiKit.Sunny };
        for (int i = 0; i < logo.Count; i++)
        {
            var col = Color.Lerp(dayCols[i], nightCols[i], b);
            logo[i].color = UiKit.Lighter(col, 0.08f);
            logoShadows[i].effectColor = UiKit.Darker(col, 0.55f);
            var rt = logo[i].rectTransform;
            float start = 0.15f + i * 0.09f;
            float k = Mathf.Clamp01((titleT - start) / 0.75f);
            float drop = (1 - Bounce(k)) * 700f;
            float wave = k >= 1 ? Mathf.Sin(time * 2.2f + i * 0.8f) * 12f : 0f;
            rt.anchoredPosition = new Vector2((i - 2) * 162, 300 + drop + wave);
            float landSquash = k > 0.3f && k < 0.6f ? Mathf.Sin((k - 0.3f) / 0.3f * Mathf.PI) * 0.15f : 0f;
            rt.localScale = new Vector3(1 + landSquash, 1 - landSquash, 1);
            rt.localRotation = Quaternion.Euler(0, 0, k >= 1 ? Mathf.Sin(time * 1.7f + i) * 4f : 0f);
            if (titleT - dt < start + 0.25f && titleT >= start + 0.25f)
            {
                Burst(FxPos(rt) + new Vector2(0, -90), 8, 360f, 20f, new[] { col, Color.white }, null, 700f);
                if (AudioFx.I != null) AudioFx.I.PlayPitch(AudioFx.I.pop, 0.4f, 0.9f + i * 0.12f);
            }
        }

        // sun & moon orbit around the logo
        float ang = time * 0.7f;
        var sunP = new Vector2(Mathf.Cos(ang) * 600f, 300 + Mathf.Sin(ang) * 120f);
        var moonP = new Vector2(Mathf.Cos(ang + Mathf.PI) * 600f, 300 + Mathf.Sin(ang + Mathf.PI) * 120f);
        orbitSun.anchoredPosition = sunP;
        orbitMoon.anchoredPosition = moonP;
        float sunDepth = Mathf.Sin(ang), moonDepth = -sunDepth;
        orbitSun.localScale = Vector3.one * (1f + 0.25f * -sunDepth);
        orbitMoon.localScale = Vector3.one * (1f + 0.25f * -moonDepth);
        orbitSun.localRotation = Quaternion.Euler(0, 0, time * 30f);
        orbitSun.GetComponent<Image>().color = new Color(UiKit.Sunny.r, UiKit.Sunny.g, UiKit.Sunny.b, sunDepth > 0 ? 0.55f : 1f);
        orbitMoon.GetComponent<Image>().color = new Color(0.85f, 0.9f, 1f, moonDepth > 0 ? 0.55f : 1f);
        // behind the letters when "far", in front when "near"
        if (sunDepth > 0) orbitSun.SetSiblingIndex(0); else orbitSun.SetSiblingIndex(logo[4].transform.GetSiblingIndex());
        if (moonDepth > 0) orbitMoon.SetSiblingIndex(0); else orbitMoon.SetSiblingIndex(logo[4].transform.GetSiblingIndex());

        tagline.rectTransform.anchoredPosition = new Vector2(0, 150 + Mathf.Sin(time * 1.6f) * 5f);
        float tagIn = Mathf.Clamp01((titleT - 0.7f) / 0.4f);
        tagline.color = new Color(1, 1, 1, tagIn);
        tagline.GetComponent<Outline>().effectColor = new Color(0.18f, 0.08f, 0.3f, tagIn);

        bestChip.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 2f) * 2f);

        // ambient twinkles
        sparkleTimer -= dt;
        if (sparkleTimer <= 0)
        {
            sparkleTimer = 0.09f;
            var pos = new Vector2(UnityEngine.Random.Range(-950f, 950f), UnityEngine.Random.Range(-520f, 520f));
            var bit = SpawnBit(UnityEngine.Random.value < 0.7f ? UiKit.Sparkle : UiKit.Star,
                Color.Lerp(Color.white, UiKit.Confetti[UnityEngine.Random.Range(0, 6)], 0.35f),
                pos, new Vector2(0, UnityEngine.Random.Range(10f, 40f)), UnityEngine.Random.Range(12f, 30f), UnityEngine.Random.Range(1.2f, 2.2f), 0f, true);
        }
    }

    void AnimateComplete(float dt, float time)
    {
        completeT += dt;
        float s = PopIn(completeT, 0.45f);
        completeCard.localScale = new Vector3(s, s, 1);
        completeHeader.transform.parent.parent.localRotation = Quaternion.Euler(0, 0, -2f + Mathf.Sin(time * 2.5f) * 2f);

        // stars pop one after another
        for (int i = 0; i < 3; i++)
        {
            float popAt = 0.55f + i * 0.38f;
            var rt = starImgs[i].rectTransform;
            bool earned = starEarned[i];
            if (completeT >= popAt)
            {
                float k = Mathf.Clamp01((completeT - popAt) / 0.45f);
                float sc = earned ? Mathf.LerpUnclamped(0f, 1f, UiKit.EaseOutBack(k)) * (1f + 0.04f * Mathf.Sin(time * 4f + i)) : Mathf.Lerp(0f, 0.8f, k);
                rt.localScale = new Vector3(sc, sc, 1);
                rt.localRotation = Quaternion.Euler(0, 0, earned ? (1 - k) * -180f + Mathf.Sin(time * 2f + i) * 5f : 0f);
                starImgs[i].sprite = earned ? UiKit.Star : UiKit.StarOutline;
                starImgs[i].color = earned ? UiKit.Sunny : new Color(0.2f, 0.12f, 0.3f, 0.25f);
                if (!starPopped[i])
                {
                    starPopped[i] = true;
                    if (earned)
                    {
                        Burst(FxPos(rt), 16, 650f, 24f, new[] { UiKit.Sunny, Color.white, UiKit.Peach, UiKit.Berry });
                        if (AudioFx.I != null) AudioFx.I.Play(i == 0 ? AudioFx.I.star1 : i == 1 ? AudioFx.I.star2 : AudioFx.I.star3, 0.9f, 0f);
                    }
                    else if (AudioFx.I != null) AudioFx.I.PlayPitch(AudioFx.I.pop, 0.35f, 0.6f);
                }
            }
            else rt.localScale = Vector3.zero;
        }

        // stats count up
        float ck = Mathf.Clamp01((completeT - 0.3f) / 0.9f);
        ck = 1 - (1 - ck) * (1 - ck);
        float tv = statTarget[0] * ck;
        int m = (int)(tv / 60);
        float sec = tv - m * 60;
        statValues[0].text = $"{m:00}:{sec:00.0}";
        statValues[1].text = Mathf.RoundToInt(statTarget[1] * ck).ToString();
        statValues[2].text = Mathf.RoundToInt(statTarget[2] * ck).ToString();
    }
}
