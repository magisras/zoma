using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    /// <summary>The bus as docs/BUS.md describes it: air brakes that lag, and air that has to be made.</summary>
    public class BusTests
    {
        private static TrafficSim Road()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            var road = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 2000) }, 10f);
            return new TrafficSim(road, tuning, 3);
        }

        /// <summary>Speed lost over a hard stop from 10 m/s after the given seconds, with full air and new brakes.</summary>
        private static float DecelAfter(float seconds, float airAtStart, float wear = 0f)
        {
            var sim = Road();
            Agent bus = sim.SpawnPlayerBus(100f, -2f);
            bus.Speed = 10f;
            sim.Bus.AirPressure = airAtStart;
            sim.Bus.BrakeWear = wear; sim.Condition.BrakeWear = wear;
            sim.Bus.Brake = 1f;
            float before = bus.Speed;
            for (float t = 0; t < seconds; t += 1f / 60f) sim.Step(1f / 60f);
            return (before - bus.Speed) / seconds;
        }

        [Test]
        public void TheDrumsFollowThePedalWithALag()
        {
            float early = DecelAfter(0.1f, 1f);                  // the first tenth of a second
            float later = DecelAfter(1.0f, 1f);                  // a full second, mostly at full bite
            Assert.Less(early, later * 0.6f, "the brakes bite late: " + early + " vs " + later);
            Assert.Greater(later, 3.5f, "and then they bite hard");
        }

        [Test]
        public void NoAirMeansSoftBrakesUntilTheCompressorCatchesUp()
        {
            float lowAir = DecelAfter(1.0f, 0.4f);
            float fullAir = DecelAfter(1.0f, 1f);
            Assert.Less(lowAir, fullAir * 0.6f, "the first stop after a cold start is soft");

            var sim = Road();
            Agent bus = sim.SpawnPlayerBus(100f, -2f);
            Assert.AreEqual(sim.Tuning.Bus.AirPressureAtDayStart, sim.Bus.AirPressure, 1e-3f, "the day starts with little air");
            for (float t = 0; t < 60f; t += 1f / 60f) sim.Step(1f / 60f);   // idling, no brake
            Assert.AreEqual(1f, sim.Bus.AirPressure, 1e-3f, "a minute of running fills the tanks");
        }

        [Test]
        public void PumpingTheBrakeSpendsAirHoldingItDoesNot()
        {
            var held = Road();
            held.SpawnPlayerBus(100f, -2f);
            held.Bus.AirPressure = 1f;
            held.Bus.Brake = 1f;
            for (float t = 0; t < 30f; t += 1f / 60f) held.Step(1f / 60f);   // standing on the pedal for half a minute
            Assert.AreEqual(1f, held.Bus.AirPressure, 0.05f, "a held pedal spends one application, which the compressor covers");

            var pumped = Road();
            pumped.SpawnPlayerBus(100f, -2f);
            pumped.Bus.AirPressure = 1f;
            for (int i = 0; i < 30; i++)                                       // thirty full applications in 30 s
            {
                pumped.Bus.Brake = 1f; for (int k = 0; k < 30; k++) pumped.Step(1f / 60f);
                pumped.Bus.Brake = 0f; for (int k = 0; k < 30; k++) pumped.Step(1f / 60f);
            }
            float expected = 1f + 30f / pumped.Tuning.Bus.AirBuildSeconds - 30f * pumped.Tuning.Bus.AirPerApplication;
            Assert.Less(pumped.Bus.AirPressure, 0.9f, "pumping in a jam empties the tanks faster than the compressor fills them");
            Assert.AreEqual(Mathf.Clamp01(expected), pumped.Bus.AirPressure, 0.08f);
        }
    }
}
