using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    /// <summary>
    /// The simulation core runs without a scene, so these tests drive whole situations: a rickshaw
    /// in front of a bus, a horn, a pedestrian stepping out. If one of these breaks, the feel broke.
    /// </summary>
    public class CoreTests
    {
        private static Corridor StraightRoad(float length = 1000f, float width = 10f)
        {
            return new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, length) }, width);
        }

        private static TrafficSim NewSim(Corridor corridor = null, int seed = 1)
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;        // tests place their own agents
            tuning.Spawn.PedestriansAround = 0;
            return new TrafficSim(corridor ?? StraightRoad(), tuning, seed);
        }

        private static void Run(TrafficSim sim, float seconds, float dt = 1f / 60f)
        {
            for (float t = 0; t < seconds; t += dt) sim.Step(dt);
        }

        [Test]
        public void CorridorProjectionRoundTrips()
        {
            var corridor = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 100), new Vector3(100, 0, 200) }, 10f);
            Vector3 world = corridor.PositionAt(150f, 2f);
            corridor.Project(world, out float s, out float lateral);
            Assert.AreEqual(150f, s, 0.01f);
            Assert.AreEqual(2f, lateral, 0.01f);
            Assert.AreEqual(100f + Mathf.Sqrt(2f) * 100f, corridor.Length, 0.01f);
        }

        [Test]
        public void PositiveLateralIsToTheRightOfTravel()
        {
            // Travelling along +Z, right is +X (Unity's left-handed frame).
            var corridor = StraightRoad();
            Vector3 p = corridor.PositionAt(10f, 3f);
            Assert.AreEqual(3f, p.x, 1e-4f);
            Assert.AreEqual(10f, p.z, 1e-4f);
        }

        [Test]
        public void FollowerBrakesForSlowVehicleAhead()
        {
            var sim = NewSim();
            Agent slow = sim.SpawnVehicle(VehicleClass.Rickshaw, 60f, 0f, 0.5f);
            slow.DesiredSpeed = 2f;
            Agent car = sim.SpawnVehicle(VehicleClass.Car, 20f, 0f, 0.0f);   // nerve 0 = widest gap
            car.DesiredSpeed = 13f;
            // Lock the car into the rickshaw's band so it cannot swerve round: a one-car-wide road.
            var narrow = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 1000) }, 2.4f);
            sim = new TrafficSim(narrow, sim.Tuning, 1);
            slow = sim.SpawnVehicle(VehicleClass.Rickshaw, 60f, 0f, 0.5f);
            slow.DesiredSpeed = 2f;
            car = sim.SpawnVehicle(VehicleClass.Car, 20f, 0f, 0.0f);
            car.DesiredSpeed = 13f;

            Run(sim, 20f);

            Assert.Less(car.S, slow.S, "the car must stay behind");
            Assert.Greater(slow.Rear - car.Front, 0f, "no overlap");
            Assert.AreEqual(slow.Speed, car.Speed, 0.5f, "it settles to the rickshaw's speed");
            // gap = floor + speed × headway (nerve 0 → 1.5 s)
            float expectedGap = sim.Tuning.Gap.FollowDistanceFloorMetres + car.Speed * 1.5f;
            Assert.AreEqual(expectedGap, slow.Rear - car.Front, 1.0f);
        }

        [Test]
        public void BlockedDriverSwervesIntoFreeBand()
        {
            var sim = NewSim();
            Agent parked = sim.SpawnVehicle(VehicleClass.Truck, 60f, 0f, 0.5f);
            parked.DesiredSpeed = 0f;
            parked.Speed = 0f;
            Agent car = sim.SpawnVehicle(VehicleClass.Car, 10f, 0f, 0.9f);
            car.DesiredSpeed = 13f;

            Run(sim, 15f);

            Assert.Greater(car.S, parked.S + 10f, "the car got past the parked truck");
            Assert.Greater(Mathf.Abs(car.Lateral), 1.5f, "by moving sideways, not through it");
        }

        [Test]
        public void BusHornMovesRickshawButRickshawHornDoesNotMoveBus()
        {
            // Many trials so the probabilistic rule shows its shape.
            int rickshawMoved = 0, busMoved = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                var sim = NewSim(seed: seed);
                Agent bus = sim.SpawnVehicle(VehicleClass.Bus, 20f, 0f, 0.5f);
                Agent rickshaw = sim.SpawnVehicle(VehicleClass.Rickshaw, 40f, 0f, 0.5f);
                HornSystem.Broadcast(sim, bus, sim.Tuning.Horn.TapStrength);
                if (rickshaw.IsYielding) rickshawMoved++;

                var sim2 = NewSim(seed: seed);
                Agent rickshaw2 = sim2.SpawnVehicle(VehicleClass.Rickshaw, 20f, 0f, 0.5f);
                Agent bus2 = sim2.SpawnVehicle(VehicleClass.Bus, 40f, 0f, 0.5f);
                HornSystem.Broadcast(sim2, rickshaw2, sim2.Tuning.Horn.TapStrength);
                if (bus2.IsYielding) busMoved++;
            }
            Assert.GreaterOrEqual(rickshawMoved, 36, "a bus tap almost always moves a rickshaw");
            Assert.LessOrEqual(busMoved, 2, "a rickshaw tap almost never moves a bus");
        }

        [Test]
        public void HornOutsideTheConeDoesNothing()
        {
            var sim = NewSim();
            Agent bus = sim.SpawnVehicle(VehicleClass.Bus, 40f, 0f, 0.5f);
            Agent behind = sim.SpawnVehicle(VehicleClass.Rickshaw, 20f, 0f, 0.5f);   // behind the bus
            HornSystem.Broadcast(sim, bus, sim.Tuning.Horn.BlastStrength);
            Assert.IsFalse(behind.IsYielding);
        }

        [Test]
        public void YieldMovesAwayFromTheHeavierVehicle()
        {
            var sim = NewSim();
            Agent bus = sim.SpawnVehicle(VehicleClass.Bus, 20f, -1f, 0.5f);         // bus slightly left
            Agent rickshaw = sim.SpawnVehicle(VehicleClass.Rickshaw, 40f, 0f, 0.5f);
            Steering.Yield(rickshaw, bus, 3f, sim.Corridor);
            Assert.Greater(rickshaw.TargetLateral, rickshaw.Lateral, "bus on the left → rickshaw goes right");
            Assert.LessOrEqual(rickshaw.TargetLateral, sim.Corridor.HalfWidth - rickshaw.HalfWidth, "but stays on the road");
        }

        [Test]
        public void PedestrianWaitsForBusButStepsOutForCar()
        {
            // A vehicle 30 m away at 10 m/s is 3 s away. A pedestrian demands 1.5 s of a car (×0.6 = 0.9 s)
            // and 1.5 + 1.0 = 2.5 s of a bus... both would cross. Bring it closer: 20 m → 2 s.
            var simCar = NewSim();
            Agent car = simCar.SpawnVehicle(VehicleClass.Car, 0f, 0f, 0.5f);
            car.Speed = 10f; car.DesiredSpeed = 10f;
            Agent pedA = simCar.SpawnPedestrian(20f + car.HalfLength, -1);
            pedA.WaitTimer = 0f;
            simCar.Step(1f / 60f);
            Assert.AreEqual(PedestrianState.Crossing, pedA.PedState, "hand up, steps out in front of the car");

            var simBus = NewSim();
            Agent bus = simBus.SpawnVehicle(VehicleClass.Bus, 0f, 0f, 0.5f);
            bus.Speed = 10f; bus.DesiredSpeed = 10f;
            Agent pedB = simBus.SpawnPedestrian(20f + bus.HalfLength, -1);
            pedB.WaitTimer = 0f;
            simBus.Step(1f / 60f);
            Assert.AreEqual(PedestrianState.Waiting, pedB.PedState, "hesitates for the bus");
        }

        [Test]
        public void HittingAPersonEndsTheDay()
        {
            var sim = NewSim();
            sim.SpawnPlayerBus(10f, 0f);
            sim.Bus.Throttle = 1f;
            Agent ped = sim.SpawnPedestrian(40f, -1);
            ped.PedState = PedestrianState.Crossing;
            ped.CrossDirection = 1f;
            ped.Speed = 0f;                 // frozen in the road, directly in the bus's path
            ped.Lateral = 0f;
            ped.WaitTimer = 999f;

            Run(sim, 15f);

            Assert.IsTrue(sim.Metrics.PersonHit);
            float frozenTime = sim.Metrics.Time;
            sim.Step(1f);
            Assert.AreEqual(frozenTime, sim.Metrics.Time, "nothing moves after the day ends");
        }

        private static float StoppingDistance(float wear)
        {
            var sim = NewSim();
            sim.SpawnPlayerBus(10f, 0f);
            sim.Bus.BrakeWear = wear;
            sim.Player.Speed = 15f;
            sim.Bus.Brake = 1f;
            float start = sim.Player.S;
            Run(sim, 20f);
            return sim.Player.S - start;
        }

        [Test]
        public void WornBrakesStopLater()
        {
            float fresh = StoppingDistance(0f);
            float worn = StoppingDistance(1f);
            Assert.Greater(worn, fresh * 1.8f, "fully worn brakes keep 40% of the bite, so stopping takes far longer");
        }

        private static float SpeedAfterTenSeconds(int passengers)
        {
            var sim = NewSim();
            sim.SpawnPlayerBus(10f, 0f);
            sim.Bus.Passengers = passengers;
            sim.Bus.Throttle = 1f;
            Run(sim, 10f);
            return sim.Player.Speed;
        }

        [Test]
        public void FullBusAcceleratesSlower()
        {
            Assert.Greater(SpeedAfterTenSeconds(0), SpeedAfterTenSeconds(90));
        }

        private static float Snapshot(int seed)
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            var sim = new TrafficSim(StraightRoad(), tuning, seed);
            sim.SpawnPlayerBus(20f, 0f);
            sim.Bus.Throttle = 0.7f;
            Run(sim, 10f);
            float sum = 0f;
            foreach (Agent a in sim.Agents) sum += a.S * 1.3f + a.Lateral;
            return sum;
        }

        [Test]
        public void SameSeedSameResult()
        {
            Assert.AreEqual(Snapshot(7), Snapshot(7), 1e-3f);
            Assert.AreNotEqual(Snapshot(7), Snapshot(8));
        }

        [Test]
        public void PopulationIsMaintainedAroundThePlayer()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            var sim = new TrafficSim(StraightRoad(3000f), tuning, 3);
            sim.SpawnPlayerBus(500f, 0f);
            Run(sim, 5f);
            int vehicles = 0, pedestrians = 0;
            foreach (Agent a in sim.Agents)
            {
                if (a.IsPlayer) continue;
                if (a.IsPedestrian) pedestrians++; else vehicles++;
                Assert.Less(a.S, 500f + tuning.Spawn.SpawnAheadMetres + 160f);
            }
            Assert.GreaterOrEqual(vehicles, tuning.Spawn.VehiclesAround * 0.8f);
            Assert.GreaterOrEqual(pedestrians, tuning.Spawn.PedestriansAround * 0.8f);
        }
    }
}
