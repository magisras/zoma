using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    public class ConditionTests
    {
        private static TrafficSim Road()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            var road = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 3000) }, 10f);
            var sim = new TrafficSim(road, tuning, 6);
            sim.SpawnPlayerBus(10f, -2f);
            return sim;
        }

        private static void Run(TrafficSim sim, float seconds)
        {
            for (float t = 0; t < seconds; t += 1f / 60f) sim.Step(1f / 60f);
        }

        [Test]
        public void BrakingWearsThePadsCoastingDoesNot()
        {
            var sim = Road();
            float start = sim.Condition.BrakeWear;
            sim.Player.Speed = 10f;
            sim.Bus.Throttle = 0f;
            Run(sim, 5f);                                 // coasting
            Assert.AreEqual(start, sim.Condition.BrakeWear, 1e-6f);
            sim.Player.Speed = 10f;
            sim.Bus.Brake = 1f;
            Run(sim, 5f);                                 // one hard stop from 36 km/h
            float added = sim.Condition.BrakeWear - start;
            float scale = 1f / sim.Tuning.Economy.MoneyScale;                 // the short day stands for a whole one
            Assert.Greater(added, 0.001f * scale);
            Assert.Less(added, 0.004f * scale, "about 0.002 per real stop, so a racing day of 150 stops adds ~0.3");
            Assert.AreEqual(added, sim.Condition.BrakeWearToday, 1e-6f);
            Assert.AreEqual(sim.Condition.BrakeWear, sim.Bus.BrakeWear, 1e-6f, "the controller fades with it");
        }

        [Test]
        public void ServiceResetsWearAndCostsPapersBuyDays()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            var h = new Household { SavingsTk = 1000f };
            h.Bus.BrakeWear = 0.8f;
            h.ServiceBrakes(tuning.Bus, tuning.Economy);
            Assert.AreEqual(tuning.Bus.WearAfterService, h.Bus.BrakeWear, 1e-6f);
            Assert.AreEqual(1000f - tuning.Economy.BrakeServiceTk * tuning.Economy.MoneyScale, h.SavingsTk, 1e-3f);

            Assert.IsFalse(h.Bus.PapersValid(h.Day));
            h.BuyPapers(tuning.Economy);
            Assert.IsTrue(h.Bus.PapersValid(h.Day));
            Assert.IsTrue(h.Bus.PapersValid(h.Day + tuning.Economy.FitnessDays));
            Assert.IsFalse(h.Bus.PapersValid(h.Day + tuning.Economy.FitnessDays + 1));
        }

        [Test]
        public void CleanPapersMeanFewerPapersStops()
        {
            // Make the base chance certain; papers at factor 0 must then prevent every 'papers' stop.
            var sim = Road();
            var cross = new Corridor(new List<Vector3> { new Vector3(-100, 0, 500), new Vector3(100, 0, 500) }, 8f, false, "cross");
            Junction j = sim.AddJunction(500f, cross, 100f);
            j.Open = JunctionFlow.Main; j.Timer = 9999f;
            sim.Tuning.Officer.SergeantStopChance = 1f;
            sim.Tuning.Economy.SergeantChancePerPassFactor = 1f;
            sim.Tuning.Economy.PapersSergeantFactor = 0f;
            sim.Condition.PapersValidUntilDay = 99;           // clean papers
            sim.Player.Speed = 10f;
            sim.Bus.Throttle = 0.8f;
            Run(sim, 70f);                                    // well past the junction
            Assert.IsFalse(sim.Economy.Sergeant.Active, "nothing to point at");
            Assert.AreEqual(0f, sim.Economy.Ledger.SergeantTk);

            var sim2 = Road();
            var cross2 = new Corridor(new List<Vector3> { new Vector3(-100, 0, 500), new Vector3(100, 0, 500) }, 8f, false, "cross");
            Junction j2 = sim2.AddJunction(500f, cross2, 100f);
            j2.Open = JunctionFlow.Main; j2.Timer = 9999f;
            sim2.Tuning.Officer.SergeantStopChance = 1f;
            sim2.Tuning.Economy.SergeantChancePerPassFactor = 1f;
            sim2.Player.Speed = 10f;
            sim2.Bus.Throttle = 0.8f;
            Run(sim2, 70f);
            Assert.IsTrue(sim2.Economy.Sergeant.Active, "no papers: he steps out");
            Assert.AreEqual("papers", sim2.Economy.Sergeant.Reason);
        }

        [Test]
        public void ScrapesLeaveDents()
        {
            var sim = Road();
            sim.Player.Speed = 8f;
            sim.Bus.Throttle = 1f;
            Agent rickshaw = sim.SpawnVehicle(VehicleClass.Rickshaw, 22f, -2f, 0f);
            rickshaw.DesiredSpeed = 0.5f; rickshaw.Speed = 0.5f;
            Run(sim, 4f);
            Assert.GreaterOrEqual(sim.Condition.Dents, 1);
            Assert.AreEqual(sim.Metrics.Contacts, sim.Condition.Dents);
        }
    }
}
