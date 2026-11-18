using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "The Eclipse" - the boss that guards every 10th level.
/// It drifts over a ring arena whose quadrants alternate day and night, and attacks with:
///   - volleys of day/night projectiles,
///   - expanding shock-rings across the floor (shift, or jump over them),
///   - meteor rain: target circles appear, then day/night meteors smash down,
///   - a sweeping laser along the floor (shift, or jump over it),
///   - the Eclipse: it forcibly flips the world (deadly if you stand in the wrong quadrant).
/// Light cells appear on the outer platforms and on the ring; touching one fires a beam that damages it.
/// At half health it enrages: faster, denser attacks, and the laser/Eclipse unlock on every tier.
/// Every attack is telegraphed by the colour of its corona.
/// </summary>
public class BossController : MonoBehaviour
{
    public class Shot { public Transform tr; public Vector3 vel; public Phase phase; public float life; public Renderer rend; }
    public class Ring { public Transform tr; public float r, speed; public Phase phase; public Renderer rend; }
    public class Meteor { public Vector3 target; public float t, total; public Phase phase; public Transform marker, rock, disc; public Renderer markerRend, rockRend, discRend; }

    public GameManager gm;
    public BossInfo info;
    public int maxHp, hp;
    public bool defeated;
    public readonly List<Shot> shots = new List<Shot>();
    public readonly List<Ring> rings = new List<Ring>();
    public readonly List<Meteor> meteors = new List<Meteor>();
    public int cellSpot = -1;   // index into BossInfo.CellSpot
    public bool Raging => hp <= maxHp / 2;
    public bool EclipsePending => eclipseT > 0 || (windup > 0 && pending == Attack.Eclipse);
    public float FloorY => info.center.y;

    // laser state (read by the bot)
    public bool laserOn, laserLive;
    public float laserAngle, laserSpeed;
    public Phase laserPhase;

    enum Attack { None, Volley, Ring, Meteor, Laser, Eclipse }

    Transform visual, body, corona, sats, eye, laserPivot;
    Renderer laserRend;
    Material bodyMat, coronaMat, eyeMat, cellMat, beamMat, rageMat;
    Material shotDay, shotNight, shotDayGhost, shotNightGhost;
    Material ringDay, ringNight, ringDayGhost, ringNightGhost;
    Material discDay, discNight, discDayGhost, discNightGhost;
    Light glow;
    Transform cell, beam;
    int lastSpot = -1;
    float cellTimer = 1.5f;
    float laserT, laserDur;
    bool meteorHit;
    bool rageAnnounced;

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
        transform.position = Home(0);

        bodyMat = Mats.Lit(new Color(0.09f, 0.07f, 0.16f), 0f, 0.92f);
        rageMat = Mats.Lit(new Color(0.30f, 0.04f, 0.10f), 0.6f, 0.92f);
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
        discDay = Mats.Transparent(Palette.DayBlock, 0.42f, 1.6f);
        discNight = Mats.Transparent(Palette.NightBlock, 0.42f, 1.8f);
        discDayGhost = Mats.Transparent(Palette.DayBlock, 0.1f, 0.3f);
        discNightGhost = Mats.Transparent(Palette.NightBlock, 0.1f, 0.3f);

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        body = MeshFactory.Part("Body", visual, MeshFactory.Sphere(1.15f, 32), bodyMat, Vector3.zero).transform;
        var cor = MeshFactory.Part("Corona", visual, MeshFactory.Torus(1.7f, 0.12f, 72, 12), coronaMat, Vector3.zero, false);
        cor.transform.localRotation = Quaternion.Euler(90, 0, 0);
        corona = cor.transform;
        MeshFactory.Part("Corona2", corona, MeshFactory.Torus(2.0f, 0.04f, 72, 8), coronaMat, Vector3.zero, false);
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

        // the laser: a long bar pivoting around the arena centre, just above the floor
        laserPivot = new GameObject("Laser").transform;
        laserPivot.SetParent(gm.LevelRoot, false);
        laserPivot.position = info.center + Vector3.up * 0.32f;
        var bar = MeshFactory.Part("Beam", laserPivot, MeshFactory.RoundedBox(new Vector3(4.6f, 0.3f, 0.36f), 0.12f, 2), ringDayGhost, new Vector3(4.6f, 0, 0), false);
        laserRend = bar.GetComponent<Renderer>();
        laserPivot.gameObject.SetActive(false);

        tele = Random.value < 0.5f ? Phase.Day : Phase.Night;
    }

    Vector3 Home(float time) =>
        info.center + Vector3.up * 3.6f + new Vector3(Mathf.Cos(time * 0.45f), 0, Mathf.Sin(time * 0.45f)) * 1.1f;

    float Cooldown()
    {
        float frac = hp / (float)maxHp;
        float cd = Mathf.Lerp(1.5f, 2.8f, frac) - 0.15f * (Tier - 1);
        if (Raging) cd *= 0.8f;
        return Mathf.Max(0.85f, cd);
    }

    static Phase RandomPhase() => Random.value < 0.5f ? Phase.Day : Phase.Night;

    // ------------------------------------------------------------------ per-frame

    public void Tick(float dt, PlayerController player)
    {
        if (defeated || dt <= 0) return;
        float time = Time.time;
        Vector3 chest = player.transform.position + Vector3.up * 0.45f;

        // drift + face the player
        transform.position = Home(time) + Vector3.up * Mathf.Sin(time * 1.3f) * 0.25f;
        var to = player.transform.position - transform.position; to.y = 0;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1 - Mathf.Exp(-3f * dt));
        sats.Rotate(0, (Raging ? 160f : 70f) * dt, 0, Space.Self);
        corona.Rotate(0, 0, (windup > 0 || eclipseT > 0 ? 240f : Raging ? 120f : 45f) * dt, Space.Self);

        if (Raging && !rageAnnounced)
        {
            rageAnnounced = true;
            body.GetComponent<Renderer>().sharedMaterial = rageMat;
            gm.BossTelegraph("THE ECLIPSE IS ENRAGED!");
        }

        // light cells
        if (cell == null && beam == null)
        {
            cellTimer -= dt;
            if (cellTimer <= 0) SpawnCell();
        }
        if (cell != null)
        {
            var pc = info.CellSpot(cellSpot) + Vector3.up * (0.75f + Mathf.Sin(time * 2.5f) * 0.12f);
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
        else if (!laserOn)
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
        UpdateMeteors(dt, player);
        UpdateLaser(dt);

        // telegraph colours
        hurt = Mathf.MoveTowards(hurt, 0, dt * 2.5f);
        Color c;
        if (EclipsePending)
            c = Color.Lerp(new Color(0.8f, 0.4f, 1f), Color.white, 0.5f + 0.5f * Mathf.Sin(time * 20f));
        else c = Palette.PhaseColor(tele);
        if (Raging) c = Color.Lerp(c, new Color(1f, 0.25f, 0.35f), 0.25f + 0.15f * Mathf.Sin(time * 8f));
        c = Color.Lerp(c, Color.white, hurt);
        Mats.SetColor(coronaMat, c, windup > 0 ? 4.5f : 2.5f);
        Mats.SetColor(eyeMat, Color.Lerp(Color.white, c, 0.5f), 4f);
        glow.color = c;
        glow.intensity = windup > 0 ? 5f : 2.5f;
        visual.localScale = Vector3.one * (1f + hurt * 0.25f + (windup > 0 ? Mathf.Sin(time * 30f) * 0.03f : 0f));
    }

    void BeginAttack()
    {
        var options = new List<Attack> { Attack.Volley, Attack.Ring, Attack.Meteor, Attack.Volley, Attack.Meteor };
        if (Tier >= 2 || Raging) { options.Add(Attack.Laser); options.Add(Attack.Ring); }
        if ((Tier >= 2 || Raging) && lastAttack != Attack.Eclipse) options.Add(Attack.Eclipse);
        options.Remove(lastAttack);   // never the same attack twice in a row
        pending = options[Random.Range(0, options.Count)];
        tele = RandomPhase();
        windup = pending == Attack.Eclipse ? 0.4f : 0.75f;
        if (pending == Attack.Eclipse) gm.BossTelegraph("ECLIPSE INCOMING - GET TO A SAFE ZONE!");
    }

    void Execute(PlayerController player)
    {
        lastAttack = pending;
        switch (pending)
        {
            case Attack.Volley:
                volleyLeft = Mathf.Min(2 + Tier, 7) + (Raging ? 1 : 0);
                volleyT = 0;
                attackT = Cooldown();
                break;
            case Attack.Ring:
                SpawnRing(tele);
                if (Tier >= 2 || Raging)
                {
                    pendingRingPhase = tele == Phase.Day ? Phase.Night : Phase.Day;
                    pendingRingT = 0.9f;
                }
                attackT = Cooldown() + 0.6f;
                break;
            case Attack.Meteor:
                StartMeteors(player);
                attackT = Cooldown() + 0.8f;
                break;
            case Attack.Laser:
                StartLaser(player);
                attackT = Cooldown();
                break;
            case Attack.Eclipse:
                eclipseT = 1.3f;
                break;
        }
        pending = Attack.None;
    }

    // ------------------------------------------------------------------ projectiles & rings

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

    // ------------------------------------------------------------------ meteors

    void StartMeteors(PlayerController player)
    {
        int n = Mathf.Min(3 + Tier + (Raging ? 1 : 0), 8);
        float telegraph = Mathf.Max(0.85f, 1.35f - 0.1f * Tier);
        float playerAng = info.AngleOf(player.transform.position);
        for (int i = 0; i < n; i++)
        {
            Vector3 target;
            if (i == 0) target = new Vector3(player.transform.position.x, FloorY, player.transform.position.z);
            else
            {
                float a = (playerAng + Random.Range(-70f, 70f)) * Mathf.Deg2Rad;
                float r = Random.Range(3f, 5f);
                target = info.center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r;
            }
            var ph = i == 0 ? tele : RandomPhase();
            var marker = MeshFactory.Part("MeteorMark", gm.LevelRoot, MeshFactory.Torus(1f, 0.11f, 48, 8), ph == Phase.Day ? ringDay : ringNight,
                target + Vector3.up * 0.08f, false);
            marker.transform.localScale = new Vector3(0.2f, 1, 0.2f);
            var disc = MeshFactory.Part("MeteorDisc", gm.LevelRoot, MeshFactory.Sphere(1f, 24), ph == Phase.Day ? discDay : discNight,
                target + Vector3.up * 0.04f, false);
            disc.transform.localScale = new Vector3(0.05f, 0.03f, 0.05f);
            var rock = MeshFactory.Part("Meteor", gm.LevelRoot, MeshFactory.Octahedron(0.38f, 0.5f), ph == Phase.Day ? shotDay : shotNight,
                target + Vector3.up * 14f, false);
            meteors.Add(new Meteor
            {
                target = target, phase = ph, t = telegraph + i * 0.18f, total = telegraph + i * 0.18f,
                marker = marker.transform, rock = rock.transform, disc = disc.transform,
                markerRend = marker.GetComponent<Renderer>(), rockRend = rock.GetComponent<Renderer>(), discRend = disc.GetComponent<Renderer>()
            });
        }
        if (AudioFx.I != null) AudioFx.I.Play(AudioFx.I.whoosh, 0.6f, 0.05f);
    }

    const float MeteorFall = 0.35f, MeteorRadius = 0.95f;

    void UpdateMeteors(float dt, PlayerController player)
    {
        meteorHit = false;
        for (int i = meteors.Count - 1; i >= 0; i--)
        {
            var m = meteors[i];
            m.t -= dt;
            bool lethal = PhaseState.Current == m.phase;
            float k = 1f - Mathf.Clamp01(m.t / m.total);
            float rr = MeteorRadius * Mathf.Lerp(0.2f, 1f, Mathf.Min(1f, k * 1.6f)) * (1f + 0.06f * Mathf.Sin(Time.time * 18f));
            m.marker.localScale = new Vector3(rr, 1, rr);
            float dr = MeteorRadius * k;   // the filled disc grows until it fills the ring at impact
            m.disc.localScale = new Vector3(dr, 0.03f, dr);
            var dm = m.phase == Phase.Day ? (lethal ? discDay : discDayGhost) : (lethal ? discNight : discNightGhost);
            if (m.discRend.sharedMaterial != dm) m.discRend.sharedMaterial = dm;
            var mm = m.phase == Phase.Day ? (lethal ? ringDay : ringDayGhost) : (lethal ? ringNight : ringNightGhost);
            if (m.markerRend.sharedMaterial != mm) m.markerRend.sharedMaterial = mm;
            var rm = m.phase == Phase.Day ? (lethal ? shotDay : shotDayGhost) : (lethal ? shotNight : shotNightGhost);
            if (m.rockRend.sharedMaterial != rm) m.rockRend.sharedMaterial = rm;
            float fall = Mathf.Clamp01(m.t / MeteorFall);
            m.rock.position = m.target + Vector3.up * (0.4f + 14f * fall);
            m.rock.Rotate(200 * dt, 300 * dt, 0);
            if (m.t <= 0)
            {
                var pp = player.transform.position;
                float d = new Vector2(pp.x - m.target.x, pp.z - m.target.z).magnitude;
                if (PhaseState.Current == m.phase && d < MeteorRadius && pp.y - FloorY < 0.9f) meteorHit = true;
                gm.BossImpact(m.target, m.phase);
                Destroy(m.marker.gameObject);
                Destroy(m.rock.gameObject);
                Destroy(m.disc.gameObject);
                meteors.RemoveAt(i);
            }
        }
    }

    // ------------------------------------------------------------------ laser

    void StartLaser(PlayerController player)
    {
        laserPhase = tele;
        laserSpeed = (Random.value < 0.5f ? -1f : 1f) * Mathf.Min(55f + 12f * Tier + (Raging ? 15f : 0f), 110f);
        // start a quarter turn away from the player so there is time to react
        laserAngle = info.AngleOf(player.transform.position) - Mathf.Sign(laserSpeed) * 100f;
        laserDur = 360f / Mathf.Abs(laserSpeed) + 0.8f;
        laserT = 0;
        laserOn = true;
        laserLive = false;
        laserPivot.gameObject.SetActive(true);
        if (AudioFx.I != null) AudioFx.I.Play(AudioFx.I.bossRing, 0.6f, 0.2f);
    }

    void UpdateLaser(float dt)
    {
        if (!laserOn) return;
        laserT += dt;
        laserLive = laserT > 0.8f;                       // 0.8s warning (ghostly, harmless)
        if (laserLive) laserAngle += laserSpeed * dt;
        laserPivot.rotation = Quaternion.Euler(0, -laserAngle, 0);
        bool lethal = laserLive && PhaseState.Current == laserPhase;
        var mat = laserPhase == Phase.Day ? (lethal ? ringDay : ringDayGhost) : (lethal ? ringNight : ringNightGhost);
        if (laserRend.sharedMaterial != mat) laserRend.sharedMaterial = mat;
        float pulse = laserLive ? 1f : 0.5f + 0.5f * Mathf.Sin(laserT * 30f);
        laserRend.transform.localScale = new Vector3(1, pulse, pulse);
        if (laserT >= laserDur)
        {
            laserOn = laserLive = false;
            laserPivot.gameObject.SetActive(false);
        }
    }

    bool LaserHits(Vector3 pos)
    {
        if (!laserLive || PhaseState.Current != laserPhase) return false;
        if (pos.y - FloorY > 0.55f) return false;           // jumped over it
        float d = new Vector2(pos.x - info.center.x, pos.z - info.center.z).magnitude;
        if (d < 2.2f || d > 7f) return false;
        float delta = Mathf.Abs(Mathf.DeltaAngle(info.AngleOf(pos), laserAngle)) * Mathf.Deg2Rad;
        return delta * d < 0.42f;
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
        if (meteorHit) return true;
        return LaserHits(pos);
    }

    /// <summary>Called when the player respawns: clear everything in flight and give a short breather.</summary>
    public void ClearThreats()
    {
        foreach (var s in shots) if (s.tr) Destroy(s.tr.gameObject);
        foreach (var r in rings) if (r.tr) Destroy(r.tr.gameObject);
        foreach (var m in meteors) { if (m.marker) Destroy(m.marker.gameObject); if (m.rock) Destroy(m.rock.gameObject); if (m.disc) Destroy(m.disc.gameObject); }
        shots.Clear(); rings.Clear(); meteors.Clear();
        meteorHit = false;
        laserOn = laserLive = false;
        if (laserPivot) laserPivot.gameObject.SetActive(false);
        volleyLeft = 0; pendingRingT = -1; eclipseT = 0; windup = 0; pending = Attack.None;
        attackT = 2.5f;
    }

    // ------------------------------------------------------------------ light cells

    void SpawnCell()
    {
        int n = info.CellSpotCount;
        int idx;
        int guard = 0;
        do
        {
            // two thirds of the time on an outer platform, otherwise out on the ring
            idx = Random.value < 0.66f ? Random.Range(0, info.platforms.Count) : Random.Range(info.platforms.Count, n);
        } while (idx == lastSpot && guard++ < 20);
        lastSpot = cellSpot = idx;
        var go = MeshFactory.Part("LightCell", gm.LevelRoot, MeshFactory.Octahedron(0.32f, 0.5f), cellMat, info.CellSpot(idx) + Vector3.up * 0.75f, false);
        cell = go.transform;
        MeshFactory.Part("Beacon", cell, MeshFactory.RoundedBox(new Vector3(0.14f, 7f, 0.14f), 0.05f, 1), beamMat, new Vector3(0, 3.8f, 0), false);
        if (AudioFx.I != null) AudioFx.I.Play(AudioFx.I.portalOpen, 0.4f, 0f);
    }

    void CollectCell()
    {
        var from = cell.position;
        Destroy(cell.gameObject);
        cell = null;
        cellSpot = -1;
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
        cellTimer = Raging ? 0.8f : 1.2f;
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
