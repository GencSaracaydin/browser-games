using System.IO;
using LongShadow.Combat.Data;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Combat.Editor
{
    /// <summary>
    /// Finds the weapon art produced by WP03_ART (RawAssets/wp03/ready → Assets/_Game/Art/Models/Weapons/&lt;Folder&gt;/):
    /// <c>SM_WPN_&lt;Name&gt;_FP.fbx</c> (body + <c>Mag</c> child pivoted at the well + <c>Socket_*</c> empties),
    /// <c>SM_WPN_&lt;Name&gt;_W.fbx</c> (or <c>_World</c>; LOD0-2 + Muzzle/LeftHand sockets) and
    /// <c>SM_WPN_&lt;Name&gt;_Mag.fbx</c> (stand-alone magazine, origin at the well entry). OBJ files are accepted too.
    /// Every lookup returns null when the file has not landed (the builders fall back to greybox).
    /// </summary>
    public static class WeaponArt
    {
        /// <summary>Root of the weapon models.</summary>
        public const string ModelsRoot = "Assets/_Game/Art/Models/Weapons";
        /// <summary>Root of the weapon textures.</summary>
        public const string TexturesRoot = "Assets/_Game/Art/Textures/Weapons";

        /// <summary>Art folder of a weapon id (Tools/mesh/weapons/weapons.json "folder").</summary>
        public static string Folder(string weaponId)
        {
            switch (weaponId)
            {
                case WeaponIds.Smoke: return "Smoke";
                case WeaponIds.DoorGun: return "M60D";
                default: return WeaponTable.ArtName(weaponId);
            }
        }

        /// <summary>File stem name of a weapon id (weapons.json "name": HushPuppy, M60D, SmokeGrenade ...).</summary>
        public static string Name(string weaponId) => WeaponTable.ArtName(weaponId);

        /// <summary>The first-person model (null until it lands).</summary>
        public static GameObject Fp(string weaponId) => Load(weaponId, "_FP");

        /// <summary>The world model (null until it lands).</summary>
        public static GameObject World(string weaponId) => Load(weaponId, "_W") ?? Load(weaponId, "_World");

        /// <summary>The stand-alone magazine (null until it lands).</summary>
        public static GameObject Magazine(string weaponId) => Load(weaponId, "_Mag");

        /// <summary>A model by explicit stem in a weapon's folder ("SM_WPN_RPG7_Rocket_W").</summary>
        public static GameObject LoadStem(string folder, string stem)
        {
            foreach (var ext in new[] { ".fbx", ".FBX", ".obj" })
            {
                var p = ModelsRoot + "/" + folder + "/" + stem + ext;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) return go;
            }
            return null;
        }

        /// <summary>Asset path of a loaded model (for logs).</summary>
        public static string PathOf(Object o) => o != null ? AssetDatabase.GetAssetPath(o) : "(missing)";

        private static GameObject Load(string weaponId, string suffix)
        {
            var folder = Folder(weaponId);
            var go = LoadStem(folder, "SM_WPN_" + Name(weaponId) + suffix);
            if (go != null) return go;
            // Any model in the folder whose name ends with the suffix (art renamed by hand).
            var dir = ModelsRoot + "/" + folder;
            if (!AssetDatabase.IsValidFolder(dir)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { dir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var stem = Path.GetFileNameWithoutExtension(p);
                if (stem.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase)) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            return null;
        }

        /// <summary>Total triangles of every mesh under a model or prefab.</summary>
        public static int Triangles(GameObject go)
        {
            if (go == null) return 0;
            var tris = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) tris += Triangles(mf.sharedMesh);
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) tris += Triangles(smr.sharedMesh);
            return tris;
        }

        /// <summary>Triangles of a mesh (all sub-meshes).</summary>
        public static int Triangles(Mesh m)
        {
            if (m == null) return 0;
            long n = 0;
            for (var i = 0; i < m.subMeshCount; i++) n += m.GetIndexCount(i) / 3;
            return (int)n;
        }

        /// <summary>A placeholder material asset of a colour (BuilderUtil, saved so prefabs can reference it).</summary>
        public static Material Placeholder(Color c) => BuilderUtil.PlaceholderMaterial(c);
    }
}
