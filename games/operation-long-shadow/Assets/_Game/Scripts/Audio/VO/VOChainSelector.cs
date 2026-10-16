using LongShadow.Audio.Core;
using LongShadow.Core;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// Chooses how a line is heard (TECH_PLAN §7.10, GDD §10.1) from the clips it has, the caller's
    /// <see cref="VOOptions"/>, whether a speaker transform is known and how far it is:
    /// <list type="bullet">
    /// <item>Squad lines with DRY + RADIO: crossfade by distance; radio only when <c>radio_filter</c>, ForceRadio or no
    /// speaker (on the net); dry only with ForceDry.</item>
    /// <item>PA_HORN: the three horns. ROOM_SPEAKER / TAPE / SCRAMBLER: 3D at their anchor marker (2D without one).</item>
    /// <item>HF_AM with ForceDry and a speaker: positional switch (G9). Other radio-type chains: 2D.</item>
    /// <item>Barks: Legion DRY within 30 m and FAR beyond, voice A/B per speaker; Greycoat squelch barks in 3D at the
    /// speaker (their own radios), 2D only with ForceRadio and no speaker.</item>
    /// </list>
    /// Pure (unit-tested).
    /// </summary>
    public static class VOChainSelector
    {
        /// <summary>Dialogue 3D rolloff (conversational voice; m).</summary>
        public const float DryMinDistance = 3.5f;
        /// <summary>Dialogue 3D max distance (m).</summary>
        public const float DryMaxDistance = 60f;

        /// <summary>Builds the plan of a line.</summary>
        public static VOPlan Select(DialogueLine line, VOOptions options, bool isBark, bool hasSpeaker, float distance, int speakerKey)
        {
            var plan = new VOPlan { Mode = VOPlanMode.Silent, SpatialBlend = 1f, MinDistance = DryMinDistance, MaxDistance = DryMaxDistance };
            if (line == null) return plan;
            plan.InBooth = VOChains.IsBooth(line.PrimaryChain);
            if (!line.HasAudio)
            {
                plan.IsRadio = line.RadioFilter || options.ForceRadio;
                return plan;
            }

            if (isBark) return SelectBark(line, options, hasSpeaker, distance, speakerKey, plan);

            if (line.HasChain(VOChains.PAHorn))
            {
                plan.Mode = VOPlanMode.Horns;
                plan.Main = line.Find(VOChains.PAHorn, null);
                plan.MinDistance = AudioConstants.PAHornMinDistance;
                plan.MaxDistance = AudioConstants.PAHornMaxDistance;
                return plan;
            }

            if (line.HasChain(VOChains.Dry) && line.HasChain(VOChains.Radio))
            {
                var dry = line.Find(VOChains.Dry, null);
                var radio = line.Find(VOChains.Radio, null);
                if (options.ForceRadio || line.RadioFilter || (!hasSpeaker && !options.ForceDry))
                    return TwoD(plan, radio, true);
                if (options.ForceDry)
                    return hasSpeaker ? ThreeD(plan, dry, null, 1f, DryMinDistance, DryMaxDistance) : TwoD(plan, dry, false);
                plan.Mode = VOPlanMode.DryRadio;
                plan.Dry = dry;
                plan.Radio = radio;
                plan.IsRadio = RadioMix.RadioShare(distance) >= 0.5f;
                return plan;
            }

            var chain = line.PrimaryChain;
            if (options.ForceRadio && chain != VOChains.Radio && line.HasChain(VOChains.Radio))
                return TwoD(plan, line.Find(VOChains.Radio, null), true);
            var main = line.Find(chain, null);
            switch (chain)
            {
                case VOChains.RoomSpeaker:
                    return ThreeD(plan, main, VOAnchors.RoomSpeaker, 1f, 2f, 35f);
                case VOChains.Tape:
                    return ThreeD(plan, main, VOAnchors.BoothReel, 0.8f, 0.7f, 10f);
                case VOChains.Scrambler:
                    return ThreeD(plan, main, VOAnchors.BoothScrambler, 0.55f, 0.7f, 10f);
                case VOChains.HfAm:
                    if (options.ForceDry && hasSpeaker)
                    {
                        plan.Mode = VOPlanMode.PositionalSwitch;
                        plan.Main = main;
                        plan.SpatialBlend = 1f;
                        plan.MinDistance = 0.8f;
                        plan.MaxDistance = 25f;
                        plan.SwitchTime = options.RadioSwitchTime > 0f ? options.RadioSwitchTime : line.RadioSwitchTime;
                        plan.IsRadio = plan.SwitchTime <= 0f;
                        if (plan.SwitchTime <= 0f) plan.Mode = VOPlanMode.ThreeD;
                        return plan;
                    }
                    return TwoD(plan, main, true);
                case VOChains.Radio:
                case VOChains.Intercom:
                case VOChains.GreycoatSquelch:
                    return TwoD(plan, main, true);
                case VOChains.Dry:
                case VOChains.Far:
                    if (hasSpeaker && !options.ForceRadio) return ThreeD(plan, main, null, 1f, DryMinDistance, DryMaxDistance);
                    return TwoD(plan, main, options.ForceRadio);
                default: // BOOTH, CLEAN and anything unknown: close, 2D
                    return TwoD(plan, main, false);
            }
        }

        private static VOPlan SelectBark(DialogueLine line, VOOptions options, bool hasSpeaker, float distance, int speakerKey, VOPlan plan)
        {
            var voice = BarkRules.VoiceOf(speakerKey);
            if (line.HasChain(VOChains.Dry) || line.HasChain(VOChains.Far))
            {
                var far = BarkRules.UseFarChain(distance) && line.HasChain(VOChains.Far);
                var clip = far ? line.Find(VOChains.Far, voice) : line.Find(VOChains.Dry, voice);
                if (clip == null || clip.Clip == null) clip = line.Find(far ? VOChains.Dry : VOChains.Far, voice);
                if (!hasSpeaker) return TwoD(plan, clip, options.ForceRadio);
                return far ? ThreeD(plan, clip, null, 1f, 20f, 300f) : ThreeD(plan, clip, null, 1f, 3f, 80f);
            }
            var main = line.PrimaryClip;
            if (!hasSpeaker) return TwoD(plan, main, true);
            var p = ThreeD(plan, main, null, 1f, 1.5f, 45f);
            p.IsRadio = true; // heard through the enemy's own radio
            return p;
        }

        private static VOPlan TwoD(VOPlan plan, DialogueClip clip, bool radio)
        {
            plan.Mode = clip != null && clip.Clip != null ? VOPlanMode.TwoD : VOPlanMode.Silent;
            plan.Main = clip;
            plan.SpatialBlend = 0f;
            plan.IsRadio = radio;
            return plan;
        }

        private static VOPlan ThreeD(VOPlan plan, DialogueClip clip, string anchor, float blend, float min, float max)
        {
            plan.Mode = clip != null && clip.Clip != null ? VOPlanMode.ThreeD : VOPlanMode.Silent;
            plan.Main = clip;
            plan.AnchorId = anchor;
            plan.SpatialBlend = blend;
            plan.MinDistance = min;
            plan.MaxDistance = max;
            return plan;
        }
    }
}
