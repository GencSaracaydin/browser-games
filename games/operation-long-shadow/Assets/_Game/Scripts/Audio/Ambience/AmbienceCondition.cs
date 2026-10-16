namespace LongShadow.Audio.Ambience
{
    /// <summary>Extra world state a bed can depend on (<see cref="AmbienceBedRule.Condition"/>).</summary>
    public enum AmbienceCondition
    {
        /// <summary>No extra condition.</summary>
        None = 0,
        /// <summary>Heard only while the station's transmitter racks are alive (the basement tube hum, GDD §7.9).</summary>
        TransmittersOn = 1,
    }
}
