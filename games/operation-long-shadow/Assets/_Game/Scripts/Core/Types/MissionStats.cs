using System;

namespace LongShadow.Core
{
    /// <summary>Debrief statistics (GDD §8.9). Owned by the MissionDirector (WP07); read by the debrief screen (WP08).</summary>
    [Serializable]
    public class MissionStats
    {
        /// <summary>Kills by the player.</summary>
        public int Kills;
        /// <summary>Player headshot kills.</summary>
        public int Headshots;
        /// <summary>Player shots fired.</summary>
        public int ShotsFired;
        /// <summary>Player shots that hit a damageable.</summary>
        public int ShotsHit;
        /// <summary>Grenades thrown by the player.</summary>
        public int GrenadesThrown;
        /// <summary>Player deaths.</summary>
        public int Deaths;
        /// <summary>Mission time in seconds (gameplay time, excluding pause).</summary>
        public float MissionSeconds;
        /// <summary>Main intel collected (of 3).</summary>
        public int Intel;
        /// <summary>Optional intel collected (of 2).</summary>
        public int OptionalIntel;

        /// <summary>Hit ratio 0..1 (0 when nothing was fired).</summary>
        public float Accuracy => ShotsFired > 0 ? (float)ShotsHit / ShotsFired : 0f;

        /// <summary>Resets every counter.</summary>
        public void Clear()
        {
            Kills = Headshots = ShotsFired = ShotsHit = GrenadesThrown = Deaths = Intel = OptionalIntel = 0;
            MissionSeconds = 0f;
        }

        /// <summary>Copies the values into a new instance.</summary>
        public MissionStats Clone() => (MissionStats)MemberwiseClone();
    }
}
