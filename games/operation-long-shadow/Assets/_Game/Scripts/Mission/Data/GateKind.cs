namespace LongShadow.Mission.Data
{
    /// <summary>What a gate entry controls.</summary>
    public enum GateKind
    {
        /// <summary>A progression gate (GDD §7.13): a door or PlayerClip that opens once.</summary>
        Gate = 0,
        /// <summary>A backtrack blocker (GDD §6.7): passable until its beat starts, then closed for good.</summary>
        Blocker = 1,
        /// <summary>A TriggerVolume armed by a gate rule (B7.T.Landing, B8.T.BasementFoot).</summary>
        TriggerArm = 2,
        /// <summary>An InteractionPoint made available by a rule (side door, Hall door, projector, heli door).</summary>
        Interaction = 3,
        /// <summary>Destructibles that stay invulnerable until a rule releases them (racks R1-R4 until D065).</summary>
        Invulnerable = 4,
    }
}
