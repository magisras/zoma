using System.Collections.Generic;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Unity
{
    /// <summary>
    /// The street around the physics bus: the core <see cref="TrafficSim"/> (crowds at the stops,
    /// rivals racing for them, traffic, people crossing, the sergeant, the ledger) run on the real
    /// route. The bus body is the authority: each physics step the player's Agent is written from the
    /// body, the sim steps the world and applies the tired hands to the BusController, the body drives
    /// with that. The sim's own kinematic bus step is off (TrafficSim.ExternalPlayer).
    ///
    /// The corridor is the whole round trip as one loop: out on route.json, back on route_back.json,
    /// so the stops of both legs are zones along one road and a trip is one lap. The lineman is paid
    /// at the Mirpur 12 stand (zone 0), the party man at Karwan Bazar.
    /// </summary>
    public sealed class StreetSim : MonoBehaviour
    {
        [Tooltip("Seed of the day: the same seed gives the same traffic and the same sergeants.")]
        public int Seed = 1;
        [Tooltip("Rivals of the player's own company, named crews with memory, spawned at the stand.")]
        public bool OwnCompanyRivals = true;
        [Tooltip("Length of the play day in real seconds. Placeholder: a real shift is 12 to 17 hours; three hours is three or four trips at Dhaka speeds with stops. Owner to decide.")]
        public float DayLengthSeconds = 3f * 3600f;

        public TrafficSim Sim { get; private set; }
        public TuningTable Tuning { get; private set; }     // a runtime copy: the sim's overrides never touch the asset
        public float OutLength { get; private set; }

        private PlayerBusDrive _drive;
        private float _lastSimSpeed;
        private float _hornHeldSeconds;
        private readonly List<string> _note = new List<string>();

        /// <summary>Build the day: the sim on the loop corridor, the zones from the stops, the rivals.</summary>
        public void Build(PlayerBusDrive drive, Corridor road, RouteStops stops, float outLength)
        {
            _drive = drive;
            OutLength = outLength;
            Tuning = Object.Instantiate(drive.Tuning);
            EconomySettings e = Tuning.Economy;
            e.MoneyScale = 1f;                      // real road, real money; the sandbox scaled a lap to a trip
            e.TripKm = outLength / 1000f;
            e.DayLengthSeconds = DayLengthSeconds;
            e.LinemanZoneIndex = 0;                 // the Mirpur 12 stand is the first stop in the file

            Sim = new TrafficSim(road, Tuning, Seed);
            Sim.ExternalPlayer = true;
            Sim.DistanceScale = 1f;

            // Zones: every stop on each leg; the back leg's S is beyond the out leg's length on the loop.
            foreach (RouteStops.Stop st in stops.All)
            {
                if (st.outS >= 0f) Sim.AddZone(st.name.Length > 0 ? st.name : st.nameBn, st.outS, st.hot);
            }
            foreach (RouteStops.Stop st in stops.All)
            {
                if (st.backS >= 0f) Sim.AddZone((st.name.Length > 0 ? st.name : st.nameBn) + " (back)", outLength + st.backS, st.hot);
            }
            for (int i = 0; i < Sim.Zones.Count; i++)
            {
                if (Sim.Zones[i].Name.StartsWith("Karwan Bazar")) { e.PartyManZoneIndex = i; break; }
            }
            // The sergeant's boxes: at the big junctions, both ways (RESEARCH.md: 8 to 10 payment points).
            string[] boxes = { "Mirpur 10 roundabout", "Agargaon crossing", "Farmgate", "Shahbagh" };
            foreach (DemandZone z in Sim.Zones)
            {
                foreach (string b in boxes)
                {
                    if (z.Name.StartsWith(b)) Sim.AddCheckpoint(z.Name + " box", z.S);
                }
            }
            Boarding.SeedCrowds(Sim, Tuning.Passengers.InitialCrowdMinutes);

            Sim.SpawnPlayerBus(drive.StartAlong, drive.StartLateral);
            if (OwnCompanyRivals)
            {
                Sim.SpawnRivalBus("Rafiq", DriverPersonality.Reckless(), drive.StartAlong + 40f, drive.StartLateral).Speed = 0f;
                Sim.SpawnRivalBus("Jamal", DriverPersonality.Spiteful(), drive.StartAlong - 25f, drive.StartLateral).Speed = 0f;
            }
            _lastSimSpeed = 0f;
        }

        /// <summary>
        /// One step: the player's Agent already holds the body's pose; the pedals go in through the
        /// tired hands, the world moves, and what the sim did to the player's speed (a truck in the
        /// nose, a sergeant's hand) comes back as a cap for the body.
        /// </summary>
        public void Step(float dt, float throttle, float brake, float steer, bool horn, float lateralVelocity, out float speedCap)
        {
            Agent p = Sim.Player;
            p.LateralVelocity = lateralVelocity;
            float before = p.Speed;
            Sim.PlayerInputs(new PlayerInput { Throttle = throttle, Brake = brake, Steer = steer, Horn = horn, Door = true });
            Sim.Step(dt);
            // The sim lowers the player's speed in a contact or when held; it never raises it for a body.
            speedCap = Sim.Bus.Held ? 0f : (p.Speed < before - 0.05f ? p.Speed : float.MaxValue);
        }

        /// <summary>P: pay the sergeant, or the men with the ropes. N: refuse.</summary>
        public void Answer(bool pay)
        {
            Sim.Economy.AnswerSergeant(pay);
            Core.Rollover.Answer(Sim, pay);
        }

        /// <summary>The body tipped over: the core's rollover flow (the crowd, the ropes, the price) takes it from here.</summary>
        public void BodyTipped(string cause)
        {
            if (!Sim.Rollover.Active) Core.Rollover.Tip(Sim, cause);
        }

        /// <summary>The next crowd ahead of the player: the zone and how many are waiting.</summary>
        public DemandZone NextZone(out float ahead, out int waiting)
        {
            DemandZone best = null;
            ahead = float.MaxValue; waiting = 0;
            foreach (DemandZone z in Sim.Zones)
            {
                float d = Sim.Corridor.DeltaS(Sim.Player.S, z.S);
                if (d < -10f || d >= ahead) continue;
                best = z; ahead = d; waiting = z.Waiting.Count;
            }
            return best;
        }

        /// <summary>The lines of the readout that belong to the street: money, people, the sergeant, the day.</summary>
        public string Readout()
        {
            Economy eco = Sim.Economy;
            Ledger l = eco.Ledger;
            var sb = new System.Text.StringBuilder();
            DemandZone next = NextZone(out float ahead, out int waiting);
            int hour = Mathf.FloorToInt(eco.ClockHours), minute = Mathf.FloorToInt((eco.ClockHours - hour) * 60f);
            sb.Append($"{hour:00}:{minute:00}   fares Tk {l.FaresTk:0}   paid out Tk {l.PaidOutTk:0}   zoma Tk {l.ZomaTk:0}   crew's Tk {l.CrewNetTk:0}   trips {l.Trips}\n");
            if (next != null) sb.Append($"{Sim.Player.Load.Count} aboard; {waiting} waiting at {next.Name} in {Mathf.Max(0f, ahead):0} m");
            Agent ahead1 = Sim.OwnBusAhead(out float mAhead), behind = Sim.OwnBusBehind(out float mBehind);
            if (ahead1 != null) sb.Append($"   {ahead1.Brain.CrewName} {mAhead:0} m ahead");
            if (behind != null) sb.Append($"   {behind.Brain.CrewName} {mBehind:0} m behind");
            sb.Append('\n');
            if (eco.Sergeant.Active && eco.Sergeant.ReleaseAt < 0f)
                sb.Append($"SERGEANT, {eco.Sergeant.Reason}: Tk {eco.Sergeant.DemandTk:0}.  P pay, N refuse.\n");
            else if (eco.Sergeant.Active)
                sb.Append("The sergeant writes the case. Wait.\n");
            if (Sim.Rollover.Pending) sb.Append("On its side. The men with the ropes want paying.  P pay, N walk away.\n");
            if (Sim.Fatigue.Level > 0.5f) sb.Append($"tired {Sim.Fatigue.Level * 100f:0} %   ");
            if (eco.DayOver) sb.Append($"DAY OVER ({eco.DayOverReason}). Crew's take Tk {l.CrewNetTk:0}.  R for the next day.\n");
            for (int i = Mathf.Max(0, l.Events.Count - 3); i < l.Events.Count; i++) sb.Append(l.Events[i]).Append('\n');
            return sb.ToString();
        }
    }
}
