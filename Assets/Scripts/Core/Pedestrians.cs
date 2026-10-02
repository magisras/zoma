using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>
    /// People crossing the road. RESEARCH.md: pedestrians "cross on estimated gap; 'hand' confidence
    /// makes them step out in front of small vehicles and hesitate for buses." And the street's one
    /// hard rule: don't hit a person. TrafficSim ends the day when the player does.
    ///
    /// Nobody crosses a lane-free road in one go. A pedestrian crosses it band by band: for every
    /// vehicle coming, they ask "can I be clear of *its* strip of road before it gets here?" If yes,
    /// they walk; if not, they stop in the road and let it pass; if they are already inside its
    /// strip, they hurry. Small vehicle: they demand less margin and raise a hand. Bus: more.
    /// </summary>
    public static class Pedestrians
    {
        /// <summary>Extra lateral room a pedestrian keeps from a passing vehicle's side, metres.</summary>
        private const float SideMargin = 0.4f;

        public static void Step(TrafficSim sim, Agent p, float dt)
        {
            PedestrianSettings tuning = sim.Tuning.Pedestrians;
            Corridor corridor = sim.Corridor;
            float kerb = corridor.HalfWidth + sim.Tuning.Spawn.KerbOffsetMetres;

            if (p.PedState == PedestrianState.Waiting)
            {
                p.WaitTimer -= dt;
                float direction = p.Lateral > 0f ? -1f : 1f;              // toward the other kerb
                if (p.WaitTimer <= 0f && Decide(sim, p, direction, tuning) == Verdict.Walk)
                {
                    p.PedState = PedestrianState.Crossing;
                    p.CrossDirection = direction;
                }
            }
            else
            {
                Verdict verdict = Decide(sim, p, p.CrossDirection, tuning);
                p.Speed = verdict == Verdict.Stop ? 0f : verdict == Verdict.Hurry ? tuning.WalkSpeed * 1.6f : tuning.WalkSpeed;
                p.Lateral += p.CrossDirection * p.Speed * dt;

                bool arrived = p.CrossDirection > 0f ? p.Lateral >= kerb : p.Lateral <= -kerb;
                if (arrived)
                {
                    p.Lateral = kerb * p.CrossDirection;
                    p.PedState = PedestrianState.Waiting;
                    p.Speed = 0f;
                    p.WaitTimer = sim.Random.Range(tuning.WaitMinSeconds, tuning.WaitMaxSeconds);
                }
            }

            p.Position = corridor.PositionAt(p.S, p.Lateral);
            // Face across the road while crossing, along it while waiting.
            p.Yaw = corridor.YawAt(p.S) + (p.PedState == PedestrianState.Crossing ? p.CrossDirection * Mathf.PI * 0.5f : 0f);
        }

        private enum Verdict { Walk, Stop, Hurry }

        /// <summary>
        /// Look at every approaching vehicle. Each one owns a strip of road (its lateral band). For the
        /// strips still ahead of the pedestrian: will they be past that strip's far side before the
        /// vehicle arrives, with the margin they demand of that kind of vehicle? For the strip they are
        /// standing in: hurry. Anything else: walk.
        /// </summary>
        private static Verdict Decide(TrafficSim sim, Agent p, float direction, PedestrianSettings tuning)
        {
            Verdict verdict = Verdict.Walk;
            for (int i = 0; i < sim.Agents.Count; i++)
            {
                Agent v = sim.Agents[i];
                if (v.IsPedestrian || v.Corridor != p.Corridor) continue;
                float ds = p.Corridor.DeltaS(p.S, v.S);                  // negative = vehicle still coming
                if (Mathf.Abs(ds) < v.HalfLength + 1f)
                {
                    // Alongside the crossing point right now: its strip is a wall until it has passed.
                    if (StripIsAhead(p, v, direction) || Inside(p, v)) return Verdict.Stop;
                    continue;
                }
                if (ds > 0f) continue;                                    // already past
                float distance = -ds - v.HalfLength;
                if (distance > tuning.LookMetres) continue;
                // A vehicle standing still further away is parked: walk round it. One standing still
                // close by might pull away any moment, so assume it will, at a walking-ish pace.
                float speed = v.Speed;
                if (speed < 0.3f)
                {
                    if (distance > tuning.PullAwayWatchMetres) continue;
                    speed = tuning.PullAwayAssumedSpeed;
                }

                float secondsAway = distance / speed;

                if (Inside(p, v))
                {
                    // Already in its strip: get out of it, now.
                    if (secondsAway < 4f) verdict = Verdict.Hurry;
                    continue;
                }
                if (!StripIsAhead(p, v, direction)) continue;             // its strip is behind me

                float farSide = v.Lateral + direction * (v.HalfWidth + SideMargin);
                float secondsToClear = Mathf.Abs(farSide - p.Lateral) / tuning.WalkSpeed;

                float margin = tuning.CrossingGapSeconds;
                if (v.Mass >= tuning.HesitateAboveMass) margin += tuning.HesitationSeconds;          // a bus: hesitate
                else if (v.Mass <= tuning.StepOutBelowMass) margin *= tuning.HandConfidenceGapFactor;  // a car: hand up

                if (secondsAway < secondsToClear + margin) return Verdict.Stop;
            }
            return verdict;
        }

        private static bool Inside(Agent p, Agent v)
        {
            return Mathf.Abs(p.Lateral - v.Lateral) < v.HalfWidth + p.HalfWidth + SideMargin;
        }

        /// <summary>Is the vehicle's strip still to come in the pedestrian's direction of travel?</summary>
        private static bool StripIsAhead(Agent p, Agent v, float direction)
        {
            float nearSide = v.Lateral - direction * (v.HalfWidth + p.HalfWidth + SideMargin);
            return direction > 0f ? nearSide > p.Lateral : nearSide < p.Lateral;
        }
    }
}
