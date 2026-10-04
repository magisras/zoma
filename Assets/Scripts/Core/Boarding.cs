using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>
    /// Demand zones filling up and buses working them. RESEARCH.md: "Boarding time 2 to 6 seconds
    /// per passenger ... Each passenger boards the first bus unless it's too full ... Helpers
    /// mostly shout, grab and pull; picking up without stopping is common. Alighting is the
    /// dangerous part: passengers are forced off running buses."
    ///
    /// A bus works a zone when it is within reach with its door open. Stopped or crawling, people
    /// get on and off at their own pace, one getting on and one getting off at the same time.
    /// Rolling faster, up to the jump speed, they still can, quicker, with a chance of falling
    /// that grows with speed. A fall at walking pace is a stumble. A fall at speed is an injury,
    /// and the street's rule about injuring people applies.
    /// </summary>
    public static class Boarding
    {
        /// <summary>Grow the crowds.</summary>
        public static void TickZones(TrafficSim sim, float dt)
        {
            PassengerSettings p = sim.Tuning.Passengers;
            for (int i = 0; i < sim.Zones.Count; i++)
            {
                DemandZone zone = sim.Zones[i];
                if (zone.Waiting.Count >= p.MaxWaiting) continue;       // the rest take a rickshaw
                zone.Accumulator += zone.RatePerMinute * dt / 60f;
                while (zone.Accumulator >= 1f && zone.Waiting.Count < p.MaxWaiting)
                {
                    zone.Accumulator -= 1f;
                    zone.Waiting.Add(NewPassenger(sim, zone));
                }
            }
        }

        /// <summary>One step of door work for one bus.</summary>
        public static void Step(TrafficSim sim, Agent bus, float dt)
        {
            BusLoad load = bus.Load;
            PassengerSettings p = sim.Tuning.Passengers;
            if (load == null) return;

            // Flying: nobody can get on or off (there is no door to shut; the doorway is always open). Whoever
            // was on the step is still on it.
            if (bus.Speed > p.JumpSpeedMs)
            {
                return;
            }

            // Rolling at a walk, the helper pulls and nobody dawdles: quicker than at a standstill. Getting off
            // happens up to the jump speed (RESEARCH: forced off running buses); getting on only up to the door
            // speed, a crawl: nobody runs alongside a bus doing 20 km/h and climbs in, and with no door on the
            // bus that is the rule that keeps a passing bus from scooping people up at speed and dropping them.
            bool rolling = bus.Speed > 1f;
            float pace = rolling ? p.MovingDoorTimeFactor : 1f;
            if (bus.IsPlayer && sim.Condition.DoorBent) pace *= 1.3f;   // a bent doorframe after the rollover
            DemandZone zone = ZoneInReach(sim, bus);
            // Arriving slow at a zone is what "first door" means now: the first bus to be here under the jump
            // speed gets the crowd until it leaves.
            if (zone != load.ArrivedZone) { load.ArrivedZone = zone; load.DoorOpenedAt = zone != null ? sim.Metrics.Time : -1f; }

            // ---- Getting off: finish the one on the step, then pick the next one for this zone. At a crawl
            // people step off as they please. Faster than that, nobody chooses to: they are forced off a
            // running bus only when it is about to carry them past (the last metres of the zone's reach),
            // the helper shouting them down the step (RESEARCH: forced off running buses). A driver who slows
            // never puts anyone in that position; one who rolls through does, and the fall rules answer.
            bool crawling = bus.Speed <= p.DoorSpeedMs;
            bool aboutToPass = zone != null && sim.Corridor.DeltaS(zone.S, bus.S) > p.ZoneHalfLengthMetres - p.ForcedOffMetres;
            if (load.Leaving != null)
            {
                load.LeavingTimer -= dt;
                if (load.LeavingTimer <= 0f)
                {
                    load.Aboard.Remove(load.Leaving);
                    load.Alighted++;
                    RollFall(sim, bus, load.Leaving, true);
                    load.Leaving = null;
                }
            }
            else if (zone != null && (crawling || aboutToPass))
            {
                for (int i = 0; i < load.Aboard.Count; i++)
                {
                    if (load.Aboard[i].DestinationZone == zone.Index)
                    {
                        load.Leaving = load.Aboard[i];
                        load.LeavingTimer = load.Leaving.BoardingSeconds * 0.7f * pace;   // getting off is quicker
                        break;
                    }
                }
            }

            // ---- Getting on: finish the one on the step, then the first in the queue if we are first door.
            if (load.AtDoor != null)
            {
                load.BoardingTimer -= dt;
                if (load.BoardingTimer <= 0f)
                {
                    Passenger boarded = load.AtDoor;
                    load.AtDoor = null;
                    if (RollFall(sim, bus, boarded, false))
                    {
                        if (zone != null) zone.Waiting.Insert(0, boarded);   // back on the kerb, shaken
                    }
                    else
                    {
                        load.Aboard.Add(boarded);
                        load.Boarded++;
                        load.FaresTk += boarded.FareTk;
                    }
                }
                return;
            }

            if (zone == null || zone.Waiting.Count == 0) return;
            if (bus.Speed > p.DoorSpeedMs) return;           // too fast to step onto
            load.LastServed = zone;
            if (load.Count >= TooFullCount(sim)) return;
            if (!IsFirstDoor(sim, bus, zone)) return;

            Passenger next = zone.Waiting[0];
            zone.Waiting.RemoveAt(0);
            next.FareTk = Fare(sim, zone, next);
            load.AtDoor = next;
            // Walking out to a bus stopped mid-road takes time too.
            float walk = Mathf.Abs(bus.Lateral - zone.Side * sim.Corridor.HalfWidth) * p.WalkToBusSecondsPerMetre;
            load.BoardingTimer = (next.BoardingSeconds + walk) * pace;
        }

        /// <summary>
        /// Did this person fall? Chance grows with speed above walking pace; getting off is worse.
        /// A fall below the injury speed is a stumble; above it, an injury the world answers.
        /// Returns true on any fall.
        /// </summary>
        private static bool RollFall(TrafficSim sim, Agent bus, Passenger person, bool alighting)
        {
            PassengerSettings p = sim.Tuning.Passengers;
            float speed = bus.Speed;
            if (speed <= 1f) return false;
            float chance = p.FallChancePerMs * (speed - 1f) * (alighting ? p.AlightFallFactor : 1f);
            if (!sim.Random.Chance(chance)) return false;

            BusLoad load = bus.Load;
            load.LastFallTime = sim.Metrics.Time;
            load.LastFallWasInjury = speed >= p.InjurySpeedMs;
            if (load.LastFallWasInjury)
            {
                load.Injuries++;
                if (bus.IsPlayer) sim.Economy.OnInjury(alighting);
            }
            else
            {
                load.Stumbles++;
            }
            return true;
        }

        /// <summary>Passengers who wanted this zone but the bus didn't slow: they ride on, unhappily.</summary>
        public static void NoteMissedAlights(TrafficSim sim, Agent bus, DemandZone zonePassed)
        {
            BusLoad load = bus.Load;
            for (int i = 0; i < load.Aboard.Count; i++)
            {
                if (load.Aboard[i].DestinationZone == zonePassed.Index)
                {
                    load.MissedAlights++;
                    // They'll get off at the next zone the bus works.
                    load.Aboard[i].DestinationZone = (zonePassed.Index + 1) % sim.Zones.Count;
                }
            }
        }

        public static DemandZone ZoneInReach(TrafficSim sim, Agent bus)
        {
            float reach = sim.Tuning.Passengers.ZoneHalfLengthMetres;
            for (int i = 0; i < sim.Zones.Count; i++)
            {
                DemandZone zone = sim.Zones[i];
                if (Mathf.Abs(sim.Corridor.DeltaS(zone.S, bus.S)) <= reach) return zone;
            }
            return null;
        }

        public static int TooFullCount(TrafficSim sim)
        {
            return Mathf.Min(sim.Tuning.Bus.CrushCapacity, Mathf.RoundToInt(sim.Tuning.Bus.Seats * sim.Tuning.Passengers.TooFullLoad));
        }

        /// <summary>Is this the bus that has been at this zone longest, slow enough to load, with room aboard?</summary>
        private static bool IsFirstDoor(TrafficSim sim, Agent bus, DemandZone zone)
        {
            float reach = sim.Tuning.Passengers.ZoneHalfLengthMetres;
            int tooFull = TooFullCount(sim);
            for (int i = 0; i < sim.Agents.Count; i++)
            {
                Agent other = sim.Agents[i];
                if (other == bus || other.Load == null || other.Load.ArrivedZone != zone) continue;
                if (other.Speed > sim.Tuning.Passengers.JumpSpeedMs) continue;
                if (other.Load.Count >= tooFull) continue;
                if (Mathf.Abs(sim.Corridor.DeltaS(zone.S, other.S)) > reach) continue;
                if (other.Load.DoorOpenedAt < bus.Load.DoorOpenedAt) return false;
                if (other.Load.DoorOpenedAt == bus.Load.DoorOpenedAt && other.Id < bus.Id) return false;   // the same step: the older agent
            }
            return true;
        }

        private static Passenger NewPassenger(TrafficSim sim, DemandZone from)
        {
            PassengerSettings p = sim.Tuning.Passengers;
            float roll = sim.Random.Value;
            var passenger = new Passenger();
            if (roll < p.StudentShare)
            {
                passenger.Kind = PassengerKind.Student;
                passenger.BoardingSeconds = p.StudentSeconds;
                passenger.FareFactor = p.StudentFareFactor;
            }
            else if (roll < p.StudentShare + p.ElderlyShare)
            {
                passenger.Kind = PassengerKind.ElderlyWithSack;
                passenger.BoardingSeconds = p.ElderlyWithSackSeconds;
            }
            else if (roll < p.StudentShare + p.ElderlyShare + p.ArguerShare)
            {
                passenger.Kind = PassengerKind.Arguer;
                passenger.BoardingSeconds = p.BoardingMaxSeconds;
            }
            else
            {
                passenger.Kind = PassengerKind.Regular;
                passenger.BoardingSeconds = sim.Random.Range(p.BoardingMinSeconds, p.BoardingMaxSeconds);
            }
            // Going somewhere one to four zones ahead.
            int hops = sim.Random.Range(1, Mathf.Min(5, sim.Zones.Count));
            passenger.DestinationZone = (from.Index + hops) % sim.Zones.Count;
            return passenger;
        }

        /// <summary>Government chart: per km with a minimum (RESEARCH.md, Ticket prices), times the kind's factor.</summary>
        public static float Fare(TrafficSim sim, DemandZone from, Passenger passenger)
        {
            PassengerSettings p = sim.Tuning.Passengers;
            DemandZone to = sim.Zones[passenger.DestinationZone];
            float km = Mathf.Abs(sim.Corridor.DeltaS(from.S, to.S)) / 1000f;
            if (sim.Corridor.Closed && sim.Corridor.DeltaS(from.S, to.S) < 0f) km = (sim.Corridor.Length + sim.Corridor.DeltaS(from.S, to.S)) / 1000f;
            km *= sim.DistanceScale;   // the ring's metres stand for a trip's kilometres
            float fare = Mathf.Max(p.FareMinTk, p.FarePerKmTk * km);
            return Mathf.Ceil(fare * passenger.FareFactor);
        }
    }
}
