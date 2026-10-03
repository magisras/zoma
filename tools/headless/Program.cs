// Headless shift runner: the sandbox world driven by a scripted policy, no browser. Prints the
// playtest metrics, the ledger, the crews and the voices; on a pedestrian hit, the scene around it.
//
//   ./tools/headless.sh [seed] [seconds] [capKmh] [-v] [--dhaka]
//   ./tools/headless.sh --batch [seeds] [seconds]     both policies over many seeds: the thesis report
using System;
using System.Collections.Generic;
using TwentyTons.Core;
using TwentyTons.Sandbox;
using TwentyTons.Tuning;
using UnityEngine;

public static class Headless
{
    /// <summary>What one day came to.</summary>
    private sealed class DayResult
    {
        public int Seed;
        public float Net, Fares, Km, Scrapes, NearMisses, StopsLost, WrongSide, Trips, Boarded, CaneRuns;
        public bool PersonHit;
    }

    private static float RateScale = 1f;

    public static int Main(string[] args)
    {
        var list = new List<string>(args);
        if (list.Contains("--batch"))
        {
            list.Remove("--batch");
            int seeds = list.Count > 0 ? int.Parse(list[0]) : 6;
            float secs = list.Count > 1 ? float.Parse(list[1]) : 600f;
            if (list.Count > 2) RateScale = float.Parse(list[2]);     // crowd rate multiplier, to test the contest for stops
            return Batch(seeds, secs);
        }
        bool verbose = list.Remove("-v");
        bool dhaka = list.Remove("--dhaka");
        int seed = list.Count > 0 ? int.Parse(list[0]) : 1;
        float seconds = list.Count > 1 ? float.Parse(list[1]) : 120f;
        float capKmh = list.Count > 2 ? float.Parse(list[2]) : 25f;

        TrafficSim sim = BuildWorld(seed);
        Agent bus = sim.Player;
        ScriptedDriver.Reset();
        ScriptedDriver.Current = dhaka ? Policy.Dhaka : Policy.Careful;
        ScriptedDriver.CapKmh = capKmh;
        bool doorWas = false;
        const float dt = 1f / 60f;
        for (float t = 0f; t < seconds && !sim.Economy.DayOver; t += dt)
        {
            ScriptedDriver.Apply(sim);
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
                    float ds = sim.Corridor.DeltaS(bus.S, a.S);
                    if (Mathf.Abs(ds) < 12f)
                        Console.WriteLine($"  ped #{a.Id} ds={ds:0.0} lat={a.Lateral:0.00} state={a.PedState} dir={a.CrossDirection} speed={a.Speed:0.0} wait={a.WaitTimer:0.0}");
                }
                break;
            }
        }
        Report(sim, seed, seconds, bus);
        return sim.Metrics.PersonHit ? 2 : 0;
    }

    private static TrafficSim BuildWorld(int seed)
    {
        var tuning = ScriptableObject.CreateInstance<TuningTable>();
        var random = new SeededRandom(seed);
        Corridor corridor = SandboxWorld.BuildCorridor(random);
        var sim = new TrafficSim(corridor, tuning, seed + 1);
        sim.Tuning.Spawn.MedianMetres = SandboxWorld.MedianMetres;
        Corridor oncoming = SandboxWorld.BuildOncoming(corridor);
        sim.SetOncoming(oncoming);
        for (int i = 0; i < SandboxWorld.JunctionS.Length; i++)
        {
            Corridor cross = SandboxWorld.BuildCrossStreet(corridor, SandboxWorld.JunctionS[i], i);
            float crossAtMain, crossAtOncoming, oncomingS, unused;
            cross.Project(corridor.PositionAt(SandboxWorld.JunctionS[i], 0f), out crossAtMain, out unused);
            Junction main = sim.AddJunction(SandboxWorld.JunctionS[i], cross, crossAtMain);
            Vector3 onOncoming = corridor.PositionAt(SandboxWorld.JunctionS[i], SandboxWorld.OncomingOffset);
            oncoming.Project(onOncoming, out oncomingS, out unused);
            cross.Project(onOncoming, out crossAtOncoming, out unused);
            sim.Junctions.Add(new Junction(oncoming, oncomingS, cross, crossAtOncoming) { Mirror = main });
        }
        tuning.Passengers.BaseRatePerMinute *= RateScale;
        for (int i = 0; i < SandboxWorld.ZoneS.Length; i++) sim.AddZone(SandboxWorld.ZoneNames[i], SandboxWorld.ZoneS[i], SandboxWorld.ZoneHot[i]);
        for (int i = 0; i < SandboxWorld.CheckpointS.Length; i++) sim.AddCheckpoint(SandboxWorld.CheckpointNames[i], SandboxWorld.CheckpointS[i]);
        sim.SpawnPlayerBus(30f, -2f);
        sim.SpawnRivalBus("Rafiq", DriverPersonality.Reckless(), 180f, -2f);
        sim.SpawnRivalBus("Jamal", DriverPersonality.Spiteful(), corridor.Length - 160f, -2f);
        return sim;
    }

    private static void Report(TrafficSim sim, int seed, float seconds, Agent bus)
    {
        DumpScene(sim, bus, sim.Corridor);
        SimMetrics m = sim.Metrics;
        BusLoad load = bus.Load;
        Ledger l = sim.Economy.Ledger;
        Console.WriteLine($"seed {seed} ({ScriptedDriver.Current}): {seconds:0}s  dist {m.DistanceMetres / 1000f:0.00} km  nearMisses {m.NearMisses} ({m.NearMissesPerMinute:0.0}/min)  minHeadway {m.MinHeadwaySeconds:0.00}s  scrapes {m.Contacts} ({m.HardContacts} hard)  caneRuns {m.CaneRuns}  horn {m.HornPresses} moved {m.YieldsToHorn}  agents {sim.Agents.Count}");
        Console.WriteLine($"  aboard {load.Count}  boarded {load.Boarded}  alighted {load.Alighted}  missed {load.MissedAlights}  stumbles {load.Stumbles}  hurt {load.Injuries}  fares Tk {load.FaresTk:0}  stopsLost {m.StopsLost}");
        Console.WriteLine($"  ledger: fares {l.FaresTk:0}  zoma {l.ZomaTk:0}  fuel {l.FuelTk:0}  lineman {l.LinemanTk:0}  party {l.PartyManTk:0}  sergeant {l.SergeantTk:0}  cases {l.CaseTk:0}  camera {l.CameraTk:0}  repairs {l.RepairsTk:0}  => crew {l.CrewNetTk:0} Tk  ({l.Trips} trips, day over: {sim.Economy.DayOver} {sim.Economy.DayOverReason})");
        Console.WriteLine($"  street: {(sim.Economy.DriveDay ? "DRIVE DAY" : "ordinary day")}  boxes manned {sim.Checkpoints.FindAll(c => c.SergeantOnDuty).Count}/{sim.Checkpoints.Count}  seized {sim.Economy.Seized}");
        foreach (Junction j in sim.Junctions)
        {
            int crossNear = 0;
            foreach (Agent a in sim.Agents) if (a.Corridor == j.Cross && !a.IsPedestrian && Math.Abs(j.Cross.DeltaS(j.CrossS, a.S)) < 40f) crossNear++;
            Console.WriteLine($"  junction S={j.MainS:0} {(j.Mirror != null ? "(mirror)" : "        ")} open={j.Open} timer={j.Timer:0}s roped={j.Roped} camera={j.Camera} inBox main={j.MainInBox} cross={j.CrossInBox} crossNear={crossNear} leakers={j.LeakersLeft}");
        }
        foreach (string e in l.Events) Console.WriteLine("    " + e);
        Console.WriteLine($"  wrong side {m.WrongSideSeconds:0} s  waited at closed canes {m.CaneWaitSeconds:0} s  of which at a rope {m.RopeHeldSeconds:0} s");
        if (sim.Rollover.Count > 0) Console.WriteLine($"  ROLLOVER x{sim.Rollover.Count}: {sim.Rollover.Cause}, {sim.Rollover.HurtPassengers} hurt");
        Console.WriteLine($"  bus: brake wear {sim.Condition.BrakeWear:0.00} (+{sim.Condition.BrakeWearToday:0.000} today)  dents {sim.Condition.Dents}  papers {(sim.Condition.PapersValid(sim.Day) ? "ok" : "none")}");
        Console.WriteLine($"  fatigue at end {sim.Fatigue.Level:0.00}  micro-sleeps {sim.Fatigue.MicroSleeps}");
        Console.WriteLine($"  voices: {sim.Voice.Lines.Count} lines");
        int from = Math.Max(0, sim.Voice.Lines.Count - 8);
        for (int i = from; i < sim.Voice.Lines.Count; i++) { VoiceLine v = sim.Voice.Lines[i]; Console.WriteLine($"    [{v.Time,5:0}s] {v.Speaker}: {v.Text}"); }
        foreach (Agent a in sim.Agents)
        {
            if (a.Brain == null) continue;
            Console.WriteLine($"  crew {a.Brain.CrewName,-6} {a.Brain.Personality.Name,-9} gap {sim.Corridor.DeltaS(bus.S, a.S),6:0} m  {a.Brain.Action,-16} aboard {a.Load.Count,2}  boarded {a.Load.Boarded,2}  fares Tk {a.Load.FaresTk,4:0}  grudge {a.Brain.Grudge}");
        }
    }

    // ---------------------------------------------------------------- the thesis report

    /// <summary>
    /// RESEARCH.md: "'polite driving still wins' means rivals too timid." Run both policies over the
    /// same seeds and compare what the crew eats. The careful driver should lose.
    /// </summary>
    private static int Batch(int seeds, float seconds)
    {
        var careful = new List<DayResult>();
        var dhaka = new List<DayResult>();
        Console.WriteLine($"Thesis report: {seeds} seeds x {seconds:0} s, careful vs Dhaka driving, crowd rate x{RateScale:0.00}");
        Console.WriteLine("seed  policy   net Tk  fares   km  scrapes  nearMiss  stopsLost  wrongSide  caneRuns  trips  hit");
        for (int seed = 1; seed <= seeds; seed++)
        {
            foreach (Policy policy in new[] { Policy.Careful, Policy.Dhaka })
            {
                DayResult r = RunDay(seed, seconds, policy);
                (policy == Policy.Careful ? careful : dhaka).Add(r);
                Console.WriteLine($"{seed,4}  {policy,-7} {r.Net,7:0} {r.Fares,6:0} {r.Km,5:0.00} {r.Scrapes,8:0} {r.NearMisses,9:0} {r.StopsLost,10:0} {r.WrongSide,9:0}s {r.CaneRuns,9:0} {r.Trips,6:0}  {(r.PersonHit ? "YES" : "")}");
            }
        }
        Console.WriteLine();
        Summ("careful", careful);
        Summ("dhaka", dhaka);
        float c = Mean(careful, r => r.Net), d = Mean(dhaka, r => r.Net);
        Console.WriteLine();
        Console.WriteLine(c >= d
            ? "VERDICT: polite driving still wins (careful net >= Dhaka net). RESEARCH says: rivals too timid, or the street too kind."
            : $"VERDICT: the trap holds. Dhaka driving nets Tk {d - c:0} more per day than careful driving.");
        return 0;
    }

    private static DayResult RunDay(int seed, float seconds, Policy policy)
    {
        TrafficSim sim = BuildWorld(seed);
        sim.Tuning.Economy.DayLengthSeconds = seconds;
        ScriptedDriver.Reset();
        ScriptedDriver.Current = policy;
        const float dt = 1f / 60f;
        for (float t = 0f; t < seconds + 1f && !sim.Economy.DayOver; t += dt)
        {
            ScriptedDriver.Apply(sim);
            sim.Step(dt);
        }
        SimMetrics m = sim.Metrics;
        Ledger l = sim.Economy.Ledger;
        return new DayResult
        {
            Seed = seed, Net = l.CrewNetTk, Fares = l.FaresTk, Km = m.DistanceMetres / 1000f, Scrapes = m.Contacts,
            NearMisses = m.NearMisses, StopsLost = m.StopsLost, WrongSide = m.WrongSideSeconds, Trips = l.Trips,
            Boarded = sim.Player.Load.Boarded, CaneRuns = m.CaneRuns, PersonHit = m.PersonHit,
        };
    }

    private static void Summ(string name, List<DayResult> rs)
    {
        int hits = 0; foreach (DayResult r in rs) if (r.PersonHit) hits++;
        Console.WriteLine($"{name,-8} mean net Tk {Mean(rs, r => r.Net),6:0}  fares {Mean(rs, r => r.Fares),5:0}  km {Mean(rs, r => r.Km),4:0.00}  scrapes {Mean(rs, r => r.Scrapes),4:0.0}  nearMiss {Mean(rs, r => r.NearMisses),4:0.0}  stopsLost {Mean(rs, r => r.StopsLost),4:0.0}  wrongSide {Mean(rs, r => r.WrongSide),4:0}s  people hit {hits}/{rs.Count}");
    }

    private static float Mean(List<DayResult> rs, Func<DayResult, float> f)
    {
        float sum = 0f; foreach (DayResult r in rs) sum += f(r);
        return rs.Count > 0 ? sum / rs.Count : 0f;
    }

    /// <summary>Everyone within 40 m of the bus along the road, nearest first.</summary>
    private static void DumpScene(TrafficSim sim, Agent bus, Corridor corridor)
    {
        Console.WriteLine($"bus S={bus.S:0.0} lat={bus.Lateral:0.00} speed={bus.Speed:0.0} gap={sim.Metrics.GapAheadMetres:0.0}");
        var near = new List<Agent>();
        foreach (Agent a in sim.Agents) if (!a.IsPlayer && a.GhostOf == null && a.Corridor == corridor && Mathf.Abs(corridor.DeltaS(bus.S, a.S)) < 40f) near.Add(a);
        near.Sort((x, y) => Mathf.Abs(corridor.DeltaS(bus.S, x.S)).CompareTo(Mathf.Abs(corridor.DeltaS(bus.S, y.S))));
        foreach (Agent a in near)
        {
            float ds = corridor.DeltaS(bus.S, a.S);
            string state = a.IsPedestrian ? a.PedState.ToString() : (a.IsYielding ? "yield" : a.BluffTimer > 0 ? "bluff" : "drive");
            Console.WriteLine($"  {a.Class,-10} #{a.Id,-3} ds={ds,6:0.0} lat={a.Lateral,6:0.00} tgt={a.TargetLateral,6:0.00} v={a.Speed,4:0.0} want={a.DesiredSpeed,4:0.0} {state}");
        }
    }
}
