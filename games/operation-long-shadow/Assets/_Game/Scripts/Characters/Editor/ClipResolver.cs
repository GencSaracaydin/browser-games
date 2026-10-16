using System;
using System.Collections.Generic;
using LongShadow.Core;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// Finds the clips of <see cref="ClipCatalog"/> in the project: every AnimationClip imported from FBX files under
    /// Art/Animation/Clips (ClipImporter names), then the authored finger poses (Art/Animation/Poses), by clip name.
    /// Resolves a slot to its first candidate that exists (role variants first), else to a generated stand-in.
    /// </summary>
    public sealed class ClipResolver
    {
        private readonly Dictionary<string, AnimationClip> _byName = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _sourceOf = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Number of clips indexed.</summary>
        public int Count => _byName.Count;

        /// <summary>Indexes the project's clips (FBX sub-assets first, then .anim files; first name wins).</summary>
        public static ClipResolver Scan()
        {
            var r = new ClipResolver();
            if (AssetDatabase.IsValidFolder(CharacterPaths.ClipsRoot))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CharacterPaths.ClipsRoot }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                        if (o is AnimationClip c) r.Add(c, path);
                }
            }
            foreach (var folder in new[] { CharacterPaths.PosesFolder, CharacterPaths.ClipsRoot })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) continue;
                    var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (c != null) r.Add(c, path);
                }
            }
            return r;
        }

        /// <summary>Adds a clip (tests, manual registration). Preview clips and duplicates are ignored.</summary>
        public void Add(AnimationClip clip, string path)
        {
            if (clip == null || clip.name.StartsWith("__preview__", StringComparison.Ordinal) || _byName.ContainsKey(clip.name)) return;
            _byName.Add(clip.name, clip);
            _sourceOf[clip.name] = path;
        }

        /// <summary>A clip by exact name, or null.</summary>
        public AnimationClip Find(string clipName) => clipName != null && _byName.TryGetValue(clipName, out var c) ? c : null;

        /// <summary>Asset path a clip came from (null if unknown).</summary>
        public string PathOf(string clipName) => clipName != null && _sourceOf.TryGetValue(clipName, out var p) ? p : null;

        /// <summary>First existing candidate of a slot for a role (role variants first), or null.</summary>
        public AnimationClip Resolve(string key, ControllerRole role = ControllerRole.Soldier)
        {
            var variant = ClipCatalog.VariantFor(role, key);
            if (variant != null)
                foreach (var n in variant)
                {
                    var c = Find(n);
                    if (c != null) return c;
                }
            foreach (var n in ClipCatalog.CandidatesFor(key))
            {
                var c = Find(n);
                if (c != null) return c;
            }
            return null;
        }

        /// <summary>
        /// The slot's clip, or the generated stand-in (logged once per slot) when nothing has landed.
        /// <paramref name="placeholder"/> tells which.
        /// </summary>
        public AnimationClip ResolveOrPlaceholder(string key, ControllerRole role, out bool placeholder)
        {
            var c = Resolve(key, role);
            placeholder = c == null;
            if (c != null) return c;
            LSLog.WarnOnce("clip-missing:" + key, LogCategory.Characters,
                "ClipResolver: no clip for slot " + key + " (candidates: " + string.Join(", ", ClipCatalog.CandidatesFor(key)) +
                "); using generated stand-in PH_" + ClipCatalog.PlaceholderFor(key));
            return ProceduralClipFactory.GetOrCreate(ClipCatalog.PlaceholderFor(key));
        }
    }
}
