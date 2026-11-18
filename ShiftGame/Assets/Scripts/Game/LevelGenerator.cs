using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>World-space description of a boss arena (filled in by the generator, read by BossController and the bot).</summary>
public class BossPlatform
{
    public Vector3 center;      // platform centre (standing height)
    public Vector3 ringPoint;   // where the bridge meets the ring
    public Vector3[] bridge;    // bridge cells, ring side first
    public Phase phase;         // phase the bridge exists in
}

public class BossInfo
{
    public int tier;
    public Vector3 center;      // arena centre at floor height
    public float ringRadius = 4f;
    public readonly List<BossPlatform> platforms = new List<BossPlatform>();
    public int portalPlatform;
    public Vector3 portal;
    /// <summary>Phase of the ring quadrants NE, NW, SW, SE (the four bridge points between them are neutral).</summary>
    public readonly Phase[] quadrant = new Phase[4];
    /// <summary>Route from the start pad to the arena: position + required phase (-1 = any, 0 = day, 1 = night).</summary>
    public readonly List<(Vector3 pos, int phase)> entry = new List<(Vector3 pos, int phase)>();
    /// <summary>Extra light-cell spots on the ring (quadrant centres, radius 4).</summary>
    public readonly List<Vector3> ringSpots = new List<Vector3>();

    public float AngleOf(Vector3 p)
    {
        float a = Mathf.Atan2(p.z - center.z, p.x - center.x) * Mathf.Rad2Deg;
        return a < 0 ? a + 360f : a;
    }

    public static bool NeutralAngle(float deg)
    {
        float m = Mathf.Repeat(deg, 90f);
        return Mathf.Min(m, 90f - m) <= 22f;
    }

    public Phase QuadrantPhase(float deg) => quadrant[(int)(Mathf.Repeat(deg, 360f) / 90f) % 4];

    /// <summary>Number of places a light cell can appear: outer platforms, then ring spots.</summary>
    public int CellSpotCount => platforms.Count + ringSpots.Count;
    public Vector3 CellSpot(int i) => i < platforms.Count ? platforms[i].center : ringSpots[i - platforms.Count];
}

/// <summary>
/// Endless mode: builds an infinite sequence of levels after the handmade campaign.
/// Each level is a winding chain of "sections" (each solvable by construction) linked by neutral pads.
/// Sections are built in local space and placed with a rotation (straight / left / right) after a collision check,
/// so paths turn and never overlap. Every 10th level is a boss arena. Difficulty rises with the level number.
/// Every level also carries a scripted solution used by the automated playtest bot.
/// </summary>
public static class LevelGenerator
{
    // ------------------------------------------------------------------ builder

    struct Cmd
    {
        public string op; public float x, z, a; public char p, ax;
    }

    class Builder
    {
        public readonly Dictionary<Vector3Int, char> cells = new Dictionary<Vector3Int, char>(); // (x, layer, z)
        public readonly List<Cmd> sol = new List<Cmd>();

        public void Floor(int x, int z, int h, char c) => cells[new Vector3Int(x, h, z)] = c;
        public void Ent(int x, int z, int h, char c) => cells[new Vector3Int(x, h + 1, z)] = c;

        public void Go(float x, float z) => sol.Add(new Cmd { op = "go", x = x, z = z });
        public void Jump(float x, float z, float shiftDelay) => sol.Add(new Cmd { op = "jump", x = x, z = z, a = shiftDelay });
        public void Phase(char p) => sol.Add(new Cmd { op = "phase", p = p });
        /// <summary>Wait until the orb patrolling lane row laneZ (local) is at least dist away from x. phase '-' = any orb.</summary>
        public void OrbFar(int laneZ, float x, float dist, char phase = '-') => sol.Add(new Cmd { op = "orbfar", x = x, z = laneZ, a = dist, p = phase });
        public void Boss() => sol.Add(new Cmd { op = "boss" });
    }

    struct Frame
    {
        public Vector2Int origin, fwd, right;
        public Frame(Vector2Int o, Vector2Int f) { origin = o; fwd = f; right = new Vector2Int(f.y, -f.x); }
        public Vector2Int Map(int lx, int lz) => origin + right * lx + fwd * lz;
        public Vector2 Map(float lx, float lz) => (Vector2)origin + (Vector2)right * lx + (Vector2)fwd * lz;
    }

    static readonly Vector2Int North = new Vector2Int(0, 1), East = new Vector2Int(1, 0), West = new Vector2Int(-1, 0);

    static char Other(char p) => p == 'D' ? 'N' : 'D';
    static char RandPhase(System.Random r) => r.Next(2) == 0 ? 'D' : 'N';

    static readonly string[] Adjectives =
    {
        "Drifting", "Hollow", "Silent", "Broken", "Twilight", "Ember", "Frozen", "Shattered", "Restless", "Gilded",
        "Whispering", "Crooked", "Lonely", "Burning", "Velvet", "Sunken", "Folded", "Distant", "Fading", "Tangled",
        "Hidden", "Wandering", "Shifting", "Glass", "Endless"
    };
    static readonly string[] Nouns =
    {
        "Steps", "Causeway", "Spire", "Crossing", "Gardens", "Vault", "Ridge", "Bridges", "Labyrinth", "Archive",
        "Orchard", "Lanterns", "Hollows", "Terrace", "Reach", "Meridian", "Observatory", "Threshold", "Cascade", "Eclipse",
        "Switchback", "Spiral", "Maze", "Gauntlet", "Overlook"
    };

    /// <summary>Difficulty grade for a level index (0-based, counting the handmade campaign).</summary>
    public static int Difficulty(int index) => index - Levels.HandmadeCount + 3;

    public static bool IsBoss(int index) => index >= Levels.HandmadeCount && (index + 1) % 10 == 0;

    // ------------------------------------------------------------------ main

    public static LevelDef Generate(int index)
    {
        if (IsBoss(index)) return GenerateBoss(index);

        int d = Difficulty(index);
        float t = Mathf.Clamp01(d / 24f);
        var rng = new System.Random(index * 92821 + 17);
        var b = new Builder();
        var occupied = new HashSet<Vector2Int>();
        var used = new List<string>();
        var recent = new List<string>();

        int h = 0;
        var C = Vector2Int.zero;
        var H = North;
        var prevPad = PadCells(new Frame(C - North, North), 0);
        foreach (var c in prevPad) b.Floor(c.x, c.y, h, '#');
        occupied.UnionWith(prevPad);
        b.Ent(C.x, C.y, h, 'S');

        int chunks = Mathf.Clamp(3 + d / 2 + rng.Next(-1, 2), 3, 12);
        int shardsWanted = Mathf.Min(1 + d / 3, 6);
        int shardsPlaced = 0;
        var blocked = new HashSet<Vector2Int>();

        for (int i = 0; i < chunks; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < 8 && !placed; attempt++)
            {
                string type = Pick(rng, d, recent);
                var cb = new Builder();
                int zEnd = 0, hEnd = h, sp = shardsPlaced;
                BuildChunk(type, cb, rng, d, t, ref zEnd, ref hEnd, shardsWanted, ref sp);
                for (int lz = zEnd; lz < zEnd + 3; lz++)
                for (int lx = -1; lx <= 1; lx++)
                    cb.Floor(lx, lz, hEnd, '#');

                foreach (var hc in Headings(rng, H, d, blocked))
                {
                    var f = new Frame(C + hc * 2, hc);
                    var fp = Footprint(cb, f);
                    if (Collides(fp, occupied, prevPad)) continue;

                    b.Go(C.x + hc.x, C.y + hc.y);
                    Commit(b, cb, f);
                    var nc = f.Map(0, zEnd + 1);
                    b.Go(nc.x, nc.y);
                    b.Ent(nc.x, nc.y, hEnd, 'C');
                    occupied.UnionWith(fp);
                    prevPad = PadCells(f, zEnd);
                    C = nc; H = hc; h = hEnd; shardsPlaced = sp;
                    recent.Add(type);
                    if (recent.Count > 2) recent.RemoveAt(0);
                    if (!used.Contains(type)) used.Add(type);
                    placed = true;
                    break;
                }
            }
            if (!placed) break;
            blocked.Clear();

            int remaining = chunks - i;
            if (shardsPlaced < shardsWanted && (rng.NextDouble() < 0.5 || shardsWanted - shardsPlaced >= remaining))
            {
                if (Spur(b, rng, d, C, H, h, occupied, prevPad, out var side))
                {
                    shardsPlaced++;
                    blocked.Add(side);
                }
            }
        }

        PlaceExit(b, rng, C, H, h, occupied, prevPad, blocked);

        float orbSpeed = 2.0f + Mathf.Min(d * 0.1f, 2.0f);
        int theme = rng.Next(Themes.All.Length - 1);   // any theme except the boss one
        string name = Adjectives[rng.Next(Adjectives.Length)] + " " + Nouns[rng.Next(Nouns.Length)];
        string hint = $"Endless depth {d - 2}  -  {Themes.Get(theme).name}.  {Describe(used)}  Shards: {shardsPlaced}.";
        var def = ToDef(b, name, hint, orbSpeed, null);
        def.theme = theme;
        def.startPhase = rng.Next(2) == 0 ? global::Phase.Day : global::Phase.Night;
        return def;
    }

    static IEnumerable<Vector2Int> Headings(System.Random rng, Vector2Int H, int d, HashSet<Vector2Int> blocked)
    {
        var all = new List<Vector2Int>();
        foreach (var c in new[] { North, East, West })
            if (c != -H && !blocked.Contains(c)) all.Add(c);
        // prefer straight early on; turn often later
        bool straightFirst = d < 3 || rng.NextDouble() < 0.4;
        for (int i = all.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (all[i], all[j]) = (all[j], all[i]); }
        if (straightFirst && all.Contains(H)) { all.Remove(H); all.Insert(0, H); }
        else if (all.Contains(H)) { all.Remove(H); all.Add(H); }
        return all;
    }

    static HashSet<Vector2Int> PadCells(Frame f, int zStart)
    {
        var s = new HashSet<Vector2Int>();
        for (int lz = zStart; lz < zStart + 3; lz++)
        for (int lx = -1; lx <= 1; lx++)
            s.Add(f.Map(lx, lz));
        return s;
    }

    static HashSet<Vector2Int> Footprint(Builder cb, Frame f)
    {
        var s = new HashSet<Vector2Int>();
        foreach (var k in cb.cells.Keys) s.Add(f.Map(k.x, k.z));
        return s;
    }

    /// <summary>True if any new cell touches (8-neighbourhood) an existing cell other than the pad it starts from.</summary>
    static bool Collides(HashSet<Vector2Int> cells, HashSet<Vector2Int> occupied, HashSet<Vector2Int> allowed)
    {
        foreach (var c in cells)
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var n = new Vector2Int(c.x + dx, c.y + dz);
                if (occupied.Contains(n) && !allowed.Contains(n)) return true;
            }
        return false;
    }

    static void Commit(Builder main, Builder cb, Frame f)
    {
        bool rotated = f.fwd.x != 0;
        foreach (var kv in cb.cells)
        {
            var w = f.Map(kv.Key.x, kv.Key.z);
            char c = kv.Value;
            if (rotated)
            {
                if (c == 'p') c = 'v'; else if (c == 'q') c = 'w';
                else if (c == 'v') c = 'p'; else if (c == 'w') c = 'q';
            }
            main.cells[new Vector3Int(w.x, kv.Key.y, w.y)] = c;
        }
        foreach (var c in cb.sol)
        {
            var cmd = c;
            switch (c.op)
            {
                case "go":
                case "jump":
                {
                    var w = f.Map(c.x, c.z);
                    cmd.x = w.x; cmd.z = w.y;
                    break;
                }
                case "orbfar":
                {
                    var w = f.Map(c.x, c.z);
                    cmd.op = "orbfar2";
                    if (!rotated) { cmd.ax = 'x'; cmd.z = w.y; cmd.x = w.x; }   // lane runs along world x at z = w.y
                    else { cmd.ax = 'z'; cmd.z = w.x; cmd.x = w.y; }            // lane runs along world z at x = w.x
                    break;
                }
            }
            main.sol.Add(cmd);
        }
    }

    static string Pick(System.Random rng, int d, List<string> recent)
    {
        var pool = new List<string> { "run" };
        if (d >= 1) { pool.Add("stones"); pool.Add("zigzag"); }
        if (d >= 2) pool.Add("stairs");
        if (d >= 3) pool.Add("gate");
        if (d >= 4) { pool.Add("spikes"); pool.Add("maze"); }
        if (d >= 5) { pool.Add("gauntlet"); pool.Add("bridge"); }
        for (int guard = 0; guard < 20; guard++)
        {
            var pick = pool[rng.Next(pool.Count)];
            if (!recent.Contains(pick) || pool.Count <= 2) return pick;
        }
        return pool[rng.Next(pool.Count)];
    }

    static void BuildChunk(string type, Builder cb, System.Random rng, int d, float t, ref int z, ref int h, int shardsWanted, ref int shardsPlaced)
    {
        switch (type)
        {
            case "run": Run(cb, rng, d, t, 0, ref z, h); break;
            case "stones": Stones(cb, rng, d, t, 0, ref z, h); break;
            case "zigzag": Zigzag(cb, rng, d, t, ref z, h); break;
            case "stairs": Stairs(cb, rng, d, t, 0, ref z, ref h); break;
            case "gate": Gate(cb, rng, d, t, 0, ref z, h); break;
            case "bridge": OrbBridge(cb, rng, d, 0, ref z, h); break;
            case "gauntlet": Gauntlet(cb, rng, d, t, ref z, h); break;
            case "spikes": Spikes(cb, rng, d, t, 0, ref z, h, shardsWanted, ref shardsPlaced, false); break;
            case "maze": Spikes(cb, rng, d, t, 0, ref z, h, shardsWanted, ref shardsPlaced, true); break;
        }
    }

    static string Describe(List<string> used)
    {
        var words = new List<string>();
        foreach (var u in used)
        {
            switch (u)
            {
                case "run": words.Add("bridges"); break;
                case "stones": words.Add("leaps"); break;
                case "zigzag": words.Add("switchbacks"); break;
                case "stairs": words.Add("climbs"); break;
                case "gate": words.Add("gates"); break;
                case "spikes": words.Add("spikes"); break;
                case "maze": words.Add("mazes"); break;
                case "gauntlet": words.Add("gauntlets"); break;
                case "bridge": words.Add("sentinels"); break;
            }
        }
        if (words.Count == 0) return "";
        var s = string.Join(", ", words);
        return char.ToUpper(s[0]) + s.Substring(1) + ".";
    }

    // ------------------------------------------------------------------ pad decorations

    /// <summary>Side branch off the current pad with a shard at its tip. Returns the side used.</summary>
    static bool Spur(Builder b, System.Random rng, int d, Vector2Int C, Vector2Int H, int h,
        HashSet<Vector2Int> occupied, HashSet<Vector2Int> pad, out Vector2Int side)
    {
        var sides = new List<Vector2Int> { North, East, West, new Vector2Int(0, -1) };
        sides.Remove(-H);
        for (int i = sides.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (sides[i], sides[j]) = (sides[j], sides[i]); }
        int len = 2 + rng.Next(0, 2 + Mathf.Min(d / 6, 2));
        char p = RandPhase(rng);
        foreach (var s in sides)
        {
            var cells = new HashSet<Vector2Int>();
            for (int k = 2; k <= len + 1; k++) cells.Add(C + s * k);
            if (Collides(cells, occupied, pad)) continue;
            var tip = C + s * (len + 1);
            foreach (var c in cells) b.Floor(c.x, c.y, h, p);
            b.Ent(tip.x, tip.y, h, '*');
            occupied.UnionWith(cells);
            var edge = C + s;
            b.Go(edge.x, edge.y);
            b.Phase(p);
            b.Go(tip.x, tip.y);
            b.Go(edge.x, edge.y);
            b.Go(C.x, C.y);
            side = s;
            return true;
        }
        side = Vector2Int.zero;
        return false;
    }

    static void PlaceExit(Builder b, System.Random rng, Vector2Int C, Vector2Int H, int h,
        HashSet<Vector2Int> occupied, HashSet<Vector2Int> pad, HashSet<Vector2Int> blocked)
    {
        foreach (var hc in Headings(rng, H, 99, blocked))
        {
            var f = new Frame(C + hc * 2, hc);
            var cells = PadCells(f, 0);
            if (Collides(cells, occupied, pad)) continue;
            foreach (var c in cells) b.Floor(c.x, c.y, h, '#');
            var e = f.Map(0, 1);
            b.Ent(e.x, e.y, h, 'E');
            b.Go(C.x + hc.x, C.y + hc.y);
            b.Go(e.x, e.y);
            return;
        }
        // nowhere to go: the last pad becomes the exit
        b.Ent(C.x, C.y, h, 'E');
        b.Go(C.x, C.y);
    }

    // ------------------------------------------------------------------ sections (local space: enter at (0,-1), run toward +z)

    /// <summary>Straight bridge of alternating day/night segments.</summary>
    static void Run(Builder b, System.Random rng, int d, float t, int cx, ref int z, int h)
    {
        int w = d < 6 ? 3 : (rng.NextDouble() < 0.5 ? 1 : 3);
        int baseSeg = Mathf.Max(1, 3 - d / 5);
        int len = 3 + rng.Next(0, 5);
        char p = RandPhase(rng);
        b.Phase(p);
        int zz = z, left = len;
        while (left > 0)
        {
            int seg = Mathf.Min(left, baseSeg + rng.Next(0, 2));
            for (int r = 0; r < seg; r++, zz++)
                for (int x = cx - w / 2; x <= cx + w / 2; x++)
                    b.Floor(x, zz, h, p);
            left -= seg;
            if (left > 0)
            {
                b.Go(cx, zz - 1 + 0.45f);
                p = Other(p);
                b.Phase(p);
            }
        }
        b.Go(cx, zz - 1);
        z = zz;
    }

    /// <summary>Floating tiles separated by gaps (with sideways offsets later on): jump, shift in mid-air, land.</summary>
    static void Stones(Builder b, System.Random rng, int d, float t, int cx, ref int z, int h)
    {
        int count = 2 + Mathf.Min(d / 3, 5) + rng.Next(0, 2);
        float flip = 0.4f + 0.5f * t;
        bool lateral = d >= 4 && rng.NextDouble() < 0.7;
        char p = RandPhase(rng);
        b.Phase(p);
        b.Go(cx, z - 1 + 0.3f);
        int zz = z + 1, x = cx;
        char prev = p;
        for (int i = 0; i < count; i++)
        {
            if (lateral && i > 0) x = Mathf.Clamp(x + rng.Next(-1, 2), -2, 2);
            char c = i == 0 ? p : (rng.NextDouble() < flip ? Other(prev) : prev);
            b.Floor(x, zz, h, c);
            b.Jump(x, zz, i > 0 && c != prev ? 0.25f : -1f);
            prev = c;
            zz += 2;
        }
        b.Jump(Mathf.Clamp(x, -1, 1), zz, -1f);
        z = zz;
    }

    /// <summary>A narrow snaking path; every bend can change the world it belongs to.</summary>
    static void Zigzag(Builder b, System.Random rng, int d, float t, ref int z, int h)
    {
        var cells = new List<Vector2Int>();
        var phases = new List<char>();
        float flip = 0.45f + 0.45f * t;
        char p = RandPhase(rng);
        int x = 0, zz = z;
        int bends = 2 + Mathf.Min(d / 4, 3) + rng.Next(0, 2);

        void Forward(int len)
        {
            for (int k = 0; k < len; k++) { cells.Add(new Vector2Int(x, zz)); phases.Add(p); zz++; }
        }
        void Lateral(int target)
        {
            int dir = target > x ? 1 : -1;
            while (x != target) { x += dir; cells.Add(new Vector2Int(x, zz - 1)); phases.Add(p); }
        }

        Forward(1 + rng.Next(0, 2));
        for (int i = 0; i < bends; i++)
        {
            if (rng.NextDouble() < flip) p = Other(p);
            int target;
            int guard = 0;
            do { target = rng.Next(-3, 4); } while (Mathf.Abs(target - x) < 2 && guard++ < 20);
            Lateral(target);
            if (rng.NextDouble() < flip) p = Other(p);
            Forward(1 + rng.Next(0, 2));
        }
        if (Mathf.Abs(x) > 1)
        {
            if (rng.NextDouble() < flip) p = Other(p);
            Lateral(rng.Next(-1, 2));
        }
        if (rng.NextDouble() < flip) p = Other(p);
        Forward(1);

        for (int i = 0; i < cells.Count; i++) b.Floor(cells[i].x, cells[i].y, h, phases[i]);

        b.Phase(phases[0]);
        b.Go(cells[0].x, cells[0].y);
        for (int i = 1; i < cells.Count; i++)
        {
            if (phases[i] != phases[i - 1])
            {
                Vector2 a = cells[i - 1], c = cells[i];
                var edge = a + (c - a) * 0.45f;
                b.Go(edge.x, edge.y);
                b.Phase(phases[i]);
            }
            b.Go(cells[i].x, cells[i].y);
        }
        z = zz;
    }

    /// <summary>Climb up (or drop down) a staircase where every step may belong to a different world.</summary>
    static void Stairs(Builder b, System.Random rng, int d, float t, int cx, ref int z, ref int h)
    {
        int n = 2 + Mathf.Min(d / 5, 3);
        bool down = h >= 2 && rng.NextDouble() < 0.5;
        if (down) n = Mathf.Min(n, h);
        int step = down ? -1 : 1;
        float flip = 0.4f + 0.5f * t;
        char prev = RandPhase(rng);
        b.Phase(prev);
        for (int i = 1; i <= n; i++)
        {
            char c = i == 1 ? prev : (rng.NextDouble() < flip ? Other(prev) : prev);
            b.Floor(cx, z + i - 1, h + i * step, c);
            b.Jump(cx, z + i - 1, i > 1 && c != prev ? (down ? 0.15f : 0.2f) : -1f);
            prev = c;
        }
        z += n;
        h += n * step;
    }

    static void Lane(Builder b, int cx, int z, int h, char c)
    {
        for (int x = cx - 3; x <= cx + 3; x++) b.Floor(x, z, h, c);
    }

    /// <summary>Rows of walls with a single day- or night-gate, separated by lanes that orbs may patrol.</summary>
    static void Gate(Builder b, System.Random rng, int d, float t, int cx, ref int z, int h)
    {
        int pairs = 1 + Mathf.Min(d / 5, 3);
        float orbChance = d >= 4 ? 0.3f + 0.6f * t : 0f;
        int zA = z;
        Lane(b, cx, zA, h, '#');
        char qa = '-';
        b.Go(cx, zA);
        for (int j = 0; j < pairs; j++)
        {
            int zw = zA + 1, zB = zA + 2;
            int g = cx + rng.Next(-3, 4);
            char r = RandPhase(rng);
            char qb = rng.NextDouble() < orbChance ? RandPhase(rng) : '-';
            if (qa == r && qb == r) qb = Other(r);

            for (int x = cx - 3; x <= cx + 3; x++)
            {
                b.Floor(x, zw, h, '#');
                b.Ent(x, zw, h, x == g ? (r == 'D' ? 'n' : 'd') : 'W');
            }
            Lane(b, cx, zB, h, '#');
            if (qb != '-')
            {
                int ox = cx + rng.Next(-3, 4);
                b.Ent(ox, zB, h, qb == 'D' ? 'p' : 'q');
            }

            b.Go(g, zA);
            if (qa == r) b.OrbFar(zA, g, 2.5f);
            if (qb == r) b.OrbFar(zB, g, 2.5f);
            b.Phase(r);
            b.Go(g, zB);
            if (qb != '-') b.Phase(Other(qb));

            qa = qb;
            zA = zB;
        }
        b.Go(cx, zA);
        z = zA + 1;
    }

    /// <summary>A narrow single-phase bridge crossed by lanes guarded by orbs of the same phase.</summary>
    static void OrbBridge(Builder b, System.Random rng, int d, int cx, ref int z, int h)
    {
        int lanes = 1 + Mathf.Min(d / 7, 2);
        char p = RandPhase(rng);
        b.Phase(p);
        int zz = z;
        for (int j = 0; j < lanes; j++)
        {
            int a = 1 + rng.Next(0, 2);
            for (int r = 0; r < a; r++, zz++) b.Floor(cx, zz, h, p);
            int zl = zz++;
            Lane(b, cx, zl, h, p);
            int ox = cx + (rng.Next(2) == 0 ? -3 : 3);
            b.Ent(ox, zl, h, p == 'D' ? 'p' : 'q');
            b.Go(cx, zl - 1);
            b.OrbFar(zl, cx, 2.5f);
            b.Go(cx, zl + 1);
        }
        b.Floor(cx, zz, h, p);
        zz++;
        b.Go(cx, zz - 1);
        z = zz;
    }

    /// <summary>A neutral corridor crossed by orb lanes. Later lanes carry a day AND a night orb: no safe phase, only timing.</summary>
    static void Gauntlet(Builder b, System.Random rng, int d, float t, ref int z, int h)
    {
        int lanes = 2 + Mathf.Min(d / 6, 3);
        float twoChance = d >= 8 ? 0.25f + 0.4f * t : 0f;
        int zz = z;
        void Safe() { for (int x = -1; x <= 1; x++) b.Floor(x, zz, h, '#'); zz++; }
        Safe();
        b.Go(0, zz - 1);
        for (int j = 0; j < lanes; j++)
        {
            int zl = zz++;
            Lane(b, 0, zl, h, '#');
            char q = RandPhase(rng);
            bool two = rng.NextDouble() < twoChance;
            int ox = rng.Next(-3, 4);
            b.Ent(ox, zl, h, q == 'D' ? 'p' : 'q');
            if (two)
            {
                int ox2 = ox <= 0 ? ox + 3 : ox - 3;
                b.Ent(ox2, zl, h, q == 'D' ? 'q' : 'p');
            }
            Safe();
            if (!two)
            {
                b.Phase(Other(q));
                b.Go(0, zz - 1);
            }
            else
            {
                char P = RandPhase(rng);
                b.Phase(P);
                b.OrbFar(zl, 0, 2.5f, P);
                b.Go(0, zz - 1);
            }
        }
        z = zz;
    }

    /// <summary>A field of spikes (or, as a maze, mostly phase tiles); a BFS over (cell, phase) guarantees a route.</summary>
    static void Spikes(Builder b, System.Random rng, int d, float t, int cx, ref int z, int h, int shardsWanted, ref int shardsPlaced, bool maze)
    {
        const int W = 5;
        int L = (maze ? 4 : 3) + Mathf.Min(d / 4, 4) + rng.Next(0, 2);
        float density = maze ? 0.05f + 0.1f * t : 0.15f + 0.25f * t;
        float phaseProb = maze ? 0.55f + 0.25f * t : 0.15f + 0.35f * t;
        int x0 = cx - W / 2;
        int ei = cx - x0, ej = 0, xi = cx - x0, xj = L - 1;

        char[,] tile = new char[W, L];
        bool[,] spike = new bool[W, L];
        bool ok = false;
        for (int attempt = 0; attempt < 80 && !ok; attempt++)
        {
            float dens = attempt > 50 ? density * 0.5f : density;
            for (int i = 0; i < W; i++)
            for (int j = 0; j < L; j++)
            {
                tile[i, j] = rng.NextDouble() < phaseProb ? RandPhase(rng) : '#';
                spike[i, j] = tile[i, j] == '#' && rng.NextDouble() < dens;
            }
            tile[ei, ej] = '#'; spike[ei, ej] = false;
            tile[xi, xj] = '#'; spike[xi, xj] = false;
            ok = Bfs(tile, spike, W, L, ei, ej, 'D', xi, xj) != null;
        }
        if (!ok)
            for (int i = 0; i < W; i++) for (int j = 0; j < L; j++) { tile[i, j] = '#'; spike[i, j] = false; }

        for (int i = 0; i < W; i++)
        for (int j = 0; j < L; j++)
        {
            b.Floor(x0 + i, z + j, h, tile[i, j]);
            if (spike[i, j]) b.Ent(x0 + i, z + j, h, '^');
        }

        b.Go(cx, z);
        b.Phase('D');
        char cur = 'D';
        int si = ei, sj = ej;

        if (shardsPlaced < shardsWanted && rng.NextDouble() < 0.6)
        {
            var reach = Reachable(tile, spike, W, L, ei, ej);
            reach.RemoveAll(c => (c.x == ei && c.y == ej) || (c.x == xi && c.y == xj));
            if (reach.Count > 0)
            {
                var c = reach[rng.Next(reach.Count)];
                b.Ent(x0 + c.x, z + c.y, h, '*');
                shardsPlaced++;
                EmitPath(b, Bfs(tile, spike, W, L, si, sj, cur, c.x, c.y), x0, z, ref cur);
                si = c.x; sj = c.y;
            }
        }
        EmitPath(b, Bfs(tile, spike, W, L, si, sj, cur, xi, xj), x0, z, ref cur);
        z += L;
    }

    struct State { public int i, j; public char p; }

    static bool Walkable(char[,] tile, bool[,] spike, int W, int L, int i, int j, char p)
    {
        if (i < 0 || j < 0 || i >= W || j >= L) return false;
        if (spike[i, j]) return false;
        char c = tile[i, j];
        return c == '#' || c == p;
    }

    static List<State> Bfs(char[,] tile, bool[,] spike, int W, int L, int si, int sj, char sp, int ti, int tj)
    {
        var prev = new Dictionary<(int, int, char), (int, int, char)>();
        var q = new Queue<(int, int, char)>();
        var start = (si, sj, sp);
        q.Enqueue(start);
        prev[start] = start;
        int[] di = { 1, -1, 0, 0 }, dj = { 0, 0, 1, -1 };
        while (q.Count > 0)
        {
            var s = q.Dequeue();
            if (s.Item1 == ti && s.Item2 == tj)
            {
                var path = new List<State>();
                var cur = s;
                while (true)
                {
                    path.Add(new State { i = cur.Item1, j = cur.Item2, p = cur.Item3 });
                    var pr = prev[cur];
                    if (pr.Equals(cur)) break;
                    cur = pr;
                }
                path.Reverse();
                return path;
            }
            if (tile[s.Item1, s.Item2] == '#')
            {
                var n = (s.Item1, s.Item2, Other(s.Item3));
                if (!prev.ContainsKey(n)) { prev[n] = s; q.Enqueue(n); }
            }
            for (int k = 0; k < 4; k++)
            {
                int ni = s.Item1 + di[k], nj = s.Item2 + dj[k];
                if (!Walkable(tile, spike, W, L, ni, nj, s.Item3)) continue;
                var n = (ni, nj, s.Item3);
                if (!prev.ContainsKey(n)) { prev[n] = s; q.Enqueue(n); }
            }
        }
        return null;
    }

    static List<Vector2Int> Reachable(char[,] tile, bool[,] spike, int W, int L, int si, int sj)
    {
        var res = new List<Vector2Int>();
        for (int i = 0; i < W; i++)
        for (int j = 0; j < L; j++)
        {
            if (spike[i, j]) continue;
            if (Bfs(tile, spike, W, L, si, sj, 'D', i, j) != null) res.Add(new Vector2Int(i, j));
        }
        return res;
    }

    static void EmitPath(Builder b, List<State> path, int x0, int z0, ref char cur)
    {
        if (path == null) return;
        for (int k = 1; k < path.Count; k++)
        {
            var s = path[k];
            if (s.p != cur) { b.Phase(s.p); cur = s.p; }
            else b.Go(x0 + s.i, z0 + s.j);
        }
    }

    // ------------------------------------------------------------------ boss arena

    static LevelDef GenerateBoss(int index)
    {
        int tier = (index + 1) / 10;
        var rng = new System.Random(index * 7177 + 3);
        var b = new Builder();
        const int h = 0;

        // start pad and a short warm-up bridge
        for (int z = 0; z < 3; z++) for (int x = -1; x <= 1; x++) b.Floor(x, z, h, '#');
        b.Ent(0, 0, h, 'S');
        b.Go(0, 2);
        char p = RandPhase(rng);
        b.Phase(p);
        for (int z = 3; z < 7; z++) for (int x = -1; x <= 1; x++) b.Floor(x, z, h, z < 5 ? p : Other(p));
        b.Go(0, 4.45f);
        b.Phase(Other(p));
        for (int z = 7; z < 9; z++) for (int x = -1; x <= 1; x++) b.Floor(x, z, h, '#');
        b.Ent(0, 8, h, 'C');
        b.Go(0, 8);

        // ring arena around (0, zc): neutral "safe zones" at the four bridge points, day/night quadrants between
        const int zc = 14;
        var info = new BossInfo { tier = tier, center = new Vector3(0, h, zc) };
        int pi = p == 'D' ? 0 : 1;
        info.entry.Add((new Vector3(0, h, 2.4f), -1));
        info.entry.Add((new Vector3(0, h, 4.45f), pi));
        info.entry.Add((new Vector3(0, h, 6.6f), 1 - pi));
        info.entry.Add((new Vector3(0, h, 8f), -1));
        char qp = RandPhase(rng);
        var quadChars = new[] { qp, Other(qp), qp, Other(qp) };
        for (int q = 0; q < 4; q++) info.quadrant[q] = quadChars[q] == 'D' ? global::Phase.Day : global::Phase.Night;
        for (int x = -6; x <= 6; x++)
        for (int z = zc - 6; z <= zc + 6; z++)
        {
            float dist = Mathf.Sqrt(x * x + (z - zc) * (z - zc));
            if (dist < 2.5f || dist > 5.2f) continue;
            float ang = Mathf.Atan2(z - zc, x) * Mathf.Rad2Deg;
            if (ang < 0) ang += 360f;
            char tile = BossInfo.NeutralAngle(ang) ? '#' : quadChars[(int)(ang / 90f) % 4];
            b.Floor(x, z, h, tile);
        }
        for (int q = 0; q < 4; q++)
        {
            float a = (45f + 90f * q) * Mathf.Deg2Rad;
            info.ringSpots.Add(new Vector3(Mathf.Cos(a) * 4f, h, zc + Mathf.Sin(a) * 4f));
        }
        // pylons framing the arena (decorative, out of reach)
        foreach (var d in new[] { new Vector2Int(5, 5), new Vector2Int(-5, 5), new Vector2Int(5, -5), new Vector2Int(-5, -5) })
        {
            b.Floor(d.x, zc + d.y, h, '#');
            b.Ent(d.x, zc + d.y, h, 'W');
        }
        char pw = RandPhase(rng), pe = Other(pw), pn = RandPhase(rng);
        var dirs = new[] { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, 1) };
        var phases = new[] { pw, pe, pn };
        for (int k = 0; k < 3; k++)
        {
            var dv = dirs[k];
            var bridge = new Vector3[2];
            for (int s = 0; s < 2; s++)
            {
                var c = new Vector2Int(dv.x * (6 + s), zc + dv.y * (6 + s));
                b.Floor(c.x, c.y, h, phases[k]);
                bridge[s] = new Vector3(c.x, h, c.y);
            }
            var pc = new Vector2Int(dv.x * 9, zc + dv.y * 9);
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) b.Floor(pc.x + x, pc.y + z, h, '#');
            info.platforms.Add(new BossPlatform
            {
                center = new Vector3(pc.x, h, pc.y),
                ringPoint = new Vector3(dv.x * 4, h, zc + dv.y * 4),
                bridge = bridge,
                phase = phases[k] == 'D' ? global::Phase.Day : global::Phase.Night,
            });
        }
        info.portalPlatform = 2;
        b.Ent(0, zc + 10, h, 'E');
        info.portal = new Vector3(0, h, zc + 10);

        b.Go(0, zc - 4);
        b.Boss();

        var def = ToDef(b, "THE ECLIPSE",
            $"BOSS - tier {tier}. Grab the light cells to strike the Eclipse. The ring has day and night quadrants - and the Eclipse can flip the world on you!",
            2.6f, info);
        def.theme = Themes.Boss;
        return def;
    }

    // ------------------------------------------------------------------ output

    static LevelDef ToDef(Builder b, string name, string hint, float orbSpeed, BossInfo boss)
    {
        int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue, maxL = 0;
        foreach (var k in b.cells.Keys)
        {
            minX = Mathf.Min(minX, k.x); maxX = Mathf.Max(maxX, k.x);
            minZ = Mathf.Min(minZ, k.z); maxZ = Mathf.Max(maxZ, k.z);
            maxL = Mathf.Max(maxL, k.y);
        }
        int depth = maxZ - minZ + 1;
        var layers = new string[maxL + 1][];
        var sb = new StringBuilder();
        for (int l = 0; l <= maxL; l++)
        {
            layers[l] = new string[depth];
            for (int r = 0; r < depth; r++)
            {
                int zz = minZ + depth - 1 - r;
                sb.Clear();
                for (int x = minX; x <= maxX; x++)
                    sb.Append(b.cells.TryGetValue(new Vector3Int(x, l, zz), out var c) ? c : '.');
                layers[l][r] = sb.ToString();
            }
        }

        var inv = CultureInfo.InvariantCulture;
        var sol = new StringBuilder();
        foreach (var c in b.sol)
        {
            switch (c.op)
            {
                case "go": sol.Append(string.Format(inv, "go {0:0.###} {1:0.###}; ", c.x - minX, c.z - minZ)); break;
                case "jump":
                    sol.Append(c.a >= 0
                        ? string.Format(inv, "jump {0:0.###} {1:0.###} {2:0.###}; ", c.x - minX, c.z - minZ, c.a)
                        : string.Format(inv, "jump {0:0.###} {1:0.###}; ", c.x - minX, c.z - minZ));
                    break;
                case "phase": sol.Append("phase ").Append(c.p).Append("; "); break;
                case "orbfar2":
                {
                    float line = c.ax == 'x' ? c.z - minZ : c.z - minX;
                    float gpos = c.ax == 'x' ? c.x - minX : c.x - minZ;
                    sol.Append(string.Format(inv, "orbfar2 {0} {1:0.###} {2:0.###} {3:0.###} {4}; ", c.ax, line, gpos, c.a, c.p));
                    break;
                }
                case "orbfar":   // unrotated (boss / legacy) – lanes along x
                    sol.Append(string.Format(inv, "orbfar2 x {0:0.###} {1:0.###} {2:0.###} {3}; ", c.z - minZ, c.x - minX, c.a, c.p == '\0' ? '-' : c.p));
                    break;
                case "boss": sol.Append("bossfight; "); break;
            }
        }

        if (boss != null)
        {
            var off = new Vector3(minX, 0, minZ);
            boss.center -= off;
            boss.portal -= off;
            for (int i = 0; i < boss.ringSpots.Count; i++) boss.ringSpots[i] -= off;
            for (int i = 0; i < boss.entry.Count; i++) boss.entry[i] = (boss.entry[i].pos - off, boss.entry[i].phase);
            foreach (var p in boss.platforms)
            {
                p.center -= off;
                p.ringPoint -= off;
                for (int i = 0; i < p.bridge.Length; i++) p.bridge[i] -= off;
            }
        }

        return new LevelDef(name, hint, layers) { orbSpeed = orbSpeed, solution = sol.ToString(), generated = true, boss = boss };
    }
}
