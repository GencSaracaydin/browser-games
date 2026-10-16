using System;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// The audio import table of TECH_PLAN §3.12 (pure function; applied by <see cref="AudioImportRules"/>). VO folder
    /// names are the chain names of ASSET_MANIFEST <c>variants</c>. An SFX group that is not in the table imports as a
    /// one-shot (and validate_assets.py reports the unknown folder).
    /// </summary>
    public static class AudioRules
    {
        /// <summary>One-shot SFX groups: mono, Decompress On Load, preload, q70.</summary>
        public static readonly string[] OneShotGroups = { "Weapons", "Impacts", "Foley", "Footsteps", "Explosions", "Whizz", "Synth" };
        /// <summary>Loop SFX groups: mono, Compressed In Memory, preload, q60.</summary>
        public static readonly string[] LoopGroups = { "SetPieces", "Vehicles" };
        /// <summary>VO chains imported at 22050 Hz, q60 (band-limited radio-style chains).</summary>
        public static readonly string[] NarrowVoChains = { "RADIO", "INTERCOM", "HF_AM", "GREYCOAT_SQUELCH", "PA_HORN" };
        /// <summary>VO chains imported at 24000 Hz, q70.</summary>
        public static readonly string[] WideVoChains = { "DRY", "FAR", "SCRAMBLER", "TAPE", "CLEAN", "BOOTH", "ROOM_SPEAKER" };

        /// <summary>Resolves the rule for a project path, or null when the rules do not manage it.</summary>
        public static AudioRule Resolve(string assetPath)
        {
            var rel = ImportRulePaths.ToRuleRelative(assetPath);
            if (rel == null || !ImportRulePaths.Under(rel, "Audio/")) return null;

            if (ImportRulePaths.Under(rel, "Audio/SFX/"))
            {
                var group = ImportRulePaths.FolderBelow(rel, "Audio/SFX/") ?? "";
                if (In(OneShotGroups, group))
                    return new AudioRule { Name = "SFX/" + group, Mono = true, LoadType = AudioClipLoadType.DecompressOnLoad, Preload = true, Quality = 70 };
                if (In(LoopGroups, group))
                    return new AudioRule { Name = "SFX/" + group, Mono = true, LoadType = AudioClipLoadType.CompressedInMemory, Preload = true, Quality = 60 };
                if (string.Equals(group, "UI", StringComparison.OrdinalIgnoreCase))
                    return new AudioRule { Name = "SFX/UI", Mono = true, LoadType = AudioClipLoadType.DecompressOnLoad, Preload = true, Quality = 60, SampleRate = 22050 };
                return new AudioRule { Name = "SFX/? (" + group + ")", Mono = true, LoadType = AudioClipLoadType.DecompressOnLoad, Preload = true, Quality = 70 };
            }
            if (ImportRulePaths.Under(rel, "Audio/Ambience/"))
                return new AudioRule { Name = "Ambience", Mono = false, LoadType = AudioClipLoadType.CompressedInMemory, Preload = false, LoadInBackground = true, Quality = 50 };
            if (ImportRulePaths.Under(rel, "Audio/Music/"))
                return new AudioRule { Name = "Music", Mono = false, LoadType = AudioClipLoadType.CompressedInMemory, Preload = false, LoadInBackground = true, Quality = 60 };
            if (ImportRulePaths.Under(rel, "Audio/VO/"))
            {
                var chain = ImportRulePaths.FolderBelow(rel, "Audio/VO/") ?? "";
                if (In(NarrowVoChains, chain))
                    return new AudioRule { Name = "VO/" + chain, Mono = true, LoadType = AudioClipLoadType.CompressedInMemory, Preload = false, LoadInBackground = true, Quality = 60, SampleRate = 22050 };
                return new AudioRule
                {
                    Name = "VO/" + (In(WideVoChains, chain) ? chain : "? (" + chain + ")"), Mono = true,
                    LoadType = AudioClipLoadType.CompressedInMemory, Preload = false, LoadInBackground = true, Quality = 70, SampleRate = 24000,
                };
            }
            return null;
        }

        private static bool In(string[] list, string name)
        {
            foreach (var s in list)
                if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
