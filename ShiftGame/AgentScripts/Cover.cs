using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Renders the cover art plates: the same showcase shot in Day and in Night.</summary>
public static class Cover
{
    const int ShowcaseIndex = 9999;

    static LevelDef Showcase() => new LevelDef("Cover", "",
        new[]
        {
            "...#####...",
            "DDDD###NNNN",
            "D..D###N..N",
            "DDDD###NNNN",
            "....###....",
            "....###....",
            "....###....",
        },
        new[]
        {
            ".....E.....",
            "..*.....*..",
            "...........",
            ".p.......q.",
            "...........",
            "......^....",
            ".....S.....",
        });

    static async Task Frames(int n) { for (int i = 0; i < n; i++) await Task.Yield(); }
    static async Task Wait(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) await Task.Yield(); }

    public static async Task<string> Render(string outDir, int w, int h, float yaw, float pitch, float dist, float fx, float fz)
    {
        // inject the showcase level into the generated-level cache
        var cacheField = typeof(Levels).GetField("generatedCache", BindingFlags.NonPublic | BindingFlags.Static);
        var cache = (System.Collections.Generic.Dictionary<int, LevelDef>)cacheField.GetValue(null);
        cache[ShowcaseIndex] = Showcase();

        var gm = GameManager.I;
        gm.DebugStartLevel(ShowcaseIndex);
        await Wait(0.6f);
        foreach (var o in gm.DebugOrbs) o.speed = 0f;

        var hudCanvas = Object.FindAnyObjectByType<Canvas>();
        var cam = Camera.main;
        var rig = cam.GetComponent<CameraRig>();
        rig.target = null;
        rig.focusPoint = new Vector3(fx, 0.2f, fz);
        rig.yaw = yaw; rig.pitch = pitch; rig.distance = dist;
        rig.Snap();
        rig.enabled = false;
        rig.Snap();
        gm.player.visual.rotation = Quaternion.Euler(0, 180 + yaw, 0);

        Directory.CreateDirectory(outDir);
        string a = await Grab(cam, Path.Combine(outDir, "cover_day.png"), w, h);

        gm.TryShift();
        await Wait(1.6f);
        foreach (var o in gm.DebugOrbs) o.speed = 0f;
        string b = await Grab(cam, Path.Combine(outDir, "cover_night.png"), w, h);

        rig.enabled = true;
        cam.aspect = w / (float)h;
        var sp = cam.WorldToViewportPoint(gm.player.transform.position + Vector3.up * 0.4f);
        cam.ResetAspect();
        return $"robot={sp.x * w:F0},{(1 - sp.y) * h:F0}";
    }

    static async Task<string> Grab(Camera cam, string path, int w, int h)
    {
        await Frames(3);
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        var prevTarget = cam.targetTexture;
        cam.targetTexture = rt;
        cam.aspect = w / (float)h;
        cam.Render();
        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;
        cam.targetTexture = prevTarget;
        cam.ResetAspect();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.Destroy(tex);
        rt.Release();
        return path;
    }
}
