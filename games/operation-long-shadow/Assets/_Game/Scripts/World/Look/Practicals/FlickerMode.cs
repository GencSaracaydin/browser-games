namespace LongShadow.World.Look.Practicals
{
    /// <summary>Brightness behaviour of a <see cref="PracticalLight"/> (see <see cref="FlickerNoise"/>).</summary>
    public enum FlickerMode
    {
        /// <summary>Constant.</summary>
        Steady = 0,
        /// <summary>Open fire.</summary>
        Fire = 1,
        /// <summary>Fluorescent tube with rare dips.</summary>
        Fluorescent = 2,
        /// <summary>Failing bulb / EXIT sign (bursts of off-blinks).</summary>
        Faulty = 3,
        /// <summary>Magnesium flare sputter.</summary>
        Flare = 4,
        /// <summary>Slow 1 Hz pulse (ON AIR lamp, GDD §7.9).</summary>
        Pulse1Hz = 5,
    }
}
