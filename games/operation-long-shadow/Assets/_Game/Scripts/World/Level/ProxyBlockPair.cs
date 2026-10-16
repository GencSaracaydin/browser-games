namespace LongShadow.World.Level
{
    /// <summary>One far-proxy block's two sides (see <see cref="ProxyBlockRegistry"/>).</summary>
    public sealed class ProxyBlockPair
    {
        /// <summary>Block id.</summary>
        public string Id;
        /// <summary>Real geometry (null while its section is unloaded).</summary>
        public SectionBlock Real;
        /// <summary>Baked proxy (null if the block has none).</summary>
        public FarProxyBlock Proxy;
        /// <summary>True while the real side is shown.</summary>
        public bool ShowingReal;
        /// <summary>True once a visibility decision was applied.</summary>
        public bool Decided;
    }
}
