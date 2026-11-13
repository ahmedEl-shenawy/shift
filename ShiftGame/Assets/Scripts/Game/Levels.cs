using UnityEngine;

/// <summary>
/// Level format: a list of layers, each a grid of rows (first row = far side).
/// Layer k blocks occupy y in [k-1, k]. Entities in layer k stand at y = k-1.
///   #  neutral block        D / N  day-only / night-only block
///   W  neutral wall (2 high) d / n  day-only / night-only wall (2 high)
///   S  start   E  exit portal   *  shard   ^  spikes
///   p / q  day / night orb patrolling along X      v / w  day / night orb patrolling along Z
/// </summary>
public class LevelDef
{
    public string name;
    public string hint;
    public string[][] layers;
    public LevelDef(string name, string hint, params string[][] layers)
    {
        this.name = name; this.hint = hint; this.layers = layers;
    }
}

public class LevelGrid
{
    readonly char[,,] cells; // [layer, x, z]
    public readonly int layers, width, depth;

    public LevelGrid(LevelDef def)
    {
        layers = def.layers.Length;
        depth = 0; width = 0;
        foreach (var l in def.layers)
        {
            depth = Mathf.Max(depth, l.Length);
            foreach (var row in l) width = Mathf.Max(width, row.Length);
        }
        cells = new char[layers, width, depth];
        for (int k = 0; k < layers; k++)
        for (int x = 0; x < width; x++)
        for (int z = 0; z < depth; z++)
        {
            var l = def.layers[k];
            int r = depth - 1 - z;
            char c = '.';
            if (r < l.Length && x < l[r].Length) c = l[r][x];
            cells[k, x, z] = c;
        }
    }

    public char Get(int k, int x, int z)
    {
        if (k < 0 || k >= layers || x < 0 || x >= width || z < 0 || z >= depth) return '.';
        return cells[k, x, z];
    }

    public static bool IsBlock(char c) => c == '#' || c == 'D' || c == 'N' || c == 'W' || c == 'd' || c == 'n';
    public static bool IsTall(char c) => c == 'W' || c == 'd' || c == 'n';

    /// <summary>Whether a block occupies layer k (including the upper half of a tall wall below).</summary>
    public bool Occupied(int k, int x, int z) => IsBlock(Get(k, x, z)) || IsTall(Get(k - 1, x, z));
}

public static class Levels
{
    public static readonly LevelDef[] All =
    {
        new LevelDef("First Shift",
            "Press SHIFT to flip between Day and Night. Amber exists by day, blue by night.",
            new[]
            {
                "###DDD###NNN###",
                "###DDD###NNN###",
                "###DDD###NNN###",
            },
            new[]
            {
                "...............",
                "S.......*.....E",
                "...............",
            }),

        new LevelDef("Walls of Light",
            "Walls obey the same rule. Collect every shard to open the portal.",
            new[]
            {
                "#######", "#######", "#######", "#######", "#######",
                "#######", "#######", "#######", "#######",
            },
            new[]
            {
                "...E...",
                ".......",
                "WWnnnWW",
                "...*...",
                ".......",
                "WWdddWW",
                ".......",
                "...S...",
                ".......",
            }),

        new LevelDef("Leap of Faith",
            "You can SHIFT in mid-air. Jump first, then flip the world under your feet.",
            new[]
            {
                "..###..", "..###..", "...N...", ".......", "...D...", ".......",
                "...N...", ".......", "...D...", ".......", "..###..", "..###..",
            },
            new[]
            {
                "...E...", ".......", ".......", ".......", ".......", ".......",
                ".......", ".......", ".......", ".......", ".......", "...S...",
            }),

        new LevelDef("Sentinels",
            "Orbs only hurt in their own phase. Wait for an opening, then shift.",
            new[]
            {
                "#################",
                "#################",
            },
            new[]
            {
                "WWWWWWWWWWWWWWWWW",
                "S....p..n..q.d.*E",
            }),

        new LevelDef("Ascent",
            "Climb. Every step is in a different world.",
            new[] { ".....", ".....", ".....", ".###.", ".###." },
            new[] { ".....", ".....", ".....", "..D..", "..S.." },
            new[] { ".....", ".....", "N.N..", ".....", "....." },
            new[] { ".###.", ".....", "*....", ".....", "....." },
            new[] { "..E..", ".....", ".....", ".....", "....." }),

        new LevelDef("Twin Loops",
            "One loop belongs to the night, the other to the day.",
            new[]
            {
                "NNN#DDD",
                "N.N#D.D",
                "NNN#DDD",
                "..###..",
                "..###..",
            },
            new[]
            {
                "*p.E..*",
                ".......",
                "...^...",
                ".......",
                "...S...",
            }),

        new LevelDef("Crossfire",
            "Gates and guardians. Read the rhythm.",
            new[]
            {
                "#########", "#########", "#########", "#########", "#########", "#########",
                "#########", "#########", "#########", "#########", "#########",
            },
            new[]
            {
                "....E....",
                "WWWWnWWWW",
                "q........",
                "WdWWWWWWW",
                "........p",
                "WWWWWWWnW",
                "*.......q",
                "WWWWdWWWW",
                ".........",
                "....S....",
                ".........",
            }),

        new LevelDef("The Last Shift",
            "Everything you've learned. Two shards, two worlds, one way home.",
            new[]
            {
                "###.......#####",
                "###NNDDDNN#####",
                "###.......#####",
                ".N.........D...",
                ".N.........D...",
                "###DDDNNN######",
                "###.......#####",
                "###.......#####",
                "###.......#####",
            },
            new[]
            {
                "...............",
                ".E....q......*.",
                "...............",
                "...............",
                "...............",
                ".....p.......^.",
                ".............^.",
                ".S..........*..",
                "...............",
            }),
    };
}
