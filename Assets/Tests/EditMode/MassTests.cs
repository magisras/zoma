using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    /// <summary>Owner, 3 Oct 2026: "I should be able to push lighter cars, rickshaws, bikes. What's the point of my twenty tons otherwise."</summary>
    public class MassTests
    {
        private static TrafficSim Road()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            var road = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 2000) }, 10f);
            return new TrafficSim(road, tuning, 11);
        }

        private static void Run(TrafficSim sim, float seconds)
        {
            for (float t = 0; t < seconds; t += 1f / 60f) sim.Step(1f / 60f);
        }

        [Test]
        public void ABusShovesARickshawAlongAndBarelySlows()
        {
            var sim = Road();
            Agent bus = sim.SpawnPlayerBus(100f, -2f);
            bus.Speed = 5f; sim.Bus.Throttle = 0.6f;
            Agent rickshaw = sim.SpawnVehicle(VehicleClass.Rickshaw, 112f, -2f, 0.5f);   // dawdling right in front of the nose
            rickshaw.Speed = 1f; rickshaw.DesiredSpeed = 1f;
            float startS = rickshaw.S;
            Run(sim, 3f);
            Assert.Greater(bus.Speed, 3.5f, "twenty tons do not stop for a rickshaw");
            Assert.Greater(rickshaw.S - startS, 8f, "the rickshaw went along in front of the nose, or got out of the way");
            Assert.GreaterOrEqual(sim.Metrics.Contacts, 1, "and it was a scrape, counted");
        }

        [Test]
        public void ABusIntoATruckIsTheOneThatSlows()
        {
            var sim = Road();
            Agent bus = sim.SpawnPlayerBus(100f, -2f);
            bus.Speed = 5f; sim.Bus.Throttle = 0.6f;
            Agent truck = sim.SpawnVehicle(VehicleClass.Truck, 114f, -2f, 0.5f);
            truck.Speed = 0.5f; truck.DesiredSpeed = 0.5f;
            Run(sim, 3f);
            Assert.Less(bus.Speed, 3f, "held near the truck's pace, the pair shoved along together");
        }
    }
}
