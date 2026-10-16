using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LongShadow.World.Look.Exposure
{
    /// <summary>
    /// Gameplay depth of field (TECH_PLAN §3.7 VP_Base row, GDD §9.6): Gaussian, far-only from 40 m, faded in while the
    /// player aims down sights (<see cref="IPlayerRig.AdsBlend"/>) and, out of combat, while a squadmate or Calder speaks
    /// in person. A runtime global volume (priority <see cref="Priority"/>), so the stage profiles keep DoF off and
    /// cinematics (WP09, Bokeh) can override it with a higher priority.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.UI)]
    [DisallowMultipleComponent]
    public sealed class DofDirector : MonoBehaviour
    {
        /// <summary>Volume priority (above interiors, below WP09 FX/cinematic volumes).</summary>
        public const float Priority = 30f;

        [SerializeField, Tooltip("Blur starts at this distance (GDD §9.6: 40 m).")]
        private float _start = 40f;
        [SerializeField, Tooltip("Full blur at this distance.")]
        private float _end = 160f;
        [SerializeField, Range(0.5f, 1.5f)] private float _maxRadius = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("Weight while a squad line plays out of combat.")]
        private float _dialogueWeight = 0.6f;
        [SerializeField, Tooltip("Blend seconds.")]
        private float _blendSeconds = 0.35f;

        private readonly HashSet<string> _talking = new HashSet<string>();
        private ServiceRef<IPlayerRig> _rig;
        private Volume _volume;
        private VolumeProfile _profile;
        private float _weight;
        private bool _inCombat;

        private void Awake()
        {
            var go = new GameObject("VOL_DoF");
            go.transform.SetParent(transform, false);
            go.layer = LayerIds.Default;
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = Priority;
            _volume.weight = 0f;
            _volume.enabled = false;
            _profile = VolumeProfileUtil.DeepCopy(null, "VP_GameplayDoF");
            var dof = VolumeProfileUtil.GetOrAdd<DepthOfField>(_profile);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(_start);
            dof.gaussianEnd.Override(_end);
            dof.gaussianMaxRadius.Override(_maxRadius);
            dof.highQualitySampling.Override(false);
            _volume.sharedProfile = _profile;
        }

        private void OnEnable()
        {
            GameEvents.VOLineStarted += OnLineStarted;
            GameEvents.VOLineFinished += OnLineFinished;
            GameEvents.CombatStateChanged += OnCombat;
            GameEvents.CheckpointLoaded += OnCheckpoint;
        }

        private void OnDisable()
        {
            GameEvents.VOLineStarted -= OnLineStarted;
            GameEvents.VOLineFinished -= OnLineFinished;
            GameEvents.CombatStateChanged -= OnCombat;
            GameEvents.CheckpointLoaded -= OnCheckpoint;
            _talking.Clear();
        }

        private void OnDestroy() => VolumeProfileUtil.DestroyRuntime(_profile);

        private void Update()
        {
            var rig = _rig.Value;
            var ads = rig != null ? Mathf.Clamp01(rig.AdsBlend) : 0f;
            var talk = !_inCombat && _talking.Count > 0 ? _dialogueWeight : 0f;
            var target = Mathf.Max(ads, talk);
            _weight = Mathf.MoveTowards(_weight, target, _blendSeconds > 0f ? Time.unscaledDeltaTime / _blendSeconds : 1f);
            if (_volume == null) return;
            _volume.weight = _weight;
            var on = _weight > 1e-3f;
            if (_volume.enabled != on) _volume.enabled = on;
        }

        private void OnLineStarted(string lineId, string speakerId)
        {
            if (string.IsNullOrEmpty(lineId)) return;
            if (speakerId == SpeakerIds.Calder || !string.IsNullOrEmpty(SpeakerIds.ToSquadId(speakerId))) _talking.Add(lineId);
        }

        private void OnLineFinished(string lineId)
        {
            if (lineId != null) _talking.Remove(lineId);
        }

        private void OnCombat(bool inCombat) => _inCombat = inCombat;

        private void OnCheckpoint(string cp)
        {
            _talking.Clear();
            _inCombat = false;
        }
    }
}
