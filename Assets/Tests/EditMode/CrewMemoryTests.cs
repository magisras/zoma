using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    /// <summary>RESEARCH.md: own-company crews are persistent, with memory. The household carries it between days.</summary>
    public class CrewMemoryTests
    {
        private static TrafficSim World(int seed = 3)
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            var road = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 1000) }, 10f);
            var sim = new TrafficSim(road, tuning, seed);
            sim.AddZone("Stand", 50f, true);
            sim.SpawnPlayerBus(100f, -2f);
            sim.SpawnRivalBus("Jamal", DriverPersonality.Spiteful(), 200f, -2f);
            sim.SpawnRivalBus("Rafiq", DriverPersonality.Reckless(), 300f, -2f);
            return sim;
        }

        private static Agent Crew(TrafficSim sim, string name)
        {
            foreach (Agent a in sim.Agents) if (a.Brain != null && a.Brain.CrewName == name) return a;
            return null;
        }

        [Test]
        public void AGrudgeComesBackTheNextMorningOneNightCooler()
        {
            var h = new Household();
            var day1 = World();
            Crew(day1, "Jamal").Brain.Grudge = 3;
            Crew(day1, "Rafiq").Brain.Grudge = -2;             // trust: you let him through twice
            day1.RememberCrews(h);
            h.StartNextWorkDay();                              // day 2

            var day2 = World();
            day2.RecallCrews(h);
            Assert.AreEqual(2, Crew(day2, "Jamal").Brain.Grudge, "one night: one point cooler");
            Assert.AreEqual(-1, Crew(day2, "Rafiq").Brain.Grudge, "trust cools the same way");
        }

        [Test]
        public void WithNoDecayTheGrudgeIsExactlyWhatItWas()
        {
            var h = new Household();
            var day1 = World();
            day1.Tuning.Memory.GrudgeDecayPerShift = 0;
            Crew(day1, "Jamal").Brain.Grudge = 3;
            day1.RememberCrews(h);
            h.StartNextWorkDay();
            var day2 = World();
            day2.Tuning.Memory.GrudgeDecayPerShift = 0;
            day2.RecallCrews(h);
            Assert.AreEqual(3, Crew(day2, "Jamal").Brain.Grudge);
        }

        [Test]
        public void ARestDayIsANightTooAndNothingCoolsPastZero()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            var h = new Household();
            h.RememberCrew("Jamal", 1);
            h.RememberCrew("Rafiq", 5);
            h.RestDay(tuning.Fatigue, tuning.Economy);         // day 2, off
            h.StartNextWorkDay();                              // day 3
            Assert.AreEqual(0, h.RecallCrew("Jamal", tuning.Memory), "two nights on a grudge of one: forgotten, not reversed");
            Assert.AreEqual(3, h.RecallCrew("Rafiq", tuning.Memory));
        }

        [Test]
        public void ANewGameHoldsNothingAndTheSameDayHoldsEverything()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            var fresh = new Household();
            Assert.AreEqual(0, fresh.RecallCrew("Jamal", tuning.Memory), "never met");

            fresh.RememberCrew("Jamal", 4);
            Assert.AreEqual(4, fresh.RecallCrew("Jamal", tuning.Memory), "a restart on the same day keeps it");
        }
    }
}
