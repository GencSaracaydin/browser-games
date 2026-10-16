using LongShadow.AI.Enemy;

namespace LongShadow.AI.Encounters
{
    /// <summary>Runtime state of one row of an encounter table inside an <see cref="EncounterRuntime"/>.</summary>
    public sealed class EnemySlot
    {
        /// <summary>Creates a slot.</summary>
        public EnemySlot(EncounterRuntime encounter, EnemySpawnDef def, WaveDef wave, int order)
        {
            Encounter = encounter;
            Def = def;
            Wave = wave;
            Order = order;
        }

        /// <summary>Owning encounter.</summary>
        public EncounterRuntime Encounter { get; }
        /// <summary>Table row.</summary>
        public EnemySpawnDef Def { get; }
        /// <summary>Wave (never null; unknown wave ids get a manual wave).</summary>
        public WaveDef Wave { get; }
        /// <summary>Table order (stable tie-break).</summary>
        public int Order { get; }
        /// <summary>State.</summary>
        public SlotState State { get; internal set; }
        /// <summary>Earliest spawn time once queued (wave start + delay).</summary>
        public float DueAt { get; internal set; }
        /// <summary>Time it was queued.</summary>
        public float QueuedAt { get; internal set; }
        /// <summary>Time it spawned.</summary>
        public float SpawnedAt { get; internal set; } = float.NegativeInfinity;
        /// <summary>Time it died (NegativeInfinity when restored dead from a checkpoint).</summary>
        public float DiedAt { get; internal set; } = float.NegativeInfinity;
        /// <summary>First time a spawn attempt had to wait (closet patience).</summary>
        public float WaitingSince { get; internal set; } = float.PositiveInfinity;
        /// <summary>Spawns (or is told) alerted.</summary>
        public bool Alerted { get; internal set; }
        /// <summary>The live enemy (null unless <see cref="SlotState.Alive"/> or a corpse awaiting despawn).</summary>
        public EnemyActor Instance { get; set; }
        /// <summary>Enemy id.</summary>
        public string Id => Def.Id;
        /// <summary>True once it has been in the world or is finished (alive, dead, despawned).</summary>
        public bool Resolved => State == SlotState.Alive || State == SlotState.Dead || State == SlotState.Despawned;
        /// <summary>True when finished (dead or despawned).</summary>
        public bool Done => State == SlotState.Dead || State == SlotState.Despawned;
    }
}
