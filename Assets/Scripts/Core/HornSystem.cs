using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>
    /// The horn is language (RESEARCH.md): "broadcast in a cone with strength; raises yield
    /// probability of agents inside, weighted by relative mass; silence moves nothing."
    ///
    /// A horn is an event, not a sound: everyone in the cone rolls once against the boost and the
    /// ones who lose the roll move aside. A rickshaw honking at a bus achieves almost nothing; a bus
    /// honking at a rickshaw almost always works. Pedestrians about to step out pause instead.
    /// </summary>
    public static class HornSystem
    {
        /// <summary>Broadcast one horn from <paramref name="source"/>. Returns how many agents gave way.</summary>
        public static int Broadcast(TrafficSim sim, Agent source, float strength)
        {
            var horn = sim.Tuning.Horn;
            source.HornTimer = 0.3f;

            Vector3 forward = new Vector3(Mathf.Sin(source.Yaw), 0f, Mathf.Cos(source.Yaw));
            float cosHalfAngle = Mathf.Cos(horn.ConeHalfAngleDegrees * Mathf.Deg2Rad);
            int moved = 0;

            for (int i = 0; i < sim.Agents.Count; i++)
            {
                Agent listener = sim.Agents[i];
                if (listener == source || listener.GhostOf != null) continue;

                Vector3 toListener = listener.Position - source.Position;
                float distance = toListener.magnitude;
                if (distance > horn.RangeMetres || distance < 0.01f) continue;
                if (Vector3.Dot(toListener / distance, forward) < cosHalfAngle) continue;   // outside the cone

                if (listener.IsPedestrian)
                {
                    // A blast makes someone on the kerb wait a moment longer. Nobody honks a person off the road.
                    if (listener.PedState == PedestrianState.Waiting)
                    {
                        listener.WaitTimer = Mathf.Max(listener.WaitTimer, 1.5f);
                    }
                    else
                    {
                        listener.AlarmTimer = sim.Tuning.Pedestrians.HornAlarmSeconds;   // in the road: run for the nearer edge
                    }
                    continue;
                }

                if (listener.IsYielding) continue;
                float boost = horn.YieldBoost(strength, source.Mass, listener.Mass);
                if (sim.Random.Chance(boost))
                {
                    Steering.Yield(listener, source, horn.YieldSeconds, sim.Corridor);
                    moved++;
                }
            }
            return moved;
        }
    }
}
