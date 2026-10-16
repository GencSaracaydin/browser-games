using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Player;
using LongShadow.Player.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;

namespace LongShadow.Tests.EditMode.Player
{
    /// <summary>
    /// The whole rig simulated at a fixed 60 Hz against real colliders (TECH_PLAN §11.4 WP02 DoD): measured speeds within
    /// ±5 % of GDD §12, jump apex, vault / climb timing, slide, fall damage above 4.5 m, and zero GC allocation per frame
    /// while moving. Movement needs <c>CharacterController.Move</c> in edit mode; where the editor does not support it the
    /// movement tests report Inconclusive (Ignore) and the course telemetry covers them in play mode.
    /// </summary>
    public class PlayerMotorSimulationTests
    {
        private const float Dt = 1f / 60f;
        private const float Tolerance = 0.05f; // WP02 DoD: ±5 %
        private readonly List<GameObject> _objects = new List<GameObject>();
        private PlayerRig _rig;
        private FakeInputReader _input;

        [SetUp]
        public void SetUp()
        {
            GameServices.Clear();
            GameEvents.ResetAll();
            Box("Ground", new Vector3(0f, -0.5f, 30f), new Vector3(30f, 1f, 120f)).AddComponent<SurfaceTag>().Surface = SurfaceType.Sand;
            _rig = PlayerRigFactory.Build(new Vector3(0f, 0f, -20f), 0f);
            _objects.Add(_rig.gameObject);
            _rig.Wire();
            _input = new FakeInputReader();
            GameServices.Register<IInputReader>(_input);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_rig != null) _rig.Unwire();
            foreach (var o in _objects) if (o != null) Object.DestroyImmediate(o);
            _objects.Clear();
            GameServices.Clear();
            GameEvents.ResetAll();
            Physics.SyncTransforms();
        }

        private GameObject Box(string name, Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = center;
            go.transform.localScale = size;
            _objects.Add(go);
            return go;
        }

        private void RequireCharacterController()
        {
            if (!EditModeCharacterController.Probe(_rig.Motor.Controller))
                Assert.Ignore("CharacterController.Move does not run in EditMode here; use the Dev_PlayerCourse telemetry in play mode.");
        }

        private void Frame()
        {
            _rig.SimulateFrame(Dt);
            _input.EndFrame();
        }

        private void Simulate(float seconds)
        {
            for (var t = 0f; t < seconds - 1e-5f; t += Dt) Frame();
        }

        private float MeasureSpeed(float settle, float window)
        {
            Simulate(settle);
            var a = _rig.transform.position;
            Simulate(window);
            var b = _rig.transform.position;
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b) / window;
        }

        [TestCase("walk", 4.4f)]
        [TestCase("sprint", 6.9f)]
        [TestCase("crouch", 2.4f)]
        [TestCase("ads", 2.6f)]
        public void GroundSpeedsWithinFivePercent(string mode, float target)
        {
            RequireCharacterController();
            Simulate(0.2f);
            _input.Move = new Vector2(0f, 1f);
            _input.Sprint = mode == "sprint";
            if (mode == "crouch")
            {
                _input.CrouchDown = true;
                Frame();
            }
            float speed;
            if (mode == "ads")
            {
                // The weapon reports ADS every frame (IPlayerRig.SetAdsState); settle 0.5 s, then measure 1 s.
                var a = Vector3.zero;
                for (var i = 0; i < 90; i++)
                {
                    if (i == 30) a = _rig.transform.position;
                    _rig.SetAdsState(true, 1f);
                    Frame();
                }
                var b = _rig.transform.position;
                speed = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            }
            else
            {
                speed = MeasureSpeed(0.8f, 1f);
            }
            Assert.That(speed, Is.EqualTo(target).Within(target * Tolerance), mode + " speed (GDD §12)");
        }

        [Test]
        public void JumpApexIsOneMetre()
        {
            RequireCharacterController();
            Simulate(0.3f);
            var y0 = _rig.transform.position.y;
            _input.JumpDown = true;
            Frame();
            var max = y0;
            for (var i = 0; i < 120; i++)
            {
                Frame();
                max = Mathf.Max(max, _rig.transform.position.y);
                if (_rig.IsGrounded && i > 5) break;
            }
            Assert.That(max - y0, Is.EqualTo(1f).Within(0.04f), "GDD §12: apex 1.0 m");
            Assert.IsTrue(_rig.IsGrounded, "lands again");
        }

        [Test]
        public void AutoVaultOntoOneMetreLedgeTakesPoint45Seconds()
        {
            RequireCharacterController();
            Box("Ledge_1.0", new Vector3(0f, 0.5f, -16f), new Vector3(3f, 1f, 2f)); // face at z = -17, 3 m ahead
            Physics.SyncTransforms();
            LedgeKind kind = LedgeKind.None;
            var start = -1f;
            var end = -1f;
            var t = 0f;
            _rig.Motor.TraversalStarted += k =>
            {
                kind = k;
                start = t;
            };
            _rig.Motor.TraversalEnded += () => end = t;
            _input.Move = new Vector2(0f, 1f);
            for (var i = 0; i < 180 && end < 0f; i++)
            {
                Frame();
                t += Dt;
            }
            Assert.That(kind, Is.EqualTo(LedgeKind.Vault), "moving into a 0.6-1.4 m ledge vaults automatically");
            Assert.That(end - start, Is.EqualTo(0.45f).Within(Dt + 1e-3f), "GDD §12: 0.45 s");
            Assert.That(_rig.transform.position.y, Is.EqualTo(1f).Within(0.06f), "on top of the ledge");
        }

        [Test]
        public void ClimbOnSpaceTakesPoint8Seconds()
        {
            RequireCharacterController();
            Box("Ledge_1.8", new Vector3(0f, 0.9f, -18.5f), new Vector3(3f, 1.8f, 2f)); // face at z = -19.5
            Physics.SyncTransforms();
            _input.Move = new Vector2(0f, 1f);
            Simulate(0.3f); // walk up to the wall
            _input.Move = Vector2.zero;
            LedgeKind kind = LedgeKind.None;
            var start = -1f;
            var end = -1f;
            var t = 0f;
            _rig.Motor.TraversalStarted += k =>
            {
                kind = k;
                start = t;
            };
            _rig.Motor.TraversalEnded += () => end = t;
            _input.JumpDown = true;
            for (var i = 0; i < 120 && end < 0f; i++)
            {
                Frame();
                t += Dt;
            }
            Assert.That(kind, Is.EqualTo(LedgeKind.Climb), "1.4-2.1 m ledges are climbed on Space");
            Assert.That(end - start, Is.EqualTo(0.8f).Within(Dt + 1e-3f), "GDD §12: 0.8 s");
            Assert.That(_rig.transform.position.y, Is.EqualTo(1.8f).Within(0.06f));
        }

        [Test]
        public void SlideFromSprintRunsEightToFour()
        {
            RequireCharacterController();
            _input.Move = new Vector2(0f, 1f);
            _input.Sprint = true;
            Simulate(1f);
            Assert.That(_rig.Stance, Is.EqualTo(Stance.Sprint));
            _input.CrouchDown = true;
            Frame();
            Assert.That(_rig.Stance, Is.EqualTo(Stance.Slide));
            Assert.That(_rig.Motor.SimulatedPlanarVelocity.magnitude, Is.EqualTo(8f).Within(0.3f), "GDD §12: 8.0 m/s");
            var frames = 1;
            while (_rig.Stance == Stance.Slide && frames < 120)
            {
                Frame();
                frames++;
            }
            Assert.That(frames * Dt, Is.EqualTo(0.75f).Within(3f * Dt), "GDD §12: 0.75 s");
            Assert.That(_rig.CameraRig.EyeHeightNow, Is.LessThan(1.6f), "camera dipped during the slide");
        }

        [TestCase(4.5f, 0f)]
        [TestCase(6f, 45f)]
        public void FallDamageStartsAboveFourPointFiveMetres(float height, float expectedDamage)
        {
            RequireCharacterController();
            Box("Tower", new Vector3(0f, height * 0.5f, 10f), new Vector3(4f, height, 4f)); // top z 8..12
            Physics.SyncTransforms();
            _rig.Teleport(new Vector3(0f, height + 0.05f, 11f), 0f);
            Simulate(0.2f);
            _input.Move = new Vector2(0f, 1f);
            Simulate(0.8f); // walk off the north edge
            _input.Move = Vector2.zero;
            Simulate(1.5f);
            Assert.IsTrue(_rig.IsGrounded);
            Assert.That(_rig.Motor.LastFallHeight, Is.EqualTo(height).Within(0.1f));
            Assert.That(100f - _rig.Health.Current, Is.EqualTo(expectedDamage).Within(3.5f), "GDD §12: 30 HP per metre above 4.5 m");
        }

        [Test]
        public void NoGcAllocationPerFrameWhileMoving()
        {
            RequireCharacterController();
            _input.Move = new Vector2(0.3f, 1f).normalized;
            _input.Sprint = true;
            Simulate(1.5f); // warm-up: placeholders, caches, first footsteps
            Assert.That(() =>
            {
                for (var i = 0; i < 30; i++) _rig.SimulateFrame(Dt);
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
