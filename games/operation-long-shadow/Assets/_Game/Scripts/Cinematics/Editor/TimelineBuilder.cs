using System.Collections.Generic;
using LongShadow.Cinematics.Booth;
using LongShadow.Cinematics.Playables;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Editor
{
    /// <summary>
    /// Writes a TL_* timeline from a shot table (TECH_PLAN §7.12: timelines generated from data so an editor agent can
    /// rebuild them with one menu call). Rebuilding keeps the asset's GUID: every track is deleted and recreated. One-shot
    /// clip kinds (VO, sounds, events, actions, booth props, rig) are spread over "lanes" so clips never overlap on a
    /// track; state tracks (camera, CRTs, tube, look, FX channels, paths) get one track each.
    /// </summary>
    public sealed class TimelineBuilder
    {
        private readonly Dictionary<string, List<TrackAsset>> _lanes = new Dictionary<string, List<TrackAsset>>();
        private readonly Dictionary<string, TrackAsset> _singles = new Dictionary<string, TrackAsset>();
        private readonly List<string> _shotLog = new List<string>();

        /// <summary>The timeline being written.</summary>
        public TimelineAsset Timeline { get; }
        /// <summary>Asset path.</summary>
        public string Path { get; }
        /// <summary>Dialogue data (durations, subtitles).</summary>
        public DialogueData Dialogue { get; }
        /// <summary>Shot list written (for the report).</summary>
        public IReadOnlyList<string> ShotLog => _shotLog;

        private TimelineBuilder(TimelineAsset timeline, string path, DialogueData dialogue)
        {
            Timeline = timeline;
            Path = path;
            Dialogue = dialogue;
        }

        /// <summary>Opens (or creates) a timeline asset, clears it and fixes its length.</summary>
        public static TimelineBuilder Begin(string assetPath, double seconds, DialogueData dialogue)
        {
            BuilderUtil.EnsureFolder(System.IO.Path.GetDirectoryName(assetPath));
            var tl = AssetDatabase.LoadAssetAtPath<TimelineAsset>(assetPath);
            if (tl == null)
            {
                tl = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(tl, assetPath);
            }
            foreach (var t in new List<TrackAsset>(tl.GetRootTracks())) tl.DeleteTrack(t);
            if (tl.markerTrack != null) tl.DeleteTrack(tl.markerTrack);
            tl.durationMode = TimelineAsset.DurationMode.FixedLength;
            tl.fixedDuration = seconds;
            tl.editorSettings.frameRate = 60d;
            return new TimelineBuilder(tl, assetPath, dialogue);
        }

        /// <summary>An in-memory timeline (tests and previews; nothing is written to disk).</summary>
        public static TimelineBuilder InMemory(double seconds, DialogueData dialogue)
        {
            var tl = ScriptableObject.CreateInstance<TimelineAsset>();
            tl.durationMode = TimelineAsset.DurationMode.FixedLength;
            tl.fixedDuration = seconds;
            return new TimelineBuilder(tl, null, dialogue);
        }

        /// <summary>Saves the asset and its sub-assets.</summary>
        public TimelineAsset Save()
        {
            EditorUtility.SetDirty(Timeline);
            foreach (var t in Timeline.GetOutputTracks())
            {
                EditorUtility.SetDirty(t);
                foreach (var c in t.GetClips())
                    if (c.asset != null) EditorUtility.SetDirty(c.asset);
            }
            AssetDatabase.SaveAssets();
            return Timeline;
        }

        // ------------------------------------------------------------------ tracks

        /// <summary>A single named track (created once).</summary>
        public T Single<T>(string label, string bindingId = null) where T : TrackAsset, new()
        {
            var name = TrackBindingResolver.TrackName(label, bindingId);
            if (_singles.TryGetValue(name, out var t)) return (T)t;
            var track = Timeline.CreateTrack<T>(null, name);
            _singles[name] = track;
            return track;
        }

        /// <summary>A lane of kind <typeparamref name="T"/> free in [start, end).</summary>
        public T Lane<T>(string kind, double start, double end) where T : TrackAsset, new()
        {
            if (!_lanes.TryGetValue(kind, out var list))
            {
                list = new List<TrackAsset>();
                _lanes[kind] = list;
            }
            foreach (var track in list)
            {
                var free = true;
                foreach (var c in track.GetClips())
                {
                    if (c.start < end - 1e-6 && start < c.end - 1e-6)
                    {
                        free = false;
                        break;
                    }
                }
                if (free) return (T)track;
            }
            var t = Timeline.CreateTrack<T>(null, kind + " " + (list.Count + 1));
            list.Add(t);
            return t;
        }

        /// <summary>Adds a clip of type <typeparamref name="TClip"/> to a track.</summary>
        public TClip Clip<TClip>(TrackAsset track, double start, double duration, string label) where TClip : PlayableAsset
        {
            var c = track.CreateClip<TClip>();
            c.start = start;
            c.duration = System.Math.Max(1d / 60d, duration);
            c.displayName = label ?? typeof(TClip).Name;
            return (TClip)c.asset;
        }

        // ------------------------------------------------------------------ helpers per clip kind

        /// <summary>Camera shot.</summary>
        public CameraShotClip Shot(string name, double start, double end, CameraMode rig, ShotPose from, ShotPose to = null,
                                   float focalFrom = 0f, float focalTo = 0f, float aperture = 2.8f, float focusFrom = 0f,
                                   float focusTo = 0f, float handheld = HandheldNoise.BreathingDeg, float hz = HandheldNoise.BreathingHz,
                                   float impulse = 0f, bool letterbox = false, ShotEase ease = ShotEase.Smooth, float fov = 0f,
                                   float fovTo = 0f, bool dof = true, float rollFrom = 0f, float rollTo = 0f, float motionBlur = 0f)
        {
            var track = Single<CameraShotTrack>("Camera");
            var c = Clip<CameraShotClip>(track, start, end - start, name);
            c.ShotName = name;
            c.Rig = rig;
            c.From = from ?? new ShotPose();
            c.To = to ?? from?.Clone() ?? new ShotPose();
            c.Move = to != null;
            c.Ease = ease;
            c.FocalFrom = focalFrom;
            c.FocalTo = focalTo;
            c.FovFrom = fov > 0f ? fov : BoothLayout.PovFov;
            c.FovTo = fovTo > 0f ? fovTo : c.FovFrom;
            c.Aperture = aperture;
            c.FocusFrom = focusFrom;
            c.FocusTo = focusTo;
            c.HandheldDeg = handheld;
            c.HandheldHz = hz;
            c.ImpulseDeg = impulse;
            c.Letterbox = letterbox;
            c.DepthOfField = dof;
            c.RollFrom = rollFrom;
            c.RollTo = rollTo;
            c.MotionBlur = motionBlur;
            _shotLog.Add(start.ToString("000.00") + "-" + end.ToString("000.00") + "  " + name + "  [" + rig + "]");
            return c;
        }

        /// <summary>Dialogue line at <paramref name="start"/> (duration from the envelopes).</summary>
        public VOLineClip Vo(double start, string lineId, string speakerBinding = null, bool forceRadio = false, bool forceDry = false)
        {
            var line = Dialogue.Get(lineId);
            var end = start + line.Duration;
            var track = Lane<VOLineTrack>("VO", start, end);
            var c = Clip<VOLineClip>(track, start, line.Duration, lineId);
            c.LineId = lineId;
            c.SpeakerId = line.Speaker;
            c.Callsign = line.Callsign;
            c.Subtitle = line.Text;
            c.SpeakerBinding = speakerBinding;
            c.ForceRadio = forceRadio;
            c.ForceDry = forceDry;
            return c;
        }

        /// <summary>End time of a line started at <paramref name="start"/>.</summary>
        public double EndOf(double start, string lineId) => start + Dialogue.Get(lineId).Duration;

        /// <summary>Sound cue.</summary>
        public AudioCueClip Sfx(double start, string cue, AudioCueClip.Mode mode = AudioCueClip.Mode.OneShot2D, double duration = 0.25,
                                string at = null, Vector3 boothPoint = default, float volume = 1f, float fade = 0.5f, int layer = 0)
        {
            var track = Lane<AudioCueTrack>("Audio", start, start + duration);
            var c = Clip<AudioCueClip>(track, start, duration, cue ?? mode.ToString());
            c.PlayMode = mode;
            c.CueId = cue;
            c.At = at;
            c.BoothPoint = boothPoint;
            c.Volume = volume;
            c.FadeOut = fade;
            c.Layer = layer;
            return c;
        }

        /// <summary>One-shot cue event.</summary>
        public CinematicEventClip Event(double start, CinematicEventKind kind, string id = null, float value = 0f, float value2 = 0f,
                                        bool flag = false, bool fireOnSkip = false, string line = null)
        {
            const double d = 0.1;
            var track = Lane<CinematicEventTrack>("Events", start, start + d);
            var c = Clip<CinematicEventClip>(track, start, d, kind + (id != null ? " " + id : ""));
            c.Kind = kind;
            c.Id = id;
            c.Value = value;
            c.Value2 = value2;
            c.Flag = flag;
            c.FireOnSkip = fireOnSkip;
            c.Line = line;
            return c;
        }

        /// <summary>CRT channel.</summary>
        public CRTChannelClip Crt(int n, double start, double end, CrtContent content, string assetId = null, float staticAmount = 0.06f,
                                  bool snapOn = false, string overlay = null)
        {
            var track = Single<CRTChannelTrack>("CRT" + n, BindingIds.Crt(n));
            var c = Clip<CRTChannelClip>(track, start, end - start, content + (assetId != null ? " " + assetId : ""));
            c.Content = content;
            c.AssetId = assetId;
            c.StaticAmount = staticAmount;
            c.SnapOn = snapOn;
            c.OverlayText = overlay;
            return c;
        }

        /// <summary>Tube mode.</summary>
        public LightFlickerClip Tube(double start, double end, FlickerMode mode, int blinks = 4)
        {
            var track = Single<LightFlickerTrack>("Tube", BindingIds.Tube);
            var c = Clip<LightFlickerClip>(track, start, end - start, mode.ToString());
            c.Mode = mode;
            c.Blinks = blinks;
            return c;
        }

        /// <summary>Limited look.</summary>
        public LookControlClip Look(double start, double end, bool enabled, float yaw = LimitedLook.DefaultYaw,
                                    float pitch = LimitedLook.DefaultPitch, float recentre = 0f)
        {
            var track = Single<LookControlTrack>("Look");
            var c = Clip<LookControlClip>(track, start, end - start, enabled ? "Look ±" + yaw + "/±" + pitch : "Look locked");
            c.LookEnabled = enabled;
            c.YawLimit = yaw;
            c.PitchUp = pitch;
            c.PitchDown = pitch;
            c.RecentreSeconds = recentre;
            return c;
        }

        /// <summary>Screen FX ramp on a channel.</summary>
        public ScreenFXClip Fx(FxChannel channel, double start, double end, float from, float to, ShotEase ease = ShotEase.Linear,
                               Color? color = null, string feedId = null, bool whiteFrame = false)
        {
            var track = Single<ScreenFXTrack>("FX " + channel);
            track.Channel = channel;
            var c = Clip<ScreenFXClip>(track, start, end - start, channel + " " + from + "→" + to);
            c.From = from;
            c.To = to;
            c.Ease = ease;
            c.Color = color ?? Color.black;
            c.FeedId = feedId;
            c.WhiteFrame = whiteFrame;
            return c;
        }

        /// <summary>Typed text.</summary>
        public TypewriterClip Type(double start, double end, string text, TypewriterClip.Slot slot, float cps, Color color,
                                   bool numberGroups = false, bool onlyWithoutHud = false)
        {
            var track = Single<TypewriterTrack>("Type");
            var c = Clip<TypewriterClip>(track, start, end - start, "Type");
            c.Text = text;
            c.Position = slot;
            c.CharsPerSecond = cps;
            c.Color = color;
            c.NumberGroups = numberGroups;
            c.OnlyWithoutHud = onlyWithoutHud;
            return c;
        }

        /// <summary>Booth action.</summary>
        public BoothPropClip Prop(double start, BoothPropClip.Action action, double duration = 0.1, int crt = 1, float seconds = 0.034f)
        {
            var track = Lane<BoothPropTrack>("Booth", start, start + duration);
            var c = Clip<BoothPropClip>(track, start, duration, action.ToString());
            c.Kind = action;
            c.Crt = crt;
            c.Seconds = seconds;
            return c;
        }

        /// <summary>Body action.</summary>
        public CharacterActionClip Act(double start, double end, string target, string actionId, string moveTo = null,
                                       Vector3 boothMark = default, float speed = 1.2f, string lookAt = null,
                                       float lookWeight = 1f, bool stopOnExit = false, string weapon = null)
        {
            var track = Lane<CharacterActionTrack>("Actions", start, end);
            var c = Clip<CharacterActionClip>(track, start, end - start, target + " " + (actionId ?? "move"));
            c.Target = target;
            c.ActionId = actionId;
            c.MoveTo = moveTo;
            c.BoothMark = boothMark;
            c.MoveSpeed = speed;
            c.LookAt = lookAt;
            c.LookWeight = lookWeight;
            c.StopOnExit = stopOnExit;
            c.WeaponVisual = weapon;
            return c;
        }

        /// <summary>Path move of a bound transform.</summary>
        public TransformPathClip PathMove(string bindingId, double start, double end, Vector3[] knots, float from = 0f, float to = 1f,
                                      ShotEase ease = ShotEase.Linear, TransformPathClip.Orient orient = TransformPathClip.Orient.Yaw,
                                      float lateral = 0f, bool groundSnap = false, float clearance = 0f, bool flare = false,
                                      bool boothFrame = false)
        {
            var track = Single<TransformPathTrack>("Path", bindingId);
            var c = Clip<TransformPathClip>(track, start, end - start, "Path " + bindingId);
            c.Knots = knots;
            c.FromT = from;
            c.ToT = to;
            c.Ease = ease;
            c.Orientation = orient;
            c.Lateral = lateral;
            c.GroundSnap = groundSnap;
            c.Clearance = clearance;
            c.Flare = flare;
            c.BoothFrame = boothFrame;
            return c;
        }

        /// <summary>Speed ramp.</summary>
        public SpeedRampClip Ramp(double start, double end, float from, float to, ShotEase ease = ShotEase.Smooth)
        {
            var track = Single<SpeedRampTrack>("Speed");
            var c = Clip<SpeedRampClip>(track, start, end - start, "Speed " + from + "→" + to);
            c.From = from;
            c.To = to;
            c.Ease = ease;
            return c;
        }

        /// <summary>Player rig action.</summary>
        public PlayerRigClip Rig(double start, double end, PlayerRigClip.Action action)
        {
            var track = Lane<PlayerRigTrack>("Player", start, end);
            var c = Clip<PlayerRigClip>(track, start, end - start, action.ToString());
            c.Kind = action;
            return c;
        }
    }
}
