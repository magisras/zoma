using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    public class BoardingTests
    {
        private static TrafficSim QuietWithZones()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            var corridor = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 2000) }, 10f);
            var sim = new TrafficSim(corridor, tuning, 5);
            sim.AddZone("Stand", 100f, false);
            sim.AddZone("Market", 600f, true);
            sim.AddZone("School", 1100f, false);
            sim.AddZone("End", 1600f, false);
            return sim;
        }

        private static void Run(TrafficSim sim, float seconds)
        {
            for (float t = 0; t < seconds; t += 1f / 60f) sim.Step(1f / 60f);
        }

        [Test]
        public void CrowdsGrowFasterAtHotZones()
        {
            var sim = QuietWithZones();
            sim.SpawnPlayerBus(1900f, -2f);      // far from every zone
            Run(sim, 120f);
            int stand = sim.Zones[0].Waiting.Count, market = sim.Zones[1].Waiting.Count;
            Assert.GreaterOrEqual(stand, 5);
            Assert.Greater(market, stand, "the hot zone fills faster");
            Assert.LessOrEqual(market, sim.Tuning.Passengers.MaxWaiting);
        }

        [Test]
        public void NobodyBoardsWithTheDoorShutOrAtSpeed()
        {
            var sim = QuietWithZones();
            Agent bus = sim.SpawnPlayerBus(100f, -2f);
            sim.SetPassengerCount(bus, 0);
            for (int i = 0; i < 5; i++) sim.Zones[0].Waiting.Add(new Passenger { BoardingSeconds = 2f, DestinationZone = 1 });
            Run(sim, 10f);
            Assert.AreEqual(0, bus.Load.Count, "door shut");

            sim.SetDoor(true);
            bus.Speed = 6f;                       // rolling too fast; BusController keeps it there with no drag? No: just test one step.
            sim.Bus.Throttle = 1f;
            sim.Step(1f / 60f);
            Assert.IsNull(bus.Load.AtDoor, "too fast for the helper to pull anyone in");
        }

        [Test]
        public void BoardingTakesEachPassengersOwnTime()
        {
            var sim = QuietWithZones();
            Agent bus = sim.SpawnPlayerBus(100f, -3.5f);      // tight to the left kerb: no walk time
            sim.SetPassengerCount(bus, 0);
            sim.Zones[0].RatePerMinute = 0f;
            sim.Zones[0].Waiting.Add(new Passenger { Kind = PassengerKind.Student, BoardingSeconds = 2f, DestinationZone = 1 });
            sim.Zones[0].Waiting.Add(new Passenger { Kind = PassengerKind.ElderlyWithSack, BoardingSeconds = 6f, DestinationZone = 2 });
            sim.SetDoor(true);
            // 1.5 m from the kerb adds 0.6 s of walking to each boarding.
            Run(sim, 2.8f);
            Assert.AreEqual(1, bus.Load.Count, "the student is aboard after 2 s + walk");
            Run(sim, 3f);
            Assert.AreEqual(1, bus.Load.Count, "the elderly passenger is still on the step");
            Run(sim, 4f);
            Assert.AreEqual(2, bus.Load.Count);
            Assert.AreEqual(0, sim.Zones[0].Waiting.Count);
        }

        [Test]
        public void FaresFollowTheChartWithStudentHalf()
        {
            var sim = QuietWithZones();
            var regular = new Passenger { DestinationZone = 1 };           // 500 m: below the minimum
            Assert.AreEqual(10f, Boarding.Fare(sim, sim.Zones[0], regular));
            var far = new Passenger { DestinationZone = 3 };               // 1500 m × 2.70 = 4.05 → min 10
            Assert.AreEqual(10f, Boarding.Fare(sim, sim.Zones[0], far));
            sim.Tuning.Passengers.FarePerKmTk = 20f;                       // make distance matter
            Assert.AreEqual(30f, Boarding.Fare(sim, sim.Zones[0], far));
            var student = new Passenger { DestinationZone = 3, FareFactor = 0.5f };
            Assert.AreEqual(15f, Boarding.Fare(sim, sim.Zones[0], student));
        }

        [Test]
        public void TooFullBusIsSkipped()
        {
            var sim = QuietWithZones();
            Agent bus = sim.SpawnPlayerBus(100f, -3.5f);
            sim.SetPassengerCount(bus, Boarding.TooFullCount(sim));
            foreach (Passenger rider in bus.Load.Aboard) rider.DestinationZone = 2;   // nobody gets off here
            sim.Zones[0].Waiting.Add(new Passenger { BoardingSeconds = 1f, DestinationZone = 1 });
            sim.SetDoor(true);
            Run(sim, 5f);
            Assert.AreEqual(1, sim.Zones[0].Waiting.Count, "nobody squeezes onto a crush-loaded bus");
        }

        [Test]
        public void PassengersGetOffAtTheirZoneAndMissedOnesRideOn()
        {
            var sim = QuietWithZones();
            Agent bus = sim.SpawnPlayerBus(600f, -3.5f);         // at the Market
            sim.SetPassengerCount(bus, 0);
            bus.Load.Aboard.Add(new Passenger { BoardingSeconds = 2f, DestinationZone = 1 });
            bus.Load.Aboard.Add(new Passenger { BoardingSeconds = 2f, DestinationZone = 2 });
            sim.Zones[1].RatePerMinute = 0f;
            sim.SetDoor(true);
            Run(sim, 3f);
            Assert.AreEqual(1, bus.Load.Count, "the Market passenger got off");
            Assert.AreEqual(1, bus.Load.Alighted);

            // Now drive past the School without slowing.
            sim.SetDoor(false);
            bus.S = 1050f; bus.Position = sim.Corridor.PositionAt(bus.S, bus.Lateral);
            sim.Bus.Throttle = 1f;
            bus.Speed = 10f;
            Run(sim, 12f);
            Assert.AreEqual(1, bus.Load.MissedAlights, "the School passenger was carried past");
            Assert.AreEqual(3, bus.Load.Aboard[0].DestinationZone, "and will get off at the next zone");
        }

        [Test]
        public void FirstOpenDoorTakesTheCrowd()
        {
            var sim = QuietWithZones();
            Agent first = sim.SpawnPlayerBus(95f, -3.5f);
            sim.SetPassengerCount(first, 0);
            sim.Zones[0].RatePerMinute = 0f;
            for (int i = 0; i < 3; i++) sim.Zones[0].Waiting.Add(new Passenger { BoardingSeconds = 1f, DestinationZone = 1 });

            Agent second = sim.SpawnVehicle(VehicleClass.Bus, 105f, 0f, 0.5f);
            second.Load = new BusLoad();
            second.Speed = 0f; second.DesiredSpeed = 0f;

            sim.SetDoor(true);                                   // player opens at t≈0
            sim.Step(1f / 60f);
            second.Load.DoorOpen = true;                         // rival opens a moment later
            second.Load.DoorOpenedAt = sim.Metrics.Time;
            Run(sim, 6f);
            Assert.AreEqual(3, first.Load.Count, "the first door got everyone");
            Assert.AreEqual(0, second.Load.Count);
        }
    }
}
