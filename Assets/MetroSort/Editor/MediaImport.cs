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
            bool art = assetPath.Contains("/Resources/Art/");
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
            // These are drawn at many sizes (a star is 44 units in the level grid and 230 on the win card).
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }

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
