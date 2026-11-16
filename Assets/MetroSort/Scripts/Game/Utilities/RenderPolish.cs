using UnityEngine;
using UnityEngine.Rendering;
#if METROSORT_URP
using UnityEngine.Rendering.Universal;
#endif

namespace MetroSort.Game
{
    /// <summary>
    /// Optional image polish when the Universal Render Pipeline is active: 4x MSAA and a light post-processing
    /// stack (a touch more saturation and contrast, soft bloom, gentle vignette). Does nothing on the
    /// built-in pipeline, and nothing here affects gameplay.
    /// </summary>
    public static class RenderPolish
    {
        public static void Apply(Camera cam, Transform parent)
        {
#if METROSORT_URP
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return;
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 48f; // tighter range = crisper shadow edges

            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;

            var go = new GameObject("PostProcessing");
            go.transform.SetParent(parent, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(12f);
            color.contrast.Override(6f);
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.08f);
            bloom.scatter.Override(0.6f);
            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.12f);
            vignette.smoothness.Override(0.55f);
            volume.profile = profile;
#endif
        }
    }
}
