using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace MetroSort.EditorTools
{
    /// <summary>
    /// The ad SDK pods (Google Mobile Ads, User Messaging Platform) still declare iOS 12.0, and Xcode 27 refuses to
    /// build anything below iOS 15, so the exported Xcode project failed to compile. External Dependency Manager
    /// writes the Podfile at post-process order 40 and runs pod install at 50; in between, this adds a post_install
    /// hook that lifts every pod to the game's own minimum iOS version.
    /// </summary>
    public static class IosPodfileFix
    {
        [PostProcessBuild(45)]
        public static void LiftPodDeploymentTarget(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string podfile = Path.Combine(path, "Podfile");
            if (!File.Exists(podfile)) return;
            if (File.ReadAllText(podfile).Contains("post_install")) return;
            string version = PlayerSettings.iOS.targetOSVersionString;
            File.AppendAllText(podfile,
                "\n# Metro Sort: Xcode 27 builds nothing below iOS 15, but the ad SDK pods still declare 12.0.\n" +
                "post_install do |installer|\n" +
                "  installer.pods_project.targets.each do |target|\n" +
                "    target.build_configurations.each do |config|\n" +
                "      config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '" + version + "'\n" +
                "    end\n" +
                "  end\n" +
                "end\n");
            Debug.Log("Metro Sort build: pods lifted to iOS " + version);
        }
    }
}
