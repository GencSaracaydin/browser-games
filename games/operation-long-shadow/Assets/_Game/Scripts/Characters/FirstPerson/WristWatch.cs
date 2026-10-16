using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.FirstPerson
{
    /// <summary>
    /// Calder's Seiko-style dive watch on the left wrist (GDD §4.1, FAL_DIVE_WATCH: "dial hands are procedural"): the
    /// hour and minute hands show the story clock (<see cref="IMissionDirector.StoryClockMinutes"/>, else
    /// <see cref="ILightStage.StoryClockMinutes"/>, else <see cref="FallbackMinutes"/>), so the MID-A watch check reads
    /// 18:40 (GDD §8.5). The seconds hand ticks once per second like a quartz movement.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WristWatch : MonoBehaviour
    {
        /// <summary>17:52, the insertion story clock (CINEMATIC_DIRECTION §2).</summary>
        public const float FallbackMinutes = 17f * 60f + 52f;

        [SerializeField] private Transform _hourHand;
        [SerializeField] private Transform _minuteHand;
        [SerializeField] private Transform _secondHand;
        [SerializeField, Tooltip("Outward dial normal in this transform's local space (toward the wearer's eyes).")]
        private Vector3 _dialAxisLocal = Vector3.up;

        private ServiceRef<IMissionDirector> _mission;
        private ServiceRef<ILightStage> _light;
        private Quaternion _hourRest = Quaternion.identity, _minuteRest = Quaternion.identity, _secondRest = Quaternion.identity;

        private void Awake()
        {
            if (_hourHand != null) _hourRest = _hourHand.localRotation;
            if (_minuteHand != null) _minuteRest = _minuteHand.localRotation;
            if (_secondHand != null) _secondRest = _secondHand.localRotation;
        }

        /// <summary>Story clock in minutes after midnight.</summary>
        public float Minutes
        {
            get
            {
                var m = _mission.Value;
                if (m != null && m.StoryClockMinutes > 0f) return m.StoryClockMinutes;
                var l = _light.Value;
                if (l != null && l.StoryClockMinutes > 0f) return l.StoryClockMinutes;
                return FallbackMinutes;
            }
        }

        private void LateUpdate()
        {
            var minutes = Minutes;
            // Unity is left-handed: a positive angle about the outward dial normal turns clockwise seen from the face.
            var minuteAngle = (minutes % 60f) * 6f;
            var hourAngle = (minutes / 60f % 12f) * 30f;
            var secondAngle = Mathf.Floor(Time.time % 60f) * 6f;
            if (_hourHand != null) _hourHand.localRotation = _hourRest * Quaternion.AngleAxis(hourAngle, LocalAxis(_hourHand));
            if (_minuteHand != null) _minuteHand.localRotation = _minuteRest * Quaternion.AngleAxis(minuteAngle, LocalAxis(_minuteHand));
            if (_secondHand != null) _secondHand.localRotation = _secondRest * Quaternion.AngleAxis(secondAngle, LocalAxis(_secondHand));
        }

        private Vector3 LocalAxis(Transform hand)
        {
            // The dial axis is authored in this transform's space; express it in the hand's rest space.
            var world = transform.TransformDirection(_dialAxisLocal);
            var parentRot = hand.parent != null ? hand.parent.rotation : Quaternion.identity;
            return Quaternion.Inverse(parentRot * (hand == _hourHand ? _hourRest : hand == _minuteHand ? _minuteRest : _secondRest)) * world;
        }

#if UNITY_EDITOR
        /// <summary>Builders: hands and dial axis.</summary>
        public void EditorSetup(Transform hour, Transform minute, Transform second, Vector3 dialAxisLocal)
        {
            _hourHand = hour;
            _minuteHand = minute;
            _secondHand = second;
            _dialAxisLocal = dialAxisLocal.sqrMagnitude > 1e-6f ? dialAxisLocal.normalized : Vector3.up;
        }
#endif
    }
}
