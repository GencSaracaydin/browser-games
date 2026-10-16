using System;
using System.Collections.Generic;

namespace LongShadow.Core
{
    /// <summary>
    /// Service locator keyed by interface type (TECH_PLAN §5.3). Register in Awake, look up in Start or lazily
    /// (cache with <see cref="ServiceRef{T}"/>). A missing service is logged once and returns null: systems must degrade
    /// and run alone in test scenes. Statics reset on SubsystemRegistration (domain reload is disabled, R6).
    /// </summary>
    public static class GameServices
    {
        private static readonly Dictionary<Type, object> s_services = new Dictionary<Type, object>();
        private static readonly HashSet<Type> s_missingLogged = new HashSet<Type>();
        private static int s_version = 1;

        /// <summary>Incremented on every register/unregister (lets <see cref="ServiceRef{T}"/> refresh its cache).</summary>
        public static int Version => s_version;

        /// <summary>Raised after a service is registered or removed (type, instance or null).</summary>
        public static event Action<Type, object> Changed;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_services.Clear();
            s_missingLogged.Clear();
            Changed = null;
            s_version++;
        }

        /// <summary>Registers <paramref name="service"/> as <typeparamref name="T"/> (an interface). Replaces an existing one with a warning.</summary>
        public static void Register<T>(T service) where T : class
        {
            if (service == null)
            {
                LSLog.Error(LogCategory.Core, "GameServices.Register<" + typeof(T).Name + ">(null)");
                return;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!typeof(T).IsInterface)
                LSLog.WarnOnce("svc-notiface:" + typeof(T).Name, LogCategory.Core, "GameServices should be keyed by interfaces; got " + typeof(T).Name);
#endif
            var t = typeof(T);
            if (s_services.TryGetValue(t, out var existing) && !ReferenceEquals(existing, service) && IsAlive(existing))
                LSLog.Warn(LogCategory.Core, "Service " + t.Name + " replaced (" + existing + " -> " + service + ")");
            s_services[t] = service;
            s_missingLogged.Remove(t);
            s_version++;
            Changed?.Invoke(t, service);
        }

        /// <summary>Removes the service if <paramref name="service"/> is the registered instance. Returns true if removed.</summary>
        public static bool Unregister<T>(T service) where T : class
        {
            var t = typeof(T);
            if (!s_services.TryGetValue(t, out var existing) || !ReferenceEquals(existing, service)) return false;
            s_services.Remove(t);
            s_version++;
            Changed?.Invoke(t, null);
            return true;
        }

        /// <summary>Returns the service or null (logs a warning once per type when missing).</summary>
        public static T Get<T>() where T : class
        {
            if (TryGet(out T s)) return s;
            if (s_missingLogged.Add(typeof(T)))
                LSLog.Warn(LogCategory.Core, "Service " + typeof(T).Name + " is not registered; the caller must degrade gracefully.");
            return null;
        }

        /// <summary>Looks up a service without logging.</summary>
        public static bool TryGet<T>(out T service) where T : class
        {
            if (s_services.TryGetValue(typeof(T), out var o))
            {
                if (IsAlive(o))
                {
                    service = (T)o;
                    return true;
                }
                // A destroyed MonoBehaviour that forgot to unregister.
                s_services.Remove(typeof(T));
                s_version++;
            }
            service = null;
            return false;
        }

        /// <summary>True if a live service is registered.</summary>
        public static bool Has<T>() where T : class => TryGet(out T _);

        /// <summary>Removes every service (tests only).</summary>
        public static void Clear()
        {
            s_services.Clear();
            s_missingLogged.Clear();
            s_version++;
        }

        private static bool IsAlive(object o)
        {
            if (o is UnityEngine.Object uo) return uo != null;
            return o != null;
        }
    }
}
