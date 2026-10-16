using System;
using UnityEngine;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>
    /// One end of a camera move (CameraShotClip): a position and orientation in a frame, or a position plus a look-at
    /// target that is tracked every frame (moving helicopters, walking cast).
    /// </summary>
    [Serializable]
    public sealed class ShotPose
    {
        [SerializeField] private ShotFrame _frame = ShotFrame.Booth;
        [SerializeField, Tooltip("Binding or marker id for Target/TargetOffset frames.")] private string _frameId;
        [SerializeField] private Vector3 _position;
        [SerializeField, Tooltip("Pitch, yaw, roll in the frame (ignored when a look-at target is set).")] private Vector3 _euler;
        [SerializeField, Tooltip("Binding or marker id to aim at (empty = use the Euler angles).")] private string _lookAtId;
        [SerializeField, Tooltip("Aim offset: world axes from the look-at target; booth-local point when no id and the frame is Booth.")]
        private Vector3 _lookAt;
        [SerializeField] private bool _useLookPoint;

        /// <summary>Frame.</summary>
        public ShotFrame Frame { get => _frame; set => _frame = value; }
        /// <summary>Frame binding id.</summary>
        public string FrameId { get => _frameId; set => _frameId = value; }
        /// <summary>Position in the frame.</summary>
        public Vector3 Position { get => _position; set => _position = value; }
        /// <summary>Euler angles in the frame.</summary>
        public Vector3 Euler { get => _euler; set => _euler = value; }
        /// <summary>Look-at binding id.</summary>
        public string LookAtId { get => _lookAtId; set => _lookAtId = value; }
        /// <summary>Look-at offset (or point in the pose's frame when <see cref="UseLookPoint"/>).</summary>
        public Vector3 LookAt { get => _lookAt; set => _lookAt = value; }
        /// <summary>Aim at <see cref="LookAt"/> as a point in the frame (no target id needed).</summary>
        public bool UseLookPoint { get => _useLookPoint; set => _useLookPoint = value; }

        /// <summary>Pose at a point with Euler angles.</summary>
        public static ShotPose At(ShotFrame frame, Vector3 position, Vector3 euler, string frameId = null) =>
            new ShotPose { _frame = frame, _position = position, _euler = euler, _frameId = frameId };

        /// <summary>Pose at a point aiming at a point in the same frame.</summary>
        public static ShotPose Aim(ShotFrame frame, Vector3 position, Vector3 lookPoint, string frameId = null) =>
            new ShotPose { _frame = frame, _position = position, _lookAt = lookPoint, _useLookPoint = true, _frameId = frameId };

        /// <summary>Pose at a point aiming at a bound target (+ world offset).</summary>
        public static ShotPose Track(ShotFrame frame, Vector3 position, string lookAtId, Vector3 lookOffset, string frameId = null) =>
            new ShotPose { _frame = frame, _position = position, _lookAtId = lookAtId, _lookAt = lookOffset, _frameId = frameId };

        /// <summary>A copy.</summary>
        public ShotPose Clone() => (ShotPose)MemberwiseClone();
    }
}
