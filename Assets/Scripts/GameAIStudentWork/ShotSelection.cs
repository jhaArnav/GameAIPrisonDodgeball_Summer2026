using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

using GameAI;


namespace GameAIStudent
{

    public class ShotSelection
    {

        public const string StudentName = "Arnav Jha";

        // --- Tunable shot-selection thresholds (adjust after a PlayMode test run if needed) ---
        // Max frame-to-frame acceleration (m/s^2) still considered "constant velocity".
        // The velocity-delta magnitude captures BOTH speed changes and turning at once.
        const float MaxSteadyAccel = 1.0f;
        // Below this speed (m/s) the opponent is treated as effectively stopped (a static target).
        const float StoppedSpeed = 0.1f;
        // Approximate dodgeball radius, used to space the twin occlusion rays.
        const float BallRadius = 0.25f;


        public enum SelectThrowReturn
        {
            DoThrow,
            NoThrowTargettingFailed,
            NoThrowOpponentCurrentlyAccelerating,
            NoThrowOpponentWillAccelerate,
            NoThrowOpponentOccluded
        }

        public static SelectThrowReturn SelectThrow(
                // the minion doing the throwing, can also be used to query generic params true of all minions
                MinionScript thisMinion,
                // info about the target
                PrisonDodgeballManager.OpponentInfo opponent,
                // What is the navmask that defines where on the navmesh the opponent can traverse
                int opponentNavmask,
                // typically this is a value a tiny bit smaller than the radius of minion added with radius of the dodgeball
                float maxAllowedThrowErrDist,
                // Time since last frame
                float deltaT,
                // Output param: The solved projectileDir for ballistic trajectory that intercepts target                
                out Vector3 projectileDir,
                // Output param: The speed the projectile is launched at in projectileDir such that
                // there is a collision with target. projectileSpeed must be <= maxProjectileSpeed
                out float projectileSpeed,
                // Output param: The time at which the projectile and target collide
                out float interceptT,
                // Output param: where the shot is expected to hit
                out Vector3 interceptPos
            )
        {
            var Mgr = PrisonDodgeballManager.Instance;


            var opponentVel = opponent.Vel; // Or perhaps use thisMinion.MaxPathSpeed (max speed a minion can go)
                                            // times dir if you think minion is nearly there.
                                            // Using something other than the opponent's current Vel requires extra logic

            interceptPos = opponent.Pos;

            // see if throw is even possible, before deciding whether to actually do it
            if (!ThrowMethods.PredictThrow(thisMinion.HeldBallPosition, thisMinion.ThrowSpeed, Physics.gravity, opponent.Pos,
                opponentVel, opponent.Forward, maxAllowedThrowErrDist,
                out projectileDir, out projectileSpeed, out interceptT, out float altT))
            {
                return SelectThrowReturn.NoThrowTargettingFailed;
            }

            interceptPos = opponent.Pos + opponent.Vel * interceptT;

            // OK, the throw is possible based on assumptions. But there are other reasons why we might skip throwing right now.


            // (1) Screen for the opponent breaking the constant-velocity assumption.
            // The frame-to-frame velocity change captures both speed changes (ramping up/slowing
            // down) AND direction changes (turning) in a single magnitude. If it's large, the
            // opponent is accelerating right now, so our prediction (which assumes constant
            // velocity) is unreliable -> defer.
            float speed = opponentVel.magnitude;
            float accelMag = (opponent.Vel - opponent.PrevVel).magnitude / Mathf.Max(deltaT, 1e-5f);
            if (accelMag > MaxSteadyAccel)
                return SelectThrowReturn.NoThrowOpponentCurrentlyAccelerating;


            // (2) Consider how the environment will force a future change in the opponent's motion.
            // If a NavMesh.Raycast from the opponent toward the predicted intercept hits a navmesh
            // edge, the opponent would "run into" a barrier before the ball arrives. It won't
            // actually do that, which means it is about to turn or stop -> our straight-line
            // prediction is wrong -> defer. (Only meaningful while the opponent is moving.)
            if (speed > StoppedSpeed)
            {
                if (NavMesh.Raycast(opponent.Pos, interceptPos, out NavMeshHit navHit, opponentNavmask))
                    return SelectThrowReturn.NoThrowOpponentWillAccelerate;
            }


            // (3) Consider whether the ball will be blocked by geometry before reaching the intercept.
            // Important for AdvancedMinionTestThrowScenario (poles/walls between thrower and target).
            // Cast two parallel rays spaced a ball-width apart so a thin obstacle clipping either
            // edge of the ball is caught.

            // carverMask exclusion only needed for AdvancedMinionTestThrowScenario
            int carverMask = ~(1 << Mgr.NavMeshCarverLayerIndex);
            // We don't care about minion hits from raycast. Self hits should already be avoided but will filter all minions.
            // And the whole point of the throw is to hit the opponent minion, so we don't want a raycast hit stopping us.
            int minionMask = ~(1 << Mgr.MinionTeamBLayerIndex) & ~(1 << Mgr.MinionTeamALayerIndex);
            // Ignore dodgeballs. They'll most likely be out of the way before they collide
            int ballMask = ~(1 << Mgr.BallTeamALayerIndex) & ~(1 << Mgr.BallTeamBLayerIndex);
            int mask = Physics.AllLayers & carverMask & ballMask & minionMask;

            Vector3 ballPos = thisMinion.HeldBallPosition;
            Vector3 toIntercept = interceptPos - ballPos;
            float losDist = toIntercept.magnitude;
            if (losDist > 1e-4f)
            {
                Vector3 losDir = toIntercept / losDist;

                // Perpendicular offset (horizontal) to separate the two rays by the ball's width.
                Vector3 perp = Vector3.Cross(losDir, Vector3.up);
                if (perp.sqrMagnitude < 1e-6f)
                    perp = Vector3.Cross(losDir, Vector3.forward);
                perp = perp.normalized * BallRadius;

                if (Physics.Raycast(ballPos + perp, losDir, losDist, mask) ||
                    Physics.Raycast(ballPos - perp, losDir, losDist, mask))
                    return SelectThrowReturn.NoThrowOpponentOccluded;
            }

            // We got this far, so the throw is probably a good idea!
            return SelectThrowReturn.DoThrow;
        }



    }


}