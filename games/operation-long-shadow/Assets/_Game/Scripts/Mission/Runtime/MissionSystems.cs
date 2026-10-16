using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Objectives;
using UnityEngine;

namespace LongShadow.Mission.Runtime
{
    /// <summary>
    /// The mission's host in S_Persistent (prefab <c>Prefabs/Systems/PF_MissionSystems</c>, built by LongShadow/Mission/
    /// Build Mission Systems Prefab): creates the <see cref="MissionDirector"/> and its <see cref="MissionSpawner"/>,
    /// registers <c>IMissionDirector</c>, <c>IObjectiveService</c> and <c>ICheckpointService</c> in Awake, subscribes to
    /// GameEvents while enabled and ticks the director every frame with scaled and unscaled delta time (R3). WP01's
    /// Bootstrap starts the mission (StartMission / ResumeFrom on the click); in the editor the dev start can do it.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Mission)]
    [DisallowMultipleComponent]
    public sealed class MissionSystems : MonoBehaviour
    {
        [SerializeField, Tooltip("LIB_Mission; when empty the registered library is used.")]
        private MissionAssetLibrary _library;
        [SerializeField, Tooltip("Editor only: start the mission on Play when no Bootstrap does (test scenes).")]
        private bool _devAutoStart;
        [SerializeField, Tooltip("Editor only: checkpoint for the dev start (empty = the intro).")]
        private string _devStartCheckpoint;
        [SerializeField, Tooltip("Run the smoke test when Bootstrap raises Boot.Autotest (?autotest=1).")]
        private bool _smokeTestOnAutotest = true;

        private static MissionSystems s_instance;
        private MissionDirector _director;
        private MissionSpawner _spawner;

        /// <summary>The live host, or null.</summary>
        public static MissionSystems Instance => s_instance;

        /// <summary>The director.</summary>
        public MissionDirector Director => _director;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_instance = null;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                LSLog.Error(LogCategory.Mission, "A second MissionSystems was created; disabling it.", this);
                enabled = false;
                return;
            }
            s_instance = this;
            _spawner = new MissionSpawner(transform, _library);
            _director = new MissionDirector(_spawner);
            _spawner.Bind(_director.CollectIntel, _director.Objectives.HasIntel);
            _director.RegisterServices();
        }

        private void OnEnable()
        {
            if (_director == null) return;
            _director.Enable();
            GameEvents.MissionEvent += OnMissionEvent;
        }

        private void OnDisable()
        {
            if (_director == null) return;
            GameEvents.MissionEvent -= OnMissionEvent;
            _director.Disable();
        }

        private void Start()
        {
#if UNITY_EDITOR
            if (!_devAutoStart || _director.IsRunning) return;
            if (string.IsNullOrEmpty(_devStartCheckpoint)) _director.StartMission();
            else _director.ResumeFrom(_devStartCheckpoint);
#endif
        }

        private void Update()
        {
            _director?.Tick(Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            if (s_instance != this) return;
            _director?.UnregisterServices();
            _spawner?.Clear();
            s_instance = null;
        }

        private void OnMissionEvent(string id)
        {
            if (!_smokeTestOnAutotest || id != MissionEvents.BootAutotest) return;
            Dev.MissionSmokeTest.Run(null);
        }
    }
}
