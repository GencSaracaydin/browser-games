using LongShadow.Core;
using LongShadow.World.Level.Data;
using UnityEngine;

namespace LongShadow.World.Level
{
    /// <summary>
    /// A market awning that tears when a tracer crosses it (GDD §7.3 G3, TECH_PLAN §7.15): listens to
    /// <see cref="GameEvents.TracerPath"/>, tests the segment against the panel's local box, swaps the intact cloth for the
    /// torn mesh, enables its sun-shaft card (AW-06…08 under the Sunset Lane band) and raises
    /// <see cref="LevelEvents.AwningTorn"/> on the first tear of the level so WP07 can play D023. The torn state is part
    /// of the checkpoint snapshot. Ids <c>B3.AW01</c>…<c>B3.AW08</c> sit on the <see cref="SceneMarker"/> of the same object.
    /// </summary>
    public sealed class AwningPanel : MonoBehaviour, ISaveable
    {
        [SerializeField] private GameObject _intact;
        [SerializeField] private GameObject _torn;
        [SerializeField, Tooltip("Sun-shaft card + dust motes enabled once torn (only where the shadow map says sun).")]
        private GameObject _shaft;
        [SerializeField, Tooltip("Local bounds of the cloth used for the tracer hit test.")]
        private Bounds _localBounds = new Bounds(new Vector3(0f, -0.5f, 1.1f), new Vector3(3f, 1.2f, 2.2f));
        [SerializeField, Tooltip("Tracers from these teams tear it (default: everyone).")]
        private bool _anyTeam = true;
        [SerializeField] private string _tearCue = "SFX_AWNING_TEAR";

        private static bool s_firstTearRaised;
        private bool _isTorn;
        private SaveRegistration _save;
        private ServiceRef<IAudioService> _audio;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_firstTearRaised = false;

        /// <summary>True once torn.</summary>
        public bool IsTorn => _isTorn;

        /// <summary>Id of the marker on this object ("B3.AW03").</summary>
        public string Id
        {
            get
            {
                var m = GetComponent<SceneMarker>();
                return m != null ? m.Id : name;
            }
        }

        /// <summary>Configures the panel (editor builders).</summary>
        public void Configure(GameObject intact, GameObject torn, GameObject shaft, Bounds localBounds)
        {
            _intact = intact;
            _torn = torn;
            _shaft = shaft;
            _localBounds = localBounds;
            Apply(false);
        }

        private void Awake()
        {
            _save = new SaveRegistration(this);
            Apply(_isTorn);
        }

        private void Start() => _save.TryRegister();

        private void OnEnable()
        {
            GameEvents.TracerPath += OnTracer;
            GameEvents.CheckpointLoaded += OnCheckpointLoaded;
        }

        private void OnDisable()
        {
            GameEvents.TracerPath -= OnTracer;
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
        }

        private void OnDestroy() => _save?.Unregister();

        private void OnCheckpointLoaded(string id)
        {
            _save.TryRegister();
            if (string.IsNullOrEmpty(id) || id == "CP1" || id == "CP2" || id == "CP3") s_firstTearRaised = _isTorn;
        }

        private void OnTracer(Vector3 from, Vector3 to, Team team)
        {
            if (_isTorn || (!_anyTeam && team != Team.Legion)) return;
            if (!SegmentHitsBounds(transform.InverseTransformPoint(from), transform.InverseTransformPoint(to), _localBounds)) return;
            Tear();
        }

        /// <summary>Tears the awning now (tracer, script or debug).</summary>
        public void Tear()
        {
            if (_isTorn) return;
            Apply(true);
            _audio.Value?.Play(_tearCue, transform.TransformPoint(_localBounds.center));
            if (!s_firstTearRaised)
            {
                s_firstTearRaised = true;
                GameEvents.RaiseMissionEvent(LevelEvents.AwningTorn);
            }
        }

        private void Apply(bool torn)
        {
            _isTorn = torn;
            if (_intact != null) _intact.SetActive(!torn);
            if (_torn != null) _torn.SetActive(torn);
            if (_shaft != null) _shaft.SetActive(torn);
        }

        /// <summary>Segment vs. axis-aligned box (slab test), in the box's space.</summary>
        public static bool SegmentHitsBounds(Vector3 a, Vector3 b, Bounds box)
        {
            var d = b - a;
            float tmin = 0f, tmax = 1f;
            for (var axis = 0; axis < 3; axis++)
            {
                var o = a[axis];
                var dir = d[axis];
                var min = box.min[axis];
                var max = box.max[axis];
                if (Mathf.Abs(dir) < 1e-6f)
                {
                    if (o < min || o > max) return false;
                    continue;
                }
                var inv = 1f / dir;
                var t1 = (min - o) * inv;
                var t2 = (max - o) * inv;
                if (t1 > t2)
                {
                    var tmp = t1;
                    t1 = t2;
                    t2 = tmp;
                }
                tmin = Mathf.Max(tmin, t1);
                tmax = Mathf.Min(tmax, t2);
                if (tmin > tmax) return false;
            }
            return true;
        }

        /// <inheritdoc/>
        public string SaveKey => "Awning." + Id;

        /// <inheritdoc/>
        public void Capture(SaveWriter w) => w.Write("torn", _isTorn);

        /// <inheritdoc/>
        public void Restore(SaveReader r) => Apply(r.ReadBool("torn", false));

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = _isTorn ? Color.red : new Color(1f, 0.7f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(_localBounds.center, _localBounds.size);
        }
#endif
    }
}
