using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests
{
    public class ThrowTestEditorMode
    {

        // Shared error tolerance used by the ShootingRange / autograder:
        // projectile radius (0.25) + target radius (0.5), with a small safety margin.
        const float MaxErr = (0.25f + 0.5f) * 0.99f;

        // Helper: call PredictThrow, then (if it claims success) simulate the projectile and the
        // constant-velocity target forward to interceptT and assert they actually collide within err.
        static void AssertThrow(
            Vector3 projectilePos, float maxSpeed, Vector3 gravity,
            Vector3 targetInitPos, Vector3 targetVel, Vector3 targetForward,
            float maxAllowedErrorDist, bool expectReachable, string label)
        {
            bool ret = GameAIStudent.ThrowMethods.PredictThrow(
                projectilePos, maxSpeed, gravity,
                targetInitPos, targetVel, targetForward, maxAllowedErrorDist,
                out Vector3 dir, out float speed, out float interceptT, out float altT);

            Assert.AreEqual(expectReachable, ret, $"[{label}] reachability mismatch");

            if (!ret)
                return;

            // Direction must be a unit vector
            Assert.That(dir.magnitude, Is.EqualTo(1f).Within(1e-3f), $"[{label}] projectileDir not normalized");
            // Speed must not exceed the max (small float tolerance)
            Assert.That(speed, Is.LessThanOrEqualTo(maxSpeed + 1e-2f), $"[{label}] speed exceeds max");
            Assert.That(interceptT, Is.GreaterThan(0f), $"[{label}] interceptT must be positive");

            // Closed-form positions at interceptT
            Vector3 projAt = projectilePos + dir * speed * interceptT + 0.5f * gravity * interceptT * interceptT;
            Vector3 tgtAt = targetInitPos + targetVel * interceptT;
            float miss = Vector3.Distance(projAt, tgtAt);

            Assert.That(miss, Is.LessThanOrEqualTo(maxAllowedErrorDist),
                $"[{label}] projectile missed by {miss} (interceptT={interceptT}, speed={speed})");
        }

        [Test]
        public void StaticTargetBelow()
        {
            // Original template case: target straight below, gravity helps.
            AssertThrow(Vector3.zero, 5f, new Vector3(0f, -9.8f, 0f),
                new Vector3(0f, -5f, 0f), Vector3.zero, Vector3.left,
                1f, true, "StaticTargetBelow");
        }

        [Test]
        public void StaticTargetHorizontalWithGravity()
        {
            AssertThrow(new Vector3(0f, 1.4f, 0f), 20f, Physics.gravity,
                new Vector3(8f, 0.7f, 6f), Vector3.zero, Vector3.forward,
                MaxErr, true, "StaticTargetHorizontalWithGravity");
        }

        [Test]
        public void MovingTargetConstVel()
        {
            // Target crossing laterally at constant velocity; requires iterative refinement.
            AssertThrow(new Vector3(0f, 1.4f, 0f), 20f, Physics.gravity,
                new Vector3(10f, 0.7f, 5f), new Vector3(0f, 0f, 8f), Vector3.forward,
                MaxErr, true, "MovingTargetConstVel");
        }

        [Test]
        public void MovingTargetTowardThrower()
        {
            AssertThrow(new Vector3(0f, 1.4f, 0f), 20f, Physics.gravity,
                new Vector3(12f, 0.7f, 0f), new Vector3(-6f, 0f, 0f), Vector3.left,
                MaxErr, true, "MovingTargetTowardThrower");
        }

        [Test]
        public void NoGravityStraightLine()
        {
            AssertThrow(Vector3.zero, 15f, Vector3.zero,
                new Vector3(10f, 3f, 4f), new Vector3(2f, 0f, 0f), Vector3.right,
                MaxErr, true, "NoGravityStraightLine");
        }

        [Test]
        public void UnreachableTooFarForSpeed()
        {
            // Very low speed, very distant target, with gravity -> no real ballistic solution.
            bool ret = GameAIStudent.ThrowMethods.PredictThrow(
                Vector3.zero, 3f, new Vector3(0f, -9.8f, 0f),
                new Vector3(500f, 0f, 0f), Vector3.zero, Vector3.right, MaxErr,
                out _, out _, out _, out _);
            Assert.IsFalse(ret, "Far target at low speed should be unreachable");
        }

        [Test]
        public void DoesNotAlwaysReturnTrue()
        {
            // Guard against the placeholder behavior of always returning true.
            bool ret = GameAIStudent.ThrowMethods.PredictThrow(
                Vector3.zero, 1f, new Vector3(0f, -9.8f, 0f),
                new Vector3(1000f, 0f, 1000f), Vector3.zero, Vector3.right, 0.5f,
                out _, out _, out _, out _);
            Assert.IsFalse(ret, "An obviously impossible throw must return false");
        }

    }
}
