using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    public class RolloverTests
    {
        private static TrafficSim Road()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            var road = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 3000) }, 10f);
            var sim = new TrafficSim(road, tuning, 8);
            sim.SpawnPlayerBus(100f, -2f);
            return sim;
        }

        private static void Run(TrafficSim sim, float seconds)
        {
            for (float t = 0; t < seconds; t += 1f / 60f) sim.Step(1f / 60f);
        }

        [Test]
        public void LeavingTheRoadAtSpeedTipsTheBusSlowlyDoesNot()
        {
            var slow = Road();
            slow.Player.Speed = 4f;
            slow.Bus.Throttle = 0.3f;
            slow.Bus.Steer = -1f;                               // hard left, slowly, onto the pavement
            Run(slow, 6f);
            Assert.IsFalse(slow.Rollover.Active, "a crawl onto the kerb is a scrape, not a rollover");

            var fast = Road();
            fast.Player.Speed = 12f;
            fast.Bus.Throttle = 1f;
            fast.Bus.Steer = -0.25f;                            // a drift, asleep at the wheel
            Run(fast, 6f);
            Assert.IsTrue(fast.Rollover.Active, "left the road at speed");
            Assert.IsTrue(fast.Rollover.Pending);
            Assert.IsTrue(fast.Player.Rolled);
            Assert.AreEqual(0f, fast.Player.Speed);
            Assert.AreEqual(0, fast.Player.Load.Count, "everyone got out");
            Assert.AreEqual(Mathf.RoundToInt(30 * 0.3f), fast.Rollover.HurtPassengers);
            Assert.IsTrue(fast.Condition.WindscreenCracked);
        }

        [Test]
        public void FullLockAtCitySpeedScrubsTheTyresAndDoesNotTip()
        {
            // Owner, 3 Oct 2026: real buses take far steeper manoeuvres than the sandbox allowed and never
            // roll unless something extreme happens. On tarmac the tyres give before the body does.
            var sim = Road();
            sim.Tuning.Bus.OffRoadRolloverMetres = 99f;         // a wide field: the turn alone is on trial
            sim.Player.Speed = 8f;                              // 29 km/h
            sim.Bus.Throttle = 1f;
            sim.Bus.Steer = 1f;                                 // full lock, held
            Run(sim, 4f);
            Assert.IsFalse(sim.Rollover.Active, "a hard turn at city speed is a plough, not a rollover");
            float lateral = Mathf.Abs(sim.Player.Speed * sim.Bus.LastYawRate);
            Assert.LessOrEqual(lateral, sim.Tuning.Bus.TyreGripMs2 + 0.1f, "the tyres cap what the wheel asks for");
        }

        [Test]
        public void ASustainedSwerveAtSpeedStillTipsIt()
        {
            var sim = Road();
            sim.Tuning.Bus.OffRoadRolloverMetres = 99f;
            sim.Player.Speed = 16f;                             // 58 km/h
            sim.Bus.Throttle = 1f;
            sim.Bus.Steer = 1f;                                 // full lock, held for seconds: the extreme case
            Run(sim, 4f);
            Assert.IsTrue(sim.Rollover.Active);
            StringAssert.Contains("too hard", sim.Rollover.Cause);
        }

        [Test]
        public void PayingForTheRopesCostsAndEventuallyRightsTheBus()
        {
            var sim = Road();
            sim.Tuning.Economy.RightingSeconds = 5f;
            Rollover.Tip(sim, "test");
            Rollover.Answer(sim, true);
            Assert.AreEqual(sim.Tuning.Economy.RopesTk, sim.Economy.Ledger.RopesTk, 1e-3f);
            Run(sim, 3f);
            Assert.IsTrue(sim.Rollover.Active, "still on its side");
            Assert.IsTrue(sim.Bus.Held);
            Run(sim, 3f);
            Assert.IsFalse(sim.Rollover.Active, "back on its wheels");
            Assert.IsFalse(sim.Player.Rolled);
            Assert.IsFalse(sim.Bus.Held);
            Assert.IsFalse(sim.Economy.DayOver, "straight back into service");
            Assert.IsTrue(sim.Condition.DoorBent);
        }

        [Test]
        public void WalkingAwayEndsTheDayWithTheZomaStillOwed()
        {
            var sim = Road();
            sim.Player.Load.FaresTk = 400f;
            Rollover.Tip(sim, "test");
            Rollover.Answer(sim, false);
            Assert.IsTrue(sim.Economy.DayOver);
            Assert.IsTrue(sim.Economy.WalkedAway);
            Assert.AreEqual(sim.Tuning.Economy.ZomaTk * sim.Tuning.Economy.MoneyScale, sim.Economy.Ledger.ZomaTk);
            Assert.Less(sim.Economy.Ledger.CrewNetTk, 400f - sim.Economy.Ledger.ZomaTk + 1f);
        }

        [Test]
        public void TheCrewCarriesItsInjuriesIntoTheNextShifts()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            var h = new Household();
            h.NoteRollover(false, tuning.Economy);
            h.StartNextWorkDay();
            Assert.AreEqual(tuning.Economy.CrewInjuryDays, h.CrewInjuredDays);
            var f = new FatigueState { Level = 0.5f, Injured = true };
            Assert.AreEqual(0.5f * tuning.Fatigue.ReactionDelayMaxSeconds * tuning.Fatigue.InjuryReactionFactor, f.ReactionDelay(tuning.Fatigue), 1e-5f);
        }
    }
}
