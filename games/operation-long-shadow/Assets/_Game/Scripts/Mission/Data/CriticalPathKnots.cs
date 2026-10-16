using UnityEngine;

namespace LongShadow.Mission.Data
{
    /// <summary>
    /// The critical-path knots of TECH_PLAN Appendix A per beat (B1-B10; B11 is exfil time / 50 s). WP10's
    /// <c>ICriticalPath</c> is authoritative; the MissionDirector projects onto these polylines itself only while no
    /// ICriticalPath is registered (dev scenes, before WP10 lands), so LightStage progress still works (GDD §9.1).
    /// </summary>
    public static class CriticalPathKnots
    {
        private static readonly Vector3[][] s_knots =
        {
            null,
            new[] { V(42, 22, 30), V(70, 18, 50), V(82, 13, 62), V(90, 8, 72), V(98, 4, 82), V(108, 0, 96) },
            new[] { V(108, 0, 96), V(136, 0, 112), V(158, 0, 106), V(172, 0, 146), V(183, 0, 163), V(185, 0, 170) },
            new[] { V(185, 0, 170), V(185, 0, 200), V(185, 0, 216), V(185, 0, 246), V(185, 0, 252) },
            new[]
            {
                V(185, 0, 248), V(181, 0, 244), V(177, 0, 270), V(177, 0, 300), V(186, 0, 307), V(192, 0, 307), V(210, 0, 320),
                V(226, 0, 314), V(226, 4.5f, 315), V(214, 4.5f, 306), V(214, 4.5f, 301), V(227, 4.5f, 300), V(227, 0, 297),
                V(236, 0, 301),
            },
            new[]
            {
                V(236, 9, 304), V(240, 9, 318), V(247, 8.5f, 318), V(254, 8, 316), V(260, 9, 324), V(266, 10, 320),
                V(276, 11, 320), V(284, 12, 322), V(291, 12, 328),
            },
            new[] { V(291, 12, 330), V(295, 0, 334), V(296, 0, 310), V(302, 0, 322), V(316, 0, 316), V(330, 0, 314), V(338, 0, 328.5f) },
            new[]
            {
                V(338, 0.3f, 331), V(330, 0.3f, 342), V(346, 0.3f, 350), V(351, 0.3f, 355), V(334.5f, 0.3f, 358),
                V(334.5f, 4.8f, 358), V(335, 4.8f, 355),
            },
            new[]
            {
                V(335, 4.8f, 355), V(323, 4.8f, 352), V(334.5f, 4.8f, 358), V(334.5f, 0.3f, 358), V(322, 0.3f, 340),
                V(319, 0.3f, 337), V(320.5f, -4.2f, 343),
            },
            new[] { V(320.5f, -4.2f, 343), V(332, -4.2f, 348), V(344, -4.2f, 358), V(344, -4.2f, 362), V(344, -4.2f, 380), V(346, 0, 382) },
            new[] { V(346, 0, 383), V(346, -1, 391), V(340, -1, 390.5f), V(350, -1, 404) },
        };

        /// <summary>Beat 11 progress is exfil spline time divided by this (Appendix A).</summary>
        public const float ExfilSeconds = 50f;

        /// <summary>Knots of a beat (null for the intro, beat 11 and the outro).</summary>
        public static Vector3[] For(int beat) => beat >= 1 && beat < s_knots.Length ? s_knots[beat] : null;

        /// <summary>Normalised arc length of <paramref name="p"/> projected on the beat's polyline (not monotonic by itself).</summary>
        public static float Project01(int beat, Vector3 p)
        {
            var k = For(beat);
            if (k == null || k.Length < 2) return 0f;
            var total = 0f;
            for (var i = 1; i < k.Length; i++) total += Vector3.Distance(k[i - 1], k[i]);
            if (total <= 1e-4f) return 0f;
            var best = float.MaxValue;
            var bestAlong = 0f;
            var run = 0f;
            for (var i = 1; i < k.Length; i++)
            {
                var a = k[i - 1];
                var b = k[i];
                var ab = b - a;
                var len = ab.magnitude;
                var t = len > 1e-4f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / (len * len)) : 0f;
                var d = (a + ab * t - p).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    bestAlong = run + len * t;
                }
                run += len;
            }
            return Mathf.Clamp01(bestAlong / total);
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    }
}
