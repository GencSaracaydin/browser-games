namespace LongShadow.UI.Menus
{
    /// <summary>Control used by an option row.</summary>
    public enum OptionKind
    {
        /// <summary>Numeric bar (<see cref="CrtSlider"/>).</summary>
        Slider = 0,
        /// <summary>Choice list (<see cref="CrtStepper"/>); the value is the choice index.</summary>
        Stepper = 1,
    }
}
