using LongShadow.Characters.Animation;
using LongShadow.Characters.Body;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.Characters.Weapons
{
    /// <summary>
    /// The world weapon a body holds (TECH_PLAN §7.8 "weapon on a right-hand socket with per-weapon grip offset").
    /// Instantiates the LIB_CharacterWeapons prefab under <see cref="BodyRig.RightGrip"/>, aligns the prefab's
    /// <c>RightGrip</c> to the socket, finds <c>Muzzle</c>/<c>LeftGrip</c> (or creates them from
    /// <see cref="WeaponHoldTable"/>), strips its physics while held and copies the body's render settings (layer,
    /// shadow mode, rendering layers). Builds a greybox gun when nothing has landed. Can stow the weapon on the back
    /// and drop it as a physics prop on death.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BodyWeaponHolder : MonoBehaviour
    {
        [SerializeField] private BodyRig _rig;
        [SerializeField, Tooltip("Seconds a dropped weapon stays before it is removed (it rides with the corpse otherwise).")]
        private float _droppedLifetime = 45f;

        private string _weaponId;
        private GameObject _instance;
        private Transform _muzzle;
        private Transform _leftGrip;
        private Transform _rightGrip;
        private WeaponHold _hold = WeaponHold.Unarmed;
        private HandPose _handPose = HandPose.Relaxed;
        private bool _dropOnDeath = true;
        private bool _stowed;
        private Quaternion _tune = Quaternion.identity;
        private Vector3 _fallbackGripPos;
        private Quaternion _fallbackGripRot = Quaternion.identity;
        private bool _hasRightGripChild;

        /// <summary>Current weapon id (null = unarmed).</summary>
        public string WeaponId => _weaponId;
        /// <summary>True if a visible weapon is held (not stowed, not unarmed).</summary>
        public bool IsHolding => _instance != null && !_stowed;
        /// <summary>The instantiated weapon (null when unarmed).</summary>
        public GameObject Instance => _instance;
        /// <summary>Muzzle of the held weapon, or the hand socket when unarmed.</summary>
        public Transform Muzzle => _muzzle != null ? _muzzle : (_rig != null ? _rig.RightGrip : null);
        /// <summary>Support-hand target on the weapon (null when unarmed or stowed).</summary>
        public Transform LeftGrip => _stowed ? null : _leftGrip;
        /// <summary>Hold style.</summary>
        public WeaponHold Hold => _instance != null && !_stowed ? _hold : WeaponHold.Unarmed;
        /// <summary>Finger pose for the hold.</summary>
        public HandPose HandPose => _instance != null && !_stowed ? _handPose : HandPose.Relaxed;

        private void Awake()
        {
            if (_rig == null) _rig = GetComponent<BodyRig>();
        }

        /// <summary>Holds a weapon by id (null/empty = unarmed). Same id is a no-op.</summary>
        public void SetWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) weaponId = null;
            if (weaponId == _weaponId && (_instance != null || weaponId == null)) return;
            Clear();
            _weaponId = weaponId;
            if (weaponId == null) return;
            if (_rig == null) _rig = GetComponent<BodyRig>();
            if (_rig == null || _rig.RightGrip == null)
            {
                LSLog.WarnOnce("holder-norig:" + name, LogCategory.Characters, name + ": no hand socket; weapon " + weaponId + " not shown.", this);
                return;
            }

            var known = WeaponHoldTable.TryGet(weaponId, out var row);
            if (!known)
                LSLog.WarnOnce("holder-unknown:" + weaponId, LogCategory.Characters, "Unknown weapon id '" + weaponId + "' for a body; rifle defaults.");
            if (!row.IsCarried)
            {
                // Mounted or not carried: the body is visually unarmed but keeps the id.
                _hold = WeaponHold.Unarmed;
                _handPose = HandPose.Relaxed;
                return;
            }

            _hold = row.Hold;
            _handPose = row.HandPose;
            _dropOnDeath = true;
            _tune = Quaternion.identity;
            GameObject prefab = null;
            if (Libraries.TryGet(out CharacterWeaponLibrary lib) && lib.TryGet(weaponId, out var entry))
            {
                prefab = entry.WorldPrefab;
                _hold = entry.Hold;
                _handPose = entry.HandPose;
                _dropOnDeath = entry.DropOnDeath;
                _tune = entry.TuneRotation;
                _fallbackGripPos = entry.GripPosition;
                _fallbackGripRot = entry.GripRotation;
            }
            else
            {
                _fallbackGripPos = Vector3.zero;
                _fallbackGripRot = Quaternion.identity;
            }

            if (prefab != null)
            {
                _instance = Instantiate(prefab, _rig.RightGrip, false);
                _instance.name = prefab.name;
            }
            else
            {
                LSLog.MissingAsset(LogCategory.Characters, "held weapon " + weaponId, CharacterWeaponLibrary.AssetPath, this);
                _instance = BuildPlaceholder(row);
                _instance.transform.SetParent(_rig.RightGrip, false);
            }

            PrepareInstance(_instance);
            _rightGrip = WeaponSockets.Find(_instance.transform, WeaponSockets.RightGripNames);
            _hasRightGripChild = _rightGrip != null && _rightGrip != _instance.transform;
            _muzzle = WeaponSockets.Find(_instance.transform, WeaponSockets.MuzzleNames);
            _leftGrip = WeaponSockets.Find(_instance.transform, WeaponSockets.LeftGripNames);
            if (_muzzle == null) _muzzle = CreateSocket(_instance.transform, "Muzzle", row.Muzzle, 0f);
            if (_leftGrip == null && row.Hold != WeaponHold.Unarmed)
                _leftGrip = CreateSocket(_instance.transform, "LeftGrip", row.LeftGrip, row.LeftGripRoll);
            AttachTo(_rig.RightGrip);
            ApplyRenderSettings(_instance);
        }

        /// <summary>Moves the weapon to the back socket (true) or back into the hand (false).</summary>
        public void SetStowed(bool stowed)
        {
            if (_stowed == stowed) return;
            _stowed = stowed;
            if (_instance == null || _rig == null) return;
            var socket = stowed ? (_rig.Back != null ? _rig.Back : _rig.RightGrip) : _rig.RightGrip;
            AttachTo(socket);
        }

        /// <summary>True while stowed on the back.</summary>
        public bool IsStowed => _stowed;

        /// <summary>
        /// Detaches the weapon as a physics prop (death). Returns the dropped object (null if none or the entry keeps it).
        /// </summary>
        public GameObject Drop(Vector3 velocity, Vector3 angularVelocity)
        {
            if (_instance == null) return null;
            var go = _instance;
            if (!_dropOnDeath)
            {
                Clear();
                return null;
            }
            _instance = null;
            _muzzle = null;
            _leftGrip = null;
            _rightGrip = null;
            go.transform.SetParent(null, true);
            BuilderLayers(go, LayerIds.Debris);
            var bounds = LocalBounds(go);
            var box = go.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = Vector3.Max(bounds.size, new Vector3(0.04f, 0.06f, 0.2f));
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 3.6f; // AKM ~3.1 kg loaded (GDD §13)
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearVelocity = velocity;
            rb.angularVelocity = angularVelocity;
            if (_droppedLifetime > 0f) Destroy(go, _droppedLifetime);
            return go;
        }

        /// <summary>Removes the held weapon (unarmed).</summary>
        public void Clear()
        {
            if (_instance != null)
            {
                if (Application.isPlaying) Destroy(_instance);
                else DestroyImmediate(_instance);
            }
            _instance = null;
            _muzzle = null;
            _leftGrip = null;
            _rightGrip = null;
            _weaponId = null;
            _stowed = false;
            _hold = WeaponHold.Unarmed;
            _handPose = HandPose.Relaxed;
        }

        /// <summary>Re-applies layer, shadow mode and rendering layers from the body (after the body's mode changed).</summary>
        public void RefreshRenderSettings()
        {
            if (_instance != null) ApplyRenderSettings(_instance);
        }

        private void AttachTo(Transform socket)
        {
            var t = _instance.transform;
            t.SetParent(socket, false);
            if (_hasRightGripChild)
            {
                // Place the root so its RightGrip child coincides with the socket (then apply the per-weapon tune).
                var gripLocalRot = Quaternion.Inverse(t.rotation) * _rightGrip.rotation;
                var gripLocalPos = t.InverseTransformPoint(_rightGrip.position);
                var rootRot = socket.rotation * _tune * Quaternion.Inverse(gripLocalRot);
                t.rotation = rootRot;
                t.position = socket.position - rootRot * Vector3.Scale(gripLocalPos, t.lossyScale);
            }
            else
            {
                t.localPosition = _fallbackGripPos;
                t.localRotation = _tune * _fallbackGripRot;
            }
        }

        private void PrepareInstance(GameObject go)
        {
            // Held weapons never collide with their owner or take part in physics.
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
        }

        private void ApplyRenderSettings(GameObject go)
        {
            // Copy the body's look so a ShadowsOnly proxy casts a gun shadow and booth/viewmodel layers stay consistent.
            var reference = GetComponentInChildren<SkinnedMeshRenderer>(true);
            var layer = reference != null ? reference.gameObject.layer : gameObject.layer;
            var shadow = reference != null ? reference.shadowCastingMode : ShadowCastingMode.On;
            var mask = reference != null ? reference.renderingLayerMask : RenderingLayerIds.WorldMask;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.gameObject.layer = layer;
                r.shadowCastingMode = shadow;
                r.renderingLayerMask = mask;
            }
        }

        private static GameObject BuildPlaceholder(in WeaponHoldTable.Row row)
        {
            var root = new GameObject("PH_Weapon_" + row.Id);
            var col = Placeholders.Greybox * 0.45f;
            col.a = 1f;
            var len = row.Muzzle.z + row.StockLength;
            var receiver = Placeholders.Create(row.Id + "_Receiver", PrimitiveType.Cube,
                new Vector3(0.05f, 0.075f, Mathf.Max(0.12f, len * 0.62f)), col, root.transform);
            receiver.transform.localPosition = new Vector3(0f, row.Muzzle.y - 0.01f, (row.Muzzle.z - row.StockLength) * 0.5f - 0.05f);
            var barrel = Placeholders.Create(row.Id + "_Barrel", PrimitiveType.Cylinder,
                new Vector3(0.025f, Mathf.Max(0.05f, len * 0.2f), 0.025f), col, root.transform);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localPosition = new Vector3(0f, row.Muzzle.y, row.Muzzle.z - len * 0.2f);
            var grip = Placeholders.Create(row.Id + "_Grip", PrimitiveType.Cube, new Vector3(0.03f, 0.11f, 0.04f), col, root.transform);
            grip.transform.localPosition = new Vector3(0f, -0.03f, 0f);
            grip.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            return root;
        }

        private static Transform CreateSocket(Transform parent, string socketName, Vector3 localPos, float roll)
        {
            var t = new GameObject(socketName).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.AngleAxis(roll, Vector3.forward);
            return t;
        }

        private static void BuilderLayers(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) BuilderLayers(c.gameObject, layer);
        }

        private static Bounds LocalBounds(GameObject go)
        {
            var b = new Bounds(Vector3.zero, Vector3.zero);
            var first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = go.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                var c = m.MultiplyPoint3x4(mb.center);
                var e = mb.extents;
                var ext = new Vector3(
                    Mathf.Abs(m.m00) * e.x + Mathf.Abs(m.m01) * e.y + Mathf.Abs(m.m02) * e.z,
                    Mathf.Abs(m.m10) * e.x + Mathf.Abs(m.m11) * e.y + Mathf.Abs(m.m12) * e.z,
                    Mathf.Abs(m.m20) * e.x + Mathf.Abs(m.m21) * e.y + Mathf.Abs(m.m22) * e.z);
                if (first)
                {
                    b = new Bounds(c, ext * 2f);
                    first = false;
                }
                else b.Encapsulate(new Bounds(c, ext * 2f));
            }
            return b;
        }

#if UNITY_EDITOR
        /// <summary>Builders: assigns the rig.</summary>
        public void EditorSetRig(BodyRig rig) => _rig = rig;
#endif
    }
}
