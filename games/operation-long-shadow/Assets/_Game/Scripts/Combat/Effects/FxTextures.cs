using UnityEngine;

namespace LongShadow.Combat.Effects
{
    /// <summary>
    /// Small procedural textures for placeholder FX when the WP10 VFX textures (TEX_VFX_*) or the decal atlases have not
    /// landed: a soft particle dot, a tracer streak (red top half, green bottom half, like T_VFX_Tracer_E), a 4×2 muzzle
    /// sheet (row 0 side flames, row 1 front stars, like T_VFX_MuzzleFlash_4x2_E) and a bullet-hole decal. Generated once
    /// per session (statics reset on SubsystemRegistration).
    /// </summary>
    public static class FxTextures
    {
        private static Texture2D s_softDot, s_tracer, s_muzzle, s_hole;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_softDot = s_tracer = s_muzzle = s_hole = null;

        /// <summary>White radial dot with soft alpha (64 px).</summary>
        public static Texture2D SoftDot => s_softDot != null ? s_softDot : (s_softDot = BuildSoftDot(64));

        /// <summary>256×64 tracer streak: v 0.5-1 red, v 0-0.5 green; tapered ends.</summary>
        public static Texture2D Tracer => s_tracer != null ? s_tracer : (s_tracer = BuildTracer());

        /// <summary>512×256 muzzle atlas, 4×2 cells.</summary>
        public static Texture2D MuzzleSheet => s_muzzle != null ? s_muzzle : (s_muzzle = BuildMuzzle());

        /// <summary>128 px bullet hole (dark crater, pale chipped rim) with alpha.</summary>
        public static Texture2D BulletHole => s_hole != null ? s_hole : (s_hole = BuildHole(128));

        /// <summary>Builds a soft dot of a size (editor builders save it as a PNG).</summary>
        public static Texture2D BuildSoftDot(int size)
        {
            var t = New(size, size, "T_FX_SoftDot");
            var px = new Color32[size * size];
            var c = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = (x - c) / c;
                var dy = (y - c) / c;
                var r = Mathf.Sqrt(dx * dx + dy * dy);
                var a = Mathf.Clamp01(1f - r);
                a = a * a * (3f - 2f * a);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }

        /// <summary>Builds the tracer streak texture.</summary>
        public static Texture2D BuildTracer()
        {
            const int w = 256, h = 64;
            var t = New(w, h, "T_FX_Tracer");
            var px = new Color32[w * h];
            for (var y = 0; y < h; y++)
            {
                var half = y >= h / 2; // top half (v >= 0.5) = red
                var vy = ((y % (h / 2)) + 0.5f) / (h / 2f) * 2f - 1f;
                var across = Mathf.Exp(-(vy * vy) / 0.05f);
                var core = Mathf.Exp(-(vy * vy) / 0.006f);
                for (var x = 0; x < w; x++)
                {
                    var u = (x + 0.5f) / w;
                    var along = Mathf.SmoothStep(0f, 1f, u / 0.85f) * Mathf.SmoothStep(0f, 1f, (1f - u) / 0.04f);
                    var i = along * across;
                    Color col = half ? new Color(1f, 0.32f, 0.18f) : new Color(0.45f, 1f, 0.4f);
                    col = Color.Lerp(col, Color.white, core * along);
                    px[y * w + x] = new Color(col.r * i + core * along * 0.3f, col.g * i + core * along * 0.3f, col.b * i + core * along * 0.3f, 1f);
                }
            }
            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }

        /// <summary>Builds the muzzle flash atlas (4 columns × 2 rows; row 0 at the top = side flames).</summary>
        public static Texture2D BuildMuzzle()
        {
            const int cell = 128, w = cell * 4, h = cell * 2;
            var t = New(w, h, "T_FX_Muzzle");
            var px = new Color32[w * h];
            var rng = new System.Random(1984);
            for (var col = 0; col < 4; col++)
            {
                var lenScale = 0.75f + 0.25f * (float)rng.NextDouble();
                var spikes = 5 + rng.Next(3);
                var phase = (float)rng.NextDouble() * Mathf.PI;
                for (var y = 0; y < cell; y++)
                for (var x = 0; x < cell; x++)
                {
                    // Side flame (top row in the image = higher y in texture space).
                    var u = (x + 0.5f) / cell;
                    var v = (y + 0.5f) / cell * 2f - 1f;
                    var widthAt = 0.08f + 0.32f * Mathf.Sin(Mathf.Clamp01(u / lenScale) * Mathf.PI) * (1f - u * 0.4f);
                    var side = u <= lenScale ? Mathf.Exp(-(v * v) / (widthAt * widthAt + 1e-4f)) * Mathf.SmoothStep(0f, 0.08f, u) * (1f - u / lenScale) : 0f;
                    side += Mathf.Exp(-((u - 0.05f) * (u - 0.05f) + v * v) / 0.004f);
                    Put(px, w, col * cell + x, cell + y, side);
                    // Front star (bottom row).
                    var dx = (x + 0.5f) / cell * 2f - 1f;
                    var dy = (y + 0.5f) / cell * 2f - 1f;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    var ang = Mathf.Atan2(dy, dx);
                    var star = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * spikes * 0.5f + phase)), 6f) * Mathf.Exp(-r / 0.35f);
                    star += Mathf.Exp(-r * r / 0.03f);
                    Put(px, w, col * cell + x, y, Mathf.Clamp01(star));
                }
            }
            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }

        /// <summary>Builds a bullet-hole decal texture.</summary>
        public static Texture2D BuildHole(int size)
        {
            var t = New(size, size, "T_FX_BulletHole");
            var px = new Color32[size * size];
            var c = (size - 1) * 0.5f;
            var rng = new System.Random(7);
            var jag = new float[32];
            for (var i = 0; i < jag.Length; i++) jag[i] = 0.85f + 0.3f * (float)rng.NextDouble();
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = (x - c) / c;
                var dy = (y - c) / c;
                var r = Mathf.Sqrt(dx * dx + dy * dy);
                var a01 = (Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 0.5f) * jag.Length;
                var j = Mathf.Lerp(jag[(int)a01 % jag.Length], jag[((int)a01 + 1) % jag.Length], a01 - Mathf.Floor(a01));
                var rr = r / j;
                var crater = Mathf.Clamp01(1f - rr / 0.22f);
                var rim = Mathf.Clamp01(1f - Mathf.Abs(rr - 0.42f) / 0.2f);
                var alpha = Mathf.Clamp01(crater * 1.4f + rim * 0.75f) * Mathf.Clamp01((0.95f - rr) / 0.2f);
                var shade = Mathf.Lerp(0.78f, 0.06f, crater);
                px[y * size + x] = new Color(shade, shade * 0.97f, shade * 0.93f, alpha);
            }
            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }

        private static void Put(Color32[] px, int w, int x, int y, float i)
        {
            i = Mathf.Clamp01(i);
            var c = new Color(Mathf.Clamp01(i * 1.0f), Mathf.Clamp01(i * 0.82f), Mathf.Clamp01(i * 0.55f), 1f);
            px[y * w + x] = c;
        }

        private static Texture2D New(int w, int h, string name) =>
            new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
    }
}
