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
        public int Contacts;                     // player scrapes
        public int NpcPersonHits;                // NPCs hitting pedestrians (it happens; counted, not fatal)
        public bool PersonHit;                   // the player hit a person: the day is over
        public float GapAheadMetres = 999f;      // live: bumper to bumper
        public float HeadwayAheadSeconds = 99f;  // live: gap / speed
        public float MinHeadwaySeconds = 99f;

        public float NearMissesPerMinute => Time > 1f ? NearMisses / (Time / 60f) : 0f;
    }

    /// <summary>
    /// The world: one corridor, one list of agents, one tuning table, one random stream. Steps the
    /// player's bus, every NPC and every pedestrian, resolves contacts, keeps the population up and
    /// records the metrics. Nothing here knows about rendering or input.
    /// </summary>
    public sealed class TrafficSim
    {
        public readonly Corridor Corridor;
        public readonly TuningTable Tuning;
        public readonly SeededRandom Random;
        public readonly List<Agent> Agents = new List<Agent>();
        public readonly SimMetrics Metrics = new SimMetrics();
        public readonly BusController Bus = new BusController();
        public Agent Player;

        private int _nextId = 1;
        private float _nearMissCooldown;
        private float _hornHeld;              // how long the player's horn has been held this press

        public TrafficSim(Corridor corridor, TuningTable tuning, int seed)
        {
            Corridor = corridor;
            Tuning = tuning;
            Random = new SeededRandom(seed);
        }

        // ---------------------------------------------------------------- spawning

        public Agent SpawnPlayerBus(float s, float lateral)
        {
            Agent bus = NewVehicle(VehicleClass.Bus, s, lateral, 0.5f);
            bus.IsPlayer = true;
            bus.Speed = 0f;
            Bus.Passengers = Tuning.Bus.StartingPassengers;
            Bus.BrakeWear = Tuning.Bus.StartingBrakeWear;
            Player = bus;
            return bus;
        }

        public Agent SpawnVehicle(VehicleClass vehicleClass, float s, float lateral, float nerve)
        {
            return NewVehicle(vehicleClass, s, lateral, nerve);
        }

        /// <param name="side">−1 = left kerb, +1 = right kerb.</param>
        public Agent SpawnPedestrian(float s, int side)
        {
            Agent p = new Agent
            {
                Id = _nextId++,
                Class = VehicleClass.Pedestrian,
                Shape = VehicleShape.For(VehicleClass.Pedestrian),
                Mass = Tuning.Mass.Pedestrian,
                Nerve = Random.Value,
                S = s,
                Lateral = (Corridor.HalfWidth + Tuning.Spawn.KerbOffsetMetres) * side,
                WaitTimer = Random.Range(0f, Tuning.Pedestrians.WaitMaxSeconds),
            };
            p.Position = Corridor.PositionAt(p.S, p.Lateral);
            p.Yaw = Corridor.YawAt(p.S);
            Agents.Add(p);
            return p;
        }

        private Agent NewVehicle(VehicleClass vehicleClass, float s, float lateral, float nerve)
        {
            VehicleShape shape = VehicleShape.For(vehicleClass);
            Agent a = new Agent
            {
                Id = _nextId++,
                Class = vehicleClass,
                Shape = shape,
                Mass = Tuning.Mass.Of(vehicleClass),
                Nerve = Mathf.Clamp01(nerve),
                S = s,
                Lateral = lateral,
                TargetLateral = lateral,
                Speed = shape.CruiseSpeed * 0.8f,
                DesiredSpeed = shape.CruiseSpeed * Random.Range(0.85f, 1.15f),
            };
            a.Position = Corridor.PositionAt(a.S, a.Lateral);
            a.Yaw = Corridor.YawAt(a.S);
            Agents.Add(a);
            return a;
        }

        // ---------------------------------------------------------------- the step

        public void Step(float dt)
        {
            if (Metrics.PersonHit) return;      // the day ended; nothing moves until a reset
            Metrics.Time += dt;

            if (Player != null)
            {
                Bus.Step(Player, Corridor, Tuning.Bus, dt);
                Metrics.DistanceMetres += Player.Speed * dt;
                Player.HornTimer = Mathf.Max(0f, Player.HornTimer - dt);
            }

            for (int i = 0; i < Agents.Count; i++)
            {
                Agent a = Agents[i];
                if (a.IsPlayer) continue;
                if (a.IsPedestrian) Pedestrians.Step(this, a, dt);
                else Steering.Drive(this, a, dt);
            }

            ResolveContacts();
            if (Player != null) UpdatePlayerMetrics(dt);
            MaintainPopulation();
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

        // ---------------------------------------------------------------- contacts

        /// <summary>
        /// Boxes overlapping in corridor coordinates. Vehicle on vehicle is a scrape: counted, the one
        /// behind loses speed, the lighter one is pushed aside. Vehicle on person is the one hard rule.
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

                    float ds = b.S - a.S;
                    float dLat = b.Lateral - a.Lateral;
                    float overlapS = a.HalfLength + b.HalfLength - Mathf.Abs(ds);
                    float overlapLat = a.HalfWidth + b.HalfWidth - Mathf.Abs(dLat);
                    if (overlapS <= 0f || overlapLat <= 0f) continue;

                    if (a.IsPedestrian || b.IsPedestrian)
                    {
                        Agent vehicle = a.IsPedestrian ? b : a;
                        if (vehicle.Speed < 0.5f) continue;            // nudged at walking pace: nothing
                        if (vehicle.IsPlayer) Metrics.PersonHit = true;
                        else Metrics.NpcPersonHits++;
                        continue;
                    }

                    // Count once per pair per couple of seconds.
                    if (Metrics.Time - a.LastContactTime > 2f || Metrics.Time - b.LastContactTime > 2f)
                    {
                        if (a.IsPlayer || b.IsPlayer) Metrics.Contacts++;
                    }
                    a.LastContactTime = b.LastContactTime = Metrics.Time;

                    // The one behind can't go faster than the one in front.
                    Agent behind = ds > 0f ? a : b;
                    Agent front = ds > 0f ? b : a;
                    behind.Speed = Mathf.Min(behind.Speed, front.Speed);

                    // Sideways: the lighter one gets pushed clear.
                    Agent light = a.Mass <= b.Mass ? a : b;
                    Agent heavy = light == a ? b : a;
                    float push = (overlapLat + 0.05f) * (light.Lateral >= heavy.Lateral ? 1f : -1f);
                    light.Lateral += push;
                    light.TargetLateral = light.Lateral;
                    if (light.IsPlayer)
                    {
                        light.Position += Corridor.RightAt(light.S) * push;
                    }
                }
            }
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
        /// Keep traffic and pedestrians around the player: recycle what fell far behind or ran off
        /// the end of the corridor, spawn into free space ahead and behind.
        /// </summary>
        private void MaintainPopulation()
        {
            if (Player == null) return;
            SpawnSettings spawn = Tuning.Spawn;
            float ps = Player.S;

            int vehicles = 0, pedestrians = 0;
            for (int i = Agents.Count - 1; i >= 0; i--)
            {
                Agent a = Agents[i];
                if (a.IsPlayer) continue;
                bool gone;
                if (a.IsPedestrian)
                {
                    gone = a.S < ps - spawn.PedestrianBehindMetres - 20f || a.S > ps + spawn.SpawnAheadMetres + 50f;
                    if (!gone) pedestrians++;
                }
                else
                {
                    gone = a.S < ps - Tuning.Performance.DespawnRadiusMetres
                        || a.S > ps + spawn.SpawnAheadMetres + 150f
                        || a.S > Corridor.Length - 5f;
                    if (!gone) vehicles++;
                }
                if (gone) Agents.RemoveAt(i);
            }

            for (int attempt = 0; vehicles < spawn.VehiclesAround && attempt < 10; attempt++)
            {
                if (TrySpawnVehicleNear(ps)) vehicles++;
            }
            for (int attempt = 0; pedestrians < spawn.PedestriansAround && attempt < 10; attempt++)
            {
                float s = Random.Range(ps - spawn.PedestrianBehindMetres, ps + spawn.SpawnAheadMetres);
                if (s > 5f && s < Corridor.Length - 5f)
                {
                    SpawnPedestrian(s, Random.Chance(0.5f) ? -1 : 1);
                    pedestrians++;
                }
            }
        }

        private bool TrySpawnVehicleNear(float playerS)
        {
            SpawnSettings spawn = Tuning.Spawn;
            // Mostly ahead (that is where the player drives into), some behind (that is who presses you).
            float s = Random.Chance(0.7f)
                ? Random.Range(playerS + spawn.SpawnClearanceMetres, playerS + spawn.SpawnAheadMetres)
                : Random.Range(playerS - spawn.SpawnBehindMetres, playerS - spawn.SpawnClearanceMetres);
            if (s < 5f || s > Corridor.Length - 30f) return false;

            VehicleClass vehicleClass = PickClass();
            VehicleShape shape = VehicleShape.For(vehicleClass);
            float edge = Corridor.HalfWidth - shape.Width * 0.5f;
            float lateral = Random.Range(-edge, edge);

            // Only into free road: nobody within a few lengths in that band.
            for (int i = 0; i < Agents.Count; i++)
            {
                Agent other = Agents[i];
                if (Mathf.Abs(other.Lateral - lateral) < other.HalfWidth + shape.Width * 0.5f + Steering.LateralMargin
                    && Mathf.Abs(other.S - s) < other.HalfLength + shape.Length * 0.5f + 8f)
                {
                    return false;
                }
            }

            Agent a = SpawnVehicle(vehicleClass, s, lateral, Random.Range(0.2f, 0.9f));
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
