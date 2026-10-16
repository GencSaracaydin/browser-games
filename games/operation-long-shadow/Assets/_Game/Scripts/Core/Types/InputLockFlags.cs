using System;

namespace LongShadow.Core
{
    /// <summary>
    /// Input channels that <see cref="IInputLock"/> can lock. Locked channels read neutral in <see cref="IInputReader"/>.
    /// Mapping: Move = Move/Sprint/Crouch; Look = Look; Fire = Fire/Aim; Weapons = Reload/Frag/Melee/weapon switch;
    /// Interact = Interact; Jump = Jump. Pause, Objectives and Skip are never locked.
    /// </summary>
    [Flags]
    public enum InputLockFlags
    {
        None = 0,
        Move = 1,
        Look = 2,
        Fire = 4,
        Weapons = 8,
        Interact = 16,
        Jump = 32,
        All = ~0,
    }
}
