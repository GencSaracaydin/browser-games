using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>
    /// One camera shot (CINEMATIC_DIRECTION §1.1): which camera (booth, cinematic world, player), a move from one
    /// <see cref="ShotPose"/> to another with easing, a physical lens (focal length on Super 35, f-stop, focus pull),
    /// operator sway, an impulse shake on the cut, dutch roll, camera motion blur and 2.39:1 letterbox. Hard cuts are
    /// clip boundaries; there is no camera blending.
    /// </summary>
    [System.Serializable]
    public sealed class CameraShotClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private string _shotName;
        [SerializeField] private CameraMode _rig = CameraMode.Booth;
        [SerializeField] private ShotPose _from = new ShotPose();
        [SerializeField] private ShotPose _to = new ShotPose();
        [SerializeField, Tooltip("Off: hold the From pose.")] private bool _move;
        [SerializeField] private ShotEase _ease = ShotEase.Smooth;
        [SerializeField, Tooltip("Focal length at the start in mm (0 = use the FOV fields).")] private float _focalFrom;
        [SerializeField] private float _focalTo;
        [SerializeField] private float _fovFrom = 50f;
        [SerializeField] private float _fovTo = 50f;
        [SerializeField] private bool _depthOfField = true;
        [SerializeField] private float _aperture = 2.8f;
        [SerializeField, Tooltip("Focus distance in m at the start (0 = distance to the look-at target).")] private float _focusFrom;
        [SerializeField] private float _focusTo;
        [SerializeField] private float _handheldDeg = HandheldNoise.BreathingDeg;
        [SerializeField] private float _handheldHz = HandheldNoise.BreathingHz;
        [SerializeField] private float _impulseDeg;
        [SerializeField] private float _rollFrom;
        [SerializeField] private float _rollTo;
        [SerializeField, Range(0f, 1f)] private float _motionBlur;
        [SerializeField] private bool _letterbox;
        [SerializeField, Tooltip("End on the player camera's FOV (hand-off shots).")] private bool _matchPlayerFov;

        /// <inheritdoc/>
        public ClipCaps clipCaps => ClipCaps.None;

        /// <summary>Shot label (shot table number, e.g. "I-7 low WS").</summary>
        public string ShotName { get => _shotName; set => _shotName = value; }
        /// <summary>Camera that renders the shot.</summary>
        public CameraMode Rig { get => _rig; set => _rig = value; }
        /// <summary>Start pose.</summary>
        public ShotPose From { get => _from; set => _from = value; }
        /// <summary>End pose (when <see cref="Move"/>).</summary>
        public ShotPose To { get => _to; set => _to = value; }
        /// <summary>Move from From to To over the clip.</summary>
        public bool Move { get => _move; set => _move = value; }
        /// <summary>Move easing.</summary>
        public ShotEase Ease { get => _ease; set => _ease = value; }
        /// <summary>Start focal length (mm, 0 = FOV).</summary>
        public float FocalFrom { get => _focalFrom; set => _focalFrom = value; }
        /// <summary>End focal length (mm, 0 = FOV).</summary>
        public float FocalTo { get => _focalTo; set => _focalTo = value; }
        /// <summary>Start vertical FOV when no focal length is given.</summary>
        public float FovFrom { get => _fovFrom; set => _fovFrom = value; }
        /// <summary>End vertical FOV when no focal length is given.</summary>
        public float FovTo { get => _fovTo; set => _fovTo = value; }
        /// <summary>Bokeh depth of field.</summary>
        public bool DepthOfField { get => _depthOfField; set => _depthOfField = value; }
        /// <summary>f-stop.</summary>
        public float Aperture { get => _aperture; set => _aperture = value; }
        /// <summary>Focus at the start (m, 0 = auto).</summary>
        public float FocusFrom { get => _focusFrom; set => _focusFrom = value; }
        /// <summary>Focus at the end (m, 0 = auto).</summary>
        public float FocusTo { get => _focusTo; set => _focusTo = value; }
        /// <summary>Operator sway amplitude (degrees).</summary>
        public float HandheldDeg { get => _handheldDeg; set => _handheldDeg = value; }
        /// <summary>Operator sway frequency (Hz).</summary>
        public float HandheldHz { get => _handheldHz; set => _handheldHz = value; }
        /// <summary>Impulse shake on the cut (degrees).</summary>
        public float ImpulseDeg { get => _impulseDeg; set => _impulseDeg = value; }
        /// <summary>Dutch roll at the start (degrees).</summary>
        public float RollFrom { get => _rollFrom; set => _rollFrom = value; }
        /// <summary>Dutch roll at the end (degrees).</summary>
        public float RollTo { get => _rollTo; set => _rollTo = value; }
        /// <summary>Camera motion blur 0..1.</summary>
        public float MotionBlur { get => _motionBlur; set => _motionBlur = value; }
        /// <summary>2.39:1 letterbox (third-person shots).</summary>
        public bool Letterbox { get => _letterbox; set => _letterbox = value; }
        /// <summary>End on the player camera's vertical FOV (intro I-12 lands on the gameplay view).</summary>
        public bool MatchPlayerFov { get => _matchPlayerFov; set => _matchPlayerFov = value; }

        /// <inheritdoc/>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<CameraShotBehaviour>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }

        /// <summary>
        /// World pose, vertical FOV, focal length and focus distance at normalised time <paramref name="t01"/>.
        /// Returns false when a frame or target could not be resolved (pose is best effort).
        /// </summary>
        public bool Evaluate(float t01, out Vector3 position, out Quaternion rotation, out float verticalFov,
                             out float focalMm, out float focus)
        {
            var k = _move ? Easing.Evaluate(_ease, t01) : 0f;
            var ok = ShotPoseResolver.ToWorld(_from, out var p0, out var r0);
            if (_move)
            {
                ok &= ShotPoseResolver.ToWorld(_to, out var p1, out var r1);
                position = Vector3.LerpUnclamped(p0, p1, k);
                rotation = Quaternion.Slerp(r0, r1, k);
                var lookId = !string.IsNullOrEmpty(_to.LookAtId) ? _to.LookAtId : _from.LookAtId;
                if (!string.IsNullOrEmpty(lookId))
                {
                    var t = CinematicBindings.ResolveTransform(lookId);
                    var off = Vector3.LerpUnclamped(_from.LookAt, _to.LookAt, k);
                    if (t != null)
                    {
                        var d = t.position + off - position;
                        if (d.sqrMagnitude > 1e-8f) rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
                    }
                }
            }
            else
            {
                position = p0;
                rotation = r0;
            }
            var roll = Mathf.Lerp(_rollFrom, _rollTo, k);
            if (Mathf.Abs(roll) > 0.001f) rotation *= Quaternion.Euler(0f, 0f, roll);

            float vf0 = _focalFrom > 0f ? LensMath.VerticalFov(_focalFrom) : _fovFrom;
            float vf1 = _focalTo > 0f ? LensMath.VerticalFov(_focalTo) : (_focalFrom > 0f && _focalTo <= 0f ? vf0 : _fovTo);
            if (_matchPlayerFov && Application.isPlaying)
            {
                var rig = LongShadow.Core.GameServices.Get<LongShadow.Core.IPlayerRig>();
                if (rig != null && rig.WorldCamera != null) vf1 = rig.WorldCamera.fieldOfView;
            }
            verticalFov = LensMath.ZoomFov(vf0, vf1, k);
            focalMm = LensMath.FocalFromVerticalFov(verticalFov);

            var f0 = _focusFrom;
            var f1 = _focusTo > 0f ? _focusTo : _focusFrom;
            focus = Mathf.Lerp(f0, f1, k);
            if (focus <= 0f)
            {
                var lookId = !string.IsNullOrEmpty(_to.LookAtId) && _move ? _to.LookAtId : _from.LookAtId;
                var t = CinematicBindings.ResolveTransform(lookId);
                focus = t != null ? Vector3.Distance(position, t.position) : 3f;
            }
            return ok;
        }
    }
}
