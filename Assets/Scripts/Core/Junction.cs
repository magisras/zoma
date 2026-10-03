using System.Collections.Generic;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>Which stream of traffic the officer is letting through.</summary>
    public enum JunctionFlow { Main, Cross }

    /// <summary>
    /// What the lamps on the pole do (docs/STREET_CONTROL.md §1). They never change behaviour: the
    /// cane and the box do. Dark is Dhaka outside the 2025–26 central pilots, and our corridor.
    /// </summary>
    public enum SignalMode { Dark, Manual, Timer }

    /// <summary>
    /// Where a cross street meets the main road, with a traffic officer in the middle.
    ///
    /// RESEARCH.md: "The police hand is the traffic light. Signals are ignored; officers with a cane
    /// release lanes; drivers keep crossing red until the opposing flow physically stops them."
    /// So: the officer opens one direction at a time with variable timing (OfficerSettings); when a
    /// direction closes, a few drivers still "leak" through; and whatever the cane says, a vehicle
    /// already inside the box physically blocks the other stream.
    ///
    /// The junction square ("the box") is the overlap of both corridors. Stop lines sit a little
    /// before the box on each corridor.
    /// </summary>
    public sealed class Junction
    {
        public readonly Corridor Main;
        public readonly Corridor Cross;
        public readonly float MainS;      // where the cross street's centreline crosses the main corridor
        public readonly float CrossS;     // where the main road's centreline crosses the cross corridor
        public readonly Vector3 Centre;

        public JunctionFlow Open = JunctionFlow.Main;
        public float Timer;               // seconds until the officer switches
        public int LeakersLeft;           // vehicles still allowed to run the cane this phase
        public SignalMode Signal = SignalMode.Dark;
        public bool Roped;                // the constable stretched a rope across the closed approach: nobody leaks
        public bool Camera;               // an AI camera watches this junction: a cane run is an SMS to the owner

        // Who is physically in the box right now, counted each step by TrafficSim.
        public int MainInBox;
        public int CrossInBox;
        public readonly List<Agent> MainBoxAgents = new List<Agent>();
        public readonly List<Agent> CrossBoxAgents = new List<Agent>();

        /// <summary>The same officer controls both carriageways: this junction copies that one's cane.</summary>
        public Junction Mirror;
        /// <summary>The other carriageway's junction of the same crossing, whichever side the mirror link is on. Set each step by TrafficSim.</summary>
        public Junction Partner;
        /// <summary>Cross-street vehicles already through the partner's box and waiting to cross this one. They are let through.</summary>
        public int CrossCommitted;

        /// <summary>Of a pair, the junction cross traffic reaches first (the smaller S on the cross street).</summary>
        public Junction FirstOfPair => Partner == null || CrossS <= Partner.CrossS ? this : Partner;

        public Junction(Corridor main, float mainS, Corridor cross, float crossS)
        {
            Main = main;
            MainS = mainS;
            Cross = cross;
            CrossS = crossS;
            Centre = main.PositionAt(mainS, 0f);
        }

        /// <summary>Half-extent of the box along the main road: the cross street's half width.</summary>
        public float MainHalfSpan => Cross.HalfWidth;

        /// <summary>Half-extent of the box along the cross street: the main road's half width.</summary>
        public float CrossHalfSpan => Main.HalfWidth;

        /// <summary>Stop line S on a given corridor, or NaN if the corridor isn't part of this junction.</summary>
        public float StopLineOn(Corridor corridor, float setback)
        {
            if (corridor == Main) return MainS - MainHalfSpan - setback;
            if (corridor == Cross) return CrossS - CrossHalfSpan - setback;
            return float.NaN;
        }

        public bool IsOpenFor(Corridor corridor)
        {
            return corridor == Main ? Open == JunctionFlow.Main : Open == JunctionFlow.Cross;
        }

        /// <summary>Is the other stream's traffic physically in the box?</summary>
        public bool BoxBlockedFor(Corridor corridor)
        {
            return corridor == Main ? CrossInBox > 0 : MainInBox > 0;
        }

        /// <summary>
        /// Is the other stream's traffic in the box actually across <paramref name="asker"/>'s path? A nose
        /// poked half a metre over the line at the kerb side does not stop a bus crossing through the middle:
        /// Dhaka traffic squeezes past it, and if it did not, that nose and the bus would wait for each other
        /// for the rest of the day (seed 1 of the 3 Oct batch locked a crossing for 500 s this way). Each
        /// standing vehicle in the box is projected onto the asker's own road: it blocks when its length lies
        /// within the asker's width, plus a hand's breadth. One still moving sweeps the box and blocks as before.
        /// </summary>
        public bool BoxBlockedFor(Corridor corridor, Agent asker)
        {
            List<Agent> others = corridor == Main ? CrossBoxAgents : MainBoxAgents;
            if (others.Count == 0) return false;
            Corridor theirs = corridor == Main ? Cross : Main;
            float sOnTheirs, unused;
            theirs.Project(asker.Position, out sOnTheirs, out unused);   // where the asker's line meets their road
            for (int i = 0; i < others.Count; i++)
            {
                Agent o = others[i];
                if (o.Speed > 0.5f) return true;                       // still moving: it will be across the path
                if (Mathf.Abs(theirs.DeltaS(sOnTheirs, o.S)) < asker.HalfWidth + o.HalfLength + 0.3f) return true;
            }
            return false;
        }

        /// <summary>The officer's clock. Variable timing is the point: nobody can plan around it.</summary>
        public void Tick(float dt, OfficerSettings officer, SeededRandom random)
        {
            if (Mirror != null)
            {
                if (Open != Mirror.Open) LeakersLeft = Mirror.Roped ? 0 : officer.LeakersPerCycle;
                Open = Mirror.Open;
                Timer = Mirror.Timer;
                Roped = Mirror.Roped;
                Camera = Mirror.Camera;
                return;
            }
            Timer -= dt;
            if (Timer > 0f) return;
            Open = Open == JunctionFlow.Main ? JunctionFlow.Cross : JunctionFlow.Main;
            Timer = random.Range(officer.OpenMinSeconds, officer.OpenMaxSeconds);
            // Sometimes the constable walks out with the rope: then nobody runs the line.
            Roped = random.Chance(officer.RopeChance);
            LeakersLeft = Roped ? 0 : officer.LeakersPerCycle;
        }

        /// <summary>Is this S on this corridor inside the box (give or take a vehicle's half length)?</summary>
        public bool InBox(Corridor corridor, float s, float halfLength)
        {
            if (corridor == Main) return Mathf.Abs(corridor.DeltaS(MainS, s)) < MainHalfSpan + halfLength;
            if (corridor == Cross) return Mathf.Abs(corridor.DeltaS(CrossS, s)) < CrossHalfSpan + halfLength;
            return false;
        }
    }
}
