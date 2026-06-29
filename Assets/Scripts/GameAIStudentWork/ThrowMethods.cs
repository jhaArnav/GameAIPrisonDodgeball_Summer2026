using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

using GameAI;


namespace GameAIStudent
{

    public class ThrowMethods
    {

        public const string StudentName = "Arnav Jha";


        // Note: You have to implement the following method with prediction:
        // Either directly solved (e.g. Law of Cosines or similar) or iterative.
        // You cannot modify the method signature. However, if you want to do more advanced
        // prediction (such as analysis of the navmesh) then you can make another method that calls
        // this one.
        // Be sure to run the editor mode unit test to confirm that this method runs without
        // any gamemode-only logic
        public static bool PredictThrow(
            // The initial launch position of the projectile
            Vector3 projectilePos,
            // The initial ballistic speed of the projectile
            float maxProjectileSpeed,
            // The gravity vector affecting the projectile (likely passed as Physics.gravity)
            Vector3 projectileGravity,
            // The initial position of the target
            Vector3 targetInitPos,
            // The constant velocity of the target (zero acceleration assumed)
            Vector3 targetConstVel,
            // The forward facing direction of the target. Possibly of use if the target
            // velocity is zero
            Vector3 targetForwardDir,
            // For algorithms that approximate the solution, this sets a limit for how far
            // the target and projectile can be from each other at the interceptT time
            // and still count as a successful prediction
            float maxAllowedErrorDist,
            // Output param: The solved projectileDir for ballistic trajectory that intercepts target
            out Vector3 projectileDir,
            // Output param: The speed the projectile is launched at in projectileDir such that
            // there is a collision with target. projectileSpeed must be <= maxProjectileSpeed
            out float projectileSpeed,
            // Output param: The time at which the projectile and target collide
            out float interceptT,
            // Output param: An alternate time at which the projectile and target collide
            // Note that this is optional to use and does NOT coincide with the solved projectileDir
            // and projectileSpeed. It is possibly useful to pass on to an incremental solver.
            // It only exists to simplify compatibility with the ShootingRange
            out float altT)
        {
            // APPROACH:
            // Use an exact, closed-form ballistic "firing solution" (Millington) that solves for the
            // launch velocity that hits a STATIC aim point at fixed speed under gravity. Then wrap that
            // static solver in iterative refinement: repeatedly project the target forward along its
            // constant velocity by the current flight time, re-solve, and converge on the true intercept.
            //
            // This only uses pure math (no live game state), so it is valid for the EditorMode tests.

            const int maxIterations = 16;
            const float convergenceTol = 1e-5f;

            // Sensible defaults so all out params are assigned on every path.
            projectileDir = Vector3.up;
            projectileSpeed = maxProjectileSpeed;
            interceptT = 0f;
            altT = -1f;

            // Initial flight-time guess: straight-line time to the target's current position.
            float distToTarget = Vector3.Distance(projectilePos, targetInitPos);
            float tEstimate = (maxProjectileSpeed > 1e-6f) ? distToTarget / maxProjectileSpeed : 0f;

            bool solved = false;

            for (int i = 0; i < maxIterations; i++)
            {
                // Where the target is predicted to be after the current estimated flight time
                Vector3 aimPoint = targetInitPos + targetConstVel * tEstimate;

                if (!SolveStaticArc(projectilePos, maxProjectileSpeed, projectileGravity, aimPoint,
                        out Vector3 dir, out float speed, out float tPrimary, out float tAlt))
                {
                    // The predicted aim point cannot be reached at max speed -> not a valid throw
                    projectileDir = Vector3.up;
                    projectileSpeed = maxProjectileSpeed;
                    interceptT = 0f;
                    altT = -1f;
                    return false;
                }

                projectileDir = dir;
                projectileSpeed = speed;
                interceptT = tPrimary;
                altT = tAlt;
                solved = true;

                // Converged when the solved flight time matches the time we aimed ahead by
                if (Mathf.Abs(tPrimary - tEstimate) < convergenceTol)
                    break;

                tEstimate = tPrimary;
            }

            if (!solved)
                return false;

            // Validate the converged solution against the ACTUAL moving target.
            // The projectile, by construction, lands exactly on the last aim point at interceptT.
            // Compare that against where the target really is at interceptT.
            Vector3 projectileImpact = projectilePos
                                       + projectileDir * projectileSpeed * interceptT
                                       + 0.5f * projectileGravity * interceptT * interceptT;
            Vector3 targetAtImpact = targetInitPos + targetConstVel * interceptT;
            float missDist = Vector3.Distance(projectileImpact, targetAtImpact);

            // Must be a forward-in-time intercept that lands within the allowed error
            return interceptT > 0f && missDist <= maxAllowedErrorDist;
        }


        // Exact closed-form ballistic firing solution for a STATIC aim point.
        // Solves for a launch velocity of magnitude == maxSpeed such that a projectile launched from
        // projectilePos under the given (constant) gravity passes through aimPoint.
        //
        // Derivation: projectile position P(t) = P0 + V0*t + 0.5*g*t^2.
        // Setting P(t) = aim and requiring |V0| = v gives a quadratic in u = t^2:
        //     (0.25*|g|^2) u^2  -  (delta.g + v^2) u  +  |delta|^2  =  0
        // The two roots are the flat (shorter time) and lofted (longer time) arcs.
        //
        // Returns false if the aim point is unreachable at the given speed (no real, positive-time root).
        static bool SolveStaticArc(
            Vector3 projectilePos,
            float maxSpeed,
            Vector3 gravity,
            Vector3 aimPoint,
            out Vector3 projectileDir,
            out float projectileSpeed,
            out float interceptT,
            out float altT)
        {
            projectileDir = Vector3.up;
            projectileSpeed = maxSpeed;
            interceptT = 0f;
            altT = -1f;

            Vector3 delta = aimPoint - projectilePos;
            float v = maxSpeed;
            float v2 = v * v;

            // Degenerate: aim point is essentially the launch position
            if (delta.sqrMagnitude < 1e-8f)
            {
                interceptT = 0f;
                return true;
            }

            if (v2 < 1e-8f)
                return false; // can't throw with no speed

            float gSqr = gravity.sqrMagnitude;

            // No gravity -> straight-line shot at full speed
            if (gSqr < 1e-8f)
            {
                float tLine = delta.magnitude / v;
                interceptT = tLine;
                altT = tLine;
                projectileDir = delta.normalized;
                projectileSpeed = v;
                return true;
            }

            // Quadratic in u = t^2:  a u^2 + b u + c = 0
            float a = 0.25f * gSqr;
            float b = -(Vector3.Dot(delta, gravity) + v2);
            float c = delta.sqrMagnitude;

            float disc = b * b - 4f * a * c;
            if (disc < 0f)
                return false; // out of range at this speed

            float sqrtDisc = Mathf.Sqrt(disc);
            float u1 = (-b - sqrtDisc) / (2f * a); // smaller root -> shorter flight (flatter arc)
            float u2 = (-b + sqrtDisc) / (2f * a); // larger root  -> longer flight (lofted arc)

            // Pick the smallest strictly-positive time as the primary (flatter, faster, less drift).
            float uPrimary, uAlt;
            if (u1 > 1e-6f)
            {
                uPrimary = u1;
                uAlt = u2;
            }
            else if (u2 > 1e-6f)
            {
                uPrimary = u2;
                uAlt = u1;
            }
            else
            {
                return false; // no positive-time solution
            }

            float tPrimary = Mathf.Sqrt(uPrimary);
            interceptT = tPrimary;
            altT = (uAlt > 1e-6f) ? Mathf.Sqrt(uAlt) : -1f;

            // Recover the launch velocity:  V0 = delta/t - 0.5*g*t   (magnitude == v by construction)
            Vector3 v0 = delta / tPrimary - 0.5f * gravity * tPrimary;
            projectileSpeed = v0.magnitude;
            projectileDir = (projectileSpeed > 1e-6f) ? (v0 / projectileSpeed) : Vector3.up;

            return true;
        }



    }

}
