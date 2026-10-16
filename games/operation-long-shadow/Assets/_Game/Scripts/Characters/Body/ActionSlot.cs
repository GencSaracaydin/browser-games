using System;
using LongShadow.Characters.Animation;

namespace LongShadow.Characters.Body
{
    /// <summary>
    /// Runtime state of one action channel of <see cref="HumanoidBody"/>: the full-body slot (Base layer), the arms slot
    /// (Arms layer) or the procedural slot (timed head poses, no state). Plain data; the body drives it.
    /// </summary>
    internal sealed class ActionSlot
    {
        public ActionDef Def;
        public Action Done;
        public float Time;
        public float Length;
        public bool Entered;
        public bool CallbackFired;
        public bool Event1Fired;
        public bool Event2Fired;
        public bool HitFired;
        public bool GestureStarted;

        /// <summary>True while the action plays (a looping action stays active after its callback fired).</summary>
        public bool Active => Def != null && !(CallbackFired && !Def.Loop);

        /// <summary>Arms the slot for a new action (callers end the previous one first).</summary>
        public void Begin(ActionDef def, Action done)
        {
            Def = def;
            Done = done;
            Time = 0f;
            Length = def.LengthFor(0f);
            Entered = false;
            CallbackFired = false;
            Event1Fired = def.Event1 == null;
            Event2Fired = def.Event2 == null;
            HitFired = def.HitAt < 0f;
            GestureStarted = false;
        }

        /// <summary>Clears the slot without callbacks.</summary>
        public void Clear()
        {
            Def = null;
            Done = null;
            CallbackFired = true;
            GestureStarted = false;
        }

        /// <summary>Takes the pending callback (null if already fired) and marks it fired.</summary>
        public Action TakeCallback()
        {
            if (CallbackFired) return null;
            CallbackFired = true;
            var cb = Done;
            Done = null;
            return cb;
        }
    }
}
