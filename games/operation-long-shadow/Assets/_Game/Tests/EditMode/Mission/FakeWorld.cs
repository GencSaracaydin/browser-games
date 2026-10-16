using System;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Mission.Beats;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>
    /// A scripted fake world for the mission (TECH_PLAN §11.4 WP07 DoD "EditMode tests with fake services"): every Core
    /// service the beat scripts use is a recording fake registered in GameServices, the player is a point
    /// (<see cref="PlayerPos"/>), the director runs on a fixed step, and an <see cref="Autopilot"/> plays the mission the
    /// way a player and the AI would (goes to the trigger a beat waits for, kills the enemies it waits on, uses the
    /// interaction once its gate is open, takes the intel, destroys vulnerable racks).
    /// </summary>
    /// <summary>A recorded gate state change.</summary>
    internal struct GateChange
    {
        public string Id;
        public bool Open;
        public int Beat;
        public float Time;

        public override string ToString() => "B" + Beat + " " + Id + (Open ? " open" : " closed");
    }

    internal sealed class FakeWorld : IDisposable
    {
        public readonly MissionDirector Director;
        public readonly FakeVO VO;
        public readonly FakeEncounters Encounters;
        public readonly FakeSquad Squad;
        public readonly FakeLight Light;
        public readonly FakeStreamer Streamer;
        public readonly FakeHud Hud;
        public readonly FakeInserts Inserts;
        public readonly FakeCinematics Cinematics;
        public readonly FakeScreenFX ScreenFX;
        public readonly FakeMusic Music;
        public readonly FakeAudio Audio;
        public readonly FakeTimeScale TimeScale;
        public readonly FakeInputLock InputLock;
        public readonly FakeRig Rig;
        public readonly FakeHealth Health;
        public readonly FakeWeapons Weapons;
        public readonly FakeFpBody FpBody;
        public readonly FakeCombat Combat;
        public readonly FakeTargets Targets;
        public readonly FakeSettings Settings;
        public readonly FakeSpawner Spawner;
        public readonly Autopilot Pilot;
        public readonly Dictionary<string, FakeDestructible> Destructibles = new Dictionary<string, FakeDestructible>();
        public readonly List<string> MissionEvents = new List<string>();
        public readonly List<KeyValuePair<int, int>> BeatsStarted = new List<KeyValuePair<int, int>>();
        public readonly List<int> BeatsCompleted = new List<int>();
        public readonly List<GateChange> GateChanges = new List<GateChange>();
        public readonly List<KeyValuePair<float, string>> EventTimes = new List<KeyValuePair<float, string>>();

        public Vector3 PlayerPos;
        public bool AutopilotOn;
        public float Step = 0.05f;

        public float Now => Director.Runner.Time;
        public float RealNow => Director.Runner.UnscaledTime;

        public FakeWorld(bool withCinematics = true, bool withEncounters = true, BeatScript[] beats = null)
        {
            GameServices.Clear();
            MarkerRegistry.ResetForTests();
            LSLog.ResetOnce();
            Spawner = new FakeSpawner();
            Director = beats != null ? new MissionDirector(Spawner, beats) : new MissionDirector(Spawner);
            Director.PlayerPositionOverride = () => PlayerPos;
            VO = new FakeVO(this);
            Encounters = new FakeEncounters(this);
            Squad = new FakeSquad(this);
            Light = new FakeLight(this);
            Streamer = new FakeStreamer(this);
            Hud = new FakeHud(this);
            Inserts = new FakeInserts(this);
            Cinematics = new FakeCinematics(this);
            ScreenFX = new FakeScreenFX(this);
            Music = new FakeMusic(this);
            Audio = new FakeAudio(this);
            TimeScale = new FakeTimeScale(this);
            InputLock = new FakeInputLock(this);
            Rig = new FakeRig(this);
            Health = new FakeHealth(this);
            Weapons = new FakeWeapons(this);
            FpBody = new FakeFpBody(this);
            Combat = new FakeCombat(this);
            Targets = new FakeTargets(this);
            Settings = new FakeSettings(this);
            Pilot = new Autopilot(this);

            GameServices.Register<IVOService>(VO);
            if (withEncounters) GameServices.Register<IEncounterService>(Encounters);
            GameServices.Register<ISquadService>(Squad);
            GameServices.Register<ILightStage>(Light);
            GameServices.Register<ISectionStreamer>(Streamer);
            GameServices.Register<IHud>(Hud);
            GameServices.Register<IInsertService>(Inserts);
            if (withCinematics) GameServices.Register<ICinematicService>(Cinematics);
            GameServices.Register<IScreenFX>(ScreenFX);
            GameServices.Register<IMusicService>(Music);
            GameServices.Register<IAudioService>(Audio);
            GameServices.Register<ITimeScaleService>(TimeScale);
            GameServices.Register<IInputLock>(InputLock);
            GameServices.Register<IPlayerRig>(Rig);
            GameServices.Register<IPlayerHealth>(Health);
            GameServices.Register<IWeaponService>(Weapons);
            GameServices.Register<IFirstPersonBody>(FpBody);
            GameServices.Register<ICombatService>(Combat);
            GameServices.Register<ITargetRegistry>(Targets);
            GameServices.Register<ISettingsService>(Settings);

            foreach (var id in new[]
                     {
                         MissionMarkers.B6F1, MissionMarkers.B6F2, MissionMarkers.B6F3, MissionMarkers.B6F4, MissionMarkers.B6BreachWall,
                         MissionMarkers.B9R1, MissionMarkers.B9R2, MissionMarkers.B9R3, MissionMarkers.B9R4, ObjectiveTable.B11Truck1,
                         ObjectiveTable.B11Truck2,
                     })
            {
                var d = new FakeDestructible(id);
                Destructibles[id] = d;
                LongShadow.Core.Destructibles.Register(d);
            }

            Director.RegisterServices();
            Director.Checkpoints.Register(new FakeEncounterSave(Encounters));
            Director.Enable();
            Director.Gates.Changed += (id, open) => GateChanges.Add(new GateChange { Id = id, Open = open, Beat = Director.Beat, Time = Now });
            GameEvents.MissionEvent += OnMissionEvent;
            GameEvents.BeatStarted += OnBeatStarted;
            GameEvents.BeatCompleted += OnBeatCompleted;
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.CheckpointLoaded += OnCheckpointLoaded;
        }

        private void OnMissionEvent(string id)
        {
            MissionEvents.Add(id);
            EventTimes.Add(new KeyValuePair<float, string>(Now, id));
        }

        private void OnBeatStarted(int b) => BeatsStarted.Add(new KeyValuePair<int, int>(b, Director.Log.Seq));
        private void OnBeatCompleted(int b) => BeatsCompleted.Add(b);
        private void OnPlayerDied(DamageInfo d) => Health.Current = 0f;
        private void OnCheckpointLoaded(string cp) => Health.Current = 100f;

        public void Tick()
        {
            var scaled = Step * TimeScale.Current;
            Director.Tick(scaled, Step);
            VO.Tick();
            Squad.Tick();
            Inserts.Tick();
            Cinematics.Tick();
            Hud.Tick();
            if (AutopilotOn) Pilot.Step();
        }

        /// <summary>Ticks until the predicate holds or <paramref name="maxSeconds"/> of real time pass. Returns the predicate.</summary>
        public bool RunUntil(Func<bool> done, float maxSeconds)
        {
            var end = RealNow + maxSeconds;
            while (RealNow < end)
            {
                if (done()) return true;
                Tick();
            }
            return done();
        }

        /// <summary>Ticks for real seconds.</summary>
        public void Run(float seconds)
        {
            var end = RealNow + seconds;
            while (RealNow < end) Tick();
        }

        /// <summary>Moves the player into a trigger (Appendix A geometry) and raises it like a TriggerVolume would.</summary>
        public void Enter(string triggerId)
        {
            if (MissionTriggers.TryGetShape(triggerId, out var s)) PlayerPos = s.Inside;
            GameEvents.RaiseTriggerEntered(triggerId, null);
        }

        public void Dispose()
        {
            GameEvents.MissionEvent -= OnMissionEvent;
            GameEvents.BeatStarted -= OnBeatStarted;
            GameEvents.BeatCompleted -= OnBeatCompleted;
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
            Director.Disable();
            Director.UnregisterServices();
            foreach (var d in Destructibles.Values) LongShadow.Core.Destructibles.Unregister(d);
            GameServices.Clear();
            MarkerRegistry.ResetForTests();
        }
    }
}
