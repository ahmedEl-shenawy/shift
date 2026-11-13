using UnityEngine;

/// <summary>Tiny procedural synthesizer: every sound effect and both music loops are generated at startup.</summary>
public static class Synth
{
    const int SR = 44100;
    const int MusicSR = 22050;

    static float Wave(int type, double phase)
    {
        float p = (float)(phase - System.Math.Floor(phase));
        switch (type)
        {
            case 1: return p < 0.5f ? 0.8f : -0.8f;            // square
            case 2: return 4f * Mathf.Abs(p - 0.5f) - 1f;       // triangle
            case 3: return 2f * p - 1f;                          // saw
            default: return Mathf.Sin(p * Mathf.PI * 2f);       // sine
        }
    }

    static AudioClip Make(string name, float[] data, int sr)
    {
        var c = AudioClip.Create(name, data.Length, 1, sr, false);
        c.SetData(data, 0);
        return c;
    }

    static float Mtof(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);

    /// <summary>Pitch sweep with optional noise, exponential-ish decay.</summary>
    public static AudioClip Sweep(string name, float f0, float f1, float dur, int wave, float vol, float noise = 0f, float decayPow = 2f)
    {
        int n = Mathf.Max(1, (int)(dur * SR));
        var d = new float[n];
        double ph = 0;
        var rng = new System.Random(name.Length * 7919 + (int)f0);
        float lp = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float f = f0 * Mathf.Pow(f1 / f0, t);
            ph += f / SR;
            float env = Mathf.Min(1f, i / (SR * 0.004f)) * Mathf.Pow(1f - t, decayPow);
            float nz = (float)(rng.NextDouble() * 2 - 1);
            lp += (nz - lp) * 0.25f;
            float s = Wave(wave, ph) * (1 - noise) + lp * noise * 1.6f;
            d[i] = s * env * vol;
        }
        return Make(name, d, SR);
    }

    /// <summary>Quick arpeggio of notes (midi), each a plucked tone.</summary>
    public static AudioClip Arp(string name, float[] midi, float noteDur, int wave, float vol, float tail = 0.3f)
    {
        float total = noteDur * midi.Length + tail;
        int n = (int)(total * SR);
        var d = new float[n];
        for (int k = 0; k < midi.Length; k++)
        {
            float f = Mtof(midi[k]);
            int start = (int)(k * noteDur * SR);
            int len = (int)((noteDur + tail) * SR);
            double ph = 0;
            for (int i = 0; i < len && start + i < n; i++)
            {
                float t = i / (float)SR;
                ph += f / SR;
                float env = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t * 7f);
                d[start + i] += (Wave(wave, ph) * 0.7f + Wave(0, ph * 2) * 0.3f) * env * vol;
            }
        }
        return Make(name, d, SR);
    }

    /// <summary>Generates a seamless 8-bar loop. Day = major and bright; Night = parallel minor, softer, with echo.</summary>
    public static AudioClip Music(bool night)
    {
        const float bpm = 104f;
        float beat = 60f / bpm;
        float bar = beat * 4;
        int bars = 8;
        int n = (int)(bar * bars * MusicSR);
        var d = new float[n];

        int[][] chords = night
            ? new[] { new[] { 60, 63, 67 }, new[] { 56, 60, 63 }, new[] { 53, 56, 60 }, new[] { 55, 59, 62 } }
            : new[] { new[] { 60, 64, 67 }, new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 } };
        int[] arpPattern = night ? new[] { 0, 2, 1, 2, 0, 2, 1, 3 } : new[] { 0, 1, 2, 3, 2, 1, 2, 3 };

        var rng = new System.Random(night ? 99 : 42);
        var arpBuf = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)MusicSR;
            int barIdx = (int)(t / bar) % bars;
            float tb = t - barIdx * bar;
            int[] ch = chords[barIdx % 4];

            // pad
            float env = Mathf.Min(1f, tb / 0.5f) * Mathf.Min(1f, (bar - tb) / 0.25f);
            float pad = 0;
            for (int k = 0; k < 3; k++)
            {
                float f = Mtof(ch[k]);
                pad += Mathf.Sin(2 * Mathf.PI * f * 1.0015f * t) + Mathf.Sin(2 * Mathf.PI * f * 0.9985f * t);
            }
            pad *= (night ? 0.045f : 0.035f) * env;

            // bass: root, beats 1 and 3 (night: 1 only, longer)
            float bass = 0;
            float beatPos = tb / beat;
            int beatIdx = (int)beatPos;
            float tbeat = (beatPos - beatIdx) * beat;
            if (!night ? (beatIdx == 0 || beatIdx == 2) : beatIdx == 0)
            {
                float f = Mtof(ch[0] - 24);
                float be = Mathf.Min(1f, tbeat / 0.01f) * Mathf.Exp(-tbeat * (night ? 1.6f : 3.5f));
                bass = (Mathf.Sin(2 * Mathf.PI * f * t) + 0.3f * Mathf.Sin(4 * Mathf.PI * f * t)) * be * 0.22f;
            }
            else if (night && beatIdx == 2)
            {
                float f = Mtof(ch[0] - 24);
                float be = Mathf.Min(1f, tbeat / 0.01f) * Mathf.Exp(-tbeat * 2.5f);
                bass = Mathf.Sin(2 * Mathf.PI * f * t) * be * 0.12f;
            }

            // arp: 8th notes
            float eighth = beat * 0.5f;
            int e = (int)(tb / eighth);
            float te = tb - e * eighth;
            int idx = arpPattern[e % 8];
            int note = idx < 3 ? ch[idx] + 12 : ch[0] + 24;
            float af = Mtof(note);
            float ae = Mathf.Min(1f, te / 0.004f) * Mathf.Exp(-te * (night ? 5f : 9f));
            float arp = night
                ? Mathf.Sin(2 * Mathf.PI * af * t) * 0.11f * ae
                : (Wave(2, af * t) * 0.6f + Mathf.Sin(2 * Mathf.PI * af * 2 * t) * 0.15f) * 0.12f * ae;
            arpBuf[i] = arp;

            // hats (day only, off-beats) / soft tick (night)
            float hat = 0;
            if (!night)
            {
                float off = (tb + beat * 0.5f) % beat;
                if (off < 0.05f) hat = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-off * 90f) * 0.05f;
            }

            d[i] = pad + bass + hat;
        }

        // arp with echo (wraps around for a seamless loop)
        int delay = (int)(beat * 0.75f * MusicSR);
        float fb = night ? 0.45f : 0.25f;
        for (int i = 0; i < n; i++)
        {
            float s = arpBuf[i];
            s += arpBuf[(i - delay + n) % n] * fb;
            s += arpBuf[(i - 2 * delay + 2 * n) % n] * fb * fb;
            d[i] += s;
        }

        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(d[i]));
        float g = 0.8f / peak;
        for (int i = 0; i < n; i++) d[i] *= g;

        return Make(night ? "music_night" : "music_day", d, MusicSR);
    }
}
