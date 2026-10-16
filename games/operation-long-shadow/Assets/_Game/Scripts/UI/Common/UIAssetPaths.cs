using UnityEngine;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// Where the imported UI art lands (Docs/ASSET_MANIFEST.json target paths + TECH_PLAN §4.2 names, as produced by
    /// WP09_ART into RawAssets/wp08/ready and RawAssets/wp09/ready). The theme builder (LongShadow/UI/Build Theme) reads
    /// these paths; runtime code never does (it reads the <see cref="UITheme"/> asset). A missing file keeps the
    /// procedural sprite, with one warning from the builder.
    /// </summary>
    public static class UIAssetPaths
    {
        /// <summary>Folder of the UI sprites (manifest UI_*).</summary>
        public const string Sprites = "Assets/_Game/UI/Sprites/";
        /// <summary>Folder of the TTF/OTF fonts (manifest UI_FONT_*).</summary>
        public const string Fonts = "Assets/_Game/Art/Fonts/";
        /// <summary>Folder the builder writes TMP SDF font assets and their materials into.</summary>
        public const string FontAssets = "Assets/_Game/Art/Fonts/SDF/";
        /// <summary>Folder of the debrief dossier art (IMG_DEBRIEF_DOSSIER, WP09_ART).</summary>
        public const string Dossier = "Assets/_Game/Art/Textures/Images/debrief/";
        /// <summary>The theme asset (WP11 owns UI/Themes; the WP08 builder writes it on request).</summary>
        public const string Theme = "Assets/_Game/UI/Themes/UI_Theme.asset";

        /// <summary>Manifest id and asset path of the imported sprite for an id, or null when the id is procedural only.</summary>
        public static string SpritePath(UISpriteId id, out string manifestId)
        {
            switch (id)
            {
                case UISpriteId.Diamond: manifestId = "UI_SHAPES"; return Sprites + "shapes/T_UI_DiamondFill.png";
                case UISpriteId.DiamondHollow: manifestId = "UI_SHAPES"; return Sprites + "shapes/T_UI_DiamondOutline.png";
                case UISpriteId.Chevron: manifestId = "UI_SHAPES"; return Sprites + "shapes/T_UI_Chevron.png";
                case UISpriteId.Arrow: manifestId = "UI_SHAPES"; return Sprites + "shapes/T_UI_EdgeArrow.png";
                case UISpriteId.Ring: manifestId = "UI_SHAPES"; return Sprites + "shapes/T_UI_Ring.png";
                case UISpriteId.RingThick: manifestId = "UI_SHAPES"; return Sprites + "shapes/T_UI_RingFill.png";
                case UISpriteId.SoftDisc: manifestId = "UI_SHAPES"; return Sprites + "shapes/T_UI_SoftDot.png";
                case UISpriteId.Line: manifestId = "UI_SHAPES"; return Sprites + "shapes/T_UI_Line.png";
                case UISpriteId.HitMarker: manifestId = "UI_SHAPES"; return Sprites + "shapes/T_UI_HitMarker.png";
                case UISpriteId.DamageArc: manifestId = "UI_SHAPES"; return Sprites + "shapes/T_UI_DamageArc.png";
                case UISpriteId.GrenadeArrow: manifestId = "UI_SHAPES"; return Sprites + "shapes/T_UI_GrenadeArrow.png";
                case UISpriteId.SoftPanel: manifestId = "UI_SHAPES"; return Sprites + "shapes/T_UI_SoftPanel.png";
                case UISpriteId.IconIntel: manifestId = "UI_HUD_ICONS"; return Sprites + "icons/T_UI_Icon_Intel.png";
                case UISpriteId.IconFrag: manifestId = "UI_HUD_ICONS"; return Sprites + "icons/T_UI_Icon_Frag.png";
                case UISpriteId.IconRadio: manifestId = "UI_HUD_ICONS"; return Sprites + "icons/T_UI_Icon_Radio.png";
                case UISpriteId.IconFireSemi: manifestId = "UI_HUD_ICONS"; return Sprites + "icons/T_UI_Icon_FireSemi.png";
                case UISpriteId.IconFireAuto: manifestId = "UI_HUD_ICONS"; return Sprites + "icons/T_UI_Icon_FireAuto.png";
                case UISpriteId.IconInteract: manifestId = "UI_HUD_ICONS"; return Sprites + "icons/T_UI_Icon_Interact.png";
                case UISpriteId.IconCrate: manifestId = "UI_HUD_ICONS"; return Sprites + "icons/T_UI_Icon_Crate.png";
                case UISpriteId.IconBinoculars: manifestId = "UI_HUD_ICONS"; return Sprites + "icons/T_UI_Icon_Binoculars.png";
                case UISpriteId.PolaroidFrame: manifestId = "UI_POLAROID_FRAME"; return Sprites + "polaroid_frame/T_UI_PolaroidFrame.png";
                case UISpriteId.BinocularMask: manifestId = "UI_BINOCULAR_RETICLE"; return Sprites + "binocular_reticle/T_UI_BinocularMask.png";
                case UISpriteId.BinocularReticle: manifestId = "UI_BINOCULAR_RETICLE"; return Sprites + "binocular_reticle/T_UI_BinocularReticle.png";
                case UISpriteId.DoorGunRing: manifestId = "UI_DOORGUN_RING"; return Sprites + "doorgun_ring/T_UI_DoorGunRing.png";
                case UISpriteId.HeatBarFrame: manifestId = "UI_DOORGUN_RING"; return Sprites + "doorgun_ring/T_UI_HeatBarFrame.png";
                case UISpriteId.CrtCursor: manifestId = "UI_CLICK_GATE"; return Sprites + "click_gate/T_UI_CrtCursor.png";
                case UISpriteId.ClickGateBG: manifestId = "UI_CLICK_GATE"; return Sprites + "click_gate/T_UI_ClickGateBG.png";
                case UISpriteId.LetterboxBar: manifestId = "UI_LETTERBOX"; return Sprites + "letterbox/T_UI_LetterboxBar.png";
                case UISpriteId.CrtPanel: manifestId = "UI_PAUSE_PANEL"; return Sprites + "pause_panel/T_UI_CrtPanel.png";
                case UISpriteId.DamageVignette: manifestId = "UI_DAMAGE_VIGNETTE"; return Sprites + "vignette/T_UI_DamageVignette.png";
                case UISpriteId.Stamp: manifestId = "IMG_DEBRIEF_DOSSIER"; return Dossier + "T_StampAssetsActive_UI_BC.png";
                case UISpriteId.RedactionBar: manifestId = "IMG_DEBRIEF_DOSSIER"; return Dossier + "T_RedactionBar1_UI_BC.png";
                case UISpriteId.StrikeThrough: manifestId = "IMG_DEBRIEF_DOSSIER"; return Dossier + "T_StrikeThrough_UI_BC.png";
                case UISpriteId.DossierBackground: manifestId = "IMG_DEBRIEF_DOSSIER"; return Dossier + "T_DebriefBackground_FS_UI_BC.png";
                default: manifestId = "UI_SHAPES"; return null; // procedural only
            }
        }

        /// <summary>
        /// Manifest id and asset path of an imported tiling texture, or null. None today: T_UI_Scanlines imports as a
        /// clamped sprite (TextureRules "UI/Sprite" forces Clamp), and RawImage UV tiling needs Repeat, so the CRT
        /// overlays use the identical procedural 4 × 4 tile (DECISIONS.md [WP08]).
        /// </summary>
        public static string TexturePath(UITextureId id, out string manifestId)
        {
            manifestId = id == UITextureId.Scanlines ? "UI_PAUSE_PANEL" : null;
            return null;
        }

        /// <summary>
        /// 9-slice border (left, bottom, right, top in source pixels, Unity's Vector4 order) that the import rows ask for;
        /// zero when the sprite is not sliced. Used by the theme builder and by the procedural sprites.
        /// </summary>
        public static Vector4 BorderOf(UISpriteId id, bool procedural)
        {
            switch (id)
            {
                case UISpriteId.Line: return procedural ? new Vector4(3, 3, 3, 3) : new Vector4(4, 6, 4, 6);
                case UISpriteId.SoftPanel: return new Vector4(24, 24, 24, 24);
                case UISpriteId.CrtPanel: return procedural ? new Vector4(20, 20, 20, 20) : new Vector4(32, 32, 32, 32);
                case UISpriteId.HeatBarFrame: return new Vector4(10, 10, 10, 10);
                case UISpriteId.LetterboxBar: return procedural ? new Vector4(2, 6, 2, 2) : new Vector4(2, 6, 2, 2);
                case UISpriteId.Outline: return new Vector4(10, 10, 10, 10);
                default: return Vector4.zero;
            }
        }

        /// <summary>Font file (under <see cref="Fonts"/>) that supplies a role, or null (Fallback = TMP default font).</summary>
        public static string FontFile(FontRole role)
        {
            switch (role)
            {
                case FontRole.Typewriter: return "SpecialElite-Regular.ttf";
                case FontRole.TypewriterClean: return "CourierPrime-Regular.ttf";
                case FontRole.Condensed: return "BarlowCondensed-SemiBold.ttf";
                case FontRole.CondensedMedium: return "BarlowCondensed-Medium.ttf";
                case FontRole.Mono: return "ShareTechMono-Regular.ttf";
                case FontRole.Sardani: return "Sardani-Regular.ttf";
                default: return null;
            }
        }
    }
}
