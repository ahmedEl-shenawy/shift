using System;
using UnityEngine;

public enum Phase { Day = 0, Night = 1 }

/// <summary>Global day/night phase. Gameplay reads Current; visuals read the smoothed Blend.</summary>
public static class PhaseState
{
    public static Phase Current = Phase.Day;
    /// <summary>0 = full day, 1 = full night. Smoothed toward Current every frame by GameManager.</summary>
    public static float Blend;
    public static event Action<Phase> Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Current = Phase.Day;
        Blend = 0;
        Changed = null;
    }

    public static Phase Other(Phase p) => p == Phase.Day ? Phase.Night : Phase.Day;

    public static void Set(Phase p, bool instant)
    {
        Current = p;
        if (instant) Blend = p == Phase.Night ? 1f : 0f;
        Changed?.Invoke(p);
    }

    public static void Tick(float dt)
    {
        float target = Current == Phase.Night ? 1f : 0f;
        Blend = Mathf.MoveTowards(Blend, target, dt / 0.45f);
    }

    /// <summary>Smoothstepped blend for nicer colour transitions.</summary>
    public static float Smooth => Blend * Blend * (3f - 2f * Blend);
}
