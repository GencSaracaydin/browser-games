using UnityEngine;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// Layout of the SX-70 polaroid frame (UI_POLAROID_FRAME T_UI_PolaroidFrame, 560 x 670 px including a 24 px soft
    /// drop shadow): the transparent photo window and the caption band, as fractions of the frame (x right, y down from
    /// the top, as in the import row). The procedural frame uses the same proportions.
    /// </summary>
    public static class PolaroidLayout
    {
        /// <summary>Frame size in source pixels.</summary>
        public static readonly Vector2 FrameSize = new Vector2(560f, 670f);

        /// <summary>Photo window, source pixels from the top-left: x 50-509, y 58-517.</summary>
        public static readonly Rect PhotoWindowPx = new Rect(50f, 58f, 459f, 459f);

        /// <summary>Caption band in the thick bottom border, source pixels from the top-left: y 527-634.</summary>
        public static readonly Rect CaptionPx = new Rect(50f, 527f, 459f, 107f);

        /// <summary>
        /// Converts a rect given in source pixels from the top-left into normalised anchors (min, max) of a RectTransform
        /// whose parent is the frame (y up).
        /// </summary>
        public static void ToAnchors(Rect px, out Vector2 anchorMin, out Vector2 anchorMax)
        {
            anchorMin = new Vector2(px.xMin / FrameSize.x, 1f - px.yMax / FrameSize.y);
            anchorMax = new Vector2(px.xMax / FrameSize.x, 1f - px.yMin / FrameSize.y);
        }
    }
}
