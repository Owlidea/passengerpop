using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace MetroSort.EditorTools
{
    /// <summary>
    /// The game's only encryption is the exempt kind (HTTPS inside the ad and purchase SDKs and iOS itself), so the
    /// exported Info.plist says so. App Store Connect then skips its export compliance question on every upload.
    /// Plain text edit: no dependency on the iOS build module, so the editor scripts compile without it.
    /// </summary>
    public static class IosEncryptionDeclaration
    {
        private const string Key = "ITSAppUsesNonExemptEncryption";

        [PostProcessBuild(60)]
        public static void Declare(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plist = Path.Combine(path, "Info.plist");
            if (!File.Exists(plist)) return;
            string text = File.ReadAllText(plist);
            if (text.Contains("<key>" + Key + "</key>")) return;
            int end = text.LastIndexOf("</dict>", System.StringComparison.Ordinal);
            if (end < 0)
            {
                Debug.LogWarning("Metro Sort build: Info.plist is not an XML plist; " + Key + " not added.");
                return;
            }
            File.WriteAllText(plist, text.Substring(0, end) + "\t<key>" + Key + "</key>\n\t<false/>\n" + text.Substring(end));
            Debug.Log("Metro Sort build: " + Key + " = NO");
        }
    }
}
