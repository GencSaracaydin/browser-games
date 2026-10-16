using System.Text;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Platform
{
    /// <summary>
    /// <see cref="IInputLock"/> (TECH_PLAN §5.4): reference-counted channel locks. <c>LongShadow.Input.InputReader</c>
    /// returns neutral values for locked channels. Used by the pause service (All), cinematics, inserts and scripts.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Platform)]
    [DisallowMultipleComponent]
    public sealed class InputLockService : MonoBehaviour, IInputLock
    {
        private readonly InputLockSet _set = new InputLockSet();

        /// <summary>Union of every live lock (diagnostics).</summary>
        public InputLockFlags Locked => _set.Union;

        private void Awake() => GameServices.Register<IInputLock>(this);

        private void OnDestroy()
        {
            GameServices.Unregister<IInputLock>(this);
            _set.Clear();
        }

        /// <inheritdoc/>
        public int Lock(InputLockFlags flags, string reason)
        {
            var h = _set.Lock(flags, reason);
            LSLog.Verbose(LogCategory.Input, "Lock #" + h + " " + flags + " (" + reason + ")");
            return h;
        }

        /// <inheritdoc/>
        public void Unlock(int handle)
        {
            if (_set.Unlock(handle)) LSLog.Verbose(LogCategory.Input, "Unlock #" + handle);
        }

        /// <inheritdoc/>
        public bool IsLocked(InputLockFlags flags) => _set.IsLocked(flags);

        /// <summary>Lists live locks (dev console).</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append("locked ").Append(_set.Union).Append('\n');
            _set.Describe(sb);
            return sb.ToString();
        }
    }
}
