using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// Critical-path knots per beat (TECH_PLAN Appendix A "Critical-path knots", ids <c>CritPath.B1</c>…<c>B10</c>).
    /// <c>ICriticalPath.Progress01</c> projects the player onto these polylines. Beat 11 has no polyline: its progress is
    /// the exfil spline time / 50 s (Appendix A).
    /// </summary>
    public static class CriticalPathTable
    {
        /// <summary>Number of beats (1..11).</summary>
        public const int BeatCount = 11;

        private static readonly Vector3[][] s_knots =
        {
            null,
            // B1 (42, 22, 30) (70, 18, 50) (82, 13, 62) (90, 8, 72) (98, 4, 82) (108, 0, 96)
            new[] { V(42, 22, 30), V(70, 18, 50), V(82, 13, 62), V(90, 8, 72), V(98, 4, 82), V(108, 0, 96) },
            // B2
            new[] { V(108, 0, 96), V(136, 0, 112), V(158, 0, 106), V(172, 0, 146), V(183, 0, 163), V(185, 0, 170) },
            // B3
            new[] { V(185, 0, 170), V(185, 0, 200), V(185, 0, 216), V(185, 0, 246), V(185, 0, 252) },
            // B4
            new[]
            {
                V(185, 0, 248), V(181, 0, 244), V(177, 0, 270), V(177, 0, 300), V(186, 0, 307), V(192, 0, 307), V(210, 0, 320),
                V(226, 0, 314), V(226, 4.5f, 315), V(214, 4.5f, 306), V(214, 4.5f, 301), V(227, 4.5f, 300), V(227, 0, 297),
                V(236, 0, 301),
            },
            // B5
            new[] { V(236, 9, 304), V(240, 9, 318), V(247, 8.5f, 318), V(254, 8, 316), V(260, 9, 324), V(266, 10, 320), V(276, 11, 320), V(284, 12, 322), V(291, 12, 328) },
            // B6
            new[] { V(291, 12, 330), V(295, 0, 334), V(296, 0, 310), V(302, 0, 322), V(316, 0, 316), V(330, 0, 314), V(338, 0, 328.5f) },
            // B7
            new[] { V(338, 0.3f, 331), V(330, 0.3f, 342), V(346, 0.3f, 350), V(351, 0.3f, 355), V(334.5f, 0.3f, 358), V(334.5f, 4.8f, 358), V(335, 4.8f, 355) },
            // B8
            new[] { V(335, 4.8f, 355), V(323, 4.8f, 352), V(334.5f, 4.8f, 358), V(334.5f, 0.3f, 358), V(322, 0.3f, 340), V(319, 0.3f, 337), V(320.5f, -4.2f, 343) },
            // B9
            new[] { V(320.5f, -4.2f, 343), V(332, -4.2f, 348), V(344, -4.2f, 358), V(344, -4.2f, 362), V(344, -4.2f, 380), V(346, 0, 382) },
            // B10
            new[] { V(346, 0, 383), V(346, -1, 391), V(340, -1, 390.5f), V(350, -1, 404) },
            // B11: time-based (exfil spline time / 50 s)
            null,
        };

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>Knots of a beat's polyline (null for Beat 11 and unknown beats). Do not modify the array.</summary>
        public static Vector3[] Knots(int beat) => beat >= 1 && beat <= BeatCount ? s_knots[beat] : null;

        /// <summary>Appendix A id of a beat's polyline ("CritPath.B4").</summary>
        public static string IdOf(int beat) => "CritPath.B" + beat;

        /// <summary>Parses "CritPath.B4" → 4 (0 when not a critical-path id).</summary>
        public static int BeatOf(string id)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith("CritPath.B")) return 0;
            return int.TryParse(id.Substring("CritPath.B".Length), out var b) && b >= 1 && b <= BeatCount ? b : 0;
        }
    }
}
