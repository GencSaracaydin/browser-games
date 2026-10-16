using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.UI.Common;
using LongShadow.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace LongShadow.Tests.EditMode.UI
{
    /// <summary>
    /// The HUD against fake services (WP08 definition of done): markers clamp with arrows and switch palette at s 2.0,
    /// subtitles format per speaker, options change ISettingsService and persist, pause / click-to-resume, the title card
    /// resolves, the debrief types with live stats, and steady-state HUD updates allocate nothing.
    /// </summary>
    public sealed class HudIntegrationTests
    {
        private HudFixture _f;

        [SetUp]
        public void SetUp() => _f = HudFixture.Create();

        [TearDown]
        public void TearDown() => _f?.Dispose();

        [Test]
        public void EveryViewIsBuilt()
        {
            Assert.GreaterOrEqual(_f.Hud.Views.Count, 25);
            Assert.IsNotNull(_f.Hud.Compass);
            Assert.IsNotNull(_f.Hud.Markers);
            Assert.IsNotNull(_f.Hud.Debrief);
            Assert.IsNotNull(_f.Hud.Pause);
            Assert.DoesNotThrow(() => _f.Tick(0.5f));
        }

        [Test]
        public void EveryHudCommandIsSafe()
        {
            IHud hud = _f.Hud;
            Assert.DoesNotThrow(() =>
            {
                hud.ShowPrompt(new PromptData { Id = "Interact", Text = "HOLD [E] Take the ledgers", Hold = true, Progress01 = 0.4f });
                hud.Tutorial("test.key", "Hold G to cook a grenade.");
                hud.IntelCard("LEDGERS", null, "INTEL 1/3");
                hud.Banner("RETURN TO THE MISSION AREA 6…", 1.15f);
                hud.SetOverlay(HudOverlay.Binoculars);
                hud.SetRing("B9.Drill", _f.PlayerObject.transform, 0.4f, "DRILL 40 %");
                hud.Letterbox(true, 0.5f);
                _f.Tick(1f);
                hud.SetOverlay(HudOverlay.DoorGun);
                hud.FailCard("YOU ABANDONED YOUR TEAM");
                _f.Tick(1f);
                hud.ClearRing("B9.Drill");
                hud.HidePrompt("Interact");
                hud.SetOverlay(HudOverlay.None);
                hud.Letterbox(false, 0f);
                hud.SetVisible(false);
                _f.Tick(0.5f);
                hud.SetVisible(true);
                _f.Tick(0.5f);
            });
            Assert.AreEqual(null, _f.Hud.Prompt.CurrentId);
        }

        [Test]
        public void PromptShowsTheMostRecentAndHides()
        {
            _f.Hud.ShowPrompt(new PromptData { Id = "Interact", Text = "[E] Open" });
            _f.Hud.ShowPrompt(new PromptData { Id = "Crate", Text = "HOLD [E] Resupply", Hold = true });
            _f.Tick(0.1f);
            Assert.AreEqual("Crate", _f.Hud.Prompt.CurrentId);
            _f.Hud.HidePrompt("Crate");
            _f.Tick(0.1f);
            Assert.AreEqual("Interact", _f.Hud.Prompt.CurrentId);
        }

        [Test]
        public void MarkersProjectClampAndHide()
        {
            var ahead = new GameObject("Ahead").transform;
            var behind = new GameObject("Behind").transform;
            var close = new GameObject("Close").transform;
            try
            {
                ahead.position = new Vector3(0f, 1.65f, 40f);
                behind.position = new Vector3(0f, 1.65f, -40f);
                close.position = new Vector3(0f, 1.65f, 2f);
                _f.Objectives.AddMarker(new MarkerDef { Id = "A", Target = ahead, Offset = new Vector3(0f, 0.001f, 0f), Kind = MarkerKind.Diamond, ShowDistance = true });
                _f.Objectives.AddMarker(new MarkerDef { Id = "B", Target = behind, Offset = new Vector3(0f, 0.001f, 0f), Kind = MarkerKind.Diamond, ShowDistance = true });
                _f.Objectives.AddMarker(new MarkerDef { Id = "C", Target = close, Offset = new Vector3(0f, 0.001f, 0f), Kind = MarkerKind.Diamond });
                _f.Tick(0.1f);
                Assert.AreEqual(2, _f.Hud.Markers.VisibleCount, "the 2 m marker is hidden (GDD §11: within 4 m)");
                Assert.IsTrue(_f.Hud.Markers.TryGetWidget(0, out _, out var edge0, out _));
                Assert.IsFalse(edge0, "the marker ahead is on screen");
                Assert.IsTrue(_f.Hud.Markers.TryGetWidget(1, out _, out var edge1, out _));
                Assert.IsTrue(edge1, "the marker behind clamps to the edge with an arrow");
            }
            finally
            {
                Object.DestroyImmediate(ahead.gameObject);
                Object.DestroyImmediate(behind.gameObject);
                Object.DestroyImmediate(close.gameObject);
            }
        }

        [Test]
        public void MarkerPaletteSwitchesAtSunset()
        {
            var t = new GameObject("Target").transform;
            try
            {
                t.position = new Vector3(0f, 1.65f, 30f);
                _f.Objectives.AddMarker(new MarkerDef { Id = "A", Target = t, Offset = new Vector3(0f, 0.001f, 0f), Kind = MarkerKind.Diamond });
                _f.Light.SetImmediate(1.9f);
                _f.Tick(0.05f);
                AssertRgb(UIColors.MarkerAmber, _f.Hud.Markers.WidgetColor(0));
                _f.Light.SetImmediate(2.0f);
                _f.Tick(0.05f);
                AssertRgb(UIColors.MarkerGreen, _f.Hud.Markers.WidgetColor(0));
                _f.Objectives.AddMarker(new MarkerDef { Id = "A", Target = t, Offset = new Vector3(0f, 0.001f, 0f), Kind = MarkerKind.HighPriority, ThroughWalls = true });
                _f.Tick(0.05f);
                AssertRgb(UIColors.Red, _f.Hud.Markers.WidgetColor(0));
            }
            finally
            {
                Object.DestroyImmediate(t.gameObject);
            }
        }

        [Test]
        public void MarkersFadeWhileAiming()
        {
            var t = new GameObject("Target").transform;
            try
            {
                t.position = new Vector3(0f, 1.65f, 30f);
                _f.Objectives.AddMarker(new MarkerDef { Id = "A", Target = t, Offset = new Vector3(0f, 0.001f, 0f), Kind = MarkerKind.Diamond });
                _f.Rig.SetAdsState(true, 1f);
                _f.Tick(0.05f);
                Assert.IsTrue(_f.Hud.Markers.TryGetWidget(0, out _, out _, out var alpha));
                Assert.AreEqual(0.3f, alpha, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(t.gameObject);
            }
        }

        [Test]
        public void SubtitlesFormatPerSpeakerAndHonourTheOptions()
        {
            GameEvents.RaiseSubtitleShow(new SubtitleData
            {
                Handle = 7, Speaker = "HOLLIS", Callsign = "SUNDIAL-ACTUAL", Text = "Glass the town.", Radio = true,
                Color = SpeakerIds.StyleOf(SpeakerIds.Hollis).Color, Duration = 2f,
            });
            _f.Tick(0.2f);
            Assert.AreEqual(1, _f.Hud.Subtitles.VisibleCount);
            StringAssert.Contains("[SUNDIAL-ACTUAL] HOLLIS:", _f.Hud.Subtitles.NewestText());

            var s = _f.Settings.Current.Clone();
            s.TranslateEnemyChatter = false;
            s.SubtitleSize = SubtitleSize.Large;
            _f.Settings.Apply(s);
            GameEvents.RaiseSubtitleShow(new SubtitleData { Handle = 8, Speaker = "LEGION", Callsign = "LEGION", Text = "(shouting) Push!", IsBark = true });
            _f.Tick(0.1f);
            Assert.AreEqual(1, _f.Hud.Subtitles.VisibleCount, "barks are hidden without translation");
            GameEvents.RaiseSubtitleShow(new SubtitleData { Handle = 9, Speaker = "SPARROW", Callsign = "SUNDIAL-2", Text = "Copy.", Color = Color.cyan });
            _f.Tick(0.1f);
            Assert.AreEqual(SubtitleFormatter.FontSize(SubtitleSize.Large), _f.Hud.Subtitles.NewestFontSize(), 0.5f);
            GameEvents.RaiseSubtitleHide(9);
            GameEvents.RaiseSubtitleHide(7);
            _f.Tick(0.5f);
            Assert.AreEqual(0, _f.Hud.Subtitles.VisibleCount);
        }

        [Test]
        public void OptionsApplyAndPersist()
        {
            _f.Hud.Options.Open(null);
            _f.Hud.Options.SetFromUser("fov", 100f);
            _f.Hud.Options.SetFromUser("difficulty", 3f);
            Assert.AreEqual(100f, _f.Settings.Current.FovHorizontal, 1e-3f);
            Assert.AreEqual(Difficulty.Veteran, _f.Settings.Current.Difficulty);
            Assert.AreEqual(2, _f.Settings.ApplyCount);
            _f.Hud.Options.Close();
            Assert.AreEqual(1, _f.Settings.SaveCount, "BACK saves (GDD §11: options persist)");
            Assert.IsFalse(_f.Hud.Options.IsOpen);
        }

        [Test]
        public void PauseAndClickToResume()
        {
            _f.Pause.Pause();
            Assert.IsTrue(_f.Hud.Pause.IsOpen);
            _f.Pause.RelockCooldownOverride = 0.6f;
            _f.Hud.Pause.PressResume();
            Assert.IsTrue(_f.Hud.Pause.WaitingForClick, "Chrome's re-lock cooldown: wait for a click");
            Assert.IsTrue(_f.Pause.IsPaused);
            _f.Hud.Pause.ClickOverlay();
            Assert.IsTrue(_f.Pause.IsPaused, "a click during the cooldown would be refused by the browser");
            _f.Pause.RelockCooldownOverride = 0f;
            _f.Tick(0.1f);
            _f.Hud.Pause.ClickOverlay();
            Assert.IsFalse(_f.Pause.IsPaused);
            Assert.IsFalse(_f.Hud.Pause.IsOpen);
        }

        [Test]
        public void QuitToSessionStopsTheMission()
        {
            _f.Pause.Pause();
            _f.Hud.Pause.ConfirmQuit();
            Assert.AreEqual("QuitToSession", _f.Director.LastCall);
            Assert.IsFalse(_f.Pause.CanPause);
            Assert.IsFalse(_f.Hud.Pause.IsOpen);
        }

        [Test]
        public void TitleCardResolvesAndRaisesItsEvent()
        {
            var events = new List<string>();
            System.Action<string> handler = events.Add;
            GameEvents.MissionEvent += handler;
            try
            {
                _f.Hud.TitleCard(8f);
                _f.Tick(1.0f);
                Assert.Greater(_f.Hud.Letterboxes.Amount, 0.99f, "bars in after 0.5 s");
                _f.Tick(6.3f);
                Assert.AreEqual(TitleCardScript.Lines[0], _f.Hud.Title.LineText(0));
                Assert.AreEqual(TitleCardScript.Lines[2], _f.Hud.Title.LineText(2));
                _f.Tick(1.0f);
                Assert.IsFalse(_f.Hud.Title.Playing);
                CollectionAssert.Contains(events, UiEvents.TitleCardFinished);
            }
            finally
            {
                GameEvents.MissionEvent -= handler;
            }
        }

        [Test]
        public void DebriefTypesTheDossierWithLiveStats()
        {
            var stats = _f.Director.Stats;
            _f.Hud.ShowDebrief(stats);
            Assert.IsTrue(_f.Hud.Debrief.IsOpen);
            Assert.IsFalse(_f.Pause.CanPause, "no pause on the debrief");
            Assert.AreEqual("1", _f.Settings.GetValue(StorageKeys.CompletedOnce), "SELECT BEAT unlocks (GDD §17)");
            for (var i = 0; i < 2400 && !_f.Hud.Debrief.Finished; i++) _f.Hud.TickAll(0.025f);
            Assert.IsTrue(_f.Hud.Debrief.Finished, "the dossier finished typing");
            StringAssert.StartsWith("MISSION TIME ....... 14:02", _f.Hud.Debrief.LineText(DebriefModelLines.StatsA));
            StringAssert.EndsWith("UNDER REVIEW", _f.Hud.Debrief.LineText(DebriefModelLines.Hollister));
            Assert.IsFalse(_f.Hud.GameplayVisible, "the gameplay HUD hides behind the debrief");
        }

        [Test]
        public void SteadyStateHudUpdatesAllocateNothing()
        {
            var t = new GameObject("Target").transform;
            try
            {
                t.position = new Vector3(5f, 1.65f, 40f);
                _f.Objectives.Set(new ObjectiveDef { Id = "B3.Push", Text = "PUSH THROUGH THE LONG MARKET", Index = 3, Total = 11 });
                _f.Objectives.AddMarker(new MarkerDef { Id = "B3.Push", Target = t, Kind = MarkerKind.Diamond, Label = "ARCH", ShowDistance = true });
                _f.Weapon.Magazine = 5; // low-ammo flash animates every frame
                _f.Health.Current = 20f; // vignette with heartbeat
                GameEvents.RaiseSubtitleShow(new SubtitleData { Handle = 3, Speaker = "TINKER", Callsign = "SUNDIAL-4", Text = "Six-forty.", Color = Color.white });
                _f.Tick(2f); // reveal, fades and first text builds settle
                Assert.That(() =>
                {
                    for (var i = 0; i < 30; i++) _f.Hud.TickAll(1f / 60f);
                }, Is.Not.AllocatingGCMemory());
            }
            finally
            {
                Object.DestroyImmediate(t.gameObject);
            }
        }

        private static void AssertRgb(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 0.01f);
            Assert.AreEqual(expected.g, actual.g, 0.01f);
            Assert.AreEqual(expected.b, actual.b, 0.01f);
        }

        private static class DebriefModelLines
        {
            public const int StatsA = DebriefModel.StatsLineA;
            public const int Hollister = DebriefModel.HollisterLine;
        }
    }
}
