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
            Step("particle material", CreateParticleMaterial);
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
            PlayerSettings.productName = "Passenger Pop";
            PlayerSettings.bundleVersion = "1.0.1";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.owlidea.metrosort");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.owlidea.metrosort");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26; // Android 8.0
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            // Google Play takes each version code once: raise it before every upload (1 was the 1.0.0 launch,
            // 2 the launcher icon fix, 3 the 1.0.1 shop update).
            PlayerSettings.Android.bundleVersionCode = 3;
            // The project had an empty format list, which shipped every "compressed" texture as raw RGBA (the art
            // alone was 182 MB). ASTC runs on every OpenGL ES 3.2 / Vulkan phone; older GPUs get it decoded.
            PlayerSettings.Android.textureCompressionFormats = new[] { TextureCompressionFormat.ASTC };
            // No "Made with Unity" screen (optional since Unity 6): the game opens on the StagFunny studio intro
            // (BrandIntro). White behind it, like the intro, so the hand-over from the launch screen does not flash.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = Color.white;
            EditorUserBuildSettings.buildAppBundle = true;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            // App Store Connect takes each build number once per version: raise it before every upload.
            PlayerSettings.iOS.buildNumber = "3";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            // The App Store team (OWLIDEA BILISIM TEKNOLOJILERI LTD. STI.), so the export archives without signing edits.
            PlayerSettings.iOS.appleDeveloperTeamID = "7ULGPGPVQ3";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
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
        /// <summary>Material for the particle effects (Fx); kept in Resources so its shader ships with builds.</summary>
        public static void CreateParticleMaterial()
        {
            string path = Root + "/Resources/MetroSortParticle.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            AssetDatabase.CreateAsset(new Material(shader), path);
            AssetDatabase.SaveAssets();
        }

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
            if (!ApplyAndroidIcon()) return;
            ApplyAdMobAppId();
            bool signed = UseUploadKey();
            try
            {
                // The bundle goes to Google Play: real ad units, whatever useTestAds says (MonetizationConfig.StoreBuild).
                Build(BuildTarget.Android, "Builds/Android/MetroSort.aab", "METROSORT_STORE_BUILD");
            }
            finally
            {
                if (signed) ForgetUploadKey();
            }
        }

        /// <summary>
        /// Google Play only accepts bundles signed with the app's upload key. The keystore lives outside the repo and
        /// its passwords are never saved: the build reads METROSORT_KEYSTORE (path), METROSORT_KEY_ALIAS,
        /// METROSORT_KEYSTORE_PASS and METROSORT_KEY_PASS from the environment, which
        /// Tools/android/build_play_bundle.sh fills after asking for the password. Without them the bundle gets the
        /// debug key, which Google Play refuses.
        /// </summary>
        private static bool UseUploadKey()
        {
            string keystore = Environment.GetEnvironmentVariable("METROSORT_KEYSTORE");
            string storePass = Environment.GetEnvironmentVariable("METROSORT_KEYSTORE_PASS");
            if (string.IsNullOrEmpty(keystore) || string.IsNullOrEmpty(storePass) || !File.Exists(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.LogWarning("Metro Sort build: no upload key (METROSORT_KEYSTORE / METROSORT_KEYSTORE_PASS); the bundle is signed with the debug key and Google Play will refuse it.");
                return false;
            }
            string alias = Environment.GetEnvironmentVariable("METROSORT_KEY_ALIAS");
            string keyPass = Environment.GetEnvironmentVariable("METROSORT_KEY_PASS");
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = string.IsNullOrEmpty(alias) ? "passengerpop" : alias;
            PlayerSettings.Android.keyaliasPass = string.IsNullOrEmpty(keyPass) ? storePass : keyPass;
            Debug.Log("Metro Sort build: signing with the upload key, alias " + PlayerSettings.Android.keyaliasName);
            return true;
        }

        /// <summary>Back to the debug key, so the keystore path is not left in ProjectSettings and test APKs still build.</summary>
        private static void ForgetUploadKey()
        {
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.keystoreName = "";
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasName = "";
            PlayerSettings.Android.keyaliasPass = "";
        }

        /// <summary>An APK can be installed straight onto a phone or emulator (an AAB cannot); for testing only.</summary>
        [MenuItem("Metro Sort/Build/Android APK (for testing)")]
        public static void BuildAndroidApk()
        {
            MetroSortProjectSetup.RunSetup();
            if (!ApplyAndroidIcon()) return;
            ApplyAdMobAppId();
            EditorUserBuildSettings.buildAppBundle = false;
            try { Build(BuildTarget.Android, "Builds/Android/MetroSort.apk"); }
            finally { EditorUserBuildSettings.buildAppBundle = true; }
        }

        /// <summary>
        /// Every Android build sets the launcher icon first: Unity 6 ignores the default icon on Android, and without
        /// the adaptive icon the game shows Unity's logo on the home screen. A batch build stops when it cannot be set.
        /// </summary>
        private static bool ApplyAndroidIcon()
        {
            if (MediaImport.TryApplyAppIcon()) return true;
            Debug.LogError("Metro Sort build: no Android app icon, not building.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return false;
        }

        /// <summary>
        /// Copies the AdMob app ids from Resources/monetization.json into the Google Mobile Ads settings asset, which
        /// writes them into the Android manifest and the iOS Info.plist (the Android app stops at launch without one,
        /// and the plugin refuses to build for iOS without one). There is no iOS AdMob app yet, and the iOS game uses
        /// the development ad stand-in, so iOS gets Google's sample app id until admobIosAppId is filled in. The
        /// settings class is internal to the plugin, so the fields are set through the asset's serialized data.
        /// </summary>
        public static void ApplyAdMobAppId()
        {
#if METROSORT_ADMOB
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/MetroSort/Resources/monetization.json");
            MonetizationConfig config = json != null ? MonetizationConfig.Parse(json.text) : null;
            string appId = config != null && !string.IsNullOrEmpty(config.AdMobAndroidAppId) ? config.AdMobAndroidAppId : MonetizationConfig.GoogleTestAppId;
            string iosAppId = config != null && !string.IsNullOrEmpty(config.AdMobIosAppId) ? config.AdMobIosAppId : MonetizationConfig.GoogleTestIosAppId;
            const string path = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (settings == null)
            {
                Type type = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
                var load = type != null ? type.GetMethod("LoadInstance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public) : null;
                settings = load != null ? load.Invoke(null, null) as ScriptableObject : null;
            }
            if (settings == null)
            {
                Debug.LogError("Metro Sort build: Google Mobile Ads settings asset not found");
                return;
            }
            var so = new SerializedObject(settings);
            bool changed = false;
            // The game shows no App Tracking Transparency prompt, so Info.plist must not carry
            // NSUserTrackingUsageDescription: the plugin writes it whenever this text is not empty, and App Store
            // Connect then refuses a "no tracking" privacy label. To add the prompt later: set the text here, publish
            // an IDFA explainer in AdMob Privacy & messaging (UMP shows it) and mark Device ID as tracking in App Privacy.
            const string tracking = "";
            foreach (var pair in new[] { new[] { "adMobAndroidAppId", appId }, new[] { "adMobIOSAppId", iosAppId }, new[] { "userTrackingUsageDescription", tracking } })
            {
                SerializedProperty id = so.FindProperty(pair[0]);
                if (id == null || id.stringValue == pair[1]) continue;
                id.stringValue = pair[1];
                changed = true;
            }
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            Debug.Log("Metro Sort build: AdMob app id " + appId + (appId == MonetizationConfig.GoogleTestAppId ? " (Google test app)" : "")
                + ", iOS " + iosAppId + (iosAppId == MonetizationConfig.GoogleTestIosAppId ? " (Google test app)" : ""));
#endif
        }

        [MenuItem("Metro Sort/Build/iOS Xcode Project")]
        public static void BuildIOS()
        {
            MetroSortProjectSetup.RunSetup();
            ApplyAdMobAppId();
            // Always state the SDK: a simulator build must never leave the device export pointing at the simulator.
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            // The device export goes to the App Store: real ad units, as in the Play bundle.
            Build(BuildTarget.iOS, "Builds/iOS", "METROSORT_STORE_BUILD");
        }

        /// <summary>Same project built against the iOS Simulator SDK, for running on a Mac without a device.</summary>
        [MenuItem("Metro Sort/Build/iOS Simulator Xcode Project")]
        public static void BuildIOSSimulator()
        {
            MetroSortProjectSetup.RunSetup();
            ApplyAdMobAppId();
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
            try { Build(BuildTarget.iOS, "Builds/iOSSimulator"); }
            finally { PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK; }
        }

        private static void Build(BuildTarget target, string output, params string[] defines)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/MetroSort/Scenes/Bootstrap/Bootstrap.unity" },
                locationPathName = output,
                target = target,
                options = BuildOptions.None,
                extraScriptingDefines = defines
            };
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("Metro Sort build " + target + ": " + report.summary.result + " -> " + output);
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
