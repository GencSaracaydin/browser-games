using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>Browser display facts (WP01 DisplayInfo via the jslib; editor fallbacks).</summary>
    public interface IDisplayInfo
    {
        /// <summary>window.devicePixelRatio, capped at 1.5 by the template.</summary>
        float DevicePixelRatio { get; }
        /// <summary>Pointer currently locked.</summary>
        bool IsPointerLocked { get; }
        /// <summary>Page runs inside an iframe (artifact host).</summary>
        bool InIframe { get; }
        /// <summary>Canvas size in device pixels (render-scale default, TECH_PLAN D21).</summary>
        Vector2Int CanvasPixelSize { get; }
    }
}
