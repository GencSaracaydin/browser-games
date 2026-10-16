using System;

namespace LongShadow.Combat.Data
{
    /// <summary>The M67-pattern frag grenade (GDD §13.2, TECH_PLAN §7.5). Treat as immutable.</summary>
    [Serializable]
    public sealed class GrenadeSpec
    {
        /// <summary>Weapon id.</summary>
        public string Id;
        /// <summary>Fuse from the pin pull (seconds).</summary>
        public float Fuse;
        /// <summary>Maximum throw distance on flat ground (metres).</summary>
        public float ThrowRange;
        /// <summary>Upward bias added to the view pitch when throwing (degrees, tuning).</summary>
        public float ThrowPitchBias = 12f;
        /// <summary>Damage at the centre.</summary>
        public float Damage;
        /// <summary>Full damage inside this radius.</summary>
        public float LethalRadius;
        /// <summary>Damage falls linearly to zero at this radius.</summary>
        public float MaxRadius;
        /// <summary>Frags at mission start.</summary>
        public int StartCount;
        /// <summary>Most frags carried.</summary>
        public int MaxCount;
        /// <summary>Frags restored at least on a checkpoint reload (GDD §17).</summary>
        public int CheckpointMinimum;
        /// <summary>AI hearing radius (GDD §14.2).</summary>
        public float NoiseRadius;
        /// <summary>Tinnitus inside this radius (TECH_PLAN §7.5).</summary>
        public float TinnitusRadius;
        /// <summary>Time from the press to the pin being out (cook starts at the press; throw no earlier than this).</summary>
        public float PinPullTime = 0.28f;
        /// <summary>Release animation time before the grenade leaves the hand.</summary>
        public float ReleaseTime = 0.12f;
        /// <summary>Recovery after the release before the weapon is usable again.</summary>
        public float RecoverTime = 0.38f;
        /// <summary>Fuse left when an AI grenade leaves the hand (AI cooks about a second).</summary>
        public float AiFuseAtRelease = 3.2f;
    }
}
