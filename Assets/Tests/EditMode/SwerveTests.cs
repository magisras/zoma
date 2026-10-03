using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    /// <summary>A yield to the horn is driven, not slid: the car swerves over a second or two and its nose follows.</summary>
    public class SwerveTests
    {
        private static TrafficSim Road()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            var road = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 2000) }, 10f);
            return new TrafficSim(road, tuning, 5);
        }

        [Test]
        public void AMovingCarSwervesAsideGraduallyAndItsNoseTurnsWithIt()
        {
            var sim = Road();
            Agent car = sim.SpawnVehicle(VehicleClass.Car, 100f, 0f, 0.5f);
            car.Speed = 8f; car.DesiredSpeed = 8f;
            for (int i = 0; i < 30; i++) sim.Step(1f / 60f);              // settle
            car.TargetLateral = 3f;                                         // what Yield does
            float maxLateralStep = 0f, maxYawStep = 0f, lastLateral = car.Lateral, lastYaw = car.Yaw;
            float t = 0f;
            for (; t < 4f && Mathf.Abs(car.Lateral - 3f) > 0.05f; t += 1f / 60f)
            {
                sim.Step(1f / 60f);
                maxLateralStep = Mathf.Max(maxLateralStep, Mathf.Abs(car.Lateral - lastLateral));
                maxYawStep = Mathf.Max(maxYawStep, Mathf.Abs(Mathf.DeltaAngle(lastYaw * Mathf.Rad2Deg, car.Yaw * Mathf.Rad2Deg)));
                lastLateral = car.Lateral; lastYaw = car.Yaw;
            }
            Assert.Less(t, 4f, "it gets there");
            Assert.Greater(t, 1.0f, "but not in a flash");
            Assert.Less(maxLateralStep, 0.06f, "no sideways jump in any frame (metres per 1/60 s)");
            Assert.Less(maxYawStep, 1.5f, "the nose turns smoothly (degrees per frame)");
        }

        [Test]
        public void AStandingCarCannotSlideAside()
        {
            var sim = Road();
            Agent car = sim.SpawnVehicle(VehicleClass.Car, 100f, 0f, 0.5f);
            car.Speed = 0f; car.DesiredSpeed = 0f;
            car.TargetLateral = 3f;
            for (int i = 0; i < 120; i++) sim.Step(1f / 60f);
            Assert.Less(Mathf.Abs(car.Lateral), 0.2f, "two seconds standing still: it barely moved");
        }

        [Test]
        public void APedestrianCrossesInFrontOfABusThatHasStoppedForThem()
        {
            // The standoff: she waits because the bus might pull away; the bus waits because she is in its band.
            var sim = Road();
            Agent bus = sim.SpawnVehicle(VehicleClass.Bus, 100f, -2f, 0.5f);
            bus.Speed = 0f; bus.DesiredSpeed = 8f;
            Agent p = sim.SpawnPedestrian(100f + bus.HalfLength + 1.5f, -1);   // 1.5 m past the nose...
            p.Lateral = -2.5f; p.PedState = PedestrianState.Crossing; p.CrossDirection = 1f;   // ...already in its band
            for (int i = 0; i < 60 * 8; i++) sim.Step(1f / 60f);
            Assert.Greater(p.Lateral, 2f, "she crossed the bus's band");
            Assert.Greater(bus.Speed, 1f, "and the bus went on once she was clear");
            Assert.AreEqual(0, sim.Metrics.NpcPersonHits);
        }

        [Test]
        public void PeopleAtTheKerbStepBackFromAnOverhangingBusAndReturn()
        {
            var sim = Road();
            Agent p = sim.SpawnPedestrian(130f, -1);
            p.WaitTimer = 999f;                                            // not crossing today
            float kerb = Mathf.Abs(p.Lateral);
            Agent bus = sim.SpawnVehicle(VehicleClass.Bus, 100f, -4.5f, 0.5f);   // side at -5.75: overhanging the kerb line
            bus.Speed = 7f; bus.DesiredSpeed = 7f; bus.TargetLateral = -4.5f;
            for (int i = 0; i < 60; i++) sim.Step(1f / 60f);              // one second: the bus is 23 m away
            Assert.Greater(Mathf.Abs(p.Lateral), kerb + 0.5f, "she stepped back");
            for (int i = 0; i < 60 * 5; i++) sim.Step(1f / 60f);         // the bus has passed
            Assert.AreEqual(0, sim.Metrics.NpcPersonHits, "nobody was clipped");
            for (int i = 0; i < 60 * 3; i++) sim.Step(1f / 60f);
            Assert.AreEqual(kerb, Mathf.Abs(p.Lateral), 0.05f, "and came back to the kerb");
        }

        [Test]
        public void AScrapePushesApartOverFramesNotAtOnce()
        {
            var sim = Road();
            Agent truck = sim.SpawnVehicle(VehicleClass.Truck, 100f, 0f, 0.5f);
            Agent car = sim.SpawnVehicle(VehicleClass.Car, 100f, 1.5f, 0.5f);         // overlapping by ~0.6 m sideways
            truck.Speed = car.Speed = 0f; truck.DesiredSpeed = car.DesiredSpeed = 0f;
            float before = car.Lateral;
            sim.Step(1f / 60f);
            Assert.Less(Mathf.Abs(car.Lateral - before), 0.05f, "one frame: a nudge");
            for (int i = 0; i < 90; i++) sim.Step(1f / 60f);
            Assert.GreaterOrEqual(Mathf.Abs(car.Lateral - truck.Lateral), truck.HalfWidth + car.HalfWidth - 0.05f, "a second and a half later: apart");
        }
    }
}
