using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace MetroSort.EditorTools
{
    /// <summary>
    /// Build steps for the Firebase Analytics bridge (Docs/ANALYTICS.md).
    ///
    /// Firebase's two configuration files are kept out of the repository (it is public), in Config/firebase:
    ///   google-services.json       Android; Tools/firebase_config.py turns it into the string resources of
    ///                              Assets/Plugins/Android/MetroFirebase.androidlib
    ///   GoogleService-Info.plist   iOS; copied into the Xcode project here
    /// A build without them still works and sends nothing, so every build says in its log which case it is.
    /// </summary>
    public sealed class FirebaseBuildSteps : IPreprocessBuildWithReport
    {
        public const string ConfigFolder = "Config/firebase";
        public const string AndroidResources = "Assets/Plugins/Android/MetroFirebase.androidlib/res/values/google-services.xml";
        private const string IosConfig = "GoogleService-Info.plist";
        // Collection starts switched off; the game switches it on after the consent step (FirebaseAnalyticsService).
        private const string CollectionKey = "FIREBASE_ANALYTICS_COLLECTION_ENABLED";
        // The ad-related consent signals follow the IAB consent string the ad SDK's consent form writes.
        private const string TcfKey = "GOOGLE_ANALYTICS_TCF_DATA_ENABLED";

        public int callbackOrder { get { return 0; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            if (target == BuildTarget.Android) Report(File.Exists(AndroidResources), AndroidResources);
            else if (target == BuildTarget.iOS) Report(File.Exists(Path.Combine(ConfigFolder, IosConfig)), Path.Combine(ConfigFolder, IosConfig));
        }

        private static void Report(bool present, string file)
        {
            if (present) Debug.Log("Metro Sort build: Firebase Analytics configured (" + file + ")");
            else Debug.LogWarning("Metro Sort build: NO ANALYTICS in this build: " + file + " is missing. See Docs/ANALYTICS.md.");
        }

        /// <summary>After the Podfile work (40-50) and the other Info.plist edits (60).</summary>
        [PostProcessBuild(61)]
        public static void ConfigureIos(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            SetFlag(Path.Combine(path, "Info.plist"), CollectionKey, false);
            SetFlag(Path.Combine(path, "Info.plist"), TcfKey, true);
            string source = Path.Combine(ConfigFolder, IosConfig);
            if (!File.Exists(source)) return;
            File.Copy(source, Path.Combine(path, IosConfig), true);
#if UNITY_IOS
            // The file has to be in the app bundle itself: that is where Firebase looks for it at start-up.
            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            string file = project.FindFileGuidByProjectPath(IosConfig);
            if (string.IsNullOrEmpty(file)) file = project.AddFile(IosConfig, IosConfig, PBXSourceTree.Source);
            project.AddFileToBuild(project.GetUnityMainTargetGuid(), file);
            project.WriteToFile(projectPath);
            Debug.Log("Metro Sort build: " + IosConfig + " added to the app");
#endif
        }

        /// <summary>Adds a yes/no key to Info.plist. Plain text edit, like IosEncryptionDeclaration.</summary>
        private static void SetFlag(string plist, string key, bool value)
        {
            if (!File.Exists(plist)) return;
            string text = File.ReadAllText(plist);
            if (text.Contains("<key>" + key + "</key>")) return;
            int end = text.LastIndexOf("</dict>", System.StringComparison.Ordinal);
            if (end < 0)
            {
                Debug.LogWarning("Metro Sort build: Info.plist is not an XML plist; " + key + " not added.");
                return;
            }
            File.WriteAllText(plist, text.Substring(0, end) + "\t<key>" + key + "</key>\n\t<" + (value ? "true" : "false") + "/>\n" + text.Substring(end));
            Debug.Log("Metro Sort build: " + key + " = " + (value ? "YES" : "NO"));
        }
    }
}
