using LongShadow.Characters.IK;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Characters
{
    public class IKTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private Transform[] Arm()
        {
            // Upper arm 0.28 m, forearm 0.26 m along +X with a slight bend (elbow plane defined).
            _root = new GameObject("Shoulder");
            var mid = new GameObject("Elbow").transform;
            mid.SetParent(_root.transform, false);
            mid.localPosition = new Vector3(0.28f, 0f, -0.01f);
            var tip = new GameObject("Hand").transform;
            tip.SetParent(mid, false);
            tip.localPosition = new Vector3(0.26f, 0f, 0.01f);
            return new[] { _root.transform, mid, tip };
        }

        [Test]
        public void TwoBoneReachesAReachableTarget()
        {
            var a = Arm();
            var target = new Vector3(0.25f, -0.2f, 0.25f);
            var rot = Quaternion.Euler(10f, 40f, 0f);
            TwoBoneIK.Solve(a[0], a[1], a[2], target, rot, 1f, 1f, new Vector3(0.2f, -0.5f, 0f), 0.5f);
            Assert.That((a[2].position - target).magnitude, Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(a[2].rotation, rot), Is.LessThan(0.01f));
            // Bone lengths are preserved.
            Assert.That((a[1].position - a[0].position).magnitude, Is.EqualTo(0.28f).Within(0.002f));
        }

        [Test]
        public void TwoBoneClampsAnUnreachableTargetWithoutNaN()
        {
            var a = Arm();
            TwoBoneIK.Solve(a[0], a[1], a[2], new Vector3(2f, 0.5f, 0f), Quaternion.identity, 1f, 0f, Vector3.zero, 0f);
            var p = a[2].position;
            Assert.That(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z), Is.False);
            Assert.That(p.magnitude, Is.LessThanOrEqualTo(0.541f));
            Assert.That(Vector3.Angle(p, new Vector3(2f, 0.5f, 0f)), Is.LessThan(0.5f), "points at the target when out of reach");
        }

        [Test]
        public void ZeroWeightLeavesThePose()
        {
            var a = Arm();
            var before = a[2].position;
            TwoBoneIK.Solve(a[0], a[1], a[2], new Vector3(0.1f, 0.1f, 0.1f), Quaternion.identity, 0f, 0f, Vector3.zero, 0f);
            Assert.That((a[2].position - before).magnitude, Is.LessThan(1e-6f));
        }

        [Test]
        public void SocketPoseMapsAGripOntoTheHand()
        {
            var a = Arm();
            var socket = new GameObject("Socket").transform;
            socket.SetParent(a[2], false);
            socket.localPosition = new Vector3(0.07f, -0.02f, 0.01f);
            socket.localRotation = Quaternion.Euler(0f, 90f, 20f);
            var gripPos = new Vector3(0.3f, -0.1f, 0.2f);
            var gripRot = Quaternion.Euler(5f, 30f, 0f);
            TwoBoneIK.TipPoseForSocket(gripPos, gripRot, socket.localPosition, socket.localRotation, a[2].lossyScale, out var p, out var r);
            TwoBoneIK.Solve(a[0], a[1], a[2], p, r, 1f, 1f, Vector3.zero, 0f);
            Assert.That((socket.position - gripPos).magnitude, Is.LessThan(0.002f), "DoD: support hand < 2 cm from the foregrip");
            Assert.That(Quaternion.Angle(socket.rotation, gripRot), Is.LessThan(0.5f));
        }

        [Test]
        public void TriangleAngleLawOfCosines()
        {
            Assert.That(TwoBoneIK.TriangleAngle(Mathf.Sqrt(2f), 1f, 1f), Is.EqualTo(Mathf.PI / 2f).Within(1e-4f));
            Assert.That(TwoBoneIK.TriangleAngle(2f, 1f, 1f), Is.EqualTo(Mathf.PI).Within(1e-3f));
        }

        [Test]
        public void AimChainPutsTheMuzzleOnTheTarget()
        {
            _root = new GameObject("Spine");
            var chest = new GameObject("Chest").transform;
            chest.SetParent(_root.transform, false);
            chest.localPosition = new Vector3(0f, 0.15f, 0f);
            var upper = new GameObject("UpperChest").transform;
            upper.SetParent(chest, false);
            upper.localPosition = new Vector3(0f, 0.15f, 0f);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(upper, false);
            muzzle.localPosition = new Vector3(0.18f, 0.05f, 0.65f); // rifle off to the right of the spine
            var bones = new[] { _root.transform, chest, upper };
            var weights = new[] { 0.25f, 0.35f, 1f };
            var target = new Vector3(-6f, 4f, 15f);
            var err = AimChain.Solve(bones, weights, 3, muzzle, Vector3.forward, target, 1f, 3, 75f);
            Assert.That(err, Is.LessThan(0.5f));
            Assert.That(AimChain.ErrorDegrees(muzzle, Vector3.forward, target), Is.LessThan(0.5f));
        }

        [Test]
        public void AimChainRespectsTheMaximumCorrection()
        {
            _root = new GameObject("Spine");
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(_root.transform, false);
            muzzle.localPosition = new Vector3(0f, 0f, 0.6f);
            var err = AimChain.Solve(new[] { _root.transform }, new[] { 1f }, 1, muzzle, Vector3.forward, new Vector3(0f, 0f, -10f), 1f, 2, 70f);
            // The clamp limits the muzzle's turn to 70°. The chain pivots at the spine, 0.6 m behind the muzzle, so the bone
            // turns a little less (about 67°) and the remaining error to the real target is about 116°, not 180 - 70.
            var turned = Quaternion.Angle(Quaternion.identity, _root.transform.rotation);
            Assert.That(turned, Is.InRange(60f, 70.5f), "a target behind is only turned toward by at most 70°");
            Assert.That(err, Is.InRange(108f, 125f), "the body must turn for the rest");
        }

        [Test]
        public void ClosestLocalAxisFindsTheBoneAxis()
        {
            _root = new GameObject("Head");
            _root.transform.rotation = Quaternion.Euler(0f, 0f, 90f); // local +X now points world up... local -Y points world right
            Assert.That(AimChain.ClosestLocalAxis(_root.transform, Vector3.up), Is.EqualTo(Vector3.right));
            Assert.That(AimChain.ClosestLocalAxis(_root.transform, Vector3.forward), Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void HandBasisFromATPoseRightHand()
        {
            // Right arm along +X, palm down, thumb toward +Z (front), index in front of the little finger.
            var b = HandBasis.Compute(Vector3.zero, new Vector3(0.09f, 0f, 0.03f), new Vector3(0.095f, 0f, 0.008f), new Vector3(0.085f, 0f, -0.035f),
                                      new Vector3(0.03f, -0.02f, 0.035f), Vector3.down);
            Assert.That(Vector3.Angle(b.Fingers, Vector3.right), Is.LessThan(8f));
            Assert.That(Vector3.Angle(b.Palm, Vector3.down), Is.LessThan(10f));
            Assert.That(Vector3.Dot(b.Across, Vector3.back), Is.GreaterThan(0.8f), "index → little points backward");
            b.PrimaryGripPose(out _, out var r);
            Assert.That(Vector3.Angle(r * Vector3.forward, b.Fingers), Is.LessThan(0.01f), "barrel along the metacarpals");
            Assert.That(Vector3.Dot(r * Vector3.up, -b.Across), Is.GreaterThan(0.99f), "top of the weapon toward the index");
        }
    }
}
