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
        public static float MaxDwellSeconds = 40f;     // long enough to take an ordinary crowd whole
        public static int DhakaMinCrowd = 1;           // the Dhaka driver stops for anyone waving (3 was tried: no gain, the bus stops anyway for people getting off)
        public static bool DhakaSkipsTakenStops = false;   // pass a stop where a crew bus is already loading (tried twice: loses a full kerb for the next one, which the rival behind then takes)
        public static bool DhakaRaceLeave = false;         // leave a nearly bare kerb when a crew bus is on the tail

        // ---- Act 1: the player is the helper, the ostad drives (RESEARCH: "Act 1 as helper is the tutorial").
        // The ostad only stops where the helper calls; the helper's hand says hurry or easy.
        public static bool HelperMode;
        public static bool HelperCalls;        // the door key: "stop here, stop here!"
        public static int HelperSignal;        // +1 hurry, −1 easy, 0 nothing
        public static float OstadCapKmh = 35f, OstadHurryKmh = 50f, OstadEasyKmh = 22f;
        public static float HelperDwellSeconds = 40f;

        private static DemandZone _working;      // the zone we are stopped at
        private static bool _dropOnly;           // a crew bus's door owns this kerb: let ours off and go, load nothing
        private static DemandZone _lastLeft;     // don't stop twice at the same zone while still in its reach
        private static float _dwell;
        private static float _stuckFor;          // seconds blocked at low speed
        private static float _standingFor;       // seconds at a standstill, whatever the reason
        private static float _wrongSideFor;      // seconds spent over the median this excursion
        private static float _lastTap;
        private static float _wantLateral = -2f;
        private static float _stopLateral = float.NaN;   // the kerb line we are pulling in to for a stop; NaN = not stopping
        public static float WantLateral => _wantLateral;   // for the headless runner's contact log

        public static void Reset()
        {
            _working = null; _lastLeft = null; _dropOnly = false; _dwell = 0f; _stuckFor = 0f; _standingFor = 0f; _wrongSideFor = 0f; _lastTap = -99f; _wantLateral = HoldLateral;
            _stopLateral = float.NaN;
            HelperCalls = false; HelperSignal = 0;
        }

        public static void Apply(TrafficSim sim)
        {
            // On its side, the only decision is the men with the ropes: both autopilots pay and wait.
            if (sim.Rollover.Pending) Rollover.Answer(sim, true);
            _standingFor = sim.Player.Speed < 0.5f ? _standingFor + 1f / 60f : 0f;
            if (Current == Policy.Dhaka) ApplyDhaka(sim);
            else ApplyCareful(sim);
        }

        // ---------------------------------------------------------------- careful

        private static void ApplyCareful(TrafficSim sim)
        {
            Agent bus = sim.Player;
            if (sim.Economy.Sergeant.Active) { sim.Economy.AnswerSergeant(true); return; }   // the careful driver pays
            float h = sim.Metrics.HeadwayAheadSeconds, g = sim.Metrics.GapAheadMetres, v = bus.Speed * 3.6f;
            float coast;
            bool tooClose = TooCloseBehind(sim, bus, 4f, out coast);
            bool closing = h < 2f || g < 12f || tooClose || PersonInTheWay(sim, bus);
            float stop = sim.StopDistanceAhead(bus, 60f);
            if (stop < 60f) closing = closing || Steering.AllowedSpeed(stop, 2f, 1f) < bus.Speed;   // respects the cane

            ApproachLateral(sim, bus, 1);
            _wantLateral = float.IsNaN(_stopLateral) ? HoldLateral : _stopLateral;
            if (WorkZone(sim, bus, MaxDwellSeconds, 1)) return;
            closing = closing || TooFastForZoneAhead(sim, bus, 1);

            sim.Bus.Throttle = closing || coast > 0f ? 0f : (v > CapKmh ? 0f : 1f);
            sim.Bus.Brake = closing ? 1f : 0f;
            sim.Bus.Steer = SteerToHold(bus, sim.Corridor, AimLateral(sim, bus, _wantLateral));
        }

        // ---------------------------------------------------------------- dhaka

        private static void ApplyDhaka(TrafficSim sim)
        {
            Agent bus = sim.Player;
            float dt = 1f / 60f;
            if (sim.Economy.Sergeant.Active) { sim.Economy.AnswerSergeant(true); return; }   // pay now, it's cheaper
            float h = sim.Metrics.HeadwayAheadSeconds, g = sim.Metrics.GapAheadMetres, v = bus.Speed * 3.6f;

            // Only the physical box stops this driver; the cane is a suggestion. Except on a drive day: the lineman
            // said so at the stand, sergeants are at every box with a target each, and a cane run costs a case
            // or Tk 450. That day everyone drives like the careful man (RESEARCH: special drives, 1,400-2,300
            // cases a day across the city).
            bool driveDay = sim.Economy.DriveDay;
            float stop = sim.StopDistanceAhead(bus, 60f, ignoreCane: !driveDay);
            bool boxBlocked = stop < 60f && Steering.AllowedSpeed(stop, 0.8f, 1f) < bus.Speed;

            float cap = 45f;
            // Pulled away with someone still on the step: the helper holds them, the bus holds walking pace
            // until they are in, then the door goes and the right foot goes down (above the door speed nobody
            // boards anyway; a fall at speed is the injury the street answers).
            if (!HelperMode && bus.Load.DoorOpen && _working == null)
            {
                // Under the injury speed with a margin, not at it: at the door speed itself a fall is an injury (Tk 2,000 and the crowd).
                float stepMs = Mathf.Min(sim.Tuning.Passengers.DoorSpeedMs, sim.Tuning.Passengers.InjurySpeedMs - 0.6f);
                if (bus.Load.AtDoor != null || bus.Load.Leaving != null) cap = Mathf.Min(cap, stepMs * 3.6f);
                else if (bus.Speed > sim.Tuning.Passengers.DoorSpeedMs * 0.8f) sim.SetDoor(false);
            }
            if (HelperMode)
            {
                cap = HelperSignal > 0 ? OstadHurryKmh : HelperSignal < 0 ? OstadEasyKmh : OstadCapKmh;
                // The ostad slows for a called stop and for nothing else.
                if (WorkZone(sim, bus, HelperDwellSeconds, HelperCalls ? 0 : int.MaxValue)) return;
            }
            else
            {
                // Grab and go, as the research says: the helper packs them in, the driver leaves the moment the
                // kerb is bare or a rival is on his tail. Not a fixed ten seconds that leaves money on the kerb.
                // A crowd is worth the stop; one person is not, the rival behind takes the big crowd ahead
                // while you are braking for a single fare. Anyone aboard who wants off still gets the stop.
                ApproachLateral(sim, bus, DhakaMinCrowd);
                if (WorkZone(sim, bus, MaxDwellSeconds, DhakaMinCrowd)) return;
            }

            // Tailgating as the research describes it: a hand's breadth, but a driver who knows what his brakes
            // will do today. Closer than that stopping distance is the brake; a bit more is the throttle off.
            float coast;
            bool tooClose = TooCloseBehind(sim, bus, 1.5f, out coast);
            bool closing = tooClose || boxBlocked || TooFastForZoneAhead(sim, bus, HelperMode ? (HelperCalls ? 0 : int.MaxValue) : DhakaMinCrowd) || PersonInTheWay(sim, bus);
            // Blocked, or stuck behind a bus: a bus ahead is the one that takes the next crowd, so it is the
            // thing to get past (RESEARCH: the race for the stop). Both start the look for a freer band.
            float gapBus;
            Agent busAhead = Steering.FindAhead(sim.Agents, bus, bus.Lateral, 40f, out gapBus);
            bool behindABus = busAhead != null && busAhead.Class == TwentyTons.Tuning.VehicleClass.Bus && busAhead.Speed < bus.Speed + 2f;
            bool blocked = (g < 15f && bus.Speed < 5f) || behindABus;
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
            if (_stuckFor > 4f && !driveDay && !sim.Metrics.WrongSideNow && sim.Oncoming != null && OncomingClear(sim, 70f) && MedianClear(sim, bus, 35f))
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
            if (!float.IsNaN(_stopLateral) && !sim.Metrics.WrongSideNow) _wantLateral = _stopLateral;   // pulling in for a crowd

            sim.Bus.Throttle = closing || coast > 0f ? 0f : (v > cap ? 0f : 1f);
            sim.Bus.Brake = closing ? 1f : 0f;
            // Hard, but not harder than the bus takes: the Dhaka driver corners at 0.6 of what tips it.
            sim.Bus.Steer = SteerToHold(bus, sim.Corridor, AimLateral(sim, bus, _wantLateral), SafeLock(sim, bus, sim.Tuning.Bus.RolloverLateralAccelMs2 * 0.6f));
        }

        /// <summary>Nobody standing on the median ahead: crossing it goes through the people waiting there otherwise.</summary>
        private static bool MedianClear(TrafficSim sim, Agent bus, float metres)
        {
            float half = sim.Corridor.HalfWidth;
            for (int i = 0; i < sim.Agents.Count; i++)
            {
                Agent p = sim.Agents[i];
                if (!p.IsPedestrian || p.Corridor != bus.Corridor || p.Lateral < half - 0.5f) continue;
                float ds = sim.Corridor.DeltaS(bus.S, p.S);
                if (ds > -bus.HalfLength && ds < metres) return false;
            }
            return true;
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
                if (ds >= 0f) continue;                        // on their road, "behind" the ghost is what we will meet
                // What a Dhaka driver actually looks for: nothing heavy within the look (a bus or truck
                // will not yield to a bus), nothing light coming within a few seconds (a rickshaw or car
                // will swerve, as the research says: the heavier vehicle takes the road), and nothing
                // standing right there. A standing queue 40 m off is room.
                bool heavy = a.Mass >= sim.Tuning.Mass.Bus * 0.9f;
                float reach = heavy ? metres : a.Speed > 1f ? Mathf.Min(metres, 35f) : Mathf.Min(metres, 20f);
                if (-ds < reach) return false;
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
            bool wantStop = zone != null && zone != _lastLeft && (zone.Waiting.Count >= minCrowd || (!HelperMode && AnyoneFor(bus, zone)))
                            && bus.Load.Count < Boarding.TooFullCount(sim);
            if (HelperMode && !HelperCalls) wantStop = false;         // nobody banged the side: drive on
            // The race, as the research has it: a bus already loading at this kerb owns the crowd (Boarding:
            // first door takes them). Queuing behind it earns nothing; the Dhaka driver goes past and takes
            // the next crowd first. Unless someone aboard wants off here.
            bool taken = DhakaSkipsTakenStops && Current == Policy.Dhaka && !HelperMode && wantStop && _working == null && AnotherBusLoadingAt(sim, bus, zone);
            if (taken && !AnyoneFor(bus, zone)) wantStop = false;        // nobody to let off: straight past
            if (_working != null && zone != _working) { _working = null; _dwell = 0f; _dropOnly = false; }
            if (wantStop && _working == null) { _working = zone; _dwell = 0f; _dropOnly = taken; }   // someone to let off: drop them and go
            if (_working == null) return false;

            // The dwell clock runs once the people getting off are done: that part is not a choice.
            bool stillAlighting = bus.Load.Leaving != null || AnyoneFor(bus, _working);
            if (!stillAlighting) _dwell += 1f / 60f;
            bool done = bus.Load.AtDoor == null && bus.Load.Leaving == null && (_working.Waiting.Count == 0 || bus.Load.Count >= Boarding.TooFullCount(sim)) && !AnyoneFor(bus, _working);
            if (_dropOnly) done = bus.Load.Leaving == null && bus.Load.AtDoor == null && !AnyoneFor(bus, _working);   // the crowd is the other door's
            if (HelperMode) done = !HelperCalls && bus.Load.AtDoor == null && bus.Load.Leaving == null;   // the helper decides when we go
            if (DhakaRaceLeave && Current == Policy.Dhaka && !HelperMode && !done)
            {
                // The race: with a crew bus closing from behind and the kerb nearly bare, take what is at the door
                // and go: the next crowd is worth more than the last two here. A full kerb is held, whoever honks.
                Agent chaser = sim.OwnBusBehind(out float behindM);
                if (chaser != null && behindM < 60f && _dwell > 4f && bus.Load.AtDoor == null && _working.Waiting.Count <= 2) done = true;
            }
            if (done || _dwell > maxDwell)
            {
                // The Dhaka driver pulls away with the door open and the helper on the pole: whoever is on the
                // step gets pulled in on the move (RESEARCH: picking up without stopping is common). The door
                // shuts itself at the jump speed. The careful driver shuts it before the wheels turn.
                if (!(Current == Policy.Dhaka && !HelperMode && bus.Load.AtDoor != null)) sim.SetDoor(false);
                _lastLeft = _working;
                _working = null;
                _dropOnly = false;
                _stopLateral = float.NaN;
                return false;
            }
            sim.Bus.Throttle = 0f;
            sim.Bus.Brake = 1f;
            // Stand where we are: the pulling in happened on the approach; a stopped bus does not sidle.
            sim.Bus.Steer = SteerToHold(bus, sim.Corridor, float.IsNaN(_stopLateral) ? bus.Lateral : _stopLateral);
            // The door: the careful driver opens it standing; the Dhaka driver's helper has it open as the bus
            // rolls in, and the first people are on the step before the wheels stop.
            float doorSpeed = Current == Policy.Dhaka ? sim.Tuning.Passengers.DoorSpeedMs : 0.5f;
            if (!HelperMode && bus.Speed < doorSpeed) sim.SetDoor(true);   // as helper, the door is the player's
            return true;
        }

        /// <summary>
        /// The mirror: moving across the road is done a band at a time, and only into a band with nobody
        /// alongside. The wanted line was checked once when chosen; by the time the bus gets there a rickshaw
        /// has drifted into the way, and angling on regardless is the sideswipe the contact log was full of
        /// (24 hard scrapes a day on seed 1, nearly all a rickshaw a few metres ahead and a lane over).
        /// Returns where to aim this frame: the wanted line, or the current one while the next band is taken.
        /// </summary>
        private static float AimLateral(TrafficSim sim, Agent bus, float want)
        {
            float remaining = want - bus.Lateral;
            if (Mathf.Abs(remaining) < 0.3f) return want;
            float next = bus.Lateral + Mathf.Sign(remaining) * Mathf.Min(1.25f, Mathf.Abs(remaining));
            return Steering.SideBlocked(sim.Agents, bus, next) ? bus.Lateral : want;
        }

        /// <summary>
        /// Pull in to the kerb for a crowd worth stopping for within 45 m, if that band is free: people
        /// board a bus at the kerb in a step, a bus in the second lane costs them a walk out and the bus
        /// the time (Boarding: WalkToBusSecondsPerMetre). Sets _stopLateral; NaN when nothing is coming.
        /// </summary>
        private static void ApproachLateral(TrafficSim sim, Agent bus, int minCrowd)
        {
            if (_working != null) return;                        // already standing: keep the line we stopped on
            DemandZone next = RivalAI.NextZone(sim, bus);
            float ds = next != null ? sim.Corridor.DeltaS(bus.S, next.S) : 0f;
            bool worth = next != null && next != _lastLeft && ds > -sim.Tuning.Passengers.ZoneHalfLengthMetres && ds < 45f
                         && (next.Waiting.Count >= minCrowd || AnyoneFor(bus, next)) && bus.Load.Count < Boarding.TooFullCount(sim);
            if (!worth) { _stopLateral = float.NaN; return; }
            float kerb = next.Side * (sim.Corridor.HalfWidth - bus.HalfWidth - 0.3f);
            // Something parked in the kerb band (a rickshaw, a bus already there): stop in our own lane and let them walk out.
            if (float.IsNaN(_stopLateral)) _stopLateral = Steering.SideBlocked(sim.Agents, bus, kerb) ? bus.Lateral : kerb;
        }

        /// <summary>
        /// Am I closer to the vehicle ahead than today's brakes can stop in, plus a margin? That is the
        /// pedal. <paramref name="coast"/> comes back positive a little further out: throttle off, no
        /// brake, so the driver does not pump the pedal (air, pads) for every rickshaw.
        /// </summary>
        private static bool TooCloseBehind(TrafficSim sim, Agent bus, float marginMetres, out float coast)
        {
            coast = 0f;
            // Whoever is ahead in the band I am in, or the one I am moving into: the nearer of the two.
            float gap, gapWant;
            Agent ahead = Steering.FindAhead(sim.Agents, bus, bus.Lateral, 80f, out gap);
            Agent aheadWant = Steering.FindAhead(sim.Agents, bus, _wantLateral, 80f, out gapWant);
            if (aheadWant != null && (ahead == null || gapWant < gap)) { ahead = aheadWant; gap = gapWant; }
            if (ahead == null) return false;
            float v = bus.Speed, vAhead = Mathf.Max(0f, ahead.Speed);
            float decel = sim.Tuning.Bus.BrakeDecelNewMs2 * (1f - sim.Tuning.Bus.BrakeWearLoss * sim.Bus.BrakeWear) * sim.Bus.AirPressure;
            float needed = v * sim.Tuning.Bus.BrakeLagSeconds + Mathf.Max(0f, v * v - vAhead * vAhead) / (2f * Mathf.Max(0.5f, decel)) + marginMetres;
            if (gap < needed) return true;
            if (vAhead < v && gap < needed * 1.6f) coast = needed * 1.6f - gap;
            return false;
        }

        /// <summary>
        /// The one hard rule, as a reflex: a person on the road ahead in my band, closer than I can
        /// stop for, means the brake, whatever else I am doing. Vehicles get the tailgating; people don't.
        /// </summary>
        private static bool PersonInTheWay(TrafficSim sim, Agent bus)
        {
            // What the brakes will actually do: worn pads, and only as much air as the tanks hold.
            float decel = sim.Tuning.Bus.BrakeDecelNewMs2 * (1f - sim.Tuning.Bus.BrakeWearLoss * sim.Bus.BrakeWear) * sim.Bus.AirPressure;
            for (int i = 0; i < sim.Agents.Count; i++)
            {
                Agent p = sim.Agents[i];
                if (!p.IsPedestrian || p.Corridor != bus.Corridor) continue;
                // Anyone within the sweep from where I am to where I am steering. People on the kerb or
                // the median count only when the bus itself is leaving the road on their side: crossing
                // the median to the wrong side goes through the people waiting on it.
                float lo = Mathf.Min(bus.Lateral, _wantLateral) - bus.HalfWidth - 1.2f;
                float hi = Mathf.Max(bus.Lateral, _wantLateral) + bus.HalfWidth + 1.2f;
                if (p.Lateral < lo || p.Lateral > hi) continue;
                float half = sim.Corridor.HalfWidth;
                if (p.Lateral > half + 0.3f && hi - 1.2f < half) continue;      // on the median, and I stay on the road
                if (p.Lateral < -half - 0.3f && lo + 1.2f > -half) continue;    // on the kerb, and I stay on the road
                float ds = sim.Corridor.DeltaS(bus.S, p.S) - bus.HalfLength;
                // Beside the body is not ahead: a person a step behind the nose is walking round a standing
                // bus (Pedestrians), and waiting for them locked the street for whole days. Steering across
                // someone beside the bus is AimLateral's business: it holds the line while the next band is
                // taken, people included.
                if (ds > 45f) continue;
                if (ds < -0.3f)
                {
                    // Beside the body and standing still, or driving straight: not in the way. Moving and steering
                    // across them (they are in the sweep but not in the band I am in): a sideswipe, so yes.
                    bool inMyBand = Mathf.Abs(p.Lateral - bus.Lateral) < bus.HalfWidth + 0.6f;
                    if (bus.Speed < 1f || inMyBand || Mathf.Abs(_wantLateral - bus.Lateral) < 0.3f) continue;
                }
                float canStopIn = bus.Speed * sim.Tuning.Bus.BrakeLagSeconds + bus.Speed * bus.Speed / (2f * Mathf.Max(0.5f, decel));
                // Standing, give someone standing in the road room before pulling away: they will cross in
                // front of a stopped bus, as everyone does, and the bus must not start into them. Someone
                // already walking across clears the band in a couple of seconds: less room, or at a crowd
                // crossing (every stand is one) the bus never gets its six clear metres and stands all day.
                float margin = bus.Speed < 1f ? (p.Speed < 0.5f ? 6f : 4f) : 3f;
                // Someone standing in the road ahead while we have stood a while: creep up to them. A bus that
                // moves is a bus they get out of the way of (Pedestrians: a moving strip is left, a standing
                // one is waited on); a bus that waits six metres for a person who waits for the bus is a day
                // gone. The Dhaka driver creeps after five seconds, the careful one after fifteen.
                if (p.Speed < 0.5f && bus.Speed < 1f && _standingFor > (Current == Policy.Dhaka ? 5f : 15f)) margin = 2f;
                if (ds < canStopIn + margin) return true;
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

        /// <summary>Is another bus working this zone with its door open and room aboard, so the crowd is its?</summary>
        private static bool AnotherBusLoadingAt(TrafficSim sim, Agent bus, DemandZone zone)
        {
            float reach = sim.Tuning.Passengers.ZoneHalfLengthMetres;
            for (int i = 0; i < sim.Agents.Count; i++)
            {
                Agent o = sim.Agents[i];
                if (o == bus || o.Corridor != bus.Corridor) continue;
                // A bus with a door open at this kerb: Boarding gives it everyone until it leaves (first door takes
                // the crowd), so standing behind it earns nothing for as long as it stays. Another company's bus
                // takes six and goes within fifteen seconds: not worth passing a full kerb for.
                if (o.Load == null || !o.Load.DoorOpen || o.Load.Count >= Boarding.TooFullCount(sim)) continue;
                if (Mathf.Abs(sim.Corridor.DeltaS(zone.S, o.S)) <= reach) return true;
            }
            return false;
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
            return SteerToHold(bus, corridor, wantedLateral, 1f);
        }

        /// <summary>
        /// Steer toward a line, never asking the wheel for more lock than the speed allows. A driver who
        /// has done this route for years does not tip the bus: at speed v the lock is capped so that
        /// v² · tan(lock) / wheelbase stays under a comfortable lateral acceleration.
        /// </summary>
        public static float SteerToHold(Agent bus, Corridor corridor, float wantedLateral, float maxSteer)
        {
            float lookAhead = 10f + bus.Speed * 1.5f;      // a driver looks further ahead than a bus length
            Vector3 aim = corridor.PositionAt(bus.S + lookAhead, wantedLateral);
            Vector3 to = aim - bus.Position;
            float wantedYaw = Mathf.Atan2(to.x, to.z);
            float error = Mathf.DeltaAngle(bus.Yaw * Mathf.Rad2Deg, wantedYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            return Mathf.Clamp(error * 3f, -maxSteer, maxSteer);
        }

        /// <summary>The share of full lock that keeps the lateral acceleration under the given limit at this speed.</summary>
        private static float SafeLock(TrafficSim sim, Agent bus, float lateralAccelLimit)
        {
            float wheelbase = sim.Tuning.Bus.WheelbaseMetres;
            float maxLockDeg = Mathf.Max(1f, sim.Tuning.Bus.MaxSteerAngleDeg);
            float v2 = Mathf.Max(1f, bus.Speed * bus.Speed);
            float lockRad = (float)System.Math.Atan(lateralAccelLimit * wheelbase / v2);
            return Mathf.Clamp01(lockRad * Mathf.Rad2Deg / maxLockDeg);
        }
    }
}
