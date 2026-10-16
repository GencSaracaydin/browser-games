using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LongShadow.Combat.Effects
{
    /// <summary>
    /// Ring buffer of URP screen-space <see cref="DecalProjector"/>s (TECH_PLAN §7.5: pool of 64 with fade; §3.5 Decal
    /// feature, 60 m draw distance). The oldest decal is recycled; each fades out over its last seconds. Decals that follow
    /// a moving collider (vehicles) are re-parented and recreated if their parent is destroyed.
    /// </summary>
    public sealed class DecalPool
    {
        private readonly Transform _root;
        private readonly DecalProjector[] _items;
        private readonly float[] _age;
        private readonly float[] _life;
        private int _next;

        /// <summary>Fade-out duration at the end of a decal's life.</summary>
        public const float FadeSeconds = 3f;

        /// <summary>Creates <paramref name="count"/> inactive projectors under <paramref name="root"/>.</summary>
        public DecalPool(Transform root, int count)
        {
            _root = root;
            _items = new DecalProjector[count];
            _age = new float[count];
            _life = new float[count];
            for (var i = 0; i < count; i++) _items[i] = Create(i);
        }

        /// <summary>Places a decal on a surface.</summary>
        /// <param name="material">Decal material (atlas).</param>
        /// <param name="rect">Atlas element (u0, v0, u1, v1).</param>
        /// <param name="size">Width/height in metres.</param>
        /// <param name="point">Surface point.</param>
        /// <param name="normal">Surface normal.</param>
        /// <param name="depth">Projection depth in metres (straddles the surface).</param>
        /// <param name="life">Seconds before it fades.</param>
        /// <param name="follow">Transform to follow (moving objects), or null.</param>
        public void Spawn(Material material, Vector4 rect, Vector2 size, Vector3 point, Vector3 normal, float depth, float life, Transform follow = null)
        {
            if (material == null || _items.Length == 0) return;
            var i = _next;
            _next = (_next + 1) % _items.Length;
            var d = _items[i];
            if (d == null) d = _items[i] = Create(i);
            var t = d.transform;
            t.SetParent(follow != null ? follow : _root, true);
            var n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            var up = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            t.SetPositionAndRotation(point, Quaternion.LookRotation(-n, up) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            d.material = material;
            d.size = new Vector3(size.x, size.y, depth);
            d.pivot = Vector3.zero;
            d.uvScale = new Vector2(rect.z - rect.x, rect.w - rect.y);
            d.uvBias = new Vector2(rect.x, rect.y);
            d.fadeFactor = 1f;
            _age[i] = 0f;
            _life[i] = Mathf.Max(FadeSeconds, life);
            d.gameObject.SetActive(true);
        }

        /// <summary>Ages and fades decals.</summary>
        public void Tick(float dt)
        {
            for (var i = 0; i < _items.Length; i++)
            {
                var d = _items[i];
                if (d == null || !d.gameObject.activeSelf) continue;
                _age[i] += dt;
                var left = _life[i] - _age[i];
                if (left <= 0f)
                {
                    Recycle(i);
                    continue;
                }
                if (left < FadeSeconds) d.fadeFactor = left / FadeSeconds;
            }
        }

        /// <summary>Hides every decal (checkpoint reload).</summary>
        public void Clear()
        {
            for (var i = 0; i < _items.Length; i++)
                if (_items[i] != null) Recycle(i);
        }

        private void Recycle(int i)
        {
            var d = _items[i];
            d.gameObject.SetActive(false);
            if (d.transform.parent != _root) d.transform.SetParent(_root, false);
        }

        private DecalProjector Create(int i)
        {
            var go = new GameObject("Decal_" + i);
            go.transform.SetParent(_root, false);
            go.SetActive(false);
            var d = go.AddComponent<DecalProjector>();
            d.drawDistance = 60f;      // TECH_PLAN §3.5: decal max draw distance 60 m
            d.fadeScale = 0.9f;
            d.startAngleFade = 160f;
            d.endAngleFade = 180f;
            d.scaleMode = DecalScaleMode.ScaleInvariant;
            return d;
        }
    }
}
