using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Frame-sampled player input (WP00 <c>LongShadow.Input.InputReader</c>). Values are neutral for channels locked by
    /// <see cref="IInputLock"/>. "Down"/"Up" members are true for exactly one frame.
    /// </summary>
    public interface IInputReader
    {
        /// <summary>WASD, normalised to length &lt;= 1.</summary>
        Vector2 Move { get; }
        /// <summary>Mouse delta this frame in CSS pixels (divided by devicePixelRatio, one-frame spikes filtered).</summary>
        Vector2 Look { get; }
        /// <summary>Fire held.</summary>
        bool Fire { get; }
        /// <summary>Fire pressed this frame.</summary>
        bool FireDown { get; }
        /// <summary>Aim held.</summary>
        bool Aim { get; }
        /// <summary>Aim pressed this frame (ADS toggle option).</summary>
        bool AimDown { get; }
        /// <summary>Reload pressed.</summary>
        bool ReloadDown { get; }
        /// <summary>Sprint held.</summary>
        bool Sprint { get; }
        /// <summary>Sprint pressed this frame.</summary>
        bool SprintDown { get; }
        /// <summary>Jump/mantle pressed.</summary>
        bool JumpDown { get; }
        /// <summary>Crouch/slide pressed (C; never Ctrl).</summary>
        bool CrouchDown { get; }
        /// <summary>Frag held (cooking).</summary>
        bool Frag { get; }
        /// <summary>Frag pressed (pin pull).</summary>
        bool FragDown { get; }
        /// <summary>Frag released (throw).</summary>
        bool FragUp { get; }
        /// <summary>Melee pressed.</summary>
        bool MeleeDown { get; }
        /// <summary>Interact held (hold prompts).</summary>
        bool Interact { get; }
        /// <summary>Interact pressed.</summary>
        bool InteractDown { get; }
        /// <summary>Objectives list held (Tab).</summary>
        bool ObjectivesHeld { get; }
        /// <summary>Objectives pressed this frame.</summary>
        bool ObjectivesDown { get; }
        /// <summary>Pause pressed (Esc or P).</summary>
        bool PauseDown { get; }
        /// <summary>1 or 2 pressed this frame, else 0.</summary>
        int WeaponSlotDown { get; }
        /// <summary>Mouse wheel steps this frame (+ up, - down).</summary>
        float Scroll { get; }
        /// <summary>Skip hold progress 0..1 (Space held 1.0 s, unscaled time).</summary>
        float SkipHeld01 { get; }
    }
}
