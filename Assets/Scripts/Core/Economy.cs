using System.Collections.Generic;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>
    /// The day's equation (RESEARCH.md, "The daily ledger"): fares collected minus zoma minus fuel
    /// minus the roadside payments equals what the crew eats. The zoma never moves.
    /// </summary>
    public sealed class Ledger
    {
        public float FaresTk;          // taken at the door
        public float ZomaTk;           // the owner's deposit, fixed before the day starts
        public float FuelTk;
        public float LinemanTk;        // the association's man at the stand, per trip, unavoidable
        public float PartyManTk;       // the party man at the market stand, per trip
        public float SergeantTk;       // paid at the roadside
        public float CaseTk;           // cases filed when you didn't pay (or hit someone)
        public float RepairsTk;        // scrapes: paint, mirrors, a bent door
        public float RopesTk;          // the men who dragged the bus back onto its wheels
        public float CameraTk;         // SMS cases from the junction cameras, booked to the owner, taken from the crew
        public float WagesTk;          // the helper's and the conductor's share, paid by the driver out of the take
        public float FoodTk;           // tea and rice on the road for three
        public int Trips;
        public bool Arrested;          // the day ended with a person under the wheels

        public readonly List<string> Events = new List<string>();

        public float PaidOutTk => FuelTk + LinemanTk + PartyManTk + SergeantTk + CaseTk + RepairsTk + RopesTk + CameraTk + WagesTk + FoodTk;
        public float CrewNetTk => (Arrested ? 0f : FaresTk) - ZomaTk - PaidOutTk;

        public void Log(string text) { Events.Add(text); if (Events.Count > 40) Events.RemoveAt(0); }
    }

    /// <summary>A sergeant standing in front of the bus with his hand up. Decide, or sit there.</summary>
    public sealed class SergeantDemand
    {
        public bool Active;
        public float DemandTk;
        public string Reason;
        public float HeldSeconds;      // how long the bus has been held
        public float ReleaseAt = -1f;  // after a case: when the paperwork ends (sim time)
    }

    /// <summary>
    /// Everything that moves money or the clock. TrafficSim calls it; the sandbox reads it.
    /// </summary>
    public sealed class Economy
    {
        public readonly Ledger Ledger = new Ledger();
        public readonly SergeantDemand Sergeant = new SergeantDemand();
        public float ShiftSeconds;
        public bool DayOver;
        public string DayOverReason;
        /// <summary>A special drive: sergeants with targets, more stops, higher prices (docs/STREET_CONTROL.md §3).</summary>
        public bool DriveDay;
        /// <summary>The bus went to the dumping yard: the day ended and tomorrow has no bus.</summary>
        public bool Seized;

        private readonly TrafficSim _sim;
        private readonly EconomySettings _e;
        private float _lastPlayerS;
        private bool _firstStep = true;
        private readonly Dictionary<Junction, bool> _passedJunction = new Dictionary<Junction, bool>();
        private readonly Dictionary<Checkpoint, bool> _passedCheckpoint = new Dictionary<Checkpoint, bool>();
        private int _caneRunsSeen;
        private bool _ranCaneRecently;

        public Economy(TrafficSim sim)
        {
            _sim = sim;
            _e = sim.Tuning.Economy;
            Ledger.ZomaTk = _e.ZomaTk * _e.MoneyScale;
            Ledger.WagesTk = _e.CrewWagesTkPerDay * _e.MoneyScale;
            Ledger.FoodTk = _e.CrewFoodTkPerDay * _e.MoneyScale;
            // Drawn from its own stream so the day's traffic does not change with the drive-day coin.
            DriveDay = new SeededRandom(sim.Seed * 7919 + 17).Chance(_e.DriveDayChance);
            if (DriveDay) Ledger.Log("The lineman says it is a drive today. Sergeants at every box.");
        }

        /// <summary>Clock on the wall: the shift starts at ShiftStartHour and the day's seconds map onto the shift hours.</summary>
        public float ClockHours => _e.ShiftStartHour + Mathf.Clamp01(ShiftSeconds / _e.DayLengthSeconds) * _e.ShiftHours;

        public void Step(float dt)
        {
            Agent bus = _sim.Player;
            if (bus == null || DayOver) return;
            ShiftSeconds += dt;

            // Fuel burns with distance. A heavy bus burns more; that comes with the physics later.
            float metres = bus.Speed * dt;
            // A trip's fuel over a trip's kilometres, per real kilometre driven (the ring's metres count TripKm/ring of them).
            Ledger.FuelTk += metres * _sim.DistanceScale / 1000f / Mathf.Max(0.1f, _e.TripKm) * _e.FuelTkPerTrip;

            WatchTripPoints(bus);
            if (InjuryHoldUntil >= 0f)
            {
                if (_sim.Metrics.Time >= InjuryHoldUntil) { InjuryHoldUntil = -1f; if (!Sergeant.Active) _sim.Bus.Held = false; }
                else _sim.Bus.Held = true;
            }
            WatchSergeant(bus, dt);

            if (_sim.Metrics.PersonHit)
            {
                Ledger.Arrested = true;
                Ledger.CaseTk += _e.PersonHitCaseTk;
                Ledger.Log("A person under the wheels. The crowd, the police, the case. The day's money is gone.");
                EndDay("person hit");
            }
            else if (ShiftSeconds >= _e.DayLengthSeconds)
            {
                EndDay("shift over");
            }
        }

        public int Injuries;
        public float InjuryHoldUntil = -1f;

        /// <summary>
        /// Someone fell from the door at speed. The crowd holds the bus, the crew pays on the spot,
        /// and the second time the police end the day (RESEARCH.md: only injuries escalate).
        /// </summary>
        public void OnInjury(bool alighting)
        {
            float tk = _e.InjuryCompensationTk;
            Injury((alighting ? "A passenger fell getting off" : "A passenger fell at the door") + " at speed. The crowd. Tk " + tk.ToString("0") + " on the spot.", tk, _e.InjuryHoldSeconds);
        }

        /// <summary>
        /// The nose put someone on the ground under the death speed. Same path as a fall: the crowd holds the
        /// bus, the crew pays, the second injury of the day ends it. More money and a longer hold than a fall.
        /// </summary>
        public void OnPedestrianKnockedDown(float speedMs, bool onKerb)
        {
            float tk = _e.KnockDownTk * (onKerb ? 2f : 1f);
            Injury("A person under the nose at " + Mathf.RoundToInt(speedMs * 3.6f) + " km/h" + (onKerb ? ", on the pavement" : "") + ". Down, not dead. The crowd closes in. Tk " + tk.ToString("0") + " on the spot, and the hospital.", tk, _e.InjuryHoldSeconds * 2f);
        }

        private void Injury(string what, float tk, float holdSeconds)
        {
            Injuries++;
            Ledger.CaseTk += tk;
            Ledger.Log(what);
            InjuryHoldUntil = _sim.Metrics.Time + holdSeconds;
            _sim.Bus.Held = true;
            if (Injuries >= _e.InjuriesBeforeArrest)
            {
                Ledger.Arrested = true;
                Ledger.Log("The second one. Police. The bus is seized and the day's money with it.");
                EndDay("passenger injured twice");
            }
        }

        /// <summary>Walking away from the bus on its side: the day ends, the deposit does not.</summary>
        public void WalkAway()
        {
            WalkedAway = true;
            EndDay("walked away from the bus");
        }

        public bool WalkedAway;

        /// <summary>
        /// Repairs come out of the crew's day (RESEARCH.md: damage comes out of the crew's day), but a
        /// touch at walking pace is paint, not a bill. Returns true when it cost something.
        /// </summary>
        public bool OnScrape(float relativeSpeed)
        {
            if (relativeSpeed < _e.CosmeticContactMs) return false;
            Ledger.RepairsTk += _e.ScrapeRepairTk;
            return true;
        }

        /// <summary>The player's answer to the sergeant: pay, or take the case.</summary>
        public void AnswerSergeant(bool pay)
        {
            if (!Sergeant.Active || Sergeant.ReleaseAt >= 0f) return;
            if (pay)
            {
                Ledger.SergeantTk += Sergeant.DemandTk;
                Ledger.Log("Sergeant, " + Sergeant.Reason + ": paid Tk " + Sergeant.DemandTk.ToString("0") + ".");
                Sergeant.Active = false;
                _sim.Bus.Held = false;
            }
            else
            {
                // Refusing with no valid papers can cost the bus itself: he has the grounds, and a target.
                if (!_sim.Condition.PapersValid(_sim.Day) && _sim.Random.Chance(_e.SeizeChanceWithoutPapers))
                {
                    SeizeBus();
                    return;
                }
                float caseTk = _e.CaseTk;
                Ledger.CaseTk += caseTk;
                Sergeant.ReleaseAt = _sim.Metrics.Time + _e.CaseDelaySeconds;
                Ledger.Log("Sergeant, " + Sergeant.Reason + ": refused. A case for Tk " + caseTk.ToString("0") + " and the papers take " + Mathf.RoundToInt(_e.CaseDelaySeconds / 60f) + " minutes.");
            }
        }

        /// <summary>The bus goes to the dumping yard. The day ends; the household learns how long it is gone.</summary>
        private void SeizeBus()
        {
            Seized = true;
            Sergeant.Active = false;
            Ledger.Log("No papers, no payment. The wrecker. The bus goes to the dumping yard for " + _e.DumpingDays + " days.");
            EndDay("bus seized");
        }

        // ---------------------------------------------------------------- trips and stands

        /// <summary>
        /// Passing the stand completes a trip and pays the lineman; passing the party man's stand
        /// pays him. Both are per trip, both come out of the takings, neither can be refused.
        /// </summary>
        private void WatchTripPoints(Agent bus)
        {
            if (_firstStep) { _lastPlayerS = bus.S; _firstStep = false; return; }
            for (int i = 0; i < _sim.Zones.Count; i++)
            {
                DemandZone zone = _sim.Zones[i];
                bool crossed = CrossedForward(_lastPlayerS, bus.S, zone.S);
                if (!crossed) continue;
                if (i == _e.LinemanZoneIndex && _sim.Metrics.DistanceMetres > 200f)
                {
                    Ledger.Trips++;
                    Ledger.LinemanTk += _e.LinemanTkPerTrip;
                    Ledger.Log("Trip " + Ledger.Trips + " at the stand. Lineman: Tk " + _e.LinemanTkPerTrip.ToString("0") + ".");
                }
                if (i == _e.PartyManZoneIndex && _sim.Metrics.DistanceMetres > 200f)
                {
                    Ledger.PartyManTk += _e.PartyManTkPerTrip;
                    Ledger.Log("Party man at " + zone.Name + ": Tk " + _e.PartyManTkPerTrip.ToString("0") + ".");
                }
            }
            _lastPlayerS = bus.S;
        }

        private bool CrossedForward(float from, float to, float point)
        {
            Corridor c = _sim.Corridor;
            float before = c.DeltaS(from, point);
            float after = c.DeltaS(to, point);
            return before > 0f && after <= 0f && before < 50f;
        }

        // ---------------------------------------------------------------- the sergeant

        /// <summary>
        /// Past a junction the sergeant may step out if you ran the cane or came up the wrong side,
        /// and a camera there books the case whether or not he does. Past a police box, the sergeant
        /// on duty may step out for anything: papers, dents, his target. The bus is held until the
        /// player answers; a refusal holds it much longer.
        /// </summary>
        private void WatchSergeant(Agent bus, float dt)
        {
            if (Sergeant.Active)
            {
                Sergeant.HeldSeconds += dt;
                _sim.Bus.Held = true;
                if (Sergeant.ReleaseAt >= 0f && _sim.Metrics.Time >= Sergeant.ReleaseAt)
                {
                    Sergeant.Active = false;
                    Sergeant.ReleaseAt = -1f;
                    _sim.Bus.Held = false;
                }
                return;
            }

            // A cane run is remembered until the bus is past the junction, where the sergeant waits.
            if (_sim.Metrics.CaneRuns > _caneRunsSeen) _ranCaneRecently = true;
            _caneRunsSeen = _sim.Metrics.CaneRuns;
            bool ranCane = _ranCaneRecently;

            for (int i = 0; i < _sim.Junctions.Count; i++)
            {
                Junction j = _sim.Junctions[i];
                if (j.Main != bus.Corridor) continue;          // the other carriageway's mirror of this officer
                float ds = _sim.Corridor.DeltaS(j.MainS, bus.S);
                bool justPast = ds > j.MainHalfSpan + 8f && ds < j.MainHalfSpan + 30f;
                bool wasPast;
                _passedJunction.TryGetValue(j, out wasPast);
                if (justPast && !wasPast)
                {
                    _passedJunction[j] = true;
                    bool wrongSide = _sim.Metrics.WrongSideNow;
                    if (ranCane || wrongSide)
                    {
                        // The camera does not negotiate: the owner gets the SMS, the crew gets the bill tonight.
                        if (j.Camera)
                        {
                            float fine = _e.CameraFineTk;
                            Ledger.CameraTk += fine;
                            Ledger.Log("The camera on the pole. " + (ranCane ? "Ran the cane" : "Wrong side") + ": an SMS to the owner, Tk " + fine.ToString("0") + ".");
                        }
                        // The officer at the junction controls flow; the sergeant beside it takes the money.
                        float chance = ranCane ? _e.SergeantChanceAfterCaneRun : _e.WrongSideSergeantChance;
                        if (DriveDay) chance *= _e.DriveDayFactor;
                        if (_sim.Random.Chance(Mathf.Clamp01(chance)))
                            StepOut(ranCane ? "ran the cane" : "wrong side");
                    }
                    _ranCaneRecently = false;
                    ranCane = false;
                }
                else if (!justPast && ds < 0f) _passedJunction[j] = false;   // reset once we're back before it (next lap)
            }

            for (int i = 0; i < _sim.Checkpoints.Count; i++)
            {
                Checkpoint cp = _sim.Checkpoints[i];
                float ds = _sim.Corridor.DeltaS(cp.S, bus.S);
                bool justPast = ds > 0f && ds < 30f;
                bool wasPast;
                _passedCheckpoint.TryGetValue(cp, out wasPast);
                if (justPast && !wasPast)
                {
                    _passedCheckpoint[cp] = true;
                    if (!cp.SergeantOnDuty) continue;
                    // Clean papers and an undented bus give him less to point at. A drive day gives him a target.
                    float chance = _e.CheckpointStopChance
                                 * (_sim.Condition.PapersValid(_sim.Day) ? _e.PapersSergeantFactor : 1f)
                                 * (1f + _sim.Condition.Dents * _e.DentSergeantFactor)
                                 * (DriveDay ? _e.DriveDayFactor : 1f);
                    if (_sim.Random.Chance(Mathf.Clamp01(chance))) StepOut("papers, " + cp.Name);
                }
                else if (!justPast && ds < 0f) _passedCheckpoint[cp] = false;
            }
        }

        /// <summary>A sergeant's hand goes up in front of the bus.</summary>
        private void StepOut(string reason)
        {
            if (Sergeant.Active) return;
            Sergeant.Active = true;
            Sergeant.HeldSeconds = 0f;
            Sergeant.ReleaseAt = -1f;
            Sergeant.DemandTk = _e.SergeantDemandTk * (DriveDay ? _e.DriveDayDemandFactor : 1f);
            Sergeant.Reason = reason;
            _sim.Bus.Held = true;
            Ledger.Log("A sergeant steps out: " + reason + ". Tk " + Sergeant.DemandTk.ToString("0") + " now, or a case.");
        }

        private void EndDay(string reason)
        {
            DayOver = true;
            DayOverReason = reason;
            if (_sim.Player != null) Ledger.FaresTk = _sim.Player.Load.FaresTk;
            _sim.Bus.Held = true;
        }

        /// <summary>Keep the fares figure live for the HUD.</summary>
        public void SyncFares()
        {
            if (!DayOver && _sim.Player != null) Ledger.FaresTk = _sim.Player.Load.FaresTk;
        }
    }
}
