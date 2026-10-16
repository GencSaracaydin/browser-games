namespace LongShadow.Core
{
    /// <summary>Reference-counted input locks (WP01 InputLockService). See <see cref="InputLockFlags"/> for the channel map.</summary>
    public interface IInputLock
    {
        /// <summary>Locks channels; returns a handle for <see cref="Unlock"/>.</summary>
        int Lock(InputLockFlags flags, string reason);
        /// <summary>Releases a handle (unknown handles are ignored).</summary>
        void Unlock(int handle);
        /// <summary>True if any of <paramref name="flags"/> is locked.</summary>
        bool IsLocked(InputLockFlags flags);
    }
}
