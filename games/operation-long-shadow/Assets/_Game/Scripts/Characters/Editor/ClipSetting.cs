namespace LongShadow.Characters.Editor
{
    /// <summary>Import settings of one clip (<see cref="ClipSettingsTable"/>).</summary>
    public readonly struct ClipSetting
    {
        /// <summary>Final clip name (A_&lt;Source&gt;_&lt;Name&gt;).</summary>
        public readonly string Name;
        /// <summary>Loop Time (and Loop Pose).</summary>
        public readonly bool Loop;
        /// <summary>The clip travels: Root Transform Position XZ is not baked (HumanoidBody applies it during actions).</summary>
        public readonly bool RootMotion;
        /// <summary>Root Transform Rotation baked into the pose (false for turn-in-place clips).</summary>
        public readonly bool BakeRotation;
        /// <summary>Also import a mirrored duplicate named "&lt;Name&gt;_Mirror" (opposite-side strafes, turns, hits).</summary>
        public readonly bool Mirror;
        /// <summary>Additive hit reaction: reference pose = first frame.</summary>
        public readonly bool Additive;
        /// <summary>
        /// Name suffixes of extra identical clips ("_Arms" → "A_Meshy_Alert_Arms") so one take can fill several controller
        /// slots as distinct clip objects (an override controller replaces a clip object everywhere it is used).
        /// </summary>
        public readonly string[] Aliases;
        /// <summary>Root height baked "based upon Original" instead of the feet (Unity SAC clips, content measurement).</summary>
        public readonly bool OriginalHeight;
        /// <summary>First frame of the take to import (-1 = the take's own range; SAC takes start with bind frames).</summary>
        public readonly int FirstFrame;
        /// <summary>Last frame of the take to import (-1 = the take's own range).</summary>
        public readonly int LastFrame;
        /// <summary>Normalised times of the left foot plants (Foot_L events), or null.</summary>
        public readonly float[] FootL;
        /// <summary>Normalised times of the right foot plants (Foot_R events), or null.</summary>
        public readonly float[] FootR;

        public ClipSetting(string name, bool loop, bool rootMotion, float[] footL = null, float[] footR = null, bool bakeRotation = true,
                           bool mirror = false, bool additive = false, string[] aliases = null, bool originalHeight = false,
                           int firstFrame = -1, int lastFrame = -1)
        {
            Name = name;
            Loop = loop;
            RootMotion = rootMotion;
            BakeRotation = bakeRotation;
            Mirror = mirror;
            Additive = additive;
            FootL = footL;
            FootR = footR;
            Aliases = aliases;
            OriginalHeight = originalHeight;
            FirstFrame = firstFrame;
            LastFrame = lastFrame;
        }

        /// <summary>True when the row exists (default struct = not found).</summary>
        public bool IsValid => !string.IsNullOrEmpty(Name);
    }
}
