using System;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>Combat resolution for AI and scripts (WP03 CombatService).</summary>
    public interface ICombatService
    {
        /// <summary>Resolves an AI shot: roll, damage or near miss, tracer, impact, events.</summary>
        void FireAI(in AIShotRequest r);
        /// <summary>AI grenade throw from a point to a target point.</summary>
        void ThrowGrenadeAI(Vector3 from, Vector3 to, Team team);
        /// <summary>RPG rocket; <paramref name="onImpact"/> receives the impact point.</summary>
        void FireRocket(Vector3 from, Vector3 to, Team team, Action<Vector3> onImpact);
        /// <summary>Explosion with linear falloff from lethal to max radius and a cover raycast.</summary>
        void Explode(Vector3 position, float damage, float lethalRadius, float maxRadius, GameObject instigator, Team team);
        /// <summary>Smoke cloud that blocks AI sight (SmokeLOS volume) for <paramref name="seconds"/>.</summary>
        void SpawnSmoke(Vector3 position, Color color, float seconds);
        /// <summary>Spawns a weapon pickup.</summary>
        void SpawnPickup(string weaponId, Vector3 position);
        /// <summary>
        /// Spawns a resupply crate (HOLD [E] Resupply, 1.0 s, unlimited uses, GDD §13.2) and returns it. The
        /// MissionDirector (WP07) calls this at the <c>CRATE.*</c> markers when their section loads; Combat never
        /// spawns crates on its own. A missing crate prefab falls back to a placeholder box.
        /// </summary>
        GameObject SpawnAmmoCrate(Vector3 position, Quaternion rotation);
    }
}
