namespace LongShadow.Cinematics
{
    /// <summary>Easing of a camera move, look turn or ramp over its clip.</summary>
    public enum ShotEase
    {
        /// <summary>Constant speed.</summary>
        Linear,
        /// <summary>Smoothstep (slow in and out): dolly and crane moves.</summary>
        Smooth,
        /// <summary>Accelerating (quadratic).</summary>
        EaseIn,
        /// <summary>Decelerating (quadratic): pushes that settle, the helicopter flare.</summary>
        EaseOut,
        /// <summary>Cubic slow in/out: forced head turns.</summary>
        EaseInOutCubic,
    }
}
