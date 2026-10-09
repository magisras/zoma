using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Unity
{
    /// <summary>
    /// The bus as a body on springs (README milestone 2): a rigid body on four wheel colliders, with
    /// the suspension of a worn Dhaka bus from the tuning table. The core <see cref="BusController"/>
    /// still decides what the engine and the air brakes give (power, lag, wear, air in the tanks) and
    /// how fast the wheel turns; this class only turns those into wheel torques and steer angles, and
    /// reads the result back into the core's Agent (position, yaw, speed, S, lateral) so everything
    /// built on the Agent keeps working. Mass is tare plus riders, and the centre of mass is high, so
    /// the lean in a bend, the dive under braking and the wallow after a kerb come from physics, not
    /// from animation.
    /// </summary>
    public sealed class PhysicsBus : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public WheelCollider[] Wheels { get; private set; }      // FL, FR, RL, RR
        public Transform[] WheelMeshes;                            // optional visuals, same order

        private BusSettings _b;
        private float _wheelRadius;

        /// <summary>Make the body and the wheels on this object. Called by the scene builder and on load.</summary>
        public void Build(BusSettings b, VehicleShape shape)
        {
            _b = b;
            _wheelRadius = b.WheelRadiusMetres;
            Body = GetComponent<Rigidbody>();
            if (Body == null) Body = gameObject.AddComponent<Rigidbody>();
            Body.mass = b.TareTonnes * 1000f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            Body.angularDamping = 0.5f;
            Body.linearDamping = 0f;

            // The body: a box from just above the wheels' bottom to the roof. Its own collider meets walls.
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = false;
            // Ground clearance of the collider: low enough that a 0.9 m median barrier or a pier meets the
            // body before the raycast wheels can ride up onto it, high enough to clear a 0.15 m kerb.
            float floor = 0.3f;
            box.center = new Vector3(0f, (floor + shape.Height) * 0.5f, 0f);
            box.size = new Vector3(shape.Width, shape.Height - floor, shape.Length);

            Wheels = new WheelCollider[4];
            float track = shape.Width * 0.5f - 0.3f;
            float rearAxle = b.FrontAxleMetres - b.WheelbaseMetres;
            // The collider hangs from the top of its travel and rests at mid travel (targetPosition 0.5),
            // so with the wheel centre one radius up at rest the top of travel is half the travel higher.
            float hub = _wheelRadius + 0.5f * b.SuspensionTravelMetres;
            Vector3[] at = {
                new Vector3(-track, hub, b.FrontAxleMetres), new Vector3(track, hub, b.FrontAxleMetres),
                new Vector3(-track, hub, rearAxle), new Vector3(track, hub, rearAxle) };
            string[] names = { "Wheel FL", "Wheel FR", "Wheel RL", "Wheel RR" };
            for (int i = 0; i < 4; i++)
            {
                Transform t = transform.Find(names[i]);
                if (t == null) { t = new GameObject(names[i]).transform; t.SetParent(transform, false); }
                t.localPosition = at[i];
                var w = t.GetComponent<WheelCollider>();
                if (w == null) w = t.gameObject.AddComponent<WheelCollider>();
                Wheels[i] = w;
                w.radius = _wheelRadius;
                w.suspensionDistance = b.SuspensionTravelMetres;
                w.wheelDampingRate = 1f;
                w.forceAppPointDistance = 0.1f;
                // Rear axle carries two thirds of the bus: a stiffer spring there than at the front.
                float cornerMass = Body.mass * (i < 2 ? 0.17f : 0.33f);
                float k = cornerMass * Mathf.Pow(2f * Mathf.PI * b.SuspensionHz, 2f);
                float c = 2f * b.SuspensionDamping * Mathf.Sqrt(k * cornerMass);
                w.suspensionSpring = new JointSpring { spring = k, damper = c, targetPosition = 0.5f };
                w.mass = 60f;
                var fwd = w.forwardFriction; fwd.stiffness = 1.0f; w.forwardFriction = fwd;
                // Sideways: the real grip of a loaded bus on tarmac, so it ploughs before it tips. The curve's
                // peak is the grip; past the slip the force settles a little lower.
                var side = w.sidewaysFriction;
                side.extremumSlip = 0.25f; side.extremumValue = b.TyreSidewaysGrip;
                side.asymptoteSlip = 0.6f; side.asymptoteValue = b.TyreSidewaysGrip * 0.85f;
                side.stiffness = 1.0f;
                w.sidewaysFriction = side;
            }
            SetLoad(0);
            // The sub-steps PhysX uses for the wheels at speed: more keep a heavy bus from jittering.
            Wheels[0].ConfigureVehicleSubsteps(5f, 12, 15);
        }

        /// <summary>Riders aboard: mass and the centre of mass (standing people raise it).</summary>
        public void SetLoad(int riders)
        {
            if (Body == null || _b == null) return;
            Body.mass = _b.TareTonnes * 1000f + riders * _b.PassengerKg;
            float standing = Mathf.Clamp01((riders - _b.Seats) / (float)Mathf.Max(1, _b.CrushCapacity - _b.Seats));
            Body.centerOfMass = new Vector3(0f, _b.CentreOfMassMetres + 0.25f * standing, 0.2f);
        }

        /// <summary>
        /// One physics step of driving. The core model turns the pedals into accelerations; here they
        /// become torques on the wheels. Reverse is a gentle negative torque, a crawl.
        /// </summary>
        public void Drive(BusController core, float forwardSpeed, float dt, bool reverse)
        {
            if (Wheels == null) return;
            core.Forces(Mathf.Abs(forwardSpeed), _b, dt, out float engineAccel, out float brakeDecel);
            core.TurnWheels(Mathf.Abs(forwardSpeed), _b, dt);

            float steerDeg = core.SteerAngle * Mathf.Rad2Deg;
            // Ackermann, roughly: the inner wheel turns a little more.
            Wheels[0].steerAngle = steerDeg;
            Wheels[1].steerAngle = steerDeg;

            AntiRoll(0, 1);
            AntiRoll(2, 3);

            float driveForce = Body.mass * engineAccel * (reverse ? -0.35f : 1f);
            float motorTorque = driveForce * _wheelRadius * 0.5f;              // two driven wheels
            float brakeTorque = Body.mass * brakeDecel * _wheelRadius * 0.25f;  // four braked wheels
            if (reverse && forwardSpeed > 0.5f) { motorTorque = 0f; brakeTorque = Mathf.Max(brakeTorque, Body.mass * 2f * _wheelRadius * 0.25f); }
            for (int i = 0; i < 4; i++)
            {
                Wheels[i].motorTorque = i >= 2 ? motorTorque : 0f;
                Wheels[i].brakeTorque = brakeTorque;
            }
            // The governor: the engine will not push past the top speed.
            if (forwardSpeed * 3.6f > _b.MaxSpeedKmh) for (int i = 2; i < 4; i++) Wheels[i].motorTorque = 0f;
        }

        /// <summary>
        /// The anti-roll bar of an axle: a force pair proportional to how differently the two wheels are
        /// compressed, pushing the outer side up and the inner side down. Keeps the lean to what a bus
        /// shows and the inner wheels on the road until the tyres have let go.
        /// </summary>
        private void AntiRoll(int left, int right)
        {
            WheelCollider l = Wheels[left], r = Wheels[right];
            float travelL = Compression(l), travelR = Compression(r);
            float force = (travelL - travelR) * _b.AntiRollNewtonsPerMetre;
            if (l.isGrounded) Body.AddForceAtPosition(l.transform.up * -force, l.transform.position);
            if (r.isGrounded) Body.AddForceAtPosition(r.transform.up * force, r.transform.position);
        }

        /// <summary>How far this wheel's spring is compressed, metres, 0 when hanging free.</summary>
        private float Compression(WheelCollider w)
        {
            if (!w.GetGroundHit(out WheelHit hit)) return 0f;
            float extension = (-w.transform.InverseTransformPoint(hit.point).y - w.radius) / w.suspensionDistance;
            return (1f - Mathf.Clamp01(extension)) * w.suspensionDistance;
        }

        /// <summary>Hold the bus to a speed with the brakes (the pavement crawl, the dirt): a soft cap.</summary>
        public void Cap(float maxSpeedMs, float forwardSpeed)
        {
            if (Wheels == null || forwardSpeed <= maxSpeedMs) return;
            float extra = Body.mass * 2.5f * _wheelRadius * 0.25f;
            for (int i = 0; i < 4; i++) Wheels[i].brakeTorque = Mathf.Max(Wheels[i].brakeTorque, extra);
        }

        /// <summary>Spin and place the wheel meshes from the colliders.</summary>
        public void UpdateWheelMeshes()
        {
            if (WheelMeshes == null || Wheels == null) return;
            for (int i = 0; i < 4 && i < WheelMeshes.Length; i++)
            {
                if (WheelMeshes[i] == null) continue;
                Wheels[i].GetWorldPose(out Vector3 pos, out Quaternion rot);
                WheelMeshes[i].position = pos;
                WheelMeshes[i].rotation = rot;
            }
        }

        /// <summary>
        /// The tripped rollover (docs/BUS.md §5: nearly every bus rollover is a kerb, a ditch or a railing
        /// catching the wheels at speed). Raycast wheels climb a kerb smoothly, so the trip is a rule from
        /// the core's Rollover: past the road at speed, the outer wheels catch and the body is thrown over.
        /// `side` is +1 to fall to the right, -1 to the left.
        /// </summary>
        public void Trip(float side)
        {
            if (Body == null) return;
            float impulse = Body.mass * 6.5f;                               // enough to put twenty tons past its balance
            Body.AddTorque(-transform.forward * side * impulse, ForceMode.Impulse);
            Body.AddForce(Vector3.up * Body.mass * 2.5f, ForceMode.Impulse);
        }

        /// <summary>Put the body somewhere, at rest.</summary>
        public void Teleport(Vector3 groundPoint, float yawRadians)
        {
            if (Body == null) return;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.position = groundPoint + Vector3.up * 0.05f;
            Body.rotation = Quaternion.Euler(0f, yawRadians * Mathf.Rad2Deg, 0f);
            transform.SetPositionAndRotation(Body.position, Body.rotation);
        }

        /// <summary>Speed along the body's own axis, m/s, negative when backing.</summary>
        public float ForwardSpeed => Body == null ? 0f : Vector3.Dot(Body.linearVelocity, transform.forward);
        /// <summary>Lean: roll angle of the body, degrees, positive to the right.</summary>
        public float RollDegrees => Body == null ? 0f : -Mathf.DeltaAngle(0f, transform.eulerAngles.z);
        /// <summary>Lateral acceleration felt by the body, m/s².</summary>
        public float LateralAccel => Body == null ? 0f : Vector3.Dot(Body.angularVelocity, Vector3.up) * ForwardSpeed;
    }
}
