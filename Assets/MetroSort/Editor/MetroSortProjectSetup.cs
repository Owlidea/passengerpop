using System;
using System.IO;
using MetroSort.Game;
using MetroSort.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
#if METROSORT_URP
using UnityEngine.Rendering.Universal;
#endif

namespace MetroSort.EditorTools
{
    /// <summary>
    /// One-time project configuration, run automatically the first time the project is opened and available
    /// from the "Metro Sort" menu. Creates the bootstrap scene, the URP assets, the shared material, the
    /// economy config asset, and applies mobile player settings.
    /// </summary>
    [InitializeOnLoad]
    public static class MetroSortProjectSetup
    {
        private const string Root = "Assets/MetroSort";
        private const string ScenePath = Root + "/Scenes/Bootstrap/Bootstrap.unity";
        private const string MarkerPath = Root + "/Data/Settings/.setup_done";

        static MetroSortProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(MarkerPath) || Application.isBatchMode) return;
                RunSetup();
            };
        }

        [MenuItem("Metro Sort/Setup Project (scene, URP, settings)")]
        public static void RunSetup()
        {
            Step("player settings", ApplyPlayerSettings);
            Step("URP pipeline", SetupUrp);
            Step("shared material", CreateSharedMaterial);
            Step("outline material", CreateOutlineMaterial);
            Step("economy config", CreateEconomyConfig);
            Step("bootstrap scene", CreateBootstrapScene);
            Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath));
            File.WriteAllText(MarkerPath, "Metro Sort setup completed " + DateTime.UtcNow.ToString("u"));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Metro Sort: project setup finished. Open " + ScenePath + " and press Play.");
        }

        private static void Step(string name, Action action)
        {
            try { action(); }
            catch (Exception e) { Debug.LogWarning("Metro Sort setup: '" + name + "' step failed: " + e.Message); }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Metro Sort Studio";
            PlayerSettings.productName = "Metro Sort";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.metrosort.game");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.metrosort.game");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26; // Android 8.0
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.bundleVersionCode = 1;
            EditorUserBuildSettings.buildAppBundle = true;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
        }

        public static void SetupUrp()
        {
#if METROSORT_URP
            string rendererPath = Root + "/Data/Settings/MetroSort_Renderer.asset";
            EnsurePostProcessData(rendererPath);
            if (GraphicsSettings.defaultRenderPipeline != null) return;
            EnsureFolder(Root + "/Data/Settings");
            string pipelinePath = Root + "/Data/Settings/MetroSort_URP.asset";
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, rendererPath);
                EnsurePostProcessData(rendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                pipeline.shadowDistance = 70f;
                pipeline.msaaSampleCount = 2;
                pipeline.supportsHDR = false;
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
#else
            Debug.Log("Metro Sort: URP package not found, staying on the built-in render pipeline.");
#endif
        }

#if METROSORT_URP
        /// <summary>A renderer created from code has no post-process resources; without them bloom and colour grading are skipped.</summary>
        private static void EnsurePostProcessData(string rendererPath)
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null || rendererData.postProcessData != null) return;
            rendererData.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
        }
#endif

        [MenuItem("Metro Sort/Rendering/Use Built-in Pipeline (fallback)")]
        public static void UseBuiltInPipeline()
        {
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            AssetDatabase.DeleteAsset(Root + "/Resources/MetroSortLit.mat");
            CreateSharedMaterial();
            Debug.Log("Metro Sort: switched to the built-in render pipeline.");
        }

        /// <summary>A material in Resources keeps the lit shader in player builds (Shader.Find alone would be stripped).</summary>
        public static void CreateSharedMaterial()
        {
            string path = Root + "/Resources/MetroSortLit.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            EnsureFolder(Root + "/Resources");
            Shader shader = GraphicsSettings.defaultRenderPipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.35f);
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, path);
        }

        /// <summary>Stored in Resources so the URP Unlit shader used for cartoon outlines ships in player builds.</summary>
        public static void CreateOutlineMaterial()
        {
            string path = Root + "/Resources/MetroSortOutline.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null || GraphicsSettings.defaultRenderPipeline == null) return;
            EnsureFolder(Root + "/Resources");
            var material = new Material(shader);
            material.SetColor("_BaseColor", InkOutline.Ink);
            material.SetFloat("_Cull", 1f);
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, path);
        }

        public static void CreateEconomyConfig()
        {
            string path = Root + "/Resources/EconomyConfig.asset";
            if (AssetDatabase.LoadAssetAtPath<EconomyConfigAsset>(path) != null) return;
            EnsureFolder(Root + "/Resources");
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<EconomyConfigAsset>(), path);
        }

        public static void CreateBootstrapScene()
        {
            if (!File.Exists(ScenePath))
            {
                EnsureFolder(Root + "/Scenes/Bootstrap");
                // The scene is intentionally empty: GameApp builds everything at runtime.
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // ------------------------------------------------------------------ level tools

        [MenuItem("Metro Sort/Levels/Validate All Levels")]
        public static void ValidateAllLevels()
        {
            int failed = 0, total = 0;
            foreach (TextAsset asset in Resources.LoadAll<TextAsset>("Levels"))
            {
                total++;
                ValidationReport report = LevelValidator.Validate(LevelData.FromJson(asset.text));
                if (report.IsValid) Debug.Log(report.ToString());
                else { failed++; Debug.LogError(report.ToString()); }
            }
            Debug.Log("Metro Sort: " + total + " levels checked, " + failed + " failed.");
        }

        [MenuItem("Metro Sort/Levels/Generate Extra Level")]
        public static void GenerateLevel()
        {
            int next = Resources.LoadAll<TextAsset>("Levels").Length + 1;
            var p = new GeneratorParams { Colors = 4, GroupsPerColor = 2, Queues = 5, Platforms = 3, MinDeadEndRatio = 0.15f, MaxDeadEndRatio = 0.55f };
            ValidationReport report;
            LevelData level = LevelGenerator.Generate(p, Environment.TickCount, out report);
            if (level == null)
            {
                Debug.LogWarning("Metro Sort: the generator found no level inside the requested band. Try again.");
                return;
            }
            level.Id = next;
            level.Name = "Generated " + next;
            string path = Root + "/Resources/Levels/level_" + next.ToString("000") + ".json";
            File.WriteAllText(path, level.ToJson());
            AssetDatabase.Refresh();
            Debug.Log("Metro Sort: wrote " + path + "  " + report);
        }

        /// <summary>Development aid: unlocks every level in the local save (and in the running game, if playing).</summary>
        [MenuItem("Metro Sort/Unlock All Levels (testing)")]
        public static void UnlockAllLevels()
        {
            if (Application.isPlaying && GameApp.Instance != null)
            {
                GameApp.Instance.Services.Save.Data.HighestUnlocked = 9999;
                GameApp.Instance.Services.Save.Save();
                GameApp.Instance.UI.Refresh();
            }
            else
            {
                var save = new MetroSort.Persistence.SaveManager(new UnityFileStore(), new MetroSort.Economy.EconomyConfig());
                save.Load();
                save.Data.HighestUnlocked = 9999;
                save.Save();
            }
            Debug.Log("Metro Sort: all levels unlocked. Use Levels on the main menu to jump to any of them.");
        }

        [MenuItem("Metro Sort/Delete Save Data")]
        public static void DeleteSave()
        {
            foreach (string name in new[] { "metrosort_save.json", "metrosort_save.bak.json" })
            {
                string path = Path.Combine(Application.persistentDataPath, name);
                if (File.Exists(path)) File.Delete(path);
            }
            Debug.Log("Metro Sort: save data deleted.");
        }
    }

    /// <summary>Command-line and menu build entry points. See Docs/BUILD_ANDROID.md and Docs/BUILD_IOS.md.</summary>
    public static class MetroSortBuild
    {
        [MenuItem("Metro Sort/Build/Android App Bundle")]
        public static void BuildAndroid()
        {
            MetroSortProjectSetup.RunSetup();
            Build(BuildTarget.Android, "Builds/Android/MetroSort.aab");
        }

        /// <summary>An APK can be installed straight onto a phone or emulator (an AAB cannot); for testing only.</summary>
        [MenuItem("Metro Sort/Build/Android APK (for testing)")]
        public static void BuildAndroidApk()
        {
            MetroSortProjectSetup.RunSetup();
            EditorUserBuildSettings.buildAppBundle = false;
            try { Build(BuildTarget.Android, "Builds/Android/MetroSort.apk"); }
            finally { EditorUserBuildSettings.buildAppBundle = true; }
        }

        [MenuItem("Metro Sort/Build/iOS Xcode Project")]
        public static void BuildIOS()
        {
            MetroSortProjectSetup.RunSetup();
            Build(BuildTarget.iOS, "Builds/iOS");
        }

        /// <summary>Same project built against the iOS Simulator SDK, for running on a Mac without a device.</summary>
        [MenuItem("Metro Sort/Build/iOS Simulator Xcode Project")]
        public static void BuildIOSSimulator()
        {
            MetroSortProjectSetup.RunSetup();
            iOSSdkVersion previous = PlayerSettings.iOS.sdkVersion;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
            try { Build(BuildTarget.iOS, "Builds/iOSSimulator"); }
            finally { PlayerSettings.iOS.sdkVersion = previous; }
        }

        private static void Build(BuildTarget target, string output)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/MetroSort/Scenes/Bootstrap/Bootstrap.unity" },
                locationPathName = output,
                target = target,
                options = BuildOptions.None
            };
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("Metro Sort build " + target + ": " + report.summary.result + " -> " + output);
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
