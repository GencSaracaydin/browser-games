using LongShadow.Core;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// Audio cue ids the UI plays (ASSET_MANIFEST ids; WP06 builds LIB_Audio from the manifest and skips missing cues
    /// with one log line). Hit-marker cues come from <see cref="AudioCues"/> (WP03 leaves them to the HUD).
    /// </summary>
    public static class UiCues
    {
        /// <summary>Typewriter key strike (title card, fail card, debrief).</summary>
        public const string TypeKey = "SFX_UI_TYPE_KEY";
        /// <summary>Teletype line burst (objective reveal, debrief line feed).</summary>
        public const string TeletypeLine = "SFX_UI_TELETYPE_LINE";
        /// <summary>Terminal blip for a new objective.</summary>
        public const string Objective = "SFX_UI_OBJECTIVE";
        /// <summary>Polaroid whir (intel card).</summary>
        public const string IntelCard = "SFX_UI_INTEL_CARD";
        /// <summary>Menu move tick.</summary>
        public const string MenuMove = "SFX_UI_MENU_MOVE";
        /// <summary>Menu confirm blip.</summary>
        public const string MenuSelect = "SFX_UI_MENU_SELECT";
        /// <summary>Hit marker tick (GDD §11).</summary>
        public const string HitMarker = AudioCues.UiHitMarker;
        /// <summary>Headshot click (GDD §11 "a headshot adds a distinct click").</summary>
        public const string Headshot = AudioCues.UiHeadshot;
        /// <summary>Kill confirm thud.</summary>
        public const string Kill = AudioCues.UiKill;
        /// <summary>Title stinger: teletype burst and a low hit (GDD §8.3).</summary>
        public const string TitleStinger = "STG_TITLE";
    }
}
