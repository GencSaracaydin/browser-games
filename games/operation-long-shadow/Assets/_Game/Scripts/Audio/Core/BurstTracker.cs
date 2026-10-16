using UnityEngine;

namespace LongShadow.Audio.Core
{
    /// <summary>
    /// Decides when a weapon's zone tail plays (TECH_PLAN §7.9 "auto fire re-triggers the one-shot at RPM and plays the
    /// tail on release"; WP06_SFX notes "on full auto play it on trigger release and every ~4th round"). Shots of one
    /// shooter closer together than <see cref="JoinSeconds"/> form a burst. The first shot of a burst and every
    /// <see cref="AudioConstants.TailEveryNthRound"/>th play the tail at once; when the burst ends and its last shot had
    /// no tail, a release tail is reported with the time since that shot, so the player starts the tail clip that far
    /// in and it sounds as if it began on the last round. Fixed slot count, no allocation, pure (unit-tested).
    /// </summary>
    public sealed class BurstTracker
    {
        /// <summary>A pending release tail.</summary>
        public struct Release
        {
            /// <summary>Shooter key.</summary>
            public int Key;
            /// <summary>Where the last shot was fired.</summary>
            public Vector3 Position;
            /// <summary>Seconds since the last shot (start offset into the tail clip).</summary>
            public float Offset;
            /// <summary>Weapon id of the burst.</summary>
            public string WeaponId;
            /// <summary>True for the player's weapon.</summary>
            public bool IsPlayer;
        }

        private struct Slot
        {
            public bool Used;
            public int Key;
            public float LastShot;
            public float Interval;
            public int Count;
            public bool LastHadTail;
            public Vector3 Position;
            public string WeaponId;
            public bool IsPlayer;
        }

        /// <summary>Shots closer than this belong to one burst (covers ≥ 200 RPM).</summary>
        public const float JoinSeconds = 0.3f;
        /// <summary>Shortest release detection delay.</summary>
        public const float MinRelease = 0.09f;

        private readonly Slot[] _slots;

        /// <summary>Creates a tracker with <paramref name="slots"/> concurrent shooters.</summary>
        public BurstTracker(int slots = 16)
        {
            _slots = new Slot[Mathf.Max(1, slots)];
        }

        /// <summary>
        /// Records a shot; returns true if the tail plays with this shot. <paramref name="firstOfBurst"/> is true for the
        /// first round of a burst.
        /// </summary>
        public bool OnShot(int key, string weaponId, bool isPlayer, Vector3 position, float now, out bool firstOfBurst)
        {
            var i = Find(key);
            if (i >= 0 && now - _slots[i].LastShot <= JoinSeconds)
            {
                ref var s = ref _slots[i];
                var dt = now - s.LastShot;
                s.Interval = s.Count <= 1 ? dt : Mathf.Lerp(s.Interval, dt, 0.5f);
                s.Count++;
                s.LastShot = now;
                s.Position = position;
                s.LastHadTail = s.Count % AudioConstants.TailEveryNthRound == 0;
                firstOfBurst = false;
                return s.LastHadTail;
            }
            if (i < 0) i = Allocate(now);
            _slots[i] = new Slot
            {
                Used = true, Key = key, LastShot = now, Interval = 0f, Count = 1, LastHadTail = true, Position = position,
                WeaponId = weaponId, IsPlayer = isPlayer,
            };
            firstOfBurst = true;
            return true;
        }

        /// <summary>
        /// Collects bursts that ended by <paramref name="now"/> (no shot for 1.5 × the measured interval, at least
        /// <see cref="MinRelease"/> and at most <see cref="JoinSeconds"/>) into <paramref name="results"/>; returns how many
        /// need a release tail. Ended bursts are freed either way.
        /// </summary>
        public int CollectReleases(float now, Release[] results)
        {
            var n = 0;
            for (var i = 0; i < _slots.Length; i++)
            {
                ref var s = ref _slots[i];
                if (!s.Used) continue;
                var wait = s.Count <= 1 ? JoinSeconds : Mathf.Clamp(s.Interval * 1.5f, MinRelease, JoinSeconds);
                var since = now - s.LastShot;
                if (since < wait) continue;
                if (!s.LastHadTail && results != null && n < results.Length)
                {
                    results[n++] = new Release { Key = s.Key, Position = s.Position, Offset = since, WeaponId = s.WeaponId, IsPlayer = s.IsPlayer };
                }
                s.Used = false;
            }
            return n;
        }

        /// <summary>Shots in the current burst of a key (0 when none).</summary>
        public int CountOf(int key)
        {
            var i = Find(key);
            return i >= 0 ? _slots[i].Count : 0;
        }

        /// <summary>Forgets every burst.</summary>
        public void Clear()
        {
            for (var i = 0; i < _slots.Length; i++) _slots[i].Used = false;
        }

        /// <summary>Stable key of a shooter: weapon, player flag and a 2 m position cell (AI shots carry no shooter id).</summary>
        public static int KeyOf(string weaponId, bool isPlayer, Vector3 position)
        {
            unchecked
            {
                var h = weaponId != null ? weaponId.GetHashCode() : 17;
                h = h * 31 + (isPlayer ? 1 : 0);
                if (isPlayer) return h;
                h = h * 31 + Mathf.FloorToInt(position.x * 0.5f);
                h = h * 31 + Mathf.FloorToInt(position.y * 0.5f);
                h = h * 31 + Mathf.FloorToInt(position.z * 0.5f);
                return h;
            }
        }

        private int Find(int key)
        {
            for (var i = 0; i < _slots.Length; i++)
                if (_slots[i].Used && _slots[i].Key == key) return i;
            return -1;
        }

        private int Allocate(float now)
        {
            var oldest = 0;
            for (var i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Used) return i;
                if (_slots[i].LastShot < _slots[oldest].LastShot) oldest = i;
            }
            return oldest;
        }
    }
}
