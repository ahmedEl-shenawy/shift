using UnityEngine;
using UnityEngine.UI;

/// <summary>All UI, built at runtime with uGUI.</summary>
public class Hud : MonoBehaviour
{
    Font font;
    public GameObject hudRoot, titleRoot, msgRoot;
    public Text levelTitle, hint, shardText, phaseText, controls;
    public Text title, titleSub, titlePrompt, titleLevel, credits;
    public Text msgHeader, msgBody, msgPrompt;
    public Image phasePill, flash, fade, msgBack;

    float flashA;
    Color flashColor;
    float hintTimer;

    public void Build()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = canvasGo.transform;

        // ---------- HUD ----------
        hudRoot = Panel("HUD", root);
        levelTitle = MakeText(hudRoot.transform, "", 40, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(50, -36), new Vector2(1100, 60), Color.white, FontStyle.Bold);
        hint = MakeText(hudRoot.transform, "", 28, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(52, -92), new Vector2(1200, 80), new Color(1, 1, 1, 0.9f));

        var pillGo = new GameObject("PhasePill", typeof(RectTransform), typeof(Image));
        pillGo.transform.SetParent(hudRoot.transform, false);
        var prt = (RectTransform)pillGo.transform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1, 1);
        prt.anchoredPosition = new Vector2(-50, -36);
        prt.sizeDelta = new Vector2(220, 64);
        phasePill = pillGo.GetComponent<Image>();
        phaseText = MakeText(pillGo.transform, "DAY", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 64), Color.white, FontStyle.Bold);

        shardText = MakeText(hudRoot.transform, "", 32, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-50, -112), new Vector2(500, 50), Palette.Shard, FontStyle.Bold);
        controls = MakeText(hudRoot.transform, "WASD / Arrows  move     SPACE  jump     SHIFT  flip world     R  restart     ESC  pause",
            24, TextAnchor.LowerCenter, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1800, 40), new Color(1, 1, 1, 0.75f));

        // ---------- Title ----------
        titleRoot = Panel("Title", root);
        title = MakeText(titleRoot.transform, "S H I F T", 190, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(1800, 240), Color.white, FontStyle.Bold);
        titleSub = MakeText(titleRoot.transform, "Day and night are one key apart.", 40, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(1600, 60), Color.white);
        titleLevel = MakeText(titleRoot.transform, "", 44, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(1600, 70), Palette.Shard, FontStyle.Bold);
        titlePrompt = MakeText(titleRoot.transform, "Press ENTER or SPACE to play", 36, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -180), new Vector2(1600, 60), Color.white);
        credits = MakeText(titleRoot.transform, "Made for GitHub Game Off 2026  -  theme: SHIFT", 24, TextAnchor.LowerCenter, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1600, 40), new Color(1, 1, 1, 0.7f));

        // ---------- Message panel ----------
        msgRoot = Panel("Message", root);
        var backGo = new GameObject("Back", typeof(RectTransform), typeof(Image));
        backGo.transform.SetParent(msgRoot.transform, false);
        var brt = (RectTransform)backGo.transform;
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(1000, 520);
        msgBack = backGo.GetComponent<Image>();
        msgBack.color = new Color(0.05f, 0.04f, 0.1f, 0.78f);
        msgHeader = MakeText(msgRoot.transform, "", 84, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(1000, 110), Color.white, FontStyle.Bold);
        msgBody = MakeText(msgRoot.transform, "", 38, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(900, 220), Color.white);
        msgPrompt = MakeText(msgRoot.transform, "", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(900, 60), Palette.Shard);

        // ---------- Overlays ----------
        flash = FullImage("Flash", root, new Color(1, 1, 1, 0));
        fade = FullImage("Fade", root, new Color(0, 0, 0, 1));

        hudRoot.SetActive(false);
        msgRoot.SetActive(false);
    }

    GameObject Panel(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    Image FullImage(string name, Transform parent, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    Text MakeText(Transform parent, string s, int size, TextAnchor anchor, Vector2 anchorPoint, Vector2 pos, Vector2 sizeDelta, Color c, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchorPoint;
        rt.pivot = anchorPoint;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = c;
        t.fontStyle = style;
        t.text = s;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0, 0, 0, 0.55f);
        sh.effectDistance = new Vector2(3, -3);
        return t;
    }

    public void Flash(Color c, float a)
    {
        flashColor = c;
        flashA = Mathf.Max(flashA, a);
    }

    public void SetHint(string s)
    {
        hint.text = s;
        hintTimer = 0;
    }

    public void ShowMessage(string header, string body, string prompt)
    {
        msgRoot.SetActive(true);
        msgHeader.text = header;
        msgBody.text = body;
        msgPrompt.text = prompt;
    }

    public void HideMessage() => msgRoot.SetActive(false);

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        flashA = Mathf.MoveTowards(flashA, 0, dt * 2.2f);
        var fc = flashColor; fc.a = flashA;
        flash.color = fc;

        float b = PhaseState.Smooth;
        var pc = Palette.Current;
        phasePill.color = new Color(pc.r, pc.g, pc.b, 0.85f);
        phaseText.text = PhaseState.Current == Phase.Day ? "DAY" : "NIGHT";

        hintTimer += dt;
        var hc = hint.color;
        hc.a = Mathf.Lerp(0.95f, 0.55f, Mathf.Clamp01((hintTimer - 8f) / 2f));
        hint.color = hc;

        if (titleRoot.activeSelf)
        {
            title.color = Color.Lerp(new Color(1f, 0.85f, 0.55f), new Color(0.6f, 0.85f, 1f), b);
            var p = titlePrompt.color;
            p.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f);
            titlePrompt.color = p;
        }
        if (msgRoot.activeSelf)
        {
            var p = msgPrompt.color;
            p.a = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 3f);
            msgPrompt.color = p;
        }
    }
}
