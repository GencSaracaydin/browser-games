using System.Collections.Generic;
using LongShadow.Core.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Editor
{
    /// <summary>
    /// Fills LIB_Cinematics (Assets/_Game/Data/Libraries/LIB_Cinematics.asset, TECH_PLAN D12): timelines, FX materials,
    /// volume profiles, CRT/intel images, the insertion cast prefabs (WP05 bodies, WP10 helicopter and rotors) and the
    /// TMP fonts. Entries whose asset has not landed are written with a null asset and a note, and listed in the report;
    /// re-run after content lands.
    /// </summary>
    public static class CinematicLibraryBuilder
    {
        /// <summary>Builds the library; returns the number of missing entries.</summary>
        public static int Build(List<string> log)
        {
            var lib = BuilderUtil.LoadOrCreateAsset<CinematicLibrary>(CinematicLibrary.AssetPath);
            var entries = new List<CinematicAssetEntry>();
            var missing = 0;

            void Add<T>(string id, string path) where T : Object
            {
                var a = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
                entries.Add(new CinematicAssetEntry(id, a, path));
                if (a == null)
                {
                    missing++;
                    log.Add("missing: " + id + " (" + (path ?? "no path") + ")");
                }
            }

            // Timelines.
            foreach (var id in new[] { CinematicAssetIds.TimelineIntro, CinematicAssetIds.TimelineTitleCard, CinematicAssetIds.TimelineMidB,
                                       CinematicAssetIds.TimelineClimax, CinematicAssetIds.TimelineOutro })
                Add<TimelineAsset>(id, CinematicPaths.Timelines + "/" + id + ".playable");

            // Materials.
            foreach (var id in new[] { CinematicAssetIds.MatCrt, CinematicAssetIds.MatGunCam, CinematicAssetIds.MatFilm, CinematicAssetIds.MatFilmBurn,
                                       CinematicAssetIds.MatGlitch, CinematicAssetIds.MatGlitchOverlay, CinematicAssetIds.MatFilmBeam, CinematicAssetIds.MatSmoke })
                Add<Material>(id, CinematicPaths.FxMaterials + "/" + id + ".mat");

            // Profiles.
            foreach (var id in new[] { CinematicAssetIds.ProfileDeath, CinematicAssetIds.ProfileLowHealth, CinematicAssetIds.ProfileSuppression,
                                       CinematicAssetIds.ProfileFlash, CinematicAssetIds.ProfileCinematic })
                Add<VolumeProfile>(id, CinematicPaths.FxProfiles + "/" + id + ".asset");
            Add<VolumeProfile>(CinematicAssetIds.ProfileBooth,
                AssetDatabase.LoadAssetAtPath<VolumeProfile>(CinematicAssetsBuilder.BoothProfile) != null
                    ? CinematicAssetsBuilder.BoothProfile
                    : CinematicAssetsBuilder.BoothFallbackProfile);

            // Images (WP09_ART paths).
            Add<Texture>(CinematicAssetIds.ImgMap, CinematicPaths.ImgMap);
            Add<Texture>(CinematicAssetIds.ImgRecon, CinematicPaths.ImgRecon);
            Add<Texture>(CinematicAssetIds.ImgHarrou, CinematicPaths.ImgHarrou);
            Add<Texture>(CinematicAssetIds.ImgSurveil, CinematicPaths.ImgSurveil);
            Add<Texture>(CinematicAssetIds.ImgTeletype, CinematicPaths.ImgTeletype);
            Add<Texture>(CinematicAssetIds.ImgRoster, CinematicPaths.ImgRoster);
            Add<Texture>(CinematicAssetIds.ImgCodebookPage, CinematicPaths.ImgCodebook);
            Add<Texture>(CinematicAssetIds.ImgTeamHollis, CinematicPaths.ImgTeamHollis);
            Add<Texture>(CinematicAssetIds.ImgTeamSparrow, CinematicPaths.ImgTeamSparrow);
            Add<Texture>(CinematicAssetIds.ImgTeamTinker, CinematicPaths.ImgTeamTinker);
            Add<Texture>(CinematicAssetIds.ImgLeader5, CinematicPaths.ImgLeader5);
            Add<Texture>(CinematicAssetIds.ImgLeader4, CinematicPaths.ImgLeader4);
            Add<Texture>(CinematicAssetIds.ImgLeader3, CinematicPaths.ImgLeader3);

            // Cast.
            foreach (var id in new[] { CinematicAssetIds.PrefabHollis, CinematicAssetIds.PrefabSparrow, CinematicAssetIds.PrefabTinker,
                                       CinematicAssetIds.PrefabCalder, CinematicAssetIds.PrefabCalderShadow })
                Add<GameObject>(id, CinematicPaths.Bodies + "/" + id + ".prefab");
            Add<GameObject>(CinematicAssetIds.PrefabHelicopter, FindPrefab(CinematicPaths.VehiclePrefabs, "Heli", "Cabin") ?? CinematicPaths.ModelHelicopter);
            Add<GameObject>(CinematicAssetIds.PrefabHeliCabin, FindPrefab(CinematicPaths.VehiclePrefabs, "HeliCabin", null));
            Add<GameObject>(CinematicAssetIds.ModelHeliMainRotor, CinematicPaths.ModelHeliMainRotor);
            Add<GameObject>(CinematicAssetIds.ModelHeliTailRotor, CinematicPaths.ModelHeliTailRotor);
            Add<GameObject>(CinematicAssetIds.PrefabRotorDownwash, CinematicPaths.FxPrefabs + "/" + CinematicAssetIds.PrefabRotorDownwash + ".prefab");
            Add<GameObject>(CinematicAssetIds.PrefabRotorShadow, CinematicPaths.FxPrefabs + "/" + CinematicAssetIds.PrefabRotorShadow + ".prefab");

            // Fonts.
            Add<TMP_FontAsset>(CinematicAssetIds.FontTypewriter, FindFont("SpecialElite", "CourierPrime"));
            Add<TMP_FontAsset>(CinematicAssetIds.FontMono, FindFont("ShareTechMono", "IBMPlexMono"));

            lib.EditorSetEntries(entries);
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            log.Insert(0, "LIB_Cinematics: " + entries.Count + " entries, " + missing + " missing");
            return missing;
        }

        private static string FindPrefab(string folder, string nameContains, string exclude)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var n = System.IO.Path.GetFileNameWithoutExtension(p);
                if (n.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (exclude != null && n.IndexOf(exclude, System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                return p;
            }
            return null;
        }

        private static string FindFont(params string[] names)
        {
            if (!AssetDatabase.IsValidFolder(CinematicPaths.Fonts)) return null;
            foreach (var want in names)
                foreach (var g in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { CinematicPaths.Fonts }))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (System.IO.Path.GetFileName(p).IndexOf(want, System.StringComparison.OrdinalIgnoreCase) >= 0) return p;
                }
            return null;
        }
    }
}
