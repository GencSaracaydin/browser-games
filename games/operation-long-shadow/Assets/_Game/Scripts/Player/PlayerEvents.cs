using LongShadow.Core;

namespace LongShadow.Player
{
    /// <summary>
    /// Mission event ids the player raises through <c>GameEvents.RaiseMissionEvent</c>. Local adapter until the Core
    /// change request "SequenceIds.GlassedPrefix" (Docs/CORE_CHANGES.md, WP02) lands; WP07 listens for the same string.
    /// <para>The requested Core constants landed (LAND pass, 2026-10-16): the shared ids here are now thin aliases of
    /// <c>LongShadow.Core.SequenceIds</c>; new code should use the Core constants directly.</para>
    /// </summary>
    public static class PlayerEvents
    {
        /// <summary>
        /// Raised once per <c>GlassingPOI</c> when the binocular view keeps it within its radius for its dwell time
        /// (GDD §7.1): "Glassed." + the POI id, e.g. "Glassed.B1.POI1". Cleared on checkpoint load.
        /// </summary>
        public const string GlassedPrefix = SequenceIds.GlassedPrefix;

        /// <summary>Prompt id used for the interaction prompt (<c>IHud.ShowPrompt</c>/<c>HidePrompt</c>).</summary>
        public const string InteractPromptId = "Interact";
    }
}
