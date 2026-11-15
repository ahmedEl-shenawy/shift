using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Scripted playtest bot. Drives the player through simulated input in Play Mode.</summary>
public static class Bot
{
    static StringBuilder log;
    static GameManager G => GameManager.I;
    static Vector3 P => G.player.transform.position;
    static bool Playing => G.state == GameManager.State.Playing;
    static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    static async Task Frame() => await Task.Yield();

    static async Task Wait(float s)
    {
        float t = Time.time + s;
        while (Time.time < t) await Frame();
    }

    static async Task<bool> Go(float x, float z, float timeout = 8f)
    {
        float end = Time.time + timeout;
        while (Time.time < end)
        {
            if (!Playing) { GameInput.SimWorldMove = Vector3.zero; return G.state == GameManager.State.Complete; }
            var d = new Vector3(x - P.x, 0, z - P.z);
            if (d.magnitude < 0.06f) { GameInput.SimWorldMove = Vector3.zero; return true; }
            GameInput.SimWorldMove = d.normalized * Mathf.Clamp01(d.magnitude * 2.5f + 0.12f);
            await Frame();
        }
        GameInput.SimWorldMove = Vector3.zero;
        log.AppendLine($"  timeout going to {x},{z} (at {P:F2})");
        return false;
    }

    static async Task<bool> Jump(float x, float z, float shiftDelay)
    {
        GameInput.SimJumpHeld = true;
        GameInput.SimPressJump();
        float t0 = Time.time;
        bool shifted = shiftDelay < 0;
        await Frame();
        while (Time.time - t0 < 3f)
        {
            if (!Playing) break;
            float el = Time.time - t0;
            if (!shifted && el >= shiftDelay) { GameInput.SimPressShift(); shifted = true; }
            var d = new Vector3(x - P.x, 0, z - P.z);
            GameInput.SimWorldMove = d.magnitude < 0.05f ? Vector3.zero : d.normalized * Mathf.Clamp01(d.magnitude * 2.5f + 0.3f);
            if (G.player.Velocity.y < 0) GameInput.SimJumpHeld = false;
            if (el > 0.2f && G.player.Grounded && shifted) break;
            await Frame();
        }
        GameInput.SimJumpHeld = false;
        return await Go(x, z, 3f);
    }

    // ---------------------------------------------------------------- orb timing on rotated sections

    static bool OnLane(Orb o, char ax, float line, char phase)
    {
        if (phase == 'D' && o.phase != Phase.Day) return false;
        if (phase == 'N' && o.phase != Phase.Night) return false;
        var pos = o.transform.position;
        if (ax == 'x') return o.dir.x != 0 && Mathf.Abs(pos.z - line) < 0.4f;
        return o.dir.y != 0 && Mathf.Abs(pos.x - line) < 0.4f;
    }

    static async Task<bool> OrbFar2(char ax, float line, float gpos, float dist, char phase)
    {
        var lane = new List<Orb>();
        foreach (var o in G.DebugOrbs) if (OnLane(o, ax, line, phase)) lane.Add(o);
        if (lane.Count == 0) { log.AppendLine($"  no orb on lane {ax} {line} {phase}"); return false; }
        float end = Time.time + 25f;
        while (Time.time < end)
        {
            if (!Playing) return false;
            bool far = true;
            foreach (var o in lane)
            {
                float along = ax == 'x' ? o.transform.position.x : o.transform.position.z;
                if (Mathf.Abs(along - gpos) < dist) { far = false; break; }
            }
            if (far) return true;
            await Frame();
        }
        log.AppendLine("  timeout waiting for orb (orbfar2)");
        return false;
    }

    // ---------------------------------------------------------------- boss fight AI

    struct WP { public Vector3 pos; public bool needPhase; public Phase phase; public bool bridgeEntry; }

    static WP Pt(Vector3 v) => new WP { pos = v };

    static Vector3 Ring(BossInfo info, float deg) =>
        info.center + new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0, Mathf.Sin(deg * Mathf.Deg2Rad)) * info.ringRadius;

    static float AngleOf(BossInfo info, Vector3 p) => Mathf.Atan2(p.z - info.center.z, p.x - info.center.x) * Mathf.Rad2Deg;

    static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

    static int PlatformAt(BossInfo info, Vector3 p)
    {
        for (int i = 0; i < info.platforms.Count; i++)
        {
            var pl = info.platforms[i];
            if (Flat(p, pl.center) < 1.9f) return i;
            foreach (var c in pl.bridge) if (Flat(p, c) < 0.7f) return i;
        }
        return -1;
    }

    static bool OnBridge(BossInfo info, Vector3 p)
    {
        foreach (var pl in info.platforms)
            foreach (var c in pl.bridge)
                if (Flat(p, c) < 0.62f) return true;
        return false;
    }

    static List<WP> Plan(BossInfo info, Vector3 p, int target, bool defeated)
    {
        var list = new List<WP>();
        int here = PlatformAt(info, p);
        float start;
        if (here >= 0 && here == target)
        {
            list.Add(Pt(info.platforms[target].center));
            if (defeated) list.Add(Pt(info.portal));
            return list;
        }
        if (here >= 0)
        {
            var pl = info.platforms[here];
            list.Add(new WP { pos = pl.bridge[1], needPhase = true, phase = pl.phase, bridgeEntry = true });
            list.Add(new WP { pos = pl.bridge[0], needPhase = true, phase = pl.phase });
            list.Add(Pt(pl.ringPoint));
            start = AngleOf(info, pl.ringPoint);
        }
        else if (Flat(p, info.center) > 5.6f)
        {
            list.Add(Pt(Ring(info, -90)));
            start = -90;
        }
        else start = AngleOf(info, p);

        if (target < 0)
        {
            if (list.Count == 0) list.Add(Pt(Ring(info, Mathf.Round(start / 45f) * 45f)));
            return list;
        }
        var tp = info.platforms[target];
        float goal = AngleOf(info, tp.ringPoint);
        float delta = Mathf.DeltaAngle(start, goal);
        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(delta) / 40f));
        for (int i = 1; i <= steps; i++) list.Add(Pt(Ring(info, start + delta * i / steps)));
        list.Add(new WP { pos = tp.bridge[0], needPhase = true, phase = tp.phase, bridgeEntry = true });
        list.Add(new WP { pos = tp.bridge[1], needPhase = true, phase = tp.phase });
        list.Add(Pt(tp.center));
        if (defeated) list.Add(Pt(info.portal));
        return list;
    }

    static bool ShotThreat(BossController boss, Phase ph, Vector3 p, float horizon)
    {
        var chest = p + Vector3.up * 0.45f;
        foreach (var s in boss.shots)
        {
            if (s.phase != ph) continue;
            var rel = s.tr.position - chest;
            var v = s.vel;
            float tt = Mathf.Clamp(-Vector3.Dot(rel, v) / Mathf.Max(0.001f, v.sqrMagnitude), 0, horizon);
            if ((rel + v * tt).magnitude < 1.0f) return true;
        }
        return false;
    }

    static bool RingThreat(BossController boss, Phase ph, Vector3 p, float horizon)
    {
        float d = Flat(p, boss.info.center);
        if (p.y - boss.FloorY > 0.9f) return false;
        foreach (var r in boss.rings)
        {
            if (r.phase != ph) continue;
            if (r.r < d + 0.7f && r.r + r.speed * horizon > d - 0.7f) return true;
        }
        return false;
    }

    static bool Threat(BossController boss, Phase ph, Vector3 p, float horizon) =>
        ShotThreat(boss, ph, p, horizon) || RingThreat(boss, ph, p, horizon);

    static async Task<bool> BossFight()
    {
        var boss = G.Boss;
        if (boss == null) { log.AppendLine("  no boss"); return false; }
        var info = boss.info;
        float end = Time.time + 240f;
        List<WP> route = null;
        int wi = 0, routeTarget = -99;
        bool routeDefeated = false;
        float shiftCd = 0, jumpCd = 0;
        while (Time.time < end)
        {
            if (G.state == GameManager.State.Complete) return true;
            if (!Playing) { GameInput.SimWorldMove = Vector3.zero; GameInput.SimJumpHeld = false; route = null; await Frame(); continue; }
            var p = P;
            int target = boss.defeated ? info.portalPlatform : boss.cellPlatform;
            if (route == null || target != routeTarget || boss.defeated != routeDefeated)
            {
                route = Plan(info, p, target, boss.defeated);
                wi = 0; routeTarget = target; routeDefeated = boss.defeated;
            }
            float dt = Time.deltaTime;
            shiftCd -= dt; jumpCd -= dt;
            bool onBridge = OnBridge(info, p);
            var cur = PhaseState.Current;
            var other = cur == Phase.Day ? Phase.Night : Phase.Day;

            // dodge
            if (Threat(boss, cur, p, 0.5f))
            {
                if (shiftCd <= 0 && !onBridge && !Threat(boss, other, p, 0.5f)) { GameInput.SimPressShift(); shiftCd = 0.3f; }
                else if (jumpCd <= 0 && G.player.Grounded && RingThreat(boss, cur, p, 0.3f))
                { GameInput.SimPressJump(); GameInput.SimJumpHeld = true; jumpCd = 0.7f; }
            }
            if (jumpCd < 0.35f) GameInput.SimJumpHeld = false;

            // move
            var move = Vector3.zero;
            if (wi < route.Count)
            {
                var w = route[wi];
                bool hold = false;
                if (w.needPhase && PhaseState.Current != w.phase)
                {
                    if (!onBridge && shiftCd <= 0 && !Threat(boss, w.phase, p, 0.8f) && !boss.EclipsePending)
                    { GameInput.SimPressShift(); shiftCd = 0.3f; }
                    hold = true;
                }
                else if (w.bridgeEntry && !onBridge && (boss.EclipsePending || Threat(boss, PhaseState.Current, p, 1.2f))) hold = true;
                if (!hold)
                {
                    var d = new Vector3(w.pos.x - p.x, 0, w.pos.z - p.z);
                    if (d.magnitude < 0.15f) wi++;
                    else move = d.normalized * Mathf.Clamp01(d.magnitude * 3f + 0.25f);
                }
            }
            GameInput.SimWorldMove = move;
            await Frame();
        }
        log.AppendLine($"  boss fight timed out (boss hp {boss.hp}/{boss.maxHp})");
        return false;
    }

    static Orb OrbAtZ(int z)
    {
        foreach (var o in G.DebugOrbs)
            if (Mathf.RoundToInt(o.transform.position.z) == z) return o;
        return null;
    }

    static async Task<bool> WaitOrb(int z, float lo, float hi, int dir, bool byIndex = false)
    {
        var o = byIndex ? G.DebugOrbs[z] : OrbAtZ(z);
        if (o == null) { log.AppendLine($"  no orb at z={z}"); return false; }
        float end = Time.time + 20f;
        while (Time.time < end)
        {
            if (!Playing) return false;
            float ox = o.transform.position.x;
            if (ox >= lo && ox <= hi && (dir == 0 || o.dir.x == dir)) return true;
            await Frame();
        }
        log.AppendLine("  timeout waiting for orb");
        return false;
    }

    static async Task<bool> OrbFar(int z, float x, float dist)
    {
        var o = OrbAtZ(z);
        if (o == null) { log.AppendLine($"  no orb at z={z}"); return false; }
        float end = Time.time + 25f;
        while (Time.time < end)
        {
            if (!Playing) return false;
            if (Mathf.Abs(o.transform.position.x - x) >= dist) return true;
            await Frame();
        }
        log.AppendLine("  timeout waiting for far orb");
        return false;
    }

    /// <summary>Plays a generated level using the solution the generator wrote for it.</summary>
    /// <summary>Walks into a boss arena and idles there (for screenshots).</summary>
    public static async Task<string> RunBossPreview(int level, float wait) =>
        await Run(level, Levels.Get(level).solution.Replace("bossfight;", string.Format(CultureInfo.InvariantCulture, "wait {0};", wait)));

    /// <summary>Plays only the first N commands of a generated level's solution (for screenshots).</summary>
    public static async Task<string> RunPartial(int level, int commands)
    {
        var parts = Levels.Get(level).solution.Split(';');
        var sb = new StringBuilder();
        for (int i = 0; i < commands && i < parts.Length; i++) sb.Append(parts[i]).Append(';');
        return await Run(level, sb.ToString()) + " theme=" + Themes.Get(Levels.Get(level).theme).name;
    }

    public static async Task<string> RunGenerated(int level) => await Run(level, Levels.Get(level).solution);

    /// <summary>Plays generated levels [from, to) and summarises the results.</summary>
    public static async Task<string> RunRange(int from, int to)
    {
        var sb = new StringBuilder();
        int pass = 0;
        for (int l = from; l < to; l++)
        {
            string r = await RunGenerated(l);
            bool ok = r.Contains("COMPLETE");
            if (ok) pass++;
            sb.Append($"L{l + 1}: {(ok ? "PASS" : "FAIL")}");
            if (!ok) sb.AppendLine().Append(r);
            sb.AppendLine();
        }
        sb.Append($"{pass}/{to - from} passed");
        return sb.ToString();
    }

    public static async Task<string> Run(int level, string script)
    {
        log = new StringBuilder();
        GameInput.SimActive = true;
        GameInput.SimWorldMove = Vector3.zero;
        try
        {
            G.DebugStartLevel(level);
            await Wait(0.4f);
            log.AppendLine($"L{level + 1} start {G.DebugDescribe()}");
            var cmds = script.Split(';');
            for (int i = 0; i < cmds.Length; i++)
            {
                var a = cmds[i].Trim().Split(' ');
                if (a.Length == 0 || a[0] == "") continue;
                bool ok = true;
                switch (a[0])
                {
                    case "go": ok = await Go(F(a[1]), F(a[2])); break;
                    case "jump": ok = await Jump(F(a[1]), F(a[2]), a.Length > 3 ? F(a[3]) : -1); break;
                    case "shift": GameInput.SimPressShift(); await Frame(); await Frame(); break;
                    case "orbi": ok = await WaitOrb(int.Parse(a[1]), F(a[2]), F(a[3]), a.Length > 4 ? int.Parse(a[4]) : 0, true); break;
                    case "phase":
                        {
                            var want = a[1] == "D" ? Phase.Day : Phase.Night;
                            if (PhaseState.Current != want)
                            {
                                GameInput.SimPressShift();
                                float until = Time.time + 0.4f;
                                while (PhaseState.Current != want && Time.time < until) await Frame();
                            }
                            ok = PhaseState.Current == want;
                            if (!ok) log.AppendLine("  shift refused");
                            break;
                        }
                    case "orbfar": ok = await OrbFar(int.Parse(a[1]), F(a[2]), F(a[3])); break;
                    case "wait": await Wait(F(a[1])); break;
                    case "orbfar2": ok = await OrbFar2(a[1][0], F(a[2]), F(a[3]), F(a[4]), a.Length > 5 ? a[5][0] : '-'); break;
                    case "bossfight": ok = await BossFight(); break;
                    case "orb": ok = await WaitOrb(int.Parse(a[1]), F(a[2]), F(a[3]), a.Length > 4 ? int.Parse(a[4]) : 0); break;
                }
                if (G.state == GameManager.State.Complete) { log.AppendLine($"  COMPLETE after '{cmds[i].Trim()}'"); break; }
                if (G.state != GameManager.State.Playing || !ok)
                {
                    log.AppendLine($"  FAILED at #{i} '{cmds[i].Trim()}': {G.DebugDescribe()}");
                    break;
                }
            }
            await Wait(0.3f);
            log.AppendLine($"  end {G.DebugDescribe()}");
        }
        finally
        {
            GameInput.SimActive = false;
            GameInput.SimWorldMove = Vector3.zero;
            GameInput.SimJumpHeld = false;
        }
        return log.ToString();
    }
}
