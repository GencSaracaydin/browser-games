using System;
using System.Collections.Generic;
using LongShadow.Characters.Weapons;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// Builds Assets/_Game/Data/Libraries/LIB_CharacterWeapons.asset (TECH_PLAN D12): for every carried weapon id of
    /// <see cref="WeaponHoldTable"/>, WP03's world prefab Prefabs/Weapons/PF_W_&lt;Id&gt; (sockets Socket_Grip,
    /// Socket_LeftHand, Socket_Muzzle) or, before it exists, nothing (BodyWeaponHolder builds a greybox gun). Hand-tuned
    /// grip corrections of existing entries are kept. Register the asset in S_Persistent's LibraryRegistry (WP11).
    /// </summary>
    public static class CharacterWeaponLibraryBuilder
    {
        /// <summary>Builds the library and returns it.</summary>
        public static CharacterWeaponLibrary Build()
        {
            var lib = BuilderUtil.LoadOrCreateAsset<CharacterWeaponLibrary>(CharacterWeaponLibrary.AssetPath);
            var entries = new List<CharacterWeaponEntry>();
            var lines = new List<string>();
            foreach (var row in WeaponHoldTable.All)
            {
                if (!row.IsCarried) continue;
                var prefab = FindWorldPrefab(row.Id);
                if (lib.TryGet(row.Id, out var existing))
                {
                    existing.EditorSetPrefab(prefab);
                    entries.Add(existing);
                }
                else entries.Add(new CharacterWeaponEntry(row.Id, prefab, row.Hold, row.HandPose));
                lines.Add(row.Id + ": " + (prefab != null ? AssetDatabase.GetAssetPath(prefab) : "(no PF_W_ prefab yet: greybox at runtime)"));
            }
            lib.EditorSetEntries(entries);
            AssetDatabase.SaveAssets();
            BodyPrefabBuilder.Write("Logs/characters_weapons.txt", lines);
            LSLog.Info(LogCategory.Characters, "CharacterWeaponLibraryBuilder: " + entries.Count + " weapon(s) in " + CharacterWeaponLibrary.AssetPath);
            return lib;
        }

        /// <summary>WP03's world prefab for a weapon id (PF_W_&lt;Id&gt;, case-insensitive), or null.</summary>
        public static GameObject FindWorldPrefab(string weaponId)
        {
            if (!AssetDatabase.IsValidFolder(CharacterPaths.WeaponPrefabsFolder)) return null;
            var want = "PF_W_" + weaponId;
            foreach (var guid in AssetDatabase.FindAssets("PF_W_ t:Prefab", new[] { CharacterPaths.WeaponPrefabsFolder }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(p), want, StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            return null;
        }
    }
}
