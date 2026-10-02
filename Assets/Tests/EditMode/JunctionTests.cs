using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    public class JunctionTests
    {
        private static Corridor Loop()
        {
            // A 400 m square loop.
            return new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 100), new Vector3(100, 0, 100), new Vector3(100, 0, 0) }, 10f, closed: true);
        }

        private static TrafficSim Quiet(Corridor corridor, int seed = 1)
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            tuning.Spawn.CrossVehiclesPerJunction = 0;
            return new TrafficSim(corridor, tuning, seed);
        }

        private static void Run(TrafficSim sim, float seconds)
        {
            for (float t = 0; t < seconds; t += 1f / 60f) sim.Step(1f / 60f);
        }

        [Test]
        public void LoopWrapsAndMeasuresTheShortWayRound()
        {
            var loop = Loop();
            Assert.AreEqual(400f, loop.Length, 1e-3f);
            Assert.AreEqual(10f, loop.Wrap(410f), 1e-3f);
            Assert.AreEqual(20f, loop.DeltaS(390f, 10f), 1e-3f, "10 is 20 m ahead of 390 on a 400 m loop");
            Assert.AreEqual(-20f, loop.DeltaS(10f, 390f), 1e-3f);
            // Position at the wrapped S equals the position at S.
            Vector3 a = loop.PositionAt(50f, 1f), b = loop.PositionAt(450f, 1f);
            Assert.AreEqual(a.x, b.x, 1e-3f); Assert.AreEqual(a.z, b.z, 1e-3f);
        }

        [Test]
        public void FollowerSeesLeaderAcrossTheSeam()
        {
            var sim = Quiet(Loop());
            Agent leader = sim.SpawnVehicle(VehicleClass.Rickshaw, 5f, 0f, 0.5f);      // just past the seam
            Agent follower = sim.SpawnVehicle(VehicleClass.Car, 390f, 0f, 0.0f);      // just before it
            float gap;
            Agent seen = Steering.FindAhead(sim.Agents, follower, 0f, 80f, out gap);
            Assert.AreSame(leader, seen);
            Assert.AreEqual(15f - follower.HalfLength - leader.HalfLength, gap, 1e-3f);
        }

        private static Corridor Straight(float length, float width = 10f, string name = "road")
        {
            return new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, length) }, width, false, name);
        }

        private static Corridor CrossStreet(float atZ)
        {
            // Runs along +X through the main road at z = atZ, from x = −100 to x = +100.
            return new Corridor(new List<Vector3> { new Vector3(-100, 0, atZ), new Vector3(100, 0, atZ) }, 8f, false, "cross");
        }

        [Test]
        public void CarStopsAtClosedStopLineAndGoesWhenOpened()
        {
            var sim = Quiet(Straight(400f));
            Junction j = sim.AddJunction(200f, CrossStreet(200f), 100f);
            j.Open = JunctionFlow.Cross;          // cane against the main road
            j.Timer = 999f;
            j.LeakersLeft = 0;
            Agent car = sim.SpawnVehicle(VehicleClass.Car, 100f, 0f, 0.5f);
            car.DesiredSpeed = 10f;

            Run(sim, 20f);
            float line = j.StopLineOn(sim.Corridor, sim.Tuning.Officer.StopLineSetbackMetres);
            Assert.Less(car.S + car.HalfLength, line + 0.1f, "waits at the line");
            Assert.Greater(car.S + car.HalfLength, line - 3f, "close to it, not far back");
            Assert.Less(car.Speed, 0.2f);

            j.Open = JunctionFlow.Main;
            Run(sim, 10f);
            Assert.Greater(car.S, 230f, "crosses once the cane lifts");
        }

        [Test]
        public void OfficerAlternatesWithVariableTiming()
        {
            var sim = Quiet(Straight(400f), seed: 3);
            Junction j = sim.AddJunction(200f, CrossStreet(200f), 100f);
            var seen = new List<float>();
            JunctionFlow last = j.Open;
            float phase = 0f;
            for (float t = 0; t < 600f; t += 1f / 60f)
            {
                sim.Step(1f / 60f);
                phase += 1f / 60f;
                if (j.Open != last) { seen.Add(phase); phase = 0f; last = j.Open; }
            }
            Assert.GreaterOrEqual(seen.Count, 5);
            foreach (float p in seen)
            {
                Assert.GreaterOrEqual(p, sim.Tuning.Officer.OpenMinSeconds - 0.1f);
                Assert.LessOrEqual(p, sim.Tuning.Officer.OpenMaxSeconds + 0.1f);
            }
        }

        [Test]
        public void AFewLeakersRunTheCane()
        {
            var sim = Quiet(Straight(400f));
            Junction j = sim.AddJunction(200f, CrossStreet(200f), 100f);
            j.Open = JunctionFlow.Main;
            j.Timer = 2f;                        // closes after 2 s, with LeakersPerCycle leakers
            // Five cars in a column approaching at 10 m/s, 15 m apart, the first 5 m short of the line at closing time.
            float line = j.StopLineOn(sim.Corridor, sim.Tuning.Officer.StopLineSetbackMetres);
            var cars = new List<Agent>();
            for (int i = 0; i < 5; i++)
            {
                Agent c = sim.SpawnVehicle(VehicleClass.Car, line - 25f - i * 15f, 0f, 0.5f);
                c.Speed = 10f; c.DesiredSpeed = 10f;
                cars.Add(c);
            }
            Run(sim, 2.1f);
            Assert.AreEqual(JunctionFlow.Cross, j.Open);
            Run(sim, 15f);

            int through = 0, waiting = 0;
            foreach (Agent c in cars)
            {
                if (c.S > 210f) through++;
                else if (c.Speed < 0.3f) waiting++;
            }
            Assert.AreEqual(sim.Tuning.Officer.LeakersPerCycle, through, "exactly the leakers went through");
            Assert.AreEqual(5 - through, waiting, "the rest queue at the line");
        }

        [Test]
        public void CrossTrafficInTheBoxBlocksEvenWhenOpen()
        {
            var sim = Quiet(Straight(400f));
            Corridor cross = CrossStreet(200f);
            Junction j = sim.AddJunction(200f, cross, 100f);
            j.Open = JunctionFlow.Main;
            j.Timer = 999f;
            // A truck stalled in the middle of the box, on the cross street.
            Agent truck = sim.SpawnVehicle(cross, VehicleClass.Truck, 100f, 0f, 0.5f);
            truck.Speed = 0f; truck.DesiredSpeed = 0f;
            Agent car = sim.SpawnVehicle(VehicleClass.Car, 150f, 0f, 0.5f);
            car.DesiredSpeed = 10f;

            Run(sim, 15f);
            Assert.Less(car.S + car.HalfLength, 200f - j.MainHalfSpan, "held before the box by the truck in it");
            Assert.AreEqual(1, j.CrossInBox);
        }

        [Test]
        public void CrossCorridorContactPushesTheLighterOne()
        {
            var sim = Quiet(Straight(400f));
            Corridor cross = CrossStreet(200f);
            sim.AddJunction(200f, cross, 100f);
            Agent bus = sim.SpawnPlayerBus(200f, 0f);              // sitting in the middle of the box
            bus.Speed = 0f;
            Agent rickshaw = sim.SpawnVehicle(cross, VehicleClass.Rickshaw, 100f, 0f, 0.5f);   // same spot, on the cross street
            rickshaw.Speed = 0f; rickshaw.DesiredSpeed = 0f;
            Vector3 before = rickshaw.Position;
            sim.Step(1f / 60f);
            Assert.Greater(Vector3.Distance(before, rickshaw.Position), 0.5f, "shoved out of the bus");
            Assert.AreEqual(1, sim.Metrics.Contacts);
        }
    }
}
