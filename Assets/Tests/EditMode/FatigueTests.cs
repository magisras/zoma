using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    public class FatigueTests
    {
        private static TrafficSim Quiet(int seed = 2)
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            var road = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 5000) }, 10f);
            var sim = new TrafficSim(road, tuning, seed);
            sim.SpawnPlayerBus(10f, -2f);
            return sim;
        }

        private static void Run(TrafficSim sim, float seconds, PlayerInput input)
        {
            for (float t = 0; t < seconds; t += 1f / 60f) { sim.PlayerInputs(input); sim.Step(1f / 60f); }
        }

        [Test]
        public void FatigueRisesOverTheShift()
        {
            var sim = Quiet();
            sim.Tuning.Economy.DayLengthSeconds = 100f;          // 14 shift hours in 100 s
            Run(sim, 50f, new PlayerInput());
            Assert.AreEqual(0.075f * 7f, sim.Fatigue.Level, 0.02f, "seven shift hours in");
        }

        [Test]
        public void TiredHandsReachTheWheelLate()
        {
            var fresh = Quiet();
            Run(fresh, 1f, new PlayerInput { Throttle = 1f });
            float freshSpeed = fresh.Player.Speed;

            var tired = Quiet();
            tired.Fatigue.Level = 1f;                            // 0.6 s of delay
            tired.Tuning.Fatigue.RisePerShiftHour = 0f;
            Run(tired, 1f, new PlayerInput { Throttle = 1f });
            Assert.Less(tired.Player.Speed, freshSpeed * 0.6f, "the throttle arrived 0.6 s late");
        }

        [Test]
        public void MicroSleepsOnlyWhenTiredAndFreezeTheHands()
        {
            var sim = Quiet();
            sim.Tuning.Fatigue.RisePerShiftHour = 0f;
            sim.Fatigue.Level = 0.3f;
            Run(sim, 60f, new PlayerInput { Throttle = 0.5f });
            Assert.AreEqual(0, sim.Fatigue.MicroSleeps, "fresh enough: none");

            sim.Fatigue.Level = 1f;
            // Hands on the throttle long enough for the 0.6 s delay to pass; then the eyes close;
            // then the hands move to the brake but nothing happens.
            sim.Tuning.Fatigue.MicroSleepChancePerSecond = 0f;
            Run(sim, 1.5f, new PlayerInput { Throttle = 1f });
            sim.Tuning.Fatigue.MicroSleepChancePerSecond = 1f;
            for (int i = 0; i < 600 && !sim.Fatigue.Asleep; i++) { sim.PlayerInputs(new PlayerInput { Throttle = 1f }); sim.Step(1f / 60f); }
            Assert.IsTrue(sim.Fatigue.Asleep);
            sim.PlayerInputs(new PlayerInput { Brake = 1f });
            sim.Step(1f / 60f);
            Assert.AreEqual(1f, sim.Bus.Throttle, "the frozen hands are still on the throttle");
            Assert.AreEqual(0f, sim.Bus.Brake);
            Assert.GreaterOrEqual(sim.Fatigue.MicroSleeps, 1);
        }

        [Test]
        public void BedRecoversMoreThanTheBusFloorAndCosts()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            var h1 = new Household();
            h1.Sleep(bed: true, fatigueAtEnd: 0.9f, f: tuning.Fatigue, e: tuning.Economy);
            var h2 = new Household();
            h2.Sleep(bed: false, fatigueAtEnd: 0.9f, f: tuning.Fatigue, e: tuning.Economy);
            Assert.Less(h1.FatigueCarried, h2.FatigueCarried);
            Assert.AreEqual(-tuning.Economy.BedTk * tuning.Economy.MoneyScale, h1.SavingsTk, 1e-3f);
            Assert.AreEqual(0f, h2.SavingsTk);
            Assert.AreEqual(Mathf.Max(0.35f, 0.9f * 0.6f), h2.FatigueCarried, 1e-4f);
        }

        [Test]
        public void ADayOffCostsFoodAndBringsTheBodyBack()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            var h = new Household { FatigueCarried = 0.5f, SavingsTk = 100f };
            DaySummary day = h.RestDay(tuning.Fatigue, tuning.Economy);
            Assert.IsFalse(day.Worked);
            Assert.AreEqual(2, h.Day);
            Assert.AreEqual(100f - tuning.Economy.FoodTkPerDay * tuning.Economy.MoneyScale, h.SavingsTk, 1e-3f);
            Assert.AreEqual(tuning.Fatigue.AfterRestDay, h.FatigueCarried, 1e-4f);
        }

        [Test]
        public void AWorkedDayNetsIntoSavings()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            var h = new Household();
            var ledger = new Ledger { FaresTk = 900f, ZomaTk = 300f, FuelTk = 50f };
            DaySummary day = h.CloseWorkedDay(ledger, tuning.Economy);
            Assert.AreEqual(550f, day.CrewNetTk, 1e-3f);
            Assert.AreEqual(550f - tuning.Economy.FoodTkPerDay * tuning.Economy.MoneyScale, h.SavingsTk, 1e-3f);
        }
    }
}
