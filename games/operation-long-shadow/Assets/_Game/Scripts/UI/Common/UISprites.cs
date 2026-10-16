using System;
using UnityEngine;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// Sprites and textures of the UI. A theme override wins (imported art: manifest UI_HUD_ICONS, UI_SHAPES,
    /// UI_DAMAGE_VIGNETTE, ...); otherwise a procedural version is painted once with anti-aliased signed-distance shapes
    /// and cached for the session (manifest UI_SHAPES is "procedural vector sprites"). Painting happens when a view is
    /// built, never per frame. <see cref="PaintSprite"/> is also what the editor bakes when art is missing.
    /// Orientation conventions match the imported art: <see cref="UISpriteId.Arrow"/> points +X,
    /// <see cref="UISpriteId.GrenadeArrow"/> points +Y, <see cref="UISpriteId.Chevron"/> points down,
    /// <see cref="UISpriteId.DamageArc"/> is a 60 degree segment at the top of a ring of radius 104/256 of the sprite.
    /// </summary>
    public static class UISprites
    {
        private static readonly Sprite[] s_sprites = new Sprite[UISpriteIds.Count];
        private static readonly Texture2D[] s_textures = new Texture2D[UISpriteIds.TextureCount];

        /// <summary>Radius of the damage-arc ring as a fraction of the sprite size (art: 104 px of 256).</summary>
        public const float DamageArcRadius01 = 104f / 256f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Array.Clear(s_sprites, 0, s_sprites.Length);
            Array.Clear(s_textures, 0, s_textures.Length);
        }

        /// <summary>Sprite for an id: the theme override, else the cached procedural sprite (null only for optional art).</summary>
        public static Sprite Get(UISpriteId id, UITheme theme)
        {
            if (theme != null)
            {
                var s = theme.GetSprite(id);
                if (s != null) return s;
            }
            if (UISpriteIds.IsOptional(id)) return null;
            var i = (int)id;
            if (i < 0 || i >= s_sprites.Length) return null;
            if (s_sprites[i] != null) return s_sprites[i];
            s_sprites[i] = CreateProcedural(id);
            return s_sprites[i];
        }

        /// <summary>Theme sprite for decorative art without a procedural stand-in, or null.</summary>
        public static Sprite GetOptional(UISpriteId id, UITheme theme) => theme != null ? theme.GetSprite(id) : null;

        /// <summary>True when the id comes from the theme (imported art), not a procedural stand-in.</summary>
        public static bool IsImported(UISpriteId id, UITheme theme) => theme != null && theme.GetSprite(id) != null;

        /// <summary>Texture for an id: the theme override, else the cached procedural texture.</summary>
        public static Texture2D Get(UITextureId id, UITheme theme)
        {
            if (theme != null)
            {
                var t = theme.GetTexture(id);
                if (t != null) return t;
            }
            var i = (int)id;
            if (i < 0 || i >= s_textures.Length) return null;
            if (s_textures[i] != null) return s_textures[i];
            s_textures[i] = CreateProcedural(id);
            return s_textures[i];
        }

        /// <summary>Paints the procedural sprite texture of an id (also used by the editor baker). Readable, no mips.</summary>
        public static Texture2D PaintSprite(UISpriteId id)
        {
            switch (id)
            {
                case UISpriteId.Diamond:
                    return Paint(64, 64, (x, y) => Fill(DiamondDist(x, y, 26f)), "Diamond");
                case UISpriteId.DiamondHollow:
                    return Paint(64, 64, (x, y) => Fill(Mathf.Abs(DiamondDist(x, y, 24f)) - 2.6f), "DiamondHollow");
                case UISpriteId.HighPriority:
                    return Paint(64, 64, (x, y) =>
                    {
                        var outer = Mathf.Abs(DiamondDist(x, y, 26f)) - 2.8f;
                        var pip = DiamondDist(x, y, 10f);
                        return Fill(Mathf.Min(outer, pip));
                    }, "HighPriority");
                case UISpriteId.Chevron:
                    return Paint(64, 64, (x, y) =>
                    {
                        var d = Mathf.Min(Segment(x, y, -22f, 12f, 0f, -10f), Segment(x, y, 0f, -10f, 22f, 12f));
                        return Fill(d - 4f);
                    }, "Chevron");
                case UISpriteId.Arrow:
                    return Paint(64, 64, (x, y) => Fill(Triangle(x, y, new Vector2(26f, 0f), new Vector2(-18f, 20f), new Vector2(-18f, -20f))), "Arrow");
                case UISpriteId.GrenadeArrow:
                    return Paint(64, 64, (x, y) =>
                    {
                        var head = Triangle(x, y, new Vector2(0f, 28f), new Vector2(-18f, 4f), new Vector2(18f, 4f));
                        var shaft = Box(x, y + 10f, 5f, 16f, 1f);
                        return Fill(Mathf.Min(head, shaft));
                    }, "GrenadeArrow");
                case UISpriteId.Ring:
                    return Paint(256, 256, (x, y) => Fill(Mathf.Abs(Len(x, y) - 122f) - 2.2f), "Ring");
                case UISpriteId.RingThick:
                    return Paint(256, 256, (x, y) => Fill(Mathf.Abs(Len(x, y) - 116f) - 9f), "RingThick");
                case UISpriteId.Disc:
                    return Paint(64, 64, (x, y) => Fill(Len(x, y) - 28f), "Disc");
                case UISpriteId.SoftDisc:
                    return Paint(64, 64, (x, y) =>
                    {
                        var r = Mathf.Clamp01(Len(x, y) / 30f);
                        var core = Fill(Len(x, y) - 9f);
                        var halo = (1f - r) * (1f - r) * 0.55f;
                        return Mathf.Max(core, halo);
                    }, "SoftDisc");
                case UISpriteId.TargetX:
                    return Paint(64, 64, (x, y) =>
                    {
                        var d = Mathf.Min(Segment(x, y, -22f, -22f, -6f, -6f), Segment(x, y, 6f, 6f, 22f, 22f));
                        d = Mathf.Min(d, Mathf.Min(Segment(x, y, -22f, 22f, -6f, 6f), Segment(x, y, 6f, -6f, 22f, -22f)));
                        return Fill(d - 3.4f);
                    }, "TargetX");
                case UISpriteId.IconIntel:
                    return Paint(128, 128, (x, y) =>
                    {
                        var body = Mathf.Abs(Box(x, y + 6f, 48f, 34f, 4f)) - 4f;
                        var tab = Mathf.Abs(Box(x + 26f, y - 34f, 20f, 7f, 3f)) - 4f;
                        var line1 = Segment(x, y, -30f, 4f, 30f, 4f) - 2.5f;
                        var line2 = Segment(x, y, -30f, -12f, 14f, -12f) - 2.5f;
                        return Fill(Mathf.Min(Mathf.Min(body, tab), Mathf.Min(line1, line2)));
                    }, "IconIntel");
                case UISpriteId.IconFrag:
                    return Paint(128, 128, (x, y) =>
                    {
                        var body = Ellipse(x, y + 10f, 30f, 38f);
                        var neck = Box(x + 2f, y - 34f, 12f, 8f, 2f);
                        var lever = Segment(x, y, 10f, -40f, 30f, 6f) - 4.5f;
                        var ring = Mathf.Abs(Len(x + 20f, y + 44f) - 9f) - 3f;
                        var groove = Segment(x, y, -30f, 8f, 30f, 8f) - 2f;
                        var shape = Mathf.Min(Mathf.Min(body, neck), Mathf.Min(lever, ring));
                        return Fill(Mathf.Max(shape, -groove));
                    }, "IconFrag");
                case UISpriteId.IconRadio:
                    return Paint(128, 128, (x, y) =>
                    {
                        var dot = Len(x + 30f, y) - 9f;
                        var a1 = Arc(x + 30f, y, 26f, 50f) - 4f;
                        var a2 = Arc(x + 30f, y, 46f, 50f) - 4f;
                        var a3 = Arc(x + 30f, y, 66f, 50f) - 4f;
                        return Fill(Mathf.Min(Mathf.Min(dot, a1), Mathf.Min(a2, a3)));
                    }, "IconRadio");
                case UISpriteId.IconFireSemi:
                    return Paint(64, 128, (x, y) => Fill(Bullet(x, y)), "IconFireSemi");
                case UISpriteId.IconFireBurst:
                    return Paint(96, 128, (x, y) => Fill(Mathf.Min(Bullet(x + 16f, y), Bullet(x - 16f, y))), "IconFireBurst");
                case UISpriteId.IconFireAuto:
                    return Paint(128, 128, (x, y) => Fill(Mathf.Min(Bullet(x + 30f, y), Mathf.Min(Bullet(x, y), Bullet(x - 30f, y)))), "IconFireAuto");
                case UISpriteId.IconInteract:
                    return Paint(96, 96, (x, y) => Fill(Mathf.Abs(Box(x, y, 40f, 40f, 9f)) - 3.2f), "IconInteract");
                case UISpriteId.IconCrate:
                    return Paint(128, 128, (x, y) =>
                    {
                        var box = Mathf.Abs(Box(x, y, 52f, 36f, 2f)) - 4f;
                        var lid = Segment(x, y, -52f, 18f, 52f, 18f) - 3f;
                        var x1 = Segment(x, y, -44f, -30f, 44f, 12f) - 3f;
                        return Fill(Mathf.Min(box, Mathf.Min(lid, x1)));
                    }, "IconCrate");
                case UISpriteId.IconBinoculars:
                    return Paint(128, 128, (x, y) =>
                    {
                        var l = Box(x + 26f, y + 8f, 18f, 34f, 9f);
                        var r = Box(x - 26f, y + 8f, 18f, 34f, 9f);
                        var bridge = Box(x, y + 18f, 10f, 8f, 2f);
                        return Fill(Mathf.Min(Mathf.Min(l, r), bridge));
                    }, "IconBinoculars");
                case UISpriteId.Square:
                    return Paint(8, 8, (x, y) => 1f, "Square");
                case UISpriteId.Line:
                    return Paint(8, 16, (x, y) => Fill(Box(x, y, 3.5f, 7.5f, 2f)), "Line");
                case UISpriteId.Corner:
                    return Paint(32, 32, (x, y) =>
                    {
                        var d = Mathf.Min(Segment(x, y, -14f, 14f, 14f, 14f), Segment(x, y, -14f, 14f, -14f, -14f));
                        return Fill(d - 1.6f);
                    }, "Corner");
                case UISpriteId.Outline:
                    return Paint(32, 32, (x, y) => Fill(Mathf.Abs(Box(x, y, 13f, 13f, 2f)) - 1.8f), "Outline");
                case UISpriteId.HitMarker:
                    return Paint(64, 64, (x, y) =>
                    {
                        var d = Mathf.Min(Segment(x, y, 8f, 8f, 22f, 22f), Segment(x, y, -8f, 8f, -22f, 22f));
                        d = Mathf.Min(d, Mathf.Min(Segment(x, y, 8f, -8f, 22f, -22f), Segment(x, y, -8f, -8f, -22f, -22f)));
                        return Fill(d - 2.2f);
                    }, "HitMarker");
                case UISpriteId.DamageArc:
                    return Paint(128, 128, (x, y) =>
                    {
                        // 60 degree segment at the top of a ring of radius 52 px (= 104/256), thick in the middle.
                        var ang = Mathf.Abs(Mathf.Atan2(x, y) * Mathf.Rad2Deg);
                        if (ang > 30f) return 0f;
                        var taper = 1f - ang / 30f;
                        var thickness = 2.5f + 6f * taper;
                        return Fill(Mathf.Abs(Len(x, y) - 52f) - thickness) * Mathf.SmoothStep(0f, 1f, taper * 3f);
                    }, "DamageArc");
                case UISpriteId.SoftPanel:
                    return Paint(64, 64, (x, y) =>
                    {
                        var d = Box(x, y, 31f, 31f, 22f);
                        return Mathf.Clamp01(0.5f - d / 10f);
                    }, "SoftPanel");
                case UISpriteId.CrtPanel:
                    return PaintTex(64, 64, (u, v) =>
                    {
                        var px = (u - 0.5f) * 64f;
                        var py = (v - 0.5f) * 64f;
                        var d = Box(px, py, 31f, 31f, 8f);
                        var inside = Mathf.Clamp01(0.5f - d);
                        var rim = Mathf.Clamp01(1f - Mathf.Abs(d + 2.5f) / 1.5f) * 0.55f;
                        var glass = new Color(0.035f, 0.055f, 0.042f, 0.96f * inside);
                        return Color.Lerp(glass, new Color(0.46f, 0.82f, 0.48f, inside), rim);
                    }, "UI_CrtPanel", TextureWrapMode.Clamp);
                case UISpriteId.LetterboxBar:
                    return PaintTex(8, 64, (u, v) =>
                    {
                        // Black with a 4 px soft inner edge at the bottom (y < 6 px).
                        var py = v * 64f;
                        var a = Mathf.Clamp01(py / 6f);
                        return new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, a));
                    }, "UI_LetterboxBar", TextureWrapMode.Clamp);
                case UISpriteId.PolaroidFrame:
                    return PaintPolaroid();
                case UISpriteId.BinocularMask:
                    return PaintTex(512, 288, (u, v) =>
                    {
                        var x = (u - 0.5f) * (16f / 9f); // aspect-correct units of height
                        var y = v - 0.5f;
                        var l = Mathf.Sqrt((x + 0.25f) * (x + 0.25f) + y * y);
                        var r = Mathf.Sqrt((x - 0.25f) * (x - 0.25f) + y * y);
                        var d = Mathf.Min(l, r);
                        var a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, 0.47f, d));
                        return new Color(0f, 0f, 0f, a);
                    }, "UI_BinocularMask", TextureWrapMode.Clamp);
                case UISpriteId.BinocularReticle:
                    return Paint(256, 256, (x, y) =>
                    {
                        var h = Segment(x, y, -110f, 0f, 110f, 0f) - 0.9f;
                        var v = Segment(x, y, 0f, -90f, 0f, 30f) - 0.9f;
                        var d = Mathf.Min(h, v);
                        for (var i = -10; i <= 10; i++)
                        {
                            if (i == 0) continue;
                            var len = i % 5 == 0 ? 9f : 4.5f;
                            d = Mathf.Min(d, Segment(x, y, i * 10f, 0f, i * 10f, len) - 0.8f);
                        }
                        for (var j = 1; j <= 4; j++)
                            d = Mathf.Min(d, Segment(x, y, -6f * j, -18f * j, 6f * j, -18f * j) - 0.8f);
                        return Fill(d);
                    }, "BinocularReticle");
                case UISpriteId.DoorGunRing:
                    return Paint(256, 256, (x, y) =>
                    {
                        var len = Len(x, y);
                        var d = Mathf.Abs(len - 120f) - 1.6f;
                        d = Mathf.Min(d, Mathf.Abs(len - 80f) - 1.2f);
                        d = Mathf.Min(d, Mathf.Abs(len - 40f) - 1.2f);
                        d = Mathf.Min(d, Segment(x, y, -120f, 0f, -10f, 0f) - 1.2f);
                        d = Mathf.Min(d, Segment(x, y, 10f, 0f, 120f, 0f) - 1.2f);
                        d = Mathf.Min(d, Segment(x, y, 0f, -120f, 0f, -10f) - 1.2f);
                        d = Mathf.Min(d, Segment(x, y, 0f, 10f, 0f, 120f) - 1.2f);
                        return Fill(d);
                    }, "DoorGunRing");
                case UISpriteId.HeatBarFrame:
                    return Paint(64, 32, (x, y) => Fill(Mathf.Abs(Box(x, y, 31f, 15f, 4f)) - 1.6f), "HeatBarFrame");
                case UISpriteId.CrtCursor:
                    return PaintTex(32, 48, (u, v) =>
                    {
                        var px = (u - 0.5f) * 32f;
                        var py = (v - 0.5f) * 48f;
                        var core = Mathf.Clamp01(0.5f - Box(px, py, 9f, 16f, 1f));
                        var bloom = Mathf.Clamp01(1f - Mathf.Max(0f, Box(px, py, 9f, 16f, 1f)) / 6f) * 0.45f;
                        var a = Mathf.Max(core, bloom);
                        return new Color(0.55f, 1f, 0.6f, a);
                    }, "UI_CrtCursor", TextureWrapMode.Clamp);
                case UISpriteId.ClickGateBG:
                    return PaintTex(256, 144, (u, v) =>
                    {
                        var dx = (u - 0.5f) * 1.6f;
                        var dy = (v - 0.5f) * 1.1f;
                        var r = dx * dx + dy * dy;
                        var glow = Mathf.Clamp01(0.08f - r * 0.12f);
                        return new Color(0.01f + glow * 0.3f, 0.02f + glow, 0.012f + glow * 0.4f, 1f);
                    }, "UI_ClickGateBG", TextureWrapMode.Clamp);
                case UISpriteId.DamageVignette:
                    return PaintTex(512, 256, (u, v) =>
                    {
                        // Edge-weighted dust and dried-blood smears (stand-in for UI_DAMAGE_VIGNETTE); clear centre.
                        var edge = Mathf.Min(Mathf.Min(u, 1f - u) * 2f, Mathf.Min(v, 1f - v) * 2f);
                        var mask = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.42f, edge));
                        var n = Fbm(u * 9f, v * 4.5f);
                        var blood = Mathf.Clamp01((n - 0.42f) * 2.4f) * mask;
                        var dust = Mathf.Clamp01(mask * 0.55f + (Fbm(u * 31f, v * 15f) - 0.5f) * 0.4f);
                        var c = Color.Lerp(new Color(0.22f, 0.17f, 0.12f, dust * 0.65f), new Color(0.34f, 0.03f, 0.02f, 0.92f), blood);
                        c.a = Mathf.Max(c.a * mask, blood * 0.9f);
                        return c;
                    }, "UI_DamageVignette", TextureWrapMode.Clamp);
                default:
                    return Paint(8, 8, (x, y) => 1f, id.ToString());
            }
        }

        /// <summary>Paints the procedural version of a tiling texture (also used by the editor baker).</summary>
        public static Texture2D PaintTexture(UITextureId id)
        {
            switch (id)
            {
                case UITextureId.Scanlines:
                {
                    // 4x4 tile: 2 px clear, 2 px dark (as T_UI_Scanlines), point filtered.
                    var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "UI_Scanlines", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
                    var px = new Color32[16];
                    byte[] rows = { 0, 0, 120, 120 };
                    for (var yy = 0; yy < 4; yy++)
                        for (var xx = 0; xx < 4; xx++)
                            px[yy * 4 + xx] = new Color32(0, 0, 0, rows[yy]);
                    t.SetPixels32(px);
                    t.Apply(false, false);
                    return t;
                }
                case UITextureId.Vignette:
                    return PaintTex(128, 128, (u, v) =>
                    {
                        var dx = (u - 0.5f) * 2f;
                        var dy = (v - 0.5f) * 2f;
                        var r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                        var a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, r));
                        return new Color(0f, 0f, 0f, a * 0.92f);
                    }, "UI_Vignette", TextureWrapMode.Clamp);
                default:
                {
                    var t = new Texture2D(128, 128, TextureFormat.RGBA32, false) { name = "UI_Noise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
                    var px = new Color32[128 * 128];
                    var seed = 0x9E3779B9u;
                    for (var i = 0; i < px.Length; i++)
                    {
                        var g = (byte)(NumberGroups.Next(ref seed) & 0xFF);
                        px[i] = new Color32(g, g, g, 255);
                    }
                    t.SetPixels32(px);
                    t.Apply(false, false);
                    return t;
                }
            }
        }

        private static Texture2D PaintPolaroid()
        {
            // Same proportions as T_UI_PolaroidFrame (560 x 670 with a 24 px shadow), painted at half size.
            const int w = 280, h = 335;
            const float s = 0.5f;
            var win = PolaroidLayout.PhotoWindowPx;
            return PaintTex(w, h, (u, v) =>
            {
                var x = u * w / s;
                var yTop = (1f - v) * h / s;
                var inPaper = x > 12f && x < 548f && yTop > 12f && yTop < 646f;
                if (!inPaper)
                {
                    var dx = Mathf.Max(0f, Mathf.Max(12f - x, x - 548f));
                    var dy = Mathf.Max(0f, Mathf.Max(12f - yTop, yTop - 646f));
                    var sh = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / 12f);
                    return new Color(0f, 0f, 0f, sh * sh * 0.35f);
                }
                if (x >= win.xMin && x <= win.xMax && yTop >= win.yMin && yTop <= win.yMax) return new Color(0f, 0f, 0f, 0f);
                var fib = (Mathf.PerlinNoise(u * 23f, v * 61f) - 0.5f) * 0.05f;
                return new Color(0.92f + fib, 0.90f + fib, 0.84f + fib, 1f);
            }, "UI_PolaroidFrame", TextureWrapMode.Clamp);
        }

        private static Sprite CreateProcedural(UISpriteId id)
        {
            var tex = PaintSprite(id);
            tex.hideFlags = HideFlags.DontSave;
            var border = UIAssetPaths.BorderOf(id, true);
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
            s.name = "UI_" + id + " (procedural)";
            s.hideFlags = HideFlags.DontSave;
            return s;
        }

        private static Texture2D CreateProcedural(UITextureId id)
        {
            var t = PaintTexture(id);
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        /// <summary>
        /// Paints a white sprite texture (tinted by the Image colour): <paramref name="alpha"/> gets pixel-centre
        /// coordinates relative to the texture centre (y up) and returns coverage 0..1.
        /// </summary>
        public static Texture2D Paint(int w, int h, Func<float, float, float> alpha, string name)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "UI_" + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (var yy = 0; yy < h; yy++)
            {
                var y = yy + 0.5f - h * 0.5f;
                for (var xx = 0; xx < w; xx++)
                {
                    var x = xx + 0.5f - w * 0.5f;
                    var a = Mathf.Clamp01(alpha(x, y));
                    px[yy * w + xx] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            t.SetPixels32(px);
            t.Apply(false, false);
            return t;
        }

        private static Texture2D PaintTex(int w, int h, Func<float, float, Color> shader, string name, TextureWrapMode wrap)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = wrap, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (var yy = 0; yy < h; yy++)
                for (var xx = 0; xx < w; xx++)
                    px[yy * w + xx] = shader((xx + 0.5f) / w, (yy + 0.5f) / h);
            t.SetPixels32(px);
            t.Apply(false, false);
            return t;
        }

        // ---- signed distance helpers (pixels; negative inside)

        /// <summary>Coverage from a signed distance with a 1 px anti-aliased edge.</summary>
        private static float Fill(float d) => Mathf.Clamp01(0.5f - d);

        private static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

        private static float DiamondDist(float x, float y, float r) => (Mathf.Abs(x) + Mathf.Abs(y) - r) * 0.70710678f;

        private static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            var pax = px - ax;
            var pay = py - ay;
            var bax = bx - ax;
            var bay = by - ay;
            var h = Mathf.Clamp01((pax * bax + pay * bay) / Mathf.Max(1e-5f, bax * bax + bay * bay));
            var dx = pax - bax * h;
            var dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float Box(float x, float y, float hx, float hy, float r)
        {
            var qx = Mathf.Abs(x) - hx + r;
            var qy = Mathf.Abs(y) - hy + r;
            var ox = Mathf.Max(qx, 0f);
            var oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static float Ellipse(float x, float y, float rx, float ry)
        {
            var k = Len(x / rx, y / ry);
            return (k - 1f) * Mathf.Min(rx, ry);
        }

        private static float Triangle(float x, float y, Vector2 a, Vector2 b, Vector2 c)
        {
            var d = Mathf.Min(Segment(x, y, a.x, a.y, b.x, b.y), Mathf.Min(Segment(x, y, b.x, b.y, c.x, c.y), Segment(x, y, c.x, c.y, a.x, a.y)));
            var p = new Vector2(x, y);
            var s1 = Cross(b - a, p - a);
            var s2 = Cross(c - b, p - b);
            var s3 = Cross(a - c, p - c);
            var inside = (s1 >= 0f && s2 >= 0f && s3 >= 0f) || (s1 <= 0f && s2 <= 0f && s3 <= 0f);
            return inside ? -d : d;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>Distance to a circular arc of radius r facing +x, spanning ±halfDeg.</summary>
        private static float Arc(float x, float y, float r, float halfDeg)
        {
            var ang = Mathf.Abs(Mathf.Atan2(y, x) * Mathf.Rad2Deg);
            if (ang <= halfDeg) return Mathf.Abs(Len(x, y) - r);
            var e = halfDeg * Mathf.Deg2Rad;
            var ex = Mathf.Cos(e) * r;
            var ey = Mathf.Sin(e) * r * Mathf.Sign(y);
            return Len(x - ex, y - ey);
        }

        private static float Bullet(float x, float y)
        {
            // Case (y -48..24) + ogive tip (to y 48), 20 px wide, y up.
            return Mathf.Min(Box(x, y + 12f, 10f, 36f, 1.5f), Ellipse(x, y - 24f, 10f, 24f));
        }

        private static float Fbm(float x, float y)
        {
            var v = 0f;
            var a = 0.5f;
            for (var i = 0; i < 4; i++)
            {
                v += a * Mathf.PerlinNoise(x, y);
                x *= 2.03f;
                y *= 1.97f;
                a *= 0.5f;
            }
            return v;
        }
    }
}
