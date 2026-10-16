namespace LongShadow.Mission.Beats
{
    /// <summary>Creates the beat scripts in beat order (index = beat number 0..12).</summary>
    public static class BeatScripts
    {
        /// <summary>Intro, Beats 1-11, Outro.</summary>
        public static BeatScript[] CreateAll() => new BeatScript[]
        {
            new IntroFlow(),
            new Beat01Shoulder(),
            new Beat02PumpHouse(),
            new Beat03LongMarket(),
            new Beat04ClockSquare(),
            new Beat05LastLight(),
            new Beat06RelayYard(),
            new Beat07RelayHall(),
            new Beat08Reel(),
            new Beat09Clockmaker(),
            new Beat10Pitch(),
            new Beat11Magpie(),
            new OutroFlow(),
        };
    }
}
