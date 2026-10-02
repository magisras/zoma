using System.Collections.Generic;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>
    /// The playtest numbers RESEARCH.md asks for: "follow distance, near misses per minute, stops
    /// lost, horn presses." Stops come with Milestone 4; the rest are counted here.
    /// </summary>
    public sealed class SimMetrics
    {
        public float Time;                       // simulated seconds
        public float DistanceMetres;             // player distance travelled
        public int HornPresses;
        public int YieldsToHorn;                 // agents that moved for the player's horn
        public int NearMisses;
        public int Contacts;                     // player scrapes, light and hard
        public int HardContacts;                 // the ones that cost money
        public int NpcPersonHits;                // NPCs hitting pedestrians (it happens; counted, not fatal)
        public int CaneRuns;                     // times the player crossed a closed stop line
        public int StopsLost;                    // a rival took a crowd the player was about to reach
        public float WrongSideSeconds;           // time spent on the oncoming carriageway
        public bool WrongSideNow;
        public bool PersonHit;                   // the player hit a person: the day is over
        public float GapAheadMetres = 999f;      // live: bumper to bumper
        public float HeadwayAheadSeconds = 99f;  // live: gap / speed
        public float MinHeadwaySeconds = 99f;

        public float NearMissesPerMinute => Time > 1f ? NearMisses / (Time / 60f) : 0f;
    }

    /// <summary>
    /// The world: corridors, junctions, one list of agents, one tuning table, one random stream.
    /// Steps the player's bus, every NPC and every pedestrian, resolves contacts, keeps the
    /// population up and records the metrics. Nothing here knows about rendering or input.
    /// </summary>
    public sealed class TrafficSim
    {
        /// <summary>The main road: the player's corridor.</summary>
        public readonly Corridor Corridor;
        /// <summary>The carriageway running the other way beside it, if any.</summary>
        public Corridor Oncoming;
        /// <summary>The player's stand-in on the oncoming carriageway: what its traffic sees coming.</summary>
        public Agent PlayerGhost;
        public readonly List<Corridor> Corridors = new List<Corridor>();
        public readonly List<Junction> Junctions = new List<Junction>();
        /// <summary>Police boxes beside the road (docs/STREET_CONTROL.md §3).</summary>
        public readonly List<Checkpoint> Checkpoints = new List<Checkpoint>();
        public readonly List<DemandZone> Zones = new List<DemandZone>();
        public readonly TuningTable Tuning;
        public readonly int Seed;
        public readonly SeededRandom Random;
        public readonly List<Agent> Agents = new List<Agent>();
        public readonly SimMetrics Metrics = new SimMetrics();
        public readonly BusController Bus = new BusController();
        public Economy Economy;
        public readonly FatigueState Fatigue = new FatigueState();
        /// <summary>The bus's wear and papers. Replace with the household's to carry it across days.</summary>
        public BusCondition Condition = new BusCondition();
        public readonly RolloverState Rollover = new RolloverState();
        public int Day = 1;
        public CrewVoice Voice;
        public Agent Player;

        private readonly InputDelay _inputDelay = new InputDelay();
        private PlayerInput _latestInput;
        private bool _inputsThisStep;

        private int _nextId = 1;
        private float _nearMissCooldown;
        private float _hornHeld;              // how long the player's horn has been held this press
        private Junction _playerRanCane;      // which closed junction the player is currently inside

        public TrafficSim(Corridor corridor, TuningTable tuning, int seed)
        {
            Corridor = corridor;
            Corridors.Add(corridor);
            Tuning = tuning;
            Seed = seed;
            Random = new SeededRandom(seed);
            Economy = new Economy(this);
            Voice = new CrewVoice(this);
        }

        /// <summary>Add a place where people wait for buses. Hot zones (near junctions, markets) fill faster.</summary>
        public DemandZone AddZone(string name, float s, bool hot)
        {
            PassengerSettings p = Tuning.Passengers;
            var zone = new DemandZone
            {
                Name = name,
                Index = Zones.Count,
                S = Corridor.Wrap(s),
                RatePerMinute = p.BaseRatePerMinute * (hot ? p.HotZoneRateMultiplier : 1f),
            };
            zone.Position = Corridor.PositionAt(zone.S, zone.Side * (Corridor.HalfWidth + Tuning.Spawn.KerbOffsetMetres + 0.5f));
            Zones.Add(zone);
            return zone;
        }

        /// <summary>
        /// Add a police box beside the main road. Whether a sergeant is on duty today is decided now,
        /// once per day, so the player can learn which boxes are quiet (and be wrong tomorrow).
        /// </summary>
        public Checkpoint AddCheckpoint(string name, float s)
        {
            var cp = new Checkpoint
            {
                Name = name,
                S = Corridor.Wrap(s),
                SergeantOnDuty = Random.Chance(Tuning.Economy.SergeantOnDutyChance),
            };
            Checkpoints.Add(cp);
            return cp;
        }

        /// <summary>
        /// Add the oncoming carriageway. Its traffic reasons on its own corridor; the player appears on
        /// it as a ghost agent with the same body, moving backwards relative to that road's direction.
        /// </summary>
        public void SetOncoming(Corridor oncoming)
        {
            Oncoming = oncoming;
            if (!Corridors.Contains(oncoming)) Corridors.Add(oncoming);
            if (Player != null) MakeGhost();
        }

        private void MakeGhost()
        {
            PlayerGhost = new Agent
            {
                Id = _nextId++,
                Corridor = Oncoming,
                Class = VehicleClass.Bus,
                Shape = Player.Shape,
                Mass = Player.Mass,
                Nerve = Player.Nerve,
                GhostOf = Player,
            };
            Agents.Add(PlayerGhost);
            SyncGhost();
        }

        /// <summary>Put the ghost where the player is, in the oncoming road's coordinates.</summary>
        private void SyncGhost()
        {
            Agent g = PlayerGhost;
            Oncoming.Project(Player.Position, out g.S, out g.Lateral);
            g.Position = Player.Position;
            g.Yaw = Player.Yaw;
            Vector3 forward = new Vector3(Mathf.Sin(Player.Yaw), 0f, Mathf.Cos(Player.Yaw));
            g.Speed = Player.Speed * Vector3.Dot(forward, Oncoming.TangentAt(g.S));   // negative when driving against the flow
            g.TargetLateral = g.Lateral;
            g.HornTimer = Player.HornTimer;
        }

        /// <summary>Add a cross street with an officer where it meets the main road.</summary>
        public Junction AddJunction(float mainS, Corridor cross, float crossS)
        {
            var junction = new Junction(Corridor, mainS, cross, crossS);
            junction.Timer = Random.Range(Tuning.Officer.OpenMinSeconds, Tuning.Officer.OpenMaxSeconds);
            Junctions.Add(junction);
            if (!Corridors.Contains(cross)) Corridors.Add(cross);
            return junction;
        }

        // ---------------------------------------------------------------- spawning

        public Agent SpawnPlayerBus(float s, float lateral)
        {
            Agent bus = NewVehicle(Corridor, VehicleClass.Bus, s, lateral, 0.5f);
            bus.IsPlayer = true;
            bus.Speed = 0f;
            bus.Load = new BusLoad();
            Condition.BrakeWear = Tuning.Bus.StartingBrakeWear;
            Bus.BrakeWear = Condition.BrakeWear;
            Player = bus;
            SetPassengerCount(bus, Tuning.Bus.StartingPassengers);
            if (Oncoming != null && PlayerGhost == null) MakeGhost();
            return bus;
        }

        /// <summary>Fill or empty a bus with anonymous riders (the sandbox slider; the start of a shift).</summary>
        public void SetPassengerCount(Agent bus, int count)
        {
            BusLoad load = bus.Load;
            while (load.Aboard.Count > count) load.Aboard.RemoveAt(load.Aboard.Count - 1);
            while (load.Aboard.Count < count)
            {
                load.Aboard.Add(new Passenger
                {
                    Kind = PassengerKind.Regular,
                    BoardingSeconds = Tuning.Passengers.BoardingMinSeconds,
                    DestinationZone = Zones.Count > 0 ? Random.Range(0, Zones.Count) : 0,
                    Paid = true,
                });
            }
            if (bus.IsPlayer) Bus.Passengers = load.Count;
        }

        public Agent SpawnVehicle(VehicleClass vehicleClass, float s, float lateral, float nerve)
        {
            return NewVehicle(Corridor, vehicleClass, s, lateral, nerve);
        }

        /// <summary>
        /// A named crew of the player's own company: persistent, full decision layer with memory.
        /// RESEARCH.md: "Own company (2 buses): persistent named crews, same livery as the player's."
        /// </summary>
        public Agent SpawnRivalBus(string crewName, DriverPersonality personality, float s, float lateral, bool ownCompany = true)
        {
            Agent bus = NewVehicle(Corridor, VehicleClass.Bus, s, lateral, personality.Nerve);
            bus.Persistent = true;
            bus.Load = new BusLoad();
            bus.Brain = new RivalBrain
            {
                CrewName = crewName,
                Personality = personality,
                OwnCompany = ownCompany,
                BaseNerve = personality.Nerve,
                BaseCruise = bus.Shape.CruiseSpeed,
            };
            bus.DesiredSpeed = bus.Shape.CruiseSpeed;
            return bus;
        }

        public Agent SpawnVehicle(Corridor corridor, VehicleClass vehicleClass, float s, float lateral, float nerve)
        {
            return NewVehicle(corridor, vehicleClass, s, lateral, nerve);
        }

        /// <summary>
        /// Write what the named crews feel into the household, where it outlives this sim. Call before
        /// the day's world is thrown away (RESEARCH.md: own-company crews are persistent, with memory).
        /// </summary>
        public void RememberCrews(Household household)
        {
            for (int i = 0; i < Agents.Count; i++)
            {
                Agent a = Agents[i];
                if (a.Brain != null) household.RememberCrew(a.Brain.CrewName, a.Brain.Grudge);
            }
        }

        /// <summary>Give the named crews back what they felt, cooled by the nights since. Call after spawning them.</summary>
        public void RecallCrews(Household household)
        {
            for (int i = 0; i < Agents.Count; i++)
            {
                Agent a = Agents[i];
                if (a.Brain != null) a.Brain.Grudge = household.RecallCrew(a.Brain.CrewName, Tuning.Memory);
            }
        }

        /// <param name="side">−1 = left kerb, +1 = right kerb.</param>
        public Agent SpawnPedestrian(float s, int side)
        {
            Agent p = new Agent
            {
                Id = _nextId++,
                Corridor = Corridor,
                Class = VehicleClass.Pedestrian,
                Shape = VehicleShape.For(VehicleClass.Pedestrian),
                Mass = Tuning.Mass.Pedestrian,
                Nerve = Random.Value,
                S = Corridor.Wrap(s),
                Lateral = (Corridor.HalfWidth + Tuning.Spawn.KerbOffsetMetres) * side,
                WaitTimer = Random.Range(0f, Tuning.Pedestrians.WaitMaxSeconds),
            };
            p.Position = Corridor.PositionAt(p.S, p.Lateral);
            p.Yaw = Corridor.YawAt(p.S);
            Agents.Add(p);
            return p;
        }

        private Agent NewVehicle(Corridor corridor, VehicleClass vehicleClass, float s, float lateral, float nerve)
        {
            VehicleShape shape = VehicleShape.For(vehicleClass);
            Agent a = new Agent
            {
                Id = _nextId++,
                Corridor = corridor,
                Class = vehicleClass,
                Shape = shape,
                Mass = Tuning.Mass.Of(vehicleClass),
                Nerve = Mathf.Clamp01(nerve),
                S = corridor.Wrap(s),
                Lateral = lateral,
                TargetLateral = lateral,
                Speed = shape.CruiseSpeed * 0.8f,
                // A wide spread: some dawdle, some race. That spread is what makes overtaking happen.
                DesiredSpeed = shape.CruiseSpeed * Random.Range(0.6f, 1.25f),
            };
            a.Position = corridor.PositionAt(a.S, a.Lateral);
            a.Yaw = corridor.YawAt(a.S);
            Agents.Add(a);
            return a;
        }

        // ---------------------------------------------------------------- the step

        public void Step(float dt)
        {
            if (Metrics.PersonHit && Economy.DayOver) return;   // the day ended; nothing moves until a reset
            if (Economy.DayOver) return;
            Metrics.Time += dt;

            for (int i = 0; i < Junctions.Count; i++) Junctions[i].Tick(dt, Tuning.Officer, Random);
            CountBoxes();

            Boarding.TickZones(this, dt);

            if (Player != null)
            {
                Fatigue.Step(dt, Tuning.Fatigue, Tuning.Economy, Random);
                ApplyPlayerInputs(dt);
                Bus.Passengers = Player.Load.Count;
                Bus.HeldByRope = RopeAhead(Player);
                // The sandbox day stands for a whole day's driving: wear is scaled like the money is.
                Condition.Brake(Bus.Held ? 0f : Bus.Brake, Player.Speed, dt / Mathf.Max(0.01f, Tuning.Economy.MoneyScale), Tuning.Bus);
                Bus.BrakeWear = Condition.BrakeWear;
                Bus.Step(Player, Corridor, Tuning.Bus, dt);
                Metrics.DistanceMetres += Player.Speed * dt;
                Player.HornTimer = Mathf.Max(0f, Player.HornTimer - dt);
                WatchPlayerAtJunctions();
                WatchPlayerAtZones();
                if (PlayerGhost != null)
                {
                    SyncGhost();
                    // Beyond the middle of the median you are on their road.
                    Metrics.WrongSideNow = Player.Lateral > Corridor.HalfWidth + Tuning.Spawn.MedianMetres * 0.5f;
                    if (Metrics.WrongSideNow) Metrics.WrongSideSeconds += dt;
                }
                Core.Rollover.Check(this, dt);      // after the ghost knows which road we are on
            }

            for (int i = 0; i < Agents.Count; i++)
            {
                Agent a = Agents[i];
                if (a.IsPlayer || a.GhostOf != null) continue;
                if (a.IsPedestrian) { Pedestrians.Step(this, a, dt); continue; }
                if (a.Brain != null) RivalAI.Step(this, a, dt);
                else if (a.Class == VehicleClass.Bus) RaceWhenNear(a);
                Steering.Drive(this, a, dt);
            }
            HoldAtRopes();
            WatchStopsLost();

            for (int i = 0; i < Agents.Count; i++)
            {
                if (Agents[i].Load != null) Boarding.Step(this, Agents[i], dt);
            }

            ResolveContacts();
            if (Player != null) UpdatePlayerMetrics(dt);
            MaintainPopulation();
            Economy.Step(dt);
            Economy.SyncFares();
            Voice.Step(dt);
            if (Economy.DayOver && !_terminalSaid) { _terminalSaid = true; TerminalScene(); }
        }

        private bool _terminalSaid;

        /// <summary>At the terminal, each crew says one thing, chosen by the day and the grudge.</summary>
        private void TerminalScene()
        {
            for (int i = 0; i < Agents.Count; i++)
            {
                Agent a = Agents[i];
                if (a.Brain == null || !a.Brain.OwnCompany) continue;
                Economy.Ledger.Log(Voice.TerminalLine(a.Brain, Economy.Ledger));
            }
        }

        /// <summary>
        /// Other-company buses have no memory and no plan: they just speed up when another bus is
        /// near (RESEARCH.md, "simple race-when-near logic").
        /// </summary>
        private void RaceWhenNear(Agent bus)
        {
            float range = Tuning.Utility.RaceWhenNearMetres;
            bool near = false;
            for (int i = 0; i < Agents.Count && !near; i++)
            {
                Agent other = Agents[i];
                if (other == bus || other.Class != VehicleClass.Bus || other.Corridor != bus.Corridor) continue;
                near = Mathf.Abs(Corridor.DeltaS(bus.S, other.S)) < range;
            }
            bus.DesiredSpeed = bus.Shape.CruiseSpeed * (near ? Tuning.Utility.RaceWhenNearFactor : 1f);
        }

        private readonly System.Collections.Generic.Dictionary<DemandZone, float> _rivalTookAt = new System.Collections.Generic.Dictionary<DemandZone, float>();

        /// <summary>
        /// "Arriving second at a stop earns almost nothing." When a rival's door takes people at a zone
        /// the player is approaching from close behind, that stop is lost. Counted once per zone visit.
        /// </summary>
        private void WatchStopsLost()
        {
            if (Player == null) return;
            for (int i = 0; i < Agents.Count; i++)
            {
                Agent rival = Agents[i];
                if (rival.Brain == null || rival.Load == null || rival.Load.AtDoor == null) continue;
                DemandZone zone = Boarding.ZoneInReach(this, rival);
                if (zone == null) continue;
                float ds = Corridor.DeltaS(Player.S, zone.S);
                if (ds > 0f && ds < Tuning.Utility.RivalCloseMetres)
                {
                    float last;
                    if (!_rivalTookAt.TryGetValue(zone, out last) || Metrics.Time - last > 60f)
                    {
                        Metrics.StopsLost++;
                    }
                    _rivalTookAt[zone] = Metrics.Time;
                }
            }
        }

        /// <summary>The nearest own-company bus ahead and behind, for the helper's gap report. Null if none.</summary>
        public Agent OwnBusAhead(out float metres)
        {
            return OwnBus(true, out metres);
        }

        public Agent OwnBusBehind(out float metres)
        {
            return OwnBus(false, out metres);
        }

        private Agent OwnBus(bool ahead, out float metres)
        {
            Agent best = null;
            metres = float.MaxValue;
            if (Player == null) return null;
            for (int i = 0; i < Agents.Count; i++)
            {
                Agent a = Agents[i];
                if (a.Brain == null || !a.Brain.OwnCompany || a.Corridor != Player.Corridor) continue;
                float ds = Corridor.DeltaS(Player.S, a.S);
                if (ahead ? ds <= 0f : ds >= 0f) continue;
                float d = Mathf.Abs(ds);
                if (d < metres) { metres = d; best = a; }
            }
            return best;
        }

        /// <summary>
        /// The player's hands, this frame. They reach the bus after the fatigue reaction delay, and
        /// not at all during a micro-sleep (the last hands stay where they were). Call before Step.
        /// </summary>
        public void PlayerInputs(PlayerInput input)
        {
            _latestInput = input;
            _inputsThisStep = true;
        }

        /// <summary>
        /// Apply the delayed, possibly frozen, inputs to the bus, the horn and the door. Only on a step
        /// that was given inputs: tests and the autopilot drive the bus directly and must not be
        /// overwritten by a stale frame.
        /// </summary>
        private void ApplyPlayerInputs(float dt)
        {
            if (!_inputsThisStep) return;
            _inputsThisStep = false;
            _inputDelay.Push(Metrics.Time, _latestInput);
            PlayerInput applied = _inputDelay.At(Metrics.Time - Fatigue.ReactionDelay(Tuning.Fatigue));
            if (Fatigue.Asleep) applied = Fatigue.Frozen;
            else Fatigue.Frozen = applied;

            Bus.Throttle = applied.Throttle;
            Bus.Brake = applied.Brake;
            Bus.Steer = applied.Steer;
            HornInput(applied.Horn, dt);
            SetDoor(applied.Door);
        }

        /// <summary>Open or close the player's door (the helper's job). Opening stamps the time: first door wins the crowd.</summary>
        public void SetDoor(bool open)
        {
            if (Player == null || Player.Load.DoorOpen == open) return;
            Player.Load.DoorOpen = open;
            if (open) Player.Load.DoorOpenedAt = Metrics.Time;
        }

        private DemandZone _playerZone;        // the zone the player is currently within reach of
        private bool _playerSlowedAtZone;

        /// <summary>Notice the player passing a zone without slowing: riders who wanted off stay on.</summary>
        private void WatchPlayerAtZones()
        {
            DemandZone zone = Boarding.ZoneInReach(this, Player);
            if (zone != _playerZone)
            {
                if (_playerZone != null && !_playerSlowedAtZone) Boarding.NoteMissedAlights(this, Player, _playerZone);
                _playerZone = zone;
                _playerSlowedAtZone = false;
            }
            if (zone != null && Player.Speed <= Tuning.Passengers.DoorSpeedMs && Player.Load.DoorOpen) _playerSlowedAtZone = true;
        }

        /// <summary>
        /// Call every frame with whether the horn key is down. A press is a tap; holding it past the
        /// threshold turns it into a blast that repeats while held.
        /// </summary>
        public void HornInput(bool held, float dt)
        {
            if (Player == null) return;
            HornSettings horn = Tuning.Horn;
            if (!held)
            {
                _hornHeld = 0f;
                return;
            }
            bool firstFrame = _hornHeld == 0f;
            float before = _hornHeld;
            _hornHeld += dt;

            if (firstFrame)
            {
                Metrics.HornPresses++;
                Metrics.YieldsToHorn += HornSystem.Broadcast(this, Player, horn.TapStrength);
            }
            else if (_hornHeld >= horn.BlastThresholdSeconds)
            {
                // Re-broadcast the blast twice a second while held.
                float period = 0.5f;
                if (Mathf.Floor((before - horn.BlastThresholdSeconds) / period) < Mathf.Floor((_hornHeld - horn.BlastThresholdSeconds) / period))
                {
                    Metrics.YieldsToHorn += HornSystem.Broadcast(this, Player, horn.BlastStrength);
                }
            }
        }

        // ---------------------------------------------------------------- junctions

        /// <summary>
        /// Distance from this agent's front to the nearest stop line it must respect, or
        /// <paramref name="lookAhead"/> if none. A line must be respected when the cane is against
        /// you (unless you are one of this phase's leakers) or when the other stream is physically
        /// in the box. Once past the line you are committed and nothing holds you.
        /// </summary>
        public float StopDistanceAhead(Agent a, float lookAhead)
        {
            return StopDistanceAhead(a, lookAhead, false);
        }

        /// <param name="ignoreCane">True for a driver who only respects what physically blocks the box.</param>
        public float StopDistanceAhead(Agent a, float lookAhead, bool ignoreCane)
        {
            float nearest = lookAhead;
            OfficerSettings officer = Tuning.Officer;
            for (int i = 0; i < Junctions.Count; i++)
            {
                Junction j = Junctions[i];
                float lineS = j.StopLineOn(a.Corridor, officer.StopLineSetbackMetres);
                if (float.IsNaN(lineS)) continue;

                float ds = a.Corridor.DeltaS(a.S, lineS) - a.HalfLength;
                if (ds > lookAhead) continue;
                if (ds < -(officer.StopLineSetbackMetres + 0.5f))
                {
                    // Nose in the box: committed. Forget a leak once clear of the box.
                    if (a.LeakingThrough == j && !j.InBox(a.Corridor, a.S, a.HalfLength)) a.LeakingThrough = null;
                    continue;
                }

                bool caneAgainst = !j.IsOpenFor(a.Corridor);
                bool boxFull = j.BoxBlockedFor(a.Corridor);
                if (boxFull)
                {
                    nearest = Mathf.Min(nearest, ds);   // physics, not politeness
                    continue;
                }
                if (!caneAgainst || ignoreCane) continue;
                if (a.LeakingThrough == j) continue;    // already decided to run it

                // The cane just dropped and I'm nearly there: a few of us go anyway.
                if (j.LeakersLeft > 0 && ds < officer.LeakZoneMetres && a.Speed > 1f)
                {
                    j.LeakersLeft--;
                    a.LeakingThrough = j;
                    continue;
                }
                nearest = Mathf.Min(nearest, ds);
            }
            return nearest;
        }

        private void CountBoxes()
        {
            for (int i = 0; i < Junctions.Count; i++)
            {
                Junction j = Junctions[i];
                j.MainInBox = 0;
                j.CrossInBox = 0;
                for (int k = 0; k < Agents.Count; k++)
                {
                    Agent a = Agents[k];
                    if (a.IsPedestrian) continue;
                    if (a.Corridor == j.Main && j.InBox(j.Main, a.S, a.HalfLength)) j.MainInBox++;
                    else if (a.Corridor == j.Cross && j.InBox(j.Cross, a.S, a.HalfLength)) j.CrossInBox++;
                }
            }
        }

        /// <summary>
        /// The rope is physical: a vehicle whose nose reaches a roped, closed stop line is put back at
        /// the line and stopped, however late it braked. Without this a car arriving fast "commits" to
        /// the box the way it does at a bare cane; with a rope there is nothing to commit through.
        /// </summary>
        private void HoldAtRopes()
        {
            OfficerSettings officer = Tuning.Officer;
            for (int i = 0; i < Junctions.Count; i++)
            {
                Junction j = Junctions[i];
                if (!j.Roped) continue;
                for (int k = 0; k < Agents.Count; k++)
                {
                    Agent a = Agents[k];
                    if (a.IsPedestrian || a.IsPlayerOrGhost) continue;
                    if (j.IsOpenFor(a.Corridor)) continue;
                    float lineS = j.StopLineOn(a.Corridor, officer.StopLineSetbackMetres);
                    if (float.IsNaN(lineS)) continue;
                    float ds = a.Corridor.DeltaS(a.S, lineS) - a.HalfLength;   // nose to rope
                    if (ds >= 0f || ds < -(officer.StopLineSetbackMetres + j.MainHalfSpan)) continue;   // short of it, or already through
                    a.S = a.Corridor.Wrap(lineS - a.HalfLength);
                    a.Position = a.Corridor.PositionAt(a.S, a.Lateral);
                    a.Speed = 0f;
                    a.LeakingThrough = null;
                }
            }
        }

        /// <summary>
        /// Is a constable's rope stretched across the road within the bus's stopping distance? The rope
        /// is the one thing on the street that stops the player's bus without a UI: nobody drives
        /// through a rope with a man holding each end. The bus brakes as if the pedal were down, and
        /// its nose is held at the line until the cane turns.
        /// </summary>
        private bool RopeAhead(Agent bus)
        {
            OfficerSettings officer = Tuning.Officer;
            BusSettings b = Tuning.Bus;
            float decel = b.BrakeDecelNewMs2 * (1f - b.BrakeWearLoss * Mathf.Clamp01(Bus.BrakeWear));
            float stopping = bus.Speed * bus.Speed / (2f * Mathf.Max(0.5f, decel)) + 1.5f;
            for (int i = 0; i < Junctions.Count; i++)
            {
                Junction j = Junctions[i];
                if (j.Main != bus.Corridor || !j.Roped || j.IsOpenFor(bus.Corridor)) continue;
                float lineS = j.StopLineOn(bus.Corridor, officer.StopLineSetbackMetres);
                float ds = bus.Corridor.DeltaS(bus.S, lineS) - bus.HalfLength;   // nose to rope
                if (ds < -1f) continue;             // already through: the rope went up behind us
                if (ds <= 0.3f) bus.Speed = 0f;     // nose at the rope: it does not give
                if (ds <= stopping) return true;
            }
            return false;
        }

        /// <summary>Count the player crossing a closed stop line (the sergeant will care later).</summary>
        private void WatchPlayerAtJunctions()
        {
            for (int i = 0; i < Junctions.Count; i++)
            {
                Junction j = Junctions[i];
                if (j.Main != Player.Corridor) continue;
                bool inBox = j.InBox(j.Main, Player.S, Player.HalfLength);
                if (inBox && _playerRanCane == null && !j.IsOpenFor(Player.Corridor))
                {
                    Metrics.CaneRuns++;
                    _playerRanCane = j;
                }
                if (!inBox && _playerRanCane == j) _playerRanCane = null;
            }
        }

        /// <summary>
        /// A random stand or junction on the main road between these two S values, or NaN if none.
        /// </summary>
        private float NearestCrowdPoint(float fromS, float toS)
        {
            float best = float.NaN;
            int seen = 0;
            for (int i = 0; i < Zones.Count; i++) seen = PickCrowdPoint(Zones[i].S, fromS, toS, seen, ref best);
            for (int i = 0; i < Junctions.Count; i++)
                if (Junctions[i].Main == Corridor) seen = PickCrowdPoint(Junctions[i].MainS, fromS, toS, seen, ref best);
            return best;
        }

        /// <summary>Reservoir sampling of one point: every candidate in the window has an equal chance.</summary>
        private int PickCrowdPoint(float pointS, float fromS, float toS, int seen, ref float best)
        {
            float ds = Corridor.DeltaS(fromS, pointS);
            if (ds < 0f || ds > toS - fromS) return seen;
            seen++;
            if (Random.Range(0, seen) == 0) best = pointS;
            return seen;
        }

        // ---------------------------------------------------------------- contacts

        /// <summary>
        /// Same corridor: boxes overlapping in corridor coordinates. Different corridors (only
        /// possible inside a junction): capsules in world space. Vehicle on vehicle is a scrape:
        /// counted, the one behind loses speed, the lighter one is pushed clear. Vehicle on person
        /// is the one hard rule.
        /// </summary>
        private void ResolveContacts()
        {
            for (int i = 0; i < Agents.Count; i++)
            {
                Agent a = Agents[i];
                for (int j = i + 1; j < Agents.Count; j++)
                {
                    Agent b = Agents[j];
                    if (a.IsPedestrian && b.IsPedestrian) continue;
                    if (a.Corridor == b.Corridor) ResolveSameCorridor(a, b);
                    else if (a.GhostOf != null || b.GhostOf != null) continue;          // the ghost meets its road's traffic only
                    else if (IsParallel(a.Corridor, b.Corridor)) continue;              // main vs oncoming: handled through the ghost
                    else if (!a.IsPedestrian && !b.IsPedestrian) ResolveCrossCorridor(a, b);
                }
            }
        }

        private bool IsParallel(Corridor x, Corridor y)
        {
            return (x == Corridor && y == Oncoming) || (x == Oncoming && y == Corridor);
        }

        private void ResolveSameCorridor(Agent a, Agent b)
        {
            float ds = a.Corridor.DeltaS(a.S, b.S);
            float dLat = b.Lateral - a.Lateral;
            float overlapS = a.HalfLength + b.HalfLength - Mathf.Abs(ds);
            float overlapLat = a.HalfWidth + b.HalfWidth - Mathf.Abs(dLat);
            if (overlapS <= 0f || overlapLat <= 0f) return;

            if (a.IsPedestrian || b.IsPedestrian)
            {
                Agent vehicle = a.IsPedestrian ? b : a;
                if (Mathf.Abs(vehicle.Speed) < 0.5f) return;   // nudged at walking pace: nothing
                if (vehicle.IsPlayerOrGhost) Metrics.PersonHit = true;
                else Metrics.NpcPersonHits++;
                return;
            }

            CountScrape(a, b);

            // The one behind can't go faster than the one in front.
            Agent behind = ds > 0f ? a : b;
            Agent front = ds > 0f ? b : a;
            behind.Speed = Mathf.Min(behind.Speed, front.Speed);

            // Separate along whichever axis penetrates least. The lighter one moves: a
            // sideswipe pushes it aside, a rear-ender shoves it forward (or holds it back).
            Agent light = a.Mass <= b.Mass ? a : b;
            Agent heavy = light == a ? b : a;
            if (overlapLat <= overlapS)
            {
                float push = (overlapLat + 0.05f) * (light.Lateral >= heavy.Lateral ? 1f : -1f);
                light.Lateral += push;
                light.TargetLateral = light.Lateral;
                if (light.IsPlayerOrGhost) RealPlayer(light).Position += light.Corridor.RightAt(light.S) * push;
            }
            else
            {
                bool lightInFront = light.Corridor.DeltaS(heavy.S, light.S) >= 0f;
                float push = (overlapS + 0.05f) * (lightInFront ? 1f : -1f);
                light.S = light.Corridor.Wrap(light.S + push);
                if (lightInFront) light.Speed = Mathf.Max(light.Speed, heavy.Speed);   // shoved along
                else light.Speed = Mathf.Min(light.Speed, heavy.Speed);               // held back
                if (light.IsPlayerOrGhost)
                {
                    Agent real = RealPlayer(light);
                    real.Position += light.Corridor.TangentAt(light.S) * push;
                    if (light.GhostOf != null) real.Speed = Mathf.Min(real.Speed, Mathf.Abs(heavy.Speed) + 0.5f);   // a head-on hit stops you
                }
            }
        }

        private void ResolveCrossCorridor(Agent a, Agent b)
        {
            // Cheap reject: far apart in the world.
            if ((a.Position - b.Position).sqrMagnitude > 400f) return;

            Geometry.Axis(a, out Vector3 ra, out Vector3 fa);
            Geometry.Axis(b, out Vector3 rb, out Vector3 fb);
            float distance = Geometry.SegmentDistance(ra, fa, rb, fb, out Vector3 ca, out Vector3 cb);
            float penetration = a.HalfWidth + b.HalfWidth - distance;
            if (penetration <= 0f) return;

            CountScrape(a, b);

            // Both stop: a box jam. The lighter one is pushed out along the shortest line.
            a.Speed = Mathf.Min(a.Speed, 0.5f);
            b.Speed = Mathf.Min(b.Speed, 0.5f);
            Agent light = a.Mass <= b.Mass ? a : b;
            Vector3 away = light == a ? ca - cb : cb - ca;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-6f) away = light.Corridor.RightAt(light.S);
            light.Position += away.normalized * (penetration + 0.05f);
            light.Corridor.Project(light.Position, out light.S, out light.Lateral);
            light.TargetLateral = light.Lateral;
        }

        private static Agent RealPlayer(Agent a)
        {
            return a.GhostOf ?? a;
        }

        private void CountScrape(Agent a, Agent b)
        {
            // Count once per pair per couple of seconds.
            if (Metrics.Time - a.LastContactTime > 2f || Metrics.Time - b.LastContactTime > 2f)
            {
                if (a.IsPlayerOrGhost || b.IsPlayerOrGhost)
                {
                    Metrics.Contacts++;
                    // How hard: the speed difference along the road (a head-on ghost has a negative speed, so it adds up).
                    float relative = a.Corridor == b.Corridor ? Mathf.Abs(a.Speed - b.Speed) : Mathf.Abs(a.Speed) + Mathf.Abs(b.Speed);
                    if (Economy.OnScrape(relative)) Metrics.HardContacts++;
                    Condition.Dents++;
                    // A scrape with a named crew is remembered.
                    Agent other = a.IsPlayerOrGhost ? b : a;
                    if (other.Brain != null) RivalAI.ChangeGrudge(other.Brain, Tuning.Memory.GrudgeWhenCutOff, Tuning.Memory);
                }
            }
            a.LastContactTime = b.LastContactTime = Metrics.Time;
        }

        // ---------------------------------------------------------------- metrics

        private void UpdatePlayerMetrics(float dt)
        {
            _nearMissCooldown = Mathf.Max(0f, _nearMissCooldown - dt);
            Steering.FindAhead(Agents, Player, Player.Lateral, 200f, out float gap);
            Metrics.GapAheadMetres = gap;
            Metrics.HeadwayAheadSeconds = Player.Speed > 0.5f ? gap / Player.Speed : 99f;
            if (Player.Speed > 3f)
            {
                Metrics.MinHeadwaySeconds = Mathf.Min(Metrics.MinHeadwaySeconds, Metrics.HeadwayAheadSeconds);
                if (Metrics.HeadwayAheadSeconds < 0.3f && _nearMissCooldown <= 0f)
                {
                    Metrics.NearMisses++;
                    _nearMissCooldown = 2f;
                }
            }
        }

        // ---------------------------------------------------------------- population

        /// <summary>
        /// Keep traffic and pedestrians around the player on the main road, and a stream on every
        /// cross street: recycle what fell far behind or ran off the end, spawn into free space.
        /// </summary>
        private void MaintainPopulation()
        {
            if (Player == null) return;
            SpawnSettings spawn = Tuning.Spawn;
            float ps = Player.S;

            int vehicles = 0, pedestrians = 0, oncoming = 0;
            var crossCounts = new Dictionary<Corridor, int>();
            for (int i = Agents.Count - 1; i >= 0; i--)
            {
                Agent a = Agents[i];
                if (a.IsPlayer || a.Persistent || a.GhostOf != null) continue;
                bool gone;
                if (a.Corridor == Oncoming)
                {
                    float ds = Oncoming.DeltaS(PlayerGhost.S, a.S);
                    gone = Mathf.Abs(ds) > Tuning.Performance.DespawnRadiusMetres;
                    if (!gone) oncoming++;
                }
                else if (a.Corridor != Corridor)
                {
                    gone = a.S > a.Corridor.Length - 5f;
                    if (!gone) crossCounts[a.Corridor] = (crossCounts.ContainsKey(a.Corridor) ? crossCounts[a.Corridor] : 0) + 1;
                }
                else
                {
                    float ds = Corridor.DeltaS(ps, a.S);
                    if (a.IsPedestrian)
                    {
                        gone = ds < -spawn.PedestrianBehindMetres - 20f || ds > spawn.SpawnAheadMetres + 50f;
                        if (!gone) pedestrians++;
                    }
                    else
                    {
                        gone = ds < -Tuning.Performance.DespawnRadiusMetres
                            || ds > spawn.SpawnAheadMetres + 150f
                            || (!Corridor.Closed && a.S > Corridor.Length - 5f);
                        if (!gone) vehicles++;
                    }
                }
                if (gone) Agents.RemoveAt(i);
            }

            for (int attempt = 0; vehicles < spawn.VehiclesAround && attempt < 10; attempt++)
            {
                if (TrySpawnVehicleNear(ps)) vehicles++;
            }
            for (int attempt = 0; pedestrians < spawn.PedestriansAround && attempt < 10; attempt++)
            {
                float s = ps + Random.Range(-spawn.PedestrianBehindMetres, spawn.SpawnAheadMetres);
                // Most people cross where the people are: by a stand or a junction, not along a blank wall.
                if (Random.Chance(spawn.PedestrianClusterShare))
                {
                    float crowd = NearestCrowdPoint(ps - spawn.PedestrianBehindMetres, ps + spawn.SpawnAheadMetres);
                    if (!float.IsNaN(crowd)) s = crowd + Random.Range(-spawn.PedestrianClusterMetres, spawn.PedestrianClusterMetres);
                }
                if (Corridor.Closed || (s > 5f && s < Corridor.Length - 5f))
                {
                    SpawnPedestrian(s, Random.Chance(0.5f) ? -1 : 1);
                    pedestrians++;
                }
            }

            if (Oncoming != null)
            {
                int want = Mathf.RoundToInt(spawn.VehiclesAround * spawn.OncomingFraction);
                for (int attempt = 0; oncoming < want && attempt < 10; attempt++)
                {
                    // Their "ahead" is where they come from: both sides of the ghost, like the main road.
                    float s = PlayerGhost.S + (Random.Chance(0.5f) ? 1f : -1f) * Random.Range(spawn.SpawnClearanceMetres, spawn.SpawnAheadMetres);
                    if (TrySpawnOn(Oncoming, s)) oncoming++;
                }
            }

            for (int i = 0; i < Junctions.Count; i++)
            {
                Corridor cross = Junctions[i].Cross;
                if (cross == Oncoming) continue;
                int have = crossCounts.ContainsKey(cross) ? crossCounts[cross] : 0;
                for (int attempt = 0; have < spawn.CrossVehiclesPerJunction && attempt < 5; attempt++)
                {
                    if (TrySpawnOn(cross, Random.Range(5f, 40f))) have++;
                }
            }
        }

        private bool TrySpawnVehicleNear(float playerS)
        {
            SpawnSettings spawn = Tuning.Spawn;
            // Mostly ahead (that is where the player drives into), some behind (that is who presses you).
            float s = Random.Chance(0.7f)
                ? playerS + Random.Range(spawn.SpawnClearanceMetres, spawn.SpawnAheadMetres)
                : playerS - Random.Range(spawn.SpawnClearanceMetres, spawn.SpawnBehindMetres);
            if (!Corridor.Closed && (s < 5f || s > Corridor.Length - 30f)) return false;
            return TrySpawnOn(Corridor, s);
        }

        /// <summary>Spawn a random-class vehicle at s if that patch of road is free.</summary>
        private bool TrySpawnOn(Corridor corridor, float s)
        {
            s = corridor.Wrap(s);
            VehicleClass vehicleClass = PickClass();
            VehicleShape shape = VehicleShape.For(vehicleClass);
            float edge = corridor.HalfWidth - shape.Width * 0.5f;
            float lateral = Random.Range(-edge, edge);

            // Only into free road: nobody within a few lengths in that band.
            for (int i = 0; i < Agents.Count; i++)
            {
                Agent other = Agents[i];
                if (other.Corridor != corridor) continue;
                if (Mathf.Abs(other.Lateral - lateral) < other.HalfWidth + shape.Width * 0.5f + Steering.LateralMargin
                    && Mathf.Abs(corridor.DeltaS(s, other.S)) < other.HalfLength + shape.Length * 0.5f + 8f)
                {
                    return false;
                }
            }

            Agent a = NewVehicle(corridor, vehicleClass, s, lateral, 0.5f);
            a.Nerve = Mathf.Clamp01(Tuning.Nerve.DefaultNerve + Random.Range(-Tuning.Nerve.RandomSpread, Tuning.Nerve.RandomSpread));
            return true;
        }

        private VehicleClass PickClass()
        {
            SpawnSettings w = Tuning.Spawn;
            float total = w.RickshawWeight + w.CngWeight + w.CarWeight + w.BusWeight + w.TruckWeight;
            float roll = Random.Range(0f, total);
            if ((roll -= w.RickshawWeight) < 0f) return VehicleClass.Rickshaw;
            if ((roll -= w.CngWeight) < 0f) return VehicleClass.Cng;
            if ((roll -= w.CarWeight) < 0f) return VehicleClass.Car;
            if ((roll -= w.BusWeight) < 0f) return VehicleClass.Bus;
            return VehicleClass.Truck;
        }
    }
}
