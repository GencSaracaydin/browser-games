using LongShadow.Cinematics.Booth;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>
    /// Camera track: the active <see cref="CameraShotClip"/> decides the camera mode and its pose, lens and letterbox
    /// every frame through <c>CinematicCameras</c>. In the editor the track may be bound to any Camera to scrub the shots
    /// without play mode (preview binding only; runtime playback ignores it).
    /// </summary>
    [TrackColor(0.92f, 0.62f, 0.22f)]
    [TrackClipType(typeof(CameraShotClip))]
    [TrackBindingType(typeof(Camera))]
    public sealed class CameraShotTrack : TrackAsset
    {
        /// <inheritdoc/>
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount) =>
            ScriptPlayable<CameraShotMixer>.Create(graph, inputCount);
    }

    /// <summary>Per-clip behaviour of a camera shot.</summary>
    internal sealed class CameraShotBehaviour : PlayableBehaviour
    {
        public CameraShotClip Clip;
    }

    /// <summary>Applies the active camera shot.</summary>
    internal sealed class CameraShotMixer : PlayableBehaviour
    {
        private int _last = -1;
        private float _letterbox;

        public override void OnGraphStart(Playable playable)
        {
            _last = -1;
            _letterbox = CinematicRuntime.Overlay != null ? CinematicRuntime.Overlay.Letterbox : 0f;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var i = PlayableUtil.ActiveInput(playable);
            if (i < 0) return;
            var input = (ScriptPlayable<CameraShotBehaviour>)playable.GetInput(i);
            var clip = input.GetBehaviour()?.Clip;
            if (clip == null) return;
            var t = PlayableUtil.Normalized(input);
            clip.Evaluate(t, out var pos, out var rot, out var fov, out var focal, out var focus);

            if (!Application.isPlaying)
            {
                Preview(playerData as Camera, clip, pos, rot, fov);
                return;
            }
            var cams = CinematicRuntime.Cameras;
            if (cams == null) return;
            var owner = (object)CinematicRuntime.Service ?? this;
            if (i != _last)
            {
                _last = i;
                cams.Push(owner, clip.Rig);
                if (clip.ImpulseDeg > 0f) cams.Impulse(clip.ImpulseDeg);
            }
            switch (clip.Rig)
            {
                case CameraMode.Booth:
                    ShotPoseResolver.ToBoothLocal(pos, rot, out var lp, out var lr);
                    cams.SetBoothPose(lp, lr, fov);
                    break;
                case CameraMode.World:
                    cams.SetWorldPose(pos, rot, fov);
                    break;
            }
            cams.SetLens(clip.DepthOfField, focus, clip.Aperture, focal);
            cams.SetHandheld(clip.HandheldDeg, clip.HandheldHz);
            cams.SetMotionBlur(clip.MotionBlur);

            var overlay = CinematicRuntime.Overlay;
            if (overlay != null)
            {
                // On the player camera the HUD draws its own bars (title card); ours only stand in without a HUD.
                var hudBars = clip.Rig == CameraMode.Player && CinematicRuntime.Service != null && CinematicRuntime.Service.HasHud;
                var target = clip.Letterbox && !hudBars ? 1f : 0f;
                _letterbox = Mathf.MoveTowards(_letterbox, target, Time.unscaledDeltaTime / 0.5f);
                overlay.SetLetterbox(_letterbox);
            }
        }

        private static void Preview(Camera cam, CameraShotClip clip, Vector3 pos, Quaternion rot, float fov)
        {
            if (cam == null || clip.Rig == CameraMode.Player) return;
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;
        }
    }
}
