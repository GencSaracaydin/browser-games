namespace LongShadow.Characters.Animation
{
    /// <summary>Where an action from <see cref="ActionTable"/> plays.</summary>
    public enum ActionLayer
    {
        /// <summary>A full-body state in the Base layer (vault, death-like one-shots, kneeling work, sitting).</summary>
        FullBody = 0,
        /// <summary>An arms-only state in the Arms layer (reload, throw, signals) so locomotion keeps playing underneath.</summary>
        Arms = 1,
        /// <summary>No animator state: solved procedurally by <c>BodyIK</c> (recoil, IK gestures, head lift).</summary>
        Procedural = 2,
        /// <summary>Changes the locomotion stance instead of playing a state (insertion crouch-run).</summary>
        Stance = 3,
    }
}
