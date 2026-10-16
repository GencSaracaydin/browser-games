using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.World.Look.Stage
{
    /// <summary>
    /// Spherical-harmonics helpers for the ambient probe (GDD §9.1: <c>RenderSettings.ambientProbe</c> is a lerp between
    /// four stage probes). Projection goes through Unity's own <see cref="SphericalHarmonicsL2.AddDirectionalLight"/> so the
    /// coefficient convention always matches the engine; the scale is normalised against a uniform environment, so a sky
    /// of constant radiance L evaluates to L (Unity's skybox-ambient convention) whatever the engine's internal constants.
    /// Equirect coordinates follow Unity's Skybox/Panoramic: <c>u = 0.5 - atan2(z, x) / 2π</c>, <c>v = 1 - acos(y) / π</c>
    /// (row 0 = bottom).
    /// </summary>
    public static class AmbientSHMath
    {
        /// <summary>Coefficients per SH (3 colour channels x 9).</summary>
        public const int CoefficientCount = 27;

        /// <summary>Copies an SH into 27 floats (rgb-major).</summary>
        public static float[] ToArray(SphericalHarmonicsL2 sh)
        {
            var a = new float[CoefficientCount];
            for (var c = 0; c < 3; c++)
                for (var i = 0; i < 9; i++)
                    a[c * 9 + i] = sh[c, i];
            return a;
        }

        /// <summary>Builds an SH from 27 floats (rgb-major); a null or short array gives a cleared SH.</summary>
        public static SphericalHarmonicsL2 FromArray(float[] a)
        {
            var sh = new SphericalHarmonicsL2();
            sh.Clear();
            if (a == null || a.Length < CoefficientCount) return sh;
            for (var c = 0; c < 3; c++)
                for (var i = 0; i < 9; i++)
                    sh[c, i] = a[c * 9 + i];
            return sh;
        }

        /// <summary>True when the array holds a non-zero SH.</summary>
        public static bool IsValid(float[] a)
        {
            if (a == null || a.Length < CoefficientCount) return false;
            for (var i = 0; i < CoefficientCount; i++)
                if (Mathf.Abs(a[i]) > 1e-7f && !float.IsNaN(a[i]))
                    return true;
            return false;
        }

        /// <summary>World direction of an equirect texel centre (Skybox/Panoramic convention, row 0 = bottom).</summary>
        public static Vector3 EquirectDirection(int column, int row, int width, int height)
        {
            var u = (column + 0.5f) / width;
            var v = (row + 0.5f) / height;
            var theta = (1f - v) * Mathf.PI; // polar angle from +Y
            var phi = (0.5f - u) * 2f * Mathf.PI; // atan2(z, x)
            var st = Mathf.Sin(theta);
            return new Vector3(st * Mathf.Cos(phi), Mathf.Cos(theta), st * Mathf.Sin(phi));
        }

        /// <summary>Solid angle of an equirect texel in row <paramref name="row"/>.</summary>
        public static float EquirectSolidAngle(int row, int width, int height)
        {
            var theta = (1f - (row + 0.5f) / height) * Mathf.PI;
            return (2f * Mathf.PI / width) * (Mathf.PI / height) * Mathf.Sin(theta);
        }

        /// <summary>
        /// Projects an equirect radiance map (linear, Panoramic convention) into an SH whose evaluation equals the diffuse
        /// ambient of a white surface, i.e. a constant map of radiance L evaluates to L. <paramref name="step"/> skips
        /// texels for speed (4 on a 2048 x 1024 map is plenty for L2). <paramref name="scale"/> multiplies the radiance;
        /// <paramref name="rotationDegrees"/> rotates the map around +Y (sample azimuth = view azimuth - rotation).
        /// </summary>
        public static SphericalHarmonicsL2 ProjectEquirect(Color[] pixels, int width, int height, int step, Color scale,
            float rotationDegrees = 0f)
        {
            var sh = new SphericalHarmonicsL2();
            sh.Clear();
            if (pixels == null || width <= 0 || height <= 0 || pixels.Length < width * height) return sh;
            step = Mathf.Max(1, step);
            var rot = Quaternion.Euler(0f, rotationDegrees, 0f);
            var sign = DirectionSign();
            for (var r = 0; r < height; r += step)
            {
                var dOmega = EquirectSolidAngle(r, width, height) * step * step;
                for (var c = 0; c < width; c += step)
                {
                    var p = pixels[r * width + c];
                    var lum = p.r + p.g + p.b;
                    if (lum <= 0f || float.IsNaN(lum) || float.IsInfinity(lum)) continue;
                    var dir = rot * EquirectDirection(c, r, width, height);
                    sh.AddDirectionalLight(sign * dir, new Color(p.r * scale.r, p.g * scale.g, p.b * scale.b, 1f), dOmega / Mathf.PI);
                }
            }
            return sh * UniformNormaliser(width, height, step);
        }

        /// <summary>An SH for a sky/ground gradient (fallback when no HDRI capture exists): sky above, ground below.</summary>
        public static SphericalHarmonicsL2 Gradient(Color sky, Color horizon, Color ground)
        {
            const int w = 64, h = 32;
            var px = new Color[w * h];
            for (var r = 0; r < h; r++)
            {
                var y = Mathf.Cos((1f - (r + 0.5f) / h) * Mathf.PI);
                var c = y >= 0f ? Color.Lerp(horizon, sky, Mathf.Sqrt(y)) : Color.Lerp(horizon, ground, Mathf.Sqrt(-y));
                for (var col = 0; col < w; col++) px[r * w + col] = c;
            }
            return ProjectEquirect(px, w, h, 1, Color.white);
        }

        /// <summary>Evaluates the SH in one direction (allocates; editor and tests only).</summary>
        public static Color Evaluate(SphericalHarmonicsL2 sh, Vector3 direction)
        {
            var dirs = new[] { direction.normalized };
            var res = new Color[1];
            sh.Evaluate(dirs, res);
            return res[0];
        }

        private static float s_sign;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_sign = 0f;

        /// <summary>
        /// +1 if <see cref="SphericalHarmonicsL2.AddDirectionalLight"/> takes the direction toward the light (Unity's
        /// documented LightProbes example passes <c>-light.transform.forward</c>), -1 otherwise. Measured once.
        /// </summary>
        public static float DirectionSign()
        {
            if (s_sign != 0f) return s_sign;
            var sh = new SphericalHarmonicsL2();
            sh.Clear();
            sh.AddDirectionalLight(Vector3.up, Color.white, 1f);
            var up = Evaluate(sh, Vector3.up).r;
            var down = Evaluate(sh, Vector3.down).r;
            s_sign = up >= down ? 1f : -1f;
            return s_sign;
        }

        private static float UniformNormaliser(int width, int height, int step)
        {
            var sh = new SphericalHarmonicsL2();
            sh.Clear();
            var sign = DirectionSign();
            for (var r = 0; r < height; r += step)
            {
                var dOmega = EquirectSolidAngle(r, width, height) * step * step;
                for (var c = 0; c < width; c += step)
                    sh.AddDirectionalLight(sign * EquirectDirection(c, r, width, height), Color.white, dOmega / Mathf.PI);
            }
            var e = Evaluate(sh, Vector3.up).r;
            return e > 1e-6f ? 1f / e : 1f;
        }
    }
}
