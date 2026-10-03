using System.Collections.Generic;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Core
{
    public enum Speaker { Helper, Conductor, Passenger, Crew }

    /// <summary>One thing somebody said, with when and who.</summary>
    public sealed class VoiceLine
    {
        public Speaker Speaker;
        public string Text;        // the subtitle (English for now; Bangla lines come with the voice recordings)
        public string Who;         // a crew name for Speaker.Crew
        public string Id;          // which line in VoiceLines
        public float Time;
    }

    /// <summary>
    /// The crew teaches (RESEARCH.md): "the helper shouts the gap, the conductor tells you to take
    /// the wrong side, passengers shout to go faster; their voices are the manual." Every line is
    /// chosen from the simulation's state, never generated, with a cooldown per trigger so nobody
    /// repeats themselves. The game never explains; these people do.
    /// </summary>
    public sealed class CrewVoice
    {
        public readonly List<VoiceLine> Lines = new List<VoiceLine>();

        private readonly TrafficSim _sim;
        private readonly Dictionary<string, float> _lastSaid = new Dictionary<string, float>();
        private float _silence;            // seconds since the player last honked
        private float _slowWithClearRoad;  // seconds crawling for no reason
        private float _stuckBehind;        // seconds held behind a slower vehicle
        private int _hornsSeen, _missedSeen, _sleepsSeen, _boardedSeen;
        private float _fallSeen = -999f;
        private int _rolloversSeen;
        private DemandZone _calledZone;
        private bool _driveDaySaid, _ropeSeen;
        private float _cameraTkSeen;

        public CrewVoice(TrafficSim sim) { _sim = sim; }

        public VoiceLine Latest => Lines.Count > 0 ? Lines[Lines.Count - 1] : null;

        public void Step(float dt)
        {
            Agent bus = _sim.Player;
            if (bus == null || _sim.Economy.DayOver) return;
            VoiceSettings v = _sim.Tuning.Voice;
            SimMetrics m = _sim.Metrics;
            BusLoad load = bus.Load;

            // ---- The helper: the gap, the crowd, the horn, the door.
            // Nobody shouts "go, go" at a bus on its side or under a sergeant's hand.
            bool canGo = !_sim.Rollover.Active && !_sim.Bus.Held;
            Agent behind = _sim.OwnBusBehind(out float behindM);
            if (canGo && behind != null && behindM < v.BusBehindCloseMetres)
                Say("gap-behind", v.GapCooldownSeconds, behind.Brain.CrewName, Mathf.RoundToInt(behindM));
            Agent ahead = _sim.OwnBusAhead(out float aheadM);
            if (ahead != null && aheadM > v.BusAheadFarMetres && (behind == null || behindM > v.BusBehindCloseMetres * 2f))
                Say("gap-ahead", v.GapCooldownSeconds * 2f, ahead.Brain.CrewName);

            DemandZone next = RivalAI.NextZone(_sim, bus);
            if (next != null && next != _calledZone)
            {
                float ds = _sim.Corridor.DeltaS(bus.S, next.S);
                if (ds > 0f && ds < v.CrowdCallMetres && next.Waiting.Count >= v.CrowdCallMinimum)
                {
                    _calledZone = next;
                    Say("crowd", 0f, next.Name, next.Waiting.Count);
                }
            }
            if (next != _calledZone && _calledZone != null && Mathf.Abs(_sim.Corridor.DeltaS(bus.S, _calledZone.S)) > v.CrowdCallMetres) _calledZone = null;

            if (m.HornPresses > _hornsSeen) { _hornsSeen = m.HornPresses; _silence = 0f; }
            else _silence += dt;
            Steering.FindAhead(_sim.Agents, bus, bus.Lateral, 40f, out float gap);
            if (_silence > _sim.Tuning.Horn.SilenceComplaintSeconds && gap < 30f && bus.Speed > 2f)
                Say("silence", v.SilenceCooldownSeconds);

            if (load.DoorOpen && bus.Speed > _sim.Tuning.Passengers.JumpSpeedMs)
                Say("door", v.DoorCooldownSeconds);
            if (load.LastFallTime > _fallSeen)
            {
                _fallSeen = load.LastFallTime;
                Say(load.LastFallWasInjury ? "fall-injury" : "fall-stumble", 0f);
            }

            if (_sim.PlayerGhost != null && m.WrongSideNow)
            {
                Agent oncoming = Steering.FindAhead(_sim.Agents, _sim.PlayerGhost, _sim.PlayerGhost.Lateral, 70f, out float headOn);
                // On their road, "ahead" of the ghost in its own direction is behind us; what we meet comes from the other way.
                Agent coming = Steering.FindHeavierBehind(_sim.Agents, _sim.PlayerGhost, 70f);
                if (coming != null || (oncoming != null && oncoming.Mass >= _sim.Tuning.Mass.Truck))
                    Say("headon", 6f);
            }
            if (_sim.Condition.BrakeWear > _sim.Tuning.Bus.SoftBrakesAbove && bus.Speed > 5f)
                Say("brakes", 600f);
            if (_sim.Rollover.Count > _rolloversSeen)
            {
                _rolloversSeen = _sim.Rollover.Count;
                Say("rollover", 0f);
            }
            if (_sim.Fatigue.MicroSleeps > _sleepsSeen) { _sleepsSeen = _sim.Fatigue.MicroSleeps; Say("sleep", 5f); }

            // ---- The street's control: the drive, the rope, the camera.
            if (_sim.Economy.DriveDay && !_driveDaySaid && _sim.Economy.ShiftSeconds > 3f) { _driveDaySaid = true; Say("driveday", 0f); }
            if (_sim.Bus.HeldByRope && !_ropeSeen) Say("rope", 30f);
            _ropeSeen = _sim.Bus.HeldByRope;
            if (_sim.Economy.Ledger.CameraTk > _cameraTkSeen) { _cameraTkSeen = _sim.Economy.Ledger.CameraTk; Say("camera", 0f); }

            // ---- The conductor: the sergeant, the arguers, the count.
            if (_sim.Economy.Sergeant.Active && _sim.Economy.Sergeant.ReleaseAt < 0f)
                Say("sergeant", 30f);
            if (load.AtDoor != null && load.AtDoor.Kind == PassengerKind.Arguer)
                Say("arguer", 20f);
            if (load.Boarded >= _boardedSeen + 10) { _boardedSeen = load.Boarded; Say("count", 0f, load.Count, load.FaresTk.ToString("0")); }

            // ---- Passengers: faster, stop here, overtake him.
            if (load.MissedAlights > _missedSeen) { _missedSeen = load.MissedAlights; Say("missed", 0f); }

            bool clear = gap > 35f;
            _slowWithClearRoad = clear && bus.Speed < v.SlowKmh / 3.6f && !load.DoorOpen ? _slowWithClearRoad + dt : 0f;
            if (_slowWithClearRoad > v.SlowSecondsBeforeComplaint && load.Count > 5)
                Say("faster", v.PassengerCooldownSeconds);

            _stuckBehind = !clear && gap < 15f && bus.Speed < 4f && bus.Speed > 0.2f ? _stuckBehind + dt : 0f;
            if (_stuckBehind > v.SlowSecondsBeforeComplaint && load.Count > 5)
                Say("overtake", v.PassengerCooldownSeconds);
            if (_stuckBehind > v.SlowSecondsBeforeComplaint * 0.7f && _sim.Oncoming != null && !m.WrongSideNow)
                Say("wrongside", v.PassengerCooldownSeconds);
        }

        /// <summary>
        /// What a crew says at the terminal at the end of the day (RESEARCH.md: grudge > 3 picks cold
        /// lines; a shared bad day picks tired, friendly ones).
        /// </summary>
        public string TerminalLine(RivalBrain crew, Ledger ledger)
        {
            int today = crew.Grudge - crew.GrudgeAtDayStart;      // a grudge carried from earlier days is not "today"
            string id = crew.Grudge > 1 && today < 1 ? "terminal-carried"
                      : crew.Grudge > _sim.Tuning.Memory.ColdLinesAbove ? "terminal-cold"
                      : crew.Grudge > 1 ? "terminal-sore"
                      : ledger.CrewNetTk < 0f ? "terminal-badday"
                      : crew.Grudge < 0 ? "terminal-thanks"
                      : "terminal-plain";
            return VoiceLines.Text(id, crew.CrewName);
        }

        /// <summary>Say the line with this id, unless it was said within its cooldown. Values fill the {0} {1} slots.</summary>
        private void Say(string id, float cooldown, params object[] values)
        {
            float now = _sim.Metrics.Time;
            float last;
            if (_lastSaid.TryGetValue(id, out last) && now - last < cooldown) return;
            _lastSaid[id] = now;
            VoiceLineText line = VoiceLines.Get(id);
            Lines.Add(new VoiceLine { Speaker = line.Speaker, Text = VoiceLines.Text(id, values), Id = id, Time = now });
            if (Lines.Count > 60) Lines.RemoveAt(0);
        }
    }
}
