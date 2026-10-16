namespace LongShadow.Audio.Core
{
    /// <summary>
    /// Voice cap with priority stealing (TECH_PLAN §7.9: 24 audible voices). When the cap is reached a new sound may
    /// replace the least important voice: lower priority first, then the quietest at the listener, then the oldest. A
    /// voice of higher priority is never stolen, and among equal priorities only a voice that is not louder than the
    /// new sound. Pure function over a caller-owned array (no allocation).
    /// </summary>
    public static class VoiceStealing
    {
        /// <summary>Number of active voices in the first <paramref name="count"/> entries.</summary>
        public static int ActiveCount(VoiceInfo[] voices, int count)
        {
            var n = 0;
            for (var i = 0; i < count; i++)
                if (voices[i].Active) n++;
            return n;
        }

        /// <summary>
        /// Index of the voice to stop so a new sound of <paramref name="newPriority"/> and
        /// <paramref name="newAudibility"/> can play, or -1 to reject the new sound.
        /// </summary>
        public static int PickVictim(VoiceInfo[] voices, int count, int newPriority, float newAudibility)
        {
            var best = -1;
            for (var i = 0; i < count; i++)
            {
                var v = voices[i];
                if (!v.Active || v.Protected) continue;
                if (v.Priority > newPriority) continue;
                if (v.Priority == newPriority && v.Audibility > newAudibility) continue;
                if (best < 0 || Worse(v, voices[best])) best = i;
            }
            return best;
        }

        private static bool Worse(VoiceInfo a, VoiceInfo b)
        {
            if (a.Priority != b.Priority) return a.Priority < b.Priority;
            if (a.Audibility != b.Audibility) return a.Audibility < b.Audibility;
            return a.StartedAt < b.StartedAt;
        }
    }
}
