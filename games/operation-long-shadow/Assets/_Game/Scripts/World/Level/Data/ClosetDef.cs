using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// A spawn closet (Appendix A kind "C", GDD §7): the opening the spawns walk out of as seen from the play space (the
    /// AI's sight-test target, WP04) and two or more spawn points deep inside the room behind it.
    /// </summary>
    public sealed class ClosetDef
    {
        /// <summary>Closet id ("SC_B3_N").</summary>
        public string Id;
        /// <summary>Door / opening centre on the floor.</summary>
        public Vector3 Door;
        /// <summary>Door facing (compass, out of the room).</summary>
        public float DoorHeading;
        /// <summary>Spawn points inside the room.</summary>
        public Vector3[] Spawns;
        /// <summary>Spawn facing (compass, towards the door).</summary>
        public float SpawnHeading;
        /// <summary>Minimum player distance before the closet may spawn (WP04 SpawnCloset).</summary>
        public float MinPlayerDistance = 15f;
        /// <summary>Why the points are where they are.</summary>
        public string Notes;
    }
}
