using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Look.Common;
using UnityEngine;

namespace LongShadow.World.Look.Practicals
{
    /// <summary>
    /// Fires the Beat 10 parachute flares (GDD §7.10; WP07 raises <c>B10.FL1</c> at t 12, <c>B10.FL2</c> at t 42 and
    /// <c>B10.FL3</c> at t 72 after a wave-off): spawns <c>PF_FX_ParachuteFlare</c> at the marker of the same id
    /// (Appendix A, +70 m over the pitch) and ignites it. Checkpoint reloads put every live flare out. Lives on
    /// <c>PF_LookRig</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlareDirector : MonoBehaviour
    {
        [SerializeField, Tooltip("PF_FX_ParachuteFlare.")]
        private GameObject _flarePrefab;
        [SerializeField, Tooltip("Mission events that fire a flare; the marker with the same id gives the position.")]
        private string[] _events = { LookIds.Flare1, LookIds.Flare2, LookIds.Flare3 };
        [SerializeField, Tooltip("Fallback positions (Appendix A) when the marker is not loaded.")]
        private Vector3[] _fallbackPositions = { new Vector3(346f, 70f, 404f), new Vector3(356f, 70f, 410f), new Vector3(340f, 70f, 400f) };

        private readonly List<ParachuteFlare> _scratch = new List<ParachuteFlare>(4);

        private void OnEnable()
        {
            GameEvents.MissionEvent += OnMissionEvent;
            GameEvents.CheckpointLoaded += OnCheckpointLoaded;
        }

        private void OnDisable()
        {
            GameEvents.MissionEvent -= OnMissionEvent;
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
        }

        /// <summary>Fires a flare at <paramref name="position"/> with owner id <paramref name="id"/>.</summary>
        public ParachuteFlare Fire(string id, Vector3 position)
        {
            GameObject go;
            if (_flarePrefab != null) go = Pools.Spawn(_flarePrefab, position, Quaternion.identity);
            else
            {
                LSLog.MissingAsset(LogCategory.Look, "PF_FX_ParachuteFlare", "Assets/_Game/Prefabs/FX/PF_FX_ParachuteFlare.prefab", this);
                go = BuildFallback(position);
            }
            var flare = go.GetComponent<ParachuteFlare>();
            if (flare == null)
            {
                LSLog.Error(LogCategory.Look, "Flare prefab has no ParachuteFlare component", go);
                return null;
            }
            flare.Ignite(id);
            LSLog.Info(LogCategory.Look, "Flare " + id + " ignited at " + position, this);
            return flare;
        }

        private void OnMissionEvent(string id)
        {
            if (string.IsNullOrEmpty(id) || _events == null) return;
            for (var i = 0; i < _events.Length; i++)
            {
                if (_events[i] != id) continue;
                var pos = _fallbackPositions != null && i < _fallbackPositions.Length ? _fallbackPositions[i] : new Vector3(346f, 70f, 404f);
                if (MarkerRegistry.Instance.TryGet(id, out SceneMarker m) && m != null) pos = m.Position;
                Fire(id, pos);
                return;
            }
        }

        private void OnCheckpointLoaded(string cp)
        {
            _scratch.Clear();
            var live = ParachuteFlare.Live;
            for (var i = 0; i < live.Count; i++) _scratch.Add(live[i]);
            foreach (var f in _scratch)
                if (f != null) f.Extinguish();
            _scratch.Clear();
        }

        private static GameObject BuildFallback(Vector3 position)
        {
            // Greybox flare: the two lights and the behaviour, no canister art (keeps Beat 10 playable before FX land).
            var root = new GameObject("PF_FX_ParachuteFlare (fallback)");
            root.transform.position = position;
            var swing = new GameObject("Swing").transform;
            swing.SetParent(root.transform, false);
            var spot = new GameObject("Spot").AddComponent<Light>();
            spot.transform.SetParent(swing, false);
            spot.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            spot.type = LightType.Spot;
            spot.spotAngle = 120f;
            spot.range = 110f;
            spot.color = Mathf.CorrelatedColorTemperatureToRGB(3200f);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(swing, false);
            fill.type = LightType.Point;
            fill.range = 60f;
            fill.color = spot.color;
            fill.shadows = LightShadows.None;
            var zone = new GameObject("Zone").AddComponent<IlluminationZone>();
            zone.transform.SetParent(root.transform, false);
            var flare = root.AddComponent<ParachuteFlare>();
            flare.Configure(swing, spot, fill, null, null, zone);
            return root;
        }

#if UNITY_EDITOR
        /// <summary>Editor builders.</summary>
        public void EditorSetup(GameObject flarePrefab) => _flarePrefab = flarePrefab;
#endif
    }
}
