using LongShadow.Cinematics.Booth;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>
    /// Moves a bound transform along a Catmull-Rom path over the clip (CINEMATIC_DIRECTION §2): the helicopter on
    /// INS.Heli.Approach/Depart with banking, cruise pitch and the nose-up flare over the last 25 m, the cast's
    /// crouch-run up the reverse slope snapped to the ground, the interrogator's walk past the chair. Knots are world
    /// or booth-local; a sub-range of the path can be played; heights are lifted to a clearance above the terrain.
    /// </summary>
    [System.Serializable]
    public sealed class TransformPathClip : PlayableAsset, ITimelineClipAsset
    {
        /// <summary>How the mover is oriented.</summary>
        public enum Orient
        {
            /// <summary>Keep the rotation.</summary>
            None,
            /// <summary>Yaw along the path (people).</summary>
            Yaw,
            /// <summary>Yaw, bank into turns, pitch for speed and the landing flare (helicopter).</summary>
            Aircraft,
        }

        [SerializeField] private Vector3[] _knots = new Vector3[0];
        [SerializeField] private bool _boothFrame;
        [SerializeField, Range(0f, 1f)] private float _from;
        [SerializeField, Range(0f, 1f)] private float _to = 1f;
        [SerializeField] private ShotEase _ease = ShotEase.Linear;
        [SerializeField] private Orient _orient = Orient.Yaw;
        [SerializeField, Tooltip("Metres to the right of the path (formation offsets).")] private float _lateral;
        [SerializeField] private bool _groundSnap;
        [SerializeField, Tooltip("Minimum height above the ground (flying), 0 = none.")] private float _clearance;
        [SerializeField] private bool _flare;
        [SerializeField] private float _yawOffset;

        [System.NonSerialized] private PathCurve _curve;

        /// <inheritdoc/>
        public ClipCaps clipCaps => ClipCaps.None;
        /// <summary>Path knots.</summary>
        public Vector3[] Knots
        {
            get => _knots;
            set
            {
                _knots = value ?? new Vector3[0];
                _curve = null;
            }
        }
        /// <summary>Knots are booth-local.</summary>
        public bool BoothFrame { get => _boothFrame; set => _boothFrame = value; }
        /// <summary>Path fraction at the clip start.</summary>
        public float FromT { get => _from; set => _from = value; }
        /// <summary>Path fraction at the clip end.</summary>
        public float ToT { get => _to; set => _to = value; }
        /// <summary>Progress easing.</summary>
        public ShotEase Ease { get => _ease; set => _ease = value; }
        /// <summary>Orientation mode.</summary>
        public Orient Orientation { get => _orient; set => _orient = value; }
        /// <summary>Lateral offset (m).</summary>
        public float Lateral { get => _lateral; set => _lateral = value; }
        /// <summary>Snap to the ground.</summary>
        public bool GroundSnap { get => _groundSnap; set => _groundSnap = value; }
        /// <summary>Minimum clearance above the ground (m).</summary>
        public float Clearance { get => _clearance; set => _clearance = value; }
        /// <summary>Landing flare at the end of the path.</summary>
        public bool Flare { get => _flare; set => _flare = value; }
        /// <summary>Extra yaw in degrees.</summary>
        public float YawOffset { get => _yawOffset; set => _yawOffset = value; }

        /// <summary>The curve (built on first use).</summary>
        public PathCurve Curve => _curve ?? (_curve = new PathCurve(_knots));

        /// <inheritdoc/>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<TransformPathBehaviour>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }

        /// <summary>Pose at normalised clip time (world space).</summary>
        public void Evaluate(float t01, float clipSeconds, Quaternion current, out Vector3 position, out Quaternion rotation)
        {
            var curve = Curve;
            var k = Mathf.Lerp(_from, _to, Easing.Evaluate(_ease, t01));
            var p = curve.Sample(k);
            var tangent = curve.Tangent(k);
            var flat = new Vector3(tangent.x, 0f, tangent.z);
            if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
            flat.Normalize();
            var right = Vector3.Cross(Vector3.up, flat);
            p += right * _lateral;
            if (_boothFrame)
            {
                var booth = BoothRoot.Current;
                p = booth != null ? booth.ToWorld(p) : BoothLayout.WorldOrigin + p;
            }
            if (_groundSnap || _clearance > 0f)
            {
                if (Physics.Raycast(p + Vector3.up * 300f, Vector3.down, out var hit, 700f, 1 << LayerIds.Default, QueryTriggerInteraction.Ignore))
                {
                    if (_groundSnap) p.y = hit.point.y;
                    else
                    {
                        var clearance = _clearance;
                        if (_flare)
                        {
                            var toEnd = (1f - k) * curve.Length;
                            clearance *= Mathf.Clamp01(toEnd / (InsertionFlareMetres * 2f));
                        }
                        p.y = Mathf.Max(p.y, hit.point.y + clearance);
                    }
                }
            }
            position = p;

            switch (_orient)
            {
                case Orient.Yaw:
                    rotation = Quaternion.LookRotation(flat, Vector3.up) * Quaternion.Euler(0f, _yawOffset, 0f);
                    break;
                case Orient.Aircraft:
                    var dk = Mathf.Abs(_to - _from) * DerivativeOfEase(_ease, t01);
                    var speed = clipSeconds > 0f ? curve.Length * dk / clipSeconds : 0f;
                    var bank = Mathf.Clamp(-Mathf.Atan(speed * speed * curve.TurnRate(k) / 9.81f) * Mathf.Rad2Deg, -25f, 25f);
                    var pitch = Mathf.Clamp(speed * 0.35f, 0f, 9f); // nose down with speed
                    if (_flare)
                    {
                        var toEnd = (1f - k) * curve.Length;
                        if (toEnd < InsertionFlareMetres)
                        {
                            var f = 1f - toEnd / InsertionFlareMetres;
                            pitch = Mathf.Lerp(pitch, -14f, Mathf.Sin(Mathf.Clamp01(f) * Mathf.PI * 0.85f));
                            bank *= 1f - f;
                        }
                    }
                    rotation = Quaternion.LookRotation(flat, Vector3.up) * Quaternion.Euler(pitch, _yawOffset, bank);
                    break;
                default:
                    rotation = current;
                    break;
            }
        }

        /// <summary>Flare distance (CINEMATIC_DIRECTION §2: nose-up flare over the last 25 m).</summary>
        public const float InsertionFlareMetres = 25f;

        private static float DerivativeOfEase(ShotEase ease, float t)
        {
            const float h = 0.01f;
            var a = Easing.Evaluate(ease, Mathf.Clamp01(t - h));
            var b = Easing.Evaluate(ease, Mathf.Clamp01(t + h));
            var span = Mathf.Clamp01(t + h) - Mathf.Clamp01(t - h);
            return span > 0f ? (b - a) / span : 1f;
        }
    }

    /// <summary>Per-clip behaviour of a path move.</summary>
    internal sealed class TransformPathBehaviour : PlayableBehaviour
    {
        public TransformPathClip Clip;
    }
}
