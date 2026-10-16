using System.Collections.Generic;
using System.IO;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEngine;

namespace LongShadow.World.Look.Editor
{
    /// <summary>
    /// Reads WP10_CC0's <c>&lt;sky&gt;_sky.json</c> files (written by <c>Tools/tex/hdri_prep.py</c> next to the LDR skies:
    /// sun measurement, rotation convention, LDR scale, exposure multiplier per unit sun intensity, horizon/zenith colours,
    /// SH). Looks in <c>Assets/_Game/Art/HDRI/Runtime/</c> first, then in the content agent's ready tree.
    /// </summary>
    public sealed class SkyCalibration
    {
        /// <summary>Sky id ("kiara_8_sunset").</summary>
        public string SkyId;
        /// <summary>True when the json was found and parsed.</summary>
        public bool Loaded;
        /// <summary>Linear sky multiplier per unit of realtime sun intensity (physically consistent calibration).</summary>
        public float ExposurePerUnitSun = 0.1452f;
        /// <summary>LDR linear scale (texel = radiance x scale below the shoulder).</summary>
        public float LinearScale = 1f;
        /// <summary>Mean horizon colour of the LDR texture (sRGB).</summary>
        public Color Horizon = new Color(0.76f, 0.83f, 0.82f);
        /// <summary>Mean zenith colour of the LDR texture (sRGB).</summary>
        public Color Zenith = new Color(0.31f, 0.38f, 0.53f);
        /// <summary>Measured sun elevation in the photo (degrees; NaN for the dusk glow).</summary>
        public float SunElevation = float.NaN;
        /// <summary>Path of the json that was read.</summary>
        public string SourcePath;

        /// <summary>Json path candidates for a sky id.</summary>
        public static IEnumerable<string> Candidates(string skyId)
        {
            yield return LookPaths.HdriRuntimeFolder + "/" + skyId + "_sky.json";
            yield return "RawAssets/wp10/ready/" + LookPaths.HdriRuntimeFolder + "/" + skyId + "_sky.json";
        }

        /// <summary>Loads a sky calibration (defaults when missing; one warning).</summary>
        public static SkyCalibration Load(string skyId)
        {
            var c = new SkyCalibration { SkyId = skyId };
            foreach (var rel in Candidates(skyId))
            {
                var full = Path.Combine(BuilderUtil.ProjectRoot, rel);
                if (!File.Exists(full)) continue;
                try
                {
                    if (!(MiniJson.Parse(File.ReadAllText(full)) is Dictionary<string, object> root)) continue;
                    var ldr = MiniJson.Obj(root, "ldr");
                    if (ldr != null)
                    {
                        c.ExposurePerUnitSun = (float)(MiniJson.Num(ldr, "exposure_multiplier_per_unit_sun_intensity") ?? c.ExposurePerUnitSun);
                        c.LinearScale = (float)(MiniJson.Num(ldr, "linear_scale") ?? 1.0);
                        if (ColorUtility.TryParseHtmlString(MiniJson.Str(ldr, "horizon_mean_srgb_hex") ?? "", out var h)) c.Horizon = h;
                        if (ColorUtility.TryParseHtmlString(MiniJson.Str(ldr, "zenith_mean_srgb_hex") ?? "", out var z)) c.Zenith = z;
                    }
                    var sun = MiniJson.Obj(root, "sun");
                    if (sun != null) c.SunElevation = (float)(MiniJson.Num(sun, "elevation_deg") ?? double.NaN);
                    c.Loaded = true;
                    c.SourcePath = rel;
                    return c;
                }
                catch (System.Exception e)
                {
                    LSLog.Warn(LogCategory.Look, "Sky json " + rel + " unreadable: " + e.Message);
                }
            }
            LSLog.MissingAsset(LogCategory.Look, skyId + "_sky.json", LookPaths.HdriRuntimeFolder);
            return c;
        }
    }
}
