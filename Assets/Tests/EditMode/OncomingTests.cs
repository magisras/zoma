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
            Assert.Less(bus.S, 212f - 5.5f + 0.5f, "and it did not pass through the truck");
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
    }
}
