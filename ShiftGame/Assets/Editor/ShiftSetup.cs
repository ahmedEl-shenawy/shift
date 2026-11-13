#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One-click project setup: base materials, main scene, build + player settings, and builds.</summary>
public static class ShiftSetup
{
    const string ResDir = "Assets/Resources";
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("SHIFT/Setup Project")]
    public static string SetupAll()
    {
        CreateMaterials();
        CreateScene();
        ConfigurePlayer();
        AssetDatabase.SaveAssets();
        return "ok";
    }

    static void CreateMaterials()
    {
        Directory.CreateDirectory(ResDir);
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        var mLit = new Material(lit) { name = "Mat_Lit" };
        mLit.EnableKeyword("_EMISSION");
        mLit.SetColor("_EmissionColor", Color.black);
        mLit.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        mLit.enableInstancing = true;
        Save(mLit, "Mat_Lit");

        var mTr = new Material(lit) { name = "Mat_Transparent" };
        mTr.SetFloat("_Surface", 1);
        mTr.SetFloat("_Blend", 0);
        mTr.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mTr.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mTr.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mTr.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        mTr.SetFloat("_ZWrite", 0);
        mTr.SetFloat("_CastShadows", 0);
        mTr.SetShaderPassEnabled("ShadowCaster", false);
        mTr.SetOverrideTag("RenderType", "Transparent");
        mTr.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mTr.EnableKeyword("_EMISSION");
        mTr.SetColor("_EmissionColor", Color.black);
        mTr.renderQueue = (int)RenderQueue.Transparent;
        mTr.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        Save(mTr, "Mat_Transparent");

        var mP = new Material(particles) { name = "Mat_Particle" };
        mP.SetFloat("_Surface", 1);
        mP.SetFloat("_Blend", 2); // additive
        mP.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mP.SetFloat("_DstBlend", (float)BlendMode.One);
        mP.SetFloat("_ZWrite", 0);
        mP.SetOverrideTag("RenderType", "Transparent");
        mP.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mP.EnableKeyword("_BLENDMODE_ADD");
        mP.renderQueue = (int)RenderQueue.Transparent;
        Save(mP, "Mat_Particle");
    }

    static void Save(Material m, string name)
    {
        string path = $"{ResDir}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.shader = m.shader;
            existing.CopyPropertiesFromMaterial(m);
            existing.shaderKeywords = m.shaderKeywords;
            existing.renderQueue = m.renderQueue;
            existing.SetOverrideTag("RenderType", m.GetTag("RenderType", false));
            if (!m.GetShaderPassEnabled("ShadowCaster")) existing.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(existing);
        }
        else AssetDatabase.CreateAsset(m, path);
    }

    static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("Game");
        go.AddComponent<GameManager>();
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    static void ConfigurePlayer()
    {
        PlayerSettings.productName = "SHIFT";
        PlayerSettings.companyName = "ShiftGameOff";
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.template = "APPLICATION:Default";
        PlayerSettings.colorSpace = ColorSpace.Linear;
    }

    [MenuItem("SHIFT/Build Windows")]
    public static string BuildWindows()
    {
        var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "../Builds/Windows/SHIFT.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        return r.summary.result + " " + r.summary.totalErrors + " errors, " + r.summary.totalSize / (1024 * 1024) + " MB";
    }

    [MenuItem("SHIFT/Build WebGL")]
    public static string BuildWebGL()
    {
        var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "../Builds/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        return r.summary.result + " " + r.summary.totalErrors + " errors, " + r.summary.totalSize / (1024 * 1024) + " MB";
    }
}
#endif
