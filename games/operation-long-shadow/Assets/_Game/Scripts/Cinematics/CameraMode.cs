namespace LongShadow.Cinematics
{
    /// <summary>Which camera owns the screen (see <c>CinematicCameras</c>).</summary>
    public enum CameraMode
    {
        /// <summary>The player rig's cameras (gameplay, title card, climax POV).</summary>
        Player,
        /// <summary>The cinematic world camera (insertion, MID-B projector POV).</summary>
        World,
        /// <summary>Cam_Booth in S0_Booth (booth shots, inserts, death insert).</summary>
        Booth,
    }
}
