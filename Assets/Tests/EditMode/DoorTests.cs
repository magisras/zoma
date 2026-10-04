using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    public class DoorTests
    {
        private static TrafficSim World()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            var road = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 2000) }, 10f);
            var sim = new TrafficSim(road, tuning, 9);
            sim.AddZone("A", 100f, false);
            sim.AddZone("B", 600f, false);
            sim.AddZone("C", 1100f, false);
            foreach (DemandZone z in sim.Zones) z.RatePerMinute = 0f;
            return sim;
        }

        private static void Run(TrafficSim sim, float seconds)
        {
            for (float t = 0; t < seconds; t += 1f / 60f) sim.Step(1f / 60f);
        }

        [Test]
        public void SomeoneGetsOffWhileSomeoneGetsOn()
        {
            var sim = World();
            Agent bus = sim.SpawnPlayerBus(100f, -3.5f);
            sim.SetPassengerCount(bus, 0);
            bus.Load.Aboard.Add(new Passenger { BoardingSeconds = 4f, DestinationZone = 0 });   // wants off here, 2.8 s
            sim.Zones[0].Waiting.Add(new Passenger { BoardingSeconds = 3f, DestinationZone = 1 });
            sim.SetDoor(true);
            Run(sim, 1f);
            Assert.IsNotNull(bus.Load.Leaving, "one on the way off");
            Assert.IsNotNull(bus.Load.AtDoor, "one on the way on, at the same time");
            Run(sim, 3f);
            Assert.AreEqual(1, bus.Load.Alighted);
            Assert.AreEqual(1, bus.Load.Boarded);
        }

        [Test]
        public void RollingUnderTheJumpSpeedStillLoadsFasterButNotAbove()
        {
            var sim = World();
            sim.Tuning.Passengers.FallChancePerMs = 0f;
            Agent bus = sim.SpawnPlayerBus(100f, -3.5f);
            sim.SetPassengerCount(bus, 0);
            sim.Zones[0].Waiting.Add(new Passenger { BoardingSeconds = 4f, DestinationZone = 1 });
            sim.SetDoor(true);
            bus.Speed = 2.5f; sim.Bus.Throttle = 0.2f;                    // rolling at a brisk walk
            Run(sim, 0.1f);
            Assert.IsNotNull(bus.Load.AtDoor, "the helper pulls them aboard on the move");
            Assert.AreEqual((4f + 0.6f) * 0.7f, bus.Load.BoardingTimer, 0.2f, "quicker than at a standstill (walk time included)");

            var fast = World();
            Agent bus2 = fast.SpawnPlayerBus(100f, -3.5f);
            fast.SetPassengerCount(bus2, 0);
            fast.Zones[0].Waiting.Add(new Passenger { BoardingSeconds = 4f, DestinationZone = 1 });
            fast.SetDoor(true);
            bus2.Speed = 5f; fast.Bus.Throttle = 1f;                      // 18 km/h: nobody climbs onto that
            Run(fast, 0.5f);
            Assert.IsNull(bus2.Load.AtDoor, "too fast for anyone to get on");
        }

        [Test]
        public void AFallAtWalkingPaceIsAStumbleBackToTheKerb()
        {
            var sim = World();
            sim.Tuning.Passengers.FallChancePerMs = 1f;                   // certain, for the test
            Agent bus = sim.SpawnPlayerBus(100f, -3.5f);
            sim.SetPassengerCount(bus, 0);
            sim.Zones[0].Waiting.Add(new Passenger { BoardingSeconds = 1f, DestinationZone = 1 });
            sim.SetDoor(true);
            bus.Speed = 2f; sim.Bus.Throttle = 0.25f;
            Run(sim, 2.5f);
            Assert.GreaterOrEqual(bus.Load.Stumbles, 1, "fell, went back to the kerb, tried again");
            Assert.AreEqual(0, bus.Load.Injuries);
            Assert.AreEqual(0, bus.Load.Count, "did not make it aboard");
            Assert.IsFalse(sim.Bus.Held);
        }

        [Test]
        public void AFallAtSpeedIsAnInjuryTheCrowdAnswers()
        {
            var sim = World();
            sim.Tuning.Passengers.FallChancePerMs = 1f;
            sim.Tuning.Economy.InjuryHoldSeconds = 5f;
            Agent bus = sim.SpawnPlayerBus(114f, -3.5f);                                       // the last metres of zone A's reach
            sim.SetPassengerCount(bus, 0);
            bus.Load.Aboard.Add(new Passenger { BoardingSeconds = 1f, DestinationZone = 0 });  // forced off at 5 m/s as the bus carries them past
            sim.SetDoor(true);
            bus.Speed = 5f; sim.Bus.Throttle = 0.5f;
            Run(sim, 1.5f);
            Assert.AreEqual(1, bus.Load.Injuries);
            Assert.IsTrue(sim.Bus.Held, "the crowd holds the bus");
            Assert.AreEqual(sim.Tuning.Economy.InjuryCompensationTk, sim.Economy.Ledger.CaseTk, 1e-3f);
            Assert.IsFalse(sim.Economy.DayOver, "the first one is paid for");
            Run(sim, 6f);
            Assert.IsFalse(sim.Bus.Held, "released after the hold");
        }

        [Test]
        public void TheSecondInjuryEndsTheDay()
        {
            var sim = World();
            sim.SpawnPlayerBus(100f, -2f);
            sim.Economy.OnInjury(true);
            Assert.IsFalse(sim.Economy.DayOver);
            sim.Economy.OnInjury(false);
            Assert.IsTrue(sim.Economy.DayOver);
            Assert.IsTrue(sim.Economy.Ledger.Arrested);
        }
    }
}
