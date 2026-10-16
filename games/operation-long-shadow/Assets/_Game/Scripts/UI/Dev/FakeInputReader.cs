using LongShadow.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LongShadow.UI.Dev
{
    /// <summary>
    /// Keyboard-only <see cref="IInputReader"/> for the UI test bed (Tab holds the objective list). Everything else is
    /// neutral; <see cref="ForceObjectivesHeld"/> lets tests hold Tab.
    /// </summary>
    public sealed class FakeInputReader : IInputReader
    {
        /// <summary>Holds the objective list regardless of the keyboard (tests).</summary>
        public bool ForceObjectivesHeld { get; set; }

        public Vector2 Move => Vector2.zero;
        public Vector2 Look => Vector2.zero;
        public bool Fire => false;
        public bool FireDown => false;
        public bool Aim => false;
        public bool AimDown => false;
        public bool ReloadDown => false;
        public bool Sprint => false;
        public bool SprintDown => false;
        public bool JumpDown => false;
        public bool CrouchDown => false;
        public bool Frag => false;
        public bool FragDown => false;
        public bool FragUp => false;
        public bool MeleeDown => false;
        public bool Interact => false;
        public bool InteractDown => false;

        public bool ObjectivesHeld
        {
            get
            {
                if (ForceObjectivesHeld) return true;
                var kb = Keyboard.current;
                return kb != null && kb.tabKey.isPressed;
            }
        }

        public bool ObjectivesDown
        {
            get
            {
                var kb = Keyboard.current;
                return kb != null && kb.tabKey.wasPressedThisFrame;
            }
        }

        public bool PauseDown => false;
        public int WeaponSlotDown => 0;
        public float Scroll => 0f;
        public float SkipHeld01 => 0f;
    }
}
