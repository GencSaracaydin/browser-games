using UnityEngine;

namespace LongShadow.Mission.Data
{
    /// <summary>
    /// A checkpoint (GDD §17: CP1-CP10 at beat starts plus CP3b at the market Crossing; Appendix A "CP"). The
    /// CheckpointMarker in the section scene is authoritative for the spawn position and yaw; the values here are the
    /// Appendix A fallbacks used before the level exists.
    /// </summary>
    public sealed class CheckpointDef
    {
        /// <summary>Checkpoint id (SequenceIds.CP*).</summary>
        public readonly string Id;
        /// <summary>Beat the checkpoint belongs to.</summary>
        public readonly int Beat;
        /// <summary>Spawn position (Appendix A).</summary>
        public readonly Vector3 Position;
        /// <summary>Spawn compass heading in degrees.</summary>
        public readonly float Yaw;
        /// <summary>Squad node set for cold jumps and reloads (null = follow mode; the CheckpointMarker's own value wins).</summary>
        public readonly string SquadNodeSet;
        /// <summary>LightStage s at this checkpoint on a cold jump (beat start s, or the mid-beat value for CP3b).</summary>
        public readonly float EntryS;
        /// <summary>True for the checkpoint that opens its beat (false for CP3b).</summary>
        public readonly bool BeatStart;

        /// <summary>Creates a definition.</summary>
        public CheckpointDef(string id, int beat, Vector3 position, float yaw, string squadNodeSet, float entryS, bool beatStart)
        {
            Id = id;
            Beat = beat;
            Position = position;
            Yaw = yaw;
            SquadNodeSet = squadNodeSet;
            EntryS = entryS;
            BeatStart = beatStart;
        }
    }
}
