using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// All UI, built at runtime: HUD (level card, day/night switch, shard pips, timer, on-screen buttons, controls bar,
/// toasts) and screens (title menu, pause menu, level-complete card, confirm dialog). Works with mouse, keyboard
/// and gamepad.
/// </summary>
public class Hud : MonoBehaviour
{
    public GameManager gm;

    Transform root;
    public Image flash, fade;

    // HUD
    GameObject hudRoot;
    RectTransform levelCard;
    Text levelBadge, levelName, hint;
    Image levelBar;
    RectTransform knob;
    Image knobImg, knobGlow, knobSun, knobMoon;
    Text dayLabel, nightLabel;
    RectTransform shardPill;
    readonly List<Image> pips = new List<Image>();
    Text shardLabel;
    Text timeText, deathText;
    RectTransform portalChip;
    Image portalChipBg;
    CanvasGroup controls;
    Menu hudButtons;
    float hudTime, hintTime;
    bool portalOpen;

    // toast
    RectTransform toast;
    Image toastBg;
    Text toastText;
    CanvasGroup toastGroup;
    float toastT = 99f;

    // title
    GameObject titleRoot;
    readonly List<Text> logo = new List<Text>();
    Text tagline, bestText;
    Menu titleMenu;
    MenuButton continueBtn, selectorBtn, newGameBtn, quitBtn;

    // pause
    GameObject pauseRoot;
    Menu pauseMenu;
    Text pauseSub;

    // complete
    GameObject completeRoot;
    Menu completeMenu;
    Text completeHeader, completeSub;
    readonly Text[] statValues = new Text[3];
    readonly Text[] statLabels = new Text[3];
    MenuButton nextBtn;
    RectTransform completeCard;
    float completeT;

    // confirm
    GameObject confirmRoot;
    Menu confirmMenu;
    Text confirmText;
    MenuButton confirmYes;
    Action onConfirmYes, onConfirmNo;
    Menu confirmReturn;

    float flashA;
    Color flashColor;

    // ------------------------------------------------------------------ build

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
        BuildToast();
        BuildTitle();
        BuildPause();
        BuildComplete();
        BuildConfirm();

        flash = UiKit.Fill(root, "Flash", new Color(1, 1, 1, 0));
        fade = UiKit.Fill(root, "Fade", new Color(0.02f, 0.02f, 0.05f, 1));

        hudRoot.SetActive(false);
        titleRoot.SetActive(false);
        pauseRoot.SetActive(false);
        completeRoot.SetActive(false);
        confirmRoot.SetActive(false);
    }

    GameObject Layer(string name) => UiKit.Stretch(name, root).gameObject;

    Image Card(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, float corner = 1.6f, float alpha = 0.7f)
    {
        var c = UiKit.Img(parent, name, UiKit.Rounded, new Color(0.06f, 0.05f, 0.13f, alpha), anchor, pivot, pos, size, corner);
        var edge = UiKit.Img(c.transform, "Edge", UiKit.RoundedOutline, new Color(1, 1, 1, 0.12f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, corner);
        edge.rectTransform.anchorMin = Vector2.zero; edge.rectTransform.anchorMax = Vector2.one;
        edge.rectTransform.offsetMin = edge.rectTransform.offsetMax = Vector2.zero;
        return c;
    }

    void BuildHud()
    {
        hudRoot = Layer("HUD");
        var t = hudRoot.transform;
        var TL = new Vector2(0, 1);
        var TR = new Vector2(1, 1);

        // ---- level card (top-left)
        var card = Card(t, "LevelCard", TL, TL, new Vector2(36, -30), new Vector2(640, 116));
        levelCard = card.rectTransform;
        levelBar = UiKit.Img(card.transform, "Bar", UiKit.Rounded, Color.white, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(8, 74), 4f);
        levelBadge = UiKit.Txt(card.transform, "", 22, Color.white, TextAnchor.UpperLeft, TL, TL, new Vector2(46, -18), new Vector2(560, 30), FontStyle.Bold, 0);
        levelName = UiKit.Txt(card.transform, "", 44, Color.white, TextAnchor.UpperLeft, TL, TL, new Vector2(44, -48), new Vector2(900, 56), FontStyle.Bold, 0.35f);
        levelName.horizontalOverflow = HorizontalWrapMode.Overflow;
        hint = UiKit.Txt(t, "", 26, new Color(1, 1, 1, 0.92f), TextAnchor.UpperLeft, TL, TL, new Vector2(42, -160), new Vector2(880, 90), FontStyle.Normal, 0.6f);

        // ---- top-right: [day/night switch] [RESTART] [MENU]
        hudButtons = UiKit.Rect("HudButtons", t, TR, TR, Vector2.zero, Vector2.zero).gameObject.AddComponent<Menu>();
        hudButtons.pointerOnly = true;
        hudButtons.selected = -1;
        var menuBtn = hudButtons.AddButton(t, "MENU", Vector2.zero, new Vector2(156, 64), UiKit.Pause, () => gm.UiPause());
        Pin(menuBtn.transform, TR, new Vector2(-36 - 78, -30 - 32));
        var restartBtn = hudButtons.AddButton(t, "RESTART", Vector2.zero, new Vector2(200, 64), UiKit.Restart, () => gm.UiAskRestartFromOne());
        Pin(restartBtn.transform, TR, new Vector2(-36 - 156 - 12 - 100, -30 - 32));

        var sw = UiKit.Img(t, "PhaseSwitch", UiKit.Rounded, new Color(0.06f, 0.05f, 0.13f, 0.7f), TR, TR, new Vector2(-36 - 156 - 12 - 200 - 12, -30), new Vector2(236, 64), 1.1f);
        UiKit.Img(sw.transform, "Edge", UiKit.RoundedOutline, new Color(1, 1, 1, 0.12f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(236, 64), 1.1f);
        dayLabel = UiKit.Txt(sw.transform, "DAY", 26, Color.white, TextAnchor.MiddleCenter, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(150, 40), FontStyle.Bold, 0);
        nightLabel = UiKit.Txt(sw.transform, "NIGHT", 26, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(150, 40), FontStyle.Bold, 0);
        knobGlow = UiKit.Img(sw.transform, "Glow", UiKit.SoftGlow, Color.white, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(38, 0), new Vector2(120, 120));
        knobImg = UiKit.Img(sw.transform, "Knob", UiKit.Dot, Color.white, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(38, 0), new Vector2(52, 52));
        knob = knobImg.rectTransform;
        knobSun = UiKit.Img(knob, "Sun", UiKit.Sun, UiKit.Ink, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38, 38));
        knobMoon = UiKit.Img(knob, "Moon", UiKit.Moon, UiKit.Ink, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(32, 32));

        // ---- shards pill
        shardPill = Card(t, "Shards", TR, TR, new Vector2(-36, -108), new Vector2(300, 54), 1.1f).rectTransform;
        shardLabel = UiKit.Txt(shardPill, "SHARDS", 20, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(120, 40), FontStyle.Bold, 0);
        for (int i = 0; i < 12; i++)
            pips.Add(UiKit.Img(shardPill, "Pip", UiKit.Diamond, Color.white, new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 32)));

        // ---- stats pill
        var stats = Card(t, "Stats", TR, TR, new Vector2(-36, -172), new Vector2(300, 50), 1.1f).rectTransform;
        UiKit.Img(stats, "Clock", UiKit.Clock, new Color(1, 1, 1, 0.75f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34, 0), new Vector2(26, 26));
        timeText = UiKit.Txt(stats, "00:00.0", 26, Color.white, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(56, 0), new Vector2(130, 40), FontStyle.Bold, 0);
        UiKit.Img(stats, "Cross", UiKit.Cross, new Color(1, 0.45f, 0.5f, 0.85f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(204, 0), new Vector2(22, 22));
        deathText = UiKit.Txt(stats, "0", 26, Color.white, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(224, 0), new Vector2(70, 40), FontStyle.Bold, 0);

        // ---- portal chip
        portalChipBg = UiKit.Img(t, "PortalChip", UiKit.Rounded, Color.white, TR, TR, new Vector2(-36, -234), new Vector2(230, 46), 1.4f);
        portalChip = portalChipBg.rectTransform;
        UiKit.Txt(portalChip, "PORTAL OPEN", 22, UiKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230, 46), FontStyle.Bold, 0);

        // ---- controls bar (bottom)
        var bar = UiKit.Rect("Controls", t, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(1600, 54));
        controls = bar.gameObject.AddComponent<CanvasGroup>();
        controls.blocksRaycasts = false;
        BuildKeyRow(bar, new[] { ("WASD", "MOVE"), ("SPACE", "JUMP"), ("SHIFT", "FLIP WORLD"), ("R", "RETRY"), ("ESC", "MENU") });
    }

    static void Pin(Transform tr, Vector2 anchor, Vector2 pos)
    {
        var rt = (RectTransform)tr;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
    }

    void BuildKeyRow(RectTransform bar, (string key, string label)[] keys)
    {
        var parts = new List<(RectTransform rt, float w)>();
        float total = 0, gap = 34;
        foreach (var (key, label) in keys)
        {
            var group = UiKit.Rect("Key " + key, bar, new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(10, 54));
            var capText = UiKit.Txt(group, key, 22, UiKit.Ink, TextAnchor.MiddleCenter, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(10, 40), FontStyle.Bold, 0);
            float capW = Mathf.Max(46, capText.preferredWidth + 26);
            var cap = UiKit.Img(group, "Cap", UiKit.Rounded, new Color(0.96f, 0.95f, 0.98f, 0.95f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(capW, 42), 2.4f);
            UiKit.Img(cap.transform, "Lip", UiKit.Rounded, new Color(0, 0, 0, 0.18f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(capW, 8), 6f);
            cap.transform.SetAsFirstSibling();
            capText.rectTransform.sizeDelta = new Vector2(capW, 40);
            var lab = UiKit.Txt(group, label, 22, new Color(1, 1, 1, 0.9f), TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(capW + 12, 0), new Vector2(400, 40), FontStyle.Bold, 0.5f);
            float w = capW + 12 + lab.preferredWidth;
            lab.rectTransform.sizeDelta = new Vector2(lab.preferredWidth + 4, 40);
            parts.Add((group, w));
            total += w;
        }
        total += gap * (parts.Count - 1);
        float x = -total / 2;
        foreach (var (rt, w) in parts) { rt.anchoredPosition = new Vector2(x, 0); x += w + gap; }
    }

    void BuildToast()
    {
        toast = UiKit.Rect("Toast", root, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(420, 66));
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.blocksRaycasts = false;
        toastBg = UiKit.Img(toast, "Bg", UiKit.Rounded, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 66), 1.1f);
        toastText = UiKit.Txt(toast, "", 30, UiKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 60), FontStyle.Bold, 0);
        toastGroup.alpha = 0;
    }

    void BuildTitle()
    {
        titleRoot = Layer("Title");
        var t = titleRoot.transform;
        var C = new Vector2(0.5f, 0.5f);


        string word = "SHIFT";
        for (int i = 0; i < word.Length; i++)
        {
            var letter = UiKit.Txt(t, word[i].ToString(), 210, Color.white, TextAnchor.MiddleCenter, C, C, new Vector2((i - 2) * 158, 300), new Vector2(220, 260), FontStyle.Bold, 0);
            var o = letter.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.05f, 0.04f, 0.12f, 0.95f);
            o.effectDistance = new Vector2(5, -5);
            var sh = letter.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.35f);
            sh.effectDistance = new Vector2(0, -14);
            logo.Add(letter);
        }
        tagline = UiKit.Txt(t, "Day and night are one key apart.", 36, new Color(1, 1, 1, 0.95f), TextAnchor.MiddleCenter, C, C, new Vector2(0, 160), new Vector2(1400, 50), FontStyle.Bold, 0.6f);

        titleMenu = UiKit.Rect("TitleMenu", t, C, C, Vector2.zero, Vector2.zero).gameObject.AddComponent<Menu>();
        var size = new Vector2(600, 84);
        continueBtn = titleMenu.AddButton(t, "PLAY", new Vector2(0, 40), size, UiKit.Play, () => gm.UiContinue(), true);
        selectorBtn = titleMenu.AddSelector(t, new Vector2(0, -60), size, () => { gm.UiSelectLevel(-1); RefreshTitle(); }, () => { gm.UiSelectLevel(1); RefreshTitle(); }, () => gm.UiPlaySelected());
        newGameBtn = titleMenu.AddButton(t, "NEW GAME  -  FROM LEVEL 1", new Vector2(0, -160), size, UiKit.Restart, () => gm.UiNewGame());
        quitBtn = titleMenu.AddButton(t, "QUIT", new Vector2(0, -260), size, UiKit.Exit, () => gm.UiQuit());
#if UNITY_WEBGL
        quitBtn.gameObject.SetActive(false);
#endif

        bestText = UiKit.Txt(t, "", 24, new Color(1, 1, 1, 0.8f), TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 30), new Vector2(700, 40), FontStyle.Bold);
        UiKit.Txt(t, "GITHUB GAME OFF 2026  -  THEME: SHIFT", 20, new Color(1, 1, 1, 0.75f), TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-36, -28), new Vector2(700, 40), FontStyle.Bold);
        var keys = UiKit.Rect("TitleKeys", t, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(1200, 54));
        BuildKeyRow(keys, new[] { ("SHIFT", "FLIP THE WORLD"), ("W / S", "CHOOSE"), ("A / D", "CHANGE LEVEL"), ("ENTER", "SELECT") });
    }

    void BuildPause()
    {
        pauseRoot = Layer("Pause");
        var t = pauseRoot.transform;
        var C = new Vector2(0.5f, 0.5f);
        UiKit.Fill(t, "Dim", new Color(0.02f, 0.01f, 0.06f, 0.55f), true);
        var card = Card(t, "Card", C, C, Vector2.zero, new Vector2(700, 660), 1.4f, 0.93f);
        UiKit.Txt(card.transform, "PAUSED", 72, Color.white, TextAnchor.MiddleCenter, C, C, new Vector2(0, 250), new Vector2(680, 90), FontStyle.Bold, 0.4f);
        pauseSub = UiKit.Txt(card.transform, "", 26, Color.white, TextAnchor.MiddleCenter, C, C, new Vector2(0, 192), new Vector2(680, 40), FontStyle.Bold, 0);

        pauseMenu = UiKit.Rect("PauseMenu", t, C, C, Vector2.zero, Vector2.zero).gameObject.AddComponent<Menu>();
        var size = new Vector2(560, 80);
        pauseMenu.AddButton(card.transform, "RESUME", new Vector2(0, 95), size, UiKit.Play, () => gm.UiResume(), true);
        pauseMenu.AddButton(card.transform, "RESTART LEVEL", new Vector2(0, 0), size, UiKit.Restart, () => gm.UiRestartLevel());
        pauseMenu.AddButton(card.transform, "RESTART FROM LEVEL 1", new Vector2(0, -95), size, UiKit.Flag, () => gm.UiAskRestartFromOne());
        pauseMenu.AddButton(card.transform, "MAIN MENU", new Vector2(0, -190), size, UiKit.Home, () => gm.UiMainMenu());
        pauseMenu.onBack = () => gm.UiResume();
    }

    void BuildComplete()
    {
        completeRoot = Layer("Complete");
        var t = completeRoot.transform;
        var C = new Vector2(0.5f, 0.5f);
        UiKit.Fill(t, "Dim", new Color(0.02f, 0.01f, 0.06f, 0.4f), true);
        var card = Card(t, "Card", C, C, Vector2.zero, new Vector2(780, 780), 1.4f, 0.93f);
        completeCard = card.rectTransform;
        completeHeader = UiKit.Txt(card.transform, "", 60, Color.white, TextAnchor.MiddleCenter, C, C, new Vector2(0, 310), new Vector2(760, 80), FontStyle.Bold, 0.4f);
        completeSub = UiKit.Txt(card.transform, "", 26, Color.white, TextAnchor.MiddleCenter, C, C, new Vector2(0, 250), new Vector2(700, 60), FontStyle.Bold, 0);

        var icons = new[] { UiKit.Clock, UiKit.ShiftArrow, UiKit.Cross };
        var names = new[] { "TIME", "SHIFTS", "DEATHS" };
        for (int i = 0; i < 3; i++)
        {
            float y = 160 - i * 70;
            var row = UiKit.Img(card.transform, "Row", UiKit.Rounded, new Color(1, 1, 1, 0.06f), C, C, new Vector2(0, y), new Vector2(600, 58), 1.6f);
            UiKit.Img(row.transform, "Icon", icons[i], new Color(1, 1, 1, 0.85f), new Vector2(0, 0.5f), C, new Vector2(40, 0), new Vector2(30, 30));
            statLabels[i] = UiKit.Txt(row.transform, names[i], 24, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(76, 0), new Vector2(260, 40), FontStyle.Bold, 0);
            statValues[i] = UiKit.Txt(row.transform, "", 32, Color.white, TextAnchor.MiddleRight, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-28, 0), new Vector2(300, 44), FontStyle.Bold, 0);
        }

        completeMenu = UiKit.Rect("CompleteMenu", t, C, C, Vector2.zero, Vector2.zero).gameObject.AddComponent<Menu>();
        var size = new Vector2(560, 80);
        nextBtn = completeMenu.AddButton(card.transform, "NEXT LEVEL", new Vector2(0, -80), size, UiKit.Play, () => gm.UiNext(), true);
        completeMenu.AddButton(card.transform, "PLAY AGAIN", new Vector2(0, -175), size, UiKit.Restart, () => gm.UiReplayCompleted());
        completeMenu.AddButton(card.transform, "MAIN MENU", new Vector2(0, -270), size, UiKit.Home, () => gm.UiMainMenu());
    }

    void BuildConfirm()
    {
        confirmRoot = Layer("Confirm");
        var t = confirmRoot.transform;
        var C = new Vector2(0.5f, 0.5f);
        UiKit.Fill(t, "Dim", new Color(0.02f, 0.01f, 0.06f, 0.6f), true);
        var card = Card(t, "Card", C, C, Vector2.zero, new Vector2(780, 420), 1.4f, 0.97f);
        UiKit.Img(card.transform, "Icon", UiKit.Flag, UiKit.Accent, C, C, new Vector2(0, 140), new Vector2(48, 48));
        confirmText = UiKit.Txt(card.transform, "", 32, Color.white, TextAnchor.MiddleCenter, C, C, new Vector2(0, 60), new Vector2(680, 110), FontStyle.Bold, 0);
        confirmMenu = UiKit.Rect("ConfirmMenu", t, C, C, Vector2.zero, Vector2.zero).gameObject.AddComponent<Menu>();
        var size = new Vector2(520, 76);
        confirmYes = confirmMenu.AddButton(card.transform, "YES", new Vector2(0, -60), size, UiKit.Restart, () => CloseConfirm(true), true);
        confirmMenu.AddButton(card.transform, "CANCEL", new Vector2(0, -150), size, UiKit.Cross, () => CloseConfirm(false));
        confirmMenu.onBack = () => CloseConfirm(false);
    }

    // ------------------------------------------------------------------ public API

    public void HideAll()
    {
        hudRoot.SetActive(false);
        titleRoot.SetActive(false);
        pauseRoot.SetActive(false);
        completeRoot.SetActive(false);
        confirmRoot.SetActive(false);
        toastT = 99f;
    }

    public void ShowTitle()
    {
        HideAll();
        titleRoot.SetActive(true);
        RefreshTitle();
        titleMenu.Open(0);
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
        selectorBtn.sub.text = def.generated ? $"{def.name.ToUpper()}  -  ENDLESS" : def.name.ToUpper();
        selectorBtn.onLeft = sel > 0 ? (Action)(() => { gm.UiSelectLevel(-1); RefreshTitle(); }) : null;
        selectorBtn.onRight = sel < unlocked - 1 ? (Action)(() => { gm.UiSelectLevel(1); RefreshTitle(); }) : null;
        bestText.text = unlocked > 1 ? $"BEST  -  LEVEL {unlocked}" : "";
    }

    public void ShowHud(int index, LevelDef def)
    {
        HideAll();
        hudRoot.SetActive(true);
        levelBadge.text = def.generated
            ? UiKit.Spaced($"LEVEL {index + 1}") + "   -   " + UiKit.Spaced("ENDLESS")
            : UiKit.Spaced($"LEVEL {index + 1}/{Levels.HandmadeCount}");
        levelName.text = def.name.ToUpper();
        levelCard.sizeDelta = new Vector2(Mathf.Max(460, levelName.preferredWidth + 90, levelBadge.preferredWidth + 80), 116);
        hint.text = def.hint;
        hudTime = 0;
        hintTime = 0;
        portalOpen = false;
    }

    public void SetShards(int got, int total)
    {
        int n = Mathf.Min(total, pips.Count);
        float pipW = 30;
        float width = 130 + Mathf.Max(n, 1) * pipW + 10;
        shardLabel.text = total == 0 ? "NO SHARDS" : "SHARDS";
        shardPill.sizeDelta = new Vector2(total == 0 ? 220 : width, 54);
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
            p.color = filled ? Palette.Shard : new Color(1, 1, 1, 0.45f);
        }
        bool open = got >= total;
        if (open && !portalOpen && hudTime > 0.5f) Toast(total == 0 ? "PORTAL OPEN" : "ALL SHARDS  -  PORTAL OPEN");
        portalOpen = open;
    }

    public void SetStats(float time, int deaths)
    {
        int m = (int)(time / 60);
        float s = time - m * 60;
        timeText.text = $"{m:00}:{s:00.0}";
        deathText.text = deaths.ToString();
    }

    public void Toast(string msg)
    {
        toastText.text = msg;
        float w = toastText.preferredWidth + 80;
        toast.sizeDelta = new Vector2(w, 66);
        toastBg.rectTransform.sizeDelta = new Vector2(w, 66);
        toastT = 0;
    }

    public void ShowPause(int index, LevelDef def)
    {
        pauseRoot.SetActive(true);
        pauseSub.text = $"LEVEL {index + 1}  -  {def.name.ToUpper()}";
        pauseMenu.Open(0);
    }

    public void HidePause() => pauseRoot.SetActive(false);

    public void ShowComplete(string header, string sub, float time, int shifts, int deaths, string nextLabel, bool totals)
    {
        completeRoot.SetActive(true);
        completeHeader.text = header;
        completeSub.text = sub;
        int m = (int)(time / 60);
        float s = time - m * 60;
        statValues[0].text = $"{m:00}:{s:00.0}";
        statValues[1].text = shifts.ToString();
        statValues[2].text = deaths.ToString();
        statLabels[0].text = totals ? "TOTAL TIME" : "TIME";
        statLabels[1].text = totals ? "TOTAL SHIFTS" : "SHIFTS";
        statLabels[2].text = totals ? "TOTAL DEATHS" : "DEATHS";
        nextBtn.label.text = nextLabel;
        completeT = 0;
        completeMenu.Open(0);
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

    // ------------------------------------------------------------------ animation

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float b = PhaseState.Smooth;
        var accent = UiKit.Accent;

        flashA = Mathf.MoveTowards(flashA, 0, dt * 2.2f);
        flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashA);

        if (hudRoot.activeSelf)
        {
            hudTime += Time.deltaTime;
            hintTime += dt;
            levelBar.color = accent;
            levelBadge.color = accent;
            bool covered = pauseRoot.activeSelf || completeRoot.activeSelf;
            hint.color = new Color(1, 1, 1, covered ? 0f : Mathf.Lerp(0.95f, 0.6f, Mathf.Clamp01((hintTime - 8f) / 2f)));
            controls.alpha = Mathf.Lerp(1f, 0.38f, Mathf.Clamp01((hudTime - 10f) / 1.5f));

            float x = Mathf.Lerp(38, 236 - 38, b);
            knob.anchoredPosition = new Vector2(x, 0);
            knobGlow.rectTransform.anchoredPosition = new Vector2(x, 0);
            knobImg.color = accent;
            knobGlow.color = new Color(accent.r, accent.g, accent.b, 0.55f);
            knobSun.color = new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 1 - b);
            knobMoon.color = new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, b);
            knob.localRotation = Quaternion.Euler(0, 0, -b * 180f);
            dayLabel.color = new Color(1, 1, 1, 1 - b);
            nightLabel.color = new Color(1, 1, 1, b);

            portalChip.gameObject.SetActive(portalOpen);
            if (portalOpen)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f);
                portalChipBg.color = new Color(Palette.Shard.r, Palette.Shard.g, Palette.Shard.b, pulse);
            }
        }

        // toast: pop in, hold, fade out
        toastT += dt;
        const float hold = 1.8f;
        if (toastT < hold + 0.4f)
        {
            float a = toastT < 0.15f ? toastT / 0.15f : Mathf.Clamp01((hold + 0.4f - toastT) / 0.4f);
            toastGroup.alpha = a;
            float s = toastT < 0.3f ? Mathf.LerpUnclamped(0.7f, 1f, UiKit.EaseOutBack(toastT / 0.3f)) : 1f;
            toast.localScale = new Vector3(s, s, 1);
            toastBg.color = accent;
        }
        else toastGroup.alpha = 0;

        if (titleRoot.activeSelf)
        {
            float split = Mathf.Lerp(1.25f, -0.25f, b);
            var dayTop = new Color(1f, 0.86f, 0.5f);
            var nightTop = new Color(0.55f, 0.82f, 1f);
            for (int i = 0; i < logo.Count; i++)
            {
                float t = i / (float)(logo.Count - 1);
                float k = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(split - 0.2f, split + 0.2f, t));
                logo[i].color = Color.Lerp(dayTop, nightTop, k);
                var rt = logo[i].rectTransform;
                rt.anchoredPosition = new Vector2((i - 2) * 158, 300 + Mathf.Sin(Time.unscaledTime * 1.8f + i * 0.7f) * 8f);
            }
        }

        if (completeRoot.activeSelf)
        {
            completeT += dt;
            float s = Mathf.LerpUnclamped(0.85f, 1f, UiKit.EaseOutBack(Mathf.Clamp01(completeT / 0.35f)));
            completeCard.localScale = new Vector3(s, s, 1);
            completeHeader.color = accent;
        }
    }
}
