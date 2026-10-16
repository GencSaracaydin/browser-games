namespace LongShadow.World.Look.Lightmaps
{
    /// <summary>What selects the active set of a <see cref="LightmapSwapper"/>.</summary>
    public enum LightmapSwapRule
    {
        /// <summary>Alternate <c>i</c> is active from LightStage s ≥ threshold <c>i</c> (S9_TownFar: SUNSET 1.35, DUSK 1.97; D7).</summary>
        StageThresholds = 0,
        /// <summary>Alternate 0 is active after the mission event (S6 basement EMERGENCY on <c>B9.TransmitterDead</c>, G11); saved.</summary>
        MissionEvent = 1,
    }
}
