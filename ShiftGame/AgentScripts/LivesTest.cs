using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
public static class LivesTest
{
    static async Task Wait(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) await Task.Yield(); }
    static async Task WaitPlaying() { float t = Time.realtimeSinceStartup + 20; while (Time.realtimeSinceStartup < t && !(GameManager.I.state == GameManager.State.Playing && GameManager.I.player != null && GameManager.I.player.alive)) await Task.Yield(); }

    /// <summary>Dies twice before ever reaching the checkpoint: hearts must NOT refill.</summary>
    public static async Task<string> NoCheckpoint(int level)
    {
        var gm = GameManager.I;
        var die = typeof(GameManager).GetMethod("Die", BindingFlags.NonPublic | BindingFlags.Instance);
        gm.DebugStartLevel(level);
        await Wait(0.5f);
        die.Invoke(gm, new object[] { true });
        await Wait(0.3f); await WaitPlaying(); await Wait(0.3f);
        int a = gm.Lives;
        die.Invoke(gm, new object[] { true });
        await Wait(0.3f); await WaitPlaying(); await Wait(0.3f);
        return $"no-checkpoint deaths: lives after 1st={a} after 2nd={gm.Lives} (expect 2, 1)";
    }

    public static async Task<string> Showcase(int level, float delay)
    {
        var gm = GameManager.I;
        GameManager.DebugInvulnerable = true;
        gm.DebugStartLevel(level);
        await Wait(0.6f);
        var boss = gm.Boss;
        var info = boss.info;
        gm.player.Teleport(info.center + new Vector3(0.6f, 0.05f, -4f));
        await Wait(1.2f);
        var t = typeof(BossController);
        t.GetMethod("StartMeteors", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boss, new object[] { gm.player });
        t.GetMethod("StartLaser", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boss, new object[] { gm.player });
        await Wait(delay);
        Time.timeScale = 0f;   // freeze for the screenshot (caller restores)
        return $"meteors={boss.meteors.Count} laserOn={boss.laserOn} live={boss.laserLive}";
    }

    public static async Task<string> Run(int level)
    {
        var gm = GameManager.I;
        var die = typeof(GameManager).GetMethod("Die", BindingFlags.NonPublic | BindingFlags.Instance);
        var log = new System.Text.StringBuilder();
        gm.DebugStartLevel(level);
        await Wait(0.5f);
        var boss = gm.Boss;
        // stand on the arena checkpoint so deaths respawn there (like real play)
        var cp = Object.FindAnyObjectByType<Checkpoint>();
        gm.player.Teleport(cp.transform.position + Vector3.up * 0.05f);
        await Wait(0.8f);
        boss.hp = boss.maxHp - 2;                  // pretend we already landed two hits
        log.Append($"start: lives={gm.Lives} bossHp={boss.hp}/{boss.maxHp}\n");
        for (int i = 1; i <= 3; i++)
        {
            await WaitPlaying();
            die.Invoke(gm, new object[] { true });
            log.Append($"death {i}: lives={gm.Lives} state={gm.state}\n");
            await Wait(0.2f);
        }
        await WaitPlaying();
        await Wait(0.5f);
        var b2 = gm.Boss;
        log.Append($"after reset: lives={gm.Lives} bossHp={b2.hp}/{b2.maxHp} sameBoss={(b2 == boss)} player={gm.player.transform.position} deaths={gm.DebugDeaths}");
        return log.ToString();
    }
}
