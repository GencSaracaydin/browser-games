using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Look.Stage;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LongShadow.World.Look.Exposure
{
    /// <summary>
    /// Eye adaptation for interiors (GDD G10, §9.2a interiors table; TECH_PLAN §3.7): for every
    /// <see cref="EyeAdaptationZone"/> in the loaded sections it keeps a runtime global volume (priority
    /// <see cref="Priority"/>) whose weight eases in over the zone's <c>FadeIn</c> (1.5 s) while the camera is inside and
    /// out over <c>FadeOut</c> (1.0 s) after it leaves. URP has no auto-exposure, so the interior post exposure is set
    /// every frame to <b>stage exposure + zone EV</b> (cinema +1.2, hall +0.8, screening +1.0, basement +1.0): an interior
    /// stays brighter than outside at every stage instead of overriding the stage's own +1.0/+1.5 EV. The zone's
    /// <c>VP_Interior_*</c> profile is deep-copied, so any extra grading it carries applies too and the asset never changes.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.LightStage + 2)]
    [DisallowMultipleComponent]
    public sealed class EyeAdaptationDriver : MonoBehaviour
    {
        /// <summary>Volume priority of interior adaptation (above the stage volumes 0–4, below FX and option volumes).</summary>
        public const float Priority = 20f;

        private sealed class Slot
        {
            public EyeAdaptationZone Zone;
            public Volume Volume;
            public VolumeProfile Profile;
            public ColorAdjustments Adjust;
            public float Weight;
            public bool Alive;
        }

        [SerializeField, Tooltip("Seconds between zone list refreshes (sections load and unload).")]
        private float _refreshInterval = 0.5f;

        private readonly List<EyeAdaptationZone> _scratch = new List<EyeAdaptationZone>();
        private readonly List<Slot> _slots = new List<Slot>();
        private ServiceRef<IPlayerRig> _rig;
        private float _nextRefresh;

        /// <summary>Highest current interior weight (0 outdoors).</summary>
        public float InteriorWeight { get; private set; }

        private void OnDisable()
        {
            foreach (var s in _slots) Release(s);
            _slots.Clear();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + _refreshInterval;
                Refresh();
            }
            if (_slots.Count == 0)
            {
                InteriorWeight = 0f;
                return;
            }

            var eye = EyePosition();
            var stageEv = LightStageController.Active != null ? LightStageController.Active.StagePostExposure : 0f;
            var dt = Time.unscaledDeltaTime; // perception, not gameplay time (inserts and slow-mo keep adapting)
            var max = 0f;
            for (var i = _slots.Count - 1; i >= 0; i--)
            {
                var s = _slots[i];
                var zone = s.Zone;
                var inside = s.Alive && zone != null && zone.isActiveAndEnabled && (zone.PlayerInside || zone.Contains(eye));
                var fade = inside ? (zone != null ? zone.FadeIn : 1.5f) : (zone != null ? zone.FadeOut : 1f);
                s.Weight = Mathf.MoveTowards(s.Weight, inside ? 1f : 0f, fade > 0f ? dt / fade : 1f);
                if (!s.Alive && s.Weight <= 0f)
                {
                    Release(s);
                    _slots.RemoveAt(i);
                    continue;
                }
                var w = s.Weight * s.Weight * (3f - 2f * s.Weight); // ease
                if (s.Volume != null)
                {
                    s.Volume.weight = w;
                    var on = w > 0f;
                    if (s.Volume.enabled != on) s.Volume.enabled = on;
                }
                if (s.Adjust != null) s.Adjust.postExposure.Override(stageEv + (zone != null ? zone.Ev : 0f));
                if (w > max) max = w;
            }
            InteriorWeight = max;
        }

        private Vector3 EyePosition()
        {
            var rig = _rig.Value;
            if (rig != null && rig.WorldCamera != null && rig.WorldCamera.isActiveAndEnabled) return rig.WorldCamera.transform.position;
            var cam = Camera.main;
            return cam != null ? cam.transform.position : transform.position;
        }

        private void Refresh()
        {
            MarkerRegistry.Instance.GetAll(_scratch);
            foreach (var s in _slots) s.Alive = false;
            foreach (var z in _scratch)
            {
                if (z == null) continue;
                var slot = Find(z);
                if (slot == null) _slots.Add(slot = Create(z));
                slot.Alive = true;
            }
            _scratch.Clear();
        }

        private Slot Find(EyeAdaptationZone z)
        {
            foreach (var s in _slots)
                if (s.Zone == z)
                    return s;
            return null;
        }

        private Slot Create(EyeAdaptationZone zone)
        {
            var go = new GameObject("VOL_Adapt_" + (string.IsNullOrEmpty(zone.Id) ? zone.name : zone.Id));
            go.transform.SetParent(transform, false);
            go.layer = LayerIds.Default;
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = Priority;
            v.weight = 0f;
            v.enabled = false;
            var p = VolumeProfileUtil.DeepCopy(zone.Profile, "VP_Adapt_" + zone.name);
            var adjust = VolumeProfileUtil.GetOrAdd<ColorAdjustments>(p);
            v.sharedProfile = p;
            return new Slot { Zone = zone, Volume = v, Profile = p, Adjust = adjust, Weight = 0f, Alive = true };
        }

        private static void Release(Slot s)
        {
            if (s.Volume != null)
            {
                if (Application.isPlaying) Destroy(s.Volume.gameObject);
                else DestroyImmediate(s.Volume.gameObject);
            }
            VolumeProfileUtil.DestroyRuntime(s.Profile);
        }
    }
}
