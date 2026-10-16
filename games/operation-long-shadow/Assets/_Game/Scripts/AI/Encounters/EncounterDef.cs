using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.AI.Encounters
{
    /// <summary>One encounter of GDD §7 (E2-E11): beat, section, max alive, waves and enemies.</summary>
    public sealed class EncounterDef
    {
        private readonly List<WaveDef> _waves = new List<WaveDef>();
        private readonly List<EnemySpawnDef> _enemies = new List<EnemySpawnDef>();

        /// <summary>Creates an encounter.</summary>
        public EncounterDef(string id, int beat, SectionId section, int maxAlive)
        {
            Id = id;
            Beat = beat;
            Section = section;
            MaxAlive = maxAlive;
        }

        /// <summary>Encounter id ("E3").</summary>
        public string Id { get; }
        /// <summary>Beat number (straggler despawn two beats later).</summary>
        public int Beat { get; }
        /// <summary>Section whose load prewarms the pools.</summary>
        public SectionId Section { get; }
        /// <summary>Max alive at once (GDD §7 "max alive"; the spawner queues the rest).</summary>
        public int MaxAlive { get; }
        /// <summary>Waves in definition order.</summary>
        public IReadOnlyList<WaveDef> Waves => _waves;
        /// <summary>Enemies in table order.</summary>
        public IReadOnlyList<EnemySpawnDef> Enemies => _enemies;
        /// <summary>Design notes.</summary>
        public string Notes { get; set; }

        /// <summary>Adds a wave.</summary>
        public WaveDef AddWave(WaveDef wave)
        {
            _waves.Add(wave);
            return wave;
        }

        /// <summary>Adds an enemy.</summary>
        public EnemySpawnDef Add(EnemySpawnDef enemy)
        {
            _enemies.Add(enemy);
            return enemy;
        }

        /// <summary>A wave by id, or null.</summary>
        public WaveDef GetWave(string id)
        {
            for (var i = 0; i < _waves.Count; i++)
                if (_waves[i].Id == id) return _waves[i];
            return null;
        }

        /// <summary>An enemy by id, or null.</summary>
        public EnemySpawnDef GetEnemy(string id)
        {
            for (var i = 0; i < _enemies.Count; i++)
                if (_enemies[i].Id == id) return _enemies[i];
            return null;
        }

        /// <summary>Members of a wave.</summary>
        public int CountInWave(string wave)
        {
            var n = 0;
            for (var i = 0; i < _enemies.Count; i++)
                if (_enemies[i].Wave == wave) n++;
            return n;
        }
    }
}
