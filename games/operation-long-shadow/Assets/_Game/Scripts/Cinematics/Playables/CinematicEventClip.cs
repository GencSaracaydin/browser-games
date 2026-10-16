using LongShadow.Cinematics.Booth;
using LongShadow.Cinematics.Sets;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>
    /// A one-shot cue at its clip start (TECH_PLAN §7.12 "Signal track for events"): mission events, timeline holds,
    /// booth state, inserts, the insertion cast, HUD title card, flashes and fades, MID-B projection. Cues marked
    /// <see cref="FireOnSkip"/> also run when the sequence is skipped before reaching them (state the game needs, such
    /// as the hand-off).
    /// </summary>
    [System.Serializable]
    public sealed class CinematicEventClip : PlayableAsset, ITimelineClipAsset
    {
        /// <summary>Owner of the LightStage override taken by cinematics.</summary>
        public static readonly object LightOwner = new object();

        [SerializeField] private CinematicEventKind _kind;
        [SerializeField] private string _id;
        [SerializeField] private string _line;
        [SerializeField] private float _value;
        [SerializeField] private float _value2;
        [SerializeField] private bool _flag;
        [SerializeField] private bool _fireOnSkip;

        /// <inheritdoc/>
        public ClipCaps clipCaps => ClipCaps.None;
        /// <summary>Action.</summary>
        public CinematicEventKind Kind { get => _kind; set => _kind = value; }
        /// <summary>Id argument (event id, insert id, binding id).</summary>
        public string Id { get => _id; set => _id = value; }
        /// <summary>Line argument (insert line).</summary>
        public string Line { get => _line; set => _line = value; }
        /// <summary>Numeric argument.</summary>
        public float Value { get => _value; set => _value = value; }
        /// <summary>Second numeric argument.</summary>
        public float Value2 { get => _value2; set => _value2 = value; }
        /// <summary>Boolean argument.</summary>
        public bool Flag { get => _flag; set => _flag = value; }
        /// <summary>Run on skip when not reached yet.</summary>
        public bool FireOnSkip { get => _fireOnSkip; set => _fireOnSkip = value; }

        /// <inheritdoc/>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<CinematicEventBehaviour>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }

        /// <summary>Runs the cue.</summary>
        public void Execute(CinematicService service)
        {
            var fx = CinematicRuntime.ScreenFx;
            var booth = BoothRoot.Current;
            switch (_kind)
            {
                case CinematicEventKind.MissionEvent:
                    if (service != null) service.RaiseEvent(_id);
                    else GameEvents.RaiseMissionEvent(_id);
                    break;
                case CinematicEventKind.Hold:
                    service?.RequestHold((HoldCondition)Mathf.RoundToInt(_value));
                    break;
                case CinematicEventKind.BoothResting:
                    booth?.SetResting();
                    break;
                case CinematicEventKind.BoothSession11:
                    booth?.State?.SetSession11(_flag);
                    break;
                case CinematicEventKind.PlayInsert:
                    CinematicRuntime.Inserts?.Play(_id, string.IsNullOrEmpty(_line) ? null : _line);
                    break;
                case CinematicEventKind.InsertBus:
                    if (_flag) GameEvents.RaiseInsertStarted(_id);
                    else GameEvents.RaiseInsertEnded(_id);
                    break;
                case CinematicEventKind.InsertionCast:
                    var cast = InsertionCast.Instance;
                    if (cast != null)
                    {
                        if (_flag) cast.Spawn();
                        else cast.Despawn();
                    }
                    break;
                case CinematicEventKind.HudTitleCard:
                    GameServices.Get<IHud>()?.TitleCard(_value);
                    break;
                case CinematicEventKind.Flash:
                    fx?.Flash(_value, _value2);
                    break;
                case CinematicEventKind.FadeOut:
                    if (_flag) fx?.FadeToWhite(_value);
                    else fx?.FadeToBlack(_value);
                    break;
                case CinematicEventKind.FadeIn:
                    fx?.FadeIn(_value);
                    break;
                case CinematicEventKind.Projection:
                    var rig = ProjectionRig.Instance;
                    if (rig != null)
                    {
                        if (_flag) rig.Setup();
                        else rig.Teardown();
                    }
                    break;
                case CinematicEventKind.ProjectionContent:
                    ProjectionRig.Instance?.Show(_id);
                    break;
                case CinematicEventKind.Tinnitus:
                    CinematicAudio.Audio?.Tinnitus(_value);
                    break;
                case CinematicEventKind.SquadLook:
                    var squad = GameServices.Get<ISquadService>();
                    squad?.SetLookTarget(string.IsNullOrEmpty(_id) ? null : CinematicBindings.ResolveTransform(_id), _value);
                    break;
                case CinematicEventKind.PlayerLook:
                    GameServices.Get<IPlayerRig>()?.SetLookAngles(_value, _value2);
                    break;
                case CinematicEventKind.LightStage:
                    var stage = GameServices.Get<ILightStage>();
                    if (stage == null) break;
                    if (_flag)
                    {
                        stage.Override(LightOwner, true);
                        stage.SetImmediate(_value);
                    }
                    else stage.Override(LightOwner, false);
                    break;
                case CinematicEventKind.Holster:
                    GameServices.Get<IWeaponService>()?.SetHolstered(service != null ? (object)service : this, _flag);
                    break;
            }
        }
    }

    /// <summary>Fires a cue when its clip starts during playback.</summary>
    internal sealed class CinematicEventBehaviour : PlayableBehaviour
    {
        public CinematicEventClip Clip;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (Clip == null || !PlayableUtil.IsLive(info)) return;
            Clip.Execute(CinematicRuntime.Service);
        }
    }
}
