using TwentyTons.Core;
using UnityEngine;

namespace TwentyTons.Sandbox
{
    /// <summary>
    /// A careful scripted driver for the player's bus: holds a lateral line, brakes at a safe
    /// headway, keeps under a speed cap. Used by the headless runner for repeatable metrics and by
    /// the browser sandbox as an autopilot (key P), so the same policy is tested both ways. It is
    /// deliberately *not* a Dhaka driver: it is the "drive properly" baseline the research says
    /// should lose.
    /// </summary>
    public static class ScriptedDriver
    {
        public static float CapKmh = 25f;
        public static float HoldLateral = -2f;

        public static void Apply(TrafficSim sim)
        {
            Agent bus = sim.Player;
            float h = sim.Metrics.HeadwayAheadSeconds, g = sim.Metrics.GapAheadMetres, v = bus.Speed * 3.6f;
            bool closing = h < 2f || g < 12f;
            sim.Bus.Throttle = closing ? 0f : (v > CapKmh ? 0f : 1f);
            sim.Bus.Brake = closing ? 1f : 0f;
            sim.Bus.Steer = SteerToHold(bus, sim.Corridor, HoldLateral);
        }

        /// <summary>
        /// Hold a lateral offset: aim the nose at a point ahead on the wanted line, like a driver does.
        /// Returns a steering input in −1..1.
        /// </summary>
        public static float SteerToHold(Agent bus, Corridor corridor, float wantedLateral)
        {
            float lookAhead = 8f + bus.Speed * 1.0f;
            Vector3 aim = corridor.PositionAt(bus.S + lookAhead, wantedLateral);
            Vector3 to = aim - bus.Position;
            float wantedYaw = Mathf.Atan2(to.x, to.z);
            float error = Mathf.DeltaAngle(bus.Yaw * Mathf.Rad2Deg, wantedYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            return Mathf.Clamp(error * 3f, -1f, 1f);
        }
    }
}
