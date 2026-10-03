using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    public class VoiceTests
    {
        private static TrafficSim World()
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            var ring = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 500), new Vector3(300, 0, 500), new Vector3(300, 0, 0) }, 10f, closed: true);
            var sim = new TrafficSim(ring, tuning, 4);
            sim.AddZone("Stand", 50f, false);
            sim.AddZone("Mid", 400f, false);
            sim.AddZone("Far", 900f, false);
            foreach (DemandZone z in sim.Zones) z.RatePerMinute = 0f;
            return sim;
        }

        private static void Run(TrafficSim sim, float seconds)
        {
            for (float t = 0; t < seconds; t += 1f / 60f) sim.Step(1f / 60f);
        }

        private static int Count(TrafficSim sim, string contains)
        {
            int n = 0;
            foreach (VoiceLine l in sim.Voice.Lines) if (l.Text.Contains(contains)) n++;
            return n;
        }

        [Test]
        public void HelperCallsTheBusBehindOncePerCooldown()
        {
            var sim = World();
            sim.SpawnPlayerBus(300f, -2f);
            Agent jamal = sim.SpawnRivalBus("Jamal", DriverPersonality.Spiteful(), 250f, -2f);
            jamal.DesiredSpeed = 0f; jamal.Speed = 0f;
            jamal.Shape.Acceleration = 0f;                    // his engine is dead for the test: he stays 50 m behind
            Run(sim, 20f);
            Assert.AreEqual(2, Count(sim, "Jamal is"), "at 0 s and after the 15 s cooldown");
            Assert.AreEqual(Speaker.Helper, sim.Voice.Lines[0].Speaker);
        }

        [Test]
        public void HelperCallsTheCrowdAhead()
        {
            var sim = World();
            Agent bus = sim.SpawnPlayerBus(350f, -2f);        // 50 m before Mid
            for (int i = 0; i < 6; i++) sim.Zones[1].Waiting.Add(new Passenger { BoardingSeconds = 2f, DestinationZone = 2 });
            sim.Step(1f / 60f);
            Assert.AreEqual(1, Count(sim, "Mid! 6 people"));
        }

        [Test]
        public void SilenceWithTrafficAheadDrawsAComplaintUntilYouHonk()
        {
            var sim = World();
            Agent bus = sim.SpawnPlayerBus(300f, 0f);
            bus.Speed = 4f;
            sim.Bus.Throttle = 0.09f;                          // holds about 4 m/s against rolling and air drag
            // A truck 25 m ahead at the same speed: traffic in the way for the whole run, no ramming.
            Agent slow = sim.SpawnVehicle(VehicleClass.Truck, 335f, 0f, 0.5f);
            slow.DesiredSpeed = 4f; slow.Speed = 4f;
            Run(sim, 25f);                                     // past the 20 s silence threshold
            Assert.AreEqual(1, Count(sim, "honking"));
            sim.HornInput(true, 1f / 60f);
            sim.HornInput(false, 1f / 60f);
            Run(sim, 15f);
            Assert.AreEqual(1, Count(sim, "honking"), "the horn reset the silence");
        }

        [Test]
        public void PassengerShoutsWhenCarriedPastTheirStop()
        {
            var sim = World();
            Agent bus = sim.SpawnPlayerBus(370f, -2f);
            sim.SetPassengerCount(bus, 0);
            bus.Load.Aboard.Add(new Passenger { BoardingSeconds = 2f, DestinationZone = 1 });
            bus.Speed = 10f;
            sim.Bus.Throttle = 1f;
            Run(sim, 10f);                                     // flies past Mid
            Assert.AreEqual(1, bus.Load.MissedAlights);
            Assert.AreEqual(1, Count(sim, "getting off here"));
        }

        [Test]
        public void TerminalLinesFollowTheGrudge()
        {
            var sim = World();
            sim.SpawnPlayerBus(300f, -2f);
            Agent rafiq = sim.SpawnRivalBus("Rafiq", DriverPersonality.Reckless(), 100f, -2f);
            var ledger = new Ledger { FaresTk = 500f, ZomaTk = 300f };
            rafiq.Brain.Grudge = 5;
            StringAssert.Contains("without a word", sim.Voice.TerminalLine(rafiq.Brain, ledger));
            rafiq.Brain.Grudge = 0;
            StringAssert.Contains("Long day", sim.Voice.TerminalLine(rafiq.Brain, ledger));
            ledger.ZomaTk = 900f;
            StringAssert.Contains("Same for us", sim.Voice.TerminalLine(rafiq.Brain, ledger));
        }
    }
}
