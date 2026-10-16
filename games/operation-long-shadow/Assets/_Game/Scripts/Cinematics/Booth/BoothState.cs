using UnityEngine;

namespace LongShadow.Cinematics.Booth
{
    /// <summary>
    /// Switches the booth between "Session 12" (now) and "Session 11" (03-84, filmed by the 16 mm camera for MID-B
    /// shots 4-6, TECH_PLAN §7.12): the seated Calder swaps to the shaved, gaunt head (WP05 BoothBody
    /// <c>SetSession11</c>), the interrogator stands at the chair holding the lit steel lighter in frame, and the
    /// slate is shown to the film camera. Returns everything to Session 12 for I3 (GDD §8.6 shot 7).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoothState : MonoBehaviour
    {
        [SerializeField, Tooltip("Where the interrogator stands in Session 11 (booth local).")]
        private Vector3 _session11Mark = new Vector3(-0.42f, 0f, 0.62f);
        [SerializeField] private float _session11Yaw = 215f;
        [SerializeField, Tooltip("Lighter pose in the film frame (booth local) when no hand bone is found.")]
        private Vector3 _lighterInFrame = new Vector3(-0.22f, 1.22f, 0.42f);

        private BoothRoot _root;
        private bool _session11;
        private Vector3 _interrogatorPos;
        private Quaternion _interrogatorRot;
        private Transform _lighterParent;
        private Vector3 _lighterPos;
        private Quaternion _lighterRot;

        /// <summary>True in the Session 11 state.</summary>
        public bool IsSession11 => _session11;

        private void Awake() => _root = GetComponent<BoothRoot>();

        /// <summary>Switches state (idempotent).</summary>
        public void SetSession11(bool on)
        {
            if (_root == null) _root = GetComponent<BoothRoot>();
            if (_root == null || _session11 == on) return;
            _session11 = on;

            var calder = _root.CalderBody;
            if (calder != null) calder.SendMessage("SetSession11", on, SendMessageOptions.DontRequireReceiver);

            var interrogator = _root.InterrogatorBody;
            var lighter = _root.Lighter;
            if (on)
            {
                if (interrogator != null)
                {
                    _interrogatorPos = interrogator.transform.localPosition;
                    _interrogatorRot = interrogator.transform.localRotation;
                    interrogator.transform.localPosition = _session11Mark;
                    interrogator.transform.localRotation = Quaternion.Euler(0f, _session11Yaw, 0f);
                }
                if (lighter != null)
                {
                    _lighterParent = lighter.transform.parent;
                    _lighterPos = lighter.transform.localPosition;
                    _lighterRot = lighter.transform.localRotation;
                    var hand = FindHand(interrogator);
                    if (hand != null)
                    {
                        lighter.transform.SetParent(hand, false);
                        lighter.transform.localPosition = new Vector3(0.02f, -0.06f, 0.07f);
                        lighter.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    }
                    else
                    {
                        lighter.transform.localPosition = _lighterInFrame;
                        lighter.transform.localRotation = Quaternion.Euler(0f, 160f, 0f);
                    }
                }
            }
            else
            {
                if (interrogator != null)
                {
                    interrogator.transform.localPosition = _interrogatorPos;
                    interrogator.transform.localRotation = _interrogatorRot;
                }
                if (lighter != null)
                {
                    lighter.Close();
                    if (_lighterParent != null) lighter.transform.SetParent(_lighterParent, false);
                    lighter.transform.localPosition = _lighterPos;
                    lighter.transform.localRotation = _lighterRot;
                }
            }
            if (_root.Slate != null) _root.Slate.SetActive(on);
        }

        /// <summary>Right-hand bone of a humanoid body (Animator.GetBoneTransform), or null.</summary>
        public static Transform FindHand(GameObject body)
        {
            if (body == null) return null;
            var animator = body.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return null;
            return animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
    }
}
