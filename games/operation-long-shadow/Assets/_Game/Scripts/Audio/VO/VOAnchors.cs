using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// Where non-squad voices come from (TECH_PLAN Appendix A marker ids, with the GDD coordinates as fallbacks while
    /// the level markup is missing): Harrou's three PA horns (GDD §7.1), the basement room speaker (Beat 9), and the
    /// booth's reel-to-reel and scrambler box (GDD §8.0). Booth fallbacks are none: without the booth markers those
    /// lines play 2D.
    /// </summary>
    public static class VOAnchors
    {
        /// <summary>PA horn marker ids.</summary>
        public static readonly string[] Horns = { "PA.Horn1", "PA.Horn2", "PA.Horn3" };

        /// <summary>GDD §7.1 horn positions (fallback).</summary>
        public static readonly Vector3[] HornFallbacks =
        {
            new Vector3(185f, 8f, 200f), new Vector3(220f, 20f, 275f), new Vector3(346f, 6f, 294f),
        };

        /// <summary>Basement room speaker (Appendix A B9).</summary>
        public const string RoomSpeaker = "B9.Speaker";
        /// <summary>Appendix A position of <see cref="RoomSpeaker"/> (fallback).</summary>
        public static readonly Vector3 RoomSpeakerFallback = new Vector3(331.5f, -3.4f, 357f);
        /// <summary>The booth's reel-to-reel (TAPE chain).</summary>
        public const string BoothReel = "BOOTH.Reel";
        /// <summary>The booth's scrambler box (SCRAMBLER chain).</summary>
        public const string BoothScrambler = "BOOTH.Scrambler";

        /// <summary>Marker id a chain's 3D source sits at, or null.</summary>
        public static string AnchorOf(string chain)
        {
            switch (chain)
            {
                case VOChains.RoomSpeaker: return RoomSpeaker;
                case VOChains.Tape: return BoothReel;
                case VOChains.Scrambler: return BoothScrambler;
                default: return null;
            }
        }

        /// <summary>Resolves an anchor (marker first, then the GDD fallback); false when neither exists.</summary>
        public static bool TryResolve(string anchorId, out Vector3 position)
        {
            position = default;
            if (string.IsNullOrEmpty(anchorId)) return false;
            if (MarkerRegistry.Instance.TryGet(anchorId, out SceneMarker m))
            {
                position = m.transform.position;
                return true;
            }
            if (anchorId == RoomSpeaker)
            {
                position = RoomSpeakerFallback;
                return true;
            }
            for (var i = 0; i < Horns.Length; i++)
            {
                if (Horns[i] != anchorId) continue;
                position = HornFallbacks[i];
                return true;
            }
            return false;
        }
    }
}
