using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>
    /// Per-character memory and state for a rival bus crew (RESEARCH.md, "Memory and relationships").
    /// Variables: grudge toward the player, fatigue, money today. Events move them; they feed the
    /// utility weights and, later, which lines the crew says at the terminal.
    /// </summary>
    public sealed class RivalBrain
    {
        public string CrewName;
        public DriverPersonality Personality;
        public bool OwnCompany;          // same deposit, same passengers: competes hardest

        // Memory.
        public int Grudge;
        public float Fatigue;            // 0..1
        public float MoneyToday;

        // What the utility layer decided, and the state of carrying it out.
        public BusAction Action = BusAction.RaceForNextStop;
        public float DecisionTimer;
        public float BaseNerve;
        public float BaseCruise;
        public DemandZone TargetZone;    // the zone we are heading for / working
        public bool Stopping;
        public float Dwell;
        public DemandZone LastLeft;

        // Scores from the last decision, kept for the HUD and for tests.
        public readonly float[] Scores = new float[5];

        // Watching the player.
        public float BlockedByPlayerFor;
        public float PlayerLateralWhenBlocked;
        public bool PlayerWasInBand;
        public float GrudgeCooldown;
    }

    /// <summary>
    /// The decision layer for rival buses: every second, score five actions and do the highest
    /// (RESEARCH.md, "Decision: utility AI for buses"). Steering still moves the bus; this layer
    /// only sets what it wants: desired speed, nerve, where to be across the road, whether to stop.
    /// A bug here never breaks steering; a bug in steering never breaks this.
    /// </summary>
    public static class RivalAI
    {
        public static void Step(TrafficSim sim, Agent bus, float dt)
        {
            RivalBrain brain = bus.Brain;
            UtilitySettings u = sim.Tuning.Utility;

            WatchThePlayer(sim, bus, dt);
            brain.Fatigue = Mathf.Clamp01(brain.Fatigue + sim.Tuning.Fatigue.RisePerShiftHour * sim.Tuning.Economy.ShiftHours / Mathf.Max(1f, sim.Tuning.Economy.DayLengthSeconds) * dt);

            brain.DecisionTimer -= dt;
            if (brain.DecisionTimer <= 0f)
            {
                brain.DecisionTimer = u.DecisionIntervalSeconds;
                Decide(sim, bus);
            }
            Act(sim, bus, dt);
        }

        // ---------------------------------------------------------------- scoring

        /// <summary>Score each action from the situation, multiply by personality, pick the best.</summary>
        public static void Decide(TrafficSim sim, Agent bus)
        {
            RivalBrain brain = bus.Brain;
            UtilitySettings u = sim.Tuning.Utility;
            BusLoad load = bus.Load;
            int seats = sim.Tuning.Bus.Seats;
            DemandZone next = NextZone(sim, bus);
            int waiting = next != null ? next.Waiting.Count : 0;
            float emptySeats = Mathf.Max(0, seats - load.Count);
            Agent rivalBehind = NearestBusBehind(sim, bus, u.RivalCloseMetres);
            bool atZone = next != null && Mathf.Abs(sim.Corridor.DeltaS(next.S, bus.S)) <= sim.Tuning.Passengers.ZoneHalfLengthMetres;
            bool nearlyFull = load.Count >= seats * u.NearlyFullLoad;
            bool early = RouteFraction(sim, bus) < u.EarlyRouteFraction;

            // Race for next stop: big crowd ahead, worth more the emptier the bus; rival close behind.
            float race = waiting * u.RacePerWaitingPassenger * (1f + emptySeats / seats) + (rivalBehind != null ? u.RaceRivalCloseBonus : 0f);

            // Skip the stop: nearly full, small crowd.
            float skip = nearlyFull && waiting <= u.SmallCrowd ? u.SkipBonus : 0f;

            // Wait and fill: at a stop early in the route with no rival near: every empty seat says stay.
            float wait = (early && rivalBehind == null && !nearlyFull && atZone) ? u.WaitBonus + emptySeats * u.RacePerEmptySeat : 0f;

            // Block the player: player about to overtake, and a grudge to settle.
            float block = PlayerAboutToOvertake(sim, bus) ? u.BlockPerGrudgePoint * brain.Grudge : 0f;

            // Back off: fatigue high, dangerous gap ahead.
            Steering.FindAhead(sim.Agents, bus, bus.Lateral, 40f, out float gap);
            bool dangerous = gap < sim.Tuning.Gap.CriticalGapSeconds(bus.Nerve) * bus.Speed;
            float backOff = Mathf.Max(0f, brain.Fatigue - u.FatigueThreshold) * u.BackOffPerFatigue + (dangerous ? u.BackOffDangerousGapBonus : 0f);

            brain.Scores[(int)BusAction.RaceForNextStop] = race * brain.Personality.Race;
            brain.Scores[(int)BusAction.SkipTheStop] = skip * brain.Personality.Skip;
            brain.Scores[(int)BusAction.WaitAndFill] = wait * brain.Personality.Wait;
            brain.Scores[(int)BusAction.BlockThePlayer] = block * brain.Personality.Block;
            brain.Scores[(int)BusAction.BackOff] = backOff * brain.Personality.BackOff;

            BusAction best = BusAction.RaceForNextStop;
            for (int i = 1; i < brain.Scores.Length; i++)
            {
                if (brain.Scores[i] > brain.Scores[(int)best]) best = (BusAction)i;
            }
            brain.Action = best;
            brain.TargetZone = next;
        }

        // ---------------------------------------------------------------- acting

        /// <summary>Turn the chosen action into what steering reads: speed, nerve, lateral, stopping.</summary>
        private static void Act(TrafficSim sim, Agent bus, float dt)
        {
            RivalBrain brain = bus.Brain;
            UtilitySettings u = sim.Tuning.Utility;
            PassengerSettings p = sim.Tuning.Passengers;
            BusLoad load = bus.Load;
            bus.LateralOverride = float.NaN;

            switch (brain.Action)
            {
                case BusAction.RaceForNextStop:
                    bus.DesiredSpeed = brain.BaseCruise * u.RaceSpeedFactor;
                    bus.Nerve = Mathf.Clamp01(brain.BaseNerve + u.RaceNerveBoost);
                    break;
                case BusAction.SkipTheStop:
                    bus.DesiredSpeed = brain.BaseCruise * u.SkipSpeedFactor;
                    bus.Nerve = brain.BaseNerve;
                    break;
                case BusAction.WaitAndFill:
                    bus.DesiredSpeed = brain.BaseCruise;
                    bus.Nerve = brain.BaseNerve;
                    break;
                case BusAction.BlockThePlayer:
                    // Sit in the player's path: mirror their lateral, hold your speed.
                    bus.DesiredSpeed = brain.BaseCruise;
                    bus.Nerve = Mathf.Clamp01(brain.BaseNerve + u.RaceNerveBoost);
                    if (sim.Player != null) bus.LateralOverride = sim.Player.Lateral;
                    break;
                case BusAction.BackOff:
                    bus.DesiredSpeed = brain.BaseCruise * u.BackOffSpeedFactor;
                    bus.Nerve = Mathf.Clamp01(brain.BaseNerve - u.BackOffNerveDrop);
                    break;
            }

            // Working a zone: pull to the kerb, stop, open, leave when done.
            DemandZone zone = brain.TargetZone;
            bool wantsStop = zone != null && zone != brain.LastLeft && brain.Action != BusAction.SkipTheStop
                             && (zone.Waiting.Count > 0 || AnyoneFor(load, zone)) && load.Count < Boarding.TooFullCount(sim);
            float ds = zone != null ? sim.Corridor.DeltaS(bus.S, zone.S) : 999f;

            if (brain.Stopping)
            {
                brain.Dwell += dt;
                float maxDwell = brain.Action == BusAction.WaitAndFill ? u.WaitDwellSeconds : u.RaceDwellSeconds;
                bool done = load.AtDoor == null && load.Leaving == null && (zone.Waiting.Count == 0 || load.Count >= Boarding.TooFullCount(sim)) && !AnyoneFor(load, zone);
                if (brain.Action == BusAction.WaitAndFill) done = done && load.Count >= sim.Tuning.Bus.Seats * u.NearlyFullLoad;
                bool passed = sim.Corridor.DeltaS(zone.S, bus.S) > p.ZoneHalfLengthMetres;
                if (done || brain.Dwell > maxDwell || passed)
                {
                    load.DoorOpen = false;
                    brain.Stopping = false;
                    brain.LastLeft = zone;
                }
                else
                {
                    bus.DesiredSpeed = 0f;
                    bus.LateralOverride = -(sim.Corridor.HalfWidth - bus.HalfWidth);
                    if (bus.Speed < 0.5f && !load.DoorOpen)
                    {
                        load.DoorOpen = true;
                        load.DoorOpenedAt = sim.Metrics.Time;
                    }
                }
            }
            else if (wantsStop && ds > -p.ZoneHalfLengthMetres && ds < u.ApproachMetres)
            {
                // Approaching: drift to the kerb; once within reach, stop.
                bus.LateralOverride = -(sim.Corridor.HalfWidth - bus.HalfWidth);
                if (ds <= p.ZoneHalfLengthMetres * 0.5f)
                {
                    brain.Stopping = true;
                    brain.Dwell = 0f;
                    bus.DesiredSpeed = 0f;
                }
                else
                {
                    // Slow in gently, aiming to stop a few metres short of the zone's centre.
                    bus.DesiredSpeed = Mathf.Min(bus.DesiredSpeed, Mathf.Sqrt(2f * u.StopDecelMs2 * Mathf.Max(0f, ds - 5f)));
                }
            }
            if (zone != null && brain.LastLeft == zone && Mathf.Abs(ds) > p.ZoneHalfLengthMetres) brain.LastLeft = null;
        }

        // ---------------------------------------------------------------- memory

        /// <summary>
        /// Events that move the grudge (RESEARCH.md: "player cuts him off: grudge +1; lets him
        /// through: grudge −1"). Three things are watched:
        ///   cut-off:     the player's bus enters this bus's band right in front of it (+1, event);
        ///   held up:     the player is the vehicle this bus is stuck behind for a few seconds (+1);
        ///   let through: while held up, the player moves aside of their own accord (−1).
        /// Scrapes with the player are counted by TrafficSim.
        /// </summary>
        private static void WatchThePlayer(TrafficSim sim, Agent bus, float dt)
        {
            RivalBrain brain = bus.Brain;
            MemorySettings m = sim.Tuning.Memory;
            brain.GrudgeCooldown = Mathf.Max(0f, brain.GrudgeCooldown - dt);
            Agent player = sim.Player;
            if (player == null || player.Corridor != bus.Corridor) { brain.BlockedByPlayerFor = 0f; return; }

            float ds = sim.Corridor.DeltaS(bus.S, player.S);
            bool aheadClose = ds > 0f && ds < m.BlockRangeMetres;
            bool overlap = bus.OverlapsLaterally(player, Steering.LateralMargin);

            // Cut-off: overlap begins while they are just ahead and we are the faster one.
            if (aheadClose && overlap && !brain.PlayerWasInBand && bus.Speed > player.Speed + 0.5f
                && ds < sim.Tuning.Gap.CriticalGapSeconds(bus.Nerve) * bus.Speed + bus.HalfLength + player.HalfLength + 5f
                && brain.GrudgeCooldown <= 0f)
            {
                ChangeGrudge(brain, m.GrudgeWhenCutOff, m);
                brain.GrudgeCooldown = m.GrudgeCooldownSeconds;
            }
            brain.PlayerWasInBand = aheadClose && overlap;

            // Held up: the player is the vehicle in front of us and we want to go faster.
            Agent inFront = Steering.FindAhead(sim.Agents, bus, bus.Lateral, m.BlockRangeMetres, out float gap);
            bool heldUp = inFront == player && player.Speed < bus.DesiredSpeed - 1f;
            if (heldUp)
            {
                if (brain.BlockedByPlayerFor == 0f) brain.PlayerLateralWhenBlocked = player.Lateral;
                brain.BlockedByPlayerFor += dt;
                if (brain.BlockedByPlayerFor > m.BlockedSecondsForGrudge && brain.GrudgeCooldown <= 0f)
                {
                    ChangeGrudge(brain, m.GrudgeWhenCutOff, m);
                    brain.GrudgeCooldown = m.GrudgeCooldownSeconds;
                    brain.BlockedByPlayerFor = 0.01f;     // keep counting, but from the start
                }
            }
            else
            {
                // We were stuck behind them, they are still ahead, and *they* moved over: let through.
                if (brain.BlockedByPlayerFor > 1f && aheadClose && Mathf.Abs(player.Lateral - brain.PlayerLateralWhenBlocked) > 1.5f)
                {
                    ChangeGrudge(brain, m.GrudgeWhenLetThrough, m);
                }
                brain.BlockedByPlayerFor = 0f;
            }
        }

        public static void ChangeGrudge(RivalBrain brain, int delta, MemorySettings m)
        {
            brain.Grudge = Mathf.Clamp(brain.Grudge + delta, -m.GrudgeCap, m.GrudgeCap);
        }

        // ---------------------------------------------------------------- helpers

        public static DemandZone NextZone(TrafficSim sim, Agent bus)
        {
            DemandZone best = null;
            float bestDs = float.MaxValue;
            float reach = sim.Tuning.Passengers.ZoneHalfLengthMetres;
            for (int i = 0; i < sim.Zones.Count; i++)
            {
                float ds = sim.Corridor.DeltaS(bus.S, sim.Zones[i].S);
                if (ds < -reach) continue;                 // behind us
                if (ds < bestDs) { bestDs = ds; best = sim.Zones[i]; }
            }
            if (best == null && sim.Zones.Count > 0 && !sim.Corridor.Closed) return null;
            return best;
        }

        /// <summary>How far round the route we are, 0..1, measured from the first zone (the stand).</summary>
        public static float RouteFraction(TrafficSim sim, Agent bus)
        {
            float start = sim.Zones.Count > 0 ? sim.Zones[0].S : 0f;
            float along = sim.Corridor.Wrap(bus.S - start);
            return sim.Corridor.Length > 0f ? along / sim.Corridor.Length : 0f;
        }

        public static Agent NearestBusBehind(TrafficSim sim, Agent bus, float range)
        {
            Agent nearest = null;
            float best = range;
            for (int i = 0; i < sim.Agents.Count; i++)
            {
                Agent other = sim.Agents[i];
                if (other == bus || other.Class != VehicleClass.Bus || other.Corridor != bus.Corridor) continue;
                float ds = sim.Corridor.DeltaS(bus.S, other.S);
                if (ds >= 0f) continue;
                if (-ds < best) { best = -ds; nearest = other; }
            }
            return nearest;
        }

        private static bool PlayerAboutToOvertake(TrafficSim sim, Agent bus)
        {
            Agent player = sim.Player;
            if (player == null || player.Corridor != bus.Corridor) return false;
            float ds = sim.Corridor.DeltaS(bus.S, player.S);
            return ds < 0f && ds > -sim.Tuning.Memory.BlockRangeMetres && player.Speed > bus.Speed + 0.5f;
        }

        private static bool AnyoneFor(BusLoad load, DemandZone zone)
        {
            for (int i = 0; i < load.Aboard.Count; i++) if (load.Aboard[i].DestinationZone == zone.Index) return true;
            return false;
        }
    }
}
