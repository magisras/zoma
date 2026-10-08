using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwentyTons.Unity
{
    /// <summary>
    /// The first drive on the real road (README milestone 2, first cut): the keyboard moves a grey
    /// box with the core's <see cref="BusController"/>, the same model the sandbox uses: power-limited
    /// engine, air brakes that lag and fade with wear, steering that goes heavy with speed, load mass.
    /// No wheel physics yet; the bus is moved kinematically and the road is only a drawing under it.
    /// Vehicle Physics Pro (or Unity's wheel colliders) replaces the integration later, reading the
    /// same BusSettings numbers.
    ///
    /// Keys: W/up throttle, S/down brake, A/D or left/right steer, space full brake, X reverse at a
    /// walk, R back to the stand, [ and ] ten riders off and on (to feel the mass).
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

        /// <summary>
        /// Scripted hands on the pedals, for drive tests run from the terminal (`unity command eval`):
        /// while Scripted is true these replace the keyboard. Instance is the bus in the open scene.
        /// </summary>
        public static bool Scripted;
        public static float ScriptThrottle, ScriptBrake, ScriptSteer;
        public static bool ScriptReverse;
        private bool _reverse;
        public static PlayerBusDrive Instance { get; private set; }

        /// <summary>One line of state for a test log.</summary>
        public string Status => $"t={_clock:0.0} s={Agent.S:0.0} lat={Agent.Lateral:0.00} v={Agent.Speed * 3.6f:0.0}km/h air={Bus.AirPressure:0.00} applied={Bus.BrakeApplied:0.00} wear={Bus.BrakeWear:0.00} riders={Bus.Passengers} yaw={Agent.Yaw * Mathf.Rad2Deg:0} latAcc={Mathf.Abs(Agent.Speed * Bus.LastYawRate):0.0}";

        [Tooltip("Width of the pavement beside the carriageway, metres (greybox.py builds 2 m). On it the bus crawls.")]
        public float PavementMetres = 2.5f;
        [Tooltip("Top speed with wheels on the pavement, km/h: a kerb, people, stalls.")]
        public float PavementKmh = 12f;
        [Tooltip("Speed lost climbing the kerb, m/s: the bump.")]
        public float KerbBumpMs = 1.5f;
        private float _carriagewayHalf = 4f;
        private bool _onPavement;

        public BusController Bus { get; private set; }
        public Agent Agent { get; private set; }
        public Corridor Corridor { get; private set; }

        private float _steerInput;

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
            _box = GetComponent<BoxCollider>();
            if (_box == null) _box = gameObject.AddComponent<BoxCollider>();
            _box.isTrigger = true;
            _box.center = Vector3.zero;
            _box.size = Vector3.one;                       // the cube is scaled to the bus, so the collider is too
            PlaceAtStart();
        }

        /// <summary>The route file is {"xz":[x0,z0,x1,z1,...],"width":w}; a fallback straight road if it is missing.</summary>
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
                    return new Corridor(pts, _carriagewayHalf * 2f + 2f * PavementMetres, false, "Rokeya Sarani");
                }
            }
            Debug.LogWarning("PlayerBusDrive: no route.json wired, driving a straight 3 km road");
            return new Corridor(new[] { Vector3.zero, new Vector3(0f, 0f, -3000f) }, 24f, false, "straight");
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
            Apply();
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null && !Scripted) return;
            if (k == null) k = InputSystem.AddDevice<Keyboard>();     // a headless test still needs the object
            float dt = Time.deltaTime;

            float throttle = (k.wKey.isPressed || k.upArrowKey.isPressed) ? 1f : 0f;
            float brake = (k.sKey.isPressed || k.downArrowKey.isPressed) ? 0.6f : 0f;   // a normal stop; space is the panic
            if (k.spaceKey.isPressed) brake = 1f;
            float steerWanted = (k.aKey.isPressed || k.leftArrowKey.isPressed ? -1f : 0f) + (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f);
            bool reverse = k.xKey.isPressed;
            if (Scripted) { throttle = ScriptThrottle; brake = ScriptBrake; steerWanted = ScriptSteer; reverse = ScriptReverse; }
            _reverse = reverse;

            if (k.rKey.wasPressedThisFrame) { PlaceAtStart(); return; }
            if (k.rightBracketKey.wasPressedThisFrame) Bus.Passengers = Mathf.Min(Tuning.Bus.CrushCapacity, Bus.Passengers + 10);
            if (k.leftBracketKey.wasPressedThisFrame) Bus.Passengers = Mathf.Max(0, Bus.Passengers - 10);

            Tick(dt, throttle, brake, steerWanted);
        }

        /// <summary>One frame of driving: the pedals go to the model, the model moves the bus, walls are checked.</summary>
        public void Tick(float dt, float throttle, float brake, float steerWanted)
        {
            // The input ramps so a tap is a nudge and a hold is full lock; the wheel's own speed is in BusSettings.
            _steerInput = Mathf.MoveTowards(_steerInput, steerWanted, SteerInputRate * dt * (steerWanted == 0f ? 2f : 1f));
            Bus.Throttle = throttle;
            Bus.Brake = brake;
            Bus.Steer = _steerInput;
            // Fixed sub-steps keep the model stable whatever the frame rate; the sandbox runs it at 60 Hz too.
            const float step = 1f / 60f;
            float left = Mathf.Min(dt, 0.1f);
            while (left > 0f)
            {
                float h = Mathf.Min(step, left);
                if (_reverse && Agent.Speed < 0.5f) Reverse(h); else Bus.Step(Agent, Corridor, Tuning.Bus, h);
                _clock += h;
                left -= h;
            }
            Kerb();
            StopAtWalls();
            Apply();
        }

        /// <summary>
        /// The kerb: wheels on the pavement cost a bump going up and hold the bus to a crawl while there.
        /// The model's own off-road drag (the stalls) begins past the pavement, where the corridor ends.
        /// </summary>
        private void Kerb()
        {
            bool onPavement = Mathf.Abs(Agent.Lateral) > _carriagewayHalf;
            if (onPavement && !_onPavement) Agent.Speed = Mathf.Max(0f, Agent.Speed - KerbBumpMs);
            if (onPavement) Agent.Speed = Mathf.Min(Agent.Speed, PavementKmh / 3.6f);
            _onPavement = onPavement;
        }

        /// <summary>
        /// Reverse gear, the simple way: the core model only goes forward, so backing up is a walking
        /// pace along the bus's own axis with the front wheels steering it, enough to get off a wall.
        /// </summary>
        private void Reverse(float dt)
        {
            const float pace = 1.4f;                                   // m/s, a helper walking beside
            Agent.Speed = 0f;
            // The wheel: the core model turns the front wheels only in its own forward step, so turn
            // them here the same way (rate-limited, full rate at a crawl) or reverse goes straight.
            BusSettings b = Tuning.Bus;
            float rate = b.SteerRateDegPerSec * Mathf.Deg2Rad;
            float wanted = Mathf.Clamp(Bus.Steer, -1f, 1f) * b.MaxSteerAngleDeg * Mathf.Deg2Rad;
            Bus.SteerAngle = Mathf.MoveTowards(Bus.SteerAngle, wanted, rate * dt);
            float yawRate = -pace / Mathf.Max(0.5f, b.WheelbaseMetres) * Mathf.Tan(Bus.SteerAngle);
            Agent.Yaw += yawRate * dt;
            Vector3 forward = new Vector3(Mathf.Sin(Agent.Yaw), 0f, Mathf.Cos(Agent.Yaw));
            Agent.Position -= forward * (pace * dt);
            Corridor.Project(Agent.Position, out Agent.S, out Agent.Lateral);
        }

        /// <summary>
        /// Drive for a while from the terminal with the scripted pedals, at 60 Hz, without waiting for the
        /// editor's own frames (which stop when its window is not in front). Returns the status after.
        /// </summary>
        public string Simulate(float seconds, float throttle, float brake, float steer, bool reverse = false)
        {
            _reverse = reverse;
            int frames = Mathf.RoundToInt(seconds * 60f);
            for (int i = 0; i < frames; i++) Tick(1f / 60f, throttle, brake, steer);
            return Status;
        }
        private float _clock;

        /// <summary>
        /// No physics yet, so walls are a rule: if the box the bus occupies after this frame's move
        /// overlaps a collider (a block, a pier, the median), the move is undone and the bus stops dead.
        /// A crash is not modelled, only prevented from being a ghost. The cast starts 0.3 m up, so the
        /// road, the paint and the 0.15 m kerbs are not walls; the 0.6 m median is.
        /// </summary>
        private void StopAtWalls()
        {
            Vector3 half = new Vector3(Agent.Shape.Width * 0.5f - 0.05f, Agent.Shape.Height * 0.5f - 0.3f, Agent.Shape.Length * 0.5f - 0.05f);
            Vector3 centre = Agent.Position + Vector3.up * (Agent.Shape.Height * 0.5f + 0.3f);
            Quaternion rot = Quaternion.Euler(0f, Agent.Yaw * Mathf.Rad2Deg, 0f);
            Collider[] hits = Physics.OverlapBox(centre, half, rot, ~0, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return;
            // Pushed out of whatever was hit, by the shortest way, and stopped. Pushing out instead of
            // undoing the move means the bus can be steered away again afterwards; a bus glued to the
            // median by its own collision rule was the first drive test's finding.
            Vector3 push = Vector3.zero;
            foreach (Collider other in hits)
            {
                if (Physics.ComputePenetration(_box, centre, rot, other, other.transform.position, other.transform.rotation, out Vector3 dir, out float dist))
                    push += dir * dist;
            }
            push.y = 0f;
            Agent.Position += push + push.normalized * 0.05f;
            Agent.Speed = 0f;
            Corridor.Project(Agent.Position, out Agent.S, out Agent.Lateral);
        }
        private BoxCollider _box;    // the bus's own shape for the penetration query; a trigger, so it is never a wall itself

        /// <summary>World pose from the model: the box sits on the road with its floor at y = 0.</summary>
        private void Apply()
        {
            transform.position = Agent.Position + Vector3.up * (Agent.Shape.Height * 0.5f);
            transform.rotation = Quaternion.Euler(0f, Agent.Yaw * Mathf.Rad2Deg, 0f);
        }

        /// <summary>A plain readout until there is a dashboard: what the model is doing this frame.</summary>
        private void OnGUI()
        {
            if (Bus == null) return;
            float kmh = Agent.Speed * 3.6f;
            float lateralG = Mathf.Abs(Agent.Speed * Bus.LastYawRate);
            string text =
                $"{kmh,5:0} km/h   air {Bus.AirPressure * 100f,3:0} %   brakes {Bus.BrakeApplied * 100f,3:0} %  (wear {Bus.BrakeWear * 100f:0} %)\n" +
                $"{Bus.Passengers} riders, {Bus.MassKg(Tuning.Bus) / 1000f:0.0} t   lateral {lateralG:0.0} m/s² (tips at {Tuning.Bus.RolloverLateralAccelMs2:0.0})\n" +
                $"{Agent.S:0} m along, {Agent.Lateral:+0.0;-0.0} m across (kerb at {-_carriagewayHalf:0}){(_onPavement ? "  ON THE PAVEMENT" : "")}\n" +
                "W/S drive, A/D steer, space full brake, X reverse, [ ] riders, R reset";
            GUI.Label(new Rect(16f, 12f, 900f, 120f), text, new GUIStyle(GUI.skin.label) { fontSize = 18, richText = false });
        }

        [System.Serializable] private class RouteFile { public float[] xz; public float width; public float length; }
    }
}
