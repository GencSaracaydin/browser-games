using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// A shootable scripted target (WP03 Destructible: floods F1-F4, racks R1-R4, trucks, rooftop targets).
    /// Registered by id in <see cref="Destructibles"/> so Mission and World can find it without referencing Combat.
    /// </summary>
    public interface IDestructible
    {
        /// <summary>Id (matches its SceneMarker, e.g. "B6.F1", "B9.R2").</summary>
        string Id { get; }
        /// <summary>True once destroyed.</summary>
        bool IsDestroyed { get; }
        /// <summary>Remaining health 0..1.</summary>
        float Health01 { get; }
        /// <summary>Invulnerable until a script releases it (racks until D065, GDD §7.13).</summary>
        bool Invulnerable { get; set; }
        /// <summary>Root transform.</summary>
        Transform Root { get; }
        /// <summary>Restores the intact state (checkpoints).</summary>
        void ResetIntact();
        /// <summary>Destroys it immediately (scripts, debug).</summary>
        void ForceDestroy();
    }
}
