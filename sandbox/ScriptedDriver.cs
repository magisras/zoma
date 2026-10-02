using TwentyTons.Core;
using UnityEngine;

namespace TwentyTons.Sandbox
{
    /// <summary>Which scripted hands are on the wheel.</summary>
    public enum Policy { None, Careful, Dhaka }

    /// <summary>
    /// Scripted drivers for the player's bus, used by the headless runner for repeatable metrics and
    /// by the browser sandbox as autopilots (P cycles them). Two policies:
    ///   Careful: holds a line, brakes at a safe headway, keeps under 25 km/h, pays everyone, waits.
    ///            The "drive properly" baseline the research says should lose.
    ///   Dhaka:   0.6 s headway, horn as language, cuts into free bands, runs the cane when the box
    ///            is clear, takes the wrong side when stuck, grabs crowds and goes.
    /// Both are deliberately simple; they are instruments, not opponents.
    /// </summary>
    public static class ScriptedDriver
    {
        public static Policy Current = Policy.Careful;
        public static float CapKmh = 25f;
        public static float HoldLateral = -2f;
        public static float MaxDwellSeconds = 25f;

        private static DemandZone _working;      // the zone we are stopped at
        private static DemandZone _lastLeft;     // don't stop twice at the same zone while still in its reach
        private static float _dwell;
        private static float _stuckFor;          // seconds blocked at low speed
        private static float _wrongSideFor;      // seconds spent over the median this excursion
        private static float _lastTap;
        private static float _wantLateral = -2f;

        public static void Reset()
        {
            _working = null; _lastLeft = null; _dwell = 0f; _stuckFor = 0f; _wrongSideFor = 0f; _lastTap = -99f; _wantLateral = HoldLateral;
        }

        public static void Apply(TrafficSim sim)
        {
            if (Current == Policy.Dhaka) ApplyDhaka(sim);
            else ApplyCareful(sim);
        }

        // ---------------------------------------------------------------- careful

        private static void ApplyCareful(TrafficSim sim)
        {
            Agent bus = sim.Player;
            if (sim.Economy.Sergeant.Active) { sim.Economy.AnswerSergeant(true); return; }   // the careful driver pays
            float h = sim.Metrics.HeadwayAheadSeconds, g = sim.Metrics.GapAheadMetres, v = bus.Speed * 3.6f;
            bool closing = h < 2f || g < 12f || PersonInTheWay(sim, bus);
            float stop = sim.StopDistanceAhead(bus, 60f);
            if (stop < 60f) closing = closing || Steering.AllowedSpeed(stop, 2f, 1f) < bus.Speed;   // respects the cane

            if (WorkZone(sim, bus, 25f, 1)) return;
            closing = closing || TooFastForZoneAhead(sim, bus, 1);

            sim.Bus.Throttle = closing ? 0f : (v > CapKmh ? 0f : 1f);
            sim.Bus.Brake = closing ? 1f : 0f;
            sim.Bus.Steer = SteerToHold(bus, sim.Corridor, HoldLateral);
        }

        // ---------------------------------------------------------------- dhaka

        private static void ApplyDhaka(TrafficSim sim)
        {
            Agent bus = sim.Player;
            float dt = 1f / 60f;
            if (sim.Economy.Sergeant.Active) { sim.Economy.AnswerSergeant(true); return; }   // pay now, it's cheaper
            float h = sim.Metrics.HeadwayAheadSeconds, g = sim.Metrics.GapAheadMetres, v = bus.Speed * 3.6f;

            // Only the physical box stops this driver; the cane is a suggestion.
            float stop = sim.StopDistanceAhead(bus, 60f, ignoreCane: true);
            bool boxBlocked = stop < 60f && Steering.AllowedSpeed(stop, 0.8f, 1f) < bus.Speed;

            if (WorkZone(sim, bus, 10f, 3)) return;   // grab and go

            bool closing = h < 0.6f || g < 4f || boxBlocked || TooFastForZoneAhead(sim, bus, 3) || PersonInTheWay(sim, bus);
            bool blocked = g < 15f && bus.Speed < 5f;
            _stuckFor = blocked ? _stuckFor + dt : 0f;

            // The horn: tap whenever something is close ahead, blast when stuck.
            if (g < 25f && h < 2f && sim.Metrics.Time - _lastTap > 2f) { _lastTap = sim.Metrics.Time; Horn(sim, true); }
            else Horn(sim, _stuckFor > 2f);

            // Cut in: when slowed, take the band with the most free road.
            if (blocked && !sim.Metrics.WrongSideNow)
            {
                float best = _wantLateral, bestFree = g;
                float edge = sim.Corridor.HalfWidth - bus.HalfWidth;
                for (float lat = -edge; lat <= edge + 0.01f; lat += 1.25f)
                {
                    if (Steering.SideBlocked(sim.Agents, bus, lat)) continue;
                    Steering.FindAhead(sim.Agents, bus, lat, 60f, out float free);
                    if (free > bestFree + 5f) { bestFree = free; best = lat; }
                }
                _wantLateral = best;
            }

            // The wrong side: stuck for a while, nothing coming, go round on their road.
            if (_stuckFor > 4f && !sim.Metrics.WrongSideNow && sim.Oncoming != null && OncomingClear(sim, 70f))
            {
                _wantLateral = sim.Corridor.HalfWidth + sim.Tuning.Spawn.MedianMetres + 2.5f;   // just inside their road
                _wrongSideFor = 0f;
            }
            if (sim.Metrics.WrongSideNow)
            {
                _wrongSideFor += dt;
                // Come back once the road at home is clear, or when something heavy is coming, or after long enough.
                Steering.FindAhead(sim.Agents, bus, HoldLateral, 40f, out float homeFree);
                bool heavyComing = Steering.FindHeavierBehind(sim.Agents, sim.PlayerGhost, 60f) != null;
                if (homeFree > 30f || heavyComing || _wrongSideFor > 12f) _wantLateral = HoldLateral;
                closing = closing || (heavyComing && g < 20f);
            }
            else if (!blocked && Mathf.Abs(_wantLateral - HoldLateral) > 0.1f && g > 30f)
            {
                _wantLateral = HoldLateral;   // drift back to the usual line when the road is open
            }

            sim.Bus.Throttle = closing ? 0f : (v > 45f ? 0f : 1f);
            sim.Bus.Brake = closing ? 1f : 0f;
            sim.Bus.Steer = SteerToHold(bus, sim.Corridor, _wantLateral);
        }

        private static bool OncomingClear(TrafficSim sim, float metres)
        {
            Agent g = sim.PlayerGhost;
            if (g == null) return false;
            for (int i = 0; i < sim.Agents.Count; i++)
            {
                Agent a = sim.Agents[i];
                if (a.Corridor != sim.Oncoming || a.GhostOf != null) continue;
                float ds = sim.Oncoming.DeltaS(g.S, a.S);
                if (ds < 0f && -ds < metres) return false;     // on their road, "behind" the ghost is what we will meet
            }
            return true;
        }

        private static void Horn(TrafficSim sim, bool held)
        {
            sim.HornInput(held, 1f / 60f);
        }

        // ---------------------------------------------------------------- shared

        /// <summary>
        /// Work a zone: stop if people are waiting (or someone wants off), open the door, leave when
        /// the step is clear and nobody is left, or after the dwell. Returns true while stopping.
        /// </summary>
        private static bool WorkZone(TrafficSim sim, Agent bus, float maxDwell, int minCrowd)
        {
            DemandZone zone = Boarding.ZoneInReach(sim, bus);
            if (zone == null) _lastLeft = null;
            bool wantStop = zone != null && zone != _lastLeft && (zone.Waiting.Count >= minCrowd || AnyoneFor(bus, zone))
                            && bus.Load.Count < Boarding.TooFullCount(sim);
            if (_working != null && zone != _working) { _working = null; _dwell = 0f; }
            if (wantStop && _working == null) { _working = zone; _dwell = 0f; }
            if (_working == null) return false;

            // The dwell clock runs once the people getting off are done: that part is not a choice.
            bool stillAlighting = bus.Load.Leaving != null || AnyoneFor(bus, _working);
            if (!stillAlighting) _dwell += 1f / 60f;
            bool done = bus.Load.AtDoor == null && bus.Load.Leaving == null && (_working.Waiting.Count == 0 || bus.Load.Count >= Boarding.TooFullCount(sim)) && !AnyoneFor(bus, _working);
            if (done || _dwell > maxDwell)
            {
                sim.SetDoor(false);
                _lastLeft = _working;
                _working = null;
                return false;
            }
            sim.Bus.Throttle = 0f;
            sim.Bus.Brake = 1f;
            sim.Bus.Steer = SteerToHold(bus, sim.Corridor, HoldLateral);
            if (bus.Speed < 0.5f) sim.SetDoor(true);
            return true;
        }

        /// <summary>
        /// The one hard rule, as a reflex: a person on the road ahead in my band, closer than I can
        /// stop for, means the brake, whatever else I am doing. Vehicles get the tailgating; people don't.
        /// </summary>
        private static bool PersonInTheWay(TrafficSim sim, Agent bus)
        {
            float decel = sim.Tuning.Bus.BrakeDecelNewMs2 * (1f - sim.Tuning.Bus.BrakeWearLoss * sim.Bus.BrakeWear);
            for (int i = 0; i < sim.Agents.Count; i++)
            {
                Agent p = sim.Agents[i];
                if (!p.IsPedestrian || p.Corridor != bus.Corridor) continue;
                // Anyone on the carriageway (not on the kerb) within the sweep from where I am to where I am steering.
                if (Mathf.Abs(p.Lateral) > sim.Corridor.HalfWidth + 0.3f) continue;
                float lo = Mathf.Min(bus.Lateral, _wantLateral) - bus.HalfWidth - 1.2f;
                float hi = Mathf.Max(bus.Lateral, _wantLateral) + bus.HalfWidth + 1.2f;
                if (p.Lateral < lo || p.Lateral > hi) continue;
                float ds = sim.Corridor.DeltaS(bus.S, p.S) - bus.HalfLength;
                if (ds < -1f || ds > 45f) continue;
                float canStopIn = bus.Speed * bus.Speed / (2f * Mathf.Max(0.5f, decel));
                if (ds < canStopIn + 3f) return true;
            }
            return false;
        }

        /// <summary>
        /// A crowd worth stopping for is coming up: am I going too fast to stop inside its reach?
        /// Brake early enough to arrive at the zone's centre at a comfortable 2.5 m/s².
        /// </summary>
        private static bool TooFastForZoneAhead(TrafficSim sim, Agent bus, int minCrowd)
        {
            DemandZone next = RivalAI.NextZone(sim, bus);
            if (next == null || next == _lastLeft) return false;
            float ds = sim.Corridor.DeltaS(bus.S, next.S);
            if (ds < 0f || ds > 70f) return false;
            bool worth = next.Waiting.Count >= minCrowd || AnyoneFor(bus, next);
            if (!worth || bus.Load.Count >= Boarding.TooFullCount(sim)) return false;
            float allowed = Mathf.Sqrt(2f * 2.5f * Mathf.Max(0f, ds - 4f));
            return bus.Speed > allowed;
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
