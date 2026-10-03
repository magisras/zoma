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
                float side = -direction;

                // People at the kerb step back from a vehicle about to overhang it (a bus swinging wide on
                // a bend) and come back when it has gone. On the median there is less room to step to.
                if (OverhangComing(sim, p, side, kerb, tuning)) p.FlinchTimer = tuning.FlinchSeconds;
                else p.FlinchTimer -= dt;
                float stand = kerb + (p.FlinchTimer > 0f ? tuning.FlinchBackMetres : 0f);
                if (side > 0f && sim.Oncoming != null) stand = Mathf.Min(stand, corridor.HalfWidth + sim.Tuning.Spawn.MedianMetres - 0.3f);
                p.Lateral = Mathf.MoveTowards(p.Lateral, stand * side, 2f * dt);   // a step, not a jump

                if (p.WaitTimer <= 0f && p.FlinchTimer <= 0f && Decide(sim, p, direction, tuning, false) == Verdict.Walk)
                {
                    p.PedState = PedestrianState.Crossing;
                    p.CrossDirection = direction;
                    p.MidRoadSeconds = 0f;
                    p.CrossingSeconds = 0f;
                }
            }
            else
            {
                // Standing in the road waiting for a gap wears thin: after a while the hand goes up and they step
                // out in front of the light traffic, which brakes (Steering: a person ahead in the band).
                bool impatient = p.MidRoadSeconds > tuning.MidRoadPatienceSeconds;
                Verdict verdict = Decide(sim, p, p.CrossDirection, tuning, impatient);
                // Caught in a moving vehicle's strip nearer the edge I came from: the way out is back, not
                // across its bonnet. Turn round; the kerb and another try are what is left of this crossing.
                if (verdict == Verdict.HurryBack) { p.CrossDirection = -p.CrossDirection; verdict = Verdict.Hurry; }
                p.Speed = verdict == Verdict.Stop ? 0f : verdict == Verdict.Hurry ? tuning.WalkSpeed * 1.6f : tuning.WalkSpeed;
                if (verdict == Verdict.Stop && Mathf.Abs(p.Lateral) < corridor.HalfWidth) p.MidRoadSeconds += dt;
                else if (verdict != Verdict.Stop) p.MidRoadSeconds = 0f;
                // Taking too long: give up, go back to the kerb, try again later. Without a backstop one
                // person standing in a lane holds a bus, and the bus holds the street, for the rest of the day.
                p.CrossingSeconds += dt;
                if (p.CrossingSeconds > tuning.CrossingGiveUpSeconds) { p.CrossDirection = -p.CrossDirection; p.CrossingSeconds = 0f; p.MidRoadSeconds = 0f; }
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

        private enum Verdict { Walk, Stop, Hurry, HurryBack }

        /// <summary>
        /// Is a moving vehicle beside or coming at this kerb with its side reaching the kerb line? Where it
        /// will be across the road when it arrives counts, not only where it is now.
        /// </summary>
        private static bool OverhangComing(TrafficSim sim, Agent p, float side, float kerb, PedestrianSettings tuning)
        {
            for (int i = 0; i < sim.Agents.Count; i++)
            {
                Agent v = sim.Agents[i];
                if (v.IsPedestrian || v.Corridor != p.Corridor || v.Speed < 0.5f) continue;
                float ds = p.Corridor.DeltaS(p.S, v.S);                 // negative = still coming
                if (ds > v.HalfLength + 1f || ds < -25f) continue;
                float arrive = Mathf.Clamp(-ds / v.Speed, 0f, 3f);
                float lateralThen = v.Lateral + v.LateralVelocity * arrive;
                float reach = side * lateralThen + v.HalfWidth;          // its side nearest my kerb, as a distance from the centreline
                if (reach > kerb - tuning.FlinchReachMetres) return true;
            }
            return false;
        }

        /// <summary>
        /// Look at every approaching vehicle. Each one owns a strip of road (its lateral band). For the
        /// strips still ahead of the pedestrian: will they be past that strip's far side before the
        /// vehicle arrives, with the margin they demand of that kind of vehicle? For the strip they are
        /// standing in: hurry. Anything else: walk.
        /// </summary>
        private static Verdict Decide(TrafficSim sim, Agent p, float direction, PedestrianSettings tuning, bool impatient)
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
                    // Standing, it is waiting for me or parked: at its nose or tail I go round it, the way
                    // everyone does (a step behind the nose counts as the nose: a person there and the bus
                    // waited for each other all day). Its body is still a body: nobody walks through a bus.
                    if (v.Speed < 0.3f)
                    {
                        bool body = Mathf.Abs(ds) < v.HalfLength - 0.3f;
                        if (!body) continue;
                        if (Inside(p, v)) { verdict = Verdict.Hurry; continue; }   // overlapping a standing body: out of it
                        if (StripIsAhead(p, v, direction)) return Verdict.Stop;      // its body is across my path: wait
                        continue;
                    }
                    // Already in its strip with its nose on me: freezing is death, so get out, fast, by the
                    // nearer edge of the strip: on, or back the way I came (seed 6 at half crowd: a man off the
                    // median hurried on into the flank of the bus he could have stepped back from).
                    if (Inside(p, v)) { verdict = NearerEdgeIsBehind(p, v, direction) ? Verdict.HurryBack : Verdict.Hurry; continue; }
                    if (StripIsAhead(p, v, direction)) return Verdict.Stop;
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
                    // Right in front of me and not moving: it has stopped for me. Go, as everyone does.
                    if (distance <= tuning.StoppedForMeMetres) continue;
                    // Boxed in behind something else that stands: it is going nowhere. In a jam people
                    // weave between the vehicles; without this they would wait for the jam to end.
                    if (v.HeldAhead) continue;
                    speed = tuning.PullAwayAssumedSpeed;
                }

                float secondsAway = distance / speed;

                if (Inside(p, v))
                {
                    // Already in its strip: get out of it, now, by the nearer edge.
                    if (secondsAway < 4f) verdict = NearerEdgeIsBehind(p, v, direction) ? Verdict.HurryBack : Verdict.Hurry;
                    continue;
                }
                if (!StripIsAhead(p, v, direction)) continue;             // its strip is behind me

                float farSide = v.Lateral + direction * (v.HalfWidth + SideMargin);
                float secondsToClear = Mathf.Abs(farSide - p.Lateral) / tuning.WalkSpeed;

                float margin = tuning.CrossingGapSeconds;
                if (v.Mass >= tuning.HesitateAboveMass) margin += tuning.HesitationSeconds;          // a bus: hesitate
                else if (v.Mass <= tuning.StepOutBelowMass) margin *= tuning.HandConfidenceGapFactor;  // a car: hand up
                // Pinned mid-road long enough: anything lighter than a bus and not fast gets the hand and has to
                // brake; only the time to get out of its way is demanded, no margin.
                if (impatient && v.Mass < tuning.HesitateAboveMass && speed < 6f) { margin = 0f; secondsToClear *= 0.5f; }

                if (secondsAway < secondsToClear + margin) return Verdict.Stop;
            }
            return verdict;
        }

        /// <summary>Inside a vehicle's strip: is the edge behind me (the way I came) the nearer way out?</summary>
        private static bool NearerEdgeIsBehind(Agent p, Agent v, float direction)
        {
            float half = v.HalfWidth + p.HalfWidth + SideMargin;
            float toFar = direction > 0f ? (v.Lateral + half) - p.Lateral : p.Lateral - (v.Lateral - half);
            return toFar > half;   // past the middle is nearer the far edge; short of it, nearer the one behind
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
