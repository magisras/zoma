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
    /// walk, H horn, P pay / N refuse the sergeant or the men with the ropes, R a new day at the stand,
    /// [ and ] ten riders off and on (to feel the mass), C the camera, T the tow.
    ///
    /// The road is the whole round trip as one loop: out to Azimpur on route.json, back to Mirpur 12
    /// on route_back.json (the other carriageway where the road is dual), joined at the stands. The
    /// leg is where you are on the loop; turning the bus at a stand and driving on is the turnaround,
    /// and passing the Mirpur 12 stand again is a trip (docs/ROUTE_AND_TRIPS.md: three or four a day).
    /// With a <see cref="StreetSim"/> on the same object the street is alive: crowds, rivals, traffic,
    /// people crossing, the sergeant, the ledger.
    /// </summary>
    public sealed class PlayerBusDrive : MonoBehaviour
    {
        [Tooltip("Every number the bus model uses lives here (Assets/Data/TuningTable.asset).")]
        public TuningTable Tuning;
        [Tooltip("Assets/World/Corridor01/route.json from tools/osm/greybox.py: the main road out, as a polyline.")]
        public TextAsset RouteJson;
        [Tooltip("Assets/World/Corridor01/route_back.json: the way back from Azimpur, on the other carriageway. Without it the road is one leg and open.")]
        public TextAsset RouteBackJson;
        [Tooltip("Assets/World/Corridor01/stops.json: the stops of both legs, for the readout and the sim's demand zones.")]
        public TextAsset StopsJson;
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
        public static bool ScriptReverse, ScriptHorn;
        public static PlayerBusDrive Instance { get; private set; }

        public BusController Bus { get; private set; }
        public Agent Agent { get; private set; }
        public Corridor Corridor { get; private set; }
        public PhysicsBus Physics { get; private set; }
        public StreetSim Street { get; private set; }
        public RouteStops Stops { get; private set; }
        /// <summary>Length of the way out: the loop's S where the way back begins.</summary>
        public float OutLength { get; private set; }
        /// <summary>0: out, Mirpur 12 to Azimpur. 1: back. Read off the loop.</summary>
        public int Leg => Corridor.Closed && Agent.S >= OutLength ? 1 : 0;
        /// <summary>The S of the stand at the end of the current leg.</summary>
        public float LegEndS => Leg == 0 ? OutLength : Corridor.Length;
        /// <summary>Round trips completed: back past the Mirpur 12 stand. The ledger's count when the street is alive.</summary>
        public int Trips => Street != null ? Street.Sim.Economy.Ledger.Trips : _trips;
        public string LegName => Leg == 0 ? "to Azimpur" : "back to Mirpur 12";

        /// <summary>One line of state for a test log.</summary>
        public string Status => $"t={_clock:0.0} leg={Leg} trips={Trips} s={Agent.S:0.0} lat={Agent.Lateral:0.00} v={(Physics != null ? Physics.ForwardSpeed : Agent.Speed) * 3.6f:0.0}km/h air={Bus.AirPressure:0.00} applied={Bus.BrakeApplied:0.00} wear={Bus.BrakeWear:0.00} riders={Bus.Passengers} yaw={Agent.Yaw * Mathf.Rad2Deg:0} latAcc={LateralAccel:0.0} roll={Roll:0.0}";

        private float _steerInput, _throttle, _brake, _steerWanted, _clock;
        private bool _reverse, _horn, _onPavement, _offRoad;
        private float _carriagewayHalf = 4f;
        private float[] _farS, _farEdge;      // distance along the loop -> lateral of the far kerb (right side)
        private BoxCollider _ghostBox;        // kinematic bus only: its shape for the wall push-out
        private int _trips, _lastLeg, _lastRiders = -1;
        private float _lastS;
        private string _standNote = ""; private float _standNoteAt = -99f;

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
            Corridor = LoadLoop();
            Stops = RouteStops.Load(StopsJson);
            Physics = GetComponent<PhysicsBus>();
            if (Physics != null)
            {
                Physics.Build(Tuning.Bus, VehicleShape.For(VehicleClass.Bus));
            }
            else
            {
                _ghostBox = GetComponent<BoxCollider>();
                if (_ghostBox == null) _ghostBox = gameObject.AddComponent<BoxCollider>();
                _ghostBox.isTrigger = true;
                _ghostBox.center = Vector3.zero;
                _ghostBox.size = Vector3.one;                   // the cube is scaled to the bus, so the collider is too
            }
            NewDay();
        }

        /// <summary>
        /// The day from the stand: with a StreetSim, a fresh sim on the loop owns the player's Agent
        /// and the BusController (so its tired hands, its wear and its ledger are the bus's); without
        /// one, a plain Agent and a plain model. Then the bus is placed at the start.
        /// </summary>
        public void NewDay()
        {
            Street = GetComponent<StreetSim>();
            if (Street != null && Street.enabled && Physics != null && Application.isPlaying)
            {
                Street.Build(this, Corridor, Stops, OutLength);
                Agent = Street.Sim.Player;
                Bus = Street.Sim.Bus;
            }
            else
            {
                Street = null;
                Agent = new Agent { Corridor = Corridor, Class = VehicleClass.Bus, Shape = VehicleShape.For(VehicleClass.Bus), IsPlayer = true };
                Bus = new BusController
                {
                    BrakeWear = Tuning.Bus.StartingBrakeWear,
                    AirPressure = Tuning.Bus.AirPressureAtDayStart,
                    Passengers = Tuning.Bus.StartingPassengers,
                };
            }
            _trips = 0;
            _lastRiders = -1;
            if (Physics != null) Physics.SetLoad(Bus.Passengers);
            PlaceAtStart();
        }

        /// <summary>
        /// The loop: route.json's points, then route_back.json's, closed. The far kerb of the oncoming
        /// carriageway per point comes along, keyed by distance along the loop. The corridor's width is
        /// the carriageway's; the pavements are PavementMetres beyond it on each side. Without the way
        /// back the road is one open leg; without any file a straight 3 km road.
        /// </summary>
        private Corridor LoadLoop()
        {
            RouteFile a = Read(RouteJson), b = Read(RouteBackJson);
            if (a == null)
            {
                Debug.LogWarning("PlayerBusDrive: no route.json wired, driving a straight 3 km road");
                OutLength = 3000f;
                _farS = null;
                return new Corridor(new[] { Vector3.zero, new Vector3(0f, 0f, -3000f) }, 8f, false, "straight");
            }
            _carriagewayHalf = (a.width > 0f ? a.width : 8f) * 0.5f;
            var pts = new System.Collections.Generic.List<Vector3>();
            var far = new System.Collections.Generic.List<float>();
            Append(pts, far, a);
            // The out leg's length on the loop is the arc length of its own points, plus the join at Azimpur.
            OutLength = 0f;
            for (int i = 1; i < pts.Count; i++) OutLength += Vector3.Distance(pts[i - 1], pts[i]);
            if (b != null)
            {
                OutLength += Vector3.Distance(pts[pts.Count - 1], new Vector3(b.xz[0], 0f, b.xz[1]));
                Append(pts, far, b);
            }
            var corridor = new Corridor(pts, _carriagewayHalf * 2f, b != null, b != null ? "Mirpur 12 to Azimpur and back" : "Rokeya Sarani");
            _farS = new float[pts.Count];
            _farEdge = far.ToArray();
            for (int i = 1; i < pts.Count; i++) _farS[i] = _farS[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            return corridor;
        }

        private static RouteFile Read(TextAsset json)
        {
            if (json == null) return null;
            RouteFile r = JsonUtility.FromJson<RouteFile>(json.text);
            return r != null && r.xz != null && r.xz.Length >= 4 ? r : null;
        }

        private void Append(System.Collections.Generic.List<Vector3> pts, System.Collections.Generic.List<float> far, RouteFile r)
        {
            int n = r.xz.Length / 2;
            bool hasFar = r.farEdge != null && r.farEdge.Length == n;
            for (int i = 0; i < n; i++)
            {
                pts.Add(new Vector3(r.xz[2 * i], 0f, r.xz[2 * i + 1]));
                far.Add(hasFar ? r.farEdge[i] : _carriagewayHalf + PavementMetres);
            }
        }

        /// <summary>How far to the right of the route line the oncoming carriageway's far kerb is, here.</summary>
        private float FarEdgeAt(float s)
        {
            if (_farS == null) return _carriagewayHalf + PavementMetres;
            int i = 1;
            while (i < _farS.Length - 1 && _farS[i] < s) i++;
            float t = Mathf.InverseLerp(_farS[i - 1], _farS[i], s);
            return Mathf.Lerp(_farEdge[i - 1], _farEdge[i], t);
        }

        /// <summary>The outer edge of the left pavement: beyond it is dirt.</summary>
        private float LeftEdge => -(_carriagewayHalf + PavementMetres);

        /// <summary>The stand at Mirpur 12, the start of the way out.</summary>
        private void PlaceAtStart() { PlaceAt(StartAlong); }

        /// <summary>At the start of a leg, in the kerb lane, facing along it. Drive tests use it for the way back.</summary>
        public void StartLeg(int leg) { PlaceAt(leg == 1 && Corridor.Closed ? OutLength + StartAlong : StartAlong); }

        private void PlaceAt(float s)
        {
            Agent.Position = Corridor.PositionAt(s, StartLateral);
            Vector3 ahead = Corridor.PositionAt(s + 5f, StartLateral) - Agent.Position;
            Agent.Yaw = Mathf.Atan2(ahead.x, ahead.z);
            Agent.Speed = 0f;
            Bus.SteerAngle = 0f;
            Bus.Throttle = Bus.Brake = Bus.Steer = 0f;
            _steerInput = 0f;
            _clock = 0f;
            _lastLeg = Corridor.Closed && s >= OutLength ? 1 : 0;
            ProjectPlayer();
            Rolled = false; _rolledAt = -1f; _lastLateral = Agent.Lateral; _wasOnPavement = false;
            _lastS = Agent.S;
            if (Physics != null) Physics.Teleport(Agent.Position, Agent.Yaw);
            else Apply();
        }

        /// <summary>
        /// The tow: a bus beached on the median or wedged in a lane gets pushed back onto the road by
        /// the people around it, or dragged by a truck. Here, for now, it is put on the kerb lane at the
        /// same point of the route, standing, facing along it. The ledger will charge for it later.
        /// </summary>
        public void Tow()
        {
            float s = Agent.S;
            Agent.Position = Corridor.PositionAt(s, StartLateral);
            Vector3 ahead = Corridor.PositionAt(s + 5f, StartLateral) - Agent.Position;
            Agent.Yaw = Mathf.Atan2(ahead.x, ahead.z);
            Agent.Speed = 0f;
            Bus.SteerAngle = 0f;
            Rolled = false; _rolledAt = -1f;
            ProjectPlayer();
            _lastLateral = Agent.Lateral; _wasOnPavement = false;
            if (Physics != null) Physics.Teleport(Agent.Position, Agent.Yaw);
            else Apply();
        }

        /// <summary>
        /// Where the bus is on the loop. The two legs run a few metres apart at the stands and wherever
        /// the road is dual, so the nearest line is not the answer: the bus stays on its leg unless the
        /// other leg is closer and the bus is heading its way (the U-turn at a stand). Crossing to the
        /// oncoming carriageway on the way out is still the way out, on the wrong side.
        /// </summary>
        private void ProjectPlayer()
        {
            if (!Corridor.Closed) { Corridor.Project(Agent.Position, out Agent.S, out Agent.Lateral); return; }
            int leg = _lastLeg;
            float a0 = leg == 0 ? 0f : OutLength, a1 = leg == 0 ? OutLength : Corridor.Length;
            float b0 = leg == 0 ? OutLength : 0f, b1 = leg == 0 ? Corridor.Length : OutLength;
            Corridor.Project(Agent.Position, a0, a1, out float sSame, out float latSame, out float dSame);
            Corridor.Project(Agent.Position, b0, b1, out float sOther, out float latOther, out float dOther);
            Vector3 forward = new Vector3(Mathf.Sin(Agent.Yaw), 0f, Mathf.Cos(Agent.Yaw));
            bool headingOther = Vector3.Dot(forward, Corridor.TangentAt(sOther)) > 0.3f;
            if (dOther < dSame - 1f && headingOther) { Agent.S = sOther; Agent.Lateral = latOther; }
            else { Agent.S = sSame; Agent.Lateral = latSame; }
        }

        /// <summary>The leg changed: the readout says so for a while. A lap past the stand is a trip.</summary>
        private void WatchLegs()
        {
            if (Corridor.Closed && Agent.S < _lastS - Corridor.Length * 0.5f) _trips++;
            _lastS = Agent.S;
            int leg = Leg;
            if (leg != _lastLeg)
            {
                _lastLeg = leg;
                _standNote = leg == 1 ? "The Azimpur stand. Turn the bus around: back to Mirpur 12." : $"The Mirpur 12 stand: trip {Trips} done. Out again.";
                _standNoteAt = _clock;
            }
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
            bool reverse = k.xKey.isPressed, horn = k.hKey.isPressed;
            if (Scripted) { throttle = ScriptThrottle; brake = ScriptBrake; steerWanted = ScriptSteer; reverse = ScriptReverse; horn = ScriptHorn; }

            if (k.rKey.wasPressedThisFrame) { NewDay(); return; }
            if (k.tKey.wasPressedThisFrame) { Tow(); return; }
            if (k.pKey.wasPressedThisFrame && Street != null) Street.Answer(true);
            if (k.nKey.wasPressedThisFrame && Street != null) Street.Answer(false);
            if (k.rightBracketKey.wasPressedThisFrame) SetRiders(Bus.Passengers + 10);
            if (k.leftBracketKey.wasPressedThisFrame) SetRiders(Bus.Passengers - 10);

            _throttle = throttle; _brake = brake; _steerWanted = steerWanted; _reverse = reverse; _horn = horn;
            if (Physics == null) Tick(Time.deltaTime, throttle, brake, steerWanted);   // the kinematic bus moves per frame
            else Physics.UpdateWheelMeshes();
        }

        private void FixedUpdate()
        {
            if (Physics != null && !Scripted) PhysicsStep(Time.fixedDeltaTime);
        }

        public void SetRiders(int riders)
        {
            riders = Mathf.Clamp(riders, 0, Tuning.Bus.CrushCapacity);
            if (Street != null) Street.Sim.SetPassengerCount(Agent, riders);
            Bus.Passengers = riders;
            if (Physics != null) Physics.SetLoad(Bus.Passengers);
        }

        // ---------------------------------------------------------------- the physics bus

        /// <summary>One physics step: the body to the Agent, the street around it, the pedals to the model, the model to the wheels.</summary>
        private void PhysicsStep(float dt)
        {
            _steerInput = Mathf.MoveTowards(_steerInput, _steerWanted, SteerInputRate * dt * (_steerWanted == 0f ? 2f : 1f));
            float forward = Physics.ForwardSpeed;
            SyncAgentFromBody();
            float lateralVel = (Agent.Lateral - _lastLateral) / dt;

            float cap = float.MaxValue;
            if (Street != null)
            {
                // Through the sim: the hands reach the pedals late when tired, the world moves, a contact or a
                // sergeant's hand comes back as a speed cap for the body.
                Street.Step(dt, _throttle, _brake, _steerInput, _horn, lateralVel, out cap);
                if (Bus.Held) { Bus.Throttle = 0f; Bus.Brake = 1f; }
                if (Bus.Passengers != _lastRiders) { Physics.SetLoad(Bus.Passengers); _lastRiders = Bus.Passengers; }
            }
            else
            {
                Bus.Throttle = _throttle;
                Bus.Brake = _brake;
                Bus.Steer = _steerInput;
            }
            // Reverse is a gear: X alone opens the throttle, the body gets a backward torque.
            if (_reverse) Bus.Throttle = Mathf.Max(Bus.Throttle, 1f);
            Physics.Drive(Bus, forward, dt, _reverse);
            if (cap < float.MaxValue) Physics.Cap(cap, forward);

            // The street's rules that are not yet objects: the pavement crawl, the dirt beyond.
            float far = FarEdgeAt(Agent.S);
            _onPavement = Agent.Lateral < -_carriagewayHalf || Agent.Lateral > far - PavementMetres;
            _offRoad = Agent.Lateral < LeftEdge || Agent.Lateral > far;
            if (_offRoad) Physics.Cap(OffRoadKmh / 3.6f, forward);
            else if (_onPavement) Physics.Cap(PavementKmh / 3.6f, forward);

            // The tripped rollover: leaving the road further than the tolerance at speed, the wheels catch
            // (the core's rule, Rollover.Check "left the road at speed"); the body is thrown physically.
            // Two trips: the kerb hit sideways at speed (the outer wheels catch the step), and leaving the
            // road altogether at speed (the ditch, the railing). Both need RolloverSpeedMs, 36 km/h.
            BusSettings b = Tuning.Bus;
            // A 15 cm kerb trips a bus only when it is thrown at it sideways at speed, and a top-heavy
            // (loaded, people standing) bus far sooner than an empty one: the threshold is the full-load
            // figure, up to 60 % higher for an empty bus. Owner, 9 Oct: an empty bus at 40 km/h at 34°
            // went over under the first rule, which no real bus would.
            float load = Mathf.Clamp01(Bus.Passengers / (float)Mathf.Max(1, b.CrushCapacity));
            float tripAt = KerbTripSidewaysMs * (1f + 0.6f * (1f - load));
            bool kerbHit = _onPavement && !_wasOnPavement && Mathf.Abs(lateralVel) > tripAt;
            float beyond = Mathf.Max(LeftEdge - Agent.Lateral, Agent.Lateral - far) - b.OffRoadToleranceMetres;
            if (!Rolled && forward >= b.RolloverSpeedMs && (kerbHit || beyond > b.OffRoadRolloverMetres))
            {
                Rolled = true;
                _rolledAt = _clock;
                Physics.Trip(lateralVel < 0f ? -1f : 1f);
                float angle = Mathf.Atan2(Mathf.Abs(lateralVel), Mathf.Max(0.1f, forward)) * Mathf.Rad2Deg;
                Debug.Log($"TRIP: {(kerbHit ? "kerb" : "off the road")} at {forward * 3.6f:0} km/h, sideways {Mathf.Abs(lateralVel):0.0} m/s ({angle:0}° to the kerb), threshold {tripAt:0.0}, {Bus.Passengers} riders, s={Agent.S:0} lat={Agent.Lateral:0.0}");
                if (Street != null) Street.BodyTipped(kerbHit ? "thrown at the kerb" : "left the road at speed");
            }
            _wasOnPavement = _onPavement;
            _lastLateral = Agent.Lateral;
            if (Rolled && Mathf.Abs(Physics.RollDegrees) < 20f && _clock - _rolledAt > 3f) Rolled = false;   // righted (R) or it rocked back

            WatchLegs();
            _clock += dt;
        }

        /// <summary>On its side. R puts it back on its wheels for now; the rope and the men come later.</summary>
        public bool Rolled { get; private set; }
        private float _rolledAt = -1f, _lastLateral;
        private bool _wasOnPavement;
        [Tooltip("Sideways speed into the kerb that trips a fully loaded bus, m/s, at or above the rollover speed; an empty bus " +
                 "needs 60 % more. 4.5 is 40 km/h at about 24° for a full bus, 40° for an empty one. A shallow drift onto the " +
                 "pavement climbs it; a bus thrown at the kerb sideways goes over. placeholder")]
        public float KerbTripSidewaysMs = 4.5f;

        private void SyncAgentFromBody()
        {
            Transform t = Physics.Body.transform;
            Agent.Position = new Vector3(t.position.x, 0f, t.position.z);
            Vector3 f = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            if (f.sqrMagnitude > 1e-4f) Agent.Yaw = Mathf.Atan2(f.x, f.z);
            Agent.Speed = Mathf.Max(0f, Physics.ForwardSpeed);
            Bus.LastYawRate = Vector3.Dot(Physics.Body.angularVelocity, Vector3.up);
            ProjectPlayer();
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
                if (_reverse && Agent.Speed < 0.5f) Reverse(h); else { Bus.Step(Agent, Corridor, Tuning.Bus, h, FarEdgeAt(Agent.S)); ProjectPlayer(); }
                _clock += h;
                left -= h;
            }
            Kerb();
            StopAtWalls();
            Apply();
            WatchLegs();
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
            ProjectPlayer();
        }

        private void Kerb()
        {
            float far = FarEdgeAt(Agent.S);
            bool onPavement = Agent.Lateral < -_carriagewayHalf || Agent.Lateral > far - PavementMetres;
            if (onPavement && !_onPavement) Agent.Speed = Mathf.Max(0f, Agent.Speed - KerbBumpMs);
            if (onPavement) Agent.Speed = Mathf.Min(Agent.Speed, PavementKmh / 3.6f);
            _onPavement = onPavement;
            _offRoad = Agent.Lateral < LeftEdge || Agent.Lateral > far;
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
            ProjectPlayer();
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
            _horn = ScriptHorn;
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
            string where = Rolled ? "  ON ITS SIDE (T: the men right it)" : _offRoad ? "  OFF THE ROAD" : _onPavement ? "  ON THE PAVEMENT" : "";
            // The leg and the next stop: "Trip 1 to Azimpur, Farmgate in 240 m".
            RouteStops.Stop next = Stops.Next(Leg, Leg == 0 ? Agent.S : Agent.S - OutLength, out float ahead);
            string trip = $"Trip {Trips + 1} {LegName}" + (next != null ? $",  {(next.IsStand ? "the stand" : next.name)} in {Mathf.Max(0f, ahead):0} m" : "") +
                          $"  ({LegEndS - Agent.S:0} m to the stand)";
            if (_clock - _standNoteAt < 10f) trip = _standNote;
            string text =
                $"{kmh,5:0} km/h   air {Bus.AirPressure * 100f,3:0} %   brakes {Bus.BrakeApplied * 100f,3:0} %  (wear {Bus.BrakeWear * 100f:0} %)\n" +
                $"{Bus.Passengers} riders, {Bus.MassKg(Tuning.Bus) / 1000f:0.0} t   lateral {LateralAccel:0.0} m/s²   lean {Roll:+0.0;-0.0}°\n" +
                $"{Agent.S:0} m along, {Agent.Lateral:+0.0;-0.0} m across (kerb at {-_carriagewayHalf:0}){where}\n" +
                trip + "\n" +
                (Street != null ? Street.Readout() : "") +
                "W/S drive, A/D steer, space full brake, X reverse, H horn, P/N pay/refuse, [ ] riders, C camera, T tow, R new day";
            GUI.Label(new Rect(16f, 12f, 1100f, 260f), text, new GUIStyle(GUI.skin.label) { fontSize = 18, richText = false });
        }

        [System.Serializable] private class RouteFile { public float[] xz; public float width; public float length; public float[] farEdge; }
    }
}
