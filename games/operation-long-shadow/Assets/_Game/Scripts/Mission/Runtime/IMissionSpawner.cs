using LongShadow.Mission.Data;
using UnityEngine;

namespace LongShadow.Mission.Runtime
{
    /// <summary>
    /// Scene objects the mission creates (intel pickups, TECH_PLAN §7.11 "Interactable spawns"). The runtime host
    /// (<c>MissionSystems</c>) provides the implementation that instantiates prefabs from LIB_Mission or greybox
    /// placeholders; the EditMode fake world provides one that only records the calls (no GameObjects).
    /// </summary>
    public interface IMissionSpawner
    {
        /// <summary>Spawns (or re-enables) the pickup of an intel item at a world pose. Idempotent per id.</summary>
        void SpawnIntel(IntelDef intel, Vector3 position, Quaternion rotation, Transform anchor);
        /// <summary>Removes the pickup of an intel item (collected, expired or reset).</summary>
        void DespawnIntel(string intelId);
        /// <summary>Removes every spawned object (mission start, quit).</summary>
        void Clear();
        /// <summary>The intel card photo (LIB_Mission), or null while it has not landed.</summary>
        Sprite IntelPhoto(IntelDef intel);
        /// <summary>
        /// A helper transform at a world position for a marker that is missing from the scene (look targets such as
        /// B5.SunLook), created once per id; null where no scene objects can be created (tests).
        /// </summary>
        Transform Anchor(string id, Vector3 position);
    }
}
