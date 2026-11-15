using UnityEngine;

/// <summary>Colour themes for levels. Day/night block colours never change (they are the core language of the game).</summary>
public class Theme
{
    public string name;
    public Color capDay, capNight, stoneA, stoneB, stoneC, wall, daySky, nightSky;
}

public static class Themes
{
    public const int Boss = 5;

    static Theme T(string name, Color capDay, Color capNight, Color stone, Color daySky, Color nightSky) => new Theme
    {
        name = name,
        capDay = capDay,
        capNight = capNight,
        stoneA = stone,
        stoneB = stone * 1.08f + new Color(0.01f, 0.01f, 0.02f),
        stoneC = stone * 0.93f,
        wall = stone * 0.78f,
        daySky = daySky,
        nightSky = nightSky,
    };

    public static readonly Theme[] All =
    {
        T("Meadow", new Color(0.60f, 0.83f, 0.60f), new Color(0.26f, 0.60f, 0.62f), new Color(0.43f, 0.40f, 0.53f),
            new Color(0.60f, 0.80f, 0.93f), new Color(0.045f, 0.04f, 0.12f)),
        T("Dunes", new Color(0.93f, 0.80f, 0.56f), new Color(0.45f, 0.48f, 0.70f), new Color(0.62f, 0.43f, 0.38f),
            new Color(0.97f, 0.80f, 0.64f), new Color(0.07f, 0.04f, 0.10f)),
        T("Frost", new Color(0.92f, 0.95f, 1.00f), new Color(0.55f, 0.70f, 0.90f), new Color(0.45f, 0.50f, 0.62f),
            new Color(0.76f, 0.87f, 0.96f), new Color(0.03f, 0.05f, 0.11f)),
        T("Dusk", new Color(0.80f, 0.68f, 0.93f), new Color(0.50f, 0.40f, 0.78f), new Color(0.40f, 0.33f, 0.52f),
            new Color(0.93f, 0.74f, 0.84f), new Color(0.06f, 0.03f, 0.10f)),
        T("Ember", new Color(0.88f, 0.60f, 0.60f), new Color(0.55f, 0.35f, 0.55f), new Color(0.45f, 0.36f, 0.36f),
            new Color(0.86f, 0.86f, 0.74f), new Color(0.07f, 0.03f, 0.05f)),
        T("Eclipse", new Color(0.62f, 0.52f, 0.78f), new Color(0.40f, 0.30f, 0.62f), new Color(0.27f, 0.23f, 0.36f),
            new Color(0.78f, 0.50f, 0.50f), new Color(0.04f, 0.02f, 0.07f)),
    };

    public static Theme Get(int i) => All[Mathf.Clamp(i, 0, All.Length - 1)];
}
