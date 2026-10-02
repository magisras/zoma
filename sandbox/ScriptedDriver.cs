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

        public static float MaxDwellSeconds = 25f;

        private static DemandZone _working;      // the zone we are stopped at
        private static DemandZone _lastLeft;     // don't stop twice at the same zone while still in its reach
        private static float _dwell;

        public static void Apply(TrafficSim sim)
        {
            Agent bus = sim.Player;
            if (sim.Economy.Sergeant.Active) { sim.Economy.AnswerSergeant(true); return; }   // the careful driver pays
            float h = sim.Metrics.HeadwayAheadSeconds, g = sim.Metrics.GapAheadMetres, v = bus.Speed * 3.6f;
            bool closing = h < 2f || g < 12f;

            // Work a zone: stop if people are waiting (or someone wants off), open the door, leave
            // when the step is clear and nobody is left, or after a maximum dwell.
            DemandZone zone = Boarding.ZoneInReach(sim, bus);
            if (zone == null) _lastLeft = null;
            bool wantStop = zone != null && zone != _lastLeft && (zone.Waiting.Count > 0 || AnyoneFor(bus, zone))
                            && bus.Load.Count < Boarding.TooFullCount(sim);
            if (_working != null && zone != _working) { _working = null; _dwell = 0f; }
            if (wantStop && _working == null) { _working = zone; _dwell = 0f; }
            if (_working != null)
            {
                _dwell += 1f / 60f;
                bool done = bus.Load.AtDoor == null && (_working.Waiting.Count == 0 || bus.Load.Count >= Boarding.TooFullCount(sim)) && !AnyoneFor(bus, _working);
                if (done || _dwell > MaxDwellSeconds)
                {
                    sim.SetDoor(false);
                    _lastLeft = _working;
                    _working = null;
                }
                else
                {
                    sim.Bus.Throttle = 0f;
                    sim.Bus.Brake = 1f;
                    sim.Bus.Steer = SteerToHold(bus, sim.Corridor, HoldLateral);
                    if (bus.Speed < 0.5f) sim.SetDoor(true);
                    return;
                }
            }

            sim.Bus.Throttle = closing ? 0f : (v > CapKmh ? 0f : 1f);
            sim.Bus.Brake = closing ? 1f : 0f;
            sim.Bus.Steer = SteerToHold(bus, sim.Corridor, HoldLateral);
        }

        private static bool AnyoneFor(Agent bus, DemandZone zone)
        {
            for (int i = 0; i < bus.Load.Aboard.Count; i++) if (bus.Load.Aboard[i].DestinationZone == zone.Index) return true;
            return false;
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
