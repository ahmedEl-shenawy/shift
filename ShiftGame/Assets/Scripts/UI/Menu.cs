using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A vertical list of buttons driven by keyboard / gamepad (Up/Down, Left/Right, Confirm, Back) and by the mouse
/// (hover selects, click activates). Visual state animates with unscaled time so it works while paused.
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
        foreach (var b in items) b.Snap(b.index == selected);
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

    void Awake() { if (pointerOnly) selected = -1; }

    public void Unhover(int i)
    {
        if (pointerOnly && selected == i) selected = -1;
    }

    public void Hover(int i)
    {
        if (blocked || GlobalLock || !Visible(i) || i == selected) return;
        selected = i;
        Tick(0.5f);
    }

    public void Activate(int i)
    {
        if (blocked || GlobalLock || !Visible(i)) return;
        selected = i;
        var b = items[i];
        b.Punch();
        if (AudioFx.I != null) AudioFx.I.Play(AudioFx.I.select, 0.7f, 0f);
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
        if (AudioFx.I != null) AudioFx.I.Play(AudioFx.I.select, 0.5f, 0.05f);
    }

    void Tick(float vol)
    {
        if (AudioFx.I != null) AudioFx.I.Play(AudioFx.I.select, 0.25f * vol, 0.1f);
    }

    void Update()
    {
        bool live = !pointerOnly && !blocked && !GlobalLock && Time.frameCount > openFrame;
        if (live && items.Count > 0)
        {
            if (GameInput.UpPressed) { selected = Next(selected, -1); Tick(1); }
            else if (GameInput.DownPressed) { selected = Next(selected, 1); Tick(1); }
            if (GameInput.LeftPressed) Side(selected, -1);
            else if (GameInput.RightPressed) Side(selected, 1);
            if (GameInput.ConfirmPressed) Activate(selected);
            else if (GameInput.BackPressed) onBack?.Invoke();
        }
        float dt = Time.unscaledDeltaTime;
        foreach (var b in items) b.Animate(b.index == selected && !blocked, dt);
    }

    // ------------------------------------------------------------------ construction

    public MenuButton AddButton(Transform parent, string label, Vector2 pos, Vector2 size, Sprite icon, Action onClick, bool primary = false)
    {
        var rt = UiKit.Rect("Button " + label, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        var b = rt.gameObject.AddComponent<MenuButton>();
        b.menu = this;
        b.index = items.Count;
        b.primary = primary;
        b.onClick = onClick;

        b.bg = rt.gameObject.AddComponent<Image>();
        b.bg.sprite = UiKit.Rounded;
        b.bg.type = Image.Type.Sliced;
        b.bg.pixelsPerUnitMultiplier = 1.25f;
        b.bg.raycastTarget = true;

        b.outline = UiKit.Img(rt, "Outline", UiKit.RoundedOutline, new Color(1, 1, 1, 0.2f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, 1.25f);

        float textX = 0;
        if (icon != null)
        {
            float isz = size.y * 0.42f;
            b.icon = UiKit.Img(rt, "Icon", icon, Color.white, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(size.y * 0.62f, 0), new Vector2(isz, isz));
            textX = size.y * 0.25f;
        }
        b.label = UiKit.Txt(rt, label, Mathf.RoundToInt(size.y * 0.36f), Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(textX, 0), new Vector2(size.x - size.y, size.y), FontStyle.Bold, 0f);
        items.Add(b);
        return b;
    }

    /// <summary>A button with left/right arrows that cycles a value (e.g. level select).</summary>
    public MenuButton AddSelector(Transform parent, Vector2 pos, Vector2 size, Action onLeft, Action onRight, Action onClick)
    {
        var b = AddButton(parent, "", pos, size, null, onClick);
        b.onLeft = onLeft;
        b.onRight = onRight;
        b.label.rectTransform.anchoredPosition = new Vector2(0, size.y * 0.15f);
        b.label.fontSize = Mathf.RoundToInt(size.y * 0.3f);
        b.sub = UiKit.Txt(b.transform, "", Mathf.RoundToInt(size.y * 0.24f), Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -size.y * 0.2f), new Vector2(size.x - size.y * 1.6f, size.y * 0.4f), FontStyle.Normal, 0f);
        float a = size.y * 0.38f;
        b.left = UiKit.Img(b.transform, "Left", UiKit.ChevronLeft, Color.white, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(size.y * 0.45f, 0), new Vector2(a, a), 1, true);
        b.right = UiKit.Img(b.transform, "Right", UiKit.ChevronRight, Color.white, new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-size.y * 0.45f, 0), new Vector2(a, a), 1, true);
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
    public Action onClick, onLeft, onRight;
    public Image bg, outline, icon, left, right;
    public Text label, sub;

    float sel, punch, nudge;

    public void OnPointerEnter(PointerEventData e) => menu.Hover(index);
    public void OnPointerExit(PointerEventData e) => menu.Unhover(index);
    public void OnPointerClick(PointerEventData e) => menu.Activate(index);

    public void Snap(bool selected) { sel = selected ? 1 : 0; Animate(selected, 0); }
    public void Punch() => punch = 1;
    public void Nudge(int dir) => nudge = dir;

    public void Animate(bool selected, float dt)
    {
        sel = Mathf.MoveTowards(sel, selected ? 1 : 0, dt * 9f);
        punch = Mathf.MoveTowards(punch, 0, dt * 5f);
        nudge = Mathf.MoveTowards(nudge, 0, dt * 6f);
        float e = sel * sel * (3 - 2 * sel);

        var accent = UiKit.Accent;
        var idle = primary ? new Color(0.10f, 0.08f, 0.20f, 0.78f) : new Color(0.06f, 0.05f, 0.13f, 0.66f);
        var on = new Color(accent.r, accent.g, accent.b, 0.97f);
        bg.color = Color.Lerp(idle, on, e);
        outline.color = new Color(1, 1, 1, Mathf.Lerp(primary ? 0.35f : 0.2f, 0f, e));
        var fg = Color.Lerp(Color.white, UiKit.Ink, e);
        label.color = fg;
        if (icon) icon.color = fg;
        if (sub) sub.color = new Color(fg.r, fg.g, fg.b, 0.8f);
        if (left) left.color = new Color(fg.r, fg.g, fg.b, onLeft != null ? 1 : 0.25f);
        if (right) right.color = new Color(fg.r, fg.g, fg.b, onRight != null ? 1 : 0.25f);
        if (sub) label.rectTransform.anchoredPosition = new Vector2(nudge * 10f, label.rectTransform.anchoredPosition.y);

        float s = 1f + 0.04f * e - 0.05f * Mathf.Sin(punch * Mathf.PI);
        transform.localScale = new Vector3(s, s, 1);
    }
}

public class MenuArrow : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
{
    public MenuButton button;
    public int dir;
    public void OnPointerEnter(PointerEventData e) => button.menu.Hover(button.index);
    public void OnPointerClick(PointerEventData e) => button.menu.Side(button.index, dir);
}
