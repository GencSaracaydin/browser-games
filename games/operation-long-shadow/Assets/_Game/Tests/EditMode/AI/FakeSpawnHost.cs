using System;
using System.Collections.Generic;
using LongShadow.AI.Encounters;

namespace LongShadow.Tests.EditMode.AI
{
    /// <summary>A world stand-in for <see cref="EncounterRuntime"/> tests: spawns instantly (or as told), counts callbacks.</summary>
    internal sealed class FakeSpawnHost : ISpawnHost
    {
        public readonly List<EncounterRuntime> Runtimes = new List<EncounterRuntime>();
        public readonly HashSet<string> Inside = new HashSet<string>();
        public Func<EnemySlot, SpawnResult> Decide = s => SpawnResult.Spawned;
        public int ExtraAlive;
        public int WavesSpawned;
        public int MoraleBreaks;
        public int Alerted;
        public int Cleared;
        public int Despawned;
        public int PeakAlive;
        public string LastBrokenWave;

        public int AliveTotal
        {
            get
            {
                var n = ExtraAlive;
                foreach (var r in Runtimes) n += r.Alive();
                return n;
            }
        }

        public SpawnResult TrySpawn(EncounterRuntime encounter, EnemySlot slot, float now)
        {
            var r = Decide(slot);
            if (r == SpawnResult.Spawned && AliveTotal + 1 > PeakAlive) PeakAlive = AliveTotal + 1;
            return r;
        }

        public void Despawn(EnemySlot slot) => Despawned++;
        public bool PlayerInside(string triggerId) => Inside.Contains(triggerId);
        public void OnWaveSpawned(EncounterRuntime encounter, WaveDef wave) => WavesSpawned++;
        public void OnAlerted(EncounterRuntime encounter, EnemySlot slot) => Alerted++;

        public void OnMoraleBreak(EncounterRuntime encounter, WaveDef wave)
        {
            MoraleBreaks++;
            LastBrokenWave = wave.Id;
        }

        public void OnCleared(EncounterRuntime encounter) => Cleared++;

        public EncounterRuntime Start(EncounterDef def, float now = 0f, ICollection<string> killed = null)
        {
            var r = new EncounterRuntime(def, this, killed);
            Runtimes.Add(r);
            r.Activate(now);
            return r;
        }

        /// <summary>Kills the first <paramref name="n"/> living members of a wave (null = any).</summary>
        public static int Kill(EncounterRuntime r, string wave, int n, float now)
        {
            var killed = 0;
            foreach (var s in r.Slots)
            {
                if (killed >= n) break;
                if (s.State != SlotState.Alive || (wave != null && s.Def.Wave != wave)) continue;
                r.NotifyDeath(s, now);
                killed++;
            }
            return killed;
        }
    }
}
