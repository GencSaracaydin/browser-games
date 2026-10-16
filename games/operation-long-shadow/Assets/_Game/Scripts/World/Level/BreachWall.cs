using LongShadow.Core;
using LongShadow.World.Level.Data;
using UnityEngine;

namespace LongShadow.World.Level
{
    /// <summary>
    /// The compound wall section at x 300, z 319–325 that Tinker's satchel blows (GDD §7.6, BLD-09 "breach section with
    /// intact and blown variants", TECH_PLAN §7.15 BreachWall). On <see cref="LevelEvents.BreachBlast"/> (raised by WP07 at the
    /// detonation) it swaps the intact wall for the blown mesh and rubble, turns the intact colliders off, the rubble
    /// colliders on, and enables the breach NavMeshLink so the squad can cross into the yard. Saved in checkpoints.
    /// Id <c>B6.BreachWall</c> sits on the <see cref="SceneMarker"/> of the same object.
    /// </summary>
    public sealed class BreachWall : MonoBehaviour, ISaveable
    {
        [SerializeField] private GameObject[] _intact = new GameObject[0];
        [SerializeField] private GameObject[] _blown = new GameObject[0];
        [SerializeField, Tooltip("Behaviours enabled once blown (the breach NavMeshLink).")]
        private Behaviour[] _enableOnBlast = new Behaviour[0];
        [SerializeField] private string _blastEvent = LevelEvents.BreachBlast;

        private bool _isBlown;
        private SaveRegistration _save;

        /// <summary>True once blown.</summary>
        public bool IsBlown => _isBlown;

        /// <summary>Configures the wall (editor builders).</summary>
        public void Configure(GameObject[] intact, GameObject[] blown, Behaviour[] enableOnBlast)
        {
            _intact = intact ?? new GameObject[0];
            _blown = blown ?? new GameObject[0];
            _enableOnBlast = enableOnBlast ?? new Behaviour[0];
        }

        private void Awake()
        {
            _save = new SaveRegistration(this);
            Apply(false);
        }

        private void Start() => _save.TryRegister();

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

        private void OnDestroy() => _save?.Unregister();

        private void OnCheckpointLoaded(string id) => _save.TryRegister();

        private void OnMissionEvent(string id)
        {
            if (id == _blastEvent) Blow();
        }

        /// <summary>Blows the wall (event, script or debug).</summary>
        public void Blow() => Apply(true);

        /// <summary>Back to the intact wall (checkpoints before the breach).</summary>
        public void ResetIntact() => Apply(false);

        private void Apply(bool blown)
        {
            _isBlown = blown;
            foreach (var g in _intact)
                if (g != null) g.SetActive(!blown);
            foreach (var g in _blown)
                if (g != null) g.SetActive(blown);
            foreach (var b in _enableOnBlast)
                if (b != null) b.enabled = blown;
        }

        /// <inheritdoc/>
        public string SaveKey => "BreachWall";

        /// <inheritdoc/>
        public void Capture(SaveWriter w) => w.Write("blown", _isBlown);

        /// <inheritdoc/>
        public void Restore(SaveReader r) => Apply(r.ReadBool("blown", false));
    }
}
