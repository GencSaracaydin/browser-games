using System.Text;
using LongShadow.Core;
using LongShadow.UI.Editor;
using LongShadow.UI.Menus;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.UI
{
    /// <summary>No HUD overlap at 1920×1080, 1280×720, 4:3 and 21:9; the click gate clicks once, only when ready.</summary>
    public sealed class LayoutAndGateTests
    {
        [TearDown]
        public void TearDown() => GameServices.Clear();

        [Test]
        public void HudHasNoOverlapsAtAnySupportedAspect()
        {
            HudFixture.RequireTmp();
            var report = new StringBuilder();
            var problems = HudLayoutAudit.Run(null, report);
            Assert.IsEmpty(problems, report.ToString());
        }

        [Test]
        public void ClickGateRaisesOnceWhenReady()
        {
            HudFixture.RequireTmp();
            var go = new GameObject("Gate");
            try
            {
                var gate = go.AddComponent<ClickGateView>();
                gate.EnsureBuilt();
                var clicks = 0;
                IClickGate api = gate;
                api.Clicked += () => clicks++;
                api.SetProgress(0.5f);
                gate.SimulatePointerDown();
                Assert.AreEqual(0, clicks, "not ready: the click is ignored (TECH_PLAN D20)");
                api.SetReady(true);
                gate.SimulatePointerDown();
                gate.SimulatePointerDown();
                Assert.AreEqual(1, clicks, "one start per showing");
                api.SetVisible(false);
                Assert.IsFalse(gate.Visible);
                api.SetVisible(true);
                api.SetReady(false);
                api.SetReady(true);
                gate.SimulatePointerDown();
                Assert.AreEqual(2, clicks, "QUIT TO SESSION shows the gate again");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
