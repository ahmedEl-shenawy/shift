using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "The Eclipse" - the boss that guards every 10th level.
/// It floats over a ring arena and attacks with day/night projectiles, expanding shock-rings and the Eclipse
/// (a forced world shift). Light cells appear on the outer platforms; touching one fires a beam that damages it.
/// Every attack is telegraphed by the colour of its corona. Higher tiers: more HP, faster and denser attacks.
/// </summary>
public class BossController : MonoBehaviour
{
    public class Shot { public Transform tr; public Vector3 vel; public Phase phase; public float life; public Renderer rend; }
    public class Ring { public Transform tr; public float r, speed; public Phase phase; public Renderer rend; }

    public GameManager gm;
    public BossInfo info;
    public int maxHp, hp;
    public bool defeated;
    public readonly List<Shot> shots = new List<Shot>();
    public readonly List<Ring> rings = new List<Ring>();
    public int cellPlatform = -1;
    public bool EclipsePending => eclipseT > 0 || (windup > 0 && pending == Attack.Eclipse);
    public float FloorY => info.center.y;

    enum Attack { None, Volley, Ring, Eclipse }

    Transform visual, body, corona, sats, eye;
    Material bodyMat, coronaMat, eyeMat, cellMat, beamMat;
    Material shotDay, shotNight, shotDayGhost, shotNightGhost;
    Material ringDay, ringNight, ringDayGhost, ringNightGhost;
    Light glow;
    Transform cell, cellBeacon, beam;
    int lastPlatform = -1;
    float cellTimer = 1.5f;
    Vector3 home;

    float attackT = 3.5f, windup, stagger, eclipseT, hurt, volleyT, pendingRingT = -1;
    int volleyLeft;
    Attack pending = Attack.None, lastAttack = Attack.None;
    Phase tele, pendingRingPhase;

    int Tier => Mathf.Max(1, info.tier);

    public void Init(GameManager manager, BossInfo bossInfo)
    {
        gm = manager;
        info = bossInfo;
        maxHp = hp = Mathf.Min(3 + Tier, 8);
        home = info.center + Vector3.up * 3.6f;
        transform.position = home;

        bodyMat = Mats.Lit(new Color(0.09f, 0.07f, 0.16f), 0f, 0.92f);
        coronaMat = Mats.Lit(Palette.DayBlock, 3f, 0.9f);
        eyeMat = Mats.Lit(Color.white, 4f, 1f);
        cellMat = Mats.Lit(new Color(1f, 0.97f, 0.8f), 4f, 1f);
        beamMat = Mats.Transparent(new Color(1f, 0.95f, 0.7f), 0.35f, 2.5f);
        shotDay = Mats.Lit(Color.Lerp(Palette.DayBlock, Color.red, 0.2f), 3f, 0.8f);
        shotNight = Mats.Lit(Color.Lerp(Palette.NightBlock, new Color(0.5f, 0.2f, 1f), 0.3f), 3f, 0.8f);
        shotDayGhost = Mats.Transparent(Palette.DayBlock, 0.2f, 0.5f);
        shotNightGhost = Mats.Transparent(Palette.NightBlock, 0.2f, 0.5f);
        ringDay = Mats.Lit(Palette.DayBlock, 2.5f, 0.6f);
        ringNight = Mats.Lit(Palette.NightBlock, 2.5f, 0.6f);
        ringDayGhost = Mats.Transparent(Palette.DayBlock, 0.18f, 0.6f);
        ringNightGhost = Mats.Transparent(Palette.NightBlock, 0.18f, 0.6f);

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        body = MeshFactory.Part("Body", visual, MeshFactory.Sphere(1.15f, 32), bodyMat, Vector3.zero).transform;
        var cor = MeshFactory.Part("Corona", visual, MeshFactory.Torus(1.7f, 0.12f, 72, 12), coronaMat, Vector3.zero, false);
        cor.transform.localRotation = Quaternion.Euler(90, 0, 0);
        corona = cor.transform;
        var cor2 = MeshFactory.Part("Corona2", corona, MeshFactory.Torus(2.0f, 0.04f, 72, 8), coronaMat, Vector3.zero, false);
        cor2.transform.localRotation = Quaternion.Euler(0, 0, 0);
        eye = MeshFactory.Part("Eye", visual, MeshFactory.Sphere(0.3f, 16), eyeMat, new Vector3(0, 0.1f, 1.02f), false).transform;
        eye.localScale = new Vector3(1.3f, 0.7f, 0.5f);
        sats = new GameObject("Satellites").transform;
        sats.SetParent(visual, false);
        MeshFactory.Part("Sun", sats, MeshFactory.Sphere(0.3f, 16), Mats.Lit(Palette.DayBlock, 3f, 0.8f), new Vector3(2.6f, 0, 0), false);
        MeshFactory.Part("Moon", sats, MeshFactory.Sphere(0.26f, 16), Mats.Lit(Palette.NightBlock, 3f, 0.8f), new Vector3(-2.6f, 0, 0), false);

        var lg = new GameObject("Glow");
        lg.transform.SetParent(transform, false);
        glow = lg.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.range = 12f;
        glow.intensity = 3f;
        glow.shadows = LightShadows.None;

        tele = Random.value < 0.5f ? Phase.Day : Phase.Night;
    }

    float Cooldown()
    {
        float frac = hp / (float)maxHp;
        return Mathf.Max(0.9f, Mathf.Lerp(1.6f, 3.0f, frac) - 0.15f * (Tier - 1));
    }

    static Phase RandomPhase() => Random.value < 0.5f ? Phase.Day : Phase.Night;

    // ------------------------------------------------------------------ per-frame

    public void Tick(float dt, PlayerController player)
    {
        if (defeated || dt <= 0) return;
        float time = Time.time;
        Vector3 chest = player.transform.position + Vector3.up * 0.45f;

        // hover + face the player
        transform.position = home + Vector3.up * Mathf.Sin(time * 1.3f) * 0.25f;
        var to = player.transform.position - transform.position; to.y = 0;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1 - Mathf.Exp(-3f * dt));
        sats.Rotate(0, 70f * dt, 0, Space.Self);
        corona.Rotate(0, 0, (windup > 0 || eclipseT > 0 ? 240f : 45f) * dt, Space.Self);

        // light cells
        if (cell == null && beam == null)
        {
            cellTimer -= dt;
            if (cellTimer <= 0) SpawnCell();
        }
        if (cell != null)
        {
            var pc = info.platforms[cellPlatform].center + Vector3.up * (0.75f + Mathf.Sin(time * 2.5f) * 0.12f);
            cell.position = pc;
            cell.Rotate(0, 140f * dt, 0, Space.World);
            if ((chest - pc).sqrMagnitude < 0.9f * 0.9f) CollectCell();
        }
        if (beam != null)
        {
            beam.position = Vector3.MoveTowards(beam.position, transform.position, 18f * dt);
            if ((beam.position - transform.position).sqrMagnitude < 0.5f) { Destroy(beam.gameObject); beam = null; Damage(); if (defeated) return; }
        }

        // attacks
        if (stagger > 0) stagger -= dt;
        else if (eclipseT > 0)
        {
            eclipseT -= dt;
            if (eclipseT <= 0) { gm.BossEclipse(); attackT = Cooldown(); }
        }
        else if (windup > 0)
        {
            windup -= dt;
            if (windup <= 0) Execute(player);
        }
        else if (volleyLeft > 0)
        {
            volleyT -= dt;
            if (volleyT <= 0) { FireShot(player, volleyLeft % 2 == 0 ? tele : RandomPhase()); volleyLeft--; volleyT = Mathf.Max(0.18f, 0.32f - 0.03f * Tier); }
        }
        else
        {
            attackT -= dt;
            if (attackT <= 0) BeginAttack();
        }
        if (pendingRingT > 0)
        {
            pendingRingT -= dt;
            if (pendingRingT <= 0) SpawnRing(pendingRingPhase);
        }

        UpdateShots(dt);
        UpdateRings(dt);

        // telegraph colours
        hurt = Mathf.MoveTowards(hurt, 0, dt * 2.5f);
        Color c;
        if (eclipseT > 0 || (windup > 0 && pending == Attack.Eclipse))
            c = Color.Lerp(new Color(0.8f, 0.4f, 1f), Color.white, 0.5f + 0.5f * Mathf.Sin(time * 20f));
        else c = Palette.PhaseColor(tele);
        c = Color.Lerp(c, Color.white, hurt);
        float boost = windup > 0 ? 4.5f : 2.5f;
        Mats.SetColor(coronaMat, c, boost);
        Mats.SetColor(eyeMat, Color.Lerp(Color.white, c, 0.5f), 4f);
        glow.color = c;
        glow.intensity = windup > 0 ? 5f : 2.5f;
        float punch = 1f + hurt * 0.25f;
        visual.localScale = Vector3.one * punch;
    }

    void BeginAttack()
    {
        var options = new List<Attack> { Attack.Volley, Attack.Ring, Attack.Volley, Attack.Ring };
        if ((Tier >= 2 || hp <= maxHp / 2) && lastAttack != Attack.Eclipse) options.Add(Attack.Eclipse);
        pending = options[Random.Range(0, options.Count)];
        tele = RandomPhase();
        windup = pending == Attack.Eclipse ? 0.4f : 0.75f;
        if (pending == Attack.Eclipse) gm.BossTelegraph("ECLIPSE INCOMING");
    }

    void Execute(PlayerController player)
    {
        lastAttack = pending;
        switch (pending)
        {
            case Attack.Volley:
                volleyLeft = Mathf.Min(2 + Tier, 7) + (hp <= maxHp / 2 ? 1 : 0);
                volleyT = 0;
                attackT = Cooldown();
                break;
            case Attack.Ring:
                SpawnRing(tele);
                if (Tier >= 2 || hp <= maxHp / 2)
                {
                    pendingRingPhase = tele == Phase.Day ? Phase.Night : Phase.Day;
                    pendingRingT = 0.9f;
                }
                attackT = Cooldown() + 0.6f;
                break;
            case Attack.Eclipse:
                eclipseT = 1.3f;
                break;
        }
        pending = Attack.None;
    }

    void FireShot(PlayerController player, Phase ph)
    {
        var from = transform.position + transform.forward * 1.3f;
        var target = player.transform.position + Vector3.up * 0.45f + player.Velocity * 0.2f;
        target += new Vector3(Random.Range(-0.6f, 0.6f), 0, Random.Range(-0.6f, 0.6f));
        float speed = Mathf.Min(4.5f + 0.5f * Tier, 8f);
        var go = MeshFactory.Part("Shot", gm.LevelRoot, MeshFactory.Sphere(0.26f, 14), ph == Phase.Day ? shotDay : shotNight, from, false);
        MeshFactory.Part("Ring", go.transform, MeshFactory.Torus(0.36f, 0.03f, 24, 6), ph == Phase.Day ? shotDay : shotNight, Vector3.zero, false)
            .transform.localRotation = Quaternion.Euler(Random.Range(0, 90), 0, 0);
        shots.Add(new Shot { tr = go.transform, vel = (target - from).normalized * speed, phase = ph, life = 6f, rend = go.GetComponent<Renderer>() });
        if (AudioFx.I != null) AudioFx.I.Play(AudioFx.I.bossShot, 0.45f, 0.1f);
    }

    void SpawnRing(Phase ph)
    {
        var go = MeshFactory.Part("ShockRing", gm.LevelRoot, MeshFactory.Torus(1f, 0.07f, 96, 8), ph == Phase.Day ? ringDay : ringNight,
            info.center + Vector3.up * 0.3f, false);
        rings.Add(new Ring { tr = go.transform, r = 2.3f, speed = Mathf.Min(3.0f + 0.35f * Tier, 5.5f), phase = ph, rend = go.GetComponent<Renderer>() });
        if (AudioFx.I != null) AudioFx.I.Play(AudioFx.I.bossRing, 0.7f, 0.05f);
    }

    void UpdateShots(float dt)
    {
        for (int i = shots.Count - 1; i >= 0; i--)
        {
            var s = shots[i];
            s.tr.position += s.vel * dt;
            s.tr.Rotate(0, 300 * dt, 0);
            s.life -= dt;
            bool lethal = PhaseState.Current == s.phase;
            var mat = s.phase == Phase.Day ? (lethal ? shotDay : shotDayGhost) : (lethal ? shotNight : shotNightGhost);
            if (s.rend.sharedMaterial != mat) s.rend.sharedMaterial = mat;
            if (s.life <= 0 || s.tr.position.y < FloorY - 3f) { Destroy(s.tr.gameObject); shots.RemoveAt(i); }
        }
    }

    void UpdateRings(float dt)
    {
        for (int i = rings.Count - 1; i >= 0; i--)
        {
            var r = rings[i];
            r.r += r.speed * dt;
            r.tr.localScale = new Vector3(r.r, 1.6f, r.r);
            bool lethal = PhaseState.Current == r.phase;
            var mat = r.phase == Phase.Day ? (lethal ? ringDay : ringDayGhost) : (lethal ? ringNight : ringNightGhost);
            if (r.rend.sharedMaterial != mat) r.rend.sharedMaterial = mat;
            if (r.r > 12f) { Destroy(r.tr.gameObject); rings.RemoveAt(i); }
        }
    }

    // ------------------------------------------------------------------ player interaction

    public bool HitsPlayer(PlayerController p)
    {
        if (defeated) return false;
        var pos = p.transform.position;
        var chest = pos + Vector3.up * 0.45f;
        foreach (var s in shots)
            if (s.phase == PhaseState.Current && (s.tr.position - chest).sqrMagnitude < 0.55f * 0.55f) return true;
        float d = new Vector2(pos.x - info.center.x, pos.z - info.center.z).magnitude;
        foreach (var r in rings)
            if (r.phase == PhaseState.Current && Mathf.Abs(d - r.r) < 0.38f && pos.y - FloorY < 0.55f) return true;
        return false;
    }

    /// <summary>Called when the player respawns: clear everything in flight and give a short breather.</summary>
    public void ClearThreats()
    {
        foreach (var s in shots) if (s.tr) Destroy(s.tr.gameObject);
        foreach (var r in rings) if (r.tr) Destroy(r.tr.gameObject);
        shots.Clear(); rings.Clear();
        volleyLeft = 0; pendingRingT = -1; eclipseT = 0; windup = 0; pending = Attack.None;
        attackT = 2.5f;
    }

    void SpawnCell()
    {
        int n = info.platforms.Count;
        int idx;
        do { idx = Random.Range(0, n); } while (idx == lastPlatform && n > 1);
        lastPlatform = cellPlatform = idx;
        var p = info.platforms[idx];
        var go = MeshFactory.Part("LightCell", gm.LevelRoot, MeshFactory.Octahedron(0.32f, 0.5f), cellMat, p.center + Vector3.up * 0.75f, false);
        cell = go.transform;
        var beacon = MeshFactory.Part("Beacon", cell, MeshFactory.RoundedBox(new Vector3(0.14f, 7f, 0.14f), 0.05f, 1), beamMat, new Vector3(0, 3.8f, 0), false);
        cellBeacon = beacon.transform;
        if (AudioFx.I != null) AudioFx.I.Play(AudioFx.I.portalOpen, 0.4f, 0f);
    }

    void CollectCell()
    {
        var from = cell.position;
        Destroy(cell.gameObject);
        cell = null; cellBeacon = null;
        cellPlatform = -1;
        var go = MeshFactory.Part("Beam", gm.LevelRoot, MeshFactory.Sphere(0.35f, 16), cellMat, from, false);
        beam = go.transform;
        gm.BossCellCollected(from);
    }

    void Damage()
    {
        hp = Mathf.Max(0, hp - 1);
        hurt = 1f;
        stagger = 1.4f;
        windup = 0; pending = Attack.None; volleyLeft = 0;
        cellTimer = 1.2f;
        gm.OnBossHit(hp, maxHp, transform.position);
        if (hp <= 0) Defeat();
    }

    void Defeat()
    {
        defeated = true;
        ClearThreats();
        if (cell != null) Destroy(cell.gameObject);
        gm.OnBossDefeated(transform.position);
        visual.gameObject.SetActive(false);
        glow.enabled = false;
    }
}
