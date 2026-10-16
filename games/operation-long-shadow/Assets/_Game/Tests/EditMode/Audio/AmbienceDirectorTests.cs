using LongShadow.Audio.Ambience;
using LongShadow.Audio.Core;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>TECH_PLAN §7.9 AmbienceDirector: beds follow the world, crossfade, and give way to the booth.</summary>
    public class AmbienceDirectorTests
    {
        private GameObject _go;
        private AmbienceDirector _amb;
        private AudioLibrary _lib;
        private float _now;

        private static AudioCue Bed(string id)
        {
            const float start = 2048f / 48000f;
            var clips = new[] { AudioTestUtil.Clip(id + "_01", 20.1f, 2), AudioTestUtil.Clip(id + "_02", 20.1f, 2) };
            return new AudioCue(id, AudioCategories.Ambience, clips).With3D(0f, 1f, 50f)
                .WithLoop(true, new[] { new Vector2(start, start + 20f), new Vector2(start, start + 20f) });
        }

        [SetUp]
        public void SetUp()
        {
            AudioRuntime.ResetForTests();
            _now = 0f;
            AudioRuntime.SetClockForTests(() => _now);
            _lib = ScriptableObject.CreateInstance<AudioLibrary>();
            _lib.EditorSetEntries(new[]
            {
                Bed("AMB_CICADAS"), Bed("AMB_CRICKETS"), Bed("AMB_DESERT_WIND"), Bed("AMB_TOWN_CURFEW"), Bed(AmbienceTable.BoothRoomTone),
            });
            _go = new GameObject("AmbienceTest");
            _amb = _go.AddComponent<AmbienceDirector>();
            _amb.Initialize();
            _amb.SetLibrary(_lib);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_lib);
            AudioTestUtil.Cleanup();
            AudioRuntime.SetClockForTests(null);
            AudioRuntime.ResetForTests();
        }

        private void Run(float seconds)
        {
            const float dt = 0.05f;
            for (var t = 0f; t < seconds; t += dt)
            {
                _now += dt;
                _amb.Tick(_now, dt);
            }
        }

        private static AmbienceContext Ctx(float s, ZoneType zone, int beat) =>
            new AmbienceContext { S = s, Zone = zone, Beat = beat, TransmittersOn = true };

        [Test]
        public void BedsFadeToTheirTargets()
        {
            _amb.ForceContext(Ctx(0.5f, ZoneType.Street, 3));
            Run(12f);
            var expected = AmbienceTable.Find("AMB_CICADAS").Weight(Ctx(0.5f, ZoneType.Street, 3));
            Assert.That(_amb.GainOf("AMB_CICADAS"), Is.EqualTo(expected).Within(0.01f));
            Assert.That(_amb.GainOf("AMB_CRICKETS"), Is.EqualTo(0f));
            Assert.That(_amb.BusyPlayers, Is.GreaterThanOrEqualTo(3), "cicadas, wind and the curfewed town");
        }

        [Test]
        public void SunsetSwapsCicadasForCrickets()
        {
            _amb.ForceContext(Ctx(1.9f, ZoneType.Roof, 5));
            Run(10f);
            Assert.That(_amb.GainOf("AMB_CICADAS"), Is.GreaterThan(0.3f));
            _amb.ForceContext(Ctx(1.98f, ZoneType.Roof, 5));
            Run(8f);
            Assert.That(_amb.GainOf("AMB_CICADAS"), Is.EqualTo(0f).Within(0.002f));
            Assert.That(_amb.GainOf("AMB_CRICKETS"), Is.GreaterThan(0.3f));
        }

        [Test]
        public void BoothModeReplacesTheWorld()
        {
            _amb.ForceContext(Ctx(0.5f, ZoneType.OpenDesert, 1));
            Run(10f);
            _amb.SetBoothMode(true);
            Run(16f);
            Assert.That(_amb.GainOf("AMB_DESERT_WIND"), Is.EqualTo(0f).Within(0.002f));
            Assert.That(_amb.GainOf(AmbienceTable.BoothRoomTone), Is.EqualTo(AmbienceTable.BoothLevel).Within(0.01f));
            _amb.SetBoothMode(false);
            Run(16f);
            Assert.That(_amb.GainOf(AmbienceTable.BoothRoomTone), Is.EqualTo(0f).Within(0.002f));
            Assert.That(_amb.GainOf("AMB_DESERT_WIND"), Is.GreaterThan(0.3f));
        }

        [Test]
        public void MuteSilencesEverything()
        {
            _amb.ForceContext(Ctx(0.5f, ZoneType.OpenDesert, 1));
            Run(5f);
            _amb.SetMuted(true);
            Run(20f);
            Assert.That(_amb.GainOf("AMB_DESERT_WIND"), Is.EqualTo(0f).Within(0.002f));
            Assert.That(_amb.BusyPlayers, Is.EqualTo(0), "silent beds free their sources");
        }

        [Test]
        public void MissingBedsAreSkipped()
        {
            _amb.ForceContext(Ctx(2.5f, ZoneType.Interior, 7)); // AMB_ROOMTONE_HALL is not in this library
            Assert.DoesNotThrow(() => Run(3f));
            Assert.That(_amb.GainOf("AMB_ROOMTONE_HALL"), Is.EqualTo(0f));
        }
    }
}
