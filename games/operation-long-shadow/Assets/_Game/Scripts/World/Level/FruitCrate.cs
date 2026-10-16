using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level
{
    /// <summary>
    /// A market crate of oranges or pomegranates that spills physics fruit when shot (GDD §7.3, ids B3.Fruit1…6,
    /// TECH_PLAN §7.15 FruitCrate). It is its own <see cref="IDamageable"/> (team None) so the weapon raycast that hits its
    /// collider spills it without a Combat dependency; it also reacts to <see cref="GameEvents.DestructibleDamaged"/> with its
    /// id if an integrator adds WP03's Destructible instead. Fruit are pooled through <see cref="Pools"/> on layer Debris.
    /// The first hit spills (the crate's fruit layer hides), later hits knock a few more out up to <see cref="_maxFruit"/>.
    /// </summary>
    public sealed class FruitCrate : MonoBehaviour, IDamageable, ISaveable
    {
        [SerializeField, Tooltip("Pooled physics fruit (FAL_ORANGE / FAL_POMEGRANATE with Rigidbody + SphereCollider on Debris).")]
        private GameObject _fruitPrefab;
        [SerializeField, Tooltip("The heaped fruit visible on top of the crate (hidden after the spill).")]
        private GameObject _fruitHeap;
        [SerializeField] private int _fruitPerHit = 7;
        [SerializeField] private int _maxFruit = 16;
        [SerializeField] private float _despawnSeconds = 45f;
        [SerializeField, Tooltip("Spawn box above the crate (local).")]
        private Bounds _spawnBox = new Bounds(new Vector3(0f, 0.55f, 0f), new Vector3(0.45f, 0.15f, 0.3f));
        [SerializeField] private string _spillCue = "SFX_FRUIT_SPILL";
        [SerializeField] private string _impactCue = "SFX_IMPACT_FRUIT";

        private int _spawned;
        private bool _spilled;
        private SaveRegistration _save;
        private ServiceRef<IAudioService> _audio;
        private uint _rng = 0x9E3779B9u;

        /// <summary>Marker id ("B3.Fruit3").</summary>
        public string Id
        {
            get
            {
                var m = GetComponent<SceneMarker>();
                return m != null ? m.Id : name;
            }
        }

        /// <summary>True after the first spill.</summary>
        public bool Spilled => _spilled;

        /// <inheritdoc/>
        public Team Team => Team.None;
        /// <inheritdoc/>
        public bool IsAlive => _spawned < _maxFruit;
        /// <inheritdoc/>
        public Transform AimPoint => transform;

        /// <summary>Configures the crate (editor builders).</summary>
        public void Configure(GameObject fruitPrefab, GameObject heap, int perHit, int max)
        {
            _fruitPrefab = fruitPrefab;
            _fruitHeap = heap;
            _fruitPerHit = perHit;
            _maxFruit = max;
        }

        private void Awake()
        {
            _save = new SaveRegistration(this);
            _rng ^= (uint)Id.GetHashCode();
        }

        private void Start()
        {
            _save.TryRegister();
            if (_fruitPrefab != null) Pools.Prewarm(_fruitPrefab, Mathf.Min(_maxFruit, 12));
        }

        private void OnEnable()
        {
            GameEvents.DestructibleDamaged += OnDestructibleDamaged;
            GameEvents.CheckpointLoaded += OnCheckpointLoaded;
        }

        private void OnDisable()
        {
            GameEvents.DestructibleDamaged -= OnDestructibleDamaged;
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
        }

        private void OnDestroy() => _save?.Unregister();

        private void OnCheckpointLoaded(string id) => _save.TryRegister();

        private void OnDestructibleDamaged(string id, float health01)
        {
            if (id == Id) Spill(transform.position + Vector3.up * 0.5f, Vector3.down);
        }

        /// <inheritdoc/>
        public void ApplyDamage(in DamageInfo d) => Spill(d.Point, d.Direction);

        /// <summary>Knocks fruit out of the crate (impact point and bullet direction drive the impulse).</summary>
        public void Spill(Vector3 point, Vector3 direction)
        {
            if (_spawned >= _maxFruit) return;
            var audio = _audio.Value;
            audio?.Play(_spilled ? _impactCue : _spillCue, point);
            if (!_spilled && _fruitHeap != null) _fruitHeap.SetActive(false);
            _spilled = true;
            if (_fruitPrefab == null) return;
            var count = Mathf.Min(_fruitPerHit, _maxFruit - _spawned);
            var push = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.forward;
            for (var i = 0; i < count; i++)
            {
                var local = _spawnBox.center + Vector3.Scale(_spawnBox.extents, new Vector3(Rand() * 2f - 1f, Rand() * 2f - 1f, Rand() * 2f - 1f));
                var pos = transform.TransformPoint(local);
                var go = Pools.Spawn(_fruitPrefab, pos, Random3(), _despawnSeconds);
                if (go == null) continue;
                _spawned++;
                var rb = go.GetComponent<Rigidbody>();
                if (rb == null) continue;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                var outward = (pos - transform.position);
                outward.y = 0f;
                var impulse = push * (1.4f + Rand() * 1.6f) + outward.normalized * (0.6f + Rand()) + Vector3.up * (0.8f + Rand() * 1.2f);
                rb.AddForce(impulse * rb.mass, ForceMode.Impulse);
                rb.AddTorque(new Vector3(Rand() - 0.5f, Rand() - 0.5f, Rand() - 0.5f) * 0.02f, ForceMode.Impulse);
            }
        }

        private float Rand()
        {
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return (_rng & 0xFFFFFF) / 16777215f;
        }

        private Quaternion Random3() => Quaternion.Euler(Rand() * 360f, Rand() * 360f, Rand() * 360f);

        /// <inheritdoc/>
        public string SaveKey => "FruitCrate." + Id;

        /// <inheritdoc/>
        public void Capture(SaveWriter w)
        {
            w.Write("spilled", _spilled);
            w.Write("spawned", _spawned);
        }

        /// <inheritdoc/>
        public void Restore(SaveReader r)
        {
            _spilled = r.ReadBool("spilled", false);
            _spawned = r.ReadInt("spawned", 0);
            if (_fruitHeap != null) _fruitHeap.SetActive(!_spilled);
        }
    }
}
