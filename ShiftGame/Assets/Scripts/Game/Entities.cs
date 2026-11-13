using UnityEngine;

/// <summary>A level block. Neutral blocks are always solid; phase blocks only in their phase.</summary>
public class Block : MonoBehaviour
{
    public bool hasPhase;
    public Phase phase;
    public bool tall;
    public Bounds bounds;
    public Transform visual;
    public Renderer rend;
    public Collider col;
    public Material solid, ghost, faded;
    public bool occluding;

    float anim = 1f, delay;
    bool targetSolid = true;
    float deny;

    public bool SolidNow => !hasPhase || PhaseState.Current == phase;

    public void Apply(bool instant, Vector3 origin)
    {
        if (!hasPhase) return;
        bool s = PhaseState.Current == phase;
        col.enabled = s;
        targetSolid = s;
        if (instant)
        {
            anim = 1; delay = 0;
            visual.localScale = s ? Vector3.one : Vector3.one * 0.9f;
        }
        else
        {
            anim = 0;
            delay = Vector3.Distance(origin, transform.position) * 0.022f;
        }
    }

    public void Deny() => deny = 1f;

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1;
        return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        bool showSolid = targetSolid;
        if (anim < 1f)
        {
            if (delay > 0) { delay -= dt; showSolid = !targetSolid; }
            else
            {
                anim = Mathf.Min(1f, anim + dt / 0.35f);
                float s = targetSolid
                    ? Mathf.LerpUnclamped(0.8f, 1f, EaseOutBack(anim))
                    : Mathf.Lerp(1.06f, 0.9f, 1 - (1 - anim) * (1 - anim));
                visual.localScale = Vector3.one * s;
            }
        }

        if (deny > 0)
        {
            deny = Mathf.Max(0, deny - dt * 2.5f);
            float w = Mathf.Sin(Time.time * 50f) * 0.08f * deny;
            visual.localScale = Vector3.one * (0.9f + w);
        }

        Material want = showSolid ? (occluding && faded != null ? faded : solid) : ghost;
        if (want != null && rend.sharedMaterial != want)
        {
            rend.sharedMaterial = want;
            rend.shadowCastingMode = want == solid
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}

/// <summary>Floating hazard that patrols a straight line on the grid. Lethal only in its own phase.</summary>
public class Orb : MonoBehaviour
{
    public Phase phase;
    [System.NonSerialized] public LevelGrid grid;
    public int layer;
    public Vector2Int cell, dir;
    public float standY;
    public float speed = 2.6f;
    public Renderer rend, ringRend;
    public Transform core, ring;
    public Material active, ghost;

    Vector3 target;
    public bool Lethal => PhaseState.Current == phase;

    public Vector3 CellPos(Vector2Int c) => new Vector3(c.x, standY + 0.55f, c.y);

    public void Begin()
    {
        target = CellPos(cell);
        transform.position = target;
    }

    bool CanEnter(Vector2Int c)
    {
        if (c.x < 0 || c.y < 0 || c.x >= grid.width || c.y >= grid.depth) return false;
        if (grid.Occupied(layer, c.x, c.y)) return false;
        return grid.Occupied(layer - 1, c.x, c.y);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * dt);
        if ((transform.position - target).sqrMagnitude < 1e-6f)
        {
            if (CanEnter(cell + dir)) cell += dir;
            else
            {
                dir = -dir;
                if (CanEnter(cell + dir)) cell += dir;
            }
            target = CellPos(cell);
        }

        bool lethal = Lethal;
        var want = lethal ? active : ghost;
        if (rend.sharedMaterial != want) { rend.sharedMaterial = want; ringRend.sharedMaterial = want; }
        float pulse = lethal ? 1f + Mathf.Sin(Time.time * 9f) * 0.08f : 0.75f;
        core.localScale = Vector3.Lerp(core.localScale, Vector3.one * pulse, dt * 12f);
        core.localPosition = new Vector3(0, Mathf.Sin(Time.time * 3f + cell.x) * 0.08f, 0);
        ring.Rotate(0, (lethal ? 240f : 60f) * dt, 0, Space.Self);
    }
}

public class Shard : MonoBehaviour
{
    public bool collected;
    Vector3 basePos;
    float seed;

    void Start()
    {
        basePos = transform.position;
        seed = Random.value * 10f;
    }

    void Update()
    {
        if (collected) return;
        transform.position = basePos + Vector3.up * (0.55f + Mathf.Sin(Time.time * 2.2f + seed) * 0.12f);
        transform.Rotate(0, 120f * Time.deltaTime, 0, Space.World);
    }
}

public class Portal : MonoBehaviour
{
    public Transform ring, ring2, core;
    public Renderer ringRend, ring2Rend, coreRend;
    public Material lockedMat, openMat, coreLocked, coreOpen;
    public bool open;
    float openAnim;

    public void SetOpen(bool o, bool instant)
    {
        open = o;
        if (instant) openAnim = o ? 1 : 0;
        ringRend.sharedMaterial = o ? openMat : lockedMat;
        ring2Rend.sharedMaterial = o ? openMat : lockedMat;
        coreRend.sharedMaterial = o ? coreOpen : coreLocked;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        openAnim = Mathf.MoveTowards(openAnim, open ? 1 : 0, dt * 2f);
        float spin = Mathf.Lerp(20f, 160f, openAnim);
        ring.Rotate(Vector3.up, spin * dt, Space.World);
        ring2.Rotate(Vector3.up, -spin * 1.3f * dt, Space.World);
        float s = Mathf.Lerp(0.25f, 0.55f, openAnim) * (1f + Mathf.Sin(Time.time * 4f) * 0.08f * openAnim);
        core.localScale = Vector3.one * s;
    }
}

/// <summary>Simple self-destroying debris chunk.</summary>
public class Debris : MonoBehaviour
{
    public Vector3 velocity;
    public Vector3 spin;
    public float life = 1f;
    float t;
    Vector3 startScale;

    void Start() => startScale = transform.localScale;

    void Update()
    {
        float dt = Time.deltaTime;
        t += dt;
        velocity += Vector3.down * 18f * dt;
        transform.position += velocity * dt;
        transform.Rotate(spin * dt);
        float k = Mathf.Clamp01(1 - t / life);
        transform.localScale = startScale * k;
        if (t >= life) Destroy(gameObject);
    }
}

/// <summary>Expanding ring fired when the world shifts.</summary>
public class Shockwave : MonoBehaviour
{
    public Material mat;
    public float duration = 0.7f, maxScale = 14f;
    public Color color;
    float t;

    void Update()
    {
        t += Time.deltaTime / duration;
        float e = 1 - Mathf.Pow(1 - Mathf.Clamp01(t), 3);
        float s = Mathf.Lerp(0.3f, maxScale, e);
        transform.localScale = new Vector3(s, 1, s);
        var c = color;
        c.a = (1 - Mathf.Clamp01(t)) * 0.7f;
        mat.SetColor("_BaseColor", c);
        mat.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * 2f * (1 - t));
        if (t >= 1) { Destroy(mat); Destroy(gameObject); }
    }
}
