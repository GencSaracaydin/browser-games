using LongShadow.Cinematics.Booth;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>
    /// A body action at its clip start (TECH_PLAN §7.12 CharacterActionClip): resolves an <see cref="ICharacterBody"/> by
    /// binding id (cast "Cast.Hollis", booth "Booth.Interrogator", squad "Squad.SPA") and plays a Core/WP05 action id
    /// (SitHeli, CrouchRun, OneFinger, HandsetToEar, RaiseCamera, ReachToggle, PhoneTalk, HeadLift, GoLimp ...), with an
    /// optional walk to a mark (booth NavMesh) and a look-at. Looping actions are stopped at the clip end when asked.
    /// Unknown ids are logged once by the body and complete at once (WP05), so timelines never hang.
    /// </summary>
    [System.Serializable]
    public sealed class CharacterActionClip : PlayableAsset, ITimelineClipAsset
    {
        /// <summary>Stop action id (WP05 extra id "Stop"; Core change requested).</summary>
        public const string StopAction = "Stop";

        [SerializeField] private string _target;
        [SerializeField] private string _actionId;
        [SerializeField, Tooltip("Walk to this binding/marker before the action (empty = no move).")] private string _moveTo;
        [SerializeField, Tooltip("Booth-local mark when Move To is \"booth\".")] private Vector3 _boothMark;
        [SerializeField] private float _moveSpeed = 1.2f;
        [SerializeField] private string _lookAt;
        [SerializeField, Range(0f, 1f)] private float _lookWeight = 1f;
        [SerializeField] private bool _stopOnExit;
        [SerializeField, Tooltip("Weapon shown in the body's hands (WeaponIds), empty = keep.")] private string _weaponVisual;

        /// <inheritdoc/>
        public ClipCaps clipCaps => ClipCaps.None;
        /// <summary>Body binding id.</summary>
        public string Target { get => _target; set => _target = value; }
        /// <summary>Action id (empty = none).</summary>
        public string ActionId { get => _actionId; set => _actionId = value; }
        /// <summary>Move target id ("booth" = <see cref="BoothMark"/>).</summary>
        public string MoveTo { get => _moveTo; set => _moveTo = value; }
        /// <summary>Booth-local mark.</summary>
        public Vector3 BoothMark { get => _boothMark; set => _boothMark = value; }
        /// <summary>Walk speed m/s.</summary>
        public float MoveSpeed { get => _moveSpeed; set => _moveSpeed = value; }
        /// <summary>Look-at binding id (empty = keep).</summary>
        public string LookAt { get => _lookAt; set => _lookAt = value; }
        /// <summary>Look weight.</summary>
        public float LookWeight { get => _lookWeight; set => _lookWeight = value; }
        /// <summary>Send "Stop" when the clip ends (looping actions).</summary>
        public bool StopOnExit { get => _stopOnExit; set => _stopOnExit = value; }
        /// <summary>Weapon visual to set (WeaponIds, empty = keep).</summary>
        public string WeaponVisual { get => _weaponVisual; set => _weaponVisual = value; }

        /// <inheritdoc/>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<CharacterActionBehaviour>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }

        /// <summary>
        /// Runs the clip on its body; returns the body (null when missing). <paramref name="slideTo"/> is set when the
        /// body has to be moved on rails (no NavMesh under it): the behaviour slides it there over the clip.
        /// </summary>
        public ICharacterBody Run(out Vector3? slideTo)
        {
            slideTo = null;
            var body = CinematicBindings.Resolve<ICharacterBody>(_target);
            if (body == null)
            {
                LSLog.WarnOnce("cine-body:" + _target, LogCategory.Cinematics, "No ICharacterBody for '" + _target + "'; action " + _actionId + " skipped.");
                return null;
            }
            if (!string.IsNullOrEmpty(_weaponVisual)) body.SetWeaponVisual(_weaponVisual);
            if (!string.IsNullOrEmpty(_lookAt)) body.SetLookAt(CinematicBindings.ResolveTransform(_lookAt), _lookWeight);
            if (!string.IsNullOrEmpty(_moveTo))
            {
                Vector3 dest;
                if (_moveTo == "booth")
                {
                    var booth = BoothRoot.Current;
                    dest = booth != null ? booth.ToWorld(_boothMark) : BoothLayout.WorldOrigin + _boothMark;
                }
                else
                {
                    var t = CinematicBindings.ResolveTransform(_moveTo);
                    dest = t != null ? t.position : Vector3.zero;
                    if (t == null) return body;
                }
                if (body.Agent != null && body.Agent.isActiveAndEnabled && body.Agent.isOnNavMesh) body.MoveTo(dest, _moveSpeed);
                else slideTo = dest;
            }
            if (!string.IsNullOrEmpty(_actionId)) body.PlayAction(_actionId);
            return body;
        }
    }

    /// <summary>Runs the action on clip entry; optional stop on exit.</summary>
    internal sealed class CharacterActionBehaviour : PlayableBehaviour
    {
        public CharacterActionClip Clip;
        private ICharacterBody _body;
        private Transform _root;
        private Vector3 _from;
        private Vector3? _slideTo;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (Clip == null || !PlayableUtil.IsLive(info)) return;
            _body = Clip.Run(out _slideTo);
            _root = _body is Component c ? c.transform : null;
            if (_root != null) _from = _root.position;
            else _slideTo = null;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (_slideTo == null || _root == null || !Application.isPlaying) return;
            var k = Easing.Evaluate(ShotEase.Smooth, PlayableUtil.Normalized(playable));
            var dest = _slideTo.Value;
            var p = Vector3.Lerp(_from, dest, k);
            var d = dest - _from;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0004f && k < 0.98f) _root.rotation = Quaternion.Slerp(_root.rotation, Quaternion.LookRotation(d.normalized), 0.2f);
            _root.position = p;
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (_body == null) return;
            if (Clip != null && Clip.StopOnExit && !(_body is Object o && o == null)) _body.PlayAction(CharacterActionClip.StopAction);
            _body = null;
        }
    }
}
