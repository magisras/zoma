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
        private DemandZone _calledZone;

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
            Agent behind = _sim.OwnBusBehind(out float behindM);
            if (behind != null && behindM < v.BusBehindCloseMetres)
                Say("gap-behind", v.GapCooldownSeconds, Speaker.Helper, behind.Brain.CrewName + " is " + Mathf.RoundToInt(behindM) + " metres behind! Go, go!");
            Agent ahead = _sim.OwnBusAhead(out float aheadM);
            if (ahead != null && aheadM > v.BusAheadFarMetres && (behind == null || behindM > v.BusBehindCloseMetres * 2f))
                Say("gap-ahead", v.GapCooldownSeconds * 2f, Speaker.Helper, ahead.Brain.CrewName + " is far ahead. Easy. Let them fill us up.");

            DemandZone next = RivalAI.NextZone(_sim, bus);
            if (next != null && next != _calledZone)
            {
                float ds = _sim.Corridor.DeltaS(bus.S, next.S);
                if (ds > 0f && ds < v.CrowdCallMetres && next.Waiting.Count >= v.CrowdCallMinimum)
                {
                    _calledZone = next;
                    Say("crowd", 0f, Speaker.Helper, next.Name + "! " + next.Waiting.Count + " people! Stop here, stop here!");
                }
            }
            if (next != _calledZone && _calledZone != null && Mathf.Abs(_sim.Corridor.DeltaS(bus.S, _calledZone.S)) > v.CrowdCallMetres) _calledZone = null;

            if (m.HornPresses > _hornsSeen) { _hornsSeen = m.HornPresses; _silence = 0f; }
            else _silence += dt;
            Steering.FindAhead(_sim.Agents, bus, bus.Lateral, 40f, out float gap);
            if (_silence > _sim.Tuning.Horn.SilenceComplaintSeconds && gap < 30f && bus.Speed > 2f)
                Say("silence", v.SilenceCooldownSeconds, Speaker.Helper, "Why aren't you honking? Nobody moves for a quiet bus!");

            if (load.DoorOpen && bus.Speed > _sim.Tuning.Passengers.DoorSpeedMs + 2f)
                Say("door", v.DoorCooldownSeconds, Speaker.Helper, "Door's open and we're flying. Someone will fall!");

            if (_sim.Fatigue.MicroSleeps > _sleepsSeen) { _sleepsSeen = _sim.Fatigue.MicroSleeps; Say("sleep", 5f, Speaker.Helper, "Ostad! Ostad! Wake up!"); }

            // ---- The conductor: the sergeant, the arguers, the count.
            if (_sim.Economy.Sergeant.Active && _sim.Economy.Sergeant.ReleaseAt < 0f)
                Say("sergeant", 30f, Speaker.Conductor, "Sergeant. I'm hiding the cash. Pay him, it's cheaper than the case.");
            if (load.AtDoor != null && !load.AtDoorIsAlighting && load.AtDoor.Kind == PassengerKind.Arguer)
                Say("arguer", 20f, Speaker.Conductor, "Half fare? Show me the card. No card, full fare.");
            if (load.Boarded >= _boardedSeen + 10) { _boardedSeen = load.Boarded; Say("count", 0f, Speaker.Conductor, load.Count + " aboard. Tk " + load.FaresTk.ToString("0") + " so far."); }

            // ---- Passengers: faster, stop here, overtake him.
            if (load.MissedAlights > _missedSeen) { _missedSeen = load.MissedAlights; Say("missed", 0f, Speaker.Passenger, "Stop! Stop! I'm getting off here!"); }

            bool clear = gap > 35f;
            _slowWithClearRoad = clear && bus.Speed < v.SlowKmh / 3.6f && !load.DoorOpen ? _slowWithClearRoad + dt : 0f;
            if (_slowWithClearRoad > v.SlowSecondsBeforeComplaint && load.Count > 5)
                Say("faster", v.PassengerCooldownSeconds, Speaker.Passenger, "Driver! Faster! We're not on a picnic!");

            _stuckBehind = !clear && gap < 15f && bus.Speed < 4f && bus.Speed > 0.2f ? _stuckBehind + dt : 0f;
            if (_stuckBehind > v.SlowSecondsBeforeComplaint && load.Count > 5)
                Say("overtake", v.PassengerCooldownSeconds, Speaker.Passenger, "Go round him! Take the other side!");
        }

        /// <summary>
        /// What a crew says at the terminal at the end of the day (RESEARCH.md: grudge > 3 picks cold
        /// lines; a shared bad day picks tired, friendly ones).
        /// </summary>
        public string TerminalLine(RivalBrain crew, Ledger ledger)
        {
            if (crew.Grudge > _sim.Tuning.Memory.ColdLinesAbove)
                return crew.CrewName + " walks past without a word.";
            if (crew.Grudge > 1)
                return crew.CrewName + ": \"You cut me twice today. Tomorrow I won't let you.\"";
            if (ledger.CrewNetTk < 0f)
                return crew.CrewName + ": \"Nothing left after the deposit again. Same for us. Tea?\"";
            if (crew.Grudge < 0)
                return crew.CrewName + ": \"Thanks for the room at Block 11. See you at six.\"";
            return crew.CrewName + ": \"Long day. Sleep in the bus or go home?\"";
        }

        private void Say(string trigger, float cooldown, Speaker speaker, string text, string who = null)
        {
            float now = _sim.Metrics.Time;
            float last;
            if (_lastSaid.TryGetValue(trigger, out last) && now - last < cooldown) return;
            _lastSaid[trigger] = now;
            Lines.Add(new VoiceLine { Speaker = speaker, Text = text, Who = who, Time = now });
            if (Lines.Count > 60) Lines.RemoveAt(0);
        }
    }
}
