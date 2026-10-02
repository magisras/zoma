using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>
    /// Demand zones filling up and buses working them. RESEARCH.md: "Boarding time 2 to 6 seconds
    /// per passenger ... Each passenger boards the first bus unless it's too full ... the conductor
    /// collects fares after departure so the door isn't blocked."
    ///
    /// A bus works a zone when it is within the zone's reach, slow enough, with its door open.
    /// People get off first, then on, one at a time, each at their own pace. The crowd goes to
    /// whichever bus opened its door first; a bus that is too full is skipped.
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

            if (!load.DoorOpen)
            {
                load.AtDoor = null;
                load.BoardingTimer = 0f;
                return;
            }

            // Too fast: nobody can get on or off. The helper hangs out and shouts; that's all.
            if (bus.Speed > p.DoorSpeedMs)
            {
                load.AtDoor = null;
                load.BoardingTimer = 0f;
                return;
            }

            DemandZone zone = ZoneInReach(sim, bus);

            // Someone already on the step: finish their boarding or alighting.
            if (load.AtDoor != null)
            {
                load.BoardingTimer -= dt;
                if (load.BoardingTimer > 0f) return;
                if (load.AtDoorIsAlighting)
                {
                    load.Aboard.Remove(load.AtDoor);
                    load.Alighted++;
                }
                else
                {
                    load.Aboard.Add(load.AtDoor);
                    load.Boarded++;
                    load.FaresTk += load.AtDoor.FareTk;
                }
                load.AtDoor = null;
                return;
            }

            if (zone == null) return;
            load.LastServed = zone;

            // 1. Off first: anyone whose destination is this zone.
            for (int i = 0; i < load.Aboard.Count; i++)
            {
                if (load.Aboard[i].DestinationZone == zone.Index)
                {
                    load.AtDoor = load.Aboard[i];
                    load.AtDoorIsAlighting = true;
                    load.BoardingTimer = load.AtDoor.BoardingSeconds * 0.7f;    // getting off is quicker
                    return;
                }
            }

            // 2. Then on, if this bus is the first door and not too full.
            if (zone.Waiting.Count == 0) return;
            if (load.Count >= TooFullCount(sim)) return;
            if (!IsFirstDoor(sim, bus, zone)) return;

            Passenger next = zone.Waiting[0];
            zone.Waiting.RemoveAt(0);
            next.FareTk = Fare(sim, zone, next);
            load.AtDoor = next;
            load.AtDoorIsAlighting = false;
            // Walking out to a bus stopped mid-road takes time too.
            float walk = Mathf.Abs(bus.Lateral - zone.Side * sim.Corridor.HalfWidth) * p.WalkToBusSecondsPerMetre;
            load.BoardingTimer = next.BoardingSeconds + walk;
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

        /// <summary>Is this the bus whose door has been open longest at this zone (and able to take people)?</summary>
        private static bool IsFirstDoor(TrafficSim sim, Agent bus, DemandZone zone)
        {
            float reach = sim.Tuning.Passengers.ZoneHalfLengthMetres;
            int tooFull = TooFullCount(sim);
            for (int i = 0; i < sim.Agents.Count; i++)
            {
                Agent other = sim.Agents[i];
                if (other == bus || other.Load == null || !other.Load.DoorOpen) continue;
                if (other.Speed > sim.Tuning.Passengers.DoorSpeedMs) continue;
                if (other.Load.Count >= tooFull) continue;
                if (Mathf.Abs(sim.Corridor.DeltaS(zone.S, other.S)) > reach) continue;
                if (other.Load.DoorOpenedAt < bus.Load.DoorOpenedAt) return false;
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
            float fare = Mathf.Max(p.FareMinTk, p.FarePerKmTk * km);
            return Mathf.Ceil(fare * passenger.FareFactor);
        }
    }
}
