using UnityEditor;

namespace MetroSort.EditorTools
{
    /// <summary>
    /// Import settings for the models exported from Blender (Tools/art). Character clips are played from code
    /// through the legacy Animation component, so no Animator controller asset is needed; props are static.
    /// </summary>
    public sealed class CharacterModelImport : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            bool character = assetPath.Contains("/Resources/Characters/");
            bool prop = assetPath.Contains("/Resources/Models/");
            if (!character && !prop) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = character ? ModelImporterAnimationType.Legacy : ModelImporterAnimationType.None;
            importer.importAnimation = character;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        /// <summary>UI icons rendered by Tools/art/icons.py are loaded as sprites.</summary>
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/UI/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.maxTextureSize = 256;
        }
    }
}
