using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    public class StreetControlTests
    {
        private static TrafficSim Road(int seed = 12)
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            tuning.Spawn.CrossVehiclesPerJunction = 0;
            tuning.Economy.DriveDayChance = 0f;
            var road = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 2000) }, 10f);
            return new TrafficSim(road, tuning, seed);
        }

        private static Junction CrossAt(TrafficSim sim, float z)
        {
            var cross = new Corridor(new List<Vector3> { new Vector3(-100, 0, z), new Vector3(100, 0, z) }, 8f, false, "cross");
            return sim.AddJunction(z, cross, 100f);
        }

        private static void Run(TrafficSim sim, float seconds)
        {
            for (float t = 0; t < seconds; t += 1f / 60f) sim.Step(1f / 60f);
        }

        [Test]
        public void TheRopeStopsLeakersAndThePlayerAlike()
        {
            var sim = Road();
            Junction j = CrossAt(sim, 300f);
            j.Open = JunctionFlow.Cross; j.Timer = 9999f; j.Roped = true; j.LeakersLeft = 0;
            Agent car = sim.SpawnVehicle(VehicleClass.Car, 250f, 2f, 0.9f);
            car.Speed = 10f; car.DesiredSpeed = 10f;
            Agent bus = sim.SpawnPlayerBus(240f, -2f);
            bus.Speed = 10f;
            sim.Bus.Throttle = 1f;                               // the player tries to run it
            Run(sim, 12f);
            float line = j.StopLineOn(sim.Corridor, sim.Tuning.Officer.StopLineSetbackMetres);
            Assert.Less(car.S + car.HalfLength, line + 0.5f, "no leaking through a rope");
            Assert.Less(bus.S + bus.HalfLength, line + 1.5f, "the bus is held at the rope");
            Assert.Less(bus.Speed, 0.5f);
            Assert.AreEqual(0, sim.Metrics.CaneRuns);

            j.Roped = false; j.Open = JunctionFlow.Main;
            Run(sim, 8f);
            Assert.Greater(bus.S, 320f, "released when the cane turns");
        }

        [Test]
        public void SignalLampsChangeNothing()
        {
            foreach (SignalMode mode in new[] { SignalMode.Dark, SignalMode.Manual, SignalMode.Timer })
            {
                var sim = Road();
                Junction j = CrossAt(sim, 300f);
                j.Signal = mode; j.Open = JunctionFlow.Cross; j.Timer = 9999f; j.LeakersLeft = 0; j.Roped = false;
                Agent car = sim.SpawnVehicle(VehicleClass.Car, 200f, 0f, 0.5f);
                car.DesiredSpeed = 10f;
                Run(sim, 15f);
                float line = j.StopLineOn(sim.Corridor, sim.Tuning.Officer.StopLineSetbackMetres);
                Assert.Less(car.S + car.HalfLength, line + 0.5f, mode + ": the cane holds, the lamp is scenery");
            }
        }

        [Test]
        public void ASergeantOnDutyAtTheBoxStopsYouOffDutyDoesNot()
        {
            var sim = Road();
            sim.Tuning.Economy.CheckpointStopChance = 1f;
            sim.Tuning.Economy.SergeantOnDutyChance = 1f;
            Checkpoint cp = sim.AddCheckpoint("Mirpur 10 box", 300f);
            Assert.IsTrue(cp.SergeantOnDuty);
            Agent bus = sim.SpawnPlayerBus(250f, -2f);
            bus.Speed = 10f; sim.Bus.Throttle = 1f;
            Run(sim, 10f);
            Assert.IsTrue(sim.Economy.Sergeant.Active);
            StringAssert.Contains("Mirpur 10 box", sim.Economy.Sergeant.Reason);

            var quiet = Road();
            quiet.Tuning.Economy.CheckpointStopChance = 1f;
            Checkpoint off = quiet.AddCheckpoint("box", 300f);
            off.SergeantOnDuty = false;
            Agent bus2 = quiet.SpawnPlayerBus(250f, -2f);
            bus2.Speed = 10f; quiet.Bus.Throttle = 1f;
            Run(quiet, 10f);
            Assert.IsFalse(quiet.Economy.Sergeant.Active, "nobody at the box");
        }

        [Test]
        public void ADriveDayMultipliesTheChanceAndThePrice()
        {
            var sim = Road();
            sim.Economy.DriveDay = true;
            sim.Tuning.Economy.CheckpointStopChance = 0.3f;      // ×2.5 on a drive day = 0.75; dents and papers aside
            sim.Tuning.Economy.SergeantOnDutyChance = 1f;
            sim.Tuning.Economy.PapersSergeantFactor = 1f;
            int stopped = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                var s = Road(seed);
                s.Economy.DriveDay = true;
                s.Tuning.Economy.CheckpointStopChance = 0.3f;
                s.Tuning.Economy.SergeantOnDutyChance = 1f;
                s.AddCheckpoint("box", 300f);
                Agent bus = s.SpawnPlayerBus(250f, -2f);
                bus.Speed = 10f; s.Bus.Throttle = 1f;
                Run(s, 8f);
                if (s.Economy.Sergeant.Active)
                {
                    stopped++;
                    Assert.AreEqual(s.Tuning.Economy.SergeantDemandTk * s.Tuning.Economy.MoneyScale * 1.5f, s.Economy.Sergeant.DemandTk, 1e-3f, "he asks more on a drive day");
                }
            }
            Assert.Greater(stopped, 20, "well over the 30% base rate");
        }

        [Test]
        public void RefusingWithoutPapersCanSendTheBusToTheYard()
        {
            var sim = Road();
            sim.Tuning.Economy.SeizeChanceWithoutPapers = 1f;
            sim.SpawnPlayerBus(250f, -2f);
            sim.Economy.Sergeant.Active = true; sim.Economy.Sergeant.DemandTk = 30f; sim.Economy.Sergeant.Reason = "papers";
            sim.Economy.AnswerSergeant(false);
            Assert.IsTrue(sim.Economy.Seized);
            Assert.IsTrue(sim.Economy.DayOver);

            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            var h = new Household { SavingsTk = 500f };
            h.NoteSeizure(tuning.Economy);
            Assert.AreEqual(tuning.Economy.DumpingDays, h.BusInYardDays);
            h.YardDay(tuning.Fatigue, tuning.Economy);
            Assert.AreEqual(tuning.Economy.DumpingDays - 1, h.BusInYardDays);
            Assert.AreEqual(500f - tuning.Economy.FoodTkPerDay * tuning.Economy.MoneyScale, h.SavingsTk, 1e-3f);
        }

        [Test]
        public void WithPapersRefusingIsJustACase()
        {
            var sim = Road();
            sim.Tuning.Economy.SeizeChanceWithoutPapers = 1f;
            sim.SpawnPlayerBus(250f, -2f);
            sim.Condition.PapersValidUntilDay = 99;
            sim.Economy.Sergeant.Active = true; sim.Economy.Sergeant.DemandTk = 30f; sim.Economy.Sergeant.Reason = "papers";
            sim.Economy.AnswerSergeant(false);
            Assert.IsFalse(sim.Economy.Seized);
            Assert.IsFalse(sim.Economy.DayOver);
        }

        [Test]
        public void ACameraTurnsACaneRunIntoAnSmsToTheOwner()
        {
            var sim = Road();
            Junction j = CrossAt(sim, 300f);
            j.Open = JunctionFlow.Cross; j.Timer = 9999f; j.Camera = true; j.Roped = false;
            sim.Tuning.Economy.SergeantChanceAfterCaneRun = 0f;
            Agent bus = sim.SpawnPlayerBus(250f, -2f);
            bus.Speed = 10f; sim.Bus.Throttle = 1f;
            Run(sim, 10f);
            Assert.GreaterOrEqual(sim.Metrics.CaneRuns, 1);
            Assert.AreEqual(sim.Tuning.Economy.CameraFineTk * sim.Tuning.Economy.MoneyScale, sim.Economy.Ledger.CameraTk, 1e-3f);
            Assert.AreEqual(sim.Economy.Ledger.CameraTk, sim.Economy.Ledger.PaidOutTk - sim.Economy.Ledger.FuelTk, 1e-3f, "and it is in the day's equation");
        }

        [Test]
        public void PedestriansClusterAtJunctionsAndStands()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 60;
            tuning.Spawn.CrossVehiclesPerJunction = 0;
            var road = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 3000) }, 10f);
            var sim = new TrafficSim(road, tuning, 4);
            sim.AddZone("Stand", 700f, true);
            CrossAt(sim, 900f);
            sim.SpawnPlayerBus(600f, -2f);
            Run(sim, 1f);
            int near = 0, total = 0;
            foreach (Agent a in sim.Agents)
            {
                if (!a.IsPedestrian) continue;
                total++;
                if (Mathf.Abs(a.S - 700f) <= 40f || Mathf.Abs(a.S - 900f) <= 40f) near++;
            }
            Assert.GreaterOrEqual(total, 40);
            Assert.Greater(near / (float)total, 0.45f, "most of the crossing happens where the people are");
        }
    }
}
