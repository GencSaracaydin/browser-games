using LongShadow.World.Level.Data;
using UnityEngine;

namespace LongShadow.World.Level
{
    /// <summary>
    /// The real geometry of one far-proxy block inside a section scene (TECH_PLAN D7). Its renderers are hidden while the
    /// player is more than the block's swap distance away and the baked proxy in <c>S9_TownFar</c> shows instead; colliders
    /// stay active. Without a proxy the block always shows.
    /// </summary>
    public sealed class SectionBlock : MonoBehaviour
    {
        [SerializeField] private string _blockId;
        [SerializeField] private AreaRect _rect;
        [SerializeField] private float _swapDistance = WorldConst.FarProxySwapDistance;
        [SerializeField, Tooltip("Renderers toggled with the block (filled by the builder; empty = every child renderer).")]
        private Renderer[] _renderers = new Renderer[0];

        private bool _visible = true;

        /// <summary>Block id.</summary>
        public string BlockId => _blockId;
        /// <summary>Block area.</summary>
        public AreaRect Rect => _rect;
        /// <summary>Hand-over distance.</summary>
        public float SwapDistance => _swapDistance;
        /// <summary>True while the renderers are enabled.</summary>
        public bool Visible => _visible;

        /// <summary>Configures the block (editor builders).</summary>
        public void Configure(string blockId, AreaRect rect, float swapDistance, Renderer[] renderers)
        {
            _blockId = blockId;
            _rect = rect;
            _swapDistance = swapDistance;
            _renderers = renderers ?? new Renderer[0];
        }

        private void OnEnable()
        {
            if (_renderers == null || _renderers.Length == 0) _renderers = GetComponentsInChildren<Renderer>(true);
            ProxyBlockRegistry.Register(this);
        }

        private void OnDisable()
        {
            SetVisible(true);
            ProxyBlockRegistry.Unregister(this);
        }

        /// <summary>Shows or hides the block's renderers.</summary>
        public void SetVisible(bool visible)
        {
            if (_visible == visible) return;
            _visible = visible;
            for (var i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = visible;
        }
    }
}
