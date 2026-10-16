namespace LongShadow.World.Look.Practicals
{
    /// <summary>What turns a <see cref="PracticalLight"/> on.</summary>
    public enum PracticalSwitch
    {
        /// <summary>Always on (interior tungsten, booth tubes).</summary>
        AlwaysOn = 0,
        /// <summary>On from the sodium strike (LightStage s ≥ 1.97) without the ripple (snap).</summary>
        AfterStrike = 1,
        /// <summary>On after sunset (s ≥ 2.0).</summary>
        AfterSunset = 2,
        /// <summary>Off until the mission event <c>OnEvent</c>, off again on <c>OffEvent</c> (saved at checkpoints).</summary>
        MissionEvent = 3,
        /// <summary>Scripts call <see cref="PracticalLight.SetOn"/> (saved at checkpoints when it has an id).</summary>
        Manual = 4,
    }
}
