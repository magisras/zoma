using System.Collections.Generic;

namespace TwentyTons.Core
{
    /// <summary>
    /// Everything the crew and the passengers can say, as data. English is the subtitle for now;
    /// the Bangla column is empty until a native writer fills it, and the recordings follow.
    /// Keyed by a short id that the code refers to, so a line can be rewritten without touching
    /// logic. In Unity this table becomes an asset; the ids stay.
    ///
    /// Lines with a {0} take one value (a name, a count, a distance).
    /// </summary>
    public sealed class VoiceLineText
    {
        public string Id;
        public Speaker Speaker;
        public string English;
        public string Bangla;        // to be written by a native speaker; left empty on purpose
        public string Note;          // for the writer: when it is said, what it must do

        public VoiceLineText(string id, Speaker speaker, string english, string note)
        {
            Id = id; Speaker = speaker; English = english; Note = note; Bangla = "";
        }
    }

    public static class VoiceLines
    {
        private static readonly Dictionary<string, VoiceLineText> _byId = new Dictionary<string, VoiceLineText>();

        public static readonly VoiceLineText[] All =
        {
            // ---- The helper: the gap, the crowd, the horn, the door, the body.
            new VoiceLineText("gap-behind", Speaker.Helper, "{0} is {1} metres behind! Go, go!", "A same-company bus is close behind; the stop ahead is at risk."),
            new VoiceLineText("gap-ahead", Speaker.Helper, "{0} is far ahead. Easy. Let them fill us up.", "The bus ahead has pulled away: slow and gather the crowd it leaves behind."),
            new VoiceLineText("crowd", Speaker.Helper, "{0}! {1} people! Stop here, stop here!", "Shouted at the driver as a crowd comes into view."),
            new VoiceLineText("silence", Speaker.Helper, "Why aren't you honking? Nobody moves for a quiet bus!", "After 20 s without a horn with traffic ahead. The game's one explicit lesson, said by a person."),
            new VoiceLineText("door", Speaker.Helper, "Door's open and we're flying. Nobody can get on like this!", "Door open above the jump speed."),
            new VoiceLineText("fall-stumble", Speaker.Helper, "She slipped. Slow down, ostad, slow down.", "Someone stumbled at the door at low speed."),
            new VoiceLineText("fall-injury", Speaker.Helper, "He's down! He's down! Stop the bus, stop!", "Someone fell at speed. The crowd follows."),
            new VoiceLineText("sleep", Speaker.Helper, "Ostad! Ostad! Wake up!", "The driver's eyes closed for a moment."),
            new VoiceLineText("headon", Speaker.Helper, "Bus! Bus coming! Back, back, come back!", "On the wrong side with something heavy coming the other way."),
            new VoiceLineText("brakes", Speaker.Helper, "Brakes are soft, ostad. Leave room. Tell the owner, for all the good it does.", "Once a day when the brakes are worn past 70 %."),

            // ---- The conductor: money, the sergeant, the arguer, the wrong side.
            new VoiceLineText("sergeant", Speaker.Conductor, "Sergeant. I'm hiding the cash. Pay him, it's cheaper than the case.", "The sergeant's hand is up."),
            new VoiceLineText("arguer", Speaker.Conductor, "Half fare? Show me the card. No card, full fare.", "A student-fare argument at the door."),
            new VoiceLineText("count", Speaker.Conductor, "{0} aboard. Tk {1} so far.", "Every ten boardings: the running count."),
            new VoiceLineText("wrongside", Speaker.Conductor, "Other side's empty. Cross over, we'll come back before the sergeant.", "Stuck in a jam with the oncoming road clear."),
            new VoiceLineText("rollover", Speaker.Conductor, "Get them out! Get them out through the windows!", "The bus has gone onto its side."),

            // ---- Passengers: faster, stop here, go round him.
            new VoiceLineText("missed", Speaker.Passenger, "Stop! Stop! I'm getting off here!", "Carried past their stop."),
            new VoiceLineText("faster", Speaker.Passenger, "Driver! Faster! We're not on a picnic!", "Crawling on a clear road with people aboard."),
            new VoiceLineText("overtake", Speaker.Passenger, "Go round him! Take the other side!", "Stuck behind something slow with people aboard."),

            // ---- The terminal, at the end of the day. {0} is the crew's name.
            new VoiceLineText("terminal-cold", Speaker.Crew, "{0} walks past without a word.", "Grudge above the cold threshold."),
            new VoiceLineText("terminal-sore", Speaker.Crew, "{0}: \"You cut me twice today. Tomorrow I won't let you.\"", "Grudge of two or three."),
            new VoiceLineText("terminal-badday", Speaker.Crew, "{0}: \"Nothing left after the deposit again. Same for us. Tea?\"", "No grudge, and the crew's day ended in the red."),
            new VoiceLineText("terminal-thanks", Speaker.Crew, "{0}: \"Thanks for the room at Block 11. See you at six.\"", "Negative grudge: the player let them through."),
            new VoiceLineText("terminal-plain", Speaker.Crew, "{0}: \"Long day. Sleep in the bus or go home?\"", "Nothing in particular happened between you."),
        };

        static VoiceLines()
        {
            foreach (VoiceLineText line in All) _byId[line.Id] = line;
        }

        public static VoiceLineText Get(string id)
        {
            VoiceLineText line;
            if (!_byId.TryGetValue(id, out line)) throw new KeyNotFoundException("No voice line with id " + id);
            return line;
        }

        /// <summary>The subtitle: Bangla when it exists, else English, with the values filled in.</summary>
        public static string Text(string id, params object[] values)
        {
            VoiceLineText line = Get(id);
            string template = string.IsNullOrEmpty(line.Bangla) ? line.English : line.Bangla;
            return values.Length == 0 ? template : string.Format(template, values);
        }
    }
}
