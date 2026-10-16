using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Player;
using LongShadow.Player.Bounds;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Player
{
    /// <summary>
    /// The assembled rig (<see cref="PlayerRigFactory"/>): hierarchy and camera contract, damage and death events, FOV
    /// requests, binocular glassing, the out-of-bounds fail/reload hook, mounting, suppression, body visibility and
    /// checkpoint restore (TECH_PLAN §11.4 WP02 DoD).
    /// </summary>
    public class PlayerRigTests
    {
        private const float Dt = 1f / 60f;
        private readonly List<GameObject> _objects = new List<GameObject>();
        private PlayerRig _rig;
        private FakeInputReader _input;

        [SetUp]
        public void SetUp()
        {
            GameServices.Clear();
            GameEvents.ResetAll();
            MarkerRegistry.ResetForTests();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            _objects.Add(ground);
            _rig = PlayerRigFactory.Build(Vector3.zero, 0f);
            _objects.Add(_rig.gameObject);
            _rig.Wire();
            _input = new FakeInputReader();
            GameServices.Register<IInputReader>(_input);
            Physics.SyncTransforms();
            EditModeCharacterController.Probe(_rig.Motor.Controller); // these tests do not need movement, only the frame loop
        }

        [TearDown]
        public void TearDown()
        {
            if (_rig != null) _rig.Unwire();
            foreach (var o in _objects) if (o != null) Object.DestroyImmediate(o);
            _objects.Clear();
            GameServices.Clear();
            GameEvents.ResetAll();
            MarkerRegistry.ResetForTests();
            Physics.SyncTransforms();
        }

        private void Simulate(float seconds)
        {
            for (var t = 0f; t < seconds - 1e-5f; t += Dt)
            {
                _rig.SimulateFrame(Dt);
                _input.EndFrame();
            }
        }

        [Test]
        public void FactoryRigPassesTheValidator()
        {
            var problems = new List<string>();
            Assert.IsTrue(PlayerRigValidator.Validate(_rig.gameObject, problems), string.Join("\n", problems));
        }

        [Test]
        public void RigExposesTheCoreContracts()
        {
            IPlayerRig rig = _rig;
            Assert.That(rig.Root, Is.SameAs(_rig.transform));
            Assert.That(rig.Eye.name, Is.EqualTo(PlayerRigNames.EyeOffset));
            Assert.That(rig.ViewmodelRoot.name, Is.EqualTo(PlayerRigNames.ViewmodelRoot));
            Assert.That(rig.ViewmodelRoot.gameObject.layer, Is.EqualTo(LayerIds.Viewmodel));
            Assert.IsTrue(rig.WorldCamera.CompareTag(Tags.MainCamera));
            Assert.That(rig.ViewmodelCamera, Is.Not.Null);
            Assert.That(_rig.GetComponentsInChildren<AudioListener>(true), Is.Empty, "WP06 owns the only AudioListener");

            ITargetable t = _rig;
            Assert.That(t.ActorId, Is.EqualTo(SquadIds.Player));
            Assert.That(t.Team, Is.EqualTo(Team.Player));
            Assert.That(t.AimPoint.localPosition.y, Is.EqualTo(1.3f).Within(1e-4f));
            Assert.IsTrue(_rig is ISuppressible);
            Assert.That(((ISaveable)_rig).SaveKey, Is.EqualTo(PlayerRig.Key));
        }

        [Test]
        public void ViewRayStartsAtTheEyeHeight()
        {
            Simulate(0.1f);
            var ray = ((IPlayerRig)_rig).ViewRay;
            Assert.That(ray.origin.y - _rig.transform.position.y, Is.EqualTo(1.65f).Within(0.05f), "GDD §12: eye 1.65 m");
            Assert.That(Vector3.Dot(ray.direction, Vector3.forward), Is.GreaterThan(0.99f));
        }

        [Test]
        public void MouseLookUsesTheGddFormula()
        {
            _rig.SimulateFrame(Dt);
            var before = _rig.Yaw;
            _input.Look = new Vector2(100f, 0f); // 100 CSS px at the default sensitivity 3.0 -> 9°
            _rig.SimulateFrame(Dt);
            Assert.That(MathX.WrapAngle180(_rig.Yaw - before), Is.EqualTo(9f).Within(1e-3f));
            _input.Look = new Vector2(0f, 10000f);
            _rig.SimulateFrame(Dt);
            Assert.That(_rig.Pitch, Is.EqualTo(85f).Within(1e-3f), "pitch clamps at +85");
        }

        [Test]
        public void DamageDirectionAndDeathEventsFire()
        {
            var damaged = new List<DamageInfo>();
            var died = 0;
            var respawned = 0;
            GameEvents.PlayerDamaged += d => damaged.Add(d);
            GameEvents.PlayerDied += d => died++;
            GameEvents.PlayerRespawned += () => respawned++;
            var dir = new Vector3(1f, 0f, 1f).normalized;
            var shot = new DamageInfo(20f, DamageType.Bullet, HitZone.Torso, Vector3.up, dir, null, Team.Legion, WeaponIds.AK);
            _rig.ApplyDamage(in shot);
            Assert.That(damaged.Count, Is.EqualTo(1));
            Assert.That(damaged[0].Direction, Is.EqualTo(dir), "direction reaches the HUD indicator");
            Assert.That(damaged[0].Amount, Is.EqualTo(20f), "Regular: unscaled");
            Assert.That(_rig.Health.Current, Is.EqualTo(80f));
            for (var i = 0; i < 4; i++) _rig.ApplyDamage(in shot);
            Assert.IsFalse(_rig.IsAlive);
            Assert.That(died, Is.EqualTo(1));
            _rig.ApplyDamage(in shot);
            Assert.That(died, Is.EqualTo(1), "death fires once");
            _rig.Health.ResetFull();
            Assert.IsTrue(_rig.IsAlive);
            Assert.That(respawned, Is.EqualTo(1));
        }

        [Test]
        public void FovRequestsBlendAndRelease()
        {
            Simulate(0.1f);
            var hip = _rig.CameraRig.HipVerticalFov;
            var owner = new object();
            _rig.RequestFov(owner, 30f, 0.2f);
            Simulate(0.1f);
            var mid = _rig.WorldCamera.fieldOfView;
            Assert.That(mid, Is.InRange(30.5f, hip - 0.5f), "blending");
            Simulate(0.15f);
            Assert.That(_rig.WorldCamera.fieldOfView, Is.EqualTo(30f).Within(0.05f));
            _rig.ReleaseFov(owner);
            Simulate(0.3f);
            Assert.That(_rig.WorldCamera.fieldOfView, Is.EqualTo(hip).Within(0.05f));
        }

        [Test]
        public void BinocularsGlassPoisAndReportIsGlassing()
        {
            var hud = new FakeHud();
            GameServices.Register<IHud>(hud);
            var events = new List<string>();
            GameEvents.MissionEvent += id => events.Add(id);
            _rig.SetBinocularsAvailable(true);
            Simulate(0.1f);
            Assert.IsFalse(_rig.IsGlassing);

            var poiGo = new GameObject("POI");
            _objects.Add(poiGo);
            var eye = _rig.Eye;
            poiGo.transform.position = eye.position + eye.forward * 60f + Vector3.up * 1f; // ~1° off centre
            var poi = poiGo.AddComponent<GlassingPOI>();
            poi.SetId("B1.POI1");
            MarkerRegistry.Instance.Add(poi);

            _input.Aim = true;
            Simulate(0.4f);
            Assert.IsTrue(_rig.IsGlassing, "raised in 0.3 s");
            Assert.That(hud.Overlay, Is.EqualTo(HudOverlay.Binoculars));
            Assert.That(_rig.WorldCamera.fieldOfView, Is.EqualTo(12f).Within(0.5f), "TECH_PLAN §7.3: 12° binoculars");
            Simulate(1.1f);
            Assert.That(events, Has.Member(PlayerEvents.GlassedPrefix + "B1.POI1"), "GDD §7.1: 1.0 s within 4°");
            Assert.That(events.FindAll(e => e == PlayerEvents.GlassedPrefix + "B1.POI1").Count, Is.EqualTo(1), "once");

            _input.Aim = false;
            Simulate(0.4f);
            Assert.IsFalse(_rig.IsGlassing);
            Assert.That(hud.Overlay, Is.EqualTo(HudOverlay.None));
        }

        [Test]
        public void OutOfBoundsCountdownShowsTheFailCardThenReloads()
        {
            var hud = new FakeHud();
            var vo = new FakeVOService();
            var cp = new FakeCheckpointService();
            GameServices.Register<IHud>(hud);
            GameServices.Register<IVOService>(vo);
            GameServices.Register<ICheckpointService>(cp);
            var bounds = _rig.Bounds;
            bounds.EnterVolume("OOB.RidgeWest", 6f);
            Assert.That(vo.Played, Has.Member("D093"), "GDD §17: D093 plays");
            bounds.Tick(0f, 0f);
            Assert.That(hud.Banners, Has.Member("RETURN TO THE MISSION AREA 6…"));
            for (var i = 0; i < 61; i++) bounds.Tick(0.1f, 0.1f);
            Assert.That(hud.Banners, Has.Member("RETURN TO THE MISSION AREA 1…"));
            Assert.That(hud.FailCards, Is.EqualTo(new[] { OutOfBoundsWatcher.FailCardText }));
            Assert.IsTrue(bounds.IsFailing);
            Assert.That(cp.Reloads, Is.Empty, "the card shows first");
            bounds.Tick(0f, 3f);
            Assert.That(cp.Reloads, Is.EqualTo(new[] { false }), "checkpoint reload without the death insert");
        }

        [Test]
        public void ReturningInTimeCancelsTheCountdown()
        {
            var hud = new FakeHud();
            var cp = new FakeCheckpointService();
            GameServices.Register<IHud>(hud);
            GameServices.Register<ICheckpointService>(cp);
            var bounds = _rig.Bounds;
            bounds.EnterVolume("OOB.A", 6f);
            for (var i = 0; i < 30; i++) bounds.Tick(0.1f, 0.1f);
            bounds.ExitVolume("OOB.A");
            for (var i = 0; i < 100; i++) bounds.Tick(0.1f, 0.1f);
            Assert.That(hud.FailCards, Is.Empty);
            Assert.That(cp.Reloads, Is.Empty);
            Assert.IsFalse(bounds.IsCounting);
        }

        [Test]
        public void MountParentsToTheSeatAndRaisesTheEvent()
        {
            var seatGo = new GameObject("Seat");
            _objects.Add(seatGo);
            seatGo.transform.SetPositionAndRotation(new Vector3(5f, 2f, 5f), Quaternion.Euler(0f, 90f, 0f));
            var changes = new List<bool>();
            GameEvents.PlayerMountChanged += m => changes.Add(m);

            _rig.Mount(seatGo.transform);
            Assert.That(_rig.transform.parent, Is.SameAs(seatGo.transform));
            Assert.That((_rig.transform.position - seatGo.transform.position).magnitude, Is.LessThan(1e-4f));
            Assert.IsTrue(_rig.Motor.IsMounted);
            Assert.That(_rig.Perch, Is.EqualTo(PerchTag.Vehicle));
            Simulate(0.05f);
            Assert.That(_rig.Stance, Is.EqualTo(Stance.Mounted));

            _rig.Dismount();
            Assert.That(_rig.transform.parent, Is.Null);
            Assert.IsFalse(_rig.Motor.IsMounted);
            Assert.That(changes, Is.EqualTo(new[] { true, false }));
            Assert.That(Vector3.Angle(_rig.transform.up, Vector3.up), Is.LessThan(0.01f), "upright after dismount");
        }

        [Test]
        public void OnlyHostileFireSuppresses()
        {
            _rig.Suppress(1f, Team.Squad);
            Simulate(0.3f);
            Assert.That(_rig.Look.Suppression01, Is.EqualTo(0f));
            _rig.Suppress(1f, Team.Legion);
            Simulate(0.3f);
            Assert.That(_rig.Look.Suppression01, Is.GreaterThan(0.5f));
        }

        [Test]
        public void BodyVisibilityCullsTheViewmodelWithoutDisablingTheGradingCamera()
        {
            var vm = _rig.ViewmodelCamera;
            _rig.SetBodyVisibility(false, false);
            Assert.That(vm.cullingMask, Is.EqualTo(0));
            Assert.IsTrue(vm.enabled, "Cam_Viewmodel still grades the stack");
            _rig.SetBodyVisibility(true, true);
            Assert.That(vm.cullingMask, Is.EqualTo(PlayerLayers.ViewmodelCameraCulling));
            _rig.SetViewActive(false);
            Assert.IsFalse(_rig.WorldCamera.enabled);
            Assert.IsFalse(vm.enabled);
            _rig.SetViewActive(true);
            Assert.IsTrue(_rig.WorldCamera.enabled);
        }

        [Test]
        public void CheckpointRestoreClearsTransientState()
        {
            var seatGo = new GameObject("Seat");
            _objects.Add(seatGo);
            _rig.SetBinocularsAvailable(true);
            var record = new SaveRecord { Key = PlayerRig.Key };
            ((ISaveable)_rig).Capture(new SaveWriter(record));
            _rig.SetBinocularsAvailable(false);
            _rig.SetForcedWalk(1.2f);
            _rig.SetLookLimits(Quaternion.identity, 20f, 10f, 10f);
            _rig.Mount(seatGo.transform);

            ((ISaveable)_rig).Restore(new SaveReader(record));
            Assert.That(_rig.ForcedWalk, Is.Null);
            Assert.IsFalse(_rig.Look.HasLimits);
            Assert.IsFalse(_rig.MountComponent.IsMounted);
            Assert.IsTrue(_rig.BinocularsComponent.Available, "binocular availability is part of the snapshot");
        }

        [Test]
        public void ForcedWalkCapsSpeedTarget()
        {
            _rig.SetForcedWalk(1.2f);
            Assert.That(_rig.ForcedWalk, Is.EqualTo(1.2f));
            _rig.SetForcedWalk(null);
            Assert.That(_rig.ForcedWalk, Is.Null);
            _rig.SetForcedWalk(0f);
            Assert.That(_rig.ForcedWalk, Is.Null, "0 releases");
        }
    }
}
