using LongShadow.AI.Encounters;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Enemy
{
    /// <summary>Everything an enemy needs when the spawner (re)uses it: its table row, wave, where and how it enters.</summary>
    public sealed class EnemySpawnContext
    {
        /// <summary>Encounter.</summary>
        public EncounterDef Encounter;
        /// <summary>Wave.</summary>
        public WaveDef Wave;
        /// <summary>Table row.</summary>
        public EnemySpawnDef Def;
        /// <summary>Spawned already in combat.</summary>
        public bool Alerted;
        /// <summary>Spawn time.</summary>
        public float SpawnTime;
        /// <summary>Where it appears (closet spawn point, marker or table position).</summary>
        public Vector3 Position;
        /// <summary>Initial rotation.</summary>
        public Quaternion Rotation = Quaternion.identity;
        /// <summary>Perch to stand on (null = none or same as the spawn position).</summary>
        public Vector3? PerchPosition;
        /// <summary>Perch tag (callouts).</summary>
        public PerchTag Perch = PerchTag.Ground;
        /// <summary>Vehicle it rides (null = none).</summary>
        public ISplineVehicle Vehicle;
        /// <summary>Seat transform on the vehicle (null = vehicle root).</summary>
        public Transform Seat;
        /// <summary>Flank route points (resolved from the marker or inline route; null = none).</summary>
        public Vector3[] Route;
        /// <summary>Suppress position for the pair suppressor (null = none).</summary>
        public Vector3? SuppressPosition;
        /// <summary>Encounter activation time (Beat 10/11 timelines).</summary>
        public float EncounterTime;

        /// <summary>Enemy id.</summary>
        public string Id => Def != null ? Def.Id : null;
    }
}
