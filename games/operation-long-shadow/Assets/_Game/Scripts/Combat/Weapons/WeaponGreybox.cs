using System;
using LongShadow.Combat.Data;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Weapons
{
    /// <summary>
    /// Greybox stand-in of a weapon built from its socket layout (same convention as the art: barrel along +Z, origin at
    /// the grip, <c>Socket_*</c> children, a separate <c>Mag</c> child pivoted at the magazine well), so view-model motion,
    /// reload choreography and IK grips work before the models land. Materials come from a provider so the editor builder
    /// can use saved placeholder materials and runtime code shared ones.
    /// </summary>
    public static class WeaponGreybox
    {
        /// <summary>Gunmetal.</summary>
        public static readonly Color Metal = new Color(0.16f, 0.16f, 0.17f);
        /// <summary>Furniture (wood/polymer).</summary>
        public static readonly Color Furniture = new Color(0.32f, 0.22f, 0.14f);
        /// <summary>Magazine.</summary>
        public static readonly Color MagColor = new Color(0.22f, 0.21f, 0.2f);

        /// <summary>Socket names exported by the art pipeline (bl_weapon.py).</summary>
        public static readonly string[] SocketNames = { "Muzzle", "SightFront", "SightRear", "Grip", "LeftHand", "Eject", "MagWell" };

        /// <summary>Builds a greybox for a weapon id (player weapons from their table sockets, AI-only weapons by length).</summary>
        public static GameObject Build(string weaponId, Func<Color, Material> material, bool withMagazine = true)
        {
            var w = WeaponTable.Get(weaponId);
            var sockets = w?.Viewmodel != null ? w.Viewmodel.Sockets : SocketsFor(weaponId);
            var kind = w?.Kind ?? KindFor(weaponId);
            return Build("GB_" + weaponId, sockets, kind, material, withMagazine);
        }

        /// <summary>Builds a greybox from sockets.</summary>
        public static GameObject Build(string name, WeaponSockets s, WeaponKind kind, Func<Color, Material> material, bool withMagazine)
        {
            var root = new GameObject(name);
            var t = root.transform;
            var metal = material(Metal);
            var furn = material(Furniture);
            var magMat = material(MagColor);
            var length = Mathf.Max(0.2f, s.Muzzle.z - s.StockEnd);
            var boreY = s.Muzzle.y;

            if (kind == WeaponKind.Pistol)
            {
                Box(t, "Slide", metal, new Vector3(0f, s.SightRear.y - 0.025f, (s.StockEnd + s.SightFront.z) * 0.5f + 0.02f), new Vector3(0.028f, 0.035f, s.SightFront.z - s.StockEnd + 0.03f));
                Box(t, "Frame", metal, new Vector3(0f, s.SightRear.y - 0.055f, (s.StockEnd + s.SightFront.z) * 0.5f), new Vector3(0.026f, 0.025f, s.SightFront.z - s.StockEnd));
                Cyl(t, "Suppressor", metal, new Vector3(0f, boreY, (s.SightFront.z + s.Muzzle.z) * 0.5f), 0.034f, s.Muzzle.z - s.SightFront.z);
                var grip = Box(t, "Grip", furn, new Vector3(0f, -0.035f, -0.012f), new Vector3(0.03f, 0.1f, 0.045f));
                grip.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            }
            else
            {
                var receiverLen = Mathf.Max(0.12f, s.LeftHand.z - 0.05f + 0.1f);
                Box(t, "Receiver", metal, new Vector3(0f, boreY - 0.005f, receiverLen * 0.5f - 0.03f), new Vector3(0.05f, 0.075f, receiverLen));
                var hgStart = receiverLen - 0.03f;
                var hgEnd = Mathf.Min(s.Muzzle.z - 0.12f, hgStart + length * 0.25f);
                if (hgEnd > hgStart) Box(t, "Handguard", furn, new Vector3(0f, boreY - 0.005f, (hgStart + hgEnd) * 0.5f), new Vector3(0.055f, 0.06f, hgEnd - hgStart));
                Cyl(t, "Barrel", metal, new Vector3(0f, boreY, (hgStart + s.Muzzle.z) * 0.5f), kind == WeaponKind.HeavyMg ? 0.04f : 0.02f, s.Muzzle.z - hgStart);
                Box(t, "Stock", kind == WeaponKind.Launcher ? metal : furn, new Vector3(0f, boreY - 0.03f, s.StockEnd * 0.5f - 0.02f), new Vector3(0.042f, 0.09f, Mathf.Max(0.08f, -s.StockEnd)));
                var grip = Box(t, "Grip", furn, new Vector3(0f, -0.035f, 0f), new Vector3(0.03f, 0.09f, 0.04f));
                grip.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
                Box(t, "SightRear", metal, s.SightRear + new Vector3(0f, -0.01f, 0f), new Vector3(0.02f, 0.02f, 0.02f));
                Box(t, "SightFront", metal, s.SightFront + new Vector3(0f, -0.012f, 0f), new Vector3(0.006f, 0.024f, 0.008f));
            }

            if (withMagazine && kind != WeaponKind.MountedMg && kind != WeaponKind.Launcher && kind != WeaponKind.HeavyMg)
                BuildMagazine(t, s, kind, magMat);

            for (var i = 0; i < SocketNames.Length; i++)
            {
                var sock = new GameObject("Socket_" + SocketNames[i]).transform;
                sock.SetParent(t, false);
                sock.localPosition = SocketPosition(s, SocketNames[i]);
            }
            return root;
        }

        /// <summary>Builds just the magazine (dropped magazines) pivoted at its top.</summary>
        public static GameObject BuildMagazineOnly(string weaponId, Func<Color, Material> material)
        {
            var w = WeaponTable.Get(weaponId);
            var s = w?.Viewmodel != null ? w.Viewmodel.Sockets : SocketsFor(weaponId);
            var root = new GameObject("GB_" + weaponId + "_Mag");
            var mag = BuildMagazine(root.transform, s, w?.Kind ?? WeaponKind.Rifle, material(MagColor));
            mag.transform.localPosition = Vector3.zero;
            return root;
        }

        /// <summary>Local position of a named socket.</summary>
        public static Vector3 SocketPosition(WeaponSockets s, string name)
        {
            switch (name)
            {
                case "Muzzle": return s.Muzzle;
                case "SightFront": return s.SightFront;
                case "SightRear": return s.SightRear;
                case "Grip": return s.Grip;
                case "LeftHand": return s.LeftHand;
                case "Eject": return s.Eject;
                case "MagWell": return s.MagWell;
                default: return Vector3.zero;
            }
        }

        /// <summary>Default sockets of AI-only weapons (by real length, GDD §13.4, manifest).</summary>
        public static WeaponSockets SocketsFor(string weaponId)
        {
            switch (weaponId)
            {
                case WeaponIds.AKMS: return WeaponTable.AK.Viewmodel.Sockets;
                case WeaponIds.SVD:
                    return new WeaponSockets(new Vector3(0f, 0.06f, 0.84f), new Vector3(0f, 0.1f, 0.8f), new Vector3(0f, 0.11f, 0.22f), Vector3.zero,
                        new Vector3(0f, 0.05f, 0.36f), new Vector3(0.014f, 0.07f, 0.13f), new Vector3(0f, 0.034f, 0.15f), -0.38f);
                case WeaponIds.RPG7:
                    return new WeaponSockets(new Vector3(0f, 0.09f, 0.55f), new Vector3(0f, 0.15f, 0.2f), new Vector3(0f, 0.15f, 0.05f), Vector3.zero,
                        new Vector3(0f, 0.0f, 0.3f), new Vector3(0f, 0.09f, -0.6f), new Vector3(0f, 0.09f, 0.55f), -0.75f);
                case WeaponIds.DShK:
                    return new WeaponSockets(new Vector3(0f, 0.05f, 1.45f), new Vector3(0f, 0.12f, 1.3f), new Vector3(0f, 0.12f, 0.3f), Vector3.zero,
                        new Vector3(-0.08f, 0f, 0f), new Vector3(0.05f, 0.06f, 0.35f), new Vector3(-0.06f, 0.05f, 0.35f), -0.15f);
                case WeaponIds.Commando: return WeaponTable.Commando.Viewmodel.Sockets;
                case WeaponIds.HushPuppy: return WeaponTable.HushPuppy.Viewmodel.Sockets;
                case WeaponIds.RPK: return WeaponTable.RPK.Viewmodel.Sockets;
                case WeaponIds.DoorGun: return WeaponTable.DoorGun.Viewmodel.Sockets;
                default: return WeaponTable.AK.Viewmodel.Sockets;
            }
        }

        private static WeaponKind KindFor(string weaponId)
        {
            switch (weaponId)
            {
                case WeaponIds.SVD: return WeaponKind.Marksman;
                case WeaponIds.RPG7: return WeaponKind.Launcher;
                case WeaponIds.DShK: return WeaponKind.HeavyMg;
                default: return WeaponKind.Rifle;
            }
        }

        private static GameObject BuildMagazine(Transform parent, WeaponSockets s, WeaponKind kind, Material mat)
        {
            var pivot = new GameObject("Mag");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = s.MagWell;
            if (kind == WeaponKind.Pistol)
            {
                Box(pivot.transform, "Body", mat, new Vector3(0f, 0.045f, 0.004f), new Vector3(0.022f, 0.095f, 0.03f));
            }
            else if (kind == WeaponKind.Lmg)
            {
                Cyl(pivot.transform, "Drum", mat, new Vector3(0f, -0.075f, 0.02f), 0.13f, 0.06f).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else
            {
                var body = Box(pivot.transform, "Body", mat, new Vector3(0f, -0.075f, 0.018f), new Vector3(0.024f, 0.16f, 0.06f));
                body.transform.localRotation = Quaternion.Euler(kind == WeaponKind.Rifle ? -16f : -8f, 0f, 0f);
            }
            return pivot;
        }

        private static GameObject Box(Transform parent, string name, Material m, Vector3 pos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            Strip(go, m);
            return go;
        }

        private static GameObject Cyl(Transform parent, string name, Material m, Vector3 pos, float diameter, float length)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(diameter, Mathf.Max(0.005f, length) * 0.5f, diameter);
            Strip(go, m);
            return go;
        }

        private static void Strip(GameObject go, Material m)
        {
            var c = go.GetComponent<Collider>();
            if (c != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(c);
                else UnityEngine.Object.DestroyImmediate(c);
            }
            go.GetComponent<Renderer>().sharedMaterial = m;
        }
    }
}
