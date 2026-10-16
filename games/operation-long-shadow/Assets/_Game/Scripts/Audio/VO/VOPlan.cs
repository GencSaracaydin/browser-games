namespace LongShadow.Audio.VO
{
    /// <summary>The rendering plan of a line (<see cref="VOChainSelector.Select"/>).</summary>
    public struct VOPlan
    {
        /// <summary>Mode.</summary>
        public VOPlanMode Mode;
        /// <summary>Main clip (2D, 3D, switch or horn clip).</summary>
        public DialogueClip Main;
        /// <summary>DRY clip of a dry/radio pair.</summary>
        public DialogueClip Dry;
        /// <summary>RADIO clip of a dry/radio pair.</summary>
        public DialogueClip Radio;
        /// <summary>Marker id the 3D source sits at (null = the speaker).</summary>
        public string AnchorId;
        /// <summary>3D spatial blend.</summary>
        public float SpatialBlend;
        /// <summary>3D min distance.</summary>
        public float MinDistance;
        /// <summary>3D max distance.</summary>
        public float MaxDistance;
        /// <summary>Positional switch time (G9).</summary>
        public float SwitchTime;
        /// <summary>The line is heard as radio (subtitle icon).</summary>
        public bool IsRadio;
        /// <summary>The line plays in the booth (italic interrogator).</summary>
        public bool InBooth;

        /// <summary>The clip whose envelope drives <c>Amplitude</c>.</summary>
        public DialogueClip EnvelopeClip => Dry ?? Main ?? Radio;
    }
}
