using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwentyTons.Unity
{
    /// <summary>
    /// The player's bus on the real road (README milestone 2). The core <see cref="BusController"/>
    /// is the model: power-limited engine, air brakes that lag and fade with wear, steering that goes
    /// heavy with speed, mass from riders. With a <see cref="PhysicsBus"/> on the same object the bus
    /// is a body on springs and the model drives its wheels; without one it is moved kinematically,
    /// as in the browser sandbox. Either way the core's Agent (position, yaw, speed, S, lateral) is
    /// kept current, so everything built on the Agent works in both.
    ///
    /// Keys: W/up throttle, S/down brake, A/D or left/right steer, space full brake, X reverse at a
    /// walk, R back to the stand, [ and ] ten riders off and on (to feel the mass), C the camera.
    /// </summary>
    public sealed class PlayerBusDrive : MonoBehaviour
    {
        [Tooltip("Every number the bus model uses lives here (Assets/Data/TuningTable.asset).")]
        public TuningTable Tuning;
        [Tooltip("Assets/World/Corridor01/route.json from tools/osm/greybox.py: the main road as a polyline.")]
        public TextAsset RouteJson;
        [Tooltip("Metres along the route to start at; the stand is at 0.")]
        public float StartAlong = 30f;
        [Tooltip("Metres across the carriageway to start at; negative is the left (kerb) side. Bangladesh drives on the left, " +
                 "and route.json is the southbound carriageway, so -2.5 is the kerb lane.")]
        public float StartLateral = -2.5f;
        [Tooltip("How fast the steering input ramps, per second; the wheel itself is rate-limited in BusSettings.")]
        public float SteerInputRate = 3f;
        [Tooltip("Width of the pavement beside the carriageway, metres (greybox.py builds 2 m). On it the bus crawls.")]
        public float PavementMetres = 2.5f;
        [Tooltip("Top speed with wheels on the pavement, km/h: a kerb, people, stalls.")]
        public float PavementKmh = 12f;
        [Tooltip("Speed lost climbing the kerb, m/s: the bump (kinematic bus only; the physics body climbs the real kerb).")]
        public float KerbBumpMs = 1.5f;
        [Tooltip("Top speed with the wheels off the road altogether, km/h: dirt and rubble between blocks.")]
        public float OffRoadKmh = 8f;

        /// <summary>
        /// Scripted hands on the pedals, for drive tests run from the terminal (`unity command eval`):
        /// while Scripted is true these replace the keyboard. Instance is the bus in the open scene.
        /// </summary>
        public static bool Scripted;
        public static float ScriptThrottle, ScriptBrake, ScriptSteer;
        public static bool ScriptReverse;
        public static PlayerBusDrive Instance { get; private set; }

        public BusController Bus { get; private set; }
        public Agent Agent { get; private set; }
        public Corridor Corridor { get; private set; }
        public PhysicsBus Physics { get; private set; }

        /// <summary>One line of state for a test log.</summary>
        public string Status => $"t={_clock:0.0} s={Agent.S:0.0} lat={Agent.Lateral:0.00} v={(Physics != null ? Physics.ForwardSpeed : Agent.Speed) * 3.6f:0.0}km/h air={Bus.AirPressure:0.00} applied={Bus.BrakeApplied:0.00} wear={Bus.BrakeWear:0.00} riders={Bus.Passengers} yaw={Agent.Yaw * Mathf.Rad2Deg:0} latAcc={LateralAccel:0.0} roll={Roll:0.0}";

        private float _steerInput, _throttle, _brake, _steerWanted, _clock;
        private bool _reverse, _onPavement, _offRoad;
        private float _carriagewayHalf = 4f;
        private float[] _farS, _farEdge;      // distance along the route -> lateral of the far kerb (right side)
        private BoxCollider _ghostBox;        // kinematic bus only: its shape for the wall push-out

        private float LateralAccel => Physics != null ? Mathf.Abs(Physics.LateralAccel) : Mathf.Abs(Agent.Speed * Bus.LastYawRate);
        private float Roll => Physics != null ? Physics.RollDegrees : 0f;

        private void Awake()
        {
            Instance = this;
            SetUp();
        }

        /// <summary>Build the corridor and the model and put the bus at its start. Public so the scene
        /// builder can place the bus in edit mode too, instead of leaving it at the origin until Play.</summary>
        public void SetUp()
        {
            if (Tuning == null) Tuning = ScriptableObject.CreateInstance<TuningTable>();   // defaults, so the scene runs even unwired
            Corridor = LoadRoute(RouteJson);
            Agent = new Agent { Corridor = Corridor, Class = VehicleClass.Bus, Shape = VehicleShape.For(VehicleClass.Bus), IsPlayer = true };
            Bus = new BusController
            {
                BrakeWear = Tuning.Bus.StartingBrakeWear,
                AirPressure = Tuning.Bus.AirPressureAtDayStart,
                Passengers = Tuning.Bus.StartingPassengers,
            };
            Physics = GetComponent<PhysicsBus>();
            if (Physics != null)
            {
                Physics.Build(Tuning.Bus, Agent.Shape);
                Physics.SetLoad(Bus.Passengers);
            }
            else
            {
                _ghostBox = GetComponent<BoxCollider>();
                if (_ghostBox == null) _ghostBox = gameObject.AddComponent<BoxCollider>();
                _ghostBox.isTrigger = true;
                _ghostBox.center = Vector3.zero;
                _ghostBox.size = Vector3.one;                   // the cube is scaled to the bus, so the collider is too
            }
            PlaceAtStart();
        }

        /// <summary>The route file is {"xz":[x0,z0,...],"width":w,"farEdge":[...]}; a fallback straight road if missing.</summary>
        private Corridor LoadRoute(TextAsset json)
        {
            if (json != null)
            {
                RouteFile r = JsonUtility.FromJson<RouteFile>(json.text);
                if (r != null && r.xz != null && r.xz.Length >= 4)
                {
                    var pts = new Vector3[r.xz.Length / 2];
                    for (int i = 0; i < pts.Length; i++) pts[i] = new Vector3(r.xz[2 * i], 0f, r.xz[2 * i + 1]);
                    // The corridor is the carriageway plus the pavement on each side: the kerb is 15 cm and
                    // a Dhaka bus mounts it; the market stalls (the model's off-road drag) start beyond.
                    _carriagewayHalf = (r.width > 0f ? r.width : 24f) * 0.5f;
                    var corridor = new Corridor(pts, _carriagewayHalf * 2f + 2f * PavementMetres, false, "Rokeya Sarani");
                    // The far kerb of the oncoming carriageway, per route point, keyed by distance along.
                    if (r.farEdge != null && r.farEdge.Length == pts.Length)
                    {
                        _farS = new float[pts.Length];
                        _farEdge = r.farEdge;
                        for (int i = 1; i < pts.Length; i++) _farS[i] = _farS[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
                    }
                    return corridor;
                }
            }
            Debug.LogWarning("PlayerBusDrive: no route.json wired, driving a straight 3 km road");
            return new Corridor(new[] { Vector3.zero, new Vector3(0f, 0f, -3000f) }, 24f, false, "straight");
        }

        /// <summary>How far to the right of the route line the oncoming carriageway's far kerb is, here.</summary>
        private float FarEdgeAt(float s)
        {
            if (_farS == null) return Corridor.HalfWidth;
            int i = 1;
            while (i < _farS.Length - 1 && _farS[i] < s) i++;
            float t = Mathf.InverseLerp(_farS[i - 1], _farS[i], s);
            return Mathf.Lerp(_farEdge[i - 1], _farEdge[i], t);
        }

        private void PlaceAtStart()
        {
            Agent.Position = Corridor.PositionAt(StartAlong, StartLateral);
            Vector3 ahead = Corridor.PositionAt(StartAlong + 5f, StartLateral) - Agent.Position;
            Agent.Yaw = Mathf.Atan2(ahead.x, ahead.z);
            Agent.Speed = 0f;
            Bus.SteerAngle = 0f;
            Bus.Throttle = Bus.Brake = Bus.Steer = 0f;
            _steerInput = 0f;
            _clock = 0f;
            Corridor.Project(Agent.Position, out Agent.S, out Agent.Lateral);
            Rolled = false; _rolledAt = -1f; _lastLateral = Agent.Lateral; _wasOnPavement = false;
            if (Physics != null) Physics.Teleport(Agent.Position, Agent.Yaw);
            else Apply();
        }

        // ---------------------------------------------------------------- input

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null && !Scripted) return;
            if (k == null) k = InputSystem.AddDevice<Keyboard>();     // a headless test still needs the object

            float throttle = (k.wKey.isPressed || k.upArrowKey.isPressed) ? 1f : 0f;
            float brake = (k.sKey.isPressed || k.downArrowKey.isPressed) ? 0.6f : 0f;   // a normal stop; space is the panic
            if (k.spaceKey.isPressed) brake = 1f;
            float steerWanted = (k.aKey.isPressed || k.leftArrowKey.isPressed ? -1f : 0f) + (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f);
            bool reverse = k.xKey.isPressed;
            if (Scripted) { throttle = ScriptThrottle; brake = ScriptBrake; steerWanted = ScriptSteer; reverse = ScriptReverse; }

            if (k.rKey.wasPressedThisFrame) { PlaceAtStart(); return; }
            if (k.rightBracketKey.wasPressedThisFrame) SetRiders(Bus.Passengers + 10);
            if (k.leftBracketKey.wasPressedThisFrame) SetRiders(Bus.Passengers - 10);

            _throttle = throttle; _brake = brake; _steerWanted = steerWanted; _reverse = reverse;
            if (Physics == null) Tick(Time.deltaTime, throttle, brake, steerWanted);   // the kinematic bus moves per frame
            else Physics.UpdateWheelMeshes();
        }

        private void FixedUpdate()
        {
            if (Physics != null && !Scripted) PhysicsStep(Time.fixedDeltaTime);
        }

        public void SetRiders(int riders)
        {
            Bus.Passengers = Mathf.Clamp(riders, 0, Tuning.Bus.CrushCapacity);
            if (Physics != null) Physics.SetLoad(Bus.Passengers);
        }

        // ---------------------------------------------------------------- the physics bus

        /// <summary>One physics step: pedals to the model, the model to the wheels, the body back to the Agent.</summary>
        private void PhysicsStep(float dt)
        {
            _steerInput = Mathf.MoveTowards(_steerInput, _steerWanted, SteerInputRate * dt * (_steerWanted == 0f ? 2f : 1f));
            // Reverse is a gear: X alone opens the throttle, the body gets a backward torque.
            Bus.Throttle = _reverse ? Mathf.Max(_throttle, 1f) : _throttle;
            Bus.Brake = _brake;
            Bus.Steer = _steerInput;
            float forward = Physics.ForwardSpeed;
            Physics.Drive(Bus, forward, dt, _reverse);

            // The street's rules that are not yet objects: the pavement crawl, the dirt beyond.
            float far = FarEdgeAt(Agent.S);
            _onPavement = Agent.Lateral < -_carriagewayHalf || Agent.Lateral > far - PavementMetres;
            _offRoad = Agent.Lateral < -Corridor.HalfWidth || Agent.Lateral > far;
            if (_offRoad) Physics.Cap(OffRoadKmh / 3.6f, forward);
            else if (_onPavement) Physics.Cap(PavementKmh / 3.6f, forward);

            // The tripped rollover: leaving the road further than the tolerance at speed, the wheels catch
            // (the core's rule, Rollover.Check "left the road at speed"); the body is thrown physically.
            // Two trips: the kerb hit sideways at speed (the outer wheels catch the step), and leaving the
            // road altogether at speed (the ditch, the railing). Both need RolloverSpeedMs, 36 km/h.
            BusSettings b = Tuning.Bus;
            float lateralVel = (Agent.Lateral - _lastLateral) / dt;
            bool kerbHit = _onPavement && !_wasOnPavement && Mathf.Abs(lateralVel) > KerbTripSidewaysMs;
            float beyond = Mathf.Max(-Agent.Lateral - Corridor.HalfWidth, Agent.Lateral - far) - b.OffRoadToleranceMetres;
            if (!Rolled && forward >= b.RolloverSpeedMs && (kerbHit || beyond > b.OffRoadRolloverMetres))
            {
                Rolled = true;
                _rolledAt = _clock;
                Physics.Trip(lateralVel < 0f ? -1f : 1f);
            }
            _wasOnPavement = _onPavement;
            _lastLateral = Agent.Lateral;
            if (Rolled && Mathf.Abs(Physics.RollDegrees) < 20f && _clock - _rolledAt > 3f) Rolled = false;   // righted (R) or it rocked back

            SyncAgentFromBody();
            _clock += dt;
        }

        /// <summary>On its side. R puts it back on its wheels for now; the rope and the men come later.</summary>
        public bool Rolled { get; private set; }
        private float _rolledAt = -1f, _lastLateral;
        private bool _wasOnPavement;
        [Tooltip("Sideways speed into the kerb that trips the outer wheels, m/s, at or above the rollover speed. A bus drifting " +
                 "onto the pavement at a shallow angle climbs it; one thrown at it sideways goes over. placeholder")]
        public float KerbTripSidewaysMs = 2.5f;

        private void SyncAgentFromBody()
        {
            Transform t = Physics.Body.transform;
            Agent.Position = new Vector3(t.position.x, 0f, t.position.z);
            Vector3 f = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            if (f.sqrMagnitude > 1e-4f) Agent.Yaw = Mathf.Atan2(f.x, f.z);
            Agent.Speed = Mathf.Max(0f, Physics.ForwardSpeed);
            Bus.LastYawRate = Vector3.Dot(Physics.Body.angularVelocity, Vector3.up);
            Corridor.Project(Agent.Position, out Agent.S, out Agent.Lateral);
        }

        // ---------------------------------------------------------------- the kinematic bus (no PhysicsBus)

        /// <summary>One frame of kinematic driving: the pedals go to the model, the model moves the bus, walls are checked.</summary>
        public void Tick(float dt, float throttle, float brake, float steerWanted)
        {
            _steerInput = Mathf.MoveTowards(_steerInput, steerWanted, SteerInputRate * dt * (steerWanted == 0f ? 2f : 1f));
            Bus.Throttle = throttle;
            Bus.Brake = brake;
            Bus.Steer = _steerInput;
            const float step = 1f / 60f;
            float left = Mathf.Min(dt, 0.1f);
            while (left > 0f)
            {
                float h = Mathf.Min(step, left);
                if (_reverse && Agent.Speed < 0.5f) Reverse(h); else Bus.Step(Agent, Corridor, Tuning.Bus, h, FarEdgeAt(Agent.S));
                _clock += h;
                left -= h;
            }
            Kerb();
            StopAtWalls();
            Apply();
        }

        private void Reverse(float dt)
        {
            const float pace = 1.4f;                                   // m/s, a helper walking beside
            Agent.Speed = 0f;
            Bus.TurnWheels(pace, Tuning.Bus, dt);
            float yawRate = -pace / Mathf.Max(0.5f, Tuning.Bus.WheelbaseMetres) * Mathf.Tan(Bus.SteerAngle);
            Agent.Yaw += yawRate * dt;
            Vector3 forward = new Vector3(Mathf.Sin(Agent.Yaw), 0f, Mathf.Cos(Agent.Yaw));
            Agent.Position -= forward * (pace * dt);
            Corridor.Project(Agent.Position, out Agent.S, out Agent.Lateral);
        }

        private void Kerb()
        {
            float far = FarEdgeAt(Agent.S);
            bool onPavement = Agent.Lateral < -_carriagewayHalf || Agent.Lateral > far - PavementMetres;
            if (onPavement && !_onPavement) Agent.Speed = Mathf.Max(0f, Agent.Speed - KerbBumpMs);
            if (onPavement) Agent.Speed = Mathf.Min(Agent.Speed, PavementKmh / 3.6f);
            _onPavement = onPavement;
            _offRoad = Agent.Lateral < -Corridor.HalfWidth || Agent.Lateral > far;
            if (_offRoad) Agent.Speed = Mathf.Min(Agent.Speed, OffRoadKmh / 3.6f);
        }

        /// <summary>Kinematic bus: pushed out of what it overlaps and stopped, so it can be steered away again.</summary>
        private void StopAtWalls()
        {
            Vector3 half = new Vector3(Agent.Shape.Width * 0.5f - 0.05f, Agent.Shape.Height * 0.5f - 0.3f, Agent.Shape.Length * 0.5f - 0.05f);
            Vector3 centre = Agent.Position + Vector3.up * (Agent.Shape.Height * 0.5f + 0.3f);
            Quaternion rot = Quaternion.Euler(0f, Agent.Yaw * Mathf.Rad2Deg, 0f);
            Collider[] hits = UnityEngine.Physics.OverlapBox(centre, half, rot, ~0, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return;
            Vector3 push = Vector3.zero;
            foreach (Collider other in hits)
            {
                if (UnityEngine.Physics.ComputePenetration(_ghostBox, centre, rot, other, other.transform.position, other.transform.rotation, out Vector3 dir, out float dist))
                    push += dir * dist;
            }
            push.y = 0f;
            Agent.Position += push + push.normalized * 0.05f;
            Agent.Speed = 0f;
            Corridor.Project(Agent.Position, out Agent.S, out Agent.Lateral);
        }

        /// <summary>Kinematic bus: world pose from the model, the box sitting on the road.</summary>
        private void Apply()
        {
            transform.position = Agent.Position + Vector3.up * (Agent.Shape.Height * 0.5f);
            transform.rotation = Quaternion.Euler(0f, Agent.Yaw * Mathf.Rad2Deg, 0f);
        }

        // ---------------------------------------------------------------- terminal drive tests

        /// <summary>
        /// Drive for a while from the terminal with the scripted pedals, at 60 Hz, without waiting for the
        /// editor's own frames (which stop when its window is not in front). With a physics body the
        /// physics is stepped by hand too. Returns the status after.
        /// </summary>
        public string Simulate(float seconds, float throttle, float brake, float steer, bool reverse = false)
        {
            _reverse = reverse;
            int frames = Mathf.RoundToInt(seconds * 60f);
            if (Physics == null)
            {
                for (int i = 0; i < frames; i++) Tick(1f / 60f, throttle, brake, steer);
                return Status;
            }
            var was = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;
            _throttle = throttle; _brake = brake; _steerWanted = steer;
            for (int i = 0; i < frames; i++)
            {
                PhysicsStep(1f / 60f);
                UnityEngine.Physics.Simulate(1f / 60f);
            }
            UnityEngine.Physics.simulationMode = was;
            Physics.UpdateWheelMeshes();
            return Status;
        }

        /// <summary>A plain readout until there is a dashboard: what the model is doing this frame.</summary>
        private void OnGUI()
        {
            if (Bus == null) return;
            float kmh = (Physics != null ? Physics.ForwardSpeed : Agent.Speed) * 3.6f;   // signed: negative when backing
            string where = Rolled ? "  ON ITS SIDE (R to right it)" : _offRoad ? "  OFF THE ROAD" : _onPavement ? "  ON THE PAVEMENT" : "";
            string text =
                $"{kmh,5:0} km/h   air {Bus.AirPressure * 100f,3:0} %   brakes {Bus.BrakeApplied * 100f,3:0} %  (wear {Bus.BrakeWear * 100f:0} %)\n" +
                $"{Bus.Passengers} riders, {Bus.MassKg(Tuning.Bus) / 1000f:0.0} t   lateral {LateralAccel:0.0} m/s²   lean {Roll:+0.0;-0.0}°\n" +
                $"{Agent.S:0} m along, {Agent.Lateral:+0.0;-0.0} m across (kerb at {-_carriagewayHalf:0}){where}\n" +
                "W/S drive, A/D steer, space full brake, X reverse, [ ] riders, C camera, R reset";
            GUI.Label(new Rect(16f, 12f, 900f, 120f), text, new GUIStyle(GUI.skin.label) { fontSize = 18, richText = false });
        }

        [System.Serializable] private class RouteFile { public float[] xz; public float width; public float length; public float[] farEdge; }
    }
}
