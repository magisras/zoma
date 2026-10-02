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
        public float LastYawRate;      // rad/s this step; speed × yaw rate is the lateral acceleration that tips a bus

        public float MassKg(BusSettings b) => b.TareTonnes * 1000f + Passengers * b.PassengerKg;

        public void Step(Agent bus, Corridor corridor, BusSettings b, float dt)
        {
            float mass = MassKg(b);
            float speed = bus.Speed;
            // Held (a sergeant's hand, the end of the day): the pedals are overridden, not overwritten.
            float throttleIn = Held ? 0f : Throttle;
            float brakeIn = Held ? 1f : Brake;

            // Engine: a fixed power means acceleration falls as speed rises and as mass rises.
            float powerLimited = (b.EnginePowerKw * 1000f) / (mass * Mathf.Max(speed, 1f));
            float engine = Mathf.Min(b.MaxAccelMs2, powerLimited) * Mathf.Clamp01(throttleIn);

            // Brakes: worn pads keep only part of their bite. This is the loan against tomorrow.
            float brake = b.BrakeDecelNewMs2 * (1f - b.BrakeWearLoss * Mathf.Clamp01(BrakeWear)) * Mathf.Clamp01(brakeIn);

            // Losses: rolling resistance, air, and the market stalls if you leave the road.
            float drag = b.RollingDecelMs2 + b.AirDragPerMs2 * speed * speed;
            if (Mathf.Abs(bus.Lateral) > corridor.HalfWidth + b.OffRoadToleranceMetres) drag += b.OffRoadDecelMs2;

            speed += (engine - brake - (speed > 0f ? drag : 0f)) * dt;
            speed = Mathf.Clamp(speed, 0f, b.MaxSpeedKmh / 3.6f);

            // Steering: the wheel turns slower the faster you go. That is the "heavy steering".
            float rate = b.SteerRateDegPerSec * Mathf.Deg2Rad / (1f + speed / Mathf.Max(0.1f, b.SteerHeavinessSpeed));
            float wanted = Mathf.Clamp(Steer, -1f, 1f) * b.MaxSteerAngleDeg * Mathf.Deg2Rad;
            SteerAngle = Mathf.MoveTowards(SteerAngle, wanted, rate * dt);

            // Bicycle model: yaw rate = v / L × tan(δ).
            float yawRate = speed / Mathf.Max(0.5f, b.WheelbaseMetres) * (float)System.Math.Tan(SteerAngle);
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
