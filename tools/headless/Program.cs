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
        public float Contested, First, LeadMetres, RivalBoarded, RivalFares;   // the pack: who was first, by how much, and what the crew buses took
        public bool PersonHit;
        public int Rollovers, KnockedDown;
    }

    private static float RateScale = 1f;
    private static float FoodShare = 50f;   // set from the tuning when the world is built
    private static float Density = 1f;

    public static int Main(string[] args)
    {
        var list = new List<string>(args);
        // --density X: traffic density multiplier (Spawn.VehiclesAround), for finding the regime the thesis needs.
        int dn = list.IndexOf("--density");
        if (dn >= 0) { Density = float.Parse(list[dn + 1]); list.RemoveRange(dn, 2); }
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
        // --crowd X: the same crowd rate multiplier the batch takes, for replaying one of its days.
        int cr = list.IndexOf("--crowd");
        if (cr >= 0) { RateScale = float.Parse(list[cr + 1]); list.RemoveRange(cr, 2); }
        // --watch S0 S1: every 5 s, print what is on the main road between S0 and S1 (a queue, a junction).
        float watch0 = -1f, watch1 = -1f;
        int w = list.IndexOf("--watch");
        if (w >= 0) { watch0 = float.Parse(list[w + 1]); watch1 = float.Parse(list[w + 2]); list.RemoveRange(w, 3); }
        int seed = list.Count > 0 ? int.Parse(list[0]) : 1;
        float seconds = list.Count > 1 ? float.Parse(list[1]) : 120f;
        float capKmh = list.Count > 2 ? float.Parse(list[2]) : 25f;

        TrafficSim sim = BuildWorld(seed);
        Agent bus = sim.Player;
        ScriptedDriver.Reset();
        ScriptedDriver.Current = dhaka ? Policy.Dhaka : Policy.Careful;
        ScriptedDriver.CapKmh = capKmh;
        DemandZone zoneWas = null;
        int frame = 0;
        int contactsWere = 0;
        int boardedAtOpen = 0;
        float openedAt = 0f;
        const float dt = 1f / 60f;
        for (float t = 0f; t < seconds && !sim.Economy.DayOver; t += dt)
        {
            ScriptedDriver.Apply(sim);
            sim.Step(dt);
            if (watch0 >= 0f && Mathf.RoundToInt(t * 60f) % 300 == 0) Watch(sim, t, watch0, watch1);
            if (verbose && bus.Load.ArrivedZone != zoneWas)
            {
                bool doorWas = bus.Load.ArrivedZone != null;    // arriving (true) or leaving (false)
                DemandZone z = doorWas ? bus.Load.ArrivedZone : zoneWas;
                zoneWas = bus.Load.ArrivedZone;
                float aheadM, behindM;
                Agent crewAhead = sim.OwnBusAhead(out aheadM), crewBehind = sim.OwnBusBehind(out behindM);
                // Another door open at this zone: ours is the second, and the second door gets nobody.
                int otherDoors = 0;
                if (z != null) foreach (Agent o in sim.Agents) if (o != bus && o.Load != null && o.Load.DoorOpen && Math.Abs(sim.Corridor.DeltaS(z.S, o.S)) <= sim.Tuning.Passengers.ZoneHalfLengthMetres) otherDoors++;
                string took = doorWas ? "" : $"  took {bus.Load.Boarded - boardedAtOpen} in {t - openedAt:0} s";
                if (doorWas) { boardedAtOpen = bus.Load.Boarded; openedAt = t; }
                Console.WriteLine($"  t={t,6:0.0} {(doorWas ? "ARRIVE" : "LEAVE ")} {(z == null ? "-" : z.Name),-10} waiting {(z == null ? 0 : z.Waiting.Count),2}  aboard {bus.Load.Count,2}  S={bus.S:0}  fares {bus.Load.FaresTk:0}  otherDoors {otherDoors}{took}  crew ahead {(crewAhead == null ? "-" : aheadM.ToString("0") + " m")} behind {(crewBehind == null ? "-" : behindM.ToString("0") + " m")}");
            }
            frame++;
            if (verbose && bus.Load.ArrivedZone != null && frame % 600 == 0)
            {
                DemandZone z = bus.Load.ArrivedZone;
                Console.WriteLine($"  t={t,6:0.0}   at {z.Name,-10} v={bus.Speed:0.0} waiting {z.Waiting.Count,2} aboard {bus.Load.Count,2} atDoor {(bus.Load.AtDoor != null ? "yes" : "no ")} leaving {(bus.Load.Leaving != null ? "yes" : "no ")} working {(ScriptedDriver.Working == null ? "-" : ScriptedDriver.Working.Name)} dwell {ScriptedDriver.Dwell:0} lastTakenBy {(z.LastTakenBy == null ? "-" : z.LastTakenBy.IsPlayer ? "us" : "#" + z.LastTakenBy.Id)} {t - z.LastTakenAt:0}s ago");
            }
            if (verbose && sim.Metrics.Contacts > contactsWere)
            {
                // Who did we touch, and how: the shape of the Dhaka driver's scrapes.
                contactsWere = sim.Metrics.Contacts;
                Console.Write($"  t={t,6:0.0} CONTACT #{contactsWere} bus S={bus.S:0} lat={bus.Lateral:0.00} v={bus.Speed:0.0} latV={bus.LateralVelocity:0.00} want={ScriptedDriver.WantLateral:0.00} steer={sim.Bus.Steer:0.00} wrongSide={sim.Metrics.WrongSideNow}:");
                foreach (Agent a in sim.Agents)
                {
                    if (a.IsPlayer || a.GhostOf != null || a.IsPedestrian) continue;
                    float ds = a.Corridor == sim.Corridor ? sim.Corridor.DeltaS(bus.S, a.S) : (a.Position - bus.Position).magnitude;
                    if (Mathf.Abs(ds) > bus.HalfLength + a.HalfLength + 1f) continue;
                    if (a.Corridor == sim.Corridor && Mathf.Abs(a.Lateral - bus.Lateral) > bus.HalfWidth + a.HalfWidth + 0.5f) continue;
                    Console.Write($"  {a.Class}#{a.Id} {(a.Corridor == sim.Corridor ? "" : a.Corridor.Name + " ")}ds={ds:0.0} lat={a.Lateral:0.00} v={a.Speed:0.0}");
                }
                Console.WriteLine();
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
        sim.DistanceScale = tuning.Economy.TripKm * 1000f / corridor.Length;   // one lap of the ring stands for one trip
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
        FoodShare = tuning.Economy.FoodTkPerDay * tuning.Economy.MoneyScale;
        tuning.Spawn.VehiclesAround = Mathf.RoundToInt(tuning.Spawn.VehiclesAround * Density);
        for (int i = 0; i < SandboxWorld.ZoneS.Length; i++) sim.AddZone(SandboxWorld.ZoneNames[i], SandboxWorld.ZoneS[i], SandboxWorld.ZoneHot[i]);
        for (int i = 0; i < SandboxWorld.CheckpointS.Length; i++) sim.AddCheckpoint(SandboxWorld.CheckpointNames[i], SandboxWorld.CheckpointS[i]);
        Boarding.SeedCrowds(sim, tuning.Passengers.InitialCrowdMinutes);   // the headway in front of the pack
        // The pack (docs/ROUTE_AND_TRIPS.md): three buses of one route leave the stand together, 18 m apart, the player in
            // the middle. Rafiq has the first door at Block 11 unless the player takes it from him; Jamal is on the player's tail.
            float packS = SandboxWorld.ZoneS[0] + tuning.Passengers.ZoneHalfLengthMetres + 12f;
            sim.SpawnPlayerBus(packS + 18f, -2f);
        sim.SpawnRivalBus("Rafiq", DriverPersonality.Reckless(), packS + 36f, -2f);
        sim.SpawnRivalBus("Jamal", DriverPersonality.Spiteful(), packS, -2f);
        return sim;
    }

    private static void Report(TrafficSim sim, int seed, float seconds, Agent bus)
    {
        DumpScene(sim, bus, sim.Corridor);
        SimMetrics m = sim.Metrics;
        BusLoad load = bus.Load;
        Ledger l = sim.Economy.Ledger;
        Console.WriteLine($"seed {seed} ({ScriptedDriver.Current}): {seconds:0}s  dist {m.DistanceMetres / 1000f:0.00} km  nearMisses {m.NearMisses} ({m.NearMissesPerMinute:0.0}/min)  minHeadway {m.MinHeadwaySeconds:0.00}s  scrapes {m.Contacts} ({m.HardContacts} hard)  caneRuns {m.CaneRuns}  horn {m.HornPresses} moved {m.YieldsToHorn}  agents {sim.Agents.Count}");
        Console.WriteLine($"  aboard {load.Count}  boarded {load.Boarded}  alighted {load.Alighted}  missed {load.MissedAlights}  stumbles {load.Stumbles}  brushes {m.Brushes}  hurt {load.Injuries}  fares Tk {load.FaresTk:0}  stopsLost {m.StopsLost}  first door {m.StopsFirst}/{m.StopsContested}  lead {(m.StopsContested > 0 ? m.LeadMetresSum / m.StopsContested : 0f):0} m");
        Console.WriteLine($"  ledger: fares {l.FaresTk:0}  zoma {l.ZomaTk:0}  fuel {l.FuelTk:0}  wages {l.WagesTk:0}  food {l.FoodTk:0}  lineman {l.LinemanTk:0}  party {l.PartyManTk:0}  sergeant {l.SergeantTk:0}  cases {l.CaseTk:0}  camera {l.CameraTk:0}  repairs {l.RepairsTk:0}  => crew {l.CrewNetTk:0} Tk  ({l.Trips} trips, day over: {sim.Economy.DayOver} {sim.Economy.DayOverReason})");
        Console.WriteLine($"  street: {(sim.Economy.DriveDay ? "DRIVE DAY" : "ordinary day")}  boxes manned {sim.Checkpoints.FindAll(c => c.SergeantOnDuty).Count}/{sim.Checkpoints.Count}  seized {sim.Economy.Seized}");
        foreach (Junction j in sim.Junctions)
        {
            int crossNear = 0;
            foreach (Agent a in sim.Agents) if (a.Corridor == j.Cross && !a.IsPedestrian && Math.Abs(j.Cross.DeltaS(j.CrossS, a.S)) < 40f) crossNear++;
            Console.WriteLine($"  junction S={j.MainS:0} {(j.Mirror != null ? "(mirror)" : "        ")} open={j.Open} timer={j.Timer:0}s roped={j.Roped} camera={j.Camera} inBox main={j.MainInBox} cross={j.CrossInBox} crossNear={crossNear} leakers={j.LeakersLeft}");
            if (j.Mirror == null)
            {
                foreach (Agent a in sim.Agents)
                {
                    if (a.Corridor != j.Cross || a.IsPedestrian || Math.Abs(j.Cross.DeltaS(j.CrossS, a.S)) >= 25f) continue;
                    float gapAhead;
                    Agent ahead = Steering.FindAhead(sim.Agents, a, a.Lateral, 60f, out gapAhead);
                    Console.WriteLine($"      cross {a.Class,-8} #{a.Id,-4} crossS-boxCentre={j.Cross.DeltaS(j.CrossS, a.S),6:0.0} lat={a.Lateral,5:0.00} v={a.Speed:0.0} want={a.DesiredSpeed:0.0} inBox={j.InBox(j.Cross, a.S, a.HalfLength)} ahead={(ahead == null ? "-" : ahead.Class + "#" + ahead.Id)} gap={gapAhead:0.0} stopAhead={sim.StopDistanceAhead(a, 60f):0.0}");
                }
            }
            else
            {
                // The oncoming carriageway at the same crossing: what stands in or before the mirror box, and why.
                foreach (Agent o in sim.Agents)
                {
                    if (o.Corridor != j.Main || o.IsPedestrian || Math.Abs(j.Main.DeltaS(j.MainS, o.S)) >= 30f) continue;
                    float gapAhead;
                    Agent ahead = Steering.FindAhead(sim.Agents, o, o.Lateral, 60f, out gapAhead);
                    string state = o.GhostOf != null ? "GHOST" : o.IsYielding ? "yield" : o.BluffTimer > 0f ? "bluff" : "drive";
                    Console.WriteLine($"      oncoming {o.Class,-8} #{o.Id,-4} S-boxCentre={j.Main.DeltaS(j.MainS, o.S),6:0.0} lat={o.Lateral,5:0.00} v={o.Speed:0.0} want={o.DesiredSpeed:0.0} inBox={j.InBox(j.Main, o.S, o.HalfLength)} {state} ahead={(ahead == null ? "-" : ahead.Class + "#" + ahead.Id + (ahead.GhostOf != null ? "(ghost)" : ""))} gap={gapAhead:0.0} stopAhead={sim.StopDistanceAhead(o, 60f):0.0}");
                }
            }
        }
        foreach (string e in l.Events) Console.WriteLine("    " + e);
        Console.WriteLine($"  wrong side {m.WrongSideSeconds:0} s  waited at closed canes {m.CaneWaitSeconds:0} s  of which at a rope {m.RopeHeldSeconds:0} s");
        if (sim.Rollover.Count > 0) Console.WriteLine($"  ROLLOVER x{sim.Rollover.Count}: {sim.Rollover.Cause}, {sim.Rollover.HurtPassengers} hurt");
        Console.WriteLine($"  bus: brake wear {sim.Condition.BrakeWear:0.00} (+{sim.Condition.BrakeWearToday:0.000} today)  dents {sim.Condition.Dents}  papers {(sim.Condition.PapersValid(sim.Day) ? "ok" : "none")}");
        Console.WriteLine($"  fatigue at end {sim.Fatigue.Level:0.00}  micro-sleeps {sim.Fatigue.MicroSleeps}");
        Console.WriteLine($"  voices: {sim.Voice.Lines.Count} lines");
        int from = Math.Max(0, sim.Voice.Lines.Count - 8);
        for (int i = from; i < sim.Voice.Lines.Count; i++) { VoiceLine v = sim.Voice.Lines[i]; Console.WriteLine($"    [{v.Time,5:0}s] {v.Speaker}: {v.Text}"); }
        Console.WriteLine($"  time: player standing at zones {load.StandingAtZoneSeconds:0} s, elsewhere {load.StandingElsewhereSeconds:0} s; door busy {load.DoorBusySeconds:0} s = {(load.Boarded > 0 ? load.DoorBusySeconds / load.Boarded : 0f):0.0} s a boarder");
        foreach (Agent a in sim.Agents)
        {
            if (a.Brain == null) continue;
            Console.WriteLine($"  crew {a.Brain.CrewName,-6} {a.Brain.Personality.Name,-9} gap {sim.Corridor.DeltaS(bus.S, a.S),6:0} m  {a.Brain.Action,-16} aboard {a.Load.Count,2}  boarded {a.Load.Boarded,2}  fares Tk {a.Load.FaresTk,4:0}  grudge {a.Brain.Grudge}  standing at zones {a.Load.StandingAtZoneSeconds:0} s, elsewhere {a.Load.StandingElsewhereSeconds:0} s, {(a.Load.Boarded > 0 ? a.Load.DoorBusySeconds / a.Load.Boarded : 0f):0.0} s a boarder, cruise {a.Brain.BaseCruise:0.0} m/s");
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
        Console.WriteLine($"Thesis report: {seeds} seeds x {seconds:0} s, careful vs Dhaka driving, crowd rate x{RateScale:0.00}, traffic x{Density:0.00}");
        Console.WriteLine("seed  policy   net Tk  fares  riders  first  lead m  rivalFares   km  scrapes  nearMiss  stopsLost  wrongSide  caneRuns  rolls  down  hit");
        for (int seed = 1; seed <= seeds; seed++)
        {
            foreach (Policy policy in new[] { Policy.Careful, Policy.Dhaka })
            {
                DayResult r = RunDay(seed, seconds, policy);
                (policy == Policy.Careful ? careful : dhaka).Add(r);
                Console.WriteLine($"{seed,4}  {policy,-7} {r.Net,7:0} {r.Fares,6:0} {r.Boarded,7:0} {FirstText(r),6} {r.LeadMetres,7:0} {r.RivalFares,11:0} {r.Km,5:0.00} {r.Scrapes,8:0} {r.NearMisses,9:0} {r.StopsLost,10:0} {r.WrongSide,9:0}s {r.CaneRuns,9:0} {r.Rollovers,6} {r.KnockedDown,5}  {(r.PersonHit ? "YES" : "")}");
            }
        }
        Console.WriteLine();
        Summ("careful", careful);
        Summ("dhaka", dhaka);
        float c = Mean(careful, r => r.Net), d = Mean(dhaka, r => r.Net);
        Console.WriteLine();
        // The eat line (owner, 4 Oct 2026): the premise is not "Dhaka earns more" but "drive carefully and you cannot
        // eat". The driver's take after the deposit, fuel, the line, the crew's wages and food has to cover the
        // household's food for the day share; careful must fail that, Dhaka must clear it.
        float food = careful.Count > 0 ? FoodShare : 0f;
        bool carefulCannotEat = c <= food, dhakaEats = d > food;
        // The pack (docs/ROUTE_AND_TRIPS.md, "The pack"): the three buses leave together and the first at the stop takes
        // all, so a crew's riders are a function of its lead. The careful bus should end up third: fewer riders than the
        // crew buses' mean, first at few stops.
        float cb = Mean(careful, r => r.Boarded), crb = Mean(careful, r => r.RivalBoarded), db = Mean(dhaka, r => r.Boarded), drb = Mean(dhaka, r => r.RivalBoarded);
        Console.WriteLine($"the pack: careful boarded {cb:0} against each crew bus's {crb:0} (first at {100f * Mean(careful, r => r.First) / Mathf.Max(1f, Mean(careful, r => r.Contested)):0} % of stops, lead {Mean(careful, r => r.LeadMetres):0} m); Dhaka boarded {db:0} against {drb:0} (first at {100f * Mean(dhaka, r => r.First) / Mathf.Max(1f, Mean(dhaka, r => r.Contested)):0} %, lead {Mean(dhaka, r => r.LeadMetres):0} m).");
        Console.WriteLine(cb < crb && db > crb ? "          careful is third in its pack, Dhaka leads: the pack holds." : cb < crb ? "          careful is third, but Dhaka does not lead its pack either." : "          careful is NOT third in its pack: the rivals leave too much on the kerb.");
        Console.WriteLine($"eat line: Tk {food:0} a day (the household's food). careful {(carefulCannotEat ? "cannot eat" : "EATS")} ({c:0}), Dhaka {(dhakaEats ? "eats" : "CANNOT EAT")} ({d:0}).");
        Console.WriteLine(carefulCannotEat && dhakaEats
            ? $"VERDICT: the trap holds. Careful driving does not feed the house; Dhaka driving does, by Tk {d - food:0}."
            : carefulCannotEat ? "VERDICT: nobody eats. The street is too hard for both; the costs or the crowds are off."
            : d > c ? $"VERDICT: polite driving still eats (careful {c:0} > food {food:0}). Dhaka earns Tk {d - c:0} more, but the premise is not met."
            : "VERDICT: polite driving still wins (careful net >= Dhaka net). RESEARCH says: rivals too timid, or the street too kind.");
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
        int crews = 0; float rivalBoarded = 0f, rivalFares = 0f;
        foreach (Agent a in sim.Agents) if (a.Brain != null && a.Load != null) { crews++; rivalBoarded += a.Load.Boarded; rivalFares += a.Load.FaresTk; }
        return new DayResult
        {
            Contested = m.StopsContested, First = m.StopsFirst, LeadMetres = m.StopsContested > 0 ? m.LeadMetresSum / m.StopsContested : 0f,
            RivalBoarded = crews > 0 ? rivalBoarded / crews : 0f, RivalFares = crews > 0 ? rivalFares / crews : 0f,
            Seed = seed, Net = l.CrewNetTk, Fares = l.FaresTk, Km = m.DistanceMetres / 1000f, Scrapes = m.Contacts,
            NearMisses = m.NearMisses, StopsLost = m.StopsLost, WrongSide = m.WrongSideSeconds, Trips = l.Trips,
            Boarded = sim.Player.Load.Boarded, CaneRuns = m.CaneRuns, PersonHit = m.PersonHit, Rollovers = sim.Rollover.Count, KnockedDown = m.PeopleKnockedDown,
        };
    }

    private static string FirstText(DayResult r)
    {
        return r.Contested > 0 ? $"{r.First:0}/{r.Contested:0}" : "-";
    }

    private static void Summ(string name, List<DayResult> rs)
    {
        int hits = 0, down = 0; foreach (DayResult r in rs) { if (r.PersonHit) hits++; down += r.KnockedDown; }
        Console.WriteLine($"{name,-8} mean net Tk {Mean(rs, r => r.Net),6:0}  fares {Mean(rs, r => r.Fares),5:0}  riders {Mean(rs, r => r.Boarded),4:0} (each crew bus {Mean(rs, r => r.RivalBoarded),4:0})  first {100f * Mean(rs, r => r.First) / Mathf.Max(1f, Mean(rs, r => r.Contested)),3:0} %  lead {Mean(rs, r => r.LeadMetres),4:0} m  km {Mean(rs, r => r.Km),4:0.00}  scrapes {Mean(rs, r => r.Scrapes),4:0.0}  stopsLost {Mean(rs, r => r.StopsLost),4:0.0}  wrongSide {Mean(rs, r => r.WrongSide),4:0}s  knocked down {down}  people hit {hits}/{rs.Count}");
    }

    private static float Mean(List<DayResult> rs, Func<DayResult, float> f)
    {
        float sum = 0f; foreach (DayResult r in rs) sum += f(r);
        return rs.Count > 0 ? sum / rs.Count : 0f;
    }

    /// <summary>Everyone within 40 m of the bus along the road, nearest first.</summary>
    private static void Watch(TrafficSim sim, float t, float s0, float s1)
    {
        Corridor c = sim.Corridor;
        var inside = new List<Agent>();
        foreach (Agent a in sim.Agents) if (a.Corridor == c && a.GhostOf == null && a.S >= s0 && a.S <= s1) inside.Add(a);
        inside.Sort((x, y) => y.S.CompareTo(x.S));
        var j = sim.Junctions.Count > 0 ? sim.Junctions[0] : null;
        Console.WriteLine($"--- t={t:0}s  junction0 open={j?.Open} timer={j?.Timer:0} roped={j?.Roped} mainInBox={j?.MainInBox} crossInBox={j?.CrossInBox}  ({inside.Count} on S {s0:0}-{s1:0}, head first)");
        foreach (Agent a in inside)
        {
            string state = a.IsPedestrian ? a.PedState.ToString() : a.IsPlayer ? $"PLAYER thr={sim.Bus.Throttle:0.0} brk={sim.Bus.Brake:0.0} want={ScriptedDriver.WantLateral:0.0} why={ScriptedDriver.Why}working={(ScriptedDriver.Working == null ? "-" : ScriptedDriver.Working.Name)}" : a.IsYielding ? "yield" : a.BluffTimer > 0f ? "bluff" : a.LeakingThrough != null ? "leak" : "drive";
            Console.WriteLine($"  {(a.IsPlayer ? "BUS*" : a.Class.ToString()),-10} #{a.Id,-4} S={a.S,6:0.0} lat={a.Lateral,5:0.00} tgt={a.TargetLateral,5:0.00} v={a.Speed,4:0.0} want={a.DesiredSpeed,4:0.0} {state,-8} heldAhead={a.HeldAhead}");
        }
    }

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
