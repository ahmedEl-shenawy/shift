using UnityEngine;

public static class Palette
{
    public static readonly Color DayBlock = new Color(1.00f, 0.60f, 0.20f);
    public static readonly Color NightBlock = new Color(0.30f, 0.62f, 1.00f);

    public static readonly Color DaySky = new Color(0.60f, 0.80f, 0.93f);
    public static readonly Color NightSky = new Color(0.045f, 0.04f, 0.12f);

    public static readonly Color StoneA = new Color(0.43f, 0.40f, 0.53f);
    public static readonly Color StoneB = new Color(0.47f, 0.43f, 0.57f);
    public static readonly Color StoneC = new Color(0.40f, 0.38f, 0.51f);
    public static readonly Color Wall = new Color(0.34f, 0.31f, 0.44f);

    public static readonly Color CapDay = new Color(0.60f, 0.83f, 0.60f);
    public static readonly Color CapNight = new Color(0.26f, 0.60f, 0.62f);

    public static readonly Color Spike = new Color(0.90f, 0.25f, 0.38f);
    public static readonly Color Shard = new Color(1.00f, 0.93f, 0.55f);
    public static readonly Color PlayerBody = new Color(0.96f, 0.94f, 0.90f);
    public static readonly Color Visor = new Color(0.08f, 0.08f, 0.13f);

    public static Color PhaseColor(Phase p) => p == Phase.Day ? DayBlock : NightBlock;
    public static Color Current => Color.Lerp(DayBlock, NightBlock, PhaseState.Smooth);
}
