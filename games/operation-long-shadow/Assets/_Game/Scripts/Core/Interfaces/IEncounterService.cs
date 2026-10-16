namespace LongShadow.Core
{
    /// <summary>Encounter spawning from EncounterTable (WP04 EncounterSpawner). Also ISaveable (killed ids).</summary>
    public interface IEncounterService
    {
        /// <summary>Activates an encounter (spawns its placed enemies, arms its waves).</summary>
        void Activate(string encounterId);
        /// <summary>Starts a wave now.</summary>
        void TriggerWave(string encounterId, string wave);
        /// <summary>Despawns an encounter's living enemies (stragglers, skipped beats).</summary>
        void Deactivate(string encounterId);
        /// <summary>Living enemies of an encounter (optionally one wave).</summary>
        int Alive(string encounterId, string wave = null);
        /// <summary>Killed enemies of an encounter.</summary>
        int Killed(string encounterId);
        /// <summary>True when every wave is spawned and dead.</summary>
        bool Cleared(string encounterId);
        /// <summary>A living enemy by id ("E4-03"), or null.</summary>
        ITargetable Actor(string enemyId);
        /// <summary>Total living enemies (hard cap 12).</summary>
        int AliveTotal { get; }
        /// <summary>
        /// Detection meter 0..1 of a living enemy (the Beat 2 sentry HUD icon, GDD §14.2); 0 when unknown or dead.
        /// </summary>
        float Awareness01(string enemyId);
    }
}
