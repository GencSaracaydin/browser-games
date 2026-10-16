using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Player.View
{
    /// <summary>
    /// Owner-based FOV requests (TECH_PLAN §7.3 FOV stack; <c>IPlayerRig.RequestFov</c>/<c>ReleaseFov</c>): the latest
    /// request wins and the camera blends from its current FOV to the new target over the request's blend time
    /// (smoothstep). Releasing blends to the next live request over that request's blend time, or back to the base FOV
    /// (settings + sprint/slide kick) over the released request's blend time. The base may move during a blend (aspect
    /// changes, kicks). Allocation-free after warm-up.
    /// </summary>
    public sealed class FovStack
    {
        private struct Entry
        {
            public object Owner;
            public float VerticalFov;
            public float BlendTime;
            public int Order;
        }

        private static readonly object BaseKey = new object();

        private readonly List<Entry> _entries = new List<Entry>(8);
        private int _order;
        private object _activeKey;
        private int _activeOrder = -1;
        private float _blendFrom;
        private float _blendTime;
        private float _blendDuration;
        private float _lastReleasedBlend;
        private float _current = -1f;

        /// <summary>FOV after the last <see cref="Evaluate"/> (vertical degrees).</summary>
        public float Current => _current;
        /// <summary>Number of live requests.</summary>
        public int Count => _entries.Count;
        /// <summary>True while a request (not the base) drives the FOV.</summary>
        public bool HasRequest => _entries.Count > 0;

        /// <summary>Adds or updates a request (vertical FOV in degrees, blend in seconds).</summary>
        public void Request(object owner, float verticalFov, float blendTime)
        {
            if (owner == null) return;
            var fov = Mathf.Clamp(verticalFov, 1f, 170f);
            var blend = Mathf.Max(0f, blendTime);
            for (var i = 0; i < _entries.Count; i++)
            {
                if (!ReferenceEquals(_entries[i].Owner, owner)) continue;
                var existing = _entries[i];
                if (Mathf.Abs(existing.VerticalFov - fov) < 0.01f)
                {
                    // Same request repeated every frame: keep the running blend.
                    existing.BlendTime = blend;
                    _entries[i] = existing;
                    return;
                }
                _entries[i] = new Entry { Owner = owner, VerticalFov = fov, BlendTime = blend, Order = ++_order };
                return;
            }
            _entries.Add(new Entry { Owner = owner, VerticalFov = fov, BlendTime = blend, Order = ++_order });
        }

        /// <summary>Removes the owner's request (unknown owners are ignored).</summary>
        public void Release(object owner)
        {
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(_entries[i].Owner, owner)) continue;
                _lastReleasedBlend = _entries[i].BlendTime;
                _entries.RemoveAt(i);
            }
        }

        /// <summary>Removes every request (respawn, teleport).</summary>
        public void Clear()
        {
            _entries.Clear();
            _lastReleasedBlend = 0f;
        }

        /// <summary>Snaps the current value (no blend in progress).</summary>
        public void Snap(float verticalFov)
        {
            _current = verticalFov;
            _blendDuration = 0f;
        }

        /// <summary>Advances the blend and returns the FOV to use this frame.</summary>
        public float Evaluate(float baseVerticalFov, float dt)
        {
            if (_current < 0f) _current = baseVerticalFov;
            var top = -1;
            for (var i = 0; i < _entries.Count; i++)
                if (top < 0 || _entries[i].Order > _entries[top].Order) top = i;

            object key;
            float target;
            int order;
            float blend;
            if (top >= 0)
            {
                key = _entries[top].Owner;
                target = _entries[top].VerticalFov;
                order = _entries[top].Order;
                blend = _entries[top].BlendTime;
            }
            else
            {
                key = BaseKey;
                target = baseVerticalFov;
                order = 0;
                blend = _lastReleasedBlend;
            }

            if (!ReferenceEquals(key, _activeKey) || order != _activeOrder)
            {
                _activeKey = key;
                _activeOrder = order;
                _blendFrom = _current;
                _blendTime = 0f;
                _blendDuration = blend;
            }

            if (_blendDuration <= 0f)
            {
                _current = target;
            }
            else
            {
                _blendTime += Mathf.Max(0f, dt);
                var x = Mathf.Clamp01(_blendTime / _blendDuration);
                _current = Mathf.Lerp(_blendFrom, target, x * x * (3f - 2f * x));
                if (x >= 1f) _blendDuration = 0f;
            }
            return _current;
        }
    }
}
