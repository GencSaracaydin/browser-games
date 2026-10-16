using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Level.Data;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace LongShadow.World.Level
{
    /// <summary>
    /// <see cref="ISplineVehicle"/> (TECH_PLAN §7.15): a vehicle root that rides a <see cref="SplineContainer"/> with
    /// authored timing — constant speed inside each segment for routes with per-segment seconds (B11 exfil 8/8/10/10/8/6 s,
    /// matching WP04's <c>ExfilFlightPath.SampleSpline(constantSpeed: true)</c>), or a constant speed (B11 trucks 4.5 m/s) —
    /// with a velocity-continuous deceleration into the last knot. Helicopters bank into turns and pitch with
    /// acceleration; trucks yaw only. Several vehicles may share one root (Magpie rides <c>B10.HeliApproach</c>,
    /// <c>B10.HeliWaveOff</c> and <c>B11.Exfil</c>): playing one stops the others. Found by id through the
    /// <see cref="SceneMarker"/> on the same GameObject (WP04: <c>MarkerRegistry.TryGet(id).GetComponent&lt;ISplineVehicle&gt;()</c>).
    /// Seats are child transforms of the root (seat 0 of a truck = tailgate dismount point, WP04).
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public sealed class SplineVehicle : MonoBehaviour, ISplineVehicle, ISaveable
    {
        [SerializeField, Tooltip("Id (Appendix A); empty = the SceneMarker id on this GameObject.")]
        private string _id;
        [SerializeField] private SplineContainer _container;
        [SerializeField, Tooltip("Transform that rides the spline (may be shared with other SplineVehicles).")]
        private Transform _root;
        [SerializeField, Tooltip("Seat / dismount points (children of the root).")]
        private Transform[] _seats = new Transform[0];
        [SerializeField, Tooltip("Seconds per segment (knot to knot); empty = constant speed.")]
        private float[] _segmentSeconds = new float[0];
        [SerializeField, Tooltip("Speed in m/s when no segment times are set.")]
        private float _speed = 5f;
        [SerializeField, Tooltip("Decelerate into the last knot (velocity-continuous).")]
        private bool _easeArrival = true;
        [SerializeField, Tooltip("Hold this compass heading at the end (helicopter wheels-down 270°).")]
        private bool _holdFinalHeading;
        [SerializeField] private float _finalHeading;
        [SerializeField, Tooltip("Offset from the spline point to the root in the vehicle frame (exfil knots are the cabin floor).")]
        private Vector3 _rootOffsetLocal;
        [SerializeField, Tooltip("Bank and pitch like a helicopter.")]
        private bool _aircraft;
        [SerializeField, Range(0f, 45f)] private float _maxBank = 22f;
        [SerializeField, Range(0f, 25f)] private float _maxPitch = 10f;
        [SerializeField, Tooltip("Vehicles following the lead along the same spline (B11 second truck).")]
        private Transform[] _followers = new Transform[0];
        [SerializeField, Tooltip("Metres each follower trails the lead.")]
        private float[] _followerSpacing = new float[0];
        [SerializeField, Tooltip("GameEvents.MissionEvent that starts the vehicle (empty = scripts call Play).")]
        private string _playOnEvent;
        [SerializeField, Tooltip("Root renderers are hidden until this vehicle plays (vehicles that appear only in their beat).")]
        private bool _hideUntilPlay;
        [SerializeField, Tooltip("Pose the root at the spline start on Awake (the route that owns a shared root).")]
        private bool _poseOnAwake = true;

        private static readonly List<SplineVehicle> s_active = new List<SplineVehicle>();

        private float[] _curveLengths = new float[0];
        private float[] _cumulative = new float[0];
        private float[] _knotTimes = new float[0];
        private float _total;
        private float _duration;
        private float _elapsed;
        private float _distance;
        private float _overrideSpeed;
        private bool _moving;
        private bool _arrived;
        private bool _prepared;
        private Vector3 _lastPos;
        private float _lastYaw;
        private float _bank;
        private float _pitch;
        private float _speedNow;
        private Renderer[] _renderers;
        private ServiceRef<ICheckpointService> _checkpoints;
        private bool _saveRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_active.Clear();

        /// <inheritdoc/>
        public string Id
        {
            get
            {
                if (!string.IsNullOrEmpty(_id)) return _id;
                var m = GetComponent<SceneMarker>();
                return m != null ? m.Id : name;
            }
        }

        /// <inheritdoc/>
        public Transform Root => _root != null ? _root : transform;
        /// <inheritdoc/>
        public float Progress01 => _total > 0f ? Mathf.Clamp01(_distance / _total) : 0f;
        /// <inheritdoc/>
        public float Elapsed => _elapsed;
        /// <inheritdoc/>
        public bool IsMoving => _moving;
        /// <inheritdoc/>
        public bool Arrived => _arrived;
        /// <inheritdoc/>
        public int SeatCount => _seats != null ? _seats.Length : 0;
        /// <summary>Authored duration in seconds.</summary>
        public float Duration
        {
            get
            {
                Prepare();
                return _duration;
            }
        }
        /// <summary>Spline length in metres.</summary>
        public float Length
        {
            get
            {
                Prepare();
                return _total;
            }
        }
        /// <summary>Current speed in m/s.</summary>
        public float CurrentSpeed => _speedNow;

        /// <inheritdoc/>
        public Transform GetSeat(int index) => _seats != null && index >= 0 && index < _seats.Length ? _seats[index] : null;

        /// <summary>Configures the vehicle (editor builders).</summary>
        public void Configure(string id, SplineContainer container, Transform root, Transform[] seats, float[] segmentSeconds, float speed,
                              bool easeArrival, float? finalHeading, Vector3 rootOffsetLocal, bool aircraft, string playOnEvent, bool hideUntilPlay,
                              bool poseOnAwake)
        {
            _id = id;
            _container = container;
            _root = root;
            _seats = seats ?? new Transform[0];
            _segmentSeconds = segmentSeconds ?? new float[0];
            _speed = speed;
            _easeArrival = easeArrival;
            _holdFinalHeading = finalHeading.HasValue;
            _finalHeading = finalHeading ?? 0f;
            _rootOffsetLocal = rootOffsetLocal;
            _aircraft = aircraft;
            _playOnEvent = playOnEvent;
            _hideUntilPlay = hideUntilPlay;
            _poseOnAwake = poseOnAwake;
            _prepared = false;
        }

        /// <summary>Sets followers that ride the same spline behind the lead (editor builders).</summary>
        public void SetFollowers(Transform[] followers, float[] spacing)
        {
            _followers = followers ?? new Transform[0];
            _followerSpacing = spacing ?? new float[0];
        }

        private void Awake()
        {
            if (_container == null) _container = GetComponent<SplineContainer>();
            Prepare();
            _renderers = Root.GetComponentsInChildren<Renderer>(true);
            if (_poseOnAwake) ApplyPose(0f, true);
            if (_hideUntilPlay) SetVisible(false);
        }

        private void OnEnable()
        {
            if (!s_active.Contains(this)) s_active.Add(this);
            GameEvents.MissionEvent += OnMissionEvent;
            GameEvents.CheckpointLoaded += OnCheckpointLoaded;
        }

        private void OnDisable()
        {
            s_active.Remove(this);
            GameEvents.MissionEvent -= OnMissionEvent;
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
        }

        private void Start() => RegisterSave();

        private void OnDestroy()
        {
            if (_saveRegistered) _checkpoints.Value?.Unregister(this);
        }

        private void RegisterSave()
        {
            if (_saveRegistered) return;
            var cp = _checkpoints.Value;
            if (cp == null) return;
            cp.Register(this);
            _saveRegistered = true;
        }

        private void OnCheckpointLoaded(string id) => RegisterSave();

        private void OnMissionEvent(string id)
        {
            if (!string.IsNullOrEmpty(_playOnEvent) && id == _playOnEvent && !_moving) Play();
        }

        /// <inheritdoc/>
        public void Play(float speed = 0f)
        {
            Prepare();
            if (_total <= 0f)
            {
                LSLog.WarnOnce("splinevehicle-empty:" + Id, LogCategory.Level, "SplineVehicle " + Id + " has no spline; Play ignored.", this);
                return;
            }
            if (_arrived)
            {
                _arrived = false;
                _distance = 0f;
                _elapsed = 0f;
            }
            _overrideSpeed = Mathf.Max(0f, speed);
            // Only one vehicle drives a shared root at a time.
            for (var i = 0; i < s_active.Count; i++)
            {
                var other = s_active[i];
                if (other != null && other != this && other.Root == Root && other._moving) other.Stop();
            }
            _moving = true;
            SetVisible(true);
            _lastPos = Root.position;
        }

        /// <inheritdoc/>
        public void Stop() => _moving = false;

        /// <inheritdoc/>
        public void SetProgress(float t01)
        {
            Prepare();
            t01 = Mathf.Clamp01(t01);
            _distance = t01 * _total;
            _elapsed = TimeAtDistance(_distance);
            _arrived = t01 >= 1f;
            ApplyPose(_distance, true);
        }

        /// <summary>Normalised progress at which knot <paramref name="index"/> lies (B10 approach: start of the last 15 s).</summary>
        public float KnotProgress(int index)
        {
            Prepare();
            if (_cumulative.Length == 0 || _total <= 0f) return 0f;
            index = Mathf.Clamp(index, 0, _cumulative.Length - 1);
            return _cumulative[index] / _total;
        }

        private void Update()
        {
            if (!_moving) return;
            var dt = Time.deltaTime;
            if (dt <= 0f) return;
            _elapsed += dt;
            if (_overrideSpeed > 0f) _distance = Mathf.Min(_total, _distance + _overrideSpeed * dt);
            else _distance = DistanceAtTime(_elapsed);
            ApplyPose(_distance, false);
            if (_distance >= _total - 1e-3f)
            {
                _distance = _total;
                _moving = false;
                _arrived = true;
                _speedNow = 0f;
            }
        }

        // ------------------------------------------------------------------ timing

        private void Prepare()
        {
            if (_prepared) return;
            _prepared = true;
            if (_container == null) _container = GetComponent<SplineContainer>();
            var spline = _container != null ? _container.Spline : null;
            if (spline == null || spline.Count < 2)
            {
                _curveLengths = new float[0];
                _cumulative = new float[0];
                _knotTimes = new float[0];
                _total = 0f;
                _duration = 0f;
                return;
            }
            var curves = spline.Count - 1;
            _curveLengths = new float[curves];
            _cumulative = new float[curves + 1];
            for (var i = 0; i < curves; i++)
            {
                _curveLengths[i] = CurveUtility.CalculateLength(spline.GetCurve(i), 64);
                _cumulative[i + 1] = _cumulative[i] + _curveLengths[i];
            }
            _total = _cumulative[curves];
            _knotTimes = new float[curves + 1];
            if (_segmentSeconds != null && _segmentSeconds.Length >= curves)
            {
                for (var i = 0; i < curves; i++) _knotTimes[i + 1] = _knotTimes[i] + Mathf.Max(0.05f, _segmentSeconds[i]);
            }
            else
            {
                var v = Mathf.Max(0.1f, _speed);
                for (var i = 0; i < curves; i++) _knotTimes[i + 1] = _knotTimes[i] + _curveLengths[i] / v;
            }
            _duration = _knotTimes[curves];
        }

        /// <summary>Distance along the spline at a time since the start (authored timing).</summary>
        public float DistanceAtTime(float t)
        {
            Prepare();
            var n = _curveLengths.Length;
            if (n == 0) return 0f;
            if (t <= 0f) return 0f;
            if (t >= _duration) return _total;
            var i = 0;
            while (i < n - 1 && t > _knotTimes[i + 1]) i++;
            var segT = _knotTimes[i + 1] - _knotTimes[i];
            var tau = segT > 0f ? Mathf.Clamp01((t - _knotTimes[i]) / segT) : 1f;
            var len = _curveLengths[i];
            if (_easeArrival && i == n - 1)
            {
                // Hermite in time: incoming speed of the previous segment (or 1.5x the average), zero speed at the end.
                var vIn = i > 0 ? _curveLengths[i - 1] / Mathf.Max(0.05f, _knotTimes[i] - _knotTimes[i - 1]) : 1.5f * len / Mathf.Max(0.05f, segT);
                var m0 = Mathf.Min(vIn * segT, 3f * len);
                var t2 = tau * tau;
                var t3 = t2 * tau;
                var d = (t3 - 2f * t2 + tau) * m0 + (-2f * t3 + 3f * t2) * len;
                return _cumulative[i] + Mathf.Clamp(d, 0f, len);
            }
            return _cumulative[i] + len * tau;
        }

        /// <summary>Time at which a distance is reached (inverse of <see cref="DistanceAtTime"/> by bisection).</summary>
        public float TimeAtDistance(float distance)
        {
            Prepare();
            if (distance <= 0f) return 0f;
            if (distance >= _total) return _duration;
            float lo = 0f, hi = _duration;
            for (var k = 0; k < 40; k++)
            {
                var mid = (lo + hi) * 0.5f;
                if (DistanceAtTime(mid) < distance) lo = mid;
                else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        // ------------------------------------------------------------------ pose

        /// <summary>World position and tangent at a distance along the spline.</summary>
        public void Evaluate(float distance, out Vector3 position, out Vector3 tangent)
        {
            Prepare();
            var spline = _container != null ? _container.Spline : null;
            if (spline == null || _curveLengths.Length == 0)
            {
                position = transform.position;
                tangent = transform.forward;
                return;
            }
            var n = _curveLengths.Length;
            var tr = _container.transform;
            if (distance < 0f)
            {
                // Before the first knot: extrapolate along the first tangent (followers that start behind the lead).
                var c0 = spline.GetCurve(0);
                var p0 = (Vector3)CurveUtility.EvaluatePosition(c0, 0f);
                var t0 = ((Vector3)CurveUtility.EvaluateTangent(c0, 0f)).normalized;
                position = tr.TransformPoint(p0 + t0 * distance);
                tangent = tr.TransformDirection(t0);
                return;
            }
            var i = 0;
            while (i < n - 1 && distance > _cumulative[i + 1]) i++;
            var curve = spline.GetCurve(i);
            var local = Mathf.Clamp(distance - _cumulative[i], 0f, _curveLengths[i]);
            var u = CurveUtility.GetDistanceToInterpolation(curve, local);
            float3 p = CurveUtility.EvaluatePosition(curve, u);
            float3 t = CurveUtility.EvaluateTangent(curve, u);
            if (math.lengthsq(t) < 1e-8f) t = curve.P3 - curve.P0;
            position = tr.TransformPoint((Vector3)p);
            tangent = tr.TransformDirection(((Vector3)t).normalized);
        }

        private void ApplyPose(float distance, bool snap)
        {
            Evaluate(distance, out var pos, out var tan);
            var flat = new Vector3(tan.x, 0f, tan.z);
            var yaw = flat.sqrMagnitude > 1e-6f ? MathX.BearingOf(flat) : _lastYaw;
            if (_holdFinalHeading && _total > 0f && _cumulative.Length > 1)
            {
                // Blend into the final heading over the last segment.
                var lastStart = _cumulative[_cumulative.Length - 2];
                var w = Mathf.InverseLerp(lastStart, _total, distance);
                w = w * w * (3f - 2f * w);
                yaw = Mathf.LerpAngle(yaw, _finalHeading, w);
            }
            var dt = Mathf.Max(1e-4f, Time.deltaTime);
            var root = Root;
            if (_aircraft && !snap)
            {
                var yawRate = Mathf.DeltaAngle(_lastYaw, yaw) / dt;
                var speed = (pos - _lastPos).magnitude / dt;
                var accel = (speed - _speedNow) / dt;
                var targetBank = Mathf.Clamp(-yawRate * speed * 0.012f, -_maxBank, _maxBank);
                var targetPitch = Mathf.Clamp(accel * 0.9f, -_maxPitch, _maxPitch);
                _bank = MathX.Damp(_bank, targetBank, 2.5f, dt);
                _pitch = MathX.Damp(_pitch, targetPitch, 2.0f, dt);
                _speedNow = speed;
            }
            else if (!snap)
            {
                _speedNow = (pos - _lastPos).magnitude / dt;
            }
            var rot = Quaternion.Euler(_aircraft ? _pitch : 0f, yaw, _aircraft ? _bank : 0f);
            root.SetPositionAndRotation(pos + rot * _rootOffsetLocal, rot);
            _lastPos = pos;
            _lastYaw = yaw;
            if (_followers == null) return;
            for (var f = 0; f < _followers.Length; f++)
            {
                var follower = _followers[f];
                if (follower == null) continue;
                var spacing = f < _followerSpacing.Length ? _followerSpacing[f] : 12f * (f + 1);
                var fd = Mathf.Min(distance - spacing, _total - spacing);
                Evaluate(fd, out var fp, out var ft);
                var fflat = new Vector3(ft.x, 0f, ft.z);
                var fyaw = fflat.sqrMagnitude > 1e-6f ? MathX.BearingOf(fflat) : yaw;
                var frot = Quaternion.Euler(0f, fyaw, 0f);
                follower.SetPositionAndRotation(fp + frot * _rootOffsetLocal, frot);
            }
        }

        private void SetVisible(bool visible)
        {
            if (!_hideUntilPlay || _renderers == null) return;
            foreach (var r in _renderers)
                if (r != null) r.enabled = visible;
        }

        // ------------------------------------------------------------------ save

        /// <inheritdoc/>
        public string SaveKey => "SplineVehicle." + Id;

        /// <inheritdoc/>
        public void Capture(SaveWriter w)
        {
            w.Write("distance", _distance);
            w.Write("elapsed", _elapsed);
            w.Write("moving", _moving);
            w.Write("arrived", _arrived);
            w.Write("speed", _overrideSpeed);
        }

        /// <inheritdoc/>
        public void Restore(SaveReader r)
        {
            Prepare();
            _distance = r.ReadFloat("distance", 0f);
            _elapsed = r.ReadFloat("elapsed", 0f);
            _moving = r.ReadBool("moving", false);
            _arrived = r.ReadBool("arrived", false);
            _overrideSpeed = r.ReadFloat("speed", 0f);
            ApplyPose(_distance, true);
            SetVisible(_moving || _arrived || !_hideUntilPlay);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Prepare();
            if (_total <= 0f) return;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
            var prev = Vector3.zero;
            for (var i = 0; i <= 64; i++)
            {
                Evaluate(_total * i / 64f, out var p, out _);
                if (i > 0) Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
#endif
    }
}
