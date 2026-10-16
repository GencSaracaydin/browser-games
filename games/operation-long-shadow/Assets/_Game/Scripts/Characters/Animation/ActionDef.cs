namespace LongShadow.Characters.Animation
{
    /// <summary>
    /// One row of <see cref="ActionTable"/>: how an action id (Core <c>ActionIds</c> and <see cref="ExtraActionIds"/>)
    /// is played by the WP05 bodies. Immutable; built once.
    /// </summary>
    public sealed class ActionDef
    {
        /// <summary>Action id passed to <c>ICharacterBody.PlayAction</c>.</summary>
        public readonly string Id;
        /// <summary>
        /// Animator index (&gt; 0, unique per layer) for animator-backed actions: the <c>Action</c> int on the Base layer
        /// (<see cref="ActionLayer.FullBody"/>) or the <c>ArmsAction</c> int on the Arms layer (<see cref="ActionLayer.Arms"/>);
        /// 0 for procedural/stance rows.
        /// </summary>
        public readonly int Index;
        /// <summary>Where it plays.</summary>
        public readonly ActionLayer Layer;
        /// <summary>
        /// Authored length in seconds (GDD/TECH_PLAN timings, e.g. BoltCut 3 s); the state loops (or holds) its clip until
        /// then. 0 = the clip's own length (one-shot).
        /// </summary>
        public readonly float Seconds;
        /// <summary>Length used when the animator has no state for the action and <see cref="Seconds"/> is 0.</summary>
        public readonly float FallbackSeconds;
        /// <summary>
        /// Holds until the next command (MoveTo, Stop, PlayAction, SetStance). The completion callback fires as soon as
        /// the loop is established so scripts never wait forever.
        /// </summary>
        public readonly bool Loop;
        /// <summary>Moves the held weapon to the back socket for the duration (kneeling work, hauling, camera).</summary>
        public readonly bool Stow;
        /// <summary>Keeps the left hand IK'd to the weapon's foregrip during the action.</summary>
        public readonly bool LeftHandOnWeapon;
        /// <summary>
        /// Procedural IK gesture (<c>BodyIK</c>). It stands in for the clip when the state has none yet (state tag
        /// "Procedural", or no controller), and always plays when <see cref="GestureAlways"/> is set (target-specific reaches).
        /// </summary>
        public readonly Gesture Gesture;
        /// <summary>Play <see cref="Gesture"/> even when a clip exists (the gesture aims at a scripted target).</summary>
        public readonly bool GestureAlways;
        /// <summary>Finger pose override during the action (-1 = keep the weapon's pose).</summary>
        public readonly int HandPoseOverride;
        /// <summary>First timed animation event (raised at <see cref="Event1At"/> of the length unless the clip raised it).</summary>
        public readonly string Event1;
        /// <summary>Normalised time of <see cref="Event1"/>.</summary>
        public readonly float Event1At;
        /// <summary>Second timed animation event.</summary>
        public readonly string Event2;
        /// <summary>Normalised time of <see cref="Event2"/>.</summary>
        public readonly float Event2At;
        /// <summary>Normalised time of the "ActionHit" event (effect point), &lt; 0 for none.</summary>
        public readonly float HitAt;
        /// <summary>Clip-catalog key the controller builder resolves for the state (editor).</summary>
        public readonly string ClipKey;
        /// <summary>
        /// The clip travels (vault, drop, skid drop, roll): its root motion moves the body while the action plays, unless
        /// the NavMeshAgent is traversing an off-mesh link (then WP04 owns the position).
        /// </summary>
        public readonly bool RootMotion;
        /// <summary>
        /// For timed one-shot clips (<see cref="Seconds"/> &gt; 0 and a non-looping clip): the controller builder sets the
        /// state speed so the clip fits the authored length (clamped to 0.75-1.5x, beyond that the clip holds/cuts).
        /// </summary>
        public readonly bool FitToSeconds;

        public ActionDef(string id, int index, ActionLayer layer, string clipKey, float seconds = 0f, float fallbackSeconds = 1f,
                         bool loop = false, bool stow = false, bool leftHandOnWeapon = false, Gesture gesture = Gesture.None,
                         int handPoseOverride = -1, string event1 = null, float event1At = 0f, string event2 = null,
                         float event2At = 0f, float hitAt = -1f, bool rootMotion = false, bool fitToSeconds = true,
                         bool gestureAlways = false)
        {
            Id = id;
            Index = index;
            Layer = layer;
            ClipKey = clipKey;
            Seconds = seconds;
            FallbackSeconds = fallbackSeconds;
            Loop = loop;
            Stow = stow;
            LeftHandOnWeapon = leftHandOnWeapon;
            Gesture = gesture;
            HandPoseOverride = handPoseOverride;
            Event1 = event1;
            Event1At = event1At;
            Event2 = event2;
            Event2At = event2At;
            HitAt = hitAt;
            RootMotion = rootMotion;
            FitToSeconds = fitToSeconds;
            GestureAlways = gestureAlways;
        }

        /// <summary>True if an animator state backs the action (full-body or arms layer).</summary>
        public bool HasState => (Layer == ActionLayer.FullBody || Layer == ActionLayer.Arms) && Index > 0;

        /// <summary>True when the state must stay until HumanoidBody releases it (looping or timed actions).</summary>
        public bool Held => Loop || Seconds > 0f;

        /// <summary>Planned length: <see cref="Seconds"/> if authored, else <paramref name="clipSeconds"/>, else the fallback.</summary>
        public float LengthFor(float clipSeconds)
        {
            if (Seconds > 0f) return Seconds;
            return clipSeconds > 0.05f ? clipSeconds : FallbackSeconds;
        }

        /// <summary>
        /// State speed multiplier the controller builder bakes for a clip of <paramref name="clipSeconds"/> (1 unless the row
        /// is a timed one-shot that should fit <see cref="Seconds"/>).
        /// </summary>
        public float StateSpeedFor(float clipSeconds, bool clipLoops)
        {
            if (!FitToSeconds || Loop || Seconds <= 0f || clipLoops || clipSeconds <= 0.05f) return 1f;
            var s = clipSeconds / Seconds;
            if (s < 0.75f) s = 0.75f;
            if (s > 1.5f) s = 1.5f;
            return s;
        }

        public override string ToString() => Id + "#" + Index + " (" + Layer + ")";
    }
}
