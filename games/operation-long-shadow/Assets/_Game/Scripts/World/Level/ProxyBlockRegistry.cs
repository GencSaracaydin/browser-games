using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.World.Level
{
    /// <summary>
    /// Cross-scene pairing of real section blocks (<see cref="SectionBlock"/>, in the section scenes) and their baked far
    /// proxies (<see cref="FarProxyBlock"/>, in <c>S9_TownFar</c>) by block id (TECH_PLAN D7). Scenes cannot reference
    /// each other, so both sides register here on enable; <see cref="ProxyBlockDirector"/> decides visibility.
    /// </summary>
    public static class ProxyBlockRegistry
    {
        private static readonly Dictionary<string, ProxyBlockPair> s_pairs = new Dictionary<string, ProxyBlockPair>();
        private static readonly List<ProxyBlockPair> s_list = new List<ProxyBlockPair>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_pairs.Clear();
            s_list.Clear();
        }

        /// <summary>All pairs (live list; do not modify).</summary>
        public static IReadOnlyList<ProxyBlockPair> Pairs => s_list;

        private static ProxyBlockPair GetOrAdd(string id)
        {
            if (s_pairs.TryGetValue(id, out var p)) return p;
            p = new ProxyBlockPair { Id = id };
            s_pairs.Add(id, p);
            s_list.Add(p);
            return p;
        }

        /// <summary>Registers a real block (SectionBlock.OnEnable).</summary>
        public static void Register(SectionBlock real)
        {
            if (real == null || string.IsNullOrEmpty(real.BlockId)) return;
            var p = GetOrAdd(real.BlockId);
            p.Real = real;
            p.Decided = false;
        }

        /// <summary>Unregisters a real block (section unloaded): the proxy shows again.</summary>
        public static void Unregister(SectionBlock real)
        {
            if (real == null || string.IsNullOrEmpty(real.BlockId) || !s_pairs.TryGetValue(real.BlockId, out var p)) return;
            if (p.Real == real) p.Real = null;
            p.Decided = false;
            if (p.Proxy != null) p.Proxy.SetVisible(true);
        }

        /// <summary>Registers a proxy block (FarProxyBlock.OnEnable).</summary>
        public static void Register(FarProxyBlock proxy)
        {
            if (proxy == null || string.IsNullOrEmpty(proxy.BlockId)) return;
            var p = GetOrAdd(proxy.BlockId);
            p.Proxy = proxy;
            p.Decided = false;
        }

        /// <summary>Unregisters a proxy block.</summary>
        public static void Unregister(FarProxyBlock proxy)
        {
            if (proxy == null || string.IsNullOrEmpty(proxy.BlockId) || !s_pairs.TryGetValue(proxy.BlockId, out var p)) return;
            if (p.Proxy == proxy) p.Proxy = null;
            p.Decided = false;
            if (p.Real != null) p.Real.SetVisible(true);
        }

        /// <summary>The pair of a block id, or null.</summary>
        public static ProxyBlockPair Get(string id) => id != null && s_pairs.TryGetValue(id, out var p) ? p : null;
    }
}
