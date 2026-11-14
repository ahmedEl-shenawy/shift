using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// Endless mode: builds an infinite sequence of levels from hand-designed "chunks" that are solvable by
/// construction, chained by neutral pads. Difficulty rises with the level number: more chunks, narrower
/// bridges, more phase flips, more and faster orbs, denser spikes and more shards.
/// Every generated level also carries a scripted solution used by the automated playtest bot.
/// </summary>
public static class LevelGenerator
{
    // ------------------------------------------------------------------ builder

    struct Cmd
    {
        public string op; public float x, z, a; public char p;
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
        public void OrbFar(int laneZ, float x, float dist) => sol.Add(new Cmd { op = "orbfar", x = x, z = laneZ, a = dist });
    }

    static char Other(char p) => p == 'D' ? 'N' : 'D';
    static char RandPhase(System.Random r) => r.Next(2) == 0 ? 'D' : 'N';

    static readonly string[] Adjectives =
    {
        "Drifting", "Hollow", "Silent", "Broken", "Twilight", "Ember", "Frozen", "Shattered", "Restless", "Gilded",
        "Whispering", "Crooked", "Lonely", "Burning", "Velvet", "Sunken", "Restless", "Folded", "Distant", "Fading"
    };
    static readonly string[] Nouns =
    {
        "Steps", "Causeway", "Spire", "Crossing", "Gardens", "Vault", "Ridge", "Bridges", "Labyrinth", "Archive",
        "Orchard", "Lanterns", "Hollows", "Terrace", "Reach", "Meridian", "Observatory", "Threshold", "Cascade", "Eclipse"
    };

    /// <summary>Difficulty grade for a level index (0-based, counting the handmade campaign).</summary>
    public static int Difficulty(int index) => index - Levels.HandmadeCount + 3;

    // ------------------------------------------------------------------ main

    public static LevelDef Generate(int index)
    {
        int d = Difficulty(index);
        float t = Mathf.Clamp01(d / 24f);
        var rng = new System.Random(index * 92821 + 17);
        var b = new Builder();
        var used = new List<string>();

        int cx = 0, z = 0, h = 0;
        int chunks = Mathf.Min(3 + d / 2, 12);
        int shardsWanted = Mathf.Min(1 + d / 3, 6);
        int shardsPlaced = 0;

        // start pad
        Pad(b, cx, cx, z, h, 3);
        b.Ent(cx, z, h, 'S');
        b.Go(cx, z + 2);
        z += 3;

        string last = "";
        for (int i = 0; i < chunks; i++)
        {
            string type = Pick(rng, d, last);
            last = type;
            if (!used.Contains(type)) used.Add(type);

            switch (type)
            {
                case "run": Run(b, rng, d, t, cx, ref z, h); break;
                case "stones": Stones(b, rng, d, t, cx, ref z, h); break;
                case "stairs": Stairs(b, rng, d, t, cx, ref z, ref h); break;
                case "gate": Gate(b, rng, d, t, cx, ref z, h); break;
                case "bridge": OrbBridge(b, rng, d, cx, ref z, h); break;
                case "spikes": Spikes(b, rng, d, t, cx, ref z, h, shardsWanted, ref shardsPlaced); break;
            }

            // connector pad (with an optional lateral offset and an optional shard spur)
            int ncx = cx + (d >= 4 ? rng.Next(-2, 3) : 0);
            int padZ = z;
            Pad(b, cx, ncx, padZ, h, 3);
            b.Go(cx, padZ);
            int remainingPads = chunks - i;
            if (shardsPlaced < shardsWanted && (rng.NextDouble() < 0.5 || shardsWanted - shardsPlaced >= remainingPads))
            {
                Spur(b, rng, d, Mathf.Min(cx, ncx) - 1, Mathf.Max(cx, ncx) + 1, padZ + 1, h, cx);
                shardsPlaced++;
            }
            b.Ent(ncx, padZ + 2, h, 'C');
            b.Go(ncx, padZ + 2);
            cx = ncx;
            z = padZ + 3;
        }

        // exit
        Pad(b, cx, cx, z, h, 2);
        b.Ent(cx, z + 1, h, 'E');
        b.Go(cx, z + 1);

        float orbSpeed = 2.0f + Mathf.Min(d * 0.1f, 2.0f);
        string name = Adjectives[rng.Next(Adjectives.Length)] + " " + Nouns[rng.Next(Nouns.Length)];
        string hint = $"Endless depth {d - 2}.  {Describe(used)}  Shards: {shardsPlaced}.";
        return ToDef(b, name, hint, orbSpeed);
    }

    static string Pick(System.Random rng, int d, string last)
    {
        var pool = new List<string> { "run" };
        if (d >= 1) pool.Add("stones");
        if (d >= 2) pool.Add("stairs");
        if (d >= 3) pool.Add("gate");
        if (d >= 4) pool.Add("spikes");
        if (d >= 5) pool.Add("bridge");
        string pick;
        int guard = 0;
        do { pick = pool[rng.Next(pool.Count)]; } while (pick == last && pool.Count > 1 && guard++ < 10);
        return pick;
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
                case "stairs": words.Add("climbs"); break;
                case "gate": words.Add("gates"); break;
                case "spikes": words.Add("spikes"); break;
                case "bridge": words.Add("sentinels"); break;
            }
        }
        if (words.Count == 0) return "";
        var s = string.Join(", ", words);
        return char.ToUpper(s[0]) + s.Substring(1) + ".";
    }

    // ------------------------------------------------------------------ pieces

    static void Pad(Builder b, int xa, int xb, int z, int h, int depth)
    {
        int x0 = Mathf.Min(xa, xb) - 1, x1 = Mathf.Max(xa, xb) + 1;
        for (int zz = z; zz < z + depth; zz++)
        for (int x = x0; x <= x1; x++)
            b.Floor(x, zz, h, '#');
    }

    /// <summary>Side branch off a pad's middle row with a shard at its tip.</summary>
    static void Spur(Builder b, System.Random rng, int d, int leftEdge, int rightEdge, int zm, int h, int cx)
    {
        int s = rng.Next(2) == 0 ? -1 : 1;
        int edge = s < 0 ? leftEdge : rightEdge;
        int len = 2 + rng.Next(0, 2 + Mathf.Min(d / 6, 2));
        char p = RandPhase(rng);
        int tip = edge;
        for (int j = 1; j <= len; j++)
        {
            tip = edge + s * j;
            b.Floor(tip, zm, h, p);
        }
        b.Ent(tip, zm, h, '*');
        b.Go(edge, zm);
        b.Phase(p);
        b.Go(tip, zm);
        b.Go(edge, zm);
    }

    /// <summary>Straight bridge of alternating day/night segments.</summary>
    static void Run(Builder b, System.Random rng, int d, float t, int cx, ref int z, int h)
    {
        int w = d < 6 ? 3 : (rng.NextDouble() < 0.5 ? 1 : 3);
        int baseSeg = Mathf.Max(1, 3 - d / 5);
        int len = 4 + rng.Next(0, 4);
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

    /// <summary>Single floating tiles separated by gaps: jump, shift in mid-air, land.</summary>
    static void Stones(Builder b, System.Random rng, int d, float t, int cx, ref int z, int h)
    {
        int count = 2 + Mathf.Min(d / 3, 5);
        float flip = 0.4f + 0.5f * t;
        char p = RandPhase(rng);
        b.Phase(p);
        b.Go(cx, z - 1 + 0.3f);
        int zz = z + 1;
        char prev = p;
        for (int i = 0; i < count; i++)
        {
            char c = i == 0 ? p : (rng.NextDouble() < flip ? Other(prev) : prev);
            b.Floor(cx, zz, h, c);
            b.Jump(cx, zz, i > 0 && c != prev ? 0.25f : -1f);
            prev = c;
            zz += 2;
        }
        // last gap, then the next pad starts at zz
        b.Jump(cx, zz, -1f);
        z = zz;
    }

    /// <summary>Climb a staircase where every step may belong to a different world.</summary>
    static void Stairs(Builder b, System.Random rng, int d, float t, int cx, ref int z, ref int h)
    {
        int n = 2 + Mathf.Min(d / 5, 3);
        float flip = 0.4f + 0.5f * t;
        char prev = RandPhase(rng);
        b.Phase(prev);
        for (int i = 1; i <= n; i++)
        {
            char c = i == 1 ? prev : (rng.NextDouble() < flip ? Other(prev) : prev);
            b.Floor(cx, z + i - 1, h + i, c);
            b.Jump(cx, z + i - 1, i > 1 && c != prev ? 0.2f : -1f);
            prev = c;
        }
        z += n;
        h += n;
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
            char r = RandPhase(rng);                      // phase needed to pass this gate
            char qb = rng.NextDouble() < orbChance ? RandPhase(rng) : '-';
            if (qa == r && qb == r) qb = Other(r);        // never two dangerous lanes at one crossing

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

    /// <summary>A field of spikes and mixed-phase tiles; a BFS over (cell, phase) guarantees a route.</summary>
    static void Spikes(Builder b, System.Random rng, int d, float t, int cx, ref int z, int h, int shardsWanted, ref int shardsPlaced)
    {
        const int W = 5;
        int L = 3 + Mathf.Min(d / 4, 4);
        float density = 0.15f + 0.25f * t;
        float phaseProb = 0.15f + 0.35f * t;
        int x0 = cx - W / 2;
        int ei = cx - x0, ej = 0, xi = cx - x0, xj = L - 1;

        char[,] tile = new char[W, L];
        bool[,] spike = new bool[W, L];
        bool ok = false;
        for (int attempt = 0; attempt < 60 && !ok; attempt++)
        {
            float dens = attempt > 40 ? density * 0.5f : density;
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
        {
            for (int i = 0; i < W; i++) for (int j = 0; j < L; j++) { tile[i, j] = '#'; spike[i, j] = false; }
        }

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

    /// <summary>Shortest sequence of states from (si,sj,phase) to target cell, or null.</summary>
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
            // shift (only while standing on neutral ground)
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

    // ------------------------------------------------------------------ output

    static LevelDef ToDef(Builder b, string name, string hint, float orbSpeed)
    {
        int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue, maxL = 0;
        foreach (var k in b.cells.Keys)
        {
            minX = Mathf.Min(minX, k.x); maxX = Mathf.Max(maxX, k.x);
            minZ = Mathf.Min(minZ, k.z); maxZ = Mathf.Max(maxZ, k.z);
            maxL = Mathf.Max(maxL, k.y);
        }
        int width = maxX - minX + 1, depth = maxZ - minZ + 1;
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
            float x = c.x - minX, zz = c.z - minZ;
            switch (c.op)
            {
                case "go": sol.Append(string.Format(inv, "go {0:0.###} {1:0.###}; ", x, zz)); break;
                case "jump": sol.Append(c.a >= 0 ? string.Format(inv, "jump {0:0.###} {1:0.###} {2:0.###}; ", x, zz, c.a) : string.Format(inv, "jump {0:0.###} {1:0.###}; ", x, zz)); break;
                case "phase": sol.Append("phase ").Append(c.p).Append("; "); break;
                case "orbfar": sol.Append(string.Format(inv, "orbfar {0} {1:0.###} {2:0.###}; ", (int)zz, x, c.a)); break;
            }
        }
        return new LevelDef(name, hint, layers) { orbSpeed = orbSpeed, solution = sol.ToString(), generated = true };
    }
}
