using UnityEngine;

namespace LongShadow.Cinematics.Booth
{
    /// <summary>
    /// The black rotary desk phone (GDD §8.8 shot 8: "He lifts the desk phone... It is the briefing-tape voice"). The
    /// Handset child (FAL_ROTARY_PHONE) is handed to the interrogator's right hand when one is given (WP05 PhoneTalk
    /// brings the hand to the ear), otherwise it rises and tilts on its own; the coiled cord gap stays out of C_POV's frame
    /// because the lift turns away from the chair (WP09_ART known issue).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeskPhone : MonoBehaviour
    {
        [SerializeField] private Transform _handset;
        [SerializeField] private Vector3 _handOffset = new Vector3(0.02f, -0.05f, 0.06f);
        [SerializeField] private Vector3 _handEuler = new Vector3(0f, 90f, 90f);

        private Transform _restParent;
        private Vector3 _restPos;
        private Quaternion _restRot;
        private float _lift;
        private bool _lifted;
        private Transform _hand;

        /// <summary>True while the handset is off the cradle.</summary>
        public bool Lifted => _lifted;

        private void Awake()
        {
            if (_handset == null) return;
            _restParent = _handset.parent;
            _restPos = _handset.localPosition;
            _restRot = _handset.localRotation;
        }

        /// <summary>Builder wiring.</summary>
        public void Setup(Transform handset) => _handset = handset;

        /// <summary>Lifts the handset (into <paramref name="hand"/> when given) with the receiver sound.</summary>
        public void Lift(Transform hand)
        {
            if (_handset == null || _lifted) return;
            _lifted = true;
            _hand = hand;
            CinematicAudio.Play(CinematicCues.Phone, transform.position);
            if (_hand != null)
            {
                _handset.SetParent(_hand, false);
                _handset.localPosition = _handOffset;
                _handset.localRotation = Quaternion.Euler(_handEuler);
            }
        }

        /// <summary>Puts the handset back on the cradle.</summary>
        public void Hang()
        {
            if (_handset == null) return;
            _lifted = false;
            _hand = null;
            _handset.SetParent(_restParent, false);
            _handset.localPosition = _restPos;
            _handset.localRotation = _restRot;
            _lift = 0f;
        }

        private void Update()
        {
            if (_handset == null || _hand != null) return;
            var target = _lifted ? 1f : 0f;
            if (Mathf.Approximately(_lift, target)) return;
            _lift = Mathf.MoveTowards(_lift, target, Time.unscaledDeltaTime / 0.7f);
            var k = Easing.Evaluate(ShotEase.Smooth, _lift);
            _handset.localPosition = _restPos + new Vector3(0.10f, 0.26f, 0.05f) * k;
            _handset.localRotation = _restRot * Quaternion.Euler(0f, 35f * k, 70f * k);
        }
    }
}
