using System;
using System.Collections.Generic;
using Microsoft.JSInterop;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Sandbox
{
    /// <summary>What the page needs once, at start: the road and the buildings.</summary>
    public sealed class SceneDto
    {
        public float RoadWidth { get; set; }
        public float RoadLength { get; set; }
        public float[] LeftEdge { get; set; }
        public float[] RightEdge { get; set; }
        public float[] Buildings { get; set; }
        /// <summary>Per cross street: its left and right edge polylines.</summary>
        public List<float[]> CrossEdges { get; set; }
        public float[] OncomingLeftEdge { get; set; }
        public float[] OncomingRightEdge { get; set; }
        /// <summary>Per zone: x, z, yaw, side. Names in ZoneNames.</summary>
        public float[] Zones { get; set; }
        public string[] ZoneNames { get; set; }
    }

    /// <summary>The day's equation, for the end-of-day card.</summary>
    public sealed class LedgerDto
    {
        public float Fares { get; set; }
        public float Zoma { get; set; }
        public float Fuel { get; set; }
        public float Lineman { get; set; }
        public float PartyMan { get; set; }
        public float Sergeant { get; set; }
        public float Cases { get; set; }
        public float Repairs { get; set; }
        public float CrewNet { get; set; }
        public bool Arrested { get; set; }
    }

    /// <summary>One named crew, every frame.</summary>
    public sealed class RivalDto
    {
        public string Name { get; set; }
        public float GapMetres { get; set; }      // + ahead of the player, − behind
        public string Action { get; set; }
        public int Grudge { get; set; }
        public int Aboard { get; set; }
        public bool DoorOpen { get; set; }
    }

    /// <summary>One junction, every frame: where the officer stands and which way the cane points.</summary>
    public sealed class JunctionDto
    {
        public float X { get; set; }
        public float Z { get; set; }
        public float MainYaw { get; set; }
        public bool MainOpen { get; set; }
        public float Timer { get; set; }
    }

    /// <summary>What the page needs every frame.</summary>
    public sealed class FrameDto
    {
        /// <summary>Per agent: id, class, x, y, z, yaw, length, width, height, flags. Flags: 1 player, 2 horn, 4 yielding, 8 crossing, 16 bluffing.</summary>
        public float[] Agents { get; set; }
        public float SpeedKmh { get; set; }
        public float GapAheadMetres { get; set; }
        public float HeadwaySeconds { get; set; }
        public float MinHeadwaySeconds { get; set; }
        public int NearMisses { get; set; }
        public float NearMissesPerMinute { get; set; }
        public int Contacts { get; set; }
        public int HornPresses { get; set; }
        public int YieldsToHorn { get; set; }
        public int Passengers { get; set; }
        public float BrakeWear { get; set; }
        public float Time { get; set; }
        public float DistanceMetres { get; set; }
        public bool PersonHit { get; set; }
        public int AgentCount { get; set; }
        public int CaneRuns { get; set; }
        public float WrongSideSeconds { get; set; }
        public bool WrongSideNow { get; set; }
        /// <summary>Debug hooks for scripted drivers: where the bus sits across the road and how it points.</summary>
        public float Lateral { get; set; }
        public float YawErrorDeg { get; set; }
        public bool Autopilot { get; set; }
        public string AutopilotName { get; set; }
        public bool DoorOpen { get; set; }
        public int Seats { get; set; }
        public float FaresTk { get; set; }
        public int Boarded { get; set; }
        public int Alighted { get; set; }
        public int MissedAlights { get; set; }
        public string ZoneName { get; set; }      // zone in reach, or null
        public int ZoneWaiting { get; set; }
        public string AtDoor { get; set; }        // who is on the step: "on: Student" / "off: Regular" / null
        public int[] ZoneCrowds { get; set; }     // waiting count per zone, for drawing
        public int StopsLost { get; set; }
        public List<RivalDto> Rivals { get; set; }
        public string HelperGap { get; set; }     // the helper's call: who is ahead and behind, how far
        public string Clock { get; set; }         // "06:42"
        public float PaidOutTk { get; set; }
        public int Trips { get; set; }
        public bool SergeantActive { get; set; }
        public string SergeantText { get; set; }
        public bool SergeantDecided { get; set; } // refused: waiting out the paperwork
        public float SergeantWaitLeft { get; set; }
        public bool DayOver { get; set; }
        public string DayOverReason { get; set; }
        public LedgerDto Ledger { get; set; }
        public string[] Events { get; set; }
        public float Fatigue { get; set; }
        public float Tunnel { get; set; }         // 0..1 how far vision has narrowed
        public bool Asleep { get; set; }
        public int MicroSleeps { get; set; }
        public int Day { get; set; }
        public float SavingsTk { get; set; }
        public bool SleptChosen { get; set; }     // the end-of-day card's two steps
        public string Subtitle { get; set; }      // "Helper: ..." or null
        public float SubtitleAge { get; set; }
        public List<JunctionDto> Junctions { get; set; }
    }

    /// <summary>
    /// The bridge JavaScript calls. Static because Blazor's synchronous interop
    /// (DotNet.invokeMethod) needs static [JSInvokable] methods. One simulation at a time.
    /// </summary>
    public static class SandboxApi
    {
        private const int KeyUp = 1, KeyDown = 2, KeyLeft = 4, KeyRight = 8, KeyHorn = 16, KeyAutopilot = 32, KeyDoor = 64, KeyPay = 128, KeyRefuse = 256, KeyDhaka = 512;
        private const float FixedStep = 1f / 60f;

        private static TrafficSim _sim;
        private static TuningTable _tuning;
        private static float _accumulator;
        private static bool _autopilot;
        private static readonly Household _household = new Household();
        private static bool _dayClosed, _sleptChosen;

        [JSInvokable]
        public static SceneDto Init(int seed)
        {
            _tuning = ScriptableObject.CreateInstance<TuningTable>();
            return Reset(seed);
        }

        [JSInvokable]
        public static SceneDto Reset(int seed)
        {
            var random = new SeededRandom(seed);
            Corridor corridor = SandboxWorld.BuildCorridor(random);
            _sim = new TrafficSim(corridor, _tuning, seed + 1);
            _tuning.Spawn.MedianMetres = SandboxWorld.MedianMetres;
            Corridor oncoming = SandboxWorld.BuildOncoming(corridor);
            _sim.SetOncoming(oncoming);
            var crossEdges = new List<float[]>();
            for (int i = 0; i < SandboxWorld.JunctionS.Length; i++)
            {
                Corridor cross = SandboxWorld.BuildCrossStreet(corridor, SandboxWorld.JunctionS[i], i);
                // Where the cross street meets each carriageway's centreline, in the cross street's own S.
                float crossAtMain, crossAtOncoming, oncomingS, unused;
                cross.Project(corridor.PositionAt(SandboxWorld.JunctionS[i], 0f), out crossAtMain, out unused);
                Junction mainJunction = _sim.AddJunction(SandboxWorld.JunctionS[i], cross, crossAtMain);
                Vector3 onOncoming = corridor.PositionAt(SandboxWorld.JunctionS[i], SandboxWorld.OncomingOffset);
                oncoming.Project(onOncoming, out oncomingS, out unused);
                cross.Project(onOncoming, out crossAtOncoming, out unused);
                var mirror = new Junction(oncoming, oncomingS, cross, crossAtOncoming) { Mirror = mainJunction };
                _sim.Junctions.Add(mirror);
                crossEdges.Add(SandboxWorld.RoadEdge(cross, -cross.HalfWidth));
                crossEdges.Add(SandboxWorld.RoadEdge(cross, cross.HalfWidth));
            }
            for (int i = 0; i < SandboxWorld.ZoneS.Length; i++)
            {
                _sim.AddZone(SandboxWorld.ZoneNames[i], SandboxWorld.ZoneS[i], SandboxWorld.ZoneHot[i]);
            }
            ScriptedDriver.Reset();
            _sim.SpawnPlayerBus(30f, -2f);
            // The two crews of the player's own company: one ahead, one behind, as the research describes.
            _sim.SpawnRivalBus("Rafiq", DriverPersonality.Reckless(), 180f, -2f);
            _sim.SpawnRivalBus("Jamal", DriverPersonality.Spiteful(), corridor.Length - 160f, -2f);
            _accumulator = 0f;

            return new SceneDto
            {
                RoadWidth = corridor.Width,
                RoadLength = corridor.Length,
                LeftEdge = SandboxWorld.RoadEdge(corridor, -corridor.HalfWidth),
                RightEdge = SandboxWorld.RoadEdge(corridor, corridor.HalfWidth),
                Buildings = SandboxWorld.BuildBuildings(corridor, random),
                CrossEdges = crossEdges,
                OncomingLeftEdge = SandboxWorld.RoadEdge(oncoming, -oncoming.HalfWidth),
                OncomingRightEdge = SandboxWorld.RoadEdge(oncoming, oncoming.HalfWidth),
                Zones = ZoneGeometry(),
                ZoneNames = SandboxWorld.ZoneNames,
            };
        }

        private static string SpeakerName(VoiceLine line)
        {
            switch (line.Speaker)
            {
                case Speaker.Helper: return "Helper";
                case Speaker.Conductor: return "Conductor";
                case Speaker.Passenger: return "Passenger";
                default: return line.Who ?? "Crew";
            }
        }

        private static float[] ZoneGeometry()
        {
            var data = new float[_sim.Zones.Count * 4];
            for (int i = 0; i < _sim.Zones.Count; i++)
            {
                DemandZone z = _sim.Zones[i];
                data[i * 4] = z.Position.x; data[i * 4 + 1] = z.Position.z;
                data[i * 4 + 2] = _sim.Corridor.YawAt(z.S); data[i * 4 + 3] = z.Side;
            }
            return data;
        }

        /// <summary>Advance the world by the frame time, in fixed 60 Hz steps so physics never depends on frame rate.</summary>
        [JSInvokable]
        public static FrameDto Tick(float dt, int keys)
        {
            var input = new PlayerInput
            {
                Throttle = (keys & KeyUp) != 0 ? 1f : 0f,
                Brake = (keys & KeyDown) != 0 ? 1f : 0f,
                Steer = ((keys & KeyRight) != 0 ? 1f : 0f) - ((keys & KeyLeft) != 0 ? 1f : 0f),
                Horn = (keys & KeyHorn) != 0,
                Door = (keys & KeyDoor) != 0,
            };
            bool autopilot = (keys & KeyAutopilot) != 0;
            _autopilot = autopilot;
            ScriptedDriver.Current = (keys & KeyDhaka) != 0 ? Policy.Dhaka : Policy.Careful;
            if ((keys & KeyPay) != 0) _sim.Economy.AnswerSergeant(true);
            if ((keys & KeyRefuse) != 0) _sim.Economy.AnswerSergeant(false);

            _accumulator += Mathf.Min(dt, 0.1f);           // a hidden tab must not fast-forward the world
            while (_accumulator >= FixedStep)
            {
                if (autopilot)
                {
                    ScriptedDriver.Apply(_sim);              // the "drive properly" baseline, for demos and scripts
                    _sim.HornInput(input.Horn, FixedStep);
                }
                else
                {
                    _sim.PlayerInputs(input);                // through the tired hands
                }
                _sim.Step(FixedStep);
                _accumulator -= FixedStep;
            }
            return Snapshot();
        }

        /// <summary>End of a worked day, step one: book the net, then choose where to sleep.</summary>
        [JSInvokable]
        public static void Sleep(bool bed)
        {
            if (!_sim.Economy.DayOver || _sleptChosen) return;
            if (!_dayClosed) { _household.CloseWorkedDay(_sim.Economy.Ledger, _tuning.Economy); _dayClosed = true; }
            _household.Sleep(bed, _sim.Fatigue.Level, _tuning.Fatigue, _tuning.Economy);
            _sleptChosen = true;
        }

        /// <summary>Step two: work tomorrow, or take the day off. Either way a new day starts.</summary>
        [JSInvokable]
        public static SceneDto NextDay(bool work, int seed)
        {
            if (!_sleptChosen) Sleep(false);
            if (!work) _household.RestDay(_tuning.Fatigue, _tuning.Economy);   // the day off is a day of its own
            _household.StartNextWorkDay();
            _dayClosed = false;
            _sleptChosen = false;
            SceneDto scene = Reset(seed);
            _sim.Fatigue.Level = _household.FatigueCarried;
            return scene;
        }

        /// <summary>Live tuning from the page's sliders. Names are explicit on purpose: no reflection to learn.</summary>
        [JSInvokable]
        public static void SetParam(string name, float value)
        {
            switch (name)
            {
                case "nerve": _tuning.Nerve.DefaultNerve = value; break;
                case "gapMin": _tuning.Gap.CriticalGapMinSeconds = value; break;
                case "gapMax": _tuning.Gap.CriticalGapMaxSeconds = value; break;
                case "hornTap": _tuning.Horn.TapStrength = value; break;
                case "yieldSilent": _tuning.Nerve.YieldChancePerSecondWithoutHorn = value; break;
                case "bluff": _tuning.Nerve.BluffChanceAtFullNerve = value; break;
                case "vehicles": _tuning.Spawn.VehiclesAround = (int)value; break;
                case "pedestrians": _tuning.Spawn.PedestriansAround = (int)value; break;
                case "brakeWear": _sim.Bus.BrakeWear = value; break;
                case "passengers": _sim.SetPassengerCount(_sim.Player, (int)value); break;
                case "dayLength": _tuning.Economy.DayLengthSeconds = value; break;
                case "fatigue": _sim.Fatigue.Level = value; break;
                case "zoma": _tuning.Economy.ZomaTk = value; _sim.Economy.Ledger.ZomaTk = value * _tuning.Economy.MoneyScale; break;
                default: throw new ArgumentException("Unknown tuning parameter: " + name);
            }
            _tuning.Gap.Clamp();
        }

        private static FrameDto Snapshot()
        {
            List<Agent> agents = _sim.Agents;
            var data = new float[agents.Count * 10];
            int k = 0;
            for (int i = 0; i < agents.Count; i++)
            {
                Agent a = agents[i];
                if (a.GhostOf != null) continue;                  // the ghost is the player, already drawn
                int flags = (a.IsPlayer ? 1 : 0) | (a.IsHorning ? 2 : 0) | (a.IsYielding ? 4 : 0)
                          | (a.PedState == PedestrianState.Crossing ? 8 : 0) | (a.BluffTimer > 0f ? 16 : 0)
                          | (a.Brain != null && a.Brain.OwnCompany ? 32 : 0) | (a.Load != null && a.Load.DoorOpen ? 64 : 0);
                data[k++] = a.Id;
                data[k++] = (int)a.Class;
                data[k++] = a.Position.x;
                data[k++] = a.Shape.Height * 0.5f;
                data[k++] = a.Position.z;
                data[k++] = a.Yaw;
                data[k++] = a.Shape.Length;
                data[k++] = a.Shape.Width;
                data[k++] = a.Shape.Height;
                data[k++] = flags;
            }

            if (k < data.Length) System.Array.Resize(ref data, k);
            var junctions = new List<JunctionDto>();
            foreach (Junction j in _sim.Junctions)
            {
                if (j.Mirror != null) continue;                  // one officer, drawn once
                junctions.Add(new JunctionDto
                {
                    X = j.Centre.x, Z = j.Centre.z, MainYaw = _sim.Corridor.YawAt(j.MainS),
                    MainOpen = j.Open == JunctionFlow.Main, Timer = j.Timer,
                });
            }

            BusLoad load = _sim.Player.Load;
            DemandZone near = Boarding.ZoneInReach(_sim, _sim.Player);
            var crowds = new int[_sim.Zones.Count];
            for (int i = 0; i < crowds.Length; i++) crowds[i] = _sim.Zones[i].Waiting.Count;
            string atDoor = load.AtDoor == null ? null : (load.AtDoorIsAlighting ? "off: " : "on: ") + load.AtDoor.Kind;

            var rivals = new List<RivalDto>();
            foreach (Agent a in _sim.Agents)
            {
                if (a.Brain == null) continue;
                rivals.Add(new RivalDto
                {
                    Name = a.Brain.CrewName, GapMetres = _sim.Corridor.DeltaS(_sim.Player.S, a.S),
                    Action = a.Brain.Action.ToString(), Grudge = a.Brain.Grudge, Aboard = a.Load.Count, DoorOpen = a.Load.DoorOpen,
                });
            }
            Agent ahead = _sim.OwnBusAhead(out float aheadM);
            Agent behind = _sim.OwnBusBehind(out float behindM);
            string helper = (ahead != null ? ahead.Brain.CrewName + " " + Mathf.RoundToInt(aheadM) + " m ahead" : "nobody ahead")
                          + " · " + (behind != null ? behind.Brain.CrewName + " " + Mathf.RoundToInt(behindM) + " m behind" : "nobody behind");

            Economy eco = _sim.Economy;
            Ledger ledger = eco.Ledger;
            float hours = eco.ClockHours;
            string clock = ((int)hours).ToString("00") + ":" + ((int)((hours - (int)hours) * 60f)).ToString("00");
            var events = ledger.Events.ToArray();

            SimMetrics m = _sim.Metrics;
            return new FrameDto
            {
                Clock = clock,
                PaidOutTk = ledger.PaidOutTk,
                Trips = ledger.Trips,
                SergeantActive = eco.Sergeant.Active,
                SergeantText = eco.Sergeant.Active ? "Sergeant: " + eco.Sergeant.Reason + ". Tk " + eco.Sergeant.DemandTk.ToString("0") + " now, or a case." : null,
                SergeantDecided = eco.Sergeant.Active && eco.Sergeant.ReleaseAt >= 0f,
                SergeantWaitLeft = eco.Sergeant.ReleaseAt >= 0f ? Mathf.Max(0f, eco.Sergeant.ReleaseAt - m.Time) : 0f,
                DayOver = eco.DayOver,
                DayOverReason = eco.DayOverReason,
                Ledger = new LedgerDto
                {
                    Fares = ledger.FaresTk, Zoma = ledger.ZomaTk, Fuel = ledger.FuelTk, Lineman = ledger.LinemanTk,
                    PartyMan = ledger.PartyManTk, Sergeant = ledger.SergeantTk, Cases = ledger.CaseTk, Repairs = ledger.RepairsTk,
                    CrewNet = ledger.CrewNetTk, Arrested = ledger.Arrested,
                },
                Events = events,
                Fatigue = _sim.Fatigue.Level,
                Tunnel = _sim.Fatigue.Tunnel(_tuning.Fatigue),
                Asleep = _sim.Fatigue.Asleep,
                MicroSleeps = _sim.Fatigue.MicroSleeps,
                Day = _household.Day,
                SavingsTk = _household.SavingsTk,
                SleptChosen = _sleptChosen,
                Subtitle = _sim.Voice.Latest == null ? null : SpeakerName(_sim.Voice.Latest) + ": " + _sim.Voice.Latest.Text,
                SubtitleAge = _sim.Voice.Latest == null ? 999f : m.Time - _sim.Voice.Latest.Time,
                StopsLost = m.StopsLost,
                Rivals = rivals,
                HelperGap = helper,
                DoorOpen = load.DoorOpen,
                Seats = _tuning.Bus.Seats,
                FaresTk = load.FaresTk,
                Boarded = load.Boarded,
                Alighted = load.Alighted,
                MissedAlights = load.MissedAlights,
                ZoneName = near == null ? null : near.Name,
                ZoneWaiting = near == null ? 0 : near.Waiting.Count,
                AtDoor = atDoor,
                ZoneCrowds = crowds,
                CaneRuns = m.CaneRuns,
                WrongSideSeconds = m.WrongSideSeconds,
                WrongSideNow = m.WrongSideNow,
                Autopilot = _autopilot,
                AutopilotName = ScriptedDriver.Current == Policy.Dhaka ? "the Dhaka driver" : "the careful driver",
                Lateral = _sim.Player.Lateral,
                YawErrorDeg = Mathf.DeltaAngle(_sim.Corridor.YawAt(_sim.Player.S) * Mathf.Rad2Deg, _sim.Player.Yaw * Mathf.Rad2Deg),
                Junctions = junctions,
                Agents = data,
                SpeedKmh = _sim.Player.Speed * 3.6f,
                GapAheadMetres = m.GapAheadMetres,
                HeadwaySeconds = m.HeadwayAheadSeconds,
                MinHeadwaySeconds = m.MinHeadwaySeconds,
                NearMisses = m.NearMisses,
                NearMissesPerMinute = m.NearMissesPerMinute,
                Contacts = m.Contacts,
                HornPresses = m.HornPresses,
                YieldsToHorn = m.YieldsToHorn,
                Passengers = load.Count,
                BrakeWear = _sim.Bus.BrakeWear,
                Time = m.Time,
                DistanceMetres = m.DistanceMetres,
                PersonHit = m.PersonHit,
                AgentCount = agents.Count,
            };
        }
    }
}
