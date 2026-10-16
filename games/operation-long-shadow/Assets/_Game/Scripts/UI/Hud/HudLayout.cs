using UnityEngine;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// HUD placement in reference pixels (canvas 1920 × 1080, Scale With Screen Size, match 0.5; GDD §11). Widths that
    /// depend on the canvas aspect are computed so no two static elements overlap from 4:3 to 21:9 (checked by the
    /// layout audit at 1920 × 1080, 1280 × 720, 1024 × 768 and 2560 × 1080).
    /// </summary>
    public static class HudLayout
    {
        /// <summary>Screen-edge margin.</summary>
        public const float Margin = 56f;

        /// <summary>Compass strip size (GDD §11: 640 × 28 px, top centre).</summary>
        public static readonly Vector2 CompassStrip = new Vector2(640f, 28f);
        /// <summary>Compass block height including labels and the heading readout.</summary>
        public const float CompassBlockHeight = 66f;
        /// <summary>Distance of the compass block from the top edge.</summary>
        public const float CompassTop = 28f;
        /// <summary>Half the field shown by the compass (GDD §11: the 180 degrees in front).</summary>
        public const float CompassHalfField = 90f;

        /// <summary>Objective block distance from the top edge.</summary>
        public const float ObjectiveTop = 40f;
        /// <summary>Objective line font size.</summary>
        public const float ObjectiveFont = 22f;
        /// <summary>Objective line pitch.</summary>
        public const float ObjectiveLine = 30f;
        /// <summary>Objective lines shown without Tab.</summary>
        public const int ObjectiveLinesCollapsed = 3;

        /// <summary>Objective block width: up to 620 px, never reaching the compass.</summary>
        public static float ObjectiveWidth(Vector2 canvas) =>
            Mathf.Clamp(canvas.x * 0.5f - CompassStrip.x * 0.5f - Margin - 32f, 360f, 620f);

        /// <summary>Ammo panel size (bottom-right).</summary>
        public static readonly Vector2 Ammo = new Vector2(400f, 156f);
        /// <summary>Ammo panel distance from the bottom edge.</summary>
        public const float AmmoBottom = 40f;

        /// <summary>Subtitle stack distance from the bottom edge.</summary>
        public const float SubtitleBottom = 56f;
        /// <summary>Subtitle box maximum width (GDD §11: at most 1,100 px).</summary>
        public const float SubtitleMaxWidth = 1100f;
        /// <summary>Subtitle entries shown at once.</summary>
        public const int SubtitleMaxEntries = 2;

        /// <summary>Subtitle width: at most 1,100 px and clear of the ammo panel.</summary>
        public static float SubtitleWidth(Vector2 canvas) =>
            Mathf.Clamp(2f * (canvas.x * 0.5f - Ammo.x - Margin - 24f), 560f, SubtitleMaxWidth);

        /// <summary>Height reserved for the subtitle stack (two entries of two lines at the largest size).</summary>
        public const float SubtitleStackHeight = 220f;

        /// <summary>Prompt distance from the bottom edge (above the subtitle stack).</summary>
        public const float PromptBottom = SubtitleBottom + SubtitleStackHeight + 20f;
        /// <summary>Prompt block size.</summary>
        public static readonly Vector2 Prompt = new Vector2(900f, 60f);

        /// <summary>Tutorial box distance from the top edge (left side, below the objectives).</summary>
        public const float TutorialTop = 380f;
        /// <summary>Tutorial box size.</summary>
        public static readonly Vector2 Tutorial = new Vector2(520f, 110f);

        /// <summary>Intel polaroid size on screen (half of the 560 × 670 art).</summary>
        public static readonly Vector2 IntelCard = new Vector2(280f, 335f);
        /// <summary>Intel card vertical offset from the screen middle.</summary>
        public const float IntelCardY = 60f;

        /// <summary>Banner distance from the top edge (below the compass and the objective block).</summary>
        public const float BannerTop = 180f;
        /// <summary>Banner size.</summary>
        public static readonly Vector2 Banner = new Vector2(980f, 64f);

        /// <summary>Radius of the damage-direction and grenade ring (GDD §11 "160 px ring", read as the radius).</summary>
        public const float DamageRingRadius = 160f;

        /// <summary>Letterbox aspect (CINEMATIC_DIRECTION §1: 2.39:1 for every bar, the title card included).</summary>
        public const float LetterboxAspect = 2.39f;

        /// <summary>Marker edge inset from the screen border.</summary>
        public const float MarkerEdgeInset = 72f;
    }
}
