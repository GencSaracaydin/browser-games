namespace LongShadow.AI.Encounters
{
    /// <summary>One wave of an encounter (GDD §7 "Spawn" and "Trigger / notes" columns).</summary>
    public sealed class WaveDef
    {
        /// <summary>Creates a wave.</summary>
        public WaveDef(string id, WaveTrigger trigger)
        {
            Id = id;
            Trigger = trigger ?? WaveTrigger.OnActivate();
        }

        /// <summary>Wave id within the encounter ("A", "B", "Gate").</summary>
        public string Id { get; }
        /// <summary>Start rule.</summary>
        public WaveTrigger Trigger { get; }
        /// <summary>Members spawn already in combat (aware of the player's position).</summary>
        public bool StartAlerted { get; private set; }
        /// <summary>Optional rule that alerts the living members (e.g. B2 gate fight trigger).</summary>
        public WaveTrigger AlertOn { get; private set; }
        /// <summary>The wave only starts while the encounter has at most this many alive (-1 = no gate).</summary>
        public int GateMaxAlive { get; private set; } = -1;
        /// <summary>Preferred cover-node group when alerted ("B2.AlertCover").</summary>
        public string CoverGroup { get; private set; }
        /// <summary>Design notes.</summary>
        public string Notes { get; private set; }

        /// <summary>Spawns alerted.</summary>
        public WaveDef Alerted(bool alerted = true) { StartAlerted = alerted; return this; }
        /// <summary>Sets the alert rule.</summary>
        public WaveDef AlertWhen(WaveTrigger rule) { AlertOn = rule; return this; }
        /// <summary>Sets the alive gate.</summary>
        public WaveDef OnlyWhileAliveAtMost(int n) { GateMaxAlive = n; return this; }
        /// <summary>Sets the preferred cover group.</summary>
        public WaveDef Cover(string group) { CoverGroup = group; return this; }
        /// <summary>Sets notes.</summary>
        public WaveDef Note(string notes) { Notes = notes; return this; }
    }
}
