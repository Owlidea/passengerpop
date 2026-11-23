using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MetroSort.EditorTools
{
    /// <summary>
    /// Import settings for the artwork and audio that Tools/art/media_import.py copies into Resources/Art and
    /// Resources/Audio, plus the store icon.
    /// </summary>
    public sealed class MediaImport : AssetPostprocessor
    {
        private const string IconPath = "Assets/MetroSort/Art/AppIcon.png";
        // The two layers of the Android adaptive launcher icon, rendered from AppIcon.png by
        // Tools/art/android_adaptive_icon.swift (run it again whenever AppIcon.png changes).
        private const string AndroidBackgroundPath = "Assets/MetroSort/Art/AppIconAndroidBackground.png";
        private const string AndroidForegroundPath = "Assets/MetroSort/Art/AppIconAndroidForeground.png";

        private void OnPreprocessTexture()
        {
            // Resources/Brand holds the StagFunny studio logo (from Slingshire's assets/images/stag-funny-logo.png).
            bool art = assetPath.Contains("/Resources/Art/") || assetPath.Contains("/Resources/Brand/");
            bool icon = assetPath == IconPath || assetPath == AndroidBackgroundPath || assetPath == AndroidForegroundPath;
            if (!art && !icon) return;
            var importer = (TextureImporter)assetImporter;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            if (!art)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                return;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            // Mipmaps keep a picture smooth when it is drawn much smaller than it is (a star is 44 units in the
            // level grid and 230 on the win card). But Unity never compresses a non-power-of-two texture that has
            // mipmaps for Android: it ships as raw RGBA, four times the size (all art came to 172 MB that way). So
            // big odd-sized pictures (backgrounds, reward animations, drawn near their own size) go without mipmaps
            // and compress to ASTC; small odd-sized icons keep their mipmaps raw (they are tiny); power-of-two
            // pictures get both.
            int width, height;
            importer.GetSourceTextureWidthAndHeight(out width, out height);
            bool powerOfTwo = Mathf.IsPowerOfTwo(width) && Mathf.IsPowerOfTwo(height);
            importer.mipmapEnabled = powerOfTwo || Mathf.Max(width, height) <= 512;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        /// <summary>Bump when the rules above change: Unity then reimports everything this class handles.</summary>
        public override uint GetVersion() { return 2; }

        private void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/Resources/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            bool music = assetPath.Contains("theme") || assetPath.Contains("loop");
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
        }

        /// <summary>Uses Art/AppIcon.png as the icon on every platform. Also callable with -executeMethod.</summary>
        [MenuItem("Metro Sort/Apply App Icon")]
        public static void ApplyAppIcon()
        {
            TryApplyAppIcon();
        }

        /// <summary>
        /// Sets Art/AppIcon.png as the default icon, which iOS builds every size from, and the two AppIconAndroid
        /// layers as the Android adaptive icon (plus AppIcon.png as the legacy icon where Unity still offers one).
        /// Unity 6 never uses the default icon on Android: with the adaptive slots empty, the bundle ships Unity's
        /// own cube logo, as Passenger Pop 1.0.0 (version code 1) did. Returns false when the Android icon could not
        /// be set, so a store build stops instead.
        /// </summary>
        public static bool TryApplyAppIcon()
        {
            Texture2D icon = LoadIcon(IconPath);
            Texture2D background = LoadIcon(AndroidBackgroundPath);
            Texture2D foreground = LoadIcon(AndroidForegroundPath);
            if (icon == null || background == null || foreground == null) return false;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            int adaptive = 0, legacy = 0;
            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                string name = kind.ToString();
                bool isAdaptive = name.StartsWith("Adaptive"), isLegacy = name.StartsWith("Legacy");
                if (!isAdaptive && !isLegacy) continue; // round icons were only for Android 7.1; the game needs 8.0
                PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (PlatformIcon slot in slots)
                {
                    if (isAdaptive)
                    {
                        slot.SetTexture(background, 0);
                        slot.SetTexture(foreground, 1);
                    }
                    else
                    {
                        slot.SetTexture(icon, 0);
                    }
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
                if (isAdaptive) adaptive += slots.Length;
                else legacy += slots.Length;
            }
            AssetDatabase.SaveAssets();
            if (adaptive == 0)
            {
                Debug.LogError("Metro Sort: the Android adaptive icon could not be set; the build would show Unity's logo.");
                return false;
            }
            Debug.Log("Metro Sort: app icon applied (default icon, " + adaptive + " Android adaptive and " + legacy + " legacy sizes)");
            return true;
        }

        private static Texture2D LoadIcon(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) Debug.LogError("Metro Sort: no icon at " + path);
            return texture;
        }
    }
}
