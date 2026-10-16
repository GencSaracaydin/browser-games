namespace LongShadow.Core
{
    /// <summary>
    /// Cached, allocation-free handle to a <see cref="GameServices"/> entry. Store as a field and read <see cref="Value"/>;
    /// it re-resolves only when the registry changes. Never logs (check for null).
    /// <code>private ServiceRef&lt;IAudioService&gt; _audio; ... _audio.Value?.Play2D("UI_Click");</code>
    /// </summary>
    public struct ServiceRef<T> where T : class
    {
        private T _value;
        private int _version;

        /// <summary>The service, or null if not registered.</summary>
        public T Value
        {
            get
            {
                if (_version != GameServices.Version || (_value is UnityEngine.Object o && o == null))
                {
                    GameServices.TryGet(out _value);
                    _version = GameServices.Version;
                }
                return _value;
            }
        }

        /// <summary>True and the service if registered.</summary>
        public bool TryGet(out T service)
        {
            service = Value;
            return service != null;
        }
    }
}
