using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// The terrain of <c>S1_Approach</c> as a pure height function (GDD §6.5 A1-A4, §7.1; TECH_PLAN §7.15; CINEMATIC_DIRECTION
    /// §2 apron). Built from four parts, all deterministic:
    /// <list type="bullet">
    /// <item>a regional tilt: the high desert in the south-west (Y ≈ 12, the insertion apron's dunes at 10-14) falling to
    /// the town shelf (Y 0) in the north-east;</item>
    /// <item>the Shoulder: a basalt ridge whose crest runs NW-SE through the spawn (42, 22, 30) at +20…+24, with a gentle
    /// reverse slope down to the apron (passing the insertion path (−10, 15, 0) and (22, 20, 18)) and a steeper face down to
    /// the grove;</item>
    /// <item>the wadi carved through that face along the Beat 1 critical path ((70, 18, 50) → (82, 13, 62) scree →
    /// (90, 8, 72) → boulder (98, 4, 82) → (108, 0, 96)), 6-10 m wide;</item>
    /// <item>flat ground (Y 0) for the grove (x 100-176, z 90-150), the Weighbridge (x 156-200, z 136-180) and slightly below
    /// the datum where S2's paving overlaps.</item>
    /// </list>
    /// The same function feeds the Unity Terrain (TerrainBuilder), the far mesh in <c>S9_TownFar</c> and the tests that check
    /// the critical-path knots lie on the ground.
    /// </summary>
    public static class ApproachHeightField
    {
        /// <summary>Terrain rectangle (x −220…200, z −160…180): playable approach plus the insertion apron.</summary>
        public static readonly AreaRect Extent = new AreaRect(-220f, -160f, 200f, 180f);
        /// <summary>Lowest terrain height (Terrain transform y).</summary>
        public const float MinY = -10f;
        /// <summary>Height range of the Terrain (size.y).</summary>
        public const float RangeY = 50f;

        private const float K = 0.70710678f;
        private const int Seed = 1984;

        /// <summary>The wadi floor polyline (x, floor y, z), head to mouth. Knots of the B1 critical path are on it.</summary>
        public static readonly Vector3[] Wadi =
        {
            new Vector3(60f, 20.6f, 41f), new Vector3(66f, 19.3f, 46f), new Vector3(70f, 18f, 50f), new Vector3(76f, 15.5f, 56f),
            new Vector3(82f, 13f, 62f), new Vector3(86f, 10.5f, 67f), new Vector3(90f, 8f, 72f), new Vector3(94f, 6f, 77f),
            new Vector3(98f, 4f, 82f), new Vector3(103f, 2f, 88f), new Vector3(108f, 0.4f, 94f), new Vector3(113f, 0f, 100f),
        };

        // Crest profile across the ridge: s = signed distance along the NE normal from the crest line through the spawn.
        private static readonly Vector2[] s_profile =
        {
            new Vector2(-300f, 12f), new Vector2(-140f, 12.1f), new Vector2(-103f, 12.5f), new Vector2(-80f, 13.4f),
            new Vector2(-58f, 15f), new Vector2(-40f, 17.2f), new Vector2(-22.6f, 20f), new Vector2(-8f, 21.6f),
            new Vector2(0f, 22f), new Vector2(12f, 21.6f), new Vector2(24f, 20.9f), new Vector2(34f, 20f), new Vector2(44f, 19f),
            new Vector2(51f, 18f), new Vector2(58f, 16.4f), new Vector2(64f, 14.2f), new Vector2(70f, 11.8f), new Vector2(76f, 9f),
            new Vector2(82f, 6f), new Vector2(88f, 3f), new Vector2(93f, 1.2f), new Vector2(98f, 0.3f), new Vector2(104f, 0f),
            new Vector2(300f, 0f),
        };

        /// <summary>Spawn pad centre (squad start slots around it are flattened to +22).</summary>
        public static readonly Vector3 SpawnPad = new Vector3(41f, 22f, 27.5f);

        /// <summary>Ridge coordinates: s across (positive towards the town), u along the crest (positive SE).</summary>
        public static void RidgeCoords(float x, float z, out float s, out float u)
        {
            var dx = x - 42f;
            var dz = z - 30f;
            s = (dx + dz) * K;
            u = (dx - dz) * K;
        }

        /// <summary>Height of the bare ridge before the wadi and the flats (metres).</summary>
        public static float RidgeSurface(float x, float z)
        {
            RidgeCoords(x, z, out var s, out var u);
            var profile = Profile(s);
            var regional = 12f * (1f - SmoothStep(-70f, 40f, s));
            var lateral = 1f - SmoothStep(40f, 85f, Mathf.Abs(u));
            var h = Mathf.Lerp(regional, profile, lateral);

            // Crest relief along the ridge (+20…+24), zero near the spawn so the prone start stays at +22.
            var crestWeight = Mathf.Exp(-(s * s) / 400f) * (1f - Mathf.Exp(-(u * u) / 196f)) * lateral;
            h += (1.3f * Mathf.Sin(u / 23f) + 0.8f * Mathf.Sin(u / 9.7f + 1.3f)) * crestWeight;

            // Natural surface: broad swells everywhere, rock-strata detail on the ridge, dune ridges on the apron.
            var broad = ValueNoise.Fbm(x / 60f, z / 60f, Seed, 3) * 1.1f;
            var detail = ValueNoise.Fbm(x / 9f, z / 9f, Seed + 7, 3) * 0.35f;
            var strata = ValueNoise.Ridged(x / 22f, z / 31f, Seed + 13) * 1.4f * lateral * SmoothStep(5f, 40f, s) * (1f - SmoothStep(80f, 100f, s));
            var dunes = (ValueNoise.Ridged((x + z * 0.6f) / 38f, (z - x * 0.6f) / 90f, Seed + 21) - 0.45f) * 3.2f * (1f - SmoothStep(-60f, -20f, s));
            h += broad * Mathf.Clamp01(lateral + 0.4f) + detail + strata + dunes * (1f - lateral * 0.6f);
            return h;
        }

        /// <summary>Final terrain height at a world XZ position.</summary>
        public static float Height(float x, float z)
        {
            var h = RidgeSurface(x, z);

            // Spawn pad: prone start (42, 22, 30) and the squad slots (36, 22, 28), (46, 22, 26), (39, 22, 24) at +22.
            var dPad = Vector2.Distance(new Vector2(x, z), new Vector2(SpawnPad.x, SpawnPad.z));
            h = Mathf.Lerp(22f + ValueNoise.Fbm(x / 4f, z / 4f, Seed + 3, 2) * 0.06f, h, SmoothStep(8f, 16f, dPad));

            // Wadi carve.
            var d = WadiDistance(x, z, out var floor, out var t);
            var halfWidth = Mathf.Lerp(1.6f, 4.0f, Mathf.Clamp01(t * 1.6f));
            if (d < halfWidth + 16f)
            {
                var floorNoise = ValueNoise.Fbm(x / 3f, z / 3f, Seed + 31, 2) * 0.12f;
                var bank = floor + floorNoise + Mathf.Max(0f, d - halfWidth) * 1.35f;
                if (bank < h) h = Mathf.Lerp(bank, h, SmoothStep(halfWidth + 9f, halfWidth + 16f, d));
            }

            // Grove, Weighbridge and the plain at the town datum.
            var flat = FlatWeight(x, z);
            if (flat > 0f)
            {
                var micro = ValueNoise.Fbm(x / 7f, z / 7f, Seed + 41, 2) * 0.06f;
                h = Mathf.Lerp(h, micro, flat);
            }

            // Under S2's paving (the market starts at z 170): keep the terrain just below the paving.
            if (x > 170f && z > 169f) h = Mathf.Min(h, -0.15f);
            return h;
        }

        /// <summary>Weight 0..1 of the flattened town-datum zones (grove, Weighbridge, plain east of the ridge).</summary>
        public static float FlatWeight(float x, float z)
        {
            var grove = RectWeight(x, z, new AreaRect(100f, 90f, 178f, 152f), 5f);
            var bridge = RectWeight(x, z, new AreaRect(156f, 132f, 200f, 182f), 6f);
            return Mathf.Max(grove, bridge);
        }

        private static float RectWeight(float x, float z, AreaRect r, float blend)
        {
            var d = r.DistanceTo(new Vector3(x, 0f, z));
            return 1f - SmoothStep(0f, blend, d);
        }

        /// <summary>Horizontal distance to the wadi polyline; outputs the floor height there and the fraction along it.</summary>
        public static float WadiDistance(float x, float z, out float floorY, out float fraction)
        {
            var p = new Vector3(x, 0f, z);
            var best = float.MaxValue;
            floorY = 0f;
            fraction = 0f;
            var total = 0f;
            for (var i = 1; i < Wadi.Length; i++) total += Vector3.Distance(Flat(Wadi[i - 1]), Flat(Wadi[i]));
            var run = 0f;
            for (var i = 1; i < Wadi.Length; i++)
            {
                var a = Flat(Wadi[i - 1]);
                var b = Flat(Wadi[i]);
                var c = MathX.ClosestPointOnSegment(a, b, p, out var t);
                var dist = Vector3.Distance(c, p);
                var seg = Vector3.Distance(a, b);
                if (dist < best)
                {
                    best = dist;
                    floorY = Mathf.Lerp(Wadi[i - 1].y, Wadi[i].y, t);
                    fraction = total > 0f ? (run + seg * t) / total : 0f;
                }
                run += seg;
            }
            return best;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static float Profile(float s)
        {
            if (s <= s_profile[0].x) return s_profile[0].y;
            for (var i = 1; i < s_profile.Length; i++)
            {
                if (s <= s_profile[i].x)
                {
                    var a = s_profile[i - 1];
                    var b = s_profile[i];
                    var t = (s - a.x) / (b.x - a.x);
                    // Smooth Hermite between knots so the slope has no kinks.
                    t = t * t * (3f - 2f * t);
                    return Mathf.Lerp(a.y, b.y, t);
                }
            }
            return s_profile[s_profile.Length - 1].y;
        }

        /// <summary>Hermite smoothstep from <paramref name="a"/> to <paramref name="b"/>.</summary>
        public static float SmoothStep(float a, float b, float x)
        {
            if (Mathf.Approximately(a, b)) return x < a ? 0f : 1f;
            var t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Surface normal from central differences (for slope-based splatting and scatter).</summary>
        public static Vector3 Normal(float x, float z, float step = 0.75f)
        {
            var hl = Height(x - step, z);
            var hr = Height(x + step, z);
            var hd = Height(x, z - step);
            var hu = Height(x, z + step);
            return new Vector3(hl - hr, 2f * step, hd - hu).normalized;
        }

        /// <summary>Slope in degrees at a point.</summary>
        public static float SlopeDegrees(float x, float z) => Vector3.Angle(Normal(x, z), Vector3.up);

        /// <summary>
        /// Terrain layer weights (sand, rocky trail, dry ground rocks, aerial sand), normalised. Layers follow the manifest's
        /// four terrain sets (TECH_PLAN §7.15).
        /// </summary>
        public static Vector4 Splat(float x, float z)
        {
            RidgeCoords(x, z, out var s, out var u);
            var slope = SlopeDegrees(x, z);
            var n = ValueNoise.Fbm(x / 14f, z / 14f, Seed + 51, 3);
            var wadiD = WadiDistance(x, z, out _, out var frac);
            var halfWidth = Mathf.Lerp(1.6f, 4.0f, Mathf.Clamp01(frac * 1.6f));

            var sand = 1f;
            var trail = 0f;
            var rocks = 0f;
            var aerial = 0f;

            // Rocky slopes and the basalt ridge top.
            var ridgeTop = (1f - SmoothStep(30f, 60f, Mathf.Abs(u))) * (1f - SmoothStep(25f, 55f, Mathf.Abs(s - 10f)));
            rocks = Mathf.Clamp01(SmoothStep(14f, 30f, slope) + ridgeTop * (0.55f + 0.35f * n));
            // Wadi floor and banks: rocky trail.
            trail = Mathf.Max(trail, 1f - SmoothStep(halfWidth, halfWidth + 3f, wadiD));
            // A worn footpath from the spawn to the wadi head and the Weighbridge track.
            trail = Mathf.Max(trail, PathWeight(x, z, new Vector3(42f, 0f, 30f), new Vector3(62f, 0f, 43f), 1.6f));
            trail = Mathf.Max(trail, PathWeight(x, z, new Vector3(183f, 0f, 145f), new Vector3(196f, 0f, 112f), 3.2f) * 0.8f);
            trail = Mathf.Max(trail, PathWeight(x, z, new Vector3(196f, 0f, 112f), new Vector3(205f, 0f, 70f), 3.2f) * 0.8f);
            // The apron and the open desert: wind-sorted aerial sand in broad patches.
            aerial = Mathf.Clamp01((1f - SmoothStep(-70f, -20f, s)) * (0.55f + 0.45f * ValueNoise.Fbm(x / 70f, z / 70f, Seed + 61, 2)));
            sand = Mathf.Clamp01(1f - rocks * 0.85f - trail - aerial * 0.6f);

            var sum = sand + trail + rocks + aerial;
            if (sum < 1e-4f) return new Vector4(1f, 0f, 0f, 0f);
            return new Vector4(sand / sum, trail / sum, rocks / sum, aerial / sum);
        }

        private static float PathWeight(float x, float z, Vector3 a, Vector3 b, float halfWidth)
        {
            var c = MathX.ClosestPointOnSegment(a, b, new Vector3(x, 0f, z), out _);
            var d = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(x, z));
            var wobble = ValueNoise.Signed(x / 5f, z / 5f, Seed + 71) * 0.6f;
            return 1f - SmoothStep(halfWidth * 0.5f, halfWidth + wobble, d);
        }
    }
}
