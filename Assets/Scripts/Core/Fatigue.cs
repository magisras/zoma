using System.Collections.Generic;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>One frame of what the player asked the bus to do.</summary>
    public struct PlayerInput
    {
        public float Throttle, Brake, Steer;
        public bool Horn, Door;
    }

    /// <summary>
    /// A delay line for inputs: what the hands do reaches the wheel a little late when the driver
    /// is tired (RESEARCH.md: "Reaction time slows"). Push every frame; read back the sample from
    /// `delay` seconds ago.
    /// </summary>
    public sealed class InputDelay
    {
        private readonly List<float> _times = new List<float>();
        private readonly List<PlayerInput> _samples = new List<PlayerInput>();

        public void Push(float time, PlayerInput input)
        {
            _times.Add(time);
            _samples.Add(input);
            while (_times.Count > 2 && _times[1] < time - 2f) { _times.RemoveAt(0); _samples.RemoveAt(0); }
        }

        /// <summary>
        /// The newest sample at or before <paramref name="time"/>. Nothing that old yet (the shift
        /// just started, the hands haven't "arrived"): no input at all.
        /// </summary>
        public PlayerInput At(float time)
        {
            PlayerInput best = new PlayerInput();
            for (int i = 0; i < _times.Count; i++)
            {
                if (_times[i] <= time) best = _samples[i];
                else break;
            }
            return best;
        }
    }

    /// <summary>
    /// The driver's body over the shift. Nothing here is shown as a bar in the game: the world shows
    /// it, through slow hands, a narrowing view and a screen that goes dark for a moment.
    /// </summary>
    public sealed class FatigueState
    {
        public float Level;                // 0 fresh .. 1 finished
        public bool Asleep;                // a micro-sleep is happening
        public float AsleepLeft;
        public int MicroSleeps;
        public PlayerInput Frozen;         // the hands as they were when the eyes closed

        public bool Injured;               // the crew carrying injuries from a rollover: slower hands
        public float ReactionDelay(FatigueSettings f) => Level * f.ReactionDelayMaxSeconds * (Injured ? f.InjuryReactionFactor : 1f);
        public float Tunnel(FatigueSettings f) => Mathf.Clamp01((Level - f.TunnelAbove) / Mathf.Max(0.01f, 1f - f.TunnelAbove));

        public void Step(float dt, FatigueSettings f, EconomySettings e, SeededRandom random)
        {
            // The day's seconds stand for shift hours; fatigue rises per shift hour.
            float hoursPerSecond = e.ShiftHours / Mathf.Max(1f, e.DayLengthSeconds);
            Level = Mathf.Clamp01(Level + f.RisePerShiftHour * hoursPerSecond * dt);

            if (Asleep)
            {
                AsleepLeft -= dt;
                if (AsleepLeft <= 0f) Asleep = false;
                return;
            }
            if (Level > f.MicroSleepAbove)
            {
                float chancePerSecond = (Level - f.MicroSleepAbove) / Mathf.Max(0.01f, 1f - f.MicroSleepAbove) * f.MicroSleepChancePerSecond;
                if (random.Chance(chancePerSecond * dt))
                {
                    Asleep = true;
                    AsleepLeft = random.Range(f.MicroSleepMinSeconds, f.MicroSleepMaxSeconds);
                    MicroSleeps++;
                }
            }
        }
    }

    /// <summary>What one day left behind.</summary>
    public sealed class DaySummary
    {
        public int Day;
        public bool Worked;
        public float CrewNetTk;
        public float FoodTk;
        public float BedTk;
        public string Note;
    }

    /// <summary>
    /// The crew between shifts (RESEARCH.md: one day on, one day off; sleep in the bus or pay for a
    /// bed; no sick pay, so resting means not eating). Lives across days; the sandbox keeps one.
    /// </summary>
    public sealed class Household
    {
        public int Day = 1;
        public bool WorkToday = true;
        public float SavingsTk;
        public float FatigueCarried;       // where the next shift starts
        public readonly BusCondition Bus = new BusCondition();   // the same bus every day
        public int CrewInjuredDays;        // shifts still to drive hurt
        public int BusInYardDays;          // days the bus still sits in the dumping yard
        public int WalkedAwayCount;        // buses left on their side
        public readonly List<DaySummary> Days = new List<DaySummary>();

        /// <summary>A roadside brake service, paid from savings (into debt if need be).</summary>
        public void ServiceBrakes(BusSettings b, EconomySettings e)
        {
            SavingsTk -= e.BrakeServiceTk * e.MoneyScale;
            Bus.ServiceBrakes(b);
        }

        /// <summary>The paper that means nothing, bought for a few days of fewer questions.</summary>
        public void BuyPapers(EconomySettings e)
        {
            SavingsTk -= e.FitnessTk * e.MoneyScale;
            Bus.PapersValidUntilDay = Day + e.FitnessDays;
        }

        /// <summary>Close a worked day: the crew's net goes into savings, food comes out.</summary>
        public DaySummary CloseWorkedDay(Ledger ledger, EconomySettings e)
        {
            var day = new DaySummary { Day = Day, Worked = true, CrewNetTk = ledger.CrewNetTk, FoodTk = e.FoodTkPerDay * e.MoneyScale };
            SavingsTk += day.CrewNetTk - day.FoodTk;
            Days.Add(day);
            return day;
        }

        /// <summary>Where to sleep: the bus floor is free and recovers little; a bed costs and recovers most.</summary>
        public void Sleep(bool bed, float fatigueAtEnd, FatigueSettings f, EconomySettings e)
        {
            DaySummary day = Days.Count > 0 ? Days[Days.Count - 1] : null;
            if (bed)
            {
                float cost = e.BedTk * e.MoneyScale;
                SavingsTk -= cost;
                if (day != null) day.BedTk = cost;
                FatigueCarried = Mathf.Min(fatigueAtEnd, f.AfterBed);
            }
            else
            {
                FatigueCarried = Mathf.Max(f.AfterBusFloor, fatigueAtEnd * f.BusFloorKeeps);
            }
        }

        /// <summary>A day off: no zoma, no fares, food still, and the body comes back.</summary>
        public DaySummary RestDay(FatigueSettings f, EconomySettings e)
        {
            Day++;
            var day = new DaySummary { Day = Day, Worked = false, FoodTk = e.FoodTkPerDay * e.MoneyScale, Note = "Rested." };
            SavingsTk -= day.FoodTk;
            FatigueCarried = Mathf.Min(FatigueCarried, f.AfterRestDay);
            Days.Add(day);
            WorkToday = true;
            return day;
        }

        public void StartNextWorkDay()
        {
            Day++;
            WorkToday = true;
            Bus.BrakeWearToday = 0f;
            if (CrewInjuredDays > 0) CrewInjuredDays--;
        }

        /// <summary>After a rollover: the crew drives hurt for a while.</summary>
        public void NoteRollover(bool walkedAway, EconomySettings e)
        {
            CrewInjuredDays = e.CrewInjuryDays + 1;   // counts down at the next start
            if (walkedAway) WalkedAwayCount++;
        }

        /// <summary>The sergeant sent the bus to the dumping yard: no bus for a while.</summary>
        public void NoteSeizure(EconomySettings e)
        {
            BusInYardDays = e.DumpingDays;
        }

        /// <summary>
        /// A day with the bus in the yard: like a rest day, but not chosen. No zoma is owed (the owner
        /// has no bus either), food still costs, the body comes back, and the yard count falls.
        /// </summary>
        public DaySummary YardDay(FatigueSettings f, EconomySettings e)
        {
            Day++;
            if (BusInYardDays > 0) BusInYardDays--;
            var day = new DaySummary { Day = Day, Worked = false, FoodTk = e.FoodTkPerDay * e.MoneyScale, Note = "The bus is in the dumping yard." };
            SavingsTk -= day.FoodTk;
            FatigueCarried = Mathf.Min(FatigueCarried, f.AfterRestDay);
            Days.Add(day);
            WorkToday = BusInYardDays == 0;
            return day;
        }
    }
}
