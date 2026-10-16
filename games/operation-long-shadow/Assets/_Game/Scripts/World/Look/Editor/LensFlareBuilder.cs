using System.Text;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.World.Look.Editor
{
    /// <summary>
    /// Builds <c>Assets/_Game/Art/VFX/LF_Sun.asset</c>, the data-driven lens flare (URP Lens Flare SRP) of the low sun
    /// (GDD §9.1 "sun disc billboard and lens-flare intensity", CINEMATIC_DIRECTION §1.1 "lens flare and glare when the
    /// camera looks toward the sun"): a warm glow, a faint halo ring, a thin anamorphic-style streak (period 1980s glass)
    /// and a few hexagonal aperture ghosts. Subtle by design: the LightStage scales the component intensity by the sun.
    /// </summary>
    public static class LensFlareBuilder
    {
        [MenuItem("LongShadow/Look/Build Sun Lens Flare", priority = 112)]
        public static void BuildMenu()
        {
            var log = new StringBuilder();
            Build(log);
            Debug.Log("[LS:Look] " + log);
        }

        /// <summary>Builds or updates LF_Sun.</summary>
        public static LensFlareDataSRP Build(StringBuilder log)
        {
            var data = BuilderUtil.LoadOrCreateAsset<LensFlareDataSRP>(LookPaths.LensFlareSun);
            data.elements = CreateElements();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            log.Append("LF_Sun: " + data.elements.Length + " elements. ");
            return data;
        }

        /// <summary>The flare elements.</summary>
        public static LensFlareDataElementSRP[] CreateElements()
        {
            var warm = new Color(1f, 0.78f, 0.55f, 1f);

            var glow = new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Circle,
                localIntensity = 0.9f,
                position = 0f,
                uniformScale = 0.22f,
                sizeXY = Vector2.one,
                tint = warm,
                modulateByLightColor = true,
                blendMode = SRPLensFlareBlendMode.Additive,
                fallOff = 0.08f,
                edgeOffset = 0.95f,
            };

            var halo = new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Ring,
                localIntensity = 0.06f,
                position = 0f,
                uniformScale = 0.9f,
                ringThickness = 0.04f,
                tint = new Color(1f, 0.85f, 0.7f, 1f),
                modulateByLightColor = true,
                blendMode = SRPLensFlareBlendMode.Additive,
                fallOff = 0.4f,
                edgeOffset = 0.3f,
            };

            var streak = new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Circle,
                localIntensity = 0.14f,
                position = 0f,
                uniformScale = 1f,
                sizeXY = new Vector2(2.6f, 0.025f),
                preserveAspectRatio = false,
                tint = new Color(1f, 0.72f, 0.5f, 1f),
                modulateByLightColor = true,
                blendMode = SRPLensFlareBlendMode.Additive,
                fallOff = 0.2f,
                edgeOffset = 0.9f,
            };

            var ghosts = new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Polygon,
                sideCount = 6,
                sdfRoundness = 0.15f,
                localIntensity = 0.05f,
                position = -0.55f,
                uniformScale = 0.11f,
                allowMultipleElement = true,
                count = 4,
                distribution = SRPLensFlareDistribution.Uniform,
                lengthSpread = 1.1f,
                tint = new Color(0.85f, 0.7f, 0.55f, 1f),
                modulateByLightColor = true,
                blendMode = SRPLensFlareBlendMode.Additive,
                fallOff = 0.6f,
                edgeOffset = 0.25f,
            };

            return new[] { glow, halo, streak, ghosts };
        }
    }
}
