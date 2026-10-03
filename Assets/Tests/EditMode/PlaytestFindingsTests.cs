using System.Collections.Generic;
using NUnit.Framework;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    /// <summary>
    /// Bugs found by the test session of 3 Oct 2026 (docs/test-reports/2026-10-03_0535.md), pinned as
    /// tests. They fail on main today, so they are [Explicit]: check.sh stays green and they run only
    /// when named. Whoever fixes one removes its [Explicit] so it guards from then on.
    ///   ./tools/check.sh --where "class == TwentyTons.Tests.PlaytestFindingsTests"
    /// </summary>
    public class PlaytestFindingsTests
    {
        private static TrafficSim NewSim(int seed = 1)
        {
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;        // tests place their own agents
            tuning.Spawn.PedestriansAround = 0;
            var road = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 1000) }, 10f);
            return new TrafficSim(road, tuning, seed);
        }

        private static void Run(TrafficSim sim, float seconds, float dt = 1f / 60f)
        {
            for (float t = 0; t < seconds; t += dt) sim.Step(dt);
        }

        [Test, Explicit("Known bug: a pedestrian 3 m in front of a standing bus waits for it all day")]
        public void PedestrianInFrontOfAStandingBusDoesNotWaitForever()
        {
            // Seen in headless seed 1 (careful): ped #382 stood in the road 3 m from the parked bus's nose,
            // state Crossing, speed 0, from t=380 s to the end of the day. Pedestrians.Decide treats a
            // standing vehicle within PullAwayWatchMetres as about to pull away at PullAwayAssumedSpeed,
            // so at 3 m it is always "too close" and the verdict is Stop; the bus, for its part, will not
            // move with a person at its nose. Neither ever moves.
            // Expected: within a minute the person has either finished crossing or gone back to a kerb.
            var sim = NewSim();
            Agent bus = sim.SpawnPlayerBus(100f, -2.1f);   // the player's bus, held on the brake as the
            Agent ped = sim.SpawnPedestrian(100f + bus.HalfLength + 3f, -1);   // careful driver holds it
            ped.PedState = PedestrianState.Crossing;
            ped.CrossDirection = 1f;
            ped.Lateral = -4.59f;                       // the position logged for ped #382

            for (float t = 0; t < 60f; t += 1f / 60f)
            {
                // The driver's reflex (sandbox/ScriptedDriver.PersonInTheWay): a person within 3 m of a
                // standing nose means the brake. Any careful human driver would do the same.
                sim.Bus.Throttle = 0f;
                sim.Bus.Brake = 1f;
                sim.Step(1f / 60f);
            }

            Assert.AreEqual(PedestrianState.Waiting, ped.PedState,
                $"still in the road after 60 s: lateral {ped.Lateral:0.00}, speed {ped.Speed:0.0}");
        }

        [Test, Explicit("Known bug: a rope snaps a vehicle already in the box back onto the line, into whoever is behind")]
        public void ARopeDoesNotPullAVehicleOutOfTheBoxBackwards()
        {
            // Seen in the browser (pass 2, rope try 2): held by the rope at t=81 with gapAhead = −6 m, a
            // vehicle overlapping the bus's nose. TrafficSim.HoldAtRopes treats anything whose nose is up
            // to StopLineSetbackMetres + MainHalfSpan past the line as "short of it" and sets its S back to
            // the line, whatever stands there. Expected: a vehicle already in the box when the rope goes up
            // clears the box; it is never moved backwards.
            var tuning = ScriptableObject.CreateInstance<TuningTable>();
            tuning.Spawn.VehiclesAround = 0;
            tuning.Spawn.PedestriansAround = 0;
            tuning.Spawn.CrossVehiclesPerJunction = 0;
            tuning.Economy.DriveDayChance = 0f;
            var road = new Corridor(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, 2000) }, 10f);
            var sim = new TrafficSim(road, tuning, 12);
            var crossRoad = new Corridor(new List<Vector3> { new Vector3(-100, 0, 300f), new Vector3(100, 0, 300f) }, 8f, false, "cross");
            Junction j = sim.AddJunction(300f, crossRoad, 100f);
            float line = j.StopLineOn(sim.Corridor, sim.Tuning.Officer.StopLineSetbackMetres);

            Agent car = sim.SpawnVehicle(VehicleClass.Car, 0f, -2f, 0.5f);
            car.S = line + 3f - car.HalfLength;                  // nose 3 m past the line, in the box
            car.Speed = 5f; car.DesiredSpeed = 5f;
            float noseBefore = car.S + car.HalfLength;
            j.Open = JunctionFlow.Cross; j.Timer = 9999f; j.Roped = true; j.LeakersLeft = 0;   // the rope goes up now

            sim.Step(1f / 60f);

            Assert.GreaterOrEqual(car.S + car.HalfLength, noseBefore - 0.01f,
                $"the car was moved backwards from nose {noseBefore:0.0} to {car.S + car.HalfLength:0.0} (line at {line:0.0})");
        }

        [Test, Explicit("Known bug: the helper keeps shouting the gap while the bus lies on its side")]
        public void HelperDoesNotShoutTheGapWhileTheBusIsOnItsSide()
        {
            // Seen in headless seed 1 (Dhaka): after "The bus is on its side (turned too hard for twenty
            // tons)", the last minutes of the voice log are "Helper: Jamal is 19 metres behind! Go, go!"
            // every 15 s. CrewVoice only goes quiet when the day is over, not while the bus is down.
            var sim = NewSim();
            Agent player = sim.SpawnPlayerBus(200f, -2f);
            sim.SpawnRivalBus("Jamal", DriverPersonality.Spiteful(), 180f, -2f);   // 20 m behind, own company
            Rollover.Tip(sim, "test");
            Assert.IsTrue(sim.Rollover.Active, "the bus is on its side");
            int before = sim.Voice.Lines.Count;

            Run(sim, 60f);

            for (int i = before; i < sim.Voice.Lines.Count; i++)
                StringAssert.DoesNotContain("Go, go", sim.Voice.Lines[i].Text, $"said at {sim.Voice.Lines[i].Time:0} s with the bus on its side");
        }
    }
}
