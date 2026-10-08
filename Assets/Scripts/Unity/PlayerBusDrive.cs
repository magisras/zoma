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
    /// Keys: W/up throttle, S/down brake, A/D or left/right steer, space full brake, R back to the
    /// stand, [ and ] ten riders off and on (to feel the mass).
    /// </summary>
    public sealed class PlayerBusDrive : MonoBehaviour
    {
        [Tooltip("Every number the bus model uses lives here (Assets/Data/TuningTable.asset).")]
        public TuningTable Tuning;
        [Tooltip("Assets/World/Corridor01/route.json from tools/osm/greybox.py: the main road as a polyline.")]
        public TextAsset RouteJson;
        [Tooltip("Metres along the route to start at; the stand is at 0.")]
        public float StartAlong = 30f;
        [Tooltip("Metres across the road to start at; negative is the left (kerb) side. Bangladesh drives on the left.")]
        public float StartLateral = -5f;
        [Tooltip("How fast the steering input ramps, per second; the wheel itself is rate-limited in BusSettings.")]
        public float SteerInputRate = 3f;

        public BusController Bus { get; private set; }
        public Agent Agent { get; private set; }
        public Corridor Corridor { get; private set; }

        private float _steerInput;

        private void Awake()
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
            PlaceAtStart();
        }

        /// <summary>The route file is {"xz":[x0,z0,x1,z1,...],"width":w}; a fallback straight road if it is missing.</summary>
        private static Corridor LoadRoute(TextAsset json)
        {
            if (json != null)
            {
                RouteFile r = JsonUtility.FromJson<RouteFile>(json.text);
                if (r != null && r.xz != null && r.xz.Length >= 4)
                {
                    var pts = new Vector3[r.xz.Length / 2];
                    for (int i = 0; i < pts.Length; i++) pts[i] = new Vector3(r.xz[2 * i], 0f, r.xz[2 * i + 1]);
                    return new Corridor(pts, r.width > 0f ? r.width : 24f, false, "Rokeya Sarani");
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
            Corridor.Project(Agent.Position, out Agent.S, out Agent.Lateral);
            Apply();
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null) return;
            float dt = Time.deltaTime;

            float throttle = (k.wKey.isPressed || k.upArrowKey.isPressed) ? 1f : 0f;
            float brake = (k.sKey.isPressed || k.downArrowKey.isPressed) ? 0.6f : 0f;   // a normal stop; space is the panic
            if (k.spaceKey.isPressed) brake = 1f;
            float steerWanted = (k.aKey.isPressed || k.leftArrowKey.isPressed ? -1f : 0f) + (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f);
            // The input ramps so a tap is a nudge and a hold is full lock; the wheel's own speed is in BusSettings.
            _steerInput = Mathf.MoveTowards(_steerInput, steerWanted, SteerInputRate * dt * (steerWanted == 0f ? 2f : 1f));

            if (k.rKey.wasPressedThisFrame) { PlaceAtStart(); return; }
            if (k.rightBracketKey.wasPressedThisFrame) Bus.Passengers = Mathf.Min(Tuning.Bus.CrushCapacity, Bus.Passengers + 10);
            if (k.leftBracketKey.wasPressedThisFrame) Bus.Passengers = Mathf.Max(0, Bus.Passengers - 10);

            Bus.Throttle = throttle;
            Bus.Brake = brake;
            Bus.Steer = _steerInput;
            // Fixed sub-steps keep the model stable whatever the frame rate; the sandbox runs it at 60 Hz too.
            const float step = 1f / 60f;
            float left = Mathf.Min(dt, 0.1f);
            while (left > 0f)
            {
                float h = Mathf.Min(step, left);
                Bus.Step(Agent, Corridor, Tuning.Bus, h);
                left -= h;
            }
            Apply();
        }

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
                $"{Agent.S:0} m along, {Agent.Lateral:+0.0;-0.0} m across (kerb at {-Corridor.HalfWidth:0})\n" +
                "W/S drive, A/D steer, space full brake, [ ] riders, R reset";
            GUI.Label(new Rect(16f, 12f, 900f, 120f), text, new GUIStyle(GUI.skin.label) { fontSize = 18, richText = false });
        }

        [System.Serializable] private class RouteFile { public float[] xz; public float width; public float length; }
    }
}
