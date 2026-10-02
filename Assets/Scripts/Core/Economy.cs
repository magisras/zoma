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
        public int Trips;
        public bool Arrested;          // the day ended with a person under the wheels

        public readonly List<string> Events = new List<string>();

        public float PaidOutTk => FuelTk + LinemanTk + PartyManTk + SergeantTk + CaseTk + RepairsTk;
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

        private readonly TrafficSim _sim;
        private readonly EconomySettings _e;
        private float _lastPlayerS;
        private bool _firstStep = true;
        private readonly Dictionary<Junction, bool> _passedJunction = new Dictionary<Junction, bool>();
        private int _caneRunsSeen;
        private bool _ranCaneRecently;

        public Economy(TrafficSim sim)
        {
            _sim = sim;
            _e = sim.Tuning.Economy;
            Ledger.ZomaTk = _e.ZomaTk * _e.MoneyScale;
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
            Ledger.FuelTk += metres / 1000f / Mathf.Max(0.1f, _e.BusKmPerLitre) * _e.DieselTkPerLitre;

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
                Ledger.CaseTk += _e.PersonHitCaseTk * _e.MoneyScale;
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
            Injuries++;
            float tk = _e.InjuryCompensationTk * _e.MoneyScale;
            Ledger.CaseTk += tk;
            Ledger.Log((alighting ? "A passenger fell getting off" : "A passenger fell at the door") + " at speed. The crowd. Tk " + tk.ToString("0") + " on the spot.");
            InjuryHoldUntil = _sim.Metrics.Time + _e.InjuryHoldSeconds;
            _sim.Bus.Held = true;
            if (Injuries >= _e.InjuriesBeforeArrest)
            {
                Ledger.Arrested = true;
                Ledger.Log("The second one. Police. The bus is seized and the day's money with it.");
                EndDay("passenger injured twice");
            }
        }

        /// <summary>Repairs come out of the crew's day (RESEARCH.md: damage comes out of the crew's day).</summary>
        public void OnScrape()
        {
            Ledger.RepairsTk += _e.ScrapeRepairTk * _e.MoneyScale;
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
                float caseTk = _e.CaseTk * _e.MoneyScale;
                Ledger.CaseTk += caseTk;
                Sergeant.ReleaseAt = _sim.Metrics.Time + _e.CaseDelaySeconds;
                Ledger.Log("Sergeant, " + Sergeant.Reason + ": refused. A case for Tk " + caseTk.ToString("0") + " and the papers take " + Mathf.RoundToInt(_e.CaseDelaySeconds / 60f) + " minutes.");
            }
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
                    Ledger.LinemanTk += _e.LinemanTkPerTrip * _e.MoneyScale;
                    Ledger.Log("Trip " + Ledger.Trips + " at the stand. Lineman: Tk " + (_e.LinemanTkPerTrip * _e.MoneyScale).ToString("0") + ".");
                }
                if (i == _e.PartyManZoneIndex && _sim.Metrics.DistanceMetres > 200f)
                {
                    Ledger.PartyManTk += _e.PartyManTkPerTrip * _e.MoneyScale;
                    Ledger.Log("Party man at " + zone.Name + ": Tk " + (_e.PartyManTkPerTrip * _e.MoneyScale).ToString("0") + ".");
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
        /// Past a junction the sergeant may step out: certainly more often if you ran the cane. The
        /// bus is held until the player answers; a refusal holds it much longer.
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
                    // Clean papers and an undented bus give him less to point at; a cane run or the wrong side, nothing helps.
                    float papers = _sim.Tuning.Officer.SergeantStopChance * _e.SergeantChancePerPassFactor
                                 * (_sim.Condition.PapersValid(_sim.Day) ? _e.PapersSergeantFactor : 1f)
                                 * (1f + _sim.Condition.Dents * _e.DentSergeantFactor);
                    float chance = ranCane ? _e.SergeantChanceAfterCaneRun
                                 : wrongSide ? _e.WrongSideSergeantChance
                                 : Mathf.Clamp01(papers);
                    if (_sim.Random.Chance(chance))
                    {
                        Sergeant.Active = true;
                        Sergeant.HeldSeconds = 0f;
                        Sergeant.ReleaseAt = -1f;
                        Sergeant.DemandTk = _e.SergeantDemandTk * _e.MoneyScale;
                        Sergeant.Reason = ranCane ? "ran the cane" : wrongSide ? "wrong side" : "papers";
                        _sim.Bus.Held = true;
                        Ledger.Log("A sergeant steps out: " + Sergeant.Reason + ". Tk " + Sergeant.DemandTk.ToString("0") + " now, or a case.");
                    }
                    _ranCaneRecently = false;
                    ranCane = false;
                }
                else if (!justPast && ds < 0f) _passedJunction[j] = false;   // reset once we're back before it (next lap)
            }
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
