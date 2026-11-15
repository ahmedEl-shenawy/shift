using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Bootstraps the whole game at runtime (camera, lights, post FX, UI, audio) and runs the state machine.
/// The scene only needs a single GameObject with this component.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager I;

    public enum State { Title, Playing, Dying, Complete, Victory, Paused }
    public State state { get; private set; }

    // world
    Camera cam;
    CameraRig rig;
    Light sun;
    Hud hud;
    AudioFx sfx;

    // level
    Transform levelRoot;
    LevelGrid grid;
    public int levelIndex { get; private set; }
    public PlayerController player { get; private set; }
    Portal portal;
    Vector3 startPos, portalPos;
    readonly List<Block> blocks = new List<Block>();
    readonly List<Block> phaseBlocks = new List<Block>();
    readonly List<Orb> orbs = new List<Orb>();
    readonly List<Shard> shards = new List<Shard>();
    readonly List<Vector3> spikes = new List<Vector3>();
    readonly List<Checkpoint> checkpoints = new List<Checkpoint>();
    BossController boss;
    Theme theme = Themes.Get(0);
    public BossController Boss => boss;
    public Transform LevelRoot => levelRoot;
    public static bool DebugInvulnerable;
    Checkpoint activeCheckpoint;
    Vector3 levelCenter;
    float levelSize, levelWidth;
    Vector3 levelMin, levelMax;
    float orbSpeed = 2.6f;
    public int shardsCollected { get; private set; }
    public int ShardTotal => shards.Count;

    // stats
    float levelTime; int levelShifts, levelDeaths;
    float totalTime; int totalShifts, totalDeaths;

    // misc
    float shiftCooldown, dyingTimer, titleShiftTimer, shiftBuffer;
    int selectedLevel, unlocked;
    float fadeA = 1f;
    bool transitioning;
    const string PrefKey = "shift_unlocked";
    const string CurrentKey = "shift_current";
    int currentLevel;

    public int Unlocked => unlocked;
    public int CurrentLevel => currentLevel;
    public int SelectedLevel => selectedLevel;

    // materials
    Material dayBlock, dayGhost, dayFaded, nightBlock, nightGhost, nightFaded;
    Material wallMat, wallFaded, capMat, stoneFaded, spikeMat, shardMat, padMat;
    Material[] stoneMats;
    Material orbDay, orbDayGhost, orbNight, orbNightGhost;
    Material portalLocked, portalOpen, coreLocked, coreOpen;
    Material playerBody, visorMat, debrisMat;
    ParticleSystem dayMotes, nightMotes;
    Material dayMoteMat, nightMoteMat;

    void Awake()
    {
        I = this;
        Application.targetFrameRate = 60;
        Time.timeScale = 1f;
        BuildWorld();
        BuildMaterials();
        BuildAmbience();

        var hudGo = new GameObject("UI");
        hud = hudGo.AddComponent<Hud>();
        hud.gm = this;
        hud.Build();

        sfx = gameObject.AddComponent<AudioFx>();

        unlocked = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 1), 1, 9999);
        currentLevel = Mathf.Clamp(PlayerPrefs.GetInt(CurrentKey, 0), 0, unlocked - 1);
        selectedLevel = currentLevel;
    }

    public void DebugUnlock(int n)
    {
        unlocked = Mathf.Max(1, n);
        PlayerPrefs.SetInt(PrefKey, unlocked);
        selectedLevel = unlocked - 1;
    }

    void Start()
    {
        EnterTitle();
    }

    // ------------------------------------------------------------------ setup

    void BuildWorld()
    {
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.fieldOfView = 38f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 150f;
        camGo.AddComponent<AudioListener>();
        var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        rig = camGo.AddComponent<CameraRig>();

        var sunGo = new GameObject("Sun");
        sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.8f;
        sunGo.transform.rotation = Quaternion.Euler(50, -40, 0);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 22f;
        RenderSettings.fogEndDistance = 55f;

        var volGo = new GameObject("PostFX");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 10;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.9f);
        bloom.threshold.Override(0.95f);
        bloom.scatter.Override(0.72f);
        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.3f);
        vig.smoothness.Override(0.45f);
        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);
        var ca = profile.Add<ColorAdjustments>(true);
        ca.saturation.Override(12f);
        ca.contrast.Override(8f);
        vol.profile = profile;
    }

    void BuildMaterials()
    {
        dayBlock = Mats.Lit(Palette.DayBlock, 0.35f, 0.45f);
        dayGhost = Mats.Transparent(Palette.DayBlock, 0.16f, 0.5f);
        dayFaded = Mats.Transparent(Palette.DayBlock, 0.4f, 0.35f);
        nightBlock = Mats.Lit(Palette.NightBlock, 0.45f, 0.5f);
        nightGhost = Mats.Transparent(Palette.NightBlock, 0.16f, 0.6f);
        nightFaded = Mats.Transparent(Palette.NightBlock, 0.4f, 0.45f);
        wallMat = Mats.Lit(Palette.Wall, 0f, 0.2f);
        wallFaded = Mats.Transparent(Palette.Wall, 0.28f);
        stoneFaded = Mats.Transparent(Palette.StoneA, 0.3f);
        capMat = Mats.Lit(Palette.CapDay, 0f, 0.25f);
        stoneMats = new[] { Mats.Lit(Palette.StoneA, 0, 0.2f), Mats.Lit(Palette.StoneB, 0, 0.2f), Mats.Lit(Palette.StoneC, 0, 0.2f) };
        spikeMat = Mats.Lit(Palette.Spike, 0.25f, 0.7f);
        shardMat = Mats.Lit(Palette.Shard, 1.8f, 0.9f);
        padMat = Mats.Lit(new Color(0.22f, 0.2f, 0.3f), 0f, 0.4f);
        orbDay = Mats.Lit(Color.Lerp(Palette.DayBlock, Color.red, 0.25f), 2.2f, 0.8f);
        orbDayGhost = Mats.Transparent(Palette.DayBlock, 0.2f, 0.4f);
        orbNight = Mats.Lit(Color.Lerp(Palette.NightBlock, new Color(0.5f, 0.2f, 1f), 0.3f), 2.5f, 0.8f);
        orbNightGhost = Mats.Transparent(Palette.NightBlock, 0.2f, 0.4f);
        portalLocked = Mats.Lit(new Color(0.45f, 0.45f, 0.55f), 0.1f, 0.6f);
        portalOpen = Mats.Lit(new Color(1f, 0.97f, 0.85f), 2.2f, 0.9f);
        coreLocked = Mats.Transparent(new Color(0.6f, 0.6f, 0.7f), 0.35f, 0.2f);
        coreOpen = Mats.Lit(new Color(1f, 1f, 1f), 3.5f, 1f);
        playerBody = Mats.Lit(Palette.PlayerBody, 0f, 0.55f);
        visorMat = Mats.Lit(Palette.Visor, 0f, 0.85f);
        debrisMat = Mats.Lit(Palette.PlayerBody, 0.6f, 0.5f);
    }

    ParticleSystem MakeMotes(string name, Material mat, float size, float rate)
    {
        var go = new GameObject(name);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.maxParticles = 400;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        em.rateOverTime = rate;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(34, 10, 34);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
        col.color = grad;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.25f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    void BuildAmbience()
    {
        dayMoteMat = Mats.Particle(new Color(1f, 0.85f, 0.5f, 0.6f));
        nightMoteMat = Mats.Particle(new Color(0.5f, 0.9f, 1f, 1f));
        dayMotes = MakeMotes("DayMotes", dayMoteMat, 0.12f, 30);
        nightMotes = MakeMotes("NightMotes", nightMoteMat, 0.16f, 40);
    }

    // ------------------------------------------------------------------ level building

    void ClearLevel()
    {
        if (levelRoot != null) Destroy(levelRoot.gameObject);
        blocks.Clear(); phaseBlocks.Clear(); orbs.Clear(); shards.Clear(); spikes.Clear(); checkpoints.Clear(); activeCheckpoint = null;
        player = null; portal = null; boss = null;
    }

    void ApplyTheme(Theme th)
    {
        theme = th;
        var stones = new[] { th.stoneA, th.stoneB, th.stoneC };
        for (int i = 0; i < stoneMats.Length; i++) Mats.SetColor(stoneMats[i], stones[i], 0f);
        Mats.SetColor(wallMat, th.wall, 0f);
        var wf = th.wall; wf.a = 0.28f; wallFaded.SetColor("_BaseColor", wf);
        var sf = th.stoneA; sf.a = 0.3f; stoneFaded.SetColor("_BaseColor", sf);
    }

    void BuildLevel(int index)
    {
        ClearLevel();
        var def = Levels.Get(index);
        orbSpeed = def.orbSpeed;
        grid = new LevelGrid(def);
        levelRoot = new GameObject("Level " + (index + 1)).transform;
        ApplyTheme(Themes.Get(def.theme));
        PhaseState.Set(def.startPhase, true);

        Vector3 min = new Vector3(999, 999, 999), max = -min;
        for (int k = 0; k < grid.layers; k++)
        for (int x = 0; x < grid.width; x++)
        for (int z = 0; z < grid.depth; z++)
        {
            char c = grid.Get(k, x, z);
            if (c == '.' || c == ' ') continue;
            var p = new Vector3(x, k - 1, z);
            min = Vector3.Min(min, p); max = Vector3.Max(max, p);
            switch (c)
            {
                case '#': case 'D': case 'N': case 'W': case 'd': case 'n':
                    AddBlock(x, k, z, c); break;
                case 'S': startPos = p; break;
                case 'E': AddPortal(p); break;
                case '*': AddShard(p); break;
                case '^': AddSpikes(p); break;
                case 'C': AddCheckpoint(p); break;
                case 'p': AddOrb(x, k, z, Phase.Day, new Vector2Int(1, 0)); break;
                case 'q': AddOrb(x, k, z, Phase.Night, new Vector2Int(1, 0)); break;
                case 'v': AddOrb(x, k, z, Phase.Day, new Vector2Int(0, 1)); break;
                case 'w': AddOrb(x, k, z, Phase.Night, new Vector2Int(0, 1)); break;
            }
        }
        levelCenter = (min + max) * 0.5f;
        levelSize = Mathf.Max(max.x - min.x, max.z - min.z);
        levelWidth = max.x - min.x;
        levelMin = min; levelMax = max;

        foreach (var b in phaseBlocks) b.Apply(true, Vector3.zero);
        if (portal != null) portal.SetOpen(shards.Count == 0 && def.boss == null, true);

        if (def.boss != null)
        {
            var bossGo = new GameObject("Boss");
            bossGo.transform.SetParent(levelRoot, false);
            boss = bossGo.AddComponent<BossController>();
            boss.Init(this, def.boss);
        }

        AddIslands(index);
        dayMotes.transform.position = levelCenter + Vector3.up * 2;
        nightMotes.transform.position = levelCenter + Vector3.up * 2;
    }

    void AddBlock(int x, int k, int z, char c)
    {
        bool tall = LevelGrid.IsTall(c);
        float h = tall ? 2f : 1f;
        var center = new Vector3(x, k - 1 + h * 0.5f, z);
        var go = new GameObject("Block " + c);
        go.transform.SetParent(levelRoot, false);
        go.transform.position = center;
        var bc = go.AddComponent<BoxCollider>();
        bc.size = new Vector3(1, h, 1);

        var vis = new GameObject("Vis").transform;
        vis.SetParent(go.transform, false);
        vis.gameObject.AddComponent<MeshFilter>().sharedMesh = MeshFactory.RoundedBox(new Vector3(0.96f, h - 0.04f, 0.96f), 0.1f, 3);
        var mr = vis.gameObject.AddComponent<MeshRenderer>();

        var b = go.AddComponent<Block>();
        b.visual = vis; b.rend = mr; b.col = bc; b.tall = tall;
        b.bounds = new Bounds(center, new Vector3(0.98f, h - 0.02f, 0.98f));

        bool topFree = !grid.Occupied(k + (tall ? 2 : 1), x, z);
        switch (c)
        {
            case '#':
                b.solid = stoneMats[Mathf.Abs(x * 73856093 ^ z * 19349663 ^ k * 83492791) % stoneMats.Length];
                b.faded = stoneFaded;
                if (topFree) MeshFactory.Part("Cap", vis, MeshFactory.RoundedBox(new Vector3(0.99f, 0.16f, 0.99f), 0.07f, 2), capMat, new Vector3(0, h * 0.5f - 0.07f, 0));
                break;
            case 'W':
                b.solid = wallMat; b.faded = wallFaded;
                break;
            case 'D': case 'd':
                b.hasPhase = true; b.phase = Phase.Day;
                b.solid = dayBlock; b.ghost = dayGhost; b.faded = dayFaded;
                break;
            case 'N': case 'n':
                b.hasPhase = true; b.phase = Phase.Night;
                b.solid = nightBlock; b.ghost = nightGhost; b.faded = nightFaded;
                break;
        }
        mr.sharedMaterial = b.solid;
        blocks.Add(b);
        if (b.hasPhase) phaseBlocks.Add(b);
    }

    void AddPortal(Vector3 p)
    {
        portalPos = p;
        var go = new GameObject("Portal");
        go.transform.SetParent(levelRoot, false);
        go.transform.position = p;
        portal = go.AddComponent<Portal>();
        MeshFactory.Part("Pad", go.transform, MeshFactory.RoundedBox(new Vector3(0.9f, 0.1f, 0.9f), 0.04f, 2), padMat, new Vector3(0, 0.05f, 0));

        var ringHolder = new GameObject("RingHolder").transform;
        ringHolder.SetParent(go.transform, false);
        ringHolder.localPosition = new Vector3(0, 0.95f, 0);
        var ring = MeshFactory.Part("Ring", ringHolder, MeshFactory.Torus(0.55f, 0.06f), portalLocked, Vector3.zero);
        ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
        var ring2Holder = new GameObject("Ring2Holder").transform;
        ring2Holder.SetParent(go.transform, false);
        ring2Holder.localPosition = new Vector3(0, 0.95f, 0);
        var ring2 = MeshFactory.Part("Ring2", ring2Holder, MeshFactory.Torus(0.42f, 0.035f), portalLocked, Vector3.zero);
        ring2.transform.localRotation = Quaternion.Euler(0, 0, 90);
        var core = MeshFactory.Part("Core", go.transform, MeshFactory.Sphere(0.5f), coreLocked, new Vector3(0, 0.95f, 0), false);

        portal.ring = ringHolder;
        portal.ring2 = ring2Holder;
        portal.core = core.transform;
        portal.ringRend = ring.GetComponent<Renderer>();
        portal.ring2Rend = ring2.GetComponent<Renderer>();
        portal.coreRend = core.GetComponent<Renderer>();
        portal.lockedMat = portalLocked; portal.openMat = portalOpen;
        portal.coreLocked = coreLocked; portal.coreOpen = coreOpen;
    }

    void AddShard(Vector3 p)
    {
        var go = new GameObject("Shard");
        go.transform.SetParent(levelRoot, false);
        go.transform.position = p;
        MeshFactory.Part("Gem", go.transform, MeshFactory.Octahedron(0.2f, 0.32f), shardMat, Vector3.zero);
        shards.Add(go.AddComponent<Shard>());
    }

    void AddCheckpoint(Vector3 p)
    {
        var go = new GameObject("Checkpoint");
        go.transform.SetParent(levelRoot, false);
        go.transform.position = p;
        MeshFactory.Part("Base", go.transform, MeshFactory.RoundedBox(new Vector3(0.3f, 0.08f, 0.3f), 0.03f, 1), padMat, new Vector3(0, 0.04f, 0));
        MeshFactory.Part("Pole", go.transform, MeshFactory.RoundedBox(new Vector3(0.06f, 1.0f, 0.06f), 0.025f, 1), padMat, new Vector3(0, 0.5f, 0));
        var flag = MeshFactory.Part("Flag", go.transform, MeshFactory.RoundedBox(new Vector3(0.36f, 0.22f, 0.04f), 0.02f, 1), portalLocked, new Vector3(0.19f, 0.86f, 0));
        var cp = go.AddComponent<Checkpoint>();
        cp.flag = flag.GetComponent<Renderer>();
        cp.activeMat = shardMat;
        checkpoints.Add(cp);
    }

    void AddSpikes(Vector3 p)
    {
        var go = new GameObject("Spikes");
        go.transform.SetParent(levelRoot, false);
        go.transform.position = p;
        var mesh = MeshFactory.Pyramid(0.36f, 0.5f);
        for (int i = 0; i < 4; i++)
        {
            float ox = (i % 2 == 0 ? -1 : 1) * 0.22f, oz = (i < 2 ? -1 : 1) * 0.22f;
            MeshFactory.Part("Spike", go.transform, mesh, spikeMat, new Vector3(ox, 0, oz));
        }
        spikes.Add(p);
    }

    void AddOrb(int x, int k, int z, Phase ph, Vector2Int dir)
    {
        var go = new GameObject("Orb " + ph);
        go.transform.SetParent(levelRoot, false);
        var orb = go.AddComponent<Orb>();
        orb.phase = ph; orb.grid = grid; orb.layer = k;
        orb.cell = new Vector2Int(x, z); orb.dir = dir; orb.standY = k - 1; orb.speed = orbSpeed;
        orb.active = ph == Phase.Day ? orbDay : orbNight;
        orb.ghost = ph == Phase.Day ? orbDayGhost : orbNightGhost;
        var coreGo = MeshFactory.Part("Core", go.transform, MeshFactory.Sphere(0.24f, 18), orb.active, Vector3.zero, false);
        var ringGo = MeshFactory.Part("Ring", coreGo.transform, MeshFactory.Torus(0.38f, 0.03f, 32, 8), orb.active, Vector3.zero, false);
        ringGo.transform.localRotation = Quaternion.Euler(18, 0, 0);
        orb.core = coreGo.transform;
        orb.ring = ringGo.transform;
        orb.rend = coreGo.GetComponent<Renderer>();
        orb.ringRend = ringGo.GetComponent<Renderer>();
        orb.Begin();
        orbs.Add(orb);
    }

    void AddIslands(int seed)
    {
        var rng = new System.Random(seed * 31 + 7);
        int count = 16 + (int)(levelSize * 0.6f);
        for (int i = 0; i < count; i++)
        {
            Vector3 p;
            int guard = 0;
            do
            {
                p = new Vector3(
                    Mathf.Lerp(levelMin.x - 18, levelMax.x + 18, (float)rng.NextDouble()),
                    levelMin.y - 4 - (float)rng.NextDouble() * 10,
                    Mathf.Lerp(levelMin.z - 14, levelMax.z + 14, (float)rng.NextDouble()));
            } while (guard++ < 20 && p.x > levelMin.x - 4 && p.x < levelMax.x + 4 && p.z > levelMin.z - 4 && p.z < levelMax.z + 4);
            var s = new Vector3(1 + (float)rng.NextDouble() * 3, 0.8f + (float)rng.NextDouble() * 2.5f, 1 + (float)rng.NextDouble() * 3);
            var go = MeshFactory.Part("Island", levelRoot, MeshFactory.RoundedBox(s, 0.25f, 2), stoneMats[i % stoneMats.Length], p, false);
            go.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 90, 0);
            MeshFactory.Part("IslandCap", go.transform, MeshFactory.RoundedBox(new Vector3(s.x + 0.04f, 0.25f, s.z + 0.04f), 0.1f, 2), capMat, new Vector3(0, s.y * 0.5f - 0.1f, 0), false);
        }
    }

    void SpawnPlayer()
    {
        var go = new GameObject("Player");
        go.transform.SetParent(levelRoot, false);
        go.transform.position = startPos + Vector3.up * 0.05f;
        go.AddComponent<CharacterController>();
        var pc = go.AddComponent<PlayerController>();
        var vis = new GameObject("Visual").transform;
        vis.SetParent(go.transform, false);
        vis.rotation = Quaternion.Euler(0, 180 + rig.yaw, 0);

        var eyeMat = Mats.Lit(Palette.DayBlock, 3f, 0.9f);
        MeshFactory.Part("Body", vis, MeshFactory.RoundedBox(new Vector3(0.56f, 0.6f, 0.5f), 0.17f, 3), playerBody, new Vector3(0, 0.36f, 0));
        MeshFactory.Part("Visor", vis, MeshFactory.RoundedBox(new Vector3(0.44f, 0.2f, 0.08f), 0.05f, 2), visorMat, new Vector3(0, 0.42f, 0.23f));
        var e1 = MeshFactory.Part("EyeL", vis, MeshFactory.RoundedBox(new Vector3(0.09f, 0.1f, 0.04f), 0.02f, 1), eyeMat, new Vector3(-0.1f, 0.42f, 0.27f), false);
        var e2 = MeshFactory.Part("EyeR", vis, MeshFactory.RoundedBox(new Vector3(0.09f, 0.1f, 0.04f), 0.02f, 1), eyeMat, new Vector3(0.1f, 0.42f, 0.27f), false);
        MeshFactory.Part("Stalk", vis, MeshFactory.RoundedBox(new Vector3(0.04f, 0.18f, 0.04f), 0.015f, 1), visorMat, new Vector3(0, 0.74f, 0));
        MeshFactory.Part("Bulb", vis, MeshFactory.Sphere(0.075f, 12), eyeMat, new Vector3(0, 0.85f, 0), false);
        MeshFactory.Part("FootL", vis, MeshFactory.RoundedBox(new Vector3(0.16f, 0.08f, 0.22f), 0.035f, 1), visorMat, new Vector3(-0.14f, 0.04f, 0.02f));
        MeshFactory.Part("FootR", vis, MeshFactory.RoundedBox(new Vector3(0.16f, 0.08f, 0.22f), 0.035f, 1), visorMat, new Vector3(0.14f, 0.04f, 0.02f));

        var lightGo = new GameObject("Glow");
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.localPosition = new Vector3(0, 0.9f, 0);
        var glow = lightGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.range = 3.5f;
        glow.shadows = LightShadows.None;

        pc.visual = vis;
        pc.eyeMat = eyeMat;
        pc.eyeRenderers = new[] { e1.GetComponent<Renderer>(), e2.GetComponent<Renderer>() };
        pc.glow = glow;
        pc.cameraTransform = cam.transform;
        pc.Jumped += () => sfx.Play(sfx.jump, 0.8f, 0.08f);
        pc.Landed += () => sfx.Play(sfx.land, 0.7f, 0.1f);
        player = pc;
    }

    // ------------------------------------------------------------------ flow

    void EnterTitle()
    {
        Time.timeScale = 1f;
        state = State.Title;
        BuildLevel(Levels.HandmadeCount - 1);
        rig.target = null;
        rig.focusPoint = levelCenter;
        rig.distance = 24f;
        rig.pitch = 38f;
        rig.orbit = true;
        rig.Snap();
        selectedLevel = Mathf.Clamp(currentLevel, 0, unlocked - 1);
        hud.ShowTitle();
        titleShiftTimer = 0;
    }

    void StartLevel(int index, bool resetStats)
    {
        levelIndex = index;
        BuildLevel(index);
        SpawnPlayer();
        rig.target = player.transform;
        rig.orbit = false;
        rig.yaw = 30f;
        rig.pitch = 56f;
        rig.distance = 12.5f + Mathf.Clamp(levelWidth - 8, 0, 10) * 0.3f;
        rig.biasWeight = 0f;
        if (boss != null)
        {
            rig.distance = 18f;
            rig.bias = boss.info.center + Vector3.up * 1.5f;
            rig.biasWeight = 0.35f;
        }
        rig.Snap();
        if (resetStats) { levelTime = 0; levelShifts = 0; levelDeaths = 0; }
        shiftBuffer = 0;
        shardsCollected = 0;
        state = State.Playing;
        hud.ShowHud(index, Levels.Get(index));
        if (boss != null) hud.ShowBoss("THE ECLIPSE", boss.maxHp, boss.info.tier);
        currentLevel = index;
        PlayerPrefs.SetInt(CurrentKey, currentLevel);
        hud.SetStats(levelTime, levelDeaths);
        UpdateShardText();
        sfx.Play(sfx.levelStart, 0.7f, 0f);
    }

    void RespawnAtCheckpoint()
    {
        player.Teleport(activeCheckpoint.transform.position + Vector3.up * 0.05f);
        player.visual.gameObject.SetActive(true);
        player.visual.localScale = Vector3.one;
        player.alive = true;
        player.controllable = true;
        rig.target = player.transform;
        rig.Snap();
        shiftBuffer = 0;
        if (boss != null) boss.ClearThreats();
        state = State.Playing;
        sfx.Play(sfx.levelStart, 0.6f, 0f);
    }

    void UpdateShardText()
    {
        if (boss == null) hud.SetShards(shardsCollected, shards.Count);
    }

    IEnumerator Transition(System.Action mid)
    {
        transitioning = true;
        while (fadeA < 1f)
        {
            fadeA = Mathf.MoveTowards(fadeA, 1f, Time.unscaledDeltaTime * 3.5f);
            yield return null;
        }
        mid();
        yield return null;
        transitioning = false;
    }

    // ------------------------------------------------------------------ update

    void Update()
    {
        float dt = Time.deltaTime;
        PhaseState.Tick(dt);
        shiftCooldown -= dt;
        UpdateAtmosphere();

        Menu.GlobalLock = transitioning;
        if (!transitioning) fadeA = Mathf.MoveTowards(fadeA, 0f, Time.unscaledDeltaTime * 2.5f);
        var fc = hud.fade.color; fc.a = fadeA; hud.fade.color = fc;
        if (transitioning) return;

        switch (state)
        {
            case State.Title: UpdateTitle(dt); break;
            case State.Playing: UpdatePlaying(dt); break;
            case State.Dying:
                dyingTimer -= dt;
                if (dyingTimer <= 0)
                {
                    if (activeCheckpoint != null) StartCoroutine(Transition(RespawnAtCheckpoint));
                    else StartCoroutine(Transition(() => StartLevel(levelIndex, false)));
                }
                break;
            // Complete and Paused are driven entirely by the menus in Hud
        }
    }

    void UpdateAtmosphere()
    {
        float b = PhaseState.Smooth;
        Color sky = Color.Lerp(theme.daySky, theme.nightSky, b);
        cam.backgroundColor = sky;
        RenderSettings.fogColor = sky;
        RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.78f, 0.82f, 0.95f), new Color(0.34f, 0.36f, 0.66f), b);
        RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.62f, 0.6f, 0.62f), new Color(0.18f, 0.17f, 0.36f), b);
        RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.35f, 0.3f, 0.32f), new Color(0.05f, 0.05f, 0.12f), b);
        sun.color = Color.Lerp(new Color(1f, 0.92f, 0.8f), new Color(0.55f, 0.65f, 1f), b);
        sun.intensity = Mathf.Lerp(1.45f, 0.9f, b);
        sun.transform.rotation = Quaternion.Slerp(Quaternion.Euler(50, -40, 0), Quaternion.Euler(40, 120, 0), b);
        Mats.SetColor(capMat, Color.Lerp(theme.capDay, theme.capNight, b), 0.05f * b);
        dayMoteMat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.5f, 0.55f * (1 - b)));
        nightMoteMat.SetColor("_BaseColor", new Color(0.5f, 0.9f, 1f, 1f * b));
        if (player != null)
        {
            var mp = player.transform.position + Vector3.up * 2;
            dayMotes.transform.position = mp;
            nightMotes.transform.position = mp;
        }
    }

    void UpdateTitle(float dt)
    {
        titleShiftTimer += dt;
        bool pressed = GameInput.ShiftPressed;
        if (pressed || titleShiftTimer > 3.5f)
        {
            titleShiftTimer = 0;
            DoShift(PhaseState.Other(PhaseState.Current), levelCenter, pressed);
        }
    }

    void UpdatePlaying(float dt)
    {
        levelTime += dt;
        hud.SetStats(levelTime, levelDeaths);
        if (GameInput.PausePressed) { Pause(); return; }
        if (GameInput.RestartPressed)
        {
            StartCoroutine(Transition(() => StartLevel(levelIndex, false)));
            return;
        }
        if (GameInput.ShiftPressed) shiftBuffer = 0.2f;
        if (shiftBuffer > 0)
        {
            shiftBuffer -= dt;
            if (shiftCooldown <= 0) { shiftBuffer = 0; TryShift(); }
        }

        Vector3 p = player.transform.position;
        Vector3 chest = p + Vector3.up * 0.45f;

        // hazards
        if (p.y < levelMin.y - 6f) { Die(false); return; }
        foreach (var s in spikes)
        {
            if (Mathf.Abs(p.x - s.x) < 0.55f && Mathf.Abs(p.z - s.z) < 0.55f && p.y < s.y + 0.4f && p.y > s.y - 0.6f)
            { Die(true); return; }
        }
        foreach (var o in orbs)
        {
            if (o.Lethal && (o.transform.position - chest).sqrMagnitude < 0.62f * 0.62f)
            { Die(true); return; }
        }

        // boss
        if (boss != null)
        {
            boss.Tick(dt, player);
            if (!DebugInvulnerable && boss.HitsPlayer(player)) { Die(true); return; }
        }

        // shards
        foreach (var s in shards)
        {
            if (s.collected) continue;
            if ((s.transform.position - chest).sqrMagnitude < 0.75f * 0.75f) Collect(s);
        }

        // checkpoints
        foreach (var cp in checkpoints)
        {
            if (cp.active) continue;
            var cpos = cp.transform.position;
            if (new Vector2(p.x - cpos.x, p.z - cpos.z).sqrMagnitude < 0.8f * 0.8f && Mathf.Abs(p.y - cpos.y) < 1f)
            {
                if (activeCheckpoint != null) activeCheckpoint.SetActive(false);
                cp.SetActive(true);
                activeCheckpoint = cp;
                sfx.Play(sfx.select, 0.8f, 0f);
                hud.Flash(Palette.Shard, 0.08f);
                hud.Toast("CHECKPOINT");
            }
        }

        // exit
        if (portal != null && portal.open)
        {
            Vector2 d = new Vector2(p.x - portalPos.x, p.z - portalPos.z);
            if (d.sqrMagnitude < 0.5f * 0.5f && Mathf.Abs(p.y - portalPos.y) < 0.7f) { Complete(); return; }
        }

        UpdateOcclusion(p);
    }

    readonly RaycastHit[] hitBuf = new RaycastHit[32];

    /// <summary>Fades any block standing between the camera and something the player needs to see.</summary>
    void UpdateOcclusion(Vector3 p)
    {
        foreach (var b in blocks) b.occluding = false;
        Vector3 c = cam.transform.position;
        RevealFromCamera(c, p + Vector3.up * 0.2f);
        RevealFromCamera(c, p + Vector3.up * 0.8f);
        foreach (var o in orbs) RevealFromCamera(c, o.transform.position);
        foreach (var s in shards) if (!s.collected) RevealFromCamera(c, s.transform.position);
        if (portal != null) RevealFromCamera(c, portalPos + Vector3.up * 0.9f);
    }

    void RevealFromCamera(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float len = d.magnitude - 0.35f;
        if (len <= 0) return;
        int n = Physics.SphereCastNonAlloc(from, 0.25f, d.normalized, hitBuf, len, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var b = hitBuf[i].collider.GetComponent<Block>();
            if (b != null && b.bounds.max.y > to.y - 0.2f) b.occluding = true;
        }
    }

    void Pause()
    {
        state = State.Paused;
        Time.timeScale = 0f;
        hud.ShowPause(levelIndex, Levels.Get(levelIndex));
    }

    void Resume()
    {
        Time.timeScale = 1f;
        state = State.Playing;
        hud.HidePause();
    }

    // ------------------------------------------------------------------ UI actions (called by Hud menus)

    /// <summary>Leaves pause / complete screens safely before a scene transition.</summary>
    void LeaveMenus()
    {
        Time.timeScale = 1f;
        if (player != null) player.controllable = false;
        hud.HidePause();
    }

    void StartRun(int index)
    {
        LeaveMenus();
        totalTime = 0; totalShifts = 0; totalDeaths = 0;
        selectedLevel = index;
        StartCoroutine(Transition(() => StartLevel(index, true)));
    }

    public void UiContinue() => StartRun(currentLevel);
    public void UiPlaySelected() => StartRun(selectedLevel);
    public void UiSelectLevel(int delta) => selectedLevel = Mathf.Clamp(selectedLevel + delta, 0, unlocked - 1);
    public void UiQuit() => Application.Quit();

    public void UiNewGame()
    {
        hud.Confirm("Start a new run from Level 1?\nYour best level stays unlocked in level select.", "YES, START OVER", () => StartRun(0), null);
    }

    public void UiPause() { if (state == State.Playing && !transitioning) Pause(); }
    public void UiResume() { if (state == State.Paused) Resume(); }

    public void UiRestartLevel()
    {
        LeaveMenus();
        StartCoroutine(Transition(() => StartLevel(levelIndex, false)));
    }

    /// <summary>The "restart from level one" button: asks first, then starts a fresh run at level 1.</summary>
    public void UiAskRestartFromOne()
    {
        if (transitioning || hud.ConfirmOpen) return;
        if (state == State.Playing) Pause();
        hud.Confirm("Restart from Level 1?\nYour current run will reset.", "YES, RESTART", () => StartRun(0), null);
    }

    public void UiMainMenu()
    {
        LeaveMenus();
        StartCoroutine(Transition(EnterTitle));
    }

    public void UiNext()
    {
        LeaveMenus();
        StartCoroutine(Transition(() => StartLevel(levelIndex + 1, true)));
    }

    public void UiReplayCompleted()
    {
        LeaveMenus();
        StartCoroutine(Transition(() => StartLevel(levelIndex, true)));
    }

    // ------------------------------------------------------------------ actions

    public bool TryShift()
    {
        if (state != State.Playing || shiftCooldown > 0 || player == null || !player.alive) return false;
        Phase target = PhaseState.Other(PhaseState.Current);
        Bounds pb = player.WorldBounds;
        foreach (var b in phaseBlocks)
        {
            if (b.phase == target && b.bounds.Intersects(pb))
            {
                b.Deny();
                sfx.Play(sfx.deny, 0.9f, 0.02f);
                hud.Flash(new Color(1f, 0.2f, 0.25f), 0.25f);
                rig.Shake(0.35f);
                shiftCooldown = 0.15f;
                return false;
            }
        }
        DoShift(target, player.transform.position, true);
        levelShifts++;
        return true;
    }

    void DoShift(Phase target, Vector3 origin, bool loud)
    {
        PhaseState.Set(target, false);
        foreach (var b in phaseBlocks) b.Apply(false, origin);
        sfx.Play(target == Phase.Night ? sfx.toNight : sfx.toDay, loud ? 0.9f : 0.45f, 0.03f);
        SpawnShockwave(origin + Vector3.up * 0.15f, Palette.PhaseColor(target), 0.7f, 16f);
        if (loud)
        {
            hud.Flash(Palette.PhaseColor(target), 0.16f);
            rig.Shake(0.22f);
            if (player != null) player.Bump(0.18f);
        }
        shiftCooldown = 0.12f;
    }

    void SpawnShockwave(Vector3 pos, Color c, float duration, float maxScale)
    {
        var mat = Mats.Transparent(c, 0.7f, 2f);
        var go = MeshFactory.Part("Shockwave", levelRoot, MeshFactory.Torus(1f, 0.035f, 64, 6), mat, pos, false);
        var sw = go.AddComponent<Shockwave>();
        sw.mat = mat; sw.color = c; sw.duration = duration; sw.maxScale = maxScale;
    }

    void SpawnDebris(Vector3 pos, Material mat, int count, float speed, float size)
    {
        for (int i = 0; i < count; i++)
        {
            float s = size * Random.Range(0.6f, 1.2f);
            var go = MeshFactory.Part("Debris", levelRoot, MeshFactory.RoundedBox(Vector3.one * 0.15f, 0.03f, 1), mat, pos, false);
            go.transform.localScale = Vector3.one * (s / 0.15f);
            go.transform.rotation = Random.rotation;
            var d = go.AddComponent<Debris>();
            d.velocity = Random.onUnitSphere * speed + Vector3.up * speed * 0.8f;
            d.spin = Random.insideUnitSphere * 720f;
            d.life = Random.Range(0.6f, 1.1f);
        }
    }

    void Collect(Shard s)
    {
        s.collected = true;
        shardsCollected++;
        sfx.Play(sfx.collect, 0.9f, 0f);
        SpawnDebris(s.transform.position, shardMat, 10, 4f, 0.12f);
        SpawnShockwave(s.transform.position, Palette.Shard, 0.45f, 3f);
        hud.Flash(Palette.Shard, 0.12f);
        s.gameObject.SetActive(false);
        UpdateShardText();
        if (shardsCollected >= shards.Count && portal != null && !portal.open)
        {
            portal.SetOpen(true, false);
            sfx.Play(sfx.portalOpen, 0.8f, 0f);
            SpawnShockwave(portalPos + Vector3.up * 0.2f, Color.white, 0.8f, 6f);
        }
    }

    void Die(bool explode)
    {
        if (DebugInvulnerable)
        {
            if (!explode) player.Teleport((activeCheckpoint != null ? activeCheckpoint.transform.position : startPos) + Vector3.up * 0.05f);
            return;
        }
        state = State.Dying;
        levelDeaths++;
        player.alive = false;
        player.controllable = false;
        sfx.Play(sfx.death, 0.9f, 0.05f);
        hud.Flash(new Color(1f, 0.15f, 0.2f), 0.35f);
        if (explode)
        {
            rig.Shake(0.7f);
            var pos = player.transform.position + Vector3.up * 0.4f;
            SpawnDebris(pos, debrisMat, 14, 5f, 0.16f);
            SpawnDebris(pos, player.eyeMat, 6, 5f, 0.1f);
            player.visual.gameObject.SetActive(false);
            dyingTimer = 0.9f;
        }
        else
        {
            rig.target = null;
            rig.focusPoint = player.transform.position;
            dyingTimer = 0.5f;
        }
    }

    void Complete()
    {
        state = State.Complete;
        player.controllable = false;
        player.alive = false;
        sfx.Play(sfx.win, 0.9f, 0f);
        totalTime += levelTime; totalShifts += levelShifts; totalDeaths += levelDeaths;
        if (levelIndex + 2 > unlocked)
        {
            unlocked = levelIndex + 2;
            PlayerPrefs.SetInt(PrefKey, unlocked);
            PlayerPrefs.Save();
        }
        selectedLevel = levelIndex + 1;
        currentLevel = levelIndex + 1;
        PlayerPrefs.SetInt(CurrentKey, currentLevel);
        PlayerPrefs.Save();
        StartCoroutine(CompleteSequence());
    }

    IEnumerator CompleteSequence()
    {
        var t0 = player.transform.position;
        var target = portalPos + Vector3.up * 0.5f;
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.7f;
            float e = t * t;
            player.transform.position = Vector3.Lerp(t0, target, e);
            player.visual.localScale = Vector3.one * (1 - e);
            player.visual.Rotate(0, 900 * Time.deltaTime, 0);
            yield return null;
        }
        player.visual.gameObject.SetActive(false);
        SpawnShockwave(target, Color.white, 0.6f, 5f);
        hud.Flash(Color.white, 0.4f);
        yield return new WaitForSeconds(0.4f);
        bool campaignEnd = levelIndex + 1 == Levels.HandmadeCount;
        if (campaignEnd)
            hud.ShowComplete("CAMPAIGN COMPLETE", "The world keeps shifting. Endless levels begin now.",
                totalTime, totalShifts, totalDeaths, "GO DEEPER  -  LEVEL " + (levelIndex + 2), true);
        else
            hud.ShowComplete($"LEVEL {levelIndex + 1} COMPLETE", Levels.Get(levelIndex).name.ToUpper(),
                levelTime, levelShifts, levelDeaths, $"NEXT  -  LEVEL {levelIndex + 2}", false);
    }

    // ------------------------------------------------------------------ boss hooks

    /// <summary>The Eclipse attack: the boss forces a world shift (skipped if a block would appear inside the player).</summary>
    public void BossEclipse()
    {
        if (state != State.Playing || player == null || !player.alive) return;
        Phase target = PhaseState.Other(PhaseState.Current);
        Bounds pb = player.WorldBounds;
        foreach (var b in phaseBlocks)
            if (b.phase == target && b.bounds.Intersects(pb)) return;
        DoShift(target, boss != null ? boss.info.center : player.transform.position, true);
        hud.Flash(new Color(0.8f, 0.5f, 1f), 0.35f);
        rig.Shake(0.5f);
    }

    public void BossTelegraph(string msg) => hud.Toast(msg);

    public void BossCellCollected(Vector3 at)
    {
        sfx.Play(sfx.collect, 0.9f, 0f);
        SpawnShockwave(at, Palette.Shard, 0.45f, 3f);
        hud.Flash(Palette.Shard, 0.15f);
    }

    public void OnBossHit(int hp, int max, Vector3 at)
    {
        hud.SetBossHp(hp);
        rig.Shake(0.6f);
        hud.Flash(Color.white, 0.3f);
        sfx.Play(sfx.bossHit, 1f, 0.05f);
        SpawnDebris(at, shardMat, 12, 6f, 0.18f);
        SpawnShockwave(at, Color.white, 0.6f, 6f);
        if (hp > 0) hud.Toast($"DIRECT HIT  -  {hp} LEFT");
    }

    public void OnBossDefeated(Vector3 at)
    {
        SpawnDebris(at, debrisMat, 30, 9f, 0.3f);
        SpawnDebris(at, shardMat, 20, 8f, 0.2f);
        SpawnShockwave(at, Palette.DayBlock, 1.2f, 22f);
        SpawnShockwave(at, Palette.NightBlock, 1.6f, 26f);
        rig.Shake(1.2f);
        hud.Flash(Color.white, 0.7f);
        sfx.Play(sfx.death, 1f, 0f);
        sfx.Play(sfx.win, 1f, 0f);
        if (portal != null) portal.SetOpen(true, false);
        hud.MarkPortalOpen("THE ECLIPSE FALLS  -  PORTAL OPEN");
    }

    static string FormatTime(float t)
    {
        int m = (int)(t / 60);
        float s = t - m * 60;
        return $"{m:00}:{s:00.0}";
    }

    // ------------------------------------------------------------------ test hooks (used by automated playtests)

    public void DebugStartLevel(int index)
    {
        StopAllCoroutines();
        transitioning = false;
        fadeA = 0;
        StartLevel(index, true);
    }

    public List<Orb> DebugOrbs => orbs;
    public int DebugDeaths => levelDeaths;

    public string DebugDescribe()
    {
        return $"state={state} level={levelIndex} phase={PhaseState.Current} shards={shardsCollected}/{shards.Count} " +
               $"player={(player ? player.transform.position.ToString("F2") : "none")} blocks={blocks.Count} orbs={orbs.Count} deaths={levelDeaths} cp={(activeCheckpoint ? activeCheckpoint.transform.position.ToString("F1") : "none")}";
    }
}
