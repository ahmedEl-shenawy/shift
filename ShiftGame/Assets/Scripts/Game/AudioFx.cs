using UnityEngine;

public class AudioFx : MonoBehaviour
{
    public static AudioFx I;

    AudioSource[] pool;
    int next;
    AudioSource musicDay, musicNight;
    public float musicVolume = 0.45f;

    public AudioClip jump, land, toNight, toDay, deny, collect, death, win, portalOpen, select, levelStart;
    public AudioClip bossShot, bossRing, bossHit;
    public AudioClip pop, star1, star2, star3, whoosh, cheer;

    void Awake()
    {
        I = this;
        pool = new AudioSource[8];
        for (int i = 0; i < pool.Length; i++)
        {
            pool[i] = gameObject.AddComponent<AudioSource>();
            pool[i].playOnAwake = false;
        }

        jump = Synth.Sweep("jump", 320, 720, 0.13f, 1, 0.22f);
        land = Synth.Sweep("land", 160, 60, 0.09f, 0, 0.35f, 0.6f);
        toNight = Synth.Sweep("toNight", 1400, 180, 0.45f, 0, 0.35f, 0.35f, 1.4f);
        toDay = Synth.Sweep("toDay", 180, 1400, 0.45f, 2, 0.30f, 0.35f, 1.4f);
        deny = Synth.Sweep("deny", 140, 110, 0.22f, 1, 0.25f, 0.1f, 0.6f);
        collect = Synth.Arp("collect", new float[] { 76, 79, 83, 88 }, 0.055f, 2, 0.28f);
        death = Synth.Sweep("death", 500, 40, 0.6f, 1, 0.3f, 0.5f, 1.2f);
        win = Synth.Arp("win", new float[] { 72, 76, 79, 84, 79, 84, 88 }, 0.09f, 2, 0.26f, 0.6f);
        portalOpen = Synth.Arp("portal", new float[] { 67, 74, 79, 86 }, 0.08f, 0, 0.3f, 0.5f);
        select = Synth.Sweep("select", 600, 900, 0.06f, 2, 0.2f);
        levelStart = Synth.Arp("start", new float[] { 60, 67, 72 }, 0.07f, 2, 0.22f, 0.4f);
        bossShot = Synth.Sweep("bossShot", 900, 380, 0.16f, 2, 0.16f, 0.15f);
        bossRing = Synth.Sweep("bossRing", 220, 70, 0.7f, 0, 0.35f, 0.3f, 1.2f);
        bossHit = Synth.Sweep("bossHit", 160, 45, 0.6f, 1, 0.35f, 0.5f, 1.4f);
        pop = Synth.Sweep("pop", 520, 980, 0.07f, 0, 0.22f, 0f, 2.5f);
        star1 = Synth.Arp("star1", new float[] { 84, 91 }, 0.06f, 2, 0.24f, 0.35f);
        star2 = Synth.Arp("star2", new float[] { 86, 93 }, 0.06f, 2, 0.24f, 0.35f);
        star3 = Synth.Arp("star3", new float[] { 88, 95, 100 }, 0.06f, 2, 0.26f, 0.45f);
        whoosh = Synth.Sweep("whoosh", 300, 1200, 0.28f, 0, 0.12f, 0.75f, 1.6f);
        cheer = Synth.Arp("cheer", new float[] { 72, 76, 79, 84, 88, 91, 96 }, 0.05f, 2, 0.2f, 0.5f);

        musicDay = gameObject.AddComponent<AudioSource>();
        musicNight = gameObject.AddComponent<AudioSource>();
        musicDay.clip = Synth.Music(false);
        musicNight.clip = Synth.Music(true);
        foreach (var m in new[] { musicDay, musicNight })
        {
            m.loop = true;
            m.playOnAwake = false;
            m.volume = 0;
        }
        musicDay.Play();
        musicNight.Play();
        musicNight.timeSamples = musicDay.timeSamples;
    }

    void Update()
    {
        float b = PhaseState.Smooth;
        musicDay.volume = (1 - b) * musicVolume;
        musicNight.volume = b * musicVolume;
    }

    public void PlayPitch(AudioClip c, float vol, float pitch)
    {
        if (c == null) return;
        var s = pool[next];
        next = (next + 1) % pool.Length;
        s.pitch = pitch;
        s.volume = vol;
        s.clip = c;
        s.Play();
    }

    public void Play(AudioClip c, float vol = 1f, float pitchVar = 0.05f)
    {
        if (c == null) return;
        var s = pool[next];
        next = (next + 1) % pool.Length;
        s.pitch = 1f + Random.Range(-pitchVar, pitchVar);
        s.volume = vol;
        s.clip = c;
        s.Play();
    }
}
