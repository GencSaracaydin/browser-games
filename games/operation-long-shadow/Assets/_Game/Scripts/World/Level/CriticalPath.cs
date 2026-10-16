using System;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Level.Data;
using UnityEngine;

namespace LongShadow.World.Level
{
    /// <summary>
    /// <see cref="ICriticalPath"/> (TECH_PLAN §7.15, Appendix A "Critical-path knots"): the beat polylines that drive
    /// LightStage progress (GDD §9.1) and squad follow (WP04). <see cref="Progress01"/> is the normalised arc length of a
    /// position's projection onto the beat's polyline and never decreases within a beat; the per-beat best values are part
    /// of the checkpoint snapshot (key "CriticalPath") so a reload restores them. Beat 11 progress is the exfil spline time
    /// / 50 s (the <c>B11.Exfil</c> <see cref="ISplineVehicle"/>). Lives in <c>S9_TownFar</c> (never unloaded) and registers
    /// itself in <see cref="GameServices"/>.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.LightStage - 10)]
    public sealed class CriticalPath : MonoBehaviour, ICriticalPath, ISaveable
    {
        [Serializable]
        private sealed class BeatPolyline
        {
            public int Beat;
            public Vector3[] Knots = new Vector3[0];
        }

        [SerializeField, Tooltip("Beat polylines (filled from CriticalPathTable by the S9 builder; empty = use the code table).")]
        private BeatPolyline[] _beats = new BeatPolyline[0];
        [SerializeField, Tooltip("Positions further than this from a beat's polyline do not advance its progress (shortcuts through out-of-bounds).")]
        private float _maxLateralDistance = 40f;

        private readonly float[] _best = new float[CriticalPathTable.BeatCount + 1];
        private readonly float[] _lengths = new float[CriticalPathTable.BeatCount + 1];
        private Vector3[][] _knots;
        private ServiceRef<ICheckpointService> _checkpoints;
        private ServiceRef<IMarkerRegistry> _markers;
        private ISplineVehicle _exfil;
        private bool _registeredSave;

        /// <summary>Raised the first time a beat's progress increases by at least 1 % (debug overlays, tests).</summary>
        public event Action<int, float> ProgressChanged;

        private void Awake()
        {
            EnsureKnots();
            GameServices.Register<ICriticalPath>(this);
        }

        private void Start() => TryRegisterSave();

        private void OnEnable() => GameEvents.CheckpointLoaded += OnCheckpointLoaded;

        private void OnDisable() => GameEvents.CheckpointLoaded -= OnCheckpointLoaded;

        private void OnDestroy()
        {
            GameServices.Unregister<ICriticalPath>(this);
            if (_registeredSave) _checkpoints.Value?.Unregister(this);
        }

        private void TryRegisterSave()
        {
            if (_registeredSave) return;
            var cp = _checkpoints.Value;
            if (cp == null) return;
            cp.Register(this);
            _registeredSave = true;
        }

        private void OnCheckpointLoaded(string cpId) => TryRegisterSave();

        /// <summary>Fills the polylines (editor builders); null or empty uses <see cref="CriticalPathTable"/>.</summary>
        public void Configure(IReadOnlyDictionary<int, Vector3[]> beats)
        {
            var list = new List<BeatPolyline>();
            if (beats != null)
                foreach (var kv in beats)
                    if (kv.Value != null && kv.Value.Length >= 2) list.Add(new BeatPolyline { Beat = kv.Key, Knots = kv.Value });
            _beats = list.ToArray();
            _knots = null;
            EnsureKnots();
        }

        private void EnsureKnots()
        {
            if (_knots != null) return;
            _knots = new Vector3[CriticalPathTable.BeatCount + 1][];
            for (var b = 1; b <= CriticalPathTable.BeatCount; b++) _knots[b] = CriticalPathTable.Knots(b);
            if (_beats != null)
                foreach (var bp in _beats)
                    if (bp != null && bp.Beat >= 1 && bp.Beat <= CriticalPathTable.BeatCount && bp.Knots != null && bp.Knots.Length >= 2)
                        _knots[bp.Beat] = bp.Knots;
            for (var b = 1; b <= CriticalPathTable.BeatCount; b++) _lengths[b] = _knots[b] != null ? MathX.PolylineLength(_knots[b]) : 0f;
        }

        /// <inheritdoc/>
        public float Progress01(Vector3 position, int beat)
        {
            if (beat < 1 || beat > CriticalPathTable.BeatCount) return 0f;
            EnsureKnots();
            float p;
            if (beat == 11) p = ExfilProgress();
            else
            {
                var knots = _knots[beat];
                if (knots == null || _lengths[beat] <= 1e-3f) return _best[beat];
                var closest = MathX.ProjectOnPolyline(knots, position, out var along, out var total);
                if ((closest - position).sqrMagnitude > _maxLateralDistance * _maxLateralDistance) return _best[beat];
                p = total > 0f ? Mathf.Clamp01(along / total) : 0f;
            }
            if (p > _best[beat])
            {
                var notify = p - _best[beat] >= 0.01f || p >= 1f;
                _best[beat] = p;
                if (notify) ProgressChanged?.Invoke(beat, p);
            }
            return _best[beat];
        }

        /// <inheritdoc/>
        public Vector3 PointAt(int beat, float t01)
        {
            EnsureKnots();
            if (beat == 11)
            {
                var v = Exfil();
                return v != null && v.Root != null ? v.Root.position : Vector3.zero;
            }
            if (beat < 1 || beat > CriticalPathTable.BeatCount || _knots[beat] == null) return Vector3.zero;
            return MathX.PointAlongPolyline(_knots[beat], Mathf.Clamp01(t01) * _lengths[beat]);
        }

        /// <summary>Length of a beat's polyline in metres (0 for Beat 11).</summary>
        public float Length(int beat)
        {
            EnsureKnots();
            return beat >= 1 && beat <= CriticalPathTable.BeatCount ? _lengths[beat] : 0f;
        }

        /// <summary>Best progress reached so far in a beat (no update).</summary>
        public float Best(int beat) => beat >= 1 && beat <= CriticalPathTable.BeatCount ? _best[beat] : 0f;

        /// <summary>Forgets every beat's progress (new mission).</summary>
        public void ResetProgress()
        {
            for (var i = 0; i < _best.Length; i++) _best[i] = 0f;
        }

        private float ExfilProgress()
        {
            var v = Exfil();
            return v != null ? Mathf.Clamp01(v.Elapsed / WorldConst.ExfilSeconds) : 0f;
        }

        private ISplineVehicle Exfil()
        {
            if (_exfil is UnityEngine.Object o && o == null) _exfil = null;
            if (_exfil != null) return _exfil;
            var reg = _markers.Value;
            if (reg != null && reg.TryGet(VehicleRoutes.Exfil, out SceneMarker m) && m != null) _exfil = m.GetComponent<ISplineVehicle>();
            return _exfil;
        }

        /// <inheritdoc/>
        public string SaveKey => "CriticalPath";

        /// <inheritdoc/>
        public void Capture(SaveWriter w)
        {
            for (var b = 1; b <= CriticalPathTable.BeatCount; b++) w.Write("b" + b, _best[b]);
        }

        /// <inheritdoc/>
        public void Restore(SaveReader r)
        {
            for (var b = 1; b <= CriticalPathTable.BeatCount; b++) _best[b] = r.ReadFloat("b" + b, 0f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            EnsureKnots();
            for (var b = 1; b <= CriticalPathTable.BeatCount; b++)
            {
                var k = _knots[b];
                if (k == null) continue;
                Gizmos.color = Color.HSVToRGB(b / 11f, 0.8f, 1f);
                for (var i = 1; i < k.Length; i++) Gizmos.DrawLine(k[i - 1] + Vector3.up * 0.3f, k[i] + Vector3.up * 0.3f);
            }
        }
#endif
    }
}
