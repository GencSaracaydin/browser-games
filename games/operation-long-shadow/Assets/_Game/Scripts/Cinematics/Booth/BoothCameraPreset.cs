namespace LongShadow.Cinematics.Booth
{
    /// <summary>Booth camera set-ups used by shots and inserts (positions in <see cref="BoothLayout"/>).</summary>
    public enum BoothCameraPreset
    {
        /// <summary>C_POV, Calder's eye (0, 1.18, 0.05), vertical FOV 50°.</summary>
        Pov,
        /// <summary>C_CLOCK, ECU of the stopped wall clock (intro shot 2, I2, I4).</summary>
        Clock,
        /// <summary>ECU of the pen on the steel table, mid-tap (I1, I4).</summary>
        PenEcu,
        /// <summary>ECU of CRT2 on static (I4, death insert static).</summary>
        CrtEcu,
        /// <summary>ECU of Calder's own eye (I4).</summary>
        EyeEcu,
        /// <summary>The 16 mm film camera's angle on the chair (MID-B I3).</summary>
        FilmCam,
        /// <summary>The deck feed angle on the reel-to-reel.</summary>
        DeckFeed,
    }
}
