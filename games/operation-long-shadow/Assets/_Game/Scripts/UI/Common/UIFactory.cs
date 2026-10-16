using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// Code construction of uGUI objects with the project's defaults (TECH_PLAN §7.13: the UI is built from code). HUD
    /// graphics never block raycasts (the pointer is locked in play; only menus receive clicks), TMP texts get the
    /// role's font and its shadow/glow material, and every rect is positioned in reference pixels (1920 × 1080).
    /// Build-time only; never call from a per-frame path.
    /// </summary>
    public static class UIFactory
    {
        /// <summary>Anchor/pivot at the centre.</summary>
        public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        /// <summary>Anchor/pivot top-left.</summary>
        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        /// <summary>Anchor/pivot top-right.</summary>
        public static readonly Vector2 TopRight = new Vector2(1f, 1f);
        /// <summary>Anchor/pivot top-centre.</summary>
        public static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        /// <summary>Anchor/pivot bottom-left.</summary>
        public static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        /// <summary>Anchor/pivot bottom-right.</summary>
        public static readonly Vector2 BottomRight = new Vector2(1f, 0f);
        /// <summary>Anchor/pivot bottom-centre.</summary>
        public static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        /// <summary>Anchor/pivot middle-left.</summary>
        public static readonly Vector2 MiddleLeft = new Vector2(0f, 0.5f);
        /// <summary>Anchor/pivot middle-right.</summary>
        public static readonly Vector2 MiddleRight = new Vector2(1f, 0.5f);

        /// <summary>Creates a RectTransform child anchored at a single point.</summary>
        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Creates a RectTransform child that stretches over its parent (with optional insets).</summary>
        public static RectTransform Stretch(string name, Transform parent, float inset = 0f)
        {
            var rt = Rect(name, parent, Center, Center, Vector2.zero, Vector2.zero);
            SetStretch(rt, inset);
            return rt;
        }

        /// <summary>Makes a rect stretch over its parent with uniform insets.</summary>
        public static void SetStretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = Center;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Makes a rect span normalised anchors with pixel offsets.</summary>
        public static void SetAnchors(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        /// <summary>Creates an Image (no raycast). Sprites with a border are drawn sliced.</summary>
        public static Image Image(string name, Transform parent, Sprite sprite, Color color, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rt = Rect(name, parent, anchor, pivot, position, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            img.maskable = false;
            if (sprite != null && sprite.border.sqrMagnitude > 0f)
            {
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.fillCenter = true;
            }
            return img;
        }

        /// <summary>Creates an Image stretched over its parent.</summary>
        public static Image StretchImage(string name, Transform parent, Sprite sprite, Color color, float inset = 0f)
        {
            var img = Image(name, parent, sprite, color, Center, Center, Vector2.zero, Vector2.zero);
            SetStretch(img.rectTransform, inset);
            return img;
        }

        /// <summary>Makes an image a radial fill (hold prompt, drill ring, cook timer).</summary>
        public static void MakeRadial(Image img, float amount = 0f, bool clockwise = true)
        {
            img.type = UnityEngine.UI.Image.Type.Filled;
            img.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            img.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            img.fillClockwise = clockwise;
            img.fillAmount = amount;
        }

        /// <summary>Makes an image a horizontal fill (heat bar).</summary>
        public static void MakeHorizontalFill(Image img, float amount = 0f)
        {
            img.type = UnityEngine.UI.Image.Type.Filled;
            img.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            img.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            img.fillAmount = amount;
        }

        /// <summary>Creates a RawImage (no raycast) stretched over its parent.</summary>
        public static RawImage StretchRaw(string name, Transform parent, Texture texture, Color color)
        {
            var rt = Stretch(name, parent);
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            raw.color = color;
            raw.raycastTarget = false;
            raw.maskable = false;
            return raw;
        }

        /// <summary>
        /// Creates a TMP text with a role font. <paramref name="shadow"/> uses the soft dark underlay (HUD over the world);
        /// a <paramref name="glow"/> colour with alpha &gt; 0 uses a tinted phosphor glow instead (CRT text).
        /// </summary>
        public static TextMeshProUGUI Text(string name, Transform parent, FontSet fonts, FontRole role, float size, Color color,
            TextAlignmentOptions alignment, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 rectSize,
            bool shadow = true, Color glow = default)
        {
            var rt = Rect(name, parent, anchor, pivot, position, rectSize);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            UIFonts.Apply(t, fonts, role, shadow, glow);
            t.fontSize = size;
            t.color = color;
            t.alignment = alignment;
            t.raycastTarget = false;
            t.maskable = false;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.isTextObjectScaleStatic = false;
            t.SetText(string.Empty);
            return t;
        }

        /// <summary>Adds a CanvasGroup (no raycast blocking unless <paramref name="interactive"/>).</summary>
        public static CanvasGroup Group(GameObject go, float alpha = 1f, bool interactive = false)
        {
            var g = go.GetComponent<CanvasGroup>();
            if (g == null) g = go.AddComponent<CanvasGroup>();
            g.alpha = alpha;
            g.interactable = interactive;
            g.blocksRaycasts = interactive;
            return g;
        }

        /// <summary>
        /// Adds a nested Canvas to split rebuilds (dynamic elements such as markers and the reticle rebuild alone).
        /// Optionally adds a GraphicRaycaster (menus).
        /// </summary>
        public static Canvas SubCanvas(GameObject go, bool raycaster, int sortingOrder = 0, bool overrideSorting = false)
        {
            var c = go.GetComponent<Canvas>();
            if (c == null) c = go.AddComponent<Canvas>();
            c.overrideSorting = overrideSorting;
            if (overrideSorting) c.sortingOrder = sortingOrder;
            c.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal |
                                         AdditionalCanvasShaderChannels.Tangent;
            if (raycaster && go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
            return c;
        }

        /// <summary>Sets an image's alpha without touching its RGB (no allocation).</summary>
        public static void SetAlpha(Graphic g, float alpha)
        {
            if (g == null) return;
            var c = g.color;
            if (Mathf.Approximately(c.a, alpha)) return;
            c.a = alpha;
            g.color = c;
        }

        /// <summary>Sets a graphic's colour only when it changed (avoids needless mesh rebuilds).</summary>
        public static void SetColor(Graphic g, Color color)
        {
            if (g == null) return;
            if (g.color == color) return;
            g.color = color;
        }

        /// <summary>Enables/disables a GameObject only when the state changes.</summary>
        public static void SetActive(Component c, bool active)
        {
            if (c != null && c.gameObject.activeSelf != active) c.gameObject.SetActive(active);
        }
    }
}
