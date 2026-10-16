namespace LongShadow.UI.Common
{
    /// <summary>
    /// Every sprite the UI draws. Each one has a procedural version (<see cref="UISprites"/>) so the HUD works before any
    /// art lands; <see cref="UITheme"/> overrides any of them with an imported sprite (manifest UI_HUD_ICONS, UI_SHAPES,
    /// UI_POLAROID_FRAME, UI_BINOCULAR_RETICLE, UI_DOORGUN_RING, UI_CLICK_GATE, UI_LETTERBOX, UI_PAUSE_PANEL,
    /// UI_DAMAGE_VIGNETTE, IMG_DEBRIEF_DOSSIER). The file each id maps to is in <see cref="UIAssetPaths"/>.
    /// Values are stable (theme arrays are indexed by them); append new ids at the end and bump <see cref="UISpriteIds.Count"/>.
    /// </summary>
    public enum UISpriteId
    {
        /// <summary>Filled objective diamond (MarkerKind.Diamond).</summary>
        Diamond = 0,
        /// <summary>Hollow objective diamond (MarkerKind.DiamondHollow, off-screen / ADS state).</summary>
        DiamondHollow = 1,
        /// <summary>FOLLOW chevron, points down (MarkerKind.Follow).</summary>
        Chevron = 2,
        /// <summary>Edge-clamp arrow, points +X (right); rotate it to the marker direction.</summary>
        Arrow = 3,
        /// <summary>Thin ring (world rings, compass centre).</summary>
        Ring = 4,
        /// <summary>Thick ring for radial fills (hold prompt, drill progress, cook timer).</summary>
        RingThick = 5,
        /// <summary>Filled hard disc.</summary>
        Disc = 6,
        /// <summary>Soft dot (squad dots on the compass).</summary>
        SoftDisc = 7,
        /// <summary>Target cross (MarkerKind.TargetX).</summary>
        TargetX = 8,
        /// <summary>Intel folder icon.</summary>
        IconIntel = 9,
        /// <summary>Frag grenade icon.</summary>
        IconFrag = 10,
        /// <summary>Radio waves (radio subtitles).</summary>
        IconRadio = 11,
        /// <summary>Fire mode semi (one round).</summary>
        IconFireSemi = 12,
        /// <summary>Fire mode auto (three rounds).</summary>
        IconFireAuto = 13,
        /// <summary>Fire mode burst (two rounds).</summary>
        IconFireBurst = 14,
        /// <summary>Interact hand / key cap.</summary>
        IconInteract = 15,
        /// <summary>Ammo crate.</summary>
        IconCrate = 16,
        /// <summary>White square (bars, blocks, fills).</summary>
        Square = 17,
        /// <summary>L-shaped panel corner bracket (top-left orientation).</summary>
        Corner = 18,
        /// <summary>SX-70 polaroid frame with a transparent photo window (layout in <see cref="PolaroidLayout"/>).</summary>
        PolaroidFrame = 19,
        /// <summary>High-priority marker (diamond with an inner pip).</summary>
        HighPriority = 20,
        /// <summary>Hollow rectangle outline (keycaps, stamp border), 9-sliced.</summary>
        Outline = 21,
        /// <summary>Binoculars icon.</summary>
        IconBinoculars = 22,
        /// <summary>Rounded bar, 9-sliced: crosshair lines, compass ticks, hit-marker ticks, heat fill.</summary>
        Line = 23,
        /// <summary>Hit marker (4 diagonal ticks).</summary>
        HitMarker = 24,
        /// <summary>Damage arc: a 60 degree segment at the top of a ring; rotate about the centre.</summary>
        DamageArc = 25,
        /// <summary>Grenade indicator arrow, points up (+Y).</summary>
        GrenadeArrow = 26,
        /// <summary>Soft rounded panel, 9-sliced (subtitle and prompt plates).</summary>
        SoftPanel = 27,
        /// <summary>Dark CRT glass panel, 9-sliced (pause, options, debrief).</summary>
        CrtPanel = 28,
        /// <summary>Letterbox bar with a soft inner edge at the bottom (flip Y for the bottom bar), 9-sliced.</summary>
        LetterboxBar = 29,
        /// <summary>Binocular double-eyepiece mask (16:9, black outside the two circles).</summary>
        BinocularMask = 30,
        /// <summary>Binocular mil reticle.</summary>
        BinocularReticle = 31,
        /// <summary>Door-gun ring sight.</summary>
        DoorGunRing = 32,
        /// <summary>Door-gun heat bar frame, 9-sliced.</summary>
        HeatBarFrame = 33,
        /// <summary>Green block cursor with phosphor bloom (click gate, debrief).</summary>
        CrtCursor = 34,
        /// <summary>Click-gate CRT glass (full screen).</summary>
        ClickGateBG = 35,
        /// <summary>Screen-edge dust-and-blood overlay (UI_DAMAGE_VIGNETTE).</summary>
        DamageVignette = 36,
        /// <summary>Debrief stamp "PROGRAM LONG SHADOW / ASSETS ACTIVE: 13" (IMG_DEBRIEF_DOSSIER, optional art).</summary>
        Stamp = 37,
        /// <summary>Felt-marker redaction bar (IMG_DEBRIEF_DOSSIER, optional art).</summary>
        RedactionBar = 38,
        /// <summary>Hand-drawn strike-through stroke (IMG_DEBRIEF_DOSSIER, optional art).</summary>
        StrikeThrough = 39,
        /// <summary>Dossier paper sheet (IMG_DEBRIEF_DOSSIER, optional art; the CRT debrief does not need it).</summary>
        DossierBackground = 40,
    }
}
