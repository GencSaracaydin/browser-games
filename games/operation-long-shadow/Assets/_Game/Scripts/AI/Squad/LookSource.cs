namespace LongShadow.AI.Squad
{
    /// <summary>What drives a squadmate's head (<see cref="SquadLookAt"/>), highest priority first after None.</summary>
    public enum LookSource
    {
        /// <summary>Nothing (free head, locomotion look).</summary>
        None,
        /// <summary>Beat-script override (<c>ISquadService.SetLookTarget</c>).</summary>
        Override,
        /// <summary>The current speaker in dialogue.</summary>
        Speaker,
        /// <summary>The player within 3 m.</summary>
        Player,
        /// <summary>The combat threat.</summary>
        Threat,
    }
}
