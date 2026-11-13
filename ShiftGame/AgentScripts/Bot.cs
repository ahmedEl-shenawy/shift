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
                    case "wait": await Wait(F(a[1])); break;
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
