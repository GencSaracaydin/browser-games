namespace LongShadow.Core
{
    /// <summary>Animation event names raised through <see cref="ICharacterBody.AnimEvent"/> (TECH_PLAN §7.8).</summary>
    public static class AnimEventIds
    {
        /// <summary>Left foot plant (footstep audio).</summary>
        public const string FootL = "Foot_L";
        /// <summary>Right foot plant.</summary>
        public const string FootR = "Foot_R";
        /// <summary>Grenade leaves the hand.</summary>
        public const string ThrowRelease = "ThrowRelease";
        /// <summary>Magazine leaves the weapon.</summary>
        public const string MagOut = "MagOut";
        /// <summary>Magazine seated.</summary>
        public const string MagIn = "MagIn";
        /// <summary>A scripted action reached its effect point (cut done, charge placed, shutter).</summary>
        public const string ActionHit = "ActionHit";
        /// <summary>A scripted action finished.</summary>
        public const string ActionEnd = "ActionEnd";
    }
}
