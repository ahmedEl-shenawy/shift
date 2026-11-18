using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A vertical list of candy-style buttons driven by keyboard / gamepad (Up/Down, Left/Right, Confirm, Back) and by
/// the mouse (hover selects, click activates). Buttons bounce on a spring, wiggle their icon, squash when pressed
/// and slide in one after another when the menu opens. Everything animates on unscaled time (works while paused).
/// </summary>
public class Menu : MonoBehaviour
{
    public static bool GlobalLock;     // set while the screen is fading between scenes

    public readonly List<MenuButton> items = new List<MenuButton>();
    public int selected;
    public Action onBack;
    public bool blocked;               // e.g. while a confirm dialog sits on top
    public bool pointerOnly;           // HUD buttons: mouse only, never steal gameplay keys
    int openFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => GlobalLock = false;

    public void Open(int select = -1)
    {
        gameObject.SetActive(true);
        openFrame = Time.frameCount;
        blocked = false;
        selected = select >= 0 ? select : 0;
        if (!Visible(selected)) selected = Next(selected, 1);
        int k = 0;
        foreach (var b in items)
        {
            b.Snap(b.index == selected);
            if (b.gameObject.activeSelf) b.Intro(0.08f + 0.06f * k++);
        }
    }

    public void Close() => gameObject.SetActive(false);

    bool Visible(int i) => i >= 0 && i < items.Count && items[i].gameObject.activeSelf;

    int Next(int from, int dir)
    {
        for (int k = 1; k <= items.Count; k++)
        {
            int i = ((from + dir * k) % items.Count + items.Count) % items.Count;
            if (Visible(i)) return i;
        }
        return from;
    }

    public void Unhover(int i)
    {
        if (pointerOnly && selected == i) selected = -1;
    }

    public void Hover(int i)
    {
        if (blocked || GlobalLock || !Visible(i) || i == selected) return;
        selected = i;
        Blip(i);
    }

    public void Activate(int i)
    {
        if (blocked || GlobalLock || !Visible(i)) return;
        selected = i;
        var b = items[i];
        b.Press();
        if (AudioFx.I != null) AudioFx.I.Play(AudioFx.I.select, 0.75f, 0f);
        b.onClick?.Invoke();
    }

    public void Side(int i, int dir)
    {
        if (blocked || GlobalLock || !Visible(i)) return;
        selected = i;
        var b = items[i];
        var cb = dir < 0 ? b.onLeft : b.onRight;
        if (cb == null) return;
        cb();
        b.Nudge(dir);
        if (AudioFx.I != null) AudioFx.I.PlayPitch(AudioFx.I.pop, 0.5f, dir < 0 ? 0.9f : 1.15f);
    }

    void Blip(int i)
    {
        // each button sings its own note
        if (AudioFx.I != null) AudioFx.I.PlayPitch(AudioFx.I.pop, 0.45f, 1.25f - 0.1f * (i % 5));
    }

    void Update()
    {
        bool live = !pointerOnly && !blocked && !GlobalLock && Time.frameCount > openFrame;
        if (live && items.Count > 0)
        {
            if (GameInput.UpPressed) { selected = Next(selected, -1); Blip(selected); }
            else if (GameInput.DownPressed) { selected = Next(selected, 1); Blip(selected); }
            if (GameInput.LeftPressed) Side(selected, -1);
            else if (GameInput.RightPressed) Side(selected, 1);
            if (GameInput.ConfirmPressed) Activate(selected);
            else if (GameInput.BackPressed) onBack?.Invoke();
        }
        float dt = Time.unscaledDeltaTime;
        foreach (var b in items) b.Animate(b.index == selected && !blocked, dt);
    }

    // ------------------------------------------------------------------ construction

    public MenuButton AddButton(Transform parent, string label, Vector2 pos, Vector2 size, Sprite icon, Action onClick,
        bool primary = false, Color? tint = null)
    {
        var rt = UiKit.Rect("Button " + label, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        var b = rt.gameObject.AddComponent<MenuButton>();
        b.menu = this;
        b.index = items.Count;
        b.primary = primary;
        b.onClick = onClick;
        b.tint = tint ?? (primary ? UiKit.Sunny : UiKit.Sky);
        b.basePos = pos;
        b.group = rt.gameObject.AddComponent<CanvasGroup>();

        float corner = 1.15f;
        b.glow = UiKit.Img(rt, "Glow", UiKit.SoftGlow, Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size + new Vector2(120, 110));
        b.glow.preserveAspect = false;
        b.lip = UiKit.Img(rt, "Lip", UiKit.Rounded, UiKit.Darker(b.tint), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -7), size, corner);
        b.face = UiKit.Rect("Face", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        b.bg = b.face.gameObject.AddComponent<Image>();
        b.bg.sprite = UiKit.Rounded;
        b.bg.type = Image.Type.Sliced;
        b.bg.pixelsPerUnitMultiplier = corner;
        b.bg.raycastTarget = true;
        b.gloss = UiKit.Img(b.face, "Gloss", UiKit.Rounded, new Color(1, 1, 1, 0.26f), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -6), new Vector2(size.x - 26, size.y * 0.36f), 2.2f);
        b.outline = UiKit.Img(b.face, "Ring", UiKit.RoundedOutline, Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size + new Vector2(10, 10), corner * 0.9f);

        float textX = 0;
        if (icon != null)
        {
            float isz = size.y * 0.44f;
            var holder = UiKit.Rect("IconHolder", b.face, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(size.y * 0.62f, 2), new Vector2(isz, isz));
            var shadow = UiKit.Img(holder, "IconShadow", icon, new Color(0, 0, 0, 0.22f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(2, -3), new Vector2(isz, isz));
            b.icon = UiKit.Img(holder, "Icon", icon, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(isz, isz));
            b.iconHolder = holder;
            textX = size.y * 0.25f;
        }
        b.label = UiKit.Txt(b.face, label, Mathf.RoundToInt(size.y * 0.37f), Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(textX, 1), new Vector2(size.x - size.y, size.y), FontStyle.Bold, 0f);
        var o = b.label.gameObject.AddComponent<Outline>();
        o.effectColor = UiKit.Darker(b.tint, 0.38f);
        o.effectDistance = new Vector2(2.6f, -2.6f);
        var sh = b.label.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0, 0, 0, 0.18f);
        sh.effectDistance = new Vector2(0, -4);
        items.Add(b);
        return b;
    }

    /// <summary>A button with left/right arrows that cycles a value (e.g. level select).</summary>
    public MenuButton AddSelector(Transform parent, Vector2 pos, Vector2 size, Action onLeft, Action onRight, Action onClick, Color? tint = null)
    {
        var b = AddButton(parent, "", pos, size, null, onClick, false, tint);
        b.onLeft = onLeft;
        b.onRight = onRight;
        b.label.rectTransform.anchoredPosition = new Vector2(0, size.y * 0.15f);
        b.label.fontSize = Mathf.RoundToInt(size.y * 0.3f);
        b.sub = UiKit.Txt(b.face, "", Mathf.RoundToInt(size.y * 0.22f), Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -size.y * 0.2f), new Vector2(size.x - size.y * 1.6f, size.y * 0.4f), FontStyle.Bold, 0f);
        var o = b.sub.gameObject.AddComponent<Outline>();
        o.effectColor = UiKit.Darker(b.tint, 0.38f);
        o.effectDistance = new Vector2(1.8f, -1.8f);
        float a = size.y * 0.4f;
        b.left = UiKit.Img(b.face, "Left", UiKit.ChevronLeft, Color.white, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(size.y * 0.45f, 0), new Vector2(a, a), 1, true);
        b.right = UiKit.Img(b.face, "Right", UiKit.ChevronRight, Color.white, new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-size.y * 0.45f, 0), new Vector2(a, a), 1, true);
        var la = b.left.gameObject.AddComponent<MenuArrow>(); la.button = b; la.dir = -1;
        var ra = b.right.gameObject.AddComponent<MenuArrow>(); ra.button = b; ra.dir = 1;
        return b;
    }
}

public class MenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Menu menu;
    public int index;
    public bool primary;
    public Color tint;
    public Action onClick, onLeft, onRight;
    public Image bg, outline, icon, left, right, lip, gloss, glow;
    public RectTransform face, iconHolder;
    public Text label, sub;
    public CanvasGroup group;
    public Vector2 basePos;

    float sel, scale = 1f, scaleVel, press, nudge, intro = 1f, introDelay, wiggleT;

    public void OnPointerEnter(PointerEventData e) => menu.Hover(index);
    public void OnPointerExit(PointerEventData e) => menu.Unhover(index);
    public void OnPointerClick(PointerEventData e) => menu.Activate(index);

    public void Snap(bool selected) { sel = selected ? 1 : 0; scale = 1; scaleVel = 0; Animate(selected, 0); }
    public void Press() { press = 1; scaleVel -= 4f; }
    public void Nudge(int dir) => nudge = dir;
    public void Intro(float delay) { intro = 0; introDelay = delay; }

    public void Animate(bool selected, float dt)
    {
        float prev = sel;
        sel = Mathf.MoveTowards(sel, selected ? 1 : 0, dt * 8f);
        if (selected && prev < 0.05f && dt > 0) scaleVel += 3.5f;   // bounce when picked
        press = Mathf.MoveTowards(press, 0, dt * 5f);
        nudge = Mathf.MoveTowards(nudge, 0, dt * 6f);
        wiggleT += dt;

        // spring scale with overshoot
        float target = 1f + 0.07f * sel + (primary ? 0.012f * Mathf.Sin(Time.unscaledTime * 3f) : 0f);
        scaleVel += ((target - scale) * 320f - scaleVel * 16f) * dt;
        scale += scaleVel * dt;

        // staggered slide-in
        float ip = 1f;
        if (intro < 1f)
        {
            if (introDelay > 0) { introDelay -= dt; ip = 0; }
            else { intro = Mathf.Min(1f, intro + dt / 0.38f); ip = intro; }
        }
        float ie = UiKit.EaseOutBack(ip);
        group.alpha = Mathf.Clamp01(ip * 2.5f);
        ((RectTransform)transform).anchoredPosition = basePos + new Vector2(0, (1 - ie) * -70f);

        float e = sel * sel * (3 - 2 * sel);
        bg.color = Color.Lerp(Color.Lerp(tint, UiKit.Darker(tint, 0.92f), 0.4f), UiKit.Lighter(tint, 0.12f), e);
        lip.color = UiKit.Darker(tint, 0.68f);
        outline.color = new Color(1, 1, 1, e);
        glow.color = new Color(tint.r, tint.g, tint.b, 0.5f * e * (0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 5f)));
        if (sub) sub.color = new Color(1, 1, 1, 0.95f);
        if (left) left.color = new Color(1, 1, 1, onLeft != null ? 1 : 0.3f);
        if (right) right.color = new Color(1, 1, 1, onRight != null ? 1 : 0.3f);

        // press squashes the face onto its lip
        float pressCurve = Mathf.Sin(press * Mathf.PI);
        face.anchoredPosition = new Vector2(0, -5f * pressCurve);
        if (iconHolder) iconHolder.localRotation = Quaternion.Euler(0, 0, e * Mathf.Sin(wiggleT * 9f) * 12f);
        if (sub) label.rectTransform.anchoredPosition = new Vector2(nudge * 12f, label.rectTransform.anchoredPosition.y);

        transform.localScale = new Vector3(scale * (1 + 0.04f * pressCurve), scale * (1 - 0.06f * pressCurve), 1);
    }
}

public class MenuArrow : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
{
    public MenuButton button;
    public int dir;
    public void OnPointerEnter(PointerEventData e) => button.menu.Hover(button.index);
    public void OnPointerClick(PointerEventData e) => button.menu.Side(button.index, dir);
}
