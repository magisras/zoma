using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>
    /// The player's bus as a simple vehicle model: power-limited acceleration, brakes that fade with
    /// wear, and steering that gets heavy with speed. A "bicycle model": one steered front axle, one
    /// fixed rear axle, no tyre slip. Enough to feel twenty tons in the sandbox; in Unity (Milestone 2)
    /// a real vehicle physics package replaces this, fed by the same BusSettings numbers.
    ///
    /// Inputs are set each frame by whoever reads the keyboard; this class never touches input itself.
    /// </summary>
    public sealed class BusController
    {
        // Inputs, 0..1 for pedals, −1..1 for steering (negative = left).
        public float Throttle;
        public float Brake;
        public float Steer;

        // State.
        public int Passengers;         // kept in sync from the bus's Load by TrafficSim
        public float BrakeWear;        // 0 = new, 1 = metal on metal
        public float SteerAngle;       // current front-wheel angle, radians
        public bool Held;              // a sergeant's hand, or the end of the day: the bus does not move
        public bool HeldByRope;        // a constable's rope across the road ahead: brake, whatever the pedal says
        public float LastYawRate;      // rad/s this step; speed × yaw rate is the lateral acceleration that tips a bus
        public float AirPressure = 1f; // 0..1 in the tanks; braking force scales with it (docs/BUS.md)
        public float BrakeApplied;     // 0..1 what the drums are actually doing, lagging the pedal

        public float MassKg(BusSettings b) => b.TareTonnes * 1000f + Passengers * b.PassengerKg;

        public void Step(Agent bus, Corridor corridor, BusSettings b, float dt)
        {
            Step(bus, corridor, b, dt, corridor.HalfWidth);
        }

        /// <param name="roadFarEdge">Lateral of the far edge of the road on the +side: the oncoming carriageway's
        /// outer edge where there is one, so the wrong side is road, not market stalls.</param>
        /// <summary>
        /// The engine and the air brakes for this step, as accelerations (m/s²), with the air tanks and
        /// the pedal lag advanced. Shared by the kinematic Step below and by the Unity physics body, so
        /// the research numbers (power, lag, wear, air) drive both the same way.
        /// </summary>
        public void Forces(float speed, BusSettings b, float dt, out float engineAccel, out float brakeDecel)
        {
            float mass = MassKg(b);
            // Held (a sergeant's hand, the end of the day): the pedals are overridden, not overwritten.
            bool held = Held || HeldByRope;
            float throttleIn = held ? 0f : Throttle;
            float brakeIn = held ? 1f : Brake;

            // Engine: a fixed power means acceleration falls as speed rises and as mass rises.
            float powerLimited = (b.EnginePowerKw * 1000f) / (mass * Mathf.Max(speed, 1f));
            engineAccel = Mathf.Min(b.MaxAccelMs2, powerLimited) * Mathf.Clamp01(throttleIn);

            // Air brakes: the drums follow the pedal with a lag, and only as hard as the air in the tanks
            // allows. The compressor refills while the engine runs; each application spends some (holding
            // the pedal down costs nothing more; pumping it does).
            float before = BrakeApplied;
            BrakeApplied = Mathf.MoveTowards(BrakeApplied, Mathf.Clamp01(brakeIn), dt / Mathf.Max(0.01f, b.BrakeLagSeconds));
            float applied = Mathf.Max(0f, BrakeApplied - before);
            AirPressure = Mathf.Clamp01(AirPressure + dt / Mathf.Max(1f, b.AirBuildSeconds) - applied * b.AirPerApplication);
            // Brakes: worn pads keep only part of their bite. This is the loan against tomorrow.
            brakeDecel = b.BrakeDecelNewMs2 * (1f - b.BrakeWearLoss * Mathf.Clamp01(BrakeWear)) * BrakeApplied * AirPressure;
        }

        /// <summary>The front wheels follow the wheel, slower the faster the bus goes: the heavy steering.</summary>
        public void TurnWheels(float speed, BusSettings b, float dt)
        {
            float rate = b.SteerRateDegPerSec * Mathf.Deg2Rad / (1f + speed / Mathf.Max(0.1f, b.SteerHeavinessSpeed));
            float wanted = Mathf.Clamp(Steer, -1f, 1f) * b.MaxSteerAngleDeg * Mathf.Deg2Rad;
            SteerAngle = Mathf.MoveTowards(SteerAngle, wanted, rate * dt);
        }

        public void Step(Agent bus, Corridor corridor, BusSettings b, float dt, float roadFarEdge)
        {
            float speed = bus.Speed;
            Forces(speed, b, dt, out float engine, out float brake);

            // Losses: rolling resistance, air, and the market stalls if you leave the road.
            float drag = b.RollingDecelMs2 + b.AirDragPerMs2 * speed * speed;
            bool offRoad = bus.Lateral < -corridor.HalfWidth - b.OffRoadToleranceMetres || bus.Lateral > roadFarEdge + b.OffRoadToleranceMetres;
            if (offRoad) drag += b.OffRoadDecelMs2;

            speed += (engine - brake - (speed > 0f ? drag : 0f)) * dt;
            speed = Mathf.Clamp(speed, 0f, b.MaxSpeedKmh / 3.6f);

            // Steering: the wheel turns slower the faster you go. That is the "heavy steering".
            TurnWheels(speed, b, dt);

            // Bicycle model: yaw rate = v / L × tan(δ).
            float yawRate = speed / Mathf.Max(0.5f, b.WheelbaseMetres) * (float)System.Math.Tan(SteerAngle);
            // Tyres: the wheel can ask for more than the rubber holds. Past the grip the front scrubs and the bus
            // runs wide at the lateral acceleration the tyres allow, which is what a hard turn at speed does to a
            // bus on tarmac: it ploughs, it does not tip. Rollover.Check reads LastYawRate for what is left.
            if (speed > 0.5f)
            {
                float maxYaw = b.TyreGripMs2 / speed;
                float asked = Mathf.Abs(yawRate);
                yawRate = Mathf.Clamp(yawRate, -maxYaw, maxYaw);
                // Scrub: the front sliding costs speed, the more the further past the grip the wheel asks.
                if (asked > maxYaw)
                {
                    float scrub = b.TyreScrubDecelMs2 * Mathf.Clamp01((asked - maxYaw) / maxYaw);
                    speed = Mathf.Max(0f, speed - scrub * dt);
                }
            }
            LastYawRate = yawRate;
            bus.Yaw += yawRate * dt;

            Vector3 forward = new Vector3(Mathf.Sin(bus.Yaw), 0f, Mathf.Cos(bus.Yaw));
            bus.Position += forward * (speed * dt);
            bus.Speed = speed;

            // Put the bus into corridor coordinates so NPCs can reason about it.
            corridor.Project(bus.Position, out bus.S, out bus.Lateral);
        }
    }
}
