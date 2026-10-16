using System.Collections.Generic;
using LongShadow.AI.Common;
using LongShadow.AI.Enemy;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.AI
{
    /// <summary>Tests that need GameObjects: cover selection, the capsule body, enemy health rules.</summary>
    public class SceneObjectTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _made)
                if (g != null) Object.DestroyImmediate(g);
            _made.Clear();
        }

        private CoverNode Node(Vector3 p, float heading, bool standing, string group = null)
        {
            var go = new GameObject("cover");
            _made.Add(go);
            go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, heading, 0f));
            var n = go.AddComponent<CoverNode>();
            n.Configure(standing, true, true, 120f, group);
            return n;
        }

        [Test]
        public void CoverIsTheNearestUnclaimedNodeThatFacesTheThreat()
        {
            var threat = new Vector3(0f, 0f, -20f);                                                // player in the south
            var near = Node(new Vector3(2f, 0f, 0f), 180f, false);
            var far = Node(new Vector3(8f, 0f, 0f), 180f, false);
            var wrong = Node(new Vector3(1f, 0f, 0f), 0f, false);                                   // faces away from the threat
            var list = new List<CoverNode> { far, wrong, near };
            var q = new CoverQuery { Agent = 1, AgentPosition = Vector3.zero, ThreatPosition = threat, PreferredMin = 10f, PreferredMax = 35f };
            var claims = new CoverClaims();
            Assert.That(CoverSelector.Select(q, list, claims), Is.SameAs(near));
            Assert.IsTrue(claims.TryClaim(near, 2));
            Assert.That(CoverSelector.Select(q, list, claims), Is.SameAs(far), "claimed by another agent");
            Assert.IsFalse(claims.TryClaim(near, 1));
            claims.Release(2);
            Assert.IsTrue(claims.TryClaim(near, 1));
        }

        [Test]
        public void AdvanceRetreatAndGroups()
        {
            var threat = new Vector3(0f, 0f, -30f);
            var back = Node(new Vector3(0f, 0f, 8f), 180f, false);
            var fwd = Node(new Vector3(0f, 0f, -8f), 180f, false);
            var grouped = Node(new Vector3(6f, 0f, 1f), 180f, false, "B2.AlertCover");
            var list = new List<CoverNode> { back, fwd, grouped };
            var q = new CoverQuery { Agent = 1, AgentPosition = Vector3.zero, ThreatPosition = threat, Advance = true };
            Assert.That(CoverSelector.Select(q, list, null), Is.SameAs(fwd), "Legion push");
            q.Advance = false;
            q.Retreat = true;
            Assert.That(CoverSelector.Select(q, list, null), Is.SameAs(back), "morale fallback: one node back");
            q.Retreat = false;
            q.PreferredGroup = "B2.AlertCover";
            Assert.That(CoverSelector.Select(q, list, null), Is.SameAs(grouped), "wave cover group");
        }

        [Test]
        public void GreycoatsPreferFullCover()
        {
            var threat = new Vector3(0f, 0f, -20f);
            var half = Node(new Vector3(1.5f, 0f, 0f), 180f, false);
            var full = Node(new Vector3(3f, 0f, 0f), 180f, true);
            var list = new List<CoverNode> { half, full };
            var q = new CoverQuery { Agent = 1, AgentPosition = Vector3.zero, ThreatPosition = threat, PreferStanding = true };
            Assert.That(CoverSelector.Select(q, list, null), Is.SameAs(full));
            q.PreferStanding = false;
            Assert.That(CoverSelector.Select(q, list, null), Is.SameAs(half));
        }

        [Test]
        public void CapsuleBodyMovesActsAndDies()
        {
            var go = new GameObject("capsule");
            _made.Add(go);
            var body = DebugCapsuleBody.Build(go, Team.Legion, Placeholders.Material);
            body.Agent.enabled = false;
            Assert.That(go.GetComponentsInChildren<Hitbox>().Length, Is.EqualTo(3));
            body.MoveTo(new Vector3(0f, 0f, 3f), 2f);
            for (var i = 0; i < 60; i++) body.Step(1f / 60f);
            Assert.That(go.transform.position.z, Is.EqualTo(2f).Within(0.05f), "2 m/s for 1 s without a NavMesh");
            for (var i = 0; i < 60; i++) body.Step(1f / 60f);
            Assert.IsTrue(body.HasArrived);

            var done = 0;
            var events = new List<string>();
            body.AnimEvent += events.Add;
            body.PlayAction(ActionIds.BoltCut, () => done++);
            for (var i = 0; i < 60 * 2; i++) body.Step(1f / 60f);
            Assert.That(done, Is.EqualTo(0), "BoltCut takes 3 s (GDD §7.2)");
            for (var i = 0; i < 60 * 2; i++) body.Step(1f / 60f);
            Assert.That(done, Is.EqualTo(1));
            Assert.That(events, Does.Contain(AnimEventIds.ActionHit));
            body.PlayAction(ActionIds.DrillLoop, () => done++);
            Assert.That(done, Is.EqualTo(2), "loops report done once established");
            Assert.IsTrue(body.IsActionPlaying);
            body.PlayAction(AIActionIds.Stop);
            Assert.IsFalse(body.IsActionPlaying);
            PlayThrow(body, ref done);

            body.Die(new DamageInfo(100f, DamageType.Bullet, HitZone.Torso, Vector3.zero, Vector3.forward, null, Team.Player));
            Assert.IsTrue(body.IsDead);
            foreach (var hb in go.GetComponentsInChildren<Hitbox>()) Assert.That(hb.gameObject.layer, Is.EqualTo(LayerIds.Ragdoll));
            body.ResetBody();
            Assert.IsFalse(body.IsDead);
            foreach (var hb in go.GetComponentsInChildren<Hitbox>()) Assert.That(hb.gameObject.layer, Is.EqualTo(LayerIds.Hitbox));
        }

        private static void PlayThrow(DebugCapsuleBody body, ref int done)
        {
            var before = done;
            var d = done;
            body.PlayAction(ActionIds.Throw, () => d++);
            for (var i = 0; i < 90; i++) body.Step(1f / 60f);
            done = d;
            Assert.That(done, Is.EqualTo(before + 1), "Throw completes");
        }

        [Test]
        public void TechnicalShieldBlocksFrontalHitsOnly()
        {
            var go = new GameObject("technical");
            _made.Add(go);
            go.transform.rotation = Quaternion.Euler(0f, 234f, 0f);
            var h = go.AddComponent<EnemyHealth>();
            h.ApplyArchetype(100f, 1f, Team.Legion);
            h.SetShield(true, 234f, 60f);
            var front = MathX.DirectionFromBearing(234f) * 20f;
            var shooterFront = new GameObject("front");
            _made.Add(shooterFront);
            shooterFront.transform.position = front;
            var side = MathX.DirectionFromBearing(234f + 85f) * 18f;                               // the marquee, ~85° off (GDD §7.4)
            var shooterSide = new GameObject("side");
            _made.Add(shooterSide);
            shooterSide.transform.position = side;
            IDamageable d = h;
            d.ApplyDamage(new DamageInfo(40f, DamageType.Bullet, HitZone.Torso, Vector3.zero, -front.normalized, shooterFront, Team.Player));
            Assert.That(h.Current, Is.EqualTo(100f), "immune within ±60° of 234°");
            Assert.That(h.BlockedHits, Is.EqualTo(1));
            d.ApplyDamage(new DamageInfo(40f, DamageType.Bullet, HitZone.Torso, Vector3.zero, -side.normalized, shooterSide, Team.Player));
            Assert.That(h.Current, Is.EqualTo(60f));
        }

        [Test]
        public void ArchetypeHealthAndArmourAreApplied()
        {
            var go = new GameObject("greycoat");
            _made.Add(go);
            var h = go.AddComponent<EnemyHealth>();
            h.ApplyArchetype(150f, 0.8f, Team.Greycoat);
            Assert.That(h.Max, Is.EqualTo(150f));
            Assert.That(h.Current, Is.EqualTo(150f));
            Assert.That(h.ArmourFactor(HitZone.Torso), Is.EqualTo(0.8f).Within(1e-5f), "Greycoat vest (GDD §14.1)");
            Assert.That(h.Team, Is.EqualTo(Team.Greycoat));

            var s = new GameObject("sentry");
            _made.Add(s);
            var sh = s.AddComponent<EnemyHealth>();
            sh.ApplyArchetype(100f, 1f, Team.Legion);
            sh.OverrideMax(30f);                                                                   // GDD §7.2: 30 HP while unaware
            sh.ApplyDamage(new DamageInfo(10f, DamageType.Bullet, HitZone.Torso, Vector3.zero, Vector3.forward, null, Team.Player));
            sh.SetMaxKeepDamage(100f);
            Assert.That(sh.Max, Is.EqualTo(100f));
            Assert.That(sh.Current, Is.EqualTo(90f), "alerted: archetype HP, damage kept");
        }
    }
}
