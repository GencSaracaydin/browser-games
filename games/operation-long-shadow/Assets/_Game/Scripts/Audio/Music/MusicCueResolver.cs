using System;

namespace LongShadow.Audio.Music
{
    /// <summary>
    /// Maps music cue ids to their layer clips (GDD §10.3): a base id with layers (<c>MUS_02_MARKET</c> →
    /// <c>_LO</c>/<c>_HI</c>, <c>MUS_04_STATION</c> → <c>_LOW</c>/<c>_FULL</c>) or a single cue (<c>MUS_01_SESSION</c>).
    /// A layer id given directly (<c>MUS_02_MARKET_HI</c>) resolves to its base and layer index. Pure (unit-tested).
    /// </summary>
    public static class MusicCueResolver
    {
        /// <summary>Layer suffix pairs, layer 0 first.</summary>
        public static readonly string[][] LayerSuffixes =
        {
            new[] { "_LO", "_HI" },
            new[] { "_LOW", "_FULL" },
        };

        /// <summary>
        /// Resolves <paramref name="cueId"/> with <paramref name="exists"/> (library lookup). <paramref name="layerIds"/>
        /// gets the layer cue ids in order; <paramref name="baseId"/> the id reported as <c>CurrentCue</c>;
        /// <paramref name="layerHint"/> the layer index named by a layer id (-1 if none). False when nothing matches.
        /// </summary>
        public static bool Resolve(string cueId, Func<string, bool> exists, out string baseId, out string[] layerIds, out int layerHint)
        {
            baseId = cueId;
            layerIds = null;
            layerHint = -1;
            if (string.IsNullOrEmpty(cueId) || exists == null) return false;

            // Base id with layers?
            for (var s = 0; s < LayerSuffixes.Length; s++)
            {
                var pair = LayerSuffixes[s];
                if (!exists(cueId + pair[0])) continue;
                layerIds = exists(cueId + pair[1]) ? new[] { cueId + pair[0], cueId + pair[1] } : new[] { cueId + pair[0] };
                return true;
            }
            // A layer id?
            for (var s = 0; s < LayerSuffixes.Length; s++)
            {
                var pair = LayerSuffixes[s];
                for (var l = 0; l < pair.Length; l++)
                {
                    if (!cueId.EndsWith(pair[l], StringComparison.Ordinal)) continue;
                    var b = cueId.Substring(0, cueId.Length - pair[l].Length);
                    if (!exists(b + pair[0]) && !exists(b + pair[1])) continue;
                    baseId = b;
                    layerIds = exists(b + pair[1]) ? new[] { b + pair[0], b + pair[1] } : new[] { b + pair[0] };
                    layerHint = Math.Min(l, layerIds.Length - 1);
                    return true;
                }
            }
            if (exists(cueId))
            {
                layerIds = new[] { cueId };
                return true;
            }
            return false;
        }
    }
}
