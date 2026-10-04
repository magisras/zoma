using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    public class RivalTests
    {
        private static TrafficSim World(int seed = 7)
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            var ring = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 500), new Vector3(300, 0, 500), new Vector3(300, 0, 0) }, 10f, closed: true);
            var sim = new TrafficSim(ring, tuning, seed);
            sim.AddZone("Stand", 50f, true);
            sim.AddZone("Mid", 400f, false);
            sim.AddZone("Far", 900f, false);
            sim.AddZone("Back", 1300f, false);
            foreach (DemandZone z in sim.Zones) z.RatePerMinute = 0f;
            return sim;
        }

        private static void Run(TrafficSim sim, float seconds)
        {
            for (float t = 0; t < seconds; t += 1f / 60f) sim.Step(1f / 60f);
        }

        private static void Crowd(DemandZone zone, int n)
        {
            for (int i = 0; i < n; i++) zone.Waiting.Add(new Passenger { BoardingSeconds = 1f, DestinationZone = (zone.Index + 1) % 4 });
        }

        [Test]
        public void HalfEmptyBusWithCrowdAheadRaces()
        {
            var sim = World();
            Agent bus = sim.SpawnRivalBus("Rafiq", DriverPersonality.Default(), 300f, -2f);
            Crowd(sim.Zones[1], 20);
            RivalAI.Decide(sim, bus);
            Assert.AreEqual(BusAction.RaceForNextStop, bus.Brain.Action);
        }

        [Test]
        public void NearlyFullBusWithSmallCrowdSkips()
        {
            var sim = World();
            Agent bus = sim.SpawnRivalBus("Rafiq", DriverPersonality.Default(), 300f, -2f);
            sim.SetPassengerCount(bus, 50);
            Crowd(sim.Zones[1], 1);
            RivalAI.Decide(sim, bus);
            Assert.AreEqual(BusAction.SkipTheStop, bus.Brain.Action);
        }

        [Test]
        public void NobodyFishesOnTheRoadUnlessTheTableSaysSo()
        {
            // Owner, 4 Oct 2026: buses do not wait for passengers. WaitBonus is 0 by default; the action still exists
            // for a table that wants the BUET "intentional waiting" at the terminal.
            var sim = World();
            Agent bus = sim.SpawnRivalBus("Rafiq", DriverPersonality.Default(), 50f, -3f);   // at the stand, early in route
            sim.SetPassengerCount(bus, 5);
            Crowd(sim.Zones[0], 2);
            RivalAI.Decide(sim, bus);
            Assert.AreNotEqual(BusAction.WaitAndFill, bus.Brain.Action);
            sim.Tuning.Utility.WaitBonus = 0.6f;
            RivalAI.Decide(sim, bus);
            Assert.AreEqual(BusAction.WaitAndFill, bus.Brain.Action);
        }

        [Test]
        public void SpitefulDriverWithGrudgeBlocksTheOvertake()
        {
            var sim = World();
            Agent player = sim.SpawnPlayerBus(280f, 2f);
            player.Speed = 8f;
            Agent bus = sim.SpawnRivalBus("Jamal", DriverPersonality.Spiteful(), 300f, -2f);
            bus.Speed = 4f;
            bus.Brain.Grudge = 4;
            RivalAI.Decide(sim, bus);
            Assert.AreEqual(BusAction.BlockThePlayer, bus.Brain.Action);
            // Without a grudge he still blocks: it is the job (the pack). Only with the base score off is it just racing.
            bus.Brain.Grudge = 0;
            RivalAI.Decide(sim, bus);
            Assert.AreEqual(BusAction.BlockThePlayer, bus.Brain.Action);
            sim.Tuning.Utility.BlockBase = 0f;
            RivalAI.Decide(sim, bus);
            Assert.AreNotEqual(BusAction.BlockThePlayer, bus.Brain.Action);
        }

        [Test]
        public void ExhaustedDriverBacksOff()
        {
            var sim = World();
            Agent bus = sim.SpawnRivalBus("Rafiq", DriverPersonality.Cautious(), 300f, -2f);
            bus.Brain.Fatigue = 1f;
            RivalAI.Decide(sim, bus);
            Assert.AreEqual(BusAction.BackOff, bus.Brain.Action);
            Assert.Greater(bus.Brain.Scores[(int)BusAction.BackOff], bus.Brain.Scores[(int)BusAction.RaceForNextStop]);
        }

        [Test]
        public void RivalStopsAtTheCrowdAndBoards()
        {
            var sim = World();
            Agent bus = sim.SpawnRivalBus("Rafiq", DriverPersonality.Default(), 330f, 0f);   // 70 m before Mid
            sim.SetPassengerCount(bus, 10);
            Crowd(sim.Zones[1], 6);
            Run(sim, 40f);
            Assert.Greater(bus.Load.Count, 10, "boarded someone at Mid");
            Assert.Less(sim.Zones[1].Waiting.Count, 6);
            Assert.Greater(sim.Corridor.DeltaS(sim.Zones[1].S, bus.S), -25f, "it got to the zone");
        }

        [Test]
        public void CuttingInFrontOfHimEarnsAGrudge()
        {
            var sim = World();
            Agent player = sim.SpawnPlayerBus(312f, 4f);          // beside the lane, 12 m ahead
            player.Speed = 4f;
            Agent bus = sim.SpawnRivalBus("Jamal", DriverPersonality.Spiteful(), 300f, 0f);
            bus.Speed = 8f;
            sim.Step(1f / 60f);
            Assert.AreEqual(0, bus.Brain.Grudge);
            // The player swerves into his band right in front of him.
            player.Lateral = 0.5f; player.Position = sim.Corridor.PositionAt(player.S, 0.5f);
            sim.Step(1f / 60f);
            Assert.AreEqual(1, bus.Brain.Grudge, "cut off");
        }

        private static TrafficSim NarrowWorld()
        {
            // A one-bus-wide road: nobody can pass anybody.
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            var road = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 2000) }, 3.2f);
            return new TrafficSim(road, tuning, 3);
        }

        [Test]
        public void HoldingHimUpEarnsAGrudgeMovingAsideRemovesIt()
        {
            var sim = NarrowWorld();
            Agent player = sim.SpawnPlayerBus(320f, 0f);
            player.Speed = 2f;
            sim.Bus.Throttle = 0.2f;                             // keeps it crawling
            Agent bus = sim.SpawnRivalBus("Jamal", DriverPersonality.Spiteful(), 300f, 0f);
            bus.Speed = 2f;
            Run(sim, 6f);
            Assert.GreaterOrEqual(bus.Brain.Grudge, 1, "held up for more than 3 s");

            int before = bus.Brain.Grudge;
            Run(sim, 2f);                                        // still stuck, cooldown running
            // The player pulls over (off this narrow road, for the test) while still ahead.
            player.Lateral = 3f; player.Position = sim.Corridor.PositionAt(player.S, 3f);
            sim.Step(1f / 60f);
            Assert.AreEqual(before - 1, bus.Brain.Grudge, "let through");
        }

        [Test]
        public void StopsLostWhenRivalTakesTheCrowdJustAhead()
        {
            var sim = World();
            Agent player = sim.SpawnPlayerBus(330f, -2f);        // 70 m behind Mid
            Agent rival = sim.SpawnRivalBus("Rafiq", DriverPersonality.Reckless(), 395f, -3.5f);  // at Mid
            rival.Speed = 0f;
            Crowd(sim.Zones[1], 5);
            Run(sim, 20f);
            Assert.GreaterOrEqual(sim.Metrics.StopsLost, 1);
        }

        [Test]
        public void OtherCompanyBusRacesWhenABusIsNear()
        {
            var sim = World();
            Agent generic = sim.SpawnVehicle(VehicleClass.Bus, 200f, 0f, 0.5f);
            sim.Step(1f / 60f);
            float alone = generic.DesiredSpeed;
            sim.SpawnVehicle(VehicleClass.Bus, 230f, 2f, 0.5f);
            sim.Step(1f / 60f);
            Assert.Greater(generic.DesiredSpeed, alone);
        }
    
        // ---- The pack (docs/ROUTE_AND_TRIPS.md, "The pack"): the first at the stop takes all, the pass happens at the door.

        [Test]
        public void LoadingRivalLeavesTheKerbWhenARouteBusClosesFromBehind()
        {
            var sim = World();
            Agent bus = sim.SpawnRivalBus("Rafiq", DriverPersonality.Default(), 395f, -3f);
            bus.Speed = 0f;
            Crowd(sim.Zones[1], 2);                       // the last couple on the kerb: at PackHoldCrowd
            Run(sim, 3f);
            Assert.IsTrue(bus.Brain.Stopping, "he should be working the kerb");
            // The player comes up behind him, moving.
            Agent player = sim.SpawnPlayerBus(375f, -2f);
            player.Speed = 6f;
            Run(sim, 1f);
            Assert.IsFalse(bus.Brain.Stopping, "a route bus 20 m behind and closing: take what is on the step and go");
            Assert.AreEqual(sim.Zones[1], bus.Brain.LastLeft);
        }

        [Test]
        public void LoadingRivalHoldsAFullKerbWhateverIsBehind()
        {
            var sim = World();
            Agent bus = sim.SpawnRivalBus("Rafiq", DriverPersonality.Default(), 395f, -3f);
            bus.Speed = 0f;
            Crowd(sim.Zones[1], 20);                      // a full kerb: worth more than the lead
            Run(sim, 3f);
            Agent player = sim.SpawnPlayerBus(375f, -2f);
            player.Speed = 6f;
            Run(sim, 1f);
            Assert.IsTrue(bus.Brain.Stopping, "twenty on the kerb: he stays, whoever honks");
        }

        [Test]
        public void RivalPassesAStopWhereAnotherBusIsAlreadyLoading()
        {
            var sim = World();
            Agent player = sim.SpawnPlayerBus(398f, -3f);  // the player's door is open at Mid
            player.Speed = 0f;
            Crowd(sim.Zones[1], 10);
            Run(sim, 2f);
            Assert.IsNotNull(player.Load.ArrivedZone, "the player should be first door at Mid");
            Agent bus = sim.SpawnRivalBus("Rafiq", DriverPersonality.Default(), 350f, -2f);
            bus.Speed = 8f;
            bus.DesiredSpeed = 8f;
            Run(sim, 12f);
            Assert.IsFalse(bus.Brain.Stopping, "the crowd is the other door's: he does not queue");
            Assert.Greater(sim.Corridor.DeltaS(sim.Zones[1].S, bus.S), 0f, "he should have gone past the zone");
            // With the rule off he stops behind the player like before.
            var sim2 = World();
            Agent p2 = sim2.SpawnPlayerBus(398f, -3f); p2.Speed = 0f;
            Crowd(sim2.Zones[1], 10);
            Run(sim2, 2f);
            sim2.Tuning.Utility.PassLoadingBus = false;
            Agent b2 = sim2.SpawnRivalBus("Rafiq", DriverPersonality.Default(), 350f, -2f);
            b2.Speed = 8f;
            Run(sim2, 12f);
            Assert.IsTrue(b2.Brain.Stopping);
        }

        [Test]
        public void TheContestIsCountedOncePerArrivalAndKnowsWhoWasFirst()
        {
            var sim = World();
            Agent player = sim.SpawnPlayerBus(398f, -3f);
            player.Speed = 0f;
            Crowd(sim.Zones[1], 10);
            Run(sim, 3f);
            Assert.AreEqual(1, sim.Metrics.StopsContested);
            Assert.AreEqual(1, sim.Metrics.StopsFirst);
            Assert.AreEqual(400f, sim.Metrics.LeadMetresSum, 0.01f, "nobody behind: the lead is the cap");
            // A second bus arriving at the same kerb is counted on its own arrival, and it is not first.
            var sim2 = World();
            Agent rival = sim2.SpawnRivalBus("Rafiq", DriverPersonality.Default(), 398f, -3f);
            rival.Speed = 0f;
            Crowd(sim2.Zones[1], 20);
            Run(sim2, 2f);
            Agent p2 = sim2.SpawnPlayerBus(385f, 1f);
            p2.Speed = 0f;
            Run(sim2, 2f);
            Assert.AreEqual(1, sim2.Metrics.StopsContested);
            Assert.AreEqual(0, sim2.Metrics.StopsFirst, "the rival's door was open first");
        }
    }
}
