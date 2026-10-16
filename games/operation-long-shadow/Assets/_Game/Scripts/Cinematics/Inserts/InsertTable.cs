using LongShadow.Cinematics.Booth;
using LongShadow.Core;

namespace LongShadow.Cinematics.Inserts
{
    /// <summary>
    /// GDD §5.5 inserts as code data. I1 pen 0.9 s with D017 (the line runs on over gameplay); I2 the stopped clock for 6
    /// frames (MID-A, the mission plays D042 itself on "Calder?"); I3 the real room for 3 s with D054 (stand-alone
    /// version; MID-B plays it inside TL_MidB_Reel); I4 four cascading shots — clock 6 f, pen 5 f, CRT static 4 f,
    /// Calder's own eye 8 f — then white. The death insert has its own flow (<see cref="DeathSeconds"/>).
    /// </summary>
    public static class InsertTable
    {
        /// <summary>Death insert booth time (GDD §8.4: 2.0 s).</summary>
        public const float DeathSeconds = 2.0f;
        /// <summary>Death slow-down before the glitch (GDD §8.4: 0.6 s, time scale to 0.3).</summary>
        public const float DeathSlowSeconds = 0.6f;
        /// <summary>Time scale at the end of the death slow-down (TECH_PLAN §5.4).</summary>
        public const float DeathSlowScale = 0.3f;
        /// <summary>Death lines DI_01..DI_06, drawn without repeats (GDD §5.5).</summary>
        public static readonly string[] DeathLines = { "DI_01", "DI_02", "DI_03", "DI_04", "DI_05", "DI_06" };
        /// <summary>Teletype line on black while the checkpoint reloads (GDD §8.4).</summary>
        public const string ResumingText = "SESSION 12 — RESUMING";

        private static readonly InsertDef[] s_rows =
        {
            new InsertDef(SequenceIds.InsertI1, new[] { new InsertShot(BoothCameraPreset.PenEcu, 0.9f) },
                "D017", true, false, CinematicCues.StingerGlitch),
            new InsertDef(SequenceIds.InsertI2, new[] { InsertShot.Frames(BoothCameraPreset.Clock, 6) },
                null, true, false, CinematicCues.StingerGlitch),
            new InsertDef(SequenceIds.InsertI3, new[] { new InsertShot(BoothCameraPreset.FilmCam, 3.0f) },
                "D054", false, false, CinematicCues.StingerGlitch),
            new InsertDef(SequenceIds.InsertI4, new[]
                {
                    InsertShot.Frames(BoothCameraPreset.Clock, 6),
                    InsertShot.Frames(BoothCameraPreset.PenEcu, 5),
                    InsertShot.Frames(BoothCameraPreset.CrtEcu, 4, true),
                    InsertShot.Frames(BoothCameraPreset.EyeEcu, 8),
                },
                null, false, true, CinematicCues.StingerGlitch),
        };

        /// <summary>Every row.</summary>
        public static System.Collections.Generic.IReadOnlyList<InsertDef> All => s_rows;

        /// <summary>The row of an insert id, or null.</summary>
        public static InsertDef Get(string id)
        {
            for (var i = 0; i < s_rows.Length; i++)
                if (s_rows[i].Id == id) return s_rows[i];
            return null;
        }
    }
}
