using UnityEngine;

namespace LongShadow.Mission.Data
{
    /// <summary>What an objective marker points at.</summary>
    public enum ObjectiveTargetKind
    {
        /// <summary>A markup id (Appendix A), resolved through IMarkerRegistry (CSV position when missing).</summary>
        Marker = 0,
        /// <summary>A squad member id (SquadIds), resolved through ISquadService.Member.</summary>
        SquadMember = 1,
        /// <summary>An encounter enemy id ("E4-01"), resolved through IEncounterService.Actor.</summary>
        Actor = 2,
        /// <summary>A destructible id ("B6.F1"), resolved through Destructibles; the marker drops when it is destroyed.</summary>
        Destructible = 3,
    }

    /// <summary>
    /// One marker target of an objective (GDD §7.12 "Marker" column). <see cref="Fallback"/> is the Appendix A world
    /// position used until the target resolves (section not loaded, enemy not spawned yet), so the HUD always has a
    /// bearing; <see cref="Offset"/> lifts the marker above the target (heads, truck beds).
    /// </summary>
    public readonly struct ObjectiveTarget
    {
        /// <summary>Target kind.</summary>
        public readonly ObjectiveTargetKind Kind;
        /// <summary>Target id (marker, member, actor or destructible id).</summary>
        public readonly string Id;
        /// <summary>World position used while the target does not resolve (Appendix A).</summary>
        public readonly Vector3 Fallback;
        /// <summary>Offset from the resolved transform.</summary>
        public readonly Vector3 Offset;

        /// <summary>Creates a target.</summary>
        public ObjectiveTarget(ObjectiveTargetKind kind, string id, Vector3 fallback, Vector3 offset)
        {
            Kind = kind;
            Id = id;
            Fallback = fallback;
            Offset = offset;
        }

        /// <summary>A markup id target.</summary>
        public static ObjectiveTarget Marker(string id, float x, float y, float z) =>
            new ObjectiveTarget(ObjectiveTargetKind.Marker, id, new Vector3(x, y, z), Vector3.zero);

        /// <summary>A squad member (FOLLOW chevron; the HUD lifts it 2.15 m above the root itself, WP08).</summary>
        public static ObjectiveTarget Member(string memberId) =>
            new ObjectiveTarget(ObjectiveTargetKind.SquadMember, memberId, Vector3.zero, Vector3.zero);

        /// <summary>
        /// An encounter enemy at its GDD §7 position (fallback until it spawns); offset zero, the HUD lifts actor markers
        /// 1.3 m itself (WP08). <paramref name="lift"/> raises only the fallback (e.g. the technical's gun at +1).
        /// </summary>
        public static ObjectiveTarget Actor(string enemyId, float x, float y, float z, float lift = 0f) =>
            new ObjectiveTarget(ObjectiveTargetKind.Actor, enemyId, new Vector3(x, y + lift, z), Vector3.zero);

        /// <summary>A destructible (floods, racks, trucks).</summary>
        public static ObjectiveTarget Destructible(string id, float x, float y, float z) =>
            new ObjectiveTarget(ObjectiveTargetKind.Destructible, id, new Vector3(x, y, z), Vector3.zero);

        /// <summary>True for a usable target.</summary>
        public bool IsValid => !string.IsNullOrEmpty(Id);
    }
}
