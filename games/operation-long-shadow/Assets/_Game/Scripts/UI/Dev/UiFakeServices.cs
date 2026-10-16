using LongShadow.Core;
using LongShadow.UI.Common;
using UnityEngine;

namespace LongShadow.UI.Dev
{
    /// <summary>
    /// Registers fake services for the UI test bed (LongShadow/UI/Build UI Test Scene) so every HUD element can be
    /// exercised without the other packages: weapon state, objectives, light stage, settings, pause (with a simulated
    /// re-lock cooldown), mission director, health, squad, encounters (sentry awareness), checkpoints and input. Runs
    /// before the HUD (execution order).
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Platform - 100)]
    [DisallowMultipleComponent]
    public sealed class UiFakeServices : MonoBehaviour
    {
        [SerializeField] private Transform _hollis;
        [SerializeField] private Transform _sparrow;
        [SerializeField] private Transform _tinker;
        [SerializeField] private Transform _sentryA;
        [SerializeField] private Transform _sentryB;

        public FakeWeaponHudState Weapon { get; private set; }
        public FakeObjectiveService Objectives { get; private set; }
        public FakeLightStage Light { get; private set; }
        public FakeSettingsService Settings { get; private set; }
        public FakePauseService Pause { get; private set; }
        public FakeMissionDirector Director { get; private set; }
        public FakePlayerHealth Health { get; private set; }
        public FakeSquadService Squad { get; private set; }
        public FakeEncounterService Encounters { get; private set; }
        public FakeCheckpointService Checkpoints { get; private set; }
        public FakeInputReader Input { get; private set; }

        /// <summary>Assigns the scene objects (builder).</summary>
        public void Configure(Transform hollis, Transform sparrow, Transform tinker, Transform sentryA, Transform sentryB)
        {
            _hollis = hollis;
            _sparrow = sparrow;
            _tinker = tinker;
            _sentryA = sentryA;
            _sentryB = sentryB;
        }

        private void Awake()
        {
            Weapon = new FakeWeaponHudState();
            Objectives = new FakeObjectiveService();
            Light = new FakeLightStage();
            Settings = new FakeSettingsService();
            Pause = new FakePauseService();
            Director = new FakeMissionDirector();
            Health = new FakePlayerHealth();
            Squad = new FakeSquadService();
            Encounters = new FakeEncounterService();
            Checkpoints = new FakeCheckpointService();
            Input = new FakeInputReader();

            Squad.SetMember(SquadIds.Hollis, _hollis);
            Squad.SetMember(SquadIds.Sparrow, _sparrow);
            Squad.SetMember(SquadIds.Tinker, _tinker);
            if (_sentryA != null) Encounters.AddActor(new FakeTargetable("E2-01", _sentryA));
            if (_sentryB != null) Encounters.AddActor(new FakeTargetable("E2-02", _sentryB));

            GameServices.Register<IWeaponHudState>(Weapon);
            GameServices.Register<IObjectiveService>(Objectives);
            GameServices.Register<ILightStage>(Light);
            GameServices.Register<ISettingsService>(Settings);
            GameServices.Register<IPauseService>(Pause);
            GameServices.Register<IMissionDirector>(Director);
            GameServices.Register<IPlayerHealth>(Health);
            GameServices.Register<ISquadService>(Squad);
            GameServices.Register<IEncounterService>(Encounters);
            GameServices.Register<ICheckpointService>(Checkpoints);
            GameServices.Register<IInputReader>(Input);

        }

        private void OnDestroy()
        {
            GameServices.Unregister<IWeaponHudState>(Weapon);
            GameServices.Unregister<IObjectiveService>(Objectives);
            GameServices.Unregister<ILightStage>(Light);
            GameServices.Unregister<ISettingsService>(Settings);
            GameServices.Unregister<IPauseService>(Pause);
            GameServices.Unregister<IMissionDirector>(Director);
            GameServices.Unregister<IPlayerHealth>(Health);
            GameServices.Unregister<ISquadService>(Squad);
            GameServices.Unregister<IEncounterService>(Encounters);
            GameServices.Unregister<ICheckpointService>(Checkpoints);
            GameServices.Unregister<IInputReader>(Input);
            if (Time.timeScale == 0f) Time.timeScale = 1f;
        }
    }
}
