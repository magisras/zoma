using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>What a pedestrian is doing. Vehicles ignore this field.</summary>
    public enum PedestrianState { Waiting, Crossing }

    /// <summary>
    /// One thing on the road: a vehicle, the player's bus, or a pedestrian. Plain data; the
    /// behaviour lives in Steering, HornSystem and TrafficSim so it can be read in one place.
    ///
    /// Two coordinate systems are kept in sync every step:
    ///   corridor coords (S, Lateral, Speed) are what the AI reasons in;
    ///   world coords (Position, Yaw) are what gets drawn.
    /// NPCs are integrated in corridor coords and the world pose is derived. The player's bus is
    /// integrated in world coords (it can turn freely) and its corridor coords are derived by
    /// projecting onto the corridor.
    /// </summary>
    public sealed class Agent
    {
        public int Id;
        public Corridor Corridor;        // the road this agent reasons on
        public VehicleClass Class;
        public VehicleShape Shape;
        public bool IsPlayer;

        // Personality is the numbers (RESEARCH.md). Mass decides who yields; nerve decides who bluffs.
        public float Mass;
        public float Nerve;

        // Corridor coordinates.
        public float S;
        public float Lateral;
        public float Speed;          // m/s along +S. Pedestrians use it for their crossing speed.
        public float TargetLateral;  // where steering wants to be
        public float DesiredSpeed;   // how fast the driver would go on an empty road

        // World pose, derived.
        public Vector3 Position;
        public float Yaw;            // radians, forward = (sin yaw, 0, cos yaw)

        // Transient state the behaviours read and write.
        public float YieldTimer;         // seconds left of an active yield; 0 = not yielding
        public float BluffTimer;         // seconds left of a "no, I'm not moving" decision
        public float HornTimer;          // seconds left of visible horn (for rendering / HUD)
        public float LastContactTime = -999f;
        public Junction LeakingThrough;  // a closed junction this driver decided to run anyway
        public BusLoad Load;             // passengers and door; null unless this is a bus that carries people
        public RivalBrain Brain;         // decision layer; null for generic traffic and the player
        public bool Persistent;          // never recycled by the population keeper (named crews)
        public Agent GhostOf;
        public bool Rolled;              // lying on its side (the player's bus after a rollover)            // a stand-in for another agent on a second corridor (the player on the oncoming road)
        public float LateralOverride = float.NaN;   // set by the decision layer to own the lateral this step

        // Pedestrians only.
        public PedestrianState PedState = PedestrianState.Waiting;
        public float CrossDirection;     // +1 = walking toward +Lateral, −1 toward −Lateral
        public float WaitTimer;          // seconds before considering the next crossing

        public bool IsPedestrian => Class == VehicleClass.Pedestrian;
        public bool IsPlayerOrGhost => IsPlayer || (GhostOf != null && GhostOf.IsPlayer);
        public bool IsYielding => YieldTimer > 0f;
        public bool IsHorning => HornTimer > 0f;
        public float HalfLength => Shape.Length * 0.5f;
        public float HalfWidth => Shape.Width * 0.5f;

        /// <summary>
        /// Bumper-to-bumper gap from my front to another agent's rear, along the road (negative when
        /// overlapping). Goes through the corridor so loops measure the short way round.
        /// </summary>
        public float GapTo(Agent other)
        {
            return Corridor.DeltaS(S, other.S) - HalfLength - other.HalfLength;
        }

        /// <summary>True when the two agents' lateral bands overlap, with a little margin.</summary>
        public bool OverlapsLaterally(Agent other, float margin)
        {
            return Mathf.Abs(Lateral - other.Lateral) < HalfWidth + other.HalfWidth + margin;
        }

        /// <summary>Same test, but for a lateral position this agent is considering moving to.</summary>
        public bool WouldOverlapLaterally(float lateral, Agent other, float margin)
        {
            return Mathf.Abs(lateral - other.Lateral) < HalfWidth + other.HalfWidth + margin;
        }
    }
}
