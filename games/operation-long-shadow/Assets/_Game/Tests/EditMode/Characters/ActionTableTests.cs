using System.Collections.Generic;
using System.Reflection;
using LongShadow.Characters.Animation;
using LongShadow.Characters.Editor;
using LongShadow.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Characters
{
    public class ActionTableTests
    {
        private static IEnumerable<string> ConstStrings(System.Type t)
        {
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
                if (f.IsLiteral && f.FieldType == typeof(string)) yield return (string)f.GetRawConstantValue();
        }

        [Test]
        public void EveryCoreActionIdHasARow()
        {
            // WP00 contract: WP05 maps every ActionIds id (unknown ids would complete immediately).
            foreach (var id in ConstStrings(typeof(ActionIds)))
                Assert.That(ActionTable.TryGet(id, out _), Is.True, "ActionTable has no row for Core ActionIds." + id);
        }

        [Test]
        public void EveryExtraActionIdHasARow()
        {
            foreach (var id in ConstStrings(typeof(ExtraActionIds)))
                Assert.That(ActionTable.TryGet(id, out _), Is.True, "ActionTable has no row for ExtraActionIds." + id);
        }

        [Test]
        public void AnimatorIndicesAreUniquePerLayerAndResolvable()
        {
            var seen = new HashSet<string>();
            foreach (var def in ActionTable.All)
            {
                if (!def.HasState) continue;
                Assert.That(def.Index, Is.GreaterThan(0), def.Id);
                Assert.That(seen.Add(def.Layer + ":" + def.Index), Is.True, "duplicate index " + def.Index + " on " + def.Layer + " (" + def.Id + ")");
                Assert.That(ActionTable.ByIndex(def.Layer, def.Index), Is.SameAs(def));
                Assert.That(def.ClipKey, Is.Not.Null.And.Not.Empty, def.Id);
            }
            Assert.That(ActionTable.MaxIndex(ActionLayer.FullBody), Is.GreaterThan(0));
            Assert.That(ActionTable.MaxIndex(ActionLayer.Arms), Is.GreaterThan(0));
        }

        [Test]
        public void EveryActionClipKeyIsInTheCatalog()
        {
            foreach (var def in ActionTable.All)
            {
                if (!def.HasState) continue;
                var known = false;
                foreach (var k in ClipCatalog.Keys) known |= k == def.ClipKey;
                Assert.That(known, Is.True, def.Id + " uses clip key " + def.ClipKey + " that ClipCatalog does not list");
            }
        }

        [Test]
        public void ArmsActionsForTheInsertionAreArmsLayer()
        {
            // CINEMATIC_DIRECTION §2: the handset and camera play while seated in the cabin, so they must not end SitHeli.
            Assert.That(ActionTable.TryGet(ActionIds.HandsetToEar, out var handset), Is.True);
            Assert.That(handset.Layer, Is.EqualTo(ActionLayer.Arms));
            Assert.That(ActionTable.TryGet(ActionIds.RaiseCamera, out var cam), Is.True);
            Assert.That(cam.Layer, Is.EqualTo(ActionLayer.Arms));
            Assert.That(ActionTable.TryGet(ActionIds.SitHeli, out var sit), Is.True);
            Assert.That(sit.Layer, Is.EqualTo(ActionLayer.FullBody));
            Assert.That(sit.Loop, Is.True);
        }

        [Test]
        public void TimingsMatchThePlan()
        {
            // TECH_PLAN §7.7: BoltCut 3 s, PlantSatchel 3 s; GDD §14.1: RPG raise 1.2 s.
            ActionTable.TryGet(ActionIds.BoltCut, out var bolt);
            ActionTable.TryGet(ActionIds.PlantSatchel, out var plant);
            ActionTable.TryGet(ActionIds.RpgRaise, out var rpg);
            Assert.That(bolt.Seconds, Is.EqualTo(3f));
            Assert.That(plant.Seconds, Is.EqualTo(3f));
            Assert.That(rpg.Seconds, Is.EqualTo(1.2f));
            Assert.That(bolt.LengthFor(9f), Is.EqualTo(3f));
        }

        [Test]
        public void StateSpeedFitsTimedOneShotsWithinLimits()
        {
            ActionTable.TryGet(ActionIds.PlantSatchel, out var plant); // 3 s
            Assert.That(plant.StateSpeedFor(6f, false), Is.EqualTo(1.5f).Within(1e-4f), "a 6 s clip is capped at 1.5x");
            Assert.That(plant.StateSpeedFor(3.6f, false), Is.EqualTo(1.2f).Within(1e-4f));
            Assert.That(plant.StateSpeedFor(1f, false), Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(plant.StateSpeedFor(6f, true), Is.EqualTo(1f), "looping clips keep their speed");
            ActionTable.TryGet(ActionIds.Reload, out var reload); // clip length, no fit
            Assert.That(reload.StateSpeedFor(3.3f, false), Is.EqualTo(1f));
        }

        [Test]
        public void HeldMeansLoopOrTimed()
        {
            foreach (var def in ActionTable.All)
                Assert.That(def.Held, Is.EqualTo(def.Loop || def.Seconds > 0f), def.Id);
        }
    }
}
