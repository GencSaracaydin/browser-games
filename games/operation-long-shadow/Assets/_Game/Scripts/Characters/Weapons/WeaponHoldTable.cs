using LongShadow.Characters.Animation;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.Weapons
{
    /// <summary>
    /// Code defaults for carrying each weapon id (TECH_PLAN D12: numbers are code, assets are libraries). Used when
    /// LIB_CharacterWeapons has no entry, when a world prefab lacks Muzzle/LeftGrip sockets, and to build the greybox
    /// placeholder gun. Distances are metres in the weapon's RightGrip frame (z = toward the muzzle, y = up), measured
    /// on the real patterns (GDD §13: XM177E2, AKM, RPK, AKMS, SVD, RPG-7, Mk 22).
    /// </summary>
    public static class WeaponHoldTable
    {
        /// <summary>One row of defaults.</summary>
        public readonly struct Row
        {
            public readonly string Id;
            public readonly WeaponHold Hold;
            public readonly HandPose HandPose;
            /// <summary>Muzzle position from the pistol grip.</summary>
            public readonly Vector3 Muzzle;
            /// <summary>Support-hand grip position from the pistol grip.</summary>
            public readonly Vector3 LeftGrip;
            /// <summary>Roll of the support grip around the barrel (deg; pistols wrap from the side).</summary>
            public readonly float LeftGripRoll;
            /// <summary>Stock end from the pistol grip (negative z), for the placeholder shape.</summary>
            public readonly float StockLength;
            /// <summary>The gun is part of a vehicle/mount: bodies show no held visual.</summary>
            public readonly bool Mounted;
            /// <summary>Not a carried weapon (grenades, knife): bodies show nothing.</summary>
            public readonly bool NotHeld;

            public Row(string id, WeaponHold hold, HandPose pose, Vector3 muzzle, Vector3 leftGrip, float stockLength,
                       float leftGripRoll = 0f, bool mounted = false, bool notHeld = false)
            {
                Id = id;
                Hold = hold;
                HandPose = pose;
                Muzzle = muzzle;
                LeftGrip = leftGrip;
                StockLength = stockLength;
                LeftGripRoll = leftGripRoll;
                Mounted = mounted;
                NotHeld = notHeld;
            }

            /// <summary>True if the body carries a visible weapon for this id.</summary>
            public bool IsCarried => !Mounted && !NotHeld && Hold != WeaponHold.Unarmed;
        }

        private static readonly Row[] s_rows =
        {
            // XM177E2: 0.76 m overall (stock collapsed), muzzle ~0.47 m ahead of the grip, handguard centre ~0.24 m.
            new Row(WeaponIds.Commando, WeaponHold.Rifle, HandPose.RifleGrip, new Vector3(0f, 0.045f, 0.47f), new Vector3(0f, 0.01f, 0.25f), 0.27f),
            // AKM: 0.88 m, muzzle ~0.57 m, handguard ~0.28 m.
            new Row(WeaponIds.AK, WeaponHold.Rifle, HandPose.RifleGrip, new Vector3(0f, 0.04f, 0.57f), new Vector3(0f, 0.0f, 0.28f), 0.31f),
            // RPK: 1.04 m, longer barrel, the support hand sits further out on the handguard.
            new Row(WeaponIds.RPK, WeaponHold.Rifle, HandPose.RifleGrip, new Vector3(0f, 0.04f, 0.73f), new Vector3(0f, 0.0f, 0.31f), 0.31f),
            // AKMS (stock folded): like the AKM forward of the grip.
            new Row(WeaponIds.AKMS, WeaponHold.Rifle, HandPose.RifleGrip, new Vector3(0f, 0.04f, 0.57f), new Vector3(0f, 0.0f, 0.28f), 0.22f),
            // SVD: 1.22 m, muzzle ~0.80 m.
            new Row(WeaponIds.SVD, WeaponHold.Rifle, HandPose.RifleGrip, new Vector3(0f, 0.05f, 0.80f), new Vector3(0f, 0.01f, 0.33f), 0.40f),
            // RPG-7: tube over the shoulder, trigger grip below it; front of the tube ~0.55 m, support on the forward grip.
            new Row(WeaponIds.RPG7, WeaponHold.Launcher, HandPose.RifleGrip, new Vector3(0f, 0.13f, 0.55f), new Vector3(0f, 0.02f, 0.20f), 0.40f),
            // Mk 22 pistol: muzzle ~0.18 m (suppressor), support hand wraps the grip from the left.
            new Row(WeaponIds.HushPuppy, WeaponHold.Pistol, HandPose.PistolGrip, new Vector3(0f, 0.06f, 0.30f), new Vector3(-0.02f, -0.01f, -0.005f), 0.03f, -70f),
            // Mounted guns: the vehicle/mount owns the visual.
            new Row(WeaponIds.DShK, WeaponHold.Rifle, HandPose.RifleGrip, new Vector3(0f, 0f, 1.1f), new Vector3(0f, 0f, 0.3f), 0.3f, mounted: true),
            new Row(WeaponIds.DoorGun, WeaponHold.Rifle, HandPose.RifleGrip, new Vector3(0f, 0f, 0.9f), new Vector3(0f, 0f, 0.3f), 0.3f, mounted: true),
            // Not carried: grenades and knife are spawned by WP03 when used.
            new Row(WeaponIds.Frag, WeaponHold.Unarmed, HandPose.Relaxed, Vector3.zero, Vector3.zero, 0f, notHeld: true),
            new Row(WeaponIds.Smoke, WeaponHold.Unarmed, HandPose.Relaxed, Vector3.zero, Vector3.zero, 0f, notHeld: true),
            new Row(WeaponIds.Knife, WeaponHold.Unarmed, HandPose.Relaxed, Vector3.zero, Vector3.zero, 0f, notHeld: true),
        };

        /// <summary>Every row.</summary>
        public static Row[] All => s_rows;

        /// <summary>Row for a weapon id; unknown ids get rifle defaults (logged once by the caller).</summary>
        public static bool TryGet(string weaponId, out Row row)
        {
            for (var i = 0; i < s_rows.Length; i++)
                if (s_rows[i].Id == weaponId)
                {
                    row = s_rows[i];
                    return true;
                }
            row = new Row(weaponId, WeaponHold.Rifle, HandPose.RifleGrip, new Vector3(0f, 0.04f, 0.55f), new Vector3(0f, 0f, 0.27f), 0.3f);
            return false;
        }
    }
}
