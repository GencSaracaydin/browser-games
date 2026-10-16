namespace LongShadow.Player.View
{
    /// <summary>
    /// Where the 1980s grade (TECH_PLAN §3.7) runs in the camera stack. URP 17 applies post-processing on every camera
    /// that enables it, so enabling it on both Cam_World and Cam_Viewmodel would tonemap and grain the world twice.
    /// </summary>
    public enum ViewmodelPostMode
    {
        /// <summary>
        /// Default: post-processing (and SMAA) only on Cam_Viewmodel, the last camera of the stack, so the world and the
        /// view model are graded once, together (grain, bloom and tonemapping also cover the gun and arms).
        /// </summary>
        GradeOnceOnOverlay = 0,
        /// <summary>
        /// Fallback: post-processing only on Cam_World; the view model is drawn after the grade (use if depth-based
        /// effects such as DoF misbehave on the overlay in the vertical-slice review).
        /// </summary>
        GradeWorldOnly = 1,
    }
}
