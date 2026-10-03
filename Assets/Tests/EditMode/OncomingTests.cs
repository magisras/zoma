using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    public class OncomingTests
    {
        // Main road along +Z at x = 0; oncoming along −Z at x = 11.5 (10 m wide, 1.5 m median).
        private static TrafficSim TwoWay(int seed = 5)
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            tuning.Spawn.MedianMetres = 1.5f;
            var main = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 1000) }, 10f, false, "main");
            var oncoming = new Corridor(new List<Vector3> { new Vector3(11.5f, 0, 1000), new Vector3(11.5f, 0, 0) }, 10f, false, "oncoming");
            var sim = new TrafficSim(main, tuning, seed);
            sim.SetOncoming(oncoming);
            return sim;
        }

        private static void Run(TrafficSim sim, float seconds)
        {
            for (float t = 0; t < seconds; t += 1f / 60f) sim.Step(1f / 60f);
        }

        [Test]
        public void GhostMirrorsThePlayerWithNegativeSpeedOnTheirRoad()
        {
            var sim = TwoWay();
            Agent bus = sim.SpawnPlayerBus(200f, 11.5f);     // sitting on the oncoming centreline
            bus.Speed = 8f;
            sim.Bus.Throttle = 0.5f;
            sim.Step(1f / 60f);
            Agent g = sim.PlayerGhost;
            Assert.IsNotNull(g);
            Assert.AreSame(sim.Oncoming, g.Corridor);
            Assert.AreEqual(1000f - bus.S, g.S, 0.5f, "their S runs the other way");
            Assert.AreEqual(0f, g.Lateral, 0.2f);
            Assert.Less(g.Speed, -7f, "coming at them");
            Assert.IsTrue(sim.Metrics.WrongSideNow);
        }

        [Test]
        public void OncomingRickshawSwervesForAHeadOnBus()
        {
            var sim = TwoWay();
            Agent bus = sim.SpawnPlayerBus(200f, 11.5f);
            bus.Speed = 10f;
            sim.Bus.Throttle = 1f;
            // A rickshaw 60 m up the oncoming road, in the same band, coming towards the bus.
            Agent rickshaw = sim.SpawnVehicle(sim.Oncoming, VehicleClass.Rickshaw, 1000f - 260f, 0f, 0.5f);
            rickshaw.DesiredSpeed = 4f;
            Run(sim, 4f);
            Assert.Greater(Mathf.Abs(rickshaw.Lateral), 1.5f, "it got out of the band");
            Assert.AreEqual(0, sim.Metrics.Contacts);
        }

        [Test]
        public void WrongSideTimeAccruesOnlyBeyondTheMedian()
        {
            var sim = TwoWay();
            Agent bus = sim.SpawnPlayerBus(100f, -2f);
            Run(sim, 2f);
            Assert.AreEqual(0f, sim.Metrics.WrongSideSeconds, 1e-3f);
            bus.Lateral = 7f; bus.Position = sim.Corridor.PositionAt(bus.S, 7f);
            Run(sim, 2f);
            Assert.AreEqual(2f, sim.Metrics.WrongSideSeconds, 0.05f);
        }

        [Test]
        public void HeadOnContactPushesTheRealPlayer()
        {
            var sim = TwoWay();
            Agent bus = sim.SpawnPlayerBus(200f, 11.5f);
            bus.Speed = 6f;
            sim.Bus.Throttle = 1f;
            Agent truck = sim.SpawnVehicle(sim.Oncoming, VehicleClass.Truck, 1000f - 212f, 0f, 0.5f);   // 12 m ahead, nose to nose
            truck.DesiredSpeed = 0f; truck.Speed = 0f;
            Run(sim, 3f);
            Assert.GreaterOrEqual(sim.Metrics.Contacts, 1, "they met");
            Assert.Less(bus.Speed, 1f, "a head-on hit stops the bus");
            Assert.Less(bus.S, 212f - 5.5f + 1.5f, "and it did not pass through the truck (shoves are rate-limited: a metre of overlap is a scrape, not a pass)");
        }

        [Test]
        public void MirroredJunctionFollowsItsOfficer()
        {
            var sim = TwoWay();
            var cross = new Corridor(new List<Vector3> { new Vector3(-100, 0, 500), new Vector3(100, 0, 500) }, 8f, false, "cross");
            Junction main = sim.AddJunction(500f, cross, 100f);
            var mirror = new Junction(sim.Oncoming, 500f, cross, 111.5f) { Mirror = main };
            sim.Junctions.Add(mirror);
            main.Open = JunctionFlow.Cross; main.Timer = 5f;
            sim.Step(1f / 60f);
            Assert.AreEqual(JunctionFlow.Cross, mirror.Open);
            Run(sim, 6f);
            Assert.AreEqual(main.Open, mirror.Open);
            Assert.AreEqual(JunctionFlow.Main, mirror.Open);
        }

        [Test]
        public void ACrossVehicleCaughtBetweenTheBoxesIsLetThroughInsteadOfLockingTheCrossing()
        {
            // The trap: the second carriageway's cross stop line lies inside the first box. A rickshaw
            // that got into the first box stops there for the second's cane, the main queue waits for
            // it, and the oncoming stream never gives it a gap. Everyone sits.
            var sim = TwoWay();
            var cross = new Corridor(new List<Vector3> { new Vector3(-100, 0, 500), new Vector3(100, 0, 500) }, 8f, false, "cross");
            Junction main = sim.AddJunction(500f, cross, 100f);
            var mirror = new Junction(sim.Oncoming, 500f, cross, 111.5f) { Mirror = main };
            sim.Junctions.Add(mirror);
            main.Open = JunctionFlow.Main; main.Timer = 9999f; main.Roped = false;
            Agent rickshaw = sim.SpawnVehicle(cross, VehicleClass.Rickshaw, 100f, 0f, 0.5f);   // in the first box
            rickshaw.Speed = 0f; rickshaw.DesiredSpeed = 4f;
            Agent queued = sim.SpawnVehicle(VehicleClass.Car, 470f, 0f, 0.5f);               // main road, behind the box
            queued.DesiredSpeed = 10f;
            var stream = new List<Agent>();
            for (int i = 0; i < 4; i++)
            {
                Agent c = sim.SpawnVehicle(sim.Oncoming, VehicleClass.Car, 420f + i * 14f, 0f, 0.5f);   // oncoming, closing on its box
                c.DesiredSpeed = 10f;
                stream.Add(c);
            }
            Run(sim, 12f);
            Assert.Greater(rickshaw.S, 111.5f + 5f + 2f, "the rickshaw crossed the second carriageway too");
            Assert.Greater(queued.S, 530f, "and the main road moved on behind it");
            Assert.AreEqual(0, sim.Metrics.Contacts, "nobody drove into anyone");
        }

        [Test]
        public void ANoseOverTheLineAtTheKerbDoesNotHoldACrossBusThroughTheMiddle()
        {
            // The other lock: a CNG on the second carriageway has crept half a metre over its stop line at
            // the kerb side and stands (the cane is against it, cross traffic is committed). A cross bus
            // through the middle of the box has four metres of clear road between it and that nose.
            // It must go, or the two wait for each other until the shift ends.
            var sim = TwoWay();
            var cross = new Corridor(new List<Vector3> { new Vector3(-100, 0, 500), new Vector3(100, 0, 500) }, 8f, false, "cross");
            Junction main = sim.AddJunction(500f, cross, 100f);
            var mirror = new Junction(sim.Oncoming, 500f, cross, 111.5f) { Mirror = main };
            sim.Junctions.Add(mirror);
            main.Open = JunctionFlow.Cross; main.Timer = 9999f; main.Roped = true;
            // Oncoming road runs −Z: its S increases toward z = 0, so "before the box" is S < 500 − 4.
            Agent nose = sim.SpawnVehicle(sim.Oncoming, VehicleClass.Cng, 500f - 4f - 1f, -4.3f, 0.5f);   // nose 0.5 m into the box, kerb side
            nose.Speed = 0f; nose.DesiredSpeed = 0f;
            Agent bus = sim.SpawnVehicle(cross, VehicleClass.Bus, 85f, 0f, 0.5f);   // through the first box, heading for the second
            bus.Speed = 2f; bus.DesiredSpeed = 6f;
            Assert.IsTrue(mirror.InBox(sim.Oncoming, nose.S, nose.HalfLength), "the CNG counts as in the box");
            Run(sim, 15f);
            Assert.Greater(bus.S, 111.5f + 5f + 3f, "the bus crossed the second carriageway past the standing nose");
            Assert.AreEqual(0, sim.Metrics.Contacts, "without touching it");
        }

        [Test]
        public void AVehicleStandingAcrossTheMiddleOfTheBoxStillHoldsTheCrossBus()
        {
            var sim = TwoWay();
            var cross = new Corridor(new List<Vector3> { new Vector3(-100, 0, 500), new Vector3(100, 0, 500) }, 8f, false, "cross");
            Junction main = sim.AddJunction(500f, cross, 100f);
            main.Open = JunctionFlow.Cross; main.Timer = 9999f; main.Roped = true;
            Agent stuck = sim.SpawnVehicle(VehicleClass.Car, 500f, 0f, 0.5f);   // dead across the box centre
            stuck.Speed = 0f; stuck.DesiredSpeed = 0f;
            Agent bus = sim.SpawnVehicle(cross, VehicleClass.Bus, 50f, 0f, 0.5f);
            bus.Speed = 4f; bus.DesiredSpeed = 6f;
            Run(sim, 15f);
            Assert.Less(bus.S + bus.HalfLength, 100f - 5f + 0.5f, "the bus waited at the line");
            Assert.AreEqual(0, sim.Metrics.Contacts);
        }
    }
}
