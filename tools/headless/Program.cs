// Headless shift runner: the sandbox world driven by a scripted policy, no browser. Prints the
// playtest metrics and, on a pedestrian hit, the scene around it. See tools/headless.sh.
using System;
using TwentyTons.Core;
using TwentyTons.Sandbox;
using TwentyTons.Tuning;
using UnityEngine;

public static class Headless
{
    public static int Main(string[] args)
    {
        int seed = args.Length > 0 ? int.Parse(args[0]) : 1;
        float seconds = args.Length > 1 ? float.Parse(args[1]) : 120f;
        float capKmh = args.Length > 2 ? float.Parse(args[2]) : 25f;

        var tuning = ScriptableObject.CreateInstance<TuningTable>();
        var random = new SeededRandom(seed);
        Corridor corridor = SandboxWorld.BuildCorridor(random);
        var sim = new TrafficSim(corridor, tuning, seed + 1);
        for (int i = 0; i < SandboxWorld.JunctionS.Length; i++)
        {
            Corridor cross = SandboxWorld.BuildCrossStreet(corridor, SandboxWorld.JunctionS[i], i);
            sim.AddJunction(SandboxWorld.JunctionS[i], cross, cross.Length * 0.5f);
        }
        for (int i = 0; i < SandboxWorld.ZoneS.Length; i++) sim.AddZone(SandboxWorld.ZoneNames[i], SandboxWorld.ZoneS[i], SandboxWorld.ZoneHot[i]);
        Agent bus = sim.SpawnPlayerBus(30f, -2f);
        sim.SpawnRivalBus("Rafiq", DriverPersonality.Reckless(), 180f, -2f);
        sim.SpawnRivalBus("Jamal", DriverPersonality.Spiteful(), corridor.Length - 160f, -2f);

        bool verbose = args.Length > 3 && args[3] == "-v";
        bool doorWas = false;
        const float dt = 1f / 60f;
        float hornClock = 0f;
        for (float t = 0f; t < seconds; t += dt)
        {
            ScriptedDriver.CapKmh = capKmh;
            ScriptedDriver.Apply(sim);
            hornClock += dt;
            sim.HornInput(hornClock % 10f < 0.2f, dt);
            sim.Step(dt);
            if (verbose && bus.Load.DoorOpen != doorWas)
            {
                doorWas = bus.Load.DoorOpen;
                DemandZone z = Boarding.ZoneInReach(sim, bus);
                Console.WriteLine($"  t={t,6:0.0} door {(doorWas ? "OPEN " : "shut ")} at {(z == null ? "-" : z.Name),-10} waiting {(z == null ? 0 : z.Waiting.Count),2}  aboard {bus.Load.Count,2}  S={bus.S:0}");
            }

            if (sim.Metrics.PersonHit)
            {
                Console.WriteLine($"PERSON HIT at t={t:0.00}s  bus S={bus.S:0.0} lat={bus.Lateral:0.00} speed={bus.Speed:0.0} m/s");
                foreach (Agent a in sim.Agents)
                {
                    if (!a.IsPedestrian) continue;
                    float ds = corridor.DeltaS(bus.S, a.S);
                    if (Mathf.Abs(ds) < 12f)
                        Console.WriteLine($"  ped #{a.Id} ds={ds:0.0} lat={a.Lateral:0.00} state={a.PedState} dir={a.CrossDirection} speed={a.Speed:0.0} wait={a.WaitTimer:0.0}");
                }
                return 2;
            }
        }
        DumpScene(sim, bus, corridor);
        SimMetrics m = sim.Metrics;
        BusLoad load = bus.Load;
        Console.WriteLine($"seed {seed}: {seconds:0}s  dist {m.DistanceMetres / 1000f:0.00} km  nearMisses {m.NearMisses} ({m.NearMissesPerMinute:0.0}/min)  minHeadway {m.MinHeadwaySeconds:0.00}s  scrapes {m.Contacts}  caneRuns {m.CaneRuns}  horn {m.HornPresses} moved {m.YieldsToHorn}  agents {sim.Agents.Count}");
        Console.WriteLine($"  aboard {load.Count}  boarded {load.Boarded}  alighted {load.Alighted}  missed {load.MissedAlights}  fares Tk {load.FaresTk:0}  stopsLost {m.StopsLost}");
        Ledger l = sim.Economy.Ledger;
        Console.WriteLine($"  ledger: fares {l.FaresTk:0}  zoma {l.ZomaTk:0}  fuel {l.FuelTk:0}  lineman {l.LinemanTk:0}  party {l.PartyManTk:0}  sergeant {l.SergeantTk:0}  cases {l.CaseTk:0}  repairs {l.RepairsTk:0}  => crew {l.CrewNetTk:0} Tk  ({l.Trips} trips, day over: {sim.Economy.DayOver} {sim.Economy.DayOverReason})");
        foreach (string e in l.Events) Console.WriteLine("    " + e);
        Console.WriteLine($"  fatigue at end {sim.Fatigue.Level:0.00}  micro-sleeps {sim.Fatigue.MicroSleeps}");
        foreach (Agent a in sim.Agents)
        {
            if (a.Brain == null) continue;
            Console.WriteLine($"  crew {a.Brain.CrewName,-6} {a.Brain.Personality.Name,-9} gap {corridor.DeltaS(bus.S, a.S),6:0} m  {a.Brain.Action,-16} aboard {a.Load.Count,2}  boarded {a.Load.Boarded,2}  fares Tk {a.Load.FaresTk,4:0}  grudge {a.Brain.Grudge}");
        }
        return 0;
    }

    /// <summary>Everyone within 40 m of the bus along the road, nearest first.</summary>
    private static void DumpScene(TrafficSim sim, Agent bus, Corridor corridor)
    {
        Console.WriteLine($"bus S={bus.S:0.0} lat={bus.Lateral:0.00} speed={bus.Speed:0.0} gap={sim.Metrics.GapAheadMetres:0.0}");
        var near = new System.Collections.Generic.List<Agent>();
        foreach (Agent a in sim.Agents) if (!a.IsPlayer && a.Corridor == corridor && Mathf.Abs(corridor.DeltaS(bus.S, a.S)) < 40f) near.Add(a);
        near.Sort((x, y) => Mathf.Abs(corridor.DeltaS(bus.S, x.S)).CompareTo(Mathf.Abs(corridor.DeltaS(bus.S, y.S))));
        foreach (Agent a in near)
        {
            float ds = corridor.DeltaS(bus.S, a.S);
            string state = a.IsPedestrian ? a.PedState.ToString() : (a.IsYielding ? "yield" : a.BluffTimer > 0 ? "bluff" : "drive");
            Console.WriteLine($"  {a.Class,-10} #{a.Id,-3} ds={ds,6:0.0} lat={a.Lateral,6:0.00} tgt={a.TargetLateral,6:0.00} v={a.Speed,4:0.0} want={a.DesiredSpeed,4:0.0} {state}");
        }
    }
}
