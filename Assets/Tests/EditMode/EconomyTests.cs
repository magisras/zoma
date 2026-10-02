using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    public class EconomyTests
    {
        private static TrafficSim Ring(int seed = 11)
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            var ring = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 300), new Vector3(200, 0, 300), new Vector3(200, 0, 0) }, 10f, closed: true);
            var sim = new TrafficSim(ring, tuning, seed);
            sim.AddZone("Stand", 50f, false);
            sim.AddZone("A", 300f, false);
            sim.AddZone("B", 550f, false);
            sim.AddZone("Market", 800f, false);
            foreach (DemandZone z in sim.Zones) z.RatePerMinute = 0f;
            return sim;
        }

        private static void Run(TrafficSim sim, float seconds)
        {
            for (float t = 0; t < seconds; t += 1f / 60f) sim.Step(1f / 60f);
        }

        /// <summary>Drive round the ring holding lateral −2, like the sandbox autopilot does.</summary>
        private static void Drive(TrafficSim sim, float seconds)
        {
            Agent bus = sim.Player;
            for (float t = 0; t < seconds; t += 1f / 60f)
            {
                float lookAhead = 8f + bus.Speed;
                Vector3 aim = sim.Corridor.PositionAt(bus.S + lookAhead, -2f);
                Vector3 to = aim - bus.Position;
                float wantedYaw = Mathf.Atan2(to.x, to.z);
                float error = Mathf.DeltaAngle(bus.Yaw * Mathf.Rad2Deg, wantedYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                sim.Bus.Steer = Mathf.Clamp(error * 3f, -1f, 1f);
                sim.Bus.Throttle = bus.Speed > 12f ? 0f : 1f;
                sim.Step(1f / 60f);
            }
        }

        [Test]
        public void FuelCostsFollowDistance()
        {
            var sim = Ring();
            sim.SpawnPlayerBus(100f, -2f);
            sim.Bus.Throttle = 1f;
            Run(sim, 30f);
            float km = sim.Metrics.DistanceMetres / 1000f;
            float expected = km / sim.Tuning.Economy.BusKmPerLitre * sim.Tuning.Economy.DieselTkPerLitre;
            Assert.Greater(km, 0.1f);
            Assert.AreEqual(expected, sim.Economy.Ledger.FuelTk, 0.5f);
        }

        [Test]
        public void ALapIsATripAndPaysTheLinemanAndThePartyMan()
        {
            var sim = Ring();
            Agent bus = sim.SpawnPlayerBus(100f, -2f);
            Drive(sim, 140f);                                 // at ~12 m/s: more than a lap of the 1 km ring
            Assert.GreaterOrEqual(sim.Economy.Ledger.Trips, 1);
            Assert.AreEqual(sim.Economy.Ledger.Trips * sim.Tuning.Economy.LinemanTkPerTrip * sim.Tuning.Economy.MoneyScale, sim.Economy.Ledger.LinemanTk, 1e-3f);
            Assert.GreaterOrEqual(sim.Economy.Ledger.PartyManTk, sim.Tuning.Economy.PartyManTkPerTrip * sim.Tuning.Economy.MoneyScale - 1e-3f);
        }

        [Test]
        public void ScrapesCostRepairs()
        {
            var sim = Ring();
            Agent bus = sim.SpawnPlayerBus(100f, 0f);
            bus.Speed = 8f;
            sim.Bus.Throttle = 1f;
            Agent rickshaw = sim.SpawnVehicle(VehicleClass.Rickshaw, 112f, 0f, 0f);
            rickshaw.DesiredSpeed = 0.5f; rickshaw.Speed = 0.5f;
            Run(sim, 4f);
            Assert.GreaterOrEqual(sim.Metrics.Contacts, 1);
            Assert.AreEqual(sim.Metrics.Contacts * sim.Tuning.Economy.ScrapeRepairTk * sim.Tuning.Economy.MoneyScale, sim.Economy.Ledger.RepairsTk, 1e-3f);
        }

        private static TrafficSim WithSergeantAfterCane(out Junction j)
        {
            var sim = Ring();
            var cross = new Corridor(new List<Vector3> { new Vector3(-100, 0, 150), new Vector3(100, 0, 150) }, 8f, false, "cross");
            j = sim.AddJunction(150f, cross, 100f);
            j.Open = JunctionFlow.Cross; j.Timer = 9999f;      // cane against us the whole time
            sim.Tuning.Economy.SergeantChanceAfterCaneRun = 1f;
            sim.Tuning.Spawn.CrossVehiclesPerJunction = 0;
            Agent bus = sim.SpawnPlayerBus(120f, -2f);
            bus.Speed = 8f;
            sim.Bus.Throttle = 1f;
            Run(sim, 12f);                                    // runs the cane, then the sergeant steps out
            return sim;
        }

        [Test]
        public void SergeantHoldsTheBusUntilYouPay()
        {
            var sim = WithSergeantAfterCane(out Junction j);
            Assert.GreaterOrEqual(sim.Metrics.CaneRuns, 1);
            Assert.IsTrue(sim.Economy.Sergeant.Active, "a sergeant stepped out");
            Run(sim, 3f);
            Assert.Less(sim.Player.Speed, 0.5f, "held");
            float s = sim.Player.S;
            sim.Economy.AnswerSergeant(true);
            Assert.IsFalse(sim.Economy.Sergeant.Active);
            Assert.AreEqual(sim.Tuning.Economy.SergeantDemandTk * sim.Tuning.Economy.MoneyScale, sim.Economy.Ledger.SergeantTk, 1e-3f);
            Run(sim, 5f);
            Assert.Greater(sim.Player.S, s + 5f, "released");
        }

        [Test]
        public void RefusingTheSergeantCostsACaseAndTime()
        {
            var sim = WithSergeantAfterCane(out Junction j);
            sim.Tuning.Economy.CaseDelaySeconds = 10f;
            sim.Economy.AnswerSergeant(false);
            Assert.AreEqual(sim.Tuning.Economy.CaseTk * sim.Tuning.Economy.MoneyScale, sim.Economy.Ledger.CaseTk, 1e-3f);
            Run(sim, 5f);
            Assert.IsTrue(sim.Economy.Sergeant.Active, "still held by the paperwork");
            Assert.Less(sim.Player.Speed, 0.5f);
            Run(sim, 7f);
            Assert.IsFalse(sim.Economy.Sergeant.Active, "released after the delay");
        }

        [Test]
        public void TheDayEndsAndTheZomaNeverMoves()
        {
            var sim = Ring();
            sim.Tuning.Economy.DayLengthSeconds = 20f;
            sim.SpawnPlayerBus(100f, -2f);
            sim.Player.Load.FaresTk = 500f;
            Run(sim, 21f);
            Assert.IsTrue(sim.Economy.DayOver);
            Ledger l = sim.Economy.Ledger;
            Assert.AreEqual(sim.Tuning.Economy.ZomaTk * sim.Tuning.Economy.MoneyScale, l.ZomaTk);
            Assert.AreEqual(500f - l.ZomaTk - l.PaidOutTk, l.CrewNetTk, 1e-3f);
            float t = sim.Metrics.Time;
            sim.Step(1f);
            Assert.AreEqual(t, sim.Metrics.Time, "nothing moves after the day");
        }

        [Test]
        public void HittingAPersonTakesTheDaysMoney()
        {
            var sim = Ring();
            sim.SpawnPlayerBus(100f, 0f);
            sim.Player.Load.FaresTk = 800f;
            sim.Bus.Throttle = 1f;
            Agent ped = sim.SpawnPedestrian(130f, -1);
            ped.PedState = PedestrianState.Crossing; ped.CrossDirection = 1f; ped.Lateral = 0f;
            sim.Tuning.Pedestrians.WalkSpeed = 0f;
            Run(sim, 15f);
            Assert.IsTrue(sim.Economy.DayOver);
            Assert.IsTrue(sim.Economy.Ledger.Arrested);
            Assert.Less(sim.Economy.Ledger.CrewNetTk, -sim.Tuning.Economy.PersonHitCaseTk * sim.Tuning.Economy.MoneyScale * 0.9f);
        }
    }
}
