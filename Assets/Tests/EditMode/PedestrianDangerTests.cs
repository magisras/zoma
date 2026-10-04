using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    /// <summary>
    /// Owner, 3 Oct 2026: "it's too easy to kill someone. Humans are not so unaware of the danger of a
    /// bus. At 20-40 km/h a person in the street sees me and moves away, especially when I horn."
    /// </summary>
    public class PedestrianDangerTests
    {
        private static TrafficSim Road()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            var road = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 2000) }, 10f);
            return new TrafficSim(road, tuning, 3);
        }

        private static void Run(TrafficSim sim, float seconds)
        {
            for (float t = 0; t < seconds; t += 1f / 60f) sim.Step(1f / 60f);
        }

        /// <summary>A person already in the road, in the bus's band, walking across.</summary>
        private static Agent Crosser(TrafficSim sim, float s, float lateral, float direction)
        {
            Agent p = sim.SpawnPedestrian(s, -1);
            p.PedState = PedestrianState.Crossing;
            p.CrossDirection = direction;
            p.Lateral = lateral;
            p.Position = sim.Corridor.PositionAt(p.S, p.Lateral);
            return p;
        }

        [Test]
        public void SomeoneInThePathOfABusAt30KmhRunsClearAndIsNotHit()
        {
            var sim = Road();
            Agent bus = sim.SpawnPlayerBus(100f, -2f);
            bus.Speed = 8f; sim.Bus.Throttle = 0.6f;                 // 29 km/h, holding it
            Agent p = Crosser(sim, 118f, -2.3f, +1f);                // 12 m ahead of the nose, in the band, walking right
            Run(sim, 4f);
            Assert.IsFalse(sim.Metrics.PersonHit, "not killed");
            Assert.AreEqual(0, sim.Metrics.PeopleKnockedDown, "not knocked down either: they ran");
            Assert.Greater(Mathf.Abs(p.Lateral - (-2.3f)), 1.5f, "they got out of the band");
        }

        [Test]
        public void TheHornMakesACrosserRun()
        {
            var sim = Road();
            Agent bus = sim.SpawnPlayerBus(100f, -2f);
            bus.Speed = 6f; sim.Bus.Throttle = 0.4f;
            Agent p = Crosser(sim, 135f, -2f, +1f);                   // 29 m ahead: not yet a danger, just walking
            Run(sim, 0.5f);
            Assert.AreEqual(sim.Tuning.Pedestrians.WalkSpeed, p.Speed, 0.01f, "walking across, no hurry yet");
            sim.HornInput(true, 1f / 60f);                             // a tap
            sim.HornInput(false, 1f / 60f);
            Run(sim, 0.5f);
            Assert.AreEqual(sim.Tuning.Pedestrians.RunSpeed, p.Speed, 0.01f, "honked at: running");
        }

        [Test]
        public void ANoseAt14KmhKnocksDownAndTheDeathCurveDecidesTheRest()
        {
            // Someone who cannot move (the rules say they would run; this is the test of the hit itself).
            var slow = Road();
            slow.Tuning.Pedestrians.WalkSpeed = 0f; slow.Tuning.Pedestrians.RunSpeed = 0f;
            slow.Tuning.Economy.DeathLogisticA = 50f;                  // the curve pinned to "lives": the knock-down itself is on trial
            Agent bus = slow.SpawnPlayerBus(100f, -2f);
            bus.Speed = 4f; slow.Bus.Throttle = 0.5f;                 // 14 km/h
            Crosser(slow, 110f, -2f, +1f);
            Run(slow, 3f);
            Assert.IsFalse(slow.Metrics.PersonHit, "down, not dead");
            Assert.AreEqual(1, slow.Metrics.PeopleKnockedDown);
            Assert.AreEqual(1, slow.Economy.Injuries);
            Assert.IsTrue(slow.Bus.Held, "the crowd holds the bus");
            Assert.IsFalse(slow.Economy.DayOver, "the first one is paid for");
            Assert.AreEqual(slow.Tuning.Economy.KnockDownTk, slow.Economy.Ledger.CaseTk, 1e-3f);

            var fast = Road();
            fast.Tuning.Pedestrians.WalkSpeed = 0f; fast.Tuning.Pedestrians.RunSpeed = 0f;
            fast.Tuning.Economy.DeathLogisticA = -50f;                 // pinned to "dies": at 40 km/h the real curve gives 18%
            Agent bus2 = fast.SpawnPlayerBus(100f, -2f);
            bus2.Speed = 11f; fast.Bus.Throttle = 1f;                  // 40 km/h
            Crosser(fast, 112f, -2f, +1f);
            Run(fast, 2f);
            Assert.IsTrue(fast.Metrics.PersonHit, "killed");
            Assert.IsTrue(fast.Economy.DayOver);
        }
    }
}
