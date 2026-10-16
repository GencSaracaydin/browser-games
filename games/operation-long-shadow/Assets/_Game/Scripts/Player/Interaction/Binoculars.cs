using System;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Player.View;
using UnityEngine;

namespace LongShadow.Player.Interaction
{
    /// <summary>
    /// Beat 1 field glasses (TECH_PLAN §7.3 Binoculars, GDD §7.1/§12): available only when Mission enables them
    /// (<c>IPlayerRig.SetBinocularsAvailable</c>); while available the weapon is holstered (they are "in hand"), holding
    /// RMB raises them in 0.3 s to a 12° vertical FOV with the <see cref="HudOverlay.Binoculars"/> overlay, and
    /// <see cref="IsGlassing"/> is true once fully raised. While glassing, every enabled <see cref="GlassingPOI"/>
    /// within its radius of the view centre for its dwell time (4°, 1.0 s) raises <see cref="PoiGlassed"/> and the
    /// mission event "Glassed.&lt;POI id&gt;" once (cleared on checkpoint load). Mission may also poll
    /// <c>IsGlassing</c>/<c>ViewRay</c> itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Binoculars : MonoBehaviour
    {
        [SerializeField, Tooltip("Model parent under the view-model root (PH_BINOCULARS or the greybox).")]
        private Transform _modelSlot;
        [SerializeField, Tooltip("Raise/lower time in seconds.")]
        private float _raiseSeconds = 0.3f;
        [SerializeField, Tooltip("TECH_PLAN §7.3: binoculars 12° (vertical FOV).")]
        private float _verticalFov = 12f;
        [SerializeField, Tooltip("Raise progress at which the overlay and glassing switch on.")]
        private float _glassingThreshold = 0.85f;
        [SerializeField] private Vector3 _loweredPosition = new Vector3(0.14f, -0.34f, 0.34f);
        [SerializeField] private Vector3 _loweredEuler = new Vector3(40f, -12f, 8f);
        [SerializeField] private Vector3 _raisedPosition = new Vector3(0f, -0.03f, 0.075f);
        [SerializeField] private bool _raiseMissionEvents = true;

        private readonly List<GlassingPOI> _pois = new List<GlassingPOI>(8);
        private readonly Dictionary<GlassingPOI, float> _dwell = new Dictionary<GlassingPOI, float>(8);
        private readonly HashSet<GlassingPOI> _ticked = new HashSet<GlassingPOI>();
        private ServiceRef<IHud> _hud;
        private ServiceRef<IWeaponService> _weapons;
        private bool _available;
        private float _raise01;
        private bool _glassing;
        private bool _fovRequested;
        private bool _holsterHeld;

        /// <summary>Raised once per POI when it is glassed.</summary>
        public event Action<GlassingPOI> PoiGlassed;

        /// <summary>True while Mission allows the binoculars (Beat 1).</summary>
        public bool Available => _available;
        /// <summary>True while fully raised (IPlayerRig.IsGlassing).</summary>
        public bool IsGlassing => _glassing;
        /// <summary>Raise progress 0..1.</summary>
        public float Raise01 => _raise01;
        /// <summary>Number of POIs glassed since the last checkpoint load.</summary>
        public int GlassedCount => _ticked.Count;

        /// <summary>Wires the model slot (PlayerRigFactory).</summary>
        internal void Bind(Transform modelSlot) => _modelSlot = modelSlot;

        private void Awake()
        {
            EnsureModel();
            ApplyModelPose();
        }

        /// <summary>Manifest id of the binocular model (Poly Haven binoculars, view-model layer).</summary>
        public const string ModelAssetId = "PH_BINOCULARS";

        /// <summary>Builds a greybox pair of field glasses in the slot while PH_BINOCULARS has not been nested by the prefab builder.</summary>
        private void EnsureModel()
        {
            if (_modelSlot == null || _modelSlot.GetComponentInChildren<Renderer>(true) != null) return;
            LSLog.MissingAsset(LogCategory.Player, ModelAssetId, "Assets/_Game/Art/Models/Props/binoculars/", this);
            var root = new GameObject(PlayerRigNames.BinocularsPlaceholder).transform;
            root.SetParent(_modelSlot, false);
            var body = new Color(0.16f, 0.17f, 0.15f);
            for (var side = -1; side <= 1; side += 2)
            {
                var barrel = Placeholders.Create("Binoculars_Barrel", PrimitiveType.Cylinder, new Vector3(0.05f, 0.065f, 0.05f), body, root);
                barrel.transform.localPosition = new Vector3(0.036f * side, 0f, 0.07f);
                barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            var bridge = Placeholders.Create("Binoculars_Bridge", PrimitiveType.Cube, new Vector3(0.03f, 0.022f, 0.07f), body, root);
            bridge.transform.localPosition = new Vector3(0f, 0.012f, 0.07f);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.gameObject.layer = LayerIds.Viewmodel;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.renderingLayerMask = RenderingLayerIds.ViewmodelReceives;
            }
            root.gameObject.layer = LayerIds.Viewmodel;
        }

        private void OnEnable() => GameEvents.CheckpointLoaded += OnCheckpointLoaded;

        private void OnDisable()
        {
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
            Lower(null, true);
            SetHolster(false);
        }

        /// <summary>Enables or disables the binoculars (IPlayerRig.SetBinocularsAvailable).</summary>
        public void SetAvailable(bool available, PlayerCameraRig cameraRig)
        {
            if (_available == available) return;
            _available = available;
            if (!available) Lower(cameraRig, true);
            SetHolster(available);
            ApplyModelPose();
        }

        /// <summary>Per-frame update (called by PlayerRig).</summary>
        public void Tick(bool aimHeld, bool canUse, Ray viewRay, PlayerCameraRig cameraRig, float dt)
        {
            if (!_available)
            {
                if (_raise01 > 0f || _glassing) Lower(cameraRig, true);
                return;
            }
            var raise = aimHeld && canUse;
            if (raise && !_fovRequested && cameraRig != null)
            {
                cameraRig.RequestFov(this, _verticalFov, _raiseSeconds);
                _fovRequested = true;
            }
            else if (!raise && _fovRequested)
            {
                cameraRig?.ReleaseFov(this);
                _fovRequested = false;
            }
            var step = _raiseSeconds > 0f ? dt / _raiseSeconds : 1f;
            _raise01 = Mathf.MoveTowards(_raise01, raise ? 1f : 0f, step);

            var glassing = _raise01 >= _glassingThreshold;
            if (glassing != _glassing)
            {
                _glassing = glassing;
                _hud.Value?.SetOverlay(glassing ? HudOverlay.Binoculars : HudOverlay.None);
            }
            ApplyModelPose();
            if (_glassing) TrackPois(viewRay, dt);
        }

        private void TrackPois(Ray view, float dt)
        {
            MarkerRegistry.Instance.GetAll(_pois);
            for (var i = 0; i < _pois.Count; i++)
            {
                var poi = _pois[i];
                if (poi == null || _ticked.Contains(poi)) continue;
                if (!poi.IsInView(view))
                {
                    _dwell[poi] = 0f;
                    continue;
                }
                _dwell.TryGetValue(poi, out var t);
                t += dt;
                _dwell[poi] = t;
                if (t < poi.DwellSeconds) continue;
                _ticked.Add(poi);
                PoiGlassed?.Invoke(poi);
                if (_raiseMissionEvents && !string.IsNullOrEmpty(poi.Id)) GameEvents.RaiseMissionEvent(PlayerEvents.GlassedPrefix + poi.Id);
            }
            _pois.Clear();
        }

        private void Lower(PlayerCameraRig cameraRig, bool instant)
        {
            if (_fovRequested)
            {
                cameraRig?.ReleaseFov(this);
                _fovRequested = false;
            }
            if (instant) _raise01 = 0f;
            if (_glassing)
            {
                _glassing = false;
                _hud.Value?.SetOverlay(HudOverlay.None);
            }
            ApplyModelPose();
        }

        private void SetHolster(bool holster)
        {
            if (holster == _holsterHeld) return;
            _holsterHeld = holster;
            _weapons.Value?.SetHolstered(this, holster);
        }

        private void ApplyModelPose()
        {
            if (_modelSlot == null) return;
            var show = _available && _raise01 < 0.98f; // fully raised: the overlay mask replaces the model
            if (_modelSlot.gameObject.activeSelf != show) _modelSlot.gameObject.SetActive(show);
            if (!show) return;
            var x = _raise01 * _raise01 * (3f - 2f * _raise01);
            _modelSlot.localPosition = Vector3.Lerp(_loweredPosition, _raisedPosition, x);
            _modelSlot.localRotation = Quaternion.Slerp(Quaternion.Euler(_loweredEuler), Quaternion.identity, x);
        }

        private void OnCheckpointLoaded(string cpId)
        {
            _ticked.Clear();
            _dwell.Clear();
        }
    }
}
