using LongShadow.Core;
using LongShadow.World.Look.Stage;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.World.Look
{
    /// <summary>The ILightStage service: events (GDD §9.1), PracticalsStrike at 1.97, overrides, checkpoint state.</summary>
    public sealed class LightStageControllerTests
    {
        private GameObject _go;
        private LightStageController _c;
        private bool _fog;
        private float _fogDensity;
        private Color _fogColor;
        private int _strikes;
        private int _changes;
        private StageId _lastStage;
        private float _lastS;

        [SetUp]
        public void SetUp()
        {
            _fog = RenderSettings.fog;
            _fogDensity = RenderSettings.fogDensity;
            _fogColor = RenderSettings.fogColor;
            _go = new GameObject("LightStageTest");
            _c = _go.AddComponent<LightStageController>();
            _strikes = 0;
            _changes = 0;
            GameEvents.PracticalsStrike += OnStrike;
            GameEvents.LightStageChanged += OnChanged;
            GameEvents.StageChanged += OnStage;
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.PracticalsStrike -= OnStrike;
            GameEvents.LightStageChanged -= OnChanged;
            GameEvents.StageChanged -= OnStage;
            _c.EditorReleasePreview();
            Object.DestroyImmediate(_go);
            RenderSettings.fog = _fog;
            RenderSettings.fogDensity = _fogDensity;
            RenderSettings.fogColor = _fogColor;
        }

        private void OnStrike() => _strikes++;

        private void OnChanged(float s)
        {
            _changes++;
            _lastS = s;
        }

        private void OnStage(StageId s) => _lastStage = s;

        [Test]
        public void ScriptedDriveStrikesThePracticalsOnce()
        {
            _c.SetImmediate(1.8f);
            Assert.AreEqual(0, _strikes);
            _c.SetTarget(1.94f, 0f);
            _c.SetTarget(1.96f, 0f);
            Assert.AreEqual(0, _strikes);
            _c.SetTarget(1.97f, 0f);
            Assert.AreEqual(1, _strikes);
            _c.SetTarget(2.0f, 0f);
            Assert.AreEqual(1, _strikes);
            Assert.AreEqual(StageId.Afterglow, _lastStage);
            Assert.IsTrue(_c.AfterSunset);
            Assert.AreEqual(LightStageTable.HudGreen, _c.HudColor);
        }

        [Test]
        public void CheckpointJumpsDoNotStrike()
        {
            _c.SetImmediate(1.5f);
            _c.SetImmediate(2.5f);
            Assert.AreEqual(0, _strikes);
            Assert.AreEqual(2.5f, _c.S, 1e-5f);
            Assert.AreEqual(2.5f, _lastS, 1e-5f);
            Assert.AreEqual(StageId.Afterglow, _c.Stage);
            Assert.AreEqual(45f, _c.AISightRange, 1e-4f);
        }

        [Test]
        public void ChangesAreReportedInHundredthSteps()
        {
            _c.SetImmediate(0f);
            var before = _changes;
            _c.SetTarget(0.004f, 0f);
            Assert.AreEqual(before, _changes, "less than a hundredth: no event");
            _c.SetTarget(0.012f, 0f);
            Assert.AreEqual(before + 1, _changes, "crossing 0.01 reports");
            _c.SetTarget(1.995f, 0f);
            _c.SetTarget(2.0f, 0f);
            Assert.AreEqual(2.0f, _lastS, 1e-5f, "sunset itself is always reported");
            _c.SetImmediate(0f);
            before = _changes;
            for (var i = 1; i <= 100; i++) _c.SetTarget(i * 0.001f, 0f);
            Assert.LessOrEqual(_changes - before, 11);
            Assert.AreEqual(0.1f, _lastS, 1e-4f);
        }

        [Test]
        public void OverrideIsReferenceCounted()
        {
            var a = new object();
            var b = new object();
            _c.Override(a, true);
            _c.Override(b, true);
            _c.Override(a, false);
            Assert.IsTrue(_c.IsOverridden);
            _c.Override(b, false);
            Assert.IsFalse(_c.IsOverridden);
        }

        [Test]
        public void FogFollowsTheStage()
        {
            _c.SetImmediate(3.5f);
            var expected = LightStageTable.Sample(3.5f).FogDensity * _c.Tuning.FogDensityScale;
            Assert.AreEqual(expected, RenderSettings.fogDensity, 1e-6f);
        }

        [Test]
        public void SaveRoundTripRestoresTheStage()
        {
            _c.SetImmediate(2.37f);
            var record = new SaveRecord { Key = _c.SaveKey };
            _c.Capture(new SaveWriter(record));
            _c.SetImmediate(0.2f);
            _c.Restore(new SaveReader(record));
            Assert.AreEqual(2.37f, _c.S, 1e-4f);
            Assert.AreEqual("LightStage", _c.SaveKey);
        }

        [Test]
        public void StoryClockFollowsS()
        {
            _c.SetImmediate(1.94f);
            Assert.AreEqual("18:40", LightStageTable.FormatClock(_c.StoryClockMinutes));
        }
    }
}
