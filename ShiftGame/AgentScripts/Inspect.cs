using System.Text;
using UnityEngine;
public static class Inspect
{
    public static string Sol(int level, int from, int to)
    {
        var c = Levels.Get(level).solution.Split(';');
        var sb = new StringBuilder();
        for (int i = from; i < Mathf.Min(to, c.Length); i++) sb.Append(i).Append(':').Append(c[i].Trim()).Append(" | ");
        return sb.ToString();
    }
    public static string Area(int level, int x0, int x1, int z0, int z1)
    {
        var g = new LevelGrid(Levels.Get(level));
        var sb = new StringBuilder();
        for (int k = 0; k < g.layers; k++)
        {
            bool any = false; var lb = new StringBuilder();
            for (int z = z1; z >= z0; z--)
            {
                lb.Append($"z{z,3} ");
                for (int x = x0; x <= x1; x++) { char ch = g.Get(k, x, z); if (ch != '.') any = true; lb.Append(ch); }
                lb.AppendLine();
            }
            if (any) sb.Append($"layer {k}:\n").Append(lb);
        }
        return sb.ToString();
    }
}
