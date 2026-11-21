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

        private void OnPreprocessTexture()
        {
            // Resources/Brand holds the StagFunny studio logo (from Slingshire's assets/images/stag-funny-logo.png).
            bool art = assetPath.Contains("/Resources/Art/") || assetPath.Contains("/Resources/Brand/");
            if (!art && assetPath != IconPath) return;
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
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                Debug.LogWarning("Metro Sort: no icon at " + IconPath);
                return;
            }
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            AssetDatabase.SaveAssets();
            Debug.Log("Metro Sort: app icon applied");
        }
    }
}
