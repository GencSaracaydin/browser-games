using UnityEngine;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// The 1984 palette of the HUD and menus: amber and green phosphor, typewriter ink and dossier paper. Marker colours
    /// are the GDD §11 values; everything else is the WP08 art direction (DECISIONS.md [WP08]).
    /// </summary>
    public static class UIColors
    {
        /// <summary>Objective markers before sunset (GDD §11 #E3A33B).</summary>
        public static readonly Color MarkerAmber = Hex(0xE3A33B);
        /// <summary>Objective markers after sunset, s &gt;= 2.0 (GDD §11 #A6E3B4).</summary>
        public static readonly Color MarkerGreen = Hex(0xA6E3B4);
        /// <summary>High-priority markers and kill hit markers (GDD §11 #E04B3B).</summary>
        public static readonly Color Red = Hex(0xE04B3B);

        /// <summary>P1 green phosphor (click gate, debrief CRT, Clockmaker #7FE07F).</summary>
        public static readonly Color Phosphor = Hex(0x7FE07F);
        /// <summary>Dim phosphor for unfilled number groups and secondary CRT text.</summary>
        public static readonly Color PhosphorDim = Hex(0x2E5E36);
        /// <summary>P3 amber phosphor (title card type, pause menu highlights).</summary>
        public static readonly Color AmberPhosphor = Hex(0xFFB547);
        /// <summary>Dim amber for inactive menu entries.</summary>
        public static readonly Color AmberDim = Hex(0x8A5A22);

        /// <summary>Warm HUD off-white (ammo numerals, crosshair).</summary>
        public static readonly Color Ink = Hex(0xF2E9D8);
        /// <summary>Secondary HUD text.</summary>
        public static readonly Color InkDim = new Color(0.95f, 0.91f, 0.85f, 0.62f);
        /// <summary>Typewriter ink on paper (polaroid caption).</summary>
        public static readonly Color TypeInk = Hex(0x1E1B17);
        /// <summary>Polaroid / dossier paper.</summary>
        public static readonly Color Paper = Hex(0xE9E2D0);
        /// <summary>Rubber-stamp red (debrief stamp, fail card).</summary>
        public static readonly Color StampRed = Hex(0xC23A2C);
        /// <summary>Squad nameplates and compass squad dots (GDD §11 "green callsign").</summary>
        public static readonly Color SquadGreen = Hex(0x8ED69B);

        /// <summary>Bright phosphor for the selected menu item and the cursor.</summary>
        public static readonly Color PhosphorBright = Hex(0xB8FFB0);
        /// <summary>CRT glass black (click gate, debrief background).</summary>
        public static readonly Color CrtBlack = new Color(0.012f, 0.02f, 0.015f, 1f);
        /// <summary>Squad nameplate secondary line.</summary>
        public static readonly Color SquadGreenDim = new Color(0.56f, 0.84f, 0.61f, 0.7f);
        /// <summary>Key labels in prompts ("[E]").</summary>
        public static readonly Color KeyAmber = Hex(0xF2B24A);
        /// <summary>Low-ammo flash and warnings.</summary>
        public static readonly Color Warning = Hex(0xF06A3C);

        /// <summary>Dark CRT panel background.</summary>
        public static readonly Color Panel = new Color(0.028f, 0.042f, 0.034f, 0.94f);
        /// <summary>Panel border / bracket colour.</summary>
        public static readonly Color PanelEdge = new Color(0.50f, 0.88f, 0.50f, 0.55f);
        /// <summary>Subtitle backing plate.</summary>
        public static readonly Color SubtitlePlate = new Color(0f, 0f, 0f, 0.46f);
        /// <summary>Drop shadow behind thin HUD lines.</summary>
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.55f);
        /// <summary>Damage arcs.</summary>
        public static readonly Color DamageArc = new Color(0.86f, 0.16f, 0.10f, 0.9f);
        /// <summary>Door-gun heat bar cold end (white, GDD §11).</summary>
        public static readonly Color HeatCold = Hex(0xF4F1EA);

        /// <summary>Marker palette for a LightStage value when no <c>ILightStage.HudColor</c> is available (GDD §11: s &gt;= 2.0).</summary>
        public static Color MarkerPalette(float s) => s >= 2f ? MarkerGreen : MarkerAmber;

        /// <summary>Colour from 0xRRGGBB.</summary>
        public static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

        /// <summary>Same colour with another alpha.</summary>
        public static Color WithAlpha(Color c, float alpha)
        {
            c.a = alpha;
            return c;
        }
    }
}
