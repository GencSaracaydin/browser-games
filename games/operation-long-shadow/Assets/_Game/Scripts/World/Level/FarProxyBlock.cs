using LongShadow.World.Level.Data;
using UnityEngine;

namespace LongShadow.World.Level
{
    /// <summary>
    /// A baked far-proxy block in <c>S9_TownFar</c> (TECH_PLAN D7, GDD §6.7): roofs, facades, ground and lamp heads of one
    /// block in URP Baked Lit with the GOLDEN/SUNSET/DUSK lightmap sets (swapped by WP10 Look's LightmapSwapper). It is
    /// hidden only while the matching <see cref="SectionBlock"/> is loaded and the player is within the swap distance.
    /// Layer FarProxy, no colliders.
    /// </summary>
    public sealed class FarProxyBlock : MonoBehaviour
    {
        [SerializeField] private string _blockId;
        [SerializeField] private AreaRect _rect;
        [SerializeField] private Renderer[] _renderers = new Renderer[0];

        private bool _visible = true;

        /// <summary>Block id.</summary>
        public string BlockId => _blockId;
        /// <summary>Block area.</summary>
        public AreaRect Rect => _rect;
        /// <summary>True while shown.</summary>
        public bool Visible => _visible;

        /// <summary>Configures the block (editor builders).</summary>
        public void Configure(string blockId, AreaRect rect, Renderer[] renderers)
        {
            _blockId = blockId;
            _rect = rect;
            _renderers = renderers ?? new Renderer[0];
        }

        private void OnEnable()
        {
            if (_renderers == null || _renderers.Length == 0) _renderers = GetComponentsInChildren<Renderer>(true);
            ProxyBlockRegistry.Register(this);
        }

        private void OnDisable() => ProxyBlockRegistry.Unregister(this);

        /// <summary>Shows or hides the proxy.</summary>
        public void SetVisible(bool visible)
        {
            if (_visible == visible) return;
            _visible = visible;
            for (var i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = visible;
        }
    }
}
