using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Player
{
    /// <summary>Settable <see cref="IInputReader"/> for rig tests (registered in GameServices).</summary>
    internal sealed class FakeInputReader : IInputReader
    {
        public Vector2 Move { get; set; }
        public Vector2 Look { get; set; }
        public bool Fire { get; set; }
        public bool FireDown { get; set; }
        public bool Aim { get; set; }
        public bool AimDown { get; set; }
        public bool ReloadDown { get; set; }
        public bool Sprint { get; set; }
        public bool SprintDown { get; set; }
        public bool JumpDown { get; set; }
        public bool CrouchDown { get; set; }
        public bool Frag { get; set; }
        public bool FragDown { get; set; }
        public bool FragUp { get; set; }
        public bool MeleeDown { get; set; }
        public bool Interact { get; set; }
        public bool InteractDown { get; set; }
        public bool ObjectivesHeld { get; set; }
        public bool ObjectivesDown { get; set; }
        public bool PauseDown { get; set; }
        public int WeaponSlotDown { get; set; }
        public float Scroll { get; set; }
        public float SkipHeld01 { get; set; }

        /// <summary>Clears the one-frame edges (call after each simulated frame).</summary>
        public void EndFrame()
        {
            FireDown = AimDown = ReloadDown = SprintDown = JumpDown = CrouchDown = FragDown = FragUp = MeleeDown = InteractDown = ObjectivesDown = PauseDown = false;
            WeaponSlotDown = 0;
            Look = Vector2.zero;
        }
    }
}
