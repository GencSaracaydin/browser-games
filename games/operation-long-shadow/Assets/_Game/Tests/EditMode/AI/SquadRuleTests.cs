using LongShadow.AI.Squad;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.AI
{
    /// <summary>GDD §15: kill share, callout directions and lines, follow slots, look-at priority.</summary>
    public class SquadRuleTests
    {
        [Test]
        public void KillShareRule()
        {
            const float now = 100f;
            Assert.IsTrue(KillShare.IsLethal(Team.Legion, false, float.NegativeInfinity, now), "never engaged by the player");
            Assert.IsTrue(KillShare.IsLethal(Team.Legion, false, now - 8f, now), "not engaged for 8 s");
            Assert.IsFalse(KillShare.IsLethal(Team.Legion, false, now - 7.9f, now), "the player is fighting him");
            Assert.IsFalse(KillShare.IsLethal(Team.Greycoat, false, float.NegativeInfinity, now), "Greycoats only when flagged");
            Assert.IsTrue(KillShare.IsLethal(Team.Greycoat, true, now, now), "squad targets always");
            Assert.IsTrue(KillShare.IsLethal(Team.Legion, true, now, now));
            Assert.IsFalse(KillShare.IsLethal(null, now));
        }

        [Test]
        public void CalloutDirectionIsRelativeToTheView()
        {
            var p = Vector3.zero;
            var north = Vector3.forward;
            Assert.That(CalloutDirection.Classify(p, north, new Vector3(0f, 0f, 20f)), Is.EqualTo(RelativeDirection.Front));
            Assert.That(CalloutDirection.Classify(p, north, new Vector3(10f, 0f, 10.5f)), Is.EqualTo(RelativeDirection.Front), "44°");
            Assert.That(CalloutDirection.Classify(p, north, new Vector3(10.5f, 0f, 10f)), Is.EqualTo(RelativeDirection.Right), "46°");
            Assert.That(CalloutDirection.Classify(p, north, new Vector3(20f, 0f, 0f)), Is.EqualTo(RelativeDirection.Right));
            Assert.That(CalloutDirection.Classify(p, north, new Vector3(-20f, 0f, 0f)), Is.EqualTo(RelativeDirection.Left));
            Assert.That(CalloutDirection.Classify(p, north, new Vector3(-5f, 0f, -20f)), Is.EqualTo(RelativeDirection.Rear));
            Assert.That(CalloutDirection.Classify(p, north, new Vector3(-10f, 0f, -9.5f)), Is.EqualTo(RelativeDirection.Left), "134°");
            // Facing east, an enemy to the north is on the left.
            Assert.That(CalloutDirection.Classify(p, Vector3.right, new Vector3(0f, 0f, 30f)), Is.EqualTo(RelativeDirection.Left));
            Assert.That(CalloutDirection.Classify(p, Vector3.right, new Vector3(0f, 0f, -30f)), Is.EqualTo(RelativeDirection.Right));
            // Facing south-west.
            var sw = new Vector3(-1f, 0f, -1f).normalized;
            Assert.That(CalloutDirection.Classify(new Vector3(100f, 0f, 100f), sw, new Vector3(80f, 5f, 80f)), Is.EqualTo(RelativeDirection.Front));
            Assert.That(CalloutDirection.Classify(new Vector3(100f, 0f, 100f), sw, new Vector3(120f, 0f, 120f)), Is.EqualTo(RelativeDirection.Rear));
        }

        [Test]
        public void CalloutLinesPerMemberAndPerch()
        {
            Assert.That(CalloutIds.Direction(SquadIds.Hollis, RelativeDirection.Front), Is.EqualTo("CO_HOL_01"));
            Assert.That(CalloutIds.Direction(SquadIds.Sparrow, RelativeDirection.Left), Is.EqualTo("CO_SPA_02"));
            Assert.That(CalloutIds.Direction(SquadIds.Tinker, RelativeDirection.Rear), Is.EqualTo("CO_TIN_04"));
            Assert.That(CalloutIds.Spot(SquadIds.Hollis, RelativeDirection.Left, PerchTag.Rooftop), Is.EqualTo("CO_HOL_05"));
            Assert.That(CalloutIds.Spot(SquadIds.Sparrow, RelativeDirection.Left, PerchTag.Balcony), Is.EqualTo("CO_SPA_05"));
            Assert.That(CalloutIds.Spot(SquadIds.Tinker, RelativeDirection.Right, PerchTag.Window), Is.EqualTo("CO_TIN_05"));
            Assert.That(CalloutIds.Spot(SquadIds.Tinker, RelativeDirection.Right, PerchTag.Rooftop), Is.EqualTo("CO_TIN_03"), "no rooftop line: direction");
            Assert.That(CalloutIds.Spot(SquadIds.Hollis, RelativeDirection.Front, PerchTag.Balcony), Is.EqualTo("CO_HOL_01"));
            Assert.That(CalloutIds.Reload(SquadIds.Sparrow), Is.EqualTo("CO_SPA_09"));
            Assert.That(CalloutIds.FragOut(SquadIds.Tinker), Is.EqualTo("CO_TIN_06"));
            Assert.That(CalloutIds.Grenade(SquadIds.Hollis), Is.EqualTo("CO_HOL_07"));
            Assert.That(CalloutIds.Moving(SquadIds.Sparrow), Is.EqualTo("CO_SPA_10"));
            Assert.IsNull(CalloutIds.ScriptedKill(SquadIds.Hollis));
        }

        [Test]
        public void ElevatedEnemiesAreRooftopCallouts()
        {
            var player = new Vector3(185f, 0f, 200f);
            Assert.That(SquadCallouts.LineFor(SquadIds.Hollis, player, Vector3.forward, new Vector3(178.5f, 8f, 214f), PerchTag.Ground), Is.EqualTo("CO_HOL_05"));
            Assert.That(SquadCallouts.LineFor(SquadIds.Hollis, player, Vector3.forward, new Vector3(178.5f, 3f, 214f), PerchTag.Ground), Is.EqualTo("CO_HOL_01"));
            Assert.That(SquadCallouts.LineFor(SquadIds.Sparrow, player, Vector3.forward, new Vector3(190.5f, 4.2f, 222f), PerchTag.Balcony), Is.EqualTo("CO_SPA_05"));
        }

        [Test]
        public void FollowSlotsFromTheView()
        {
            var p = new Vector3(10f, 0f, 10f);
            var h = SquadFollowPlanner.SlotFromView(p, Vector3.forward, SquadFollowPlanner.Along[0], SquadFollowPlanner.Lateral[0]);
            Assert.That((h - p).magnitude, Is.EqualTo(SquadFollowPlanner.HollisLead).Within(1e-3f));
            Assert.That(h.z, Is.GreaterThan(p.z), "Hollis leads");
            var t = SquadFollowPlanner.SlotFromView(p, Vector3.forward, SquadFollowPlanner.Along[2], SquadFollowPlanner.Lateral[2]);
            Assert.That(t.z, Is.LessThan(p.z), "Tinker trails");
            var s = SquadFollowPlanner.SlotFromView(p, Vector3.forward, SquadFollowPlanner.Along[1], SquadFollowPlanner.Lateral[1]);
            var ds = (s - p).magnitude;
            Assert.That(ds, Is.InRange(3f, 6f), "others 3-6 m from the player (GDD §15)");
            Assert.That((t - p).magnitude, Is.InRange(3f, 6f));
            Assert.That(SquadFollowPlanner.HollisLead, Is.InRange(SquadFollowPlanner.HollisLeadMin, SquadFollowPlanner.HollisLeadMax));
        }

        [Test]
        public void HollisRepathsOutsideHisBand()
        {
            var player = Vector3.zero;
            var slot = new Vector3(0f, 0f, 8f);
            Assert.IsFalse(SquadFollowPlanner.NeedsRepath(0, new Vector3(0f, 0f, 8f), slot, player));
            Assert.IsFalse(SquadFollowPlanner.NeedsRepath(0, new Vector3(0f, 0f, 6.5f), slot, player));
            Assert.IsTrue(SquadFollowPlanner.NeedsRepath(0, new Vector3(0f, 0f, 13f), slot, player), "more than 12 m ahead");
            Assert.IsTrue(SquadFollowPlanner.NeedsRepath(0, new Vector3(0f, 0f, 2f), slot, player), "fell back into the player");
            Assert.IsTrue(SquadFollowPlanner.NeedsRepath(1, new Vector3(3f, 0f, 4f), new Vector3(0f, 0f, 4f), player));
            Assert.IsFalse(SquadFollowPlanner.NeedsRepath(1, new Vector3(1f, 0f, 4f), new Vector3(0f, 0f, 4f), player));
        }

        [Test]
        public void LookAtPriority()
        {
            Assert.That(SquadLookAt.Choose(true, true, true, true), Is.EqualTo(LookSource.Override));
            Assert.That(SquadLookAt.Choose(false, true, true, true), Is.EqualTo(LookSource.Speaker));
            Assert.That(SquadLookAt.Choose(false, false, true, true), Is.EqualTo(LookSource.Player));
            Assert.That(SquadLookAt.Choose(false, false, false, true), Is.EqualTo(LookSource.Threat));
            Assert.That(SquadLookAt.Choose(false, false, false, false), Is.EqualTo(LookSource.None));
        }
    }
}
