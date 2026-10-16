using LongShadow.Core;

namespace LongShadow.Audio.Core
{
    /// <summary>
    /// The layers of one weapon's shot (GDD §10.2, TECH_PLAN §7.9: transient + mech + body + zone tail + far variant).
    /// Cue ids follow <see cref="AudioCues"/>; the body layer is shared by weapon class (WP06_SFX: SFX_WPN_BODY_RIFLE
    /// for 5.56/7.62×39 rifles, SFX_WPN_BODY_HEAVY for the RPK, SVD, DShK and M60); the suppressed Hush Puppy has no
    /// body and only a faint tail.
    /// </summary>
    public readonly struct WeaponSoundProfile
    {
        /// <summary>Body layer cue of rifles.</summary>
        public const string BodyRifle = "SFX_WPN_BODY_RIFLE";
        /// <summary>Body layer cue of heavy weapons.</summary>
        public const string BodyHeavy = "SFX_WPN_BODY_HEAVY";

        /// <summary>Transient cue (random of 4).</summary>
        public readonly string Shot;
        /// <summary>Mechanical layer (player and door gun only), or null.</summary>
        public readonly string Mech;
        /// <summary>Low-frequency body layer, or null.</summary>
        public readonly string Body;
        /// <summary>Tail level relative to the cue volume (suppressed weapons are quieter).</summary>
        public readonly float TailGain;
        /// <summary>Overall level of the AI version (distance rolloff does the rest).</summary>
        public readonly float Level;
        /// <summary>Rolloff min distance of the near layers when fired by AI.</summary>
        public readonly float NearMinDistance;
        /// <summary>True for suppressed weapons (no far variant crossfade beyond 60 m: inaudible).</summary>
        public readonly bool Suppressed;

        private WeaponSoundProfile(string shot, string mech, string body, float tailGain, float level, float nearMin, bool suppressed)
        {
            Shot = shot;
            Mech = mech;
            Body = body;
            TailGain = tailGain;
            Level = level;
            NearMinDistance = nearMin;
            Suppressed = suppressed;
        }

        /// <summary>The profile of a weapon id (<see cref="WeaponIds"/>); unknown ids get a rifle profile.</summary>
        public static WeaponSoundProfile For(string weaponId)
        {
            var shot = AudioCues.WeaponShot(weaponId);
            var mech = AudioCues.WeaponMech(weaponId);
            switch (weaponId)
            {
                case WeaponIds.HushPuppy:
                    return new WeaponSoundProfile(shot, mech, null, 0.18f, 0.6f, 2f, true);
                case WeaponIds.RPK:
                case WeaponIds.SVD:
                case WeaponIds.DoorGun:
                    return new WeaponSoundProfile(shot, mech, BodyHeavy, 1f, 1f, 8f, false);
                case WeaponIds.DShK:
                    return new WeaponSoundProfile(shot, null, BodyHeavy, 1.1f, 1f, 12f, false);
                case WeaponIds.RPG7:
                    return new WeaponSoundProfile(shot, null, null, 0.9f, 1f, 8f, false);
                default: // COMMANDO, AK, AKMS
                    return new WeaponSoundProfile(shot, mech, BodyRifle, 1f, 1f, 6f, false);
            }
        }
    }
}
