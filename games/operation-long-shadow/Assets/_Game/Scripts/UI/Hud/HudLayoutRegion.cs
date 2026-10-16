using UnityEngine;

namespace LongShadow.UI.Hud
{
    /// <summary>A named static HUD region for the overlap audit (canvas units, origin bottom-left).</summary>
    public readonly struct HudLayoutRegion
    {
        /// <summary>Region name ("Compass", "Ammo" ...).</summary>
        public readonly string Name;
        /// <summary>Rect in canvas units.</summary>
        public readonly Rect Rect;

        /// <summary>Creates a region.</summary>
        public HudLayoutRegion(string name, Rect rect)
        {
            Name = name;
            Rect = rect;
        }

        /// <summary>Region of a RectTransform inside the canvas (corners projected into canvas space).</summary>
        public static HudLayoutRegion Of(string name, RectTransform rt, RectTransform canvas)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var canvasRect = canvas.rect;
            for (var i = 0; i < 4; i++)
            {
                var local = (Vector2)canvas.InverseTransformPoint(corners[i]) - canvasRect.min;
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return new HudLayoutRegion(name, Rect.MinMaxRect(min.x, min.y, max.x, max.y));
        }

        /// <inheritdoc/>
        public override string ToString() =>
            Name + " [" + Rect.xMin.ToString("0") + ".." + Rect.xMax.ToString("0") + ", " + Rect.yMin.ToString("0") + ".." + Rect.yMax.ToString("0") + "]";
    }
}
