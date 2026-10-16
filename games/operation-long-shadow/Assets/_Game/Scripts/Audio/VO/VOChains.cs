using LongShadow.Core;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// The offline VO chains (GDD §10.1; folder names under <c>Assets/_Game/Audio/VO/</c>, ASSET_MANIFEST <c>variants</c>)
    /// and how each one is played back.
    /// </summary>
    public static class VOChains
    {
        public const string Dry = "DRY";
        public const string Radio = "RADIO";
        public const string Intercom = "INTERCOM";
        public const string Scrambler = "SCRAMBLER";
        public const string Tape = "TAPE";
        public const string Clean = "CLEAN";
        public const string PAHorn = "PA_HORN";
        public const string HfAm = "HF_AM";
        public const string RoomSpeaker = "ROOM_SPEAKER";
        public const string GreycoatSquelch = "GREYCOAT_SQUELCH";
        public const string Booth = "BOOTH";
        public const string Far = "FAR";

        /// <summary>Every chain (import order).</summary>
        public static readonly string[] All =
        {
            Dry, Radio, Intercom, Scrambler, Tape, Clean, PAHorn, HfAm, RoomSpeaker, GreycoatSquelch, Booth, Far,
        };

        /// <summary>Radio-type chains: heard on a headset/handset (2D) unless an enemy's own radio (barks).</summary>
        public static bool IsRadio(string chain) =>
            chain == Radio || chain == Intercom || chain == HfAm || chain == GreycoatSquelch;

        /// <summary>Booth chains (the interrogation room).</summary>
        public static bool IsBooth(string chain) => chain == Scrambler || chain == Booth || chain == Tape || chain == Clean;

        /// <summary>True for a known chain name.</summary>
        public static bool IsKnown(string chain)
        {
            for (var i = 0; i < All.Length; i++)
                if (All[i] == chain) return true;
            return false;
        }

        /// <summary>
        /// Channel of a line from its speaker and primary chain: booth chains → Booth, the Clockmaker and Harrou →
        /// Broadcast, enemy speakers → Bark, everyone else → Net.
        /// </summary>
        public static VOChannel ChannelOf(string speaker, string primaryChain)
        {
            if (IsBooth(primaryChain)) return VOChannel.Booth;
            if (speaker == SpeakerIds.Clockmaker || speaker == SpeakerIds.Harrou || primaryChain == PAHorn) return VOChannel.Broadcast;
            if (SpeakerIds.IsEnemy(speaker)) return VOChannel.Bark;
            return VOChannel.Net;
        }
    }
}
