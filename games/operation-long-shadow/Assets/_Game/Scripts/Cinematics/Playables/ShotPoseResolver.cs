using LongShadow.Cinematics.Booth;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>Resolves <see cref="ShotPose"/>s to world poses at play time (frames, bindings, look-at targets).</summary>
    public static class ShotPoseResolver
    {
        private static ServiceRef<IPlayerRig> s_rig;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_rig = default;

        /// <summary>
        /// World position and rotation of a pose. Returns false (and a best-effort pose) when its frame target is missing.
        /// </summary>
        public static bool ToWorld(ShotPose pose, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (pose == null) return false;
            var ok = Frame(pose.Frame, pose.FrameId, out var origin, out var frameRot);
            switch (pose.Frame)
            {
                case ShotFrame.TargetOffset:
                    position = origin + pose.Position;
                    rotation = Quaternion.Euler(pose.Euler);
                    break;
                case ShotFrame.World:
                    position = pose.Position;
                    rotation = Quaternion.Euler(pose.Euler);
                    break;
                default:
                    position = origin + frameRot * pose.Position;
                    rotation = frameRot * Quaternion.Euler(pose.Euler);
                    break;
            }
            if (pose.UseLookPoint)
            {
                var look = pose.Frame == ShotFrame.World || pose.Frame == ShotFrame.TargetOffset
                    ? pose.LookAt
                    : origin + frameRot * pose.LookAt;
                rotation = LookRotation(look - position, rotation);
            }
            else if (!string.IsNullOrEmpty(pose.LookAtId))
            {
                var t = CinematicBindings.ResolveTransform(pose.LookAtId);
                if (t != null) rotation = LookRotation(t.position + pose.LookAt - position, rotation);
                else ok = false;
            }
            return ok;
        }

        /// <summary>Origin and rotation of a frame.</summary>
        public static bool Frame(ShotFrame frame, string frameId, out Vector3 origin, out Quaternion rotation)
        {
            origin = Vector3.zero;
            rotation = Quaternion.identity;
            switch (frame)
            {
                case ShotFrame.Booth:
                    var booth = BoothRoot.Current;
                    if (booth != null)
                    {
                        origin = booth.transform.position;
                        rotation = booth.transform.rotation;
                        return true;
                    }
                    origin = BoothLayout.WorldOrigin;
                    return false;
                case ShotFrame.Target:
                case ShotFrame.TargetOffset:
                    var t = CinematicBindings.ResolveTransform(frameId);
                    if (t == null) return false;
                    origin = t.position;
                    rotation = t.rotation;
                    return true;
                case ShotFrame.PlayerEye:
                    var rig = s_rig.Value;
                    if (rig == null || rig.Eye == null) return false;
                    origin = rig.Eye.position;
                    rotation = rig.Eye.rotation;
                    return true;
                default:
                    return true;
            }
        }

        /// <summary>World pose to booth-local pose (booth root, or the GDD origin when the booth is not loaded).</summary>
        public static void ToBoothLocal(Vector3 worldPos, Quaternion worldRot, out Vector3 localPos, out Quaternion localRot)
        {
            var booth = BoothRoot.Current;
            if (booth != null)
            {
                localPos = booth.transform.InverseTransformPoint(worldPos);
                localRot = Quaternion.Inverse(booth.transform.rotation) * worldRot;
                return;
            }
            localPos = worldPos - BoothLayout.WorldOrigin;
            localRot = worldRot;
        }

        private static Quaternion LookRotation(Vector3 dir, Quaternion fallback) =>
            dir.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : fallback;
    }
}
