using System;
using System.IO;
using HeroesOfMetalAge.Bootstrap;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeroesOfMetalAge.EditorTools
{
    /// <summary>
    /// Command-line entry points (Tools/Build.ps1): generate the bootstrap scene from the authored assets and build the
    /// Windows player. The scene is the only generated asset; databases, maps and prefabs are authored and never rebuilt
    /// (missing dialogue assets are seeded once, see <see cref="DialogueAssetSeeder"/>).
    /// The cameras come with the World prefab.
    /// </summary>
    public static class PrototypePipeline
    {
        private const string PLAYER_PATH = "Builds/Windows/HeroesOfMetalAge.exe";
        private const string BUNDLE_VERSION = "0.7.0";
        private const int DEFAULT_SCREEN_WIDTH = 1920;
        private const int DEFAULT_SCREEN_HEIGHT = 1080;
        private const int LEGACY_INPUT_MANAGER = 0;

        /// <summary>Validates the content and regenerates Assets/Scenes/AshenPass.unity wired to the authored assets.</summary>
        /// <exception cref="InvalidOperationException">Content is invalid or an authored asset is missing.</exception>
        [MenuItem("Tools/Heroes of Metal Age/Generate scene")]
        public static void Setup()
        {
            ContentAssets.LoadCatalog();
            DialogueAssetSeeder.CreateMissingAssets();
            RequireAsset(ContentAssets.RootPrefab, ContentAssets.UI_FOLDER + "/GameUiRoot.prefab");
            RequireAsset(ContentAssets.DebugPanelPrefab, ContentAssets.UI_FOLDER + "/Screens/DebugPanel.prefab");
            RequireAsset(ContentAssets.WorldPrefab, ContentAssets.WORLD_FOLDER + "/World.prefab");
            RequireAsset(ContentAssets.Dialogue, ContentAssets.DIALOGUE_DATABASE_PATH);
            RequireAsset(ContentAssets.DialogueOverlayPrefab, ContentAssets.DIALOGUE_OVERLAY_PATH);

            Directory.CreateDirectory(Path.GetDirectoryName(ContentAssets.SCENE_PATH));
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameBootstrap bootstrap = new GameObject("Heroes of Metal Age").AddComponent<GameBootstrap>();
            bootstrap.Configure(ContentAssets.Units, ContentAssets.Artefacts, ContentAssets.Heroes, ContentAssets.Audio, ContentAssets.Mission,
                ContentAssets.Dialogue, ContentAssets.RootPrefab, ContentAssets.WorldPrefab, ContentAssets.DebugPanelPrefab,
                ContentAssets.DialogueOverlayPrefab);
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.SaveScene(scene, ContentAssets.SCENE_PATH);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ContentAssets.SCENE_PATH, true) };

            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("PROTOTYPE_SETUP_OK");
        }

        /// <summary>Generates the scene and builds the development player used by Tools/SmokeTest.ps1.</summary>
        [MenuItem("Tools/Heroes of Metal Age/Build Windows prototype")]
        public static void Build()
        {
            Setup();
            Directory.CreateDirectory(Path.GetDirectoryName(PLAYER_PATH));
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ContentAssets.SCENE_PATH },
                locationPathName = PLAYER_PATH,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("Build failed: " + report.summary.result);
            }

            Debug.Log("PROTOTYPE_BUILD_OK / " + report.summary.totalSize + " bytes");
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Iron Oath Studio";
            PlayerSettings.productName = "HeroesOfMetalAge";
            PlayerSettings.bundleVersion = BUNDLE_VERSION;
            PlayerSettings.defaultScreenWidth = DEFAULT_SCREEN_WIDTH;
            PlayerSettings.defaultScreenHeight = DEFAULT_SCREEN_HEIGHT;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });

            // the UI uses the legacy input module, so the project stays on the old Input Manager
            SerializedObject settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            SerializedProperty input = settings.FindProperty("activeInputHandler");
            if (input != null)
            {
                input.intValue = LEGACY_INPUT_MANAGER;
                settings.ApplyModifiedProperties();
            }
        }

        private static void RequireAsset(UnityEngine.Object asset, string path)
        {
            if (asset == null)
            {
                throw new InvalidOperationException("Missing authored asset: " + path);
            }
        }
    }
}
