using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>
    /// People crossing the road. RESEARCH.md: pedestrians "cross on estimated gap; 'hand' confidence
    /// makes them step out in front of small vehicles and hesitate for buses." And the street's one
    /// hard rule: don't hit a person. TrafficSim ends the day when the player does.
    ///
    /// A pedestrian waits on one kerb, estimates the time until each approaching vehicle reaches
    /// them, and steps out when every vehicle is further away (in seconds) than they demand of it.
    /// Small vehicle: they demand less and raise a hand. Bus or truck: they demand more.
    /// </summary>
    public static class Pedestrians
    {
        public static void Step(TrafficSim sim, Agent p, float dt)
        {
            PedestrianSettings tuning = sim.Tuning.Pedestrians;
            Corridor corridor = sim.Corridor;
            float kerb = corridor.HalfWidth + sim.Tuning.Spawn.KerbOffsetMetres;

            if (p.PedState == PedestrianState.Waiting)
            {
                p.WaitTimer -= dt;
                if (p.WaitTimer <= 0f && GapLooksSafe(sim, p, tuning))
                {
                    p.PedState = PedestrianState.Crossing;
                    p.CrossDirection = p.Lateral > 0f ? -1f : 1f;   // head for the other kerb
                    p.Speed = tuning.WalkSpeed;
                }
            }
            else
            {
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

        /// <summary>
        /// Every vehicle approaching the crossing point must be more than the demanded seconds away.
        /// A vehicle already alongside the crossing point means no.
        /// </summary>
        private static bool GapLooksSafe(TrafficSim sim, Agent p, PedestrianSettings tuning)
        {
            for (int i = 0; i < sim.Agents.Count; i++)
            {
                Agent v = sim.Agents[i];
                if (v.IsPedestrian) continue;

                // Alongside or just past the crossing point: wait for it to clear.
                if (v.Front > p.S - 1f && v.Rear < p.S + 1f) return false;
                if (v.Front >= p.S) continue;                          // already past

                float distance = p.S - v.Front;
                if (distance > tuning.LookMetres) continue;
                if (v.Speed < 0.3f) continue;                          // parked or stuck: walk round it

                float demanded = tuning.CrossingGapSeconds;
                if (v.Mass >= tuning.HesitateAboveMass) demanded += tuning.HesitationSeconds;        // a bus: hesitate
                else if (v.Mass <= tuning.StepOutBelowMass) demanded *= tuning.HandConfidenceGapFactor; // a car: hand up

                float secondsAway = distance / v.Speed;
                if (secondsAway < demanded) return false;
            }
            return true;
        }
    }
}
