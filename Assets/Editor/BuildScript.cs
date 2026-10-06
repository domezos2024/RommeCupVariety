using System;
using System.IO;
using RommeCup.App;
using RommeCup.Rummikub;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RommeCup.EditorTools
{
    public static class BuildScript
    {
        const string ScenePath = "Assets/Scenes/Main.unity", Apk = "Builds/RommeCupVariety.apk";

        [MenuItem("RommeCup/Setup Project")]
        public static void Setup()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Resources");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("App").AddComponent<GameApp>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Mat("RC_Lit", "Standard", false);
            Mat("RC_LitEmit", "Standard", true);
            Mat("RC_LitN", "Standard", false, true);
            Mat("RC_LitNE", "Standard", true, true);
            Mat("RC_Add", "Legacy Shaders/Particles/Additive", false);
            Mat("RC_Alpha", "Legacy Shaders/Particles/Alpha Blended", false);
            var icon = TileArt.Icon(512);
            File.WriteAllBytes("Assets/Icon.png", icon.EncodeToPNG());
            AssetDatabase.Refresh();
            var ti = (TextureImporter)AssetImporter.GetAtPath("Assets/Icon.png");
            ti.textureCompression = TextureImporterCompression.Uncompressed; ti.SaveAndReimport();
            var iconAsset = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icon.png");
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { iconAsset }, IconKind.Any);
            PlayerSettings.companyName = "Domezos";
            PlayerSettings.productName = "Romme Cup Variety";
            PlayerSettings.bundleVersion = "2.0.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.domezos.rommecupvariety");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Minimal);
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            var sdk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk");
            if (Directory.Exists(Path.Combine(sdk, "platform-tools")))
            {
                EditorPrefs.SetBool("SdkUseEmbedded", false);
                AndroidExternalToolsSettings.sdkRootPath = sdk;
                Debug.Log("[RC] sdk=" + sdk);
            }
            PlayerSettings.Android.bundleVersionCode = 2;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultScreenWidth = 1818;
            PlayerSettings.defaultScreenHeight = 810;
            PlayerSettings.runInBackground = true;
            try { PlayerSettings.SplashScreen.show = false; } catch (Exception e) { Debug.LogWarning(e.Message); }
            AssetDatabase.SaveAssets();
            Debug.Log("[RC] setup done");
        }

        static void Mat(string name, string shader, bool emission, bool normal = false)
        {
            var path = "Assets/Resources/" + name + ".mat";
            var sh = Shader.Find(shader);
            if (!sh) sh = Shader.Find("Standard");
            var m = new Material(sh);
            if (normal) m.EnableKeyword("_NORMALMAP");
            if (emission) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(.001f, .001f, .001f)); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
        }

        [MenuItem("RommeCup/Build Windows Test")]
        public static void BuildWindowsTest()
        {
            Setup();
            Directory.CreateDirectory("Builds/WinTest");
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = "Builds/WinTest/RommeCup.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            Debug.Log("[RC] win build result=" + r.summary.result + " errors=" + r.summary.totalErrors);
            if (Application.isBatchMode) EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("RommeCup/Build Android APK")]
        public static void BuildAndroid()
        {
            Setup();
            Directory.CreateDirectory("Builds");
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = Apk, target = BuildTarget.Android, options = BuildOptions.None });
            Debug.Log("[RC] build result=" + r.summary.result + " size=" + r.summary.totalSize + " errors=" + r.summary.totalErrors);
            if (Application.isBatchMode) EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }

    public class ManifestPatcher : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var f = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(f)) { Debug.LogWarning("[RC] manifest not found " + f); return; }
            var x = File.ReadAllText(f);
            if (x.Contains("BLUETOOTH_CONNECT")) return;
            const string perms =
                "<uses-permission android:name=\"android.permission.BLUETOOTH\" android:maxSdkVersion=\"30\" />\n" +
                "<uses-permission android:name=\"android.permission.BLUETOOTH_ADMIN\" android:maxSdkVersion=\"30\" />\n" +
                "<uses-permission android:name=\"android.permission.ACCESS_FINE_LOCATION\" android:maxSdkVersion=\"30\" />\n" +
                "<uses-permission android:name=\"android.permission.ACCESS_COARSE_LOCATION\" android:maxSdkVersion=\"30\" />\n" +
                "<uses-permission android:name=\"android.permission.BLUETOOTH_SCAN\" android:usesPermissionFlags=\"neverForLocation\" />\n" +
                "<uses-permission android:name=\"android.permission.BLUETOOTH_CONNECT\" />\n" +
                "<uses-permission android:name=\"android.permission.BLUETOOTH_ADVERTISE\" />\n" +
                "<uses-feature android:name=\"android.hardware.bluetooth\" android:required=\"false\" />\n";
            int i = x.IndexOf("<application", StringComparison.Ordinal);
            x = i >= 0 ? x.Insert(i, perms) : x.Replace("</manifest>", perms + "</manifest>");
            File.WriteAllText(f, x);
            Debug.Log("[RC] bluetooth permissions injected");
        }
    }
}
