using LongShadow.Core;

namespace LongShadow.World.Level
{
    /// <summary>
    /// Registers an <see cref="ISaveable"/> with <see cref="ICheckpointService"/> as soon as the service exists (WP07 may
    /// load after the section), and unregisters it on destroy. Allocation-free after registration.
    /// </summary>
    public sealed class SaveRegistration
    {
        private readonly ISaveable _owner;
        private ServiceRef<ICheckpointService> _service;
        private ICheckpointService _registeredWith;

        /// <summary>Creates a registration for <paramref name="owner"/>.</summary>
        public SaveRegistration(ISaveable owner) => _owner = owner;

        /// <summary>True once registered.</summary>
        public bool IsRegistered => _registeredWith != null;

        /// <summary>Registers if the checkpoint service is available and not yet registered. Call from Start and on events.</summary>
        public void TryRegister()
        {
            if (_registeredWith != null) return;
            var s = _service.Value;
            if (s == null) return;
            s.Register(_owner);
            _registeredWith = s;
        }

        /// <summary>Unregisters (OnDestroy).</summary>
        public void Unregister()
        {
            if (_registeredWith == null) return;
            _registeredWith.Unregister(_owner);
            _registeredWith = null;
        }
    }
}
