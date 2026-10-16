using System;
using UnityEngine;

namespace LongShadow.Characters.Weapons
{
    /// <summary>
    /// Finds the sockets of a world weapon prefab by name. WP03's art pipeline exports <c>Socket_Muzzle</c>,
    /// <c>Socket_Grip</c> (origin of the weapon = the pistol grip) and <c>Socket_LeftHand</c> (support hand) with the
    /// barrel along +Z and the top of the weapon along +Y (WeaponGreybox / WeaponSockets in LongShadow.Combat); the
    /// generic names <c>Muzzle</c>, <c>RightGrip</c>, <c>LeftGrip</c> (as in <c>IWeaponGrips</c>) and common alternatives
    /// are accepted too. List order wins. Mesh names such as "Grip" or "Handguard" (WP03 greybox parts) are deliberately not
    /// accepted: without a grip socket the prefab origin is the grip (WP03 convention).
    /// </summary>
    public static class WeaponSockets
    {
        public static readonly string[] MuzzleNames = { "Socket_Muzzle", "Muzzle", "MuzzlePoint", "muzzle", "Barrel_End" };
        public static readonly string[] RightGripNames = { "Socket_Grip", "Socket_RightGrip", "RightGrip", "Grip_R" };
        public static readonly string[] LeftGripNames =
            { "Socket_LeftHand", "Socket_LeftGrip", "LeftGrip", "LeftHand", "Grip_L", "Foregrip", "SupportGrip" };

        /// <summary>First descendant (or self) whose name matches one of <paramref name="names"/> (case-insensitive, list order wins).</summary>
        public static Transform Find(Transform root, string[] names)
        {
            if (root == null || names == null) return null;
            for (var n = 0; n < names.Length; n++)
            {
                var t = FindDeep(root, names[n]);
                if (t != null) return t;
            }
            return null;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase)) return t;
            for (var i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
