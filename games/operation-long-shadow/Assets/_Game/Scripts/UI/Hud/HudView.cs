using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Base of every HUD element. Views are plain C# objects owned by <see cref="HudRoot"/> (one MonoBehaviour ticks
    /// them all, in LateUpdate with unscaled time), so they can be built and ticked in EditMode tests. A view builds its
    /// uGUI objects once in <see cref="OnBuild"/>, subscribes to events in <see cref="Enable"/>, and must not allocate in
    /// <see cref="Tick"/> (TECH_PLAN R2).
    /// </summary>
    public abstract class HudView
    {
        /// <summary>Shared HUD state.</summary>
        protected HudContext Ctx { get; private set; }

        /// <summary>Root rect of the view (null before Build).</summary>
        public RectTransform Root { get; protected set; }

        /// <summary>Display name for logs and the layout audit.</summary>
        public virtual string Name => GetType().Name;

        /// <summary>Builds the view under <paramref name="parent"/>.</summary>
        public void Build(HudContext ctx, RectTransform parent)
        {
            Ctx = ctx;
            OnBuild(parent);
        }

        /// <summary>Creates the uGUI objects.</summary>
        protected abstract void OnBuild(RectTransform parent);

        /// <summary>Subscribes to events (HudRoot.OnEnable).</summary>
        public virtual void Enable() { }

        /// <summary>Unsubscribes (HudRoot.OnDisable).</summary>
        public virtual void Disable() { }

        /// <summary>Per-frame update with unscaled delta time. Must not allocate.</summary>
        public virtual void Tick(float dt) { }

        /// <summary>Clears transient state (checkpoint loaded, quit to session).</summary>
        public virtual void ResetState() { }

        /// <summary>Re-applies layout after the canvas size changed.</summary>
        public virtual void Relayout(Vector2 canvasSize) { }

        /// <summary>Adds the view's static screen regions to the layout audit (GDD §11 "no overlap").</summary>
        public virtual void CollectLayout(List<HudLayoutRegion> regions) { }
    }
}
