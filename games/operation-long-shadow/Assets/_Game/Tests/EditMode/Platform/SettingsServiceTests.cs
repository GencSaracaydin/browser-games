using System.Collections.Generic;
using System.Text.RegularExpressions;
using LongShadow.Core;
using LongShadow.Platform;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LongShadow.Tests.EditMode.Platform
{
    /// <summary>
    /// Options persist across reloads (localStorage on the web) and fall back to GDD defaults when storage is refused
    /// (private windows); WP01 DoD.
    /// </summary>
    public class SettingsServiceTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [SetUp]
        public void ResetLogOnce() => LSLog.ResetOnce(); // WarnOnce keys survive between runs (domain reload is off)

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _objects)
                if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        private SettingsService NewService(IKeyValueStore store)
        {
            var go = new GameObject("SettingsServiceTest");
            _objects.Add(go);
            var s = go.AddComponent<SettingsService>();
            s.Initialize(store);
            return s;
        }

        [Test]
        public void FirstRunUsesGddDefaults()
        {
            var s = NewService(new KeyValueStores.MemoryStore());
            Assert.IsFalse(s.LoadedFromStorage);
            var d = new GameSettings();
            Assert.That(s.Current.MouseSensitivity, Is.EqualTo(d.MouseSensitivity));
            Assert.That(s.Current.Difficulty, Is.EqualTo(d.Difficulty));
            Assert.That(s.Difficulty.Level, Is.EqualTo(d.Difficulty));
        }

        [Test]
        public void SavedOptionsSurviveAReload()
        {
            var store = new KeyValueStores.MemoryStore();
            var a = NewService(store);
            var changed = a.Current.Clone();
            changed.MouseSensitivity = 4.5f;
            changed.FilmGrain = 0.25f;
            changed.PerformanceMode = true;
            changed.Difficulty = Difficulty.Veteran;
            a.Apply(changed);
            a.Save();
            Assert.IsNotNull(store.Get(StorageKeys.Settings));

            var b = NewService(store); // "reload": a fresh service on the same storage
            Assert.IsTrue(b.LoadedFromStorage);
            Assert.That(b.Current.MouseSensitivity, Is.EqualTo(4.5f));
            Assert.That(b.Current.FilmGrain, Is.EqualTo(0.25f));
            Assert.IsTrue(b.Current.PerformanceMode);
            Assert.That(b.Difficulty.Level, Is.EqualTo(Difficulty.Veteran));
        }

        [Test]
        public void ApplySanitisesAndNotifies()
        {
            var s = NewService(new KeyValueStores.MemoryStore());
            var raised = 0;
            s.Changed += () => raised++;
            var bad = s.Current.Clone();
            bad.MouseSensitivity = 999f;
            s.Apply(bad);
            Assert.That(raised, Is.EqualTo(1));
            Assert.That(s.Current.MouseSensitivity, Is.LessThanOrEqualTo(GameSettings.MaxSensitivity));
            Assert.That(bad.MouseSensitivity, Is.EqualTo(999f), "Apply must not modify the caller's object");
        }

        [Test]
        public void RefusingStorageKeepsTheSessionWorking()
        {
            var primary = new RefusingStore();
            var store = new KeyValueStores.FallbackStore(primary, new KeyValueStores.MemoryStore());
            var s = NewService(store);
            LogAssert.Expect(LogType.Warning, new Regex("refused"));
            var changed = s.Current.Clone();
            changed.InvertY = true;
            s.Apply(changed);
            s.Save();
            Assert.IsFalse(store.IsPersistent);
            var again = NewService(store); // same session: the memory shadow still has it
            Assert.IsTrue(again.Current.InvertY);

            var fresh = NewService(new RefusingStore()); // next visit in a private window: defaults
            Assert.IsFalse(fresh.LoadedFromStorage);
            Assert.IsFalse(fresh.Current.InvertY);
        }

        [Test]
        public void KeyValuesAndReadOnlyLaunchOptions()
        {
            var s = NewService(new KeyValueStores.MemoryStore());
            s.SetValue(StorageKeys.IntroSeen, "1");
            Assert.That(s.GetValue(StorageKeys.IntroSeen), Is.EqualTo("1"));
            s.SetValue(StorageKeys.IntroSeen, null);
            Assert.IsNull(s.GetValue(StorageKeys.IntroSeen));
            LogAssert.Expect(LogType.Warning, new Regex("read-only"));
            s.SetValue("?cp", "CP5");
            Assert.IsNull(s.GetValue(null));
        }

        [Test]
        public void CorruptJsonFallsBackToDefaults()
        {
            var store = new KeyValueStores.MemoryStore();
            store.Set(StorageKeys.Settings, "{not json");
            LogAssert.Expect(LogType.Warning, new Regex("GameSettings JSON invalid"));
            var s = NewService(store);
            Assert.That(s.Current.MouseSensitivity, Is.EqualTo(new GameSettings().MouseSensitivity));
        }

        private sealed class RefusingStore : IKeyValueStore
        {
            public string Name => "refusing";
            public bool IsPersistent => true;
            public string Get(string key) => null;
            public bool Set(string key, string value) => false;
            public bool Remove(string key) => false;
        }
    }
}
