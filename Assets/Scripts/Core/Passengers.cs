using System.Collections.Generic;
using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>Who is getting on. Each kind boards at its own pace and pays its own fare.</summary>
    public enum PassengerKind { Regular, Student, ElderlyWithSack, Arguer }

    /// <summary>One rider: where they are going, how long they take at the door, what they pay.</summary>
    public sealed class Passenger
    {
        public PassengerKind Kind;
        public int DestinationZone;      // index into TrafficSim.Zones
        public float BoardingSeconds;
        public float FareFactor = 1f;    // student half fare
        public float FareTk;             // set when they board
        public bool Paid;                // the conductor's job (ledger step)
    }

    /// <summary>
    /// A stretch of kerb where people wait. RESEARCH.md: "demand zones along the road rather than
    /// fixed stops, with hot clusters at junctions". People accumulate by a rate; a bus with its
    /// door open nearby takes them, first door first.
    /// </summary>
    public sealed class DemandZone
    {
        public string Name;
        public int Index;
        public float S;
        public int Side = -1;                 // −1: left kerb. Bangladesh drives on the left; doors are on the left.
        public float RatePerMinute;           // how fast the crowd grows
        public readonly List<Passenger> Waiting = new List<Passenger>();
        public float Accumulator;             // fractional people, spills over into Waiting
        public Vector3 Position;
    }

    /// <summary>
    /// Passengers aboard a bus and the state of its door. Attached to any bus agent, player or AI.
    /// </summary>
    public sealed class BusLoad
    {
        public readonly List<Passenger> Aboard = new List<Passenger>();
        public bool DoorOpen;
        public float DoorOpenedAt = -1f;       // sim time; earliest door at a zone gets the next passenger
        public float BoardingTimer;            // seconds left on the person currently at the door
        public Passenger AtDoor;               // who is on the step right now
        public bool AtDoorIsAlighting;
        public float FaresTk;                  // fares taken today (before the conductor leaks any)
        public int Boarded, Alighted, MissedAlights;
        public DemandZone LastServed;          // the zone we last worked, so one stop isn't counted twice

        public int Count => Aboard.Count;
    }
}
