using System;
using UnityEngine;

namespace TwentyTons.Tuning
{
    /// <summary>
    /// The one table that holds every gameplay number.
    ///
    /// RESEARCH.md ("Traffic and character AI (method)" > Tuning) asks for exactly this: one place
    /// for mass, nerve, critical gap, horn strength, officer timing and action weights, so that
    /// playtesting means editing numbers in the Inspector, not hunting through code.
    ///
    /// How to use it:
    ///   1. Right-click in Assets/Data  >  Create  >  Twenty Tons  >  Tuning Table.
    ///   2. The new asset starts with the defaults written below (they are the research figures).
    ///   3. Behaviour code receives a TuningTable reference and reads from it. It never hardcodes
    ///      a number of its own. If you need a new number, add a field here with a Tooltip that
    ///      names the RESEARCH.md line it comes from.
    ///
    /// A ScriptableObject is a plain data asset that lives in the project, not in a scene, so one
    /// table can be shared by every vehicle and tweaked while the game runs in the editor.
    ///
    /// Fields marked "placeholder" are not in RESEARCH.md. They exist because the method needs a
    /// number there; playtesting will set them.
    /// </summary>
    [CreateAssetMenu(fileName = "TuningTable", menuName = "Twenty Tons/Tuning Table")]
    public sealed class TuningTable : ScriptableObject
    {
        [Header("Steering layer")]
        public MassSettings Mass = new MassSettings();
        public NerveSettings Nerve = new NerveSettings();
        public GapSettings Gap = new GapSettings();
        public HornSettings Horn = new HornSettings();
        public OfficerSettings Officer = new OfficerSettings();
        public PedestrianSettings Pedestrians = new PedestrianSettings();

        [Header("The player's bus")]
        public BusSettings Bus = new BusSettings();

        [Header("Traffic population")]
        public SpawnSettings Spawn = new SpawnSettings();

        [Header("Money and the clock")]
        public EconomySettings Economy = new EconomySettings();

        [Header("The driver's body")]
        public FatigueSettings Fatigue = new FatigueSettings();

        [Header("The crew's voices")]
        public VoiceSettings Voice = new VoiceSettings();

        [Header("Decision layer (rival buses)")]
        public UtilitySettings Utility = new UtilitySettings();
        public MemorySettings Memory = new MemorySettings();

        [Header("Passengers")]
        public PassengerSettings Passengers = new PassengerSettings();

        [Header("Performance")]
        public PerformanceSettings Performance = new PerformanceSettings();

        /// <summary>
        /// Unity calls this in the editor whenever a value changes. We use it to keep the table
        /// self-consistent (a minimum can never exceed its maximum) so behaviour code can trust it.
        /// </summary>
        private void OnValidate()
        {
            Gap.Clamp();
            Officer.Clamp();
            Passengers.Clamp();
            Performance.Clamp();
        }
    }

    // ------------------------------------------------------------------------------------------
    // Steering layer: how a vehicle moves.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Yield by mass (RESEARCH.md: "mass (bus 20, truck 15, car 3, CNG 1.5, rickshaw 1,
    /// pedestrian 0.3); smaller yields unless nerve calls the bluff").
    ///
    /// These are not kilograms. They are relative weights that say who pushes whom. A bus really is
    /// about 20 tons, which is where the game's title and this scale come from.
    /// </summary>
    [Serializable]
    public sealed class MassSettings
    {
        [Tooltip("Bus. The player's own vehicle and the top of the hierarchy. RESEARCH: 20")]
        public float Bus = 20f;

        [Tooltip("Truck. Yields only to buses, and not always. RESEARCH: 15")]
        public float Truck = 15f;

        [Tooltip("Private car. Yields to buses and trucks; pushes everything smaller. RESEARCH: 3")]
        public float Car = 3f;

        [Tooltip("CNG auto-rickshaw. RESEARCH: 1.5")]
        public float Cng = 1.5f;

        [Tooltip("Cycle rickshaw. Fills every gap you leave. RESEARCH: 1")]
        public float Rickshaw = 1f;

        [Tooltip("Pedestrian. Lightest, but hitting one is the only real punishment. RESEARCH: 0.3")]
        public float Pedestrian = 0.3f;

        /// <summary>Look up the mass for a class so steering code never switches on the enum itself.</summary>
        public float Of(VehicleClass vehicleClass)
        {
            switch (vehicleClass)
            {
                case VehicleClass.Bus: return Bus;
                case VehicleClass.Truck: return Truck;
                case VehicleClass.Car: return Car;
                case VehicleClass.Cng: return Cng;
                case VehicleClass.Rickshaw: return Rickshaw;
                case VehicleClass.Pedestrian: return Pedestrian;
                default: throw new ArgumentOutOfRangeException(nameof(vehicleClass), vehicleClass, null);
            }
        }
    }

    /// <summary>
    /// Nerve is a 0..1 number per driver. High nerve accepts smaller gaps and refuses to yield to
    /// something heavier ("calls the bluff"). Personality is the numbers; nerve is the first one.
    /// </summary>
    [Serializable]
    public sealed class NerveSettings
    {
        [Tooltip("Nerve given to a freshly spawned generic driver when no personality is set. placeholder")]
        [Range(0f, 1f)] public float DefaultNerve = 0.5f;

        [Tooltip("Spread around DefaultNerve for generic traffic so not every car behaves the same. placeholder")]
        [Range(0f, 0.5f)] public float RandomSpread = 0.2f;

        [Tooltip("Chance per second (at nerve = 1) that a lighter vehicle refuses to yield to a heavier one " +
                 "pressing from behind. At nerve = 0 it never bluffs. RESEARCH: 'smaller yields unless nerve " +
                 "calls the bluff'. placeholder")]
        [Range(0f, 1f)] public float BluffChanceAtFullNerve = 0.35f;

        [Tooltip("Chance per second that a lighter vehicle yields to a heavier one pressing from behind when " +
                 "nobody honks. Kept low on purpose: RESEARCH says 'silence moves nothing'. placeholder")]
        [Range(0f, 1f)] public float YieldChancePerSecondWithoutHorn = 0.15f;

        [Tooltip("How far behind a driver notices a heavier vehicle closing in, metres. placeholder")]
        public float YieldLookBehindMetres = 25f;

        [Tooltip("How long a yield (moving aside) is held before the driver reconsiders, seconds. placeholder")]
        public float YieldSeconds = 3f;

        [Tooltip("How long a bluff (refusing to move) is held before the driver reconsiders, seconds. placeholder")]
        public float BluffSeconds = 4f;

        [Tooltip("A much heavier vehicle coming head-on within this distance makes a driver swerve at once, metres. " +
                 "RESEARCH: wrong side is normal; size is right of way. placeholder")]
        public float HeadOnYieldMetres = 45f;
    }

    /// <summary>
    /// Critical gap: the shortest time gap (seconds) a driver accepts before pulling into traffic.
    /// RESEARCH.md: "Critical gap 0.5–1.5 s scaled by nerve (Western sims use 4–6 s)".
    /// This single pair of numbers is most of what makes the road feel like Dhaka and not Munich.
    /// </summary>
    [Serializable]
    public sealed class GapSettings
    {
        [Tooltip("Headway accepted by a driver with nerve = 1, seconds. RESEARCH: 0.5 s")]
        public float CriticalGapMinSeconds = 0.5f;

        [Tooltip("Headway accepted by a driver with nerve = 0, seconds. RESEARCH: 1.5 s")]
        public float CriticalGapMaxSeconds = 1.5f;

        [Tooltip("Bumper-to-bumper distance kept when stopped, metres. Buses stop centimetres short " +
                 "because damage comes out of the crew's day. placeholder")]
        public float FollowDistanceFloorMetres = 0.5f;

        [Tooltip("How hard a driver corrects toward the allowed speed, per second. Below ~2.5 the approach " +
                 "to a stopped vehicle is under-damped and drivers overshoot into it. placeholder")]
        public float ClosingGain = 3f;

        [Tooltip("How far ahead a driver looks for the next vehicle, metres. placeholder")]
        public float LookAheadMetres = 80f;

        [Tooltip("A driver counts as blocked when the vehicle ahead holds them below this fraction of " +
                 "their desired speed; then they start looking for a gap beside it. placeholder")]
        [Range(0f, 1f)] public float BlockedFraction = 0.6f;

        [Tooltip("Extra free road (metres) a sideways move must offer before a nerve-0 driver takes it. " +
                 "Nerve shrinks this; a nerve-1 driver moves for almost nothing. placeholder")]
        public float SeekGapMinAdvantageMetres = 8f;

        [Tooltip("Sideways step between the positions a driver considers, metres. placeholder")]
        public float LateralStepMetres = 1.0f;

        [Tooltip("How much sideways speed a vehicle gets per m/s of forward speed at cruising speed (a crab angle of " +
                 "~20° at 0.4). A yield is driven, not slid: standing still, nobody moves aside. placeholder")]
        public float CrabRatio = 0.4f;

        [Tooltip("The same at a crawl, where the wheel can go to full lock (~40° at 0.8). placeholder")]
        public float CrabRatioCrawl = 0.8f;

        [Tooltip("A driver starts looking for a freer band when something slower than BlockedFraction of their desired " +
                 "speed is within this many seconds ahead: the move is planned while there is still room, not from a " +
                 "standstill. placeholder")]
        public float SeekGapSecondsAhead = 3f;

        [Tooltip("Share of the vehicle's full braking a driver uses to close on something slower ahead. Caps the " +
                 "headway rule so nobody arrives at a parked truck at full speed and stops in half a second. placeholder")]
        [Range(0.1f, 1f)] public float ComfortableBrakingShare = 0.6f;

        [Tooltip("Steering into another band, a driver rolls on into it at up to this speed (m/s) even though the " +
                 "band ahead is blocked: angling out, as long as there is room to do it. placeholder")]
        public float AngleOutMs = 2.5f;

        /// <summary>Sideways speed per unit of forward speed: full lock at a crawl, a gentle crab at speed.</summary>
        public float CrabRatioAt(float speed) => Mathf.Lerp(CrabRatioCrawl, CrabRatio, Mathf.Clamp01(speed / 8f));

        [Tooltip("Sideways shuffle allowed while standing still, m/s (a rickshaw puller can walk it a little). placeholder")]
        public float LateralCreepMs = 0.05f;

        [Tooltip("How fast sideways speed can build up or die away, m/s². Keeps a swerve a swerve and lets the nose " +
                 "turn into it smoothly instead of snapping. placeholder")]
        public float LateralAccelMs2 = 2.5f;

        [Tooltip("How fast two overlapping vehicles are pushed apart, m/s. Lower means a scrape is seen as a scrape; " +
                 "higher means boxes never overlap but jump. placeholder")]
        public float ContactShoveMs = 2.0f;

        /// <summary>
        /// The headway a given driver accepts. Nerve 0 gives the cautious maximum, nerve 1 the brave
        /// minimum. Linear for now; playtesting may want a curve.
        /// </summary>
        public float CriticalGapSeconds(float nerve)
        {
            return Mathf.Lerp(CriticalGapMaxSeconds, CriticalGapMinSeconds, Mathf.Clamp01(nerve));
        }

        public void Clamp()
        {
            CriticalGapMinSeconds = Mathf.Max(0.05f, CriticalGapMinSeconds);
            CriticalGapMaxSeconds = Mathf.Max(CriticalGapMinSeconds, CriticalGapMaxSeconds);
            FollowDistanceFloorMetres = Mathf.Max(0f, FollowDistanceFloorMetres);
            LookAheadMetres = Mathf.Max(5f, LookAheadMetres);
            LateralStepMetres = Mathf.Max(0.1f, LateralStepMetres);
            LateralAccelMs2 = Mathf.Max(0.1f, LateralAccelMs2);
            ContactShoveMs = Mathf.Max(0.1f, ContactShoveMs);
        }
    }

    /// <summary>
    /// The horn is language. RESEARCH.md: "broadcast in a cone with strength; raises yield
    /// probability of agents inside, weighted by relative mass; silence moves nothing".
    /// Short taps clear a path; a long blast warns the rickshaw ahead.
    /// </summary>
    [Serializable]
    public sealed class HornSettings
    {
        [Tooltip("How far a horn is heard, in metres. placeholder")]
        public float RangeMetres = 40f;

        [Tooltip("Half-angle of the cone in front of the vehicle, in degrees. 45 means a 90° wedge. placeholder")]
        [Range(5f, 90f)] public float ConeHalfAngleDegrees = 45f;

        [Tooltip("Yield-probability boost from one short tap, before mass weighting. 0..1. placeholder")]
        [Range(0f, 1f)] public float TapStrength = 0.25f;

        [Tooltip("Yield-probability boost from a sustained blast, before mass weighting. 0..1. placeholder")]
        [Range(0f, 1f)] public float BlastStrength = 0.6f;

        [Tooltip("Seconds a press must last to count as a blast rather than a tap. placeholder")]
        public float BlastThresholdSeconds = 0.6f;

        [Tooltip("How strongly relative mass scales the effect. 1 = a bus horn moves a rickshaw 20× more " +
                 "than a rickshaw horn moves a bus. RESEARCH: 'weighted by relative mass'. placeholder")]
        [Range(0f, 2f)] public float MassWeighting = 1f;

        [Tooltip("How long a vehicle that gave way to a horn keeps giving way, seconds. placeholder")]
        public float YieldSeconds = 5f;

        [Tooltip("Seconds of silence after which the helper and passengers start asking 'why are you " +
                 "not honking?'. RESEARCH: 'A quiet bus has lost its nerve'. placeholder")]
        public float SilenceComplaintSeconds = 20f;

        /// <summary>
        /// The yield boost one listener receives. Heavier horn vs lighter listener → stronger effect;
        /// a rickshaw honking at a bus achieves next to nothing. Result is clamped to 0..1.
        /// </summary>
        public float YieldBoost(float strength, float hornerMass, float listenerMass)
        {
            // Ratio above 1 means the honker is heavier. Raise it to MassWeighting so the designer
            // can soften (0.5) or sharpen (1.5) how much the hierarchy matters.
            float ratio = Mathf.Pow(hornerMass / Mathf.Max(0.01f, listenerMass), MassWeighting);
            return Mathf.Clamp01(strength * ratio);
        }
    }

    /// <summary>
    /// The police hand is the traffic light. RESEARCH.md: "an officer object opens one direction at
    /// a time with variable timing; a few agents 'leak' across until blocked".
    /// </summary>
    [Serializable]
    public sealed class OfficerSettings
    {
        [Tooltip("Shortest time one direction stays open, seconds. placeholder")]
        public float OpenMinSeconds = 20f;

        [Tooltip("Longest time one direction stays open, seconds. placeholder")]
        public float OpenMaxSeconds = 90f;

        [Tooltip("How many vehicles from a closed direction get across before the opposing flow physically " +
                 "blocks them. RESEARCH: 'a few agents leak'. placeholder")]
        public int LeakersPerCycle = 3;

        [Tooltip("Distance at which drivers read the officer's cane and start reacting, metres. placeholder")]
        public float CaneReadDistanceMetres = 30f;

        [Tooltip("How far before the junction box the stop line sits, metres. placeholder")]
        public float StopLineSetbackMetres = 2f;

        [Tooltip("A driver this close to a stop line when the cane drops may still run it (if leakers remain), metres. placeholder")]
        public float LeakZoneMetres = 14f;

        [Tooltip("Chance that a closing constable ropes the approach, so nobody leaks and the player is held at the line. " +
                 "docs/STREET_CONTROL.md: ropes, cones and bamboo. placeholder")]
        [Range(0f, 1f)] public float RopeChance = 0.3f;

        [Tooltip("Probability per shift that a sergeant appears at a given junction and takes the cash. " +
                 "Lowered by a fit bus with clean papers (Milestone 5). placeholder")]
        [Range(0f, 1f)] public float SergeantStopChance = 0.3f;

        public void Clamp()
        {
            OpenMinSeconds = Mathf.Max(1f, OpenMinSeconds);
            OpenMaxSeconds = Mathf.Max(OpenMinSeconds, OpenMaxSeconds);
            LeakersPerCycle = Mathf.Max(0, LeakersPerCycle);
        }
    }

    /// <summary>
    /// RESEARCH.md: pedestrians "cross on estimated gap; 'hand' confidence makes them step out in
    /// front of small vehicles and hesitate for buses". Hitting one is the game's one hard rule.
    /// </summary>
    [Serializable]
    public sealed class PedestrianSettings
    {
        [Tooltip("Vehicles with mass at or below this are stepped in front of with one hand raised. " +
                 "Default = car. placeholder")]
        public float StepOutBelowMass = 3f;

        [Tooltip("Vehicles with mass at or above this make pedestrians hesitate. Default = truck. placeholder")]
        public float HesitateAboveMass = 15f;

        [Tooltip("Extra seconds of gap a pedestrian wants before crossing in front of a heavy vehicle. placeholder")]
        public float HesitationSeconds = 1.0f;

        [Tooltip("Base crossing gap a pedestrian accepts in front of a vehicle, seconds. placeholder")]
        public float CrossingGapSeconds = 1.5f;

        [Tooltip("The raised hand: the crossing gap is multiplied by this in front of small vehicles, " +
                 "because the pedestrian expects them to stop. RESEARCH: 'hand confidence'. placeholder")]
        [Range(0.1f, 1f)] public float HandConfidenceGapFactor = 0.6f;

        [Tooltip("How far up the road a pedestrian looks for traffic before stepping out, metres. placeholder")]
        public float LookMetres = 60f;

        [Tooltip("A vehicle standing still within this distance is treated as about to pull away, metres. placeholder")]
        public float PullAwayWatchMetres = 15f;

        [Tooltip("...at this assumed speed, m/s. placeholder")]
        public float PullAwayAssumedSpeed = 2f;

        [Tooltip("People waiting at the kerb step back this far when a moving vehicle is about to overhang it (a bus " +
                 "swinging wide on a bend), metres. Nobody waits with a shoulder at the road edge. placeholder")]
        public float FlinchBackMetres = 0.8f;

        [Tooltip("How close to the kerb line a vehicle's side may come before people step back, metres. placeholder")]
        public float FlinchReachMetres = 0.6f;

        [Tooltip("Seconds people stay back after the last threat passed. placeholder")]
        public float FlinchSeconds = 2f;

        [Tooltip("A driver keeps this much road between the nose and a person crossing ahead, metres: nobody closes " +
                 "on a pedestrian the way they close on a car. placeholder")]
        public float ClearanceAheadMetres = 2f;

        [Tooltip("A vehicle standing still with its nose this close has stopped for me: I cross in front of it. " +
                 "Without this a pedestrian and a stopped bus wait for each other forever. Must reach past the " +
                 "distance drivers keep from people when standing (6 m in the autopilots), or the standoff returns. placeholder")]
        public float StoppedForMeMetres = 7f;

        [Tooltip("A crosser stuck mid-road this long stops waiting for a proper gap in the light traffic: hand up, step " +
                 "out, the rickshaw brakes. Without it a stream of rickshaws in the next band pins a pedestrian in the " +
                 "middle of the road, and behind the pedestrian a bus, for the rest of the day (seed 2 of the 3 Oct " +
                 "batch stood 500 s). Buses and trucks are still waited for. placeholder")]
        public float MidRoadPatienceSeconds = 4f;

        [Tooltip("Stuck mid-road this long even so, a crosser gives up and goes back to the kerb they came from, to try " +
                 "again later. The backstop: nobody stands in the road all day, whatever the traffic does. placeholder")]
        public float MidRoadGiveUpSeconds = 10f;

        [Tooltip("Walking speed, m/s. placeholder")]
        public float WalkSpeed = 1.3f;

        [Tooltip("Shortest and longest pause on the kerb between crossings, seconds. placeholder")]
        public float WaitMinSeconds = 3f;
        public float WaitMaxSeconds = 12f;
    }

    /// <summary>
    /// The player's bus as a character (RESEARCH.md): "A 20-ton vehicle with worn brakes and heavy
    /// steering." Milestone 2 in Unity will drive these numbers into a real vehicle physics package;
    /// the sandbox uses them in a simple bicycle model (Core/BusController.cs).
    /// </summary>
    [Serializable]
    public sealed class BusSettings
    {
        [Tooltip("Empty weight, tonnes: a Hino AK1J chassis (~5.5 t) with a heavy local steel body. With ninety aboard " +
                 "it is the twenty tons of the title, on a chassis designed for 14.2 t. docs/BUS.md. estimate")]
        public float TareTonnes = 12f;

        [Tooltip("Weight per passenger, kg, with the bag. estimate")]
        public float PassengerKg = 65f;

        [Tooltip("Seats. RESEARCH: fares are set on a 52-seat basis.")]
        public int Seats = 52;

        [Tooltip("Most people the crew will pack in, standing included. RESEARCH: overloading is routine. placeholder")]
        public int CrushCapacity = 90;

        [Tooltip("Engine power, kW. Hino J08C: 210 PS = 155 kW at 2,900 rpm, 554 Nm at 1,500. Acceleration = power / " +
                 "(mass × speed), so a full bus is slow. docs/BUS.md")]
        public float EnginePowerKw = 155f;

        [Tooltip("Acceleration cap at low speed, m/s². Measured sudden starts average 2.0 on asphalt; standing " +
                 "passengers fall above 2.0. A laden chassis gives about 1.8. docs/BUS.md")]
        public float MaxAccelMs2 = 1.8f;

        [Tooltip("Top speed, km/h. The chassis does 90–100; the 2024 city limit for buses is 40. docs/BUS.md. estimate")]
        public float MaxSpeedKmh = 80f;

        [Tooltip("Braking with new brakes and full air, m/s². UN R13 cold test: ~5 for the service brake. docs/BUS.md")]
        public float BrakeDecelNewMs2 = 5f;

        [Tooltip("Air brakes: seconds for the braking force to reach what the pedal asks. Peak deceleration comes " +
                 "~0.35 s after the pedal. docs/BUS.md")]
        public float BrakeLagSeconds = 0.35f;

        [Header("Air (docs/BUS.md: pressure drops with the engine off; pumping the pedal spends it)")]
        [Tooltip("Air pressure at the start of the day, 0..1: the bus was not left idling. Brakes are this fraction of " +
                 "themselves until the compressor catches up. placeholder")]
        [Range(0f, 1f)] public float AirPressureAtDayStart = 0.4f;

        [Tooltip("Seconds of running for the compressor to fill the tanks from empty. placeholder")]
        public float AirBuildSeconds = 45f;

        [Tooltip("Pressure spent by one full application of the pedal. Holding it costs nothing more; pumping it " +
                 "in a jam is what empties the tanks. placeholder")]
        public float AirPerApplication = 0.03f;

        [Tooltip("Fraction of braking lost at brake wear = 1. At 0.6, fully worn brakes keep 40%. placeholder")]
        [Range(0f, 1f)] public float BrakeWearLoss = 0.6f;

        [Tooltip("Rolling resistance plus driveline losses, m/s² lost when coasting. Tyres alone are ~0.1. docs/BUS.md. estimate")]
        public float RollingDecelMs2 = 0.15f;

        [Tooltip("Air drag, m/s² lost per (m/s)²: Cd ~0.7, 8.5 m², 15 t. docs/BUS.md. estimate")]
        public float AirDragPerMs2 = 0.0003f;

        [Tooltip("Distance between axles, metres. Hino AK1J: 5.2–6.0 m. Longer = wider turns. docs/BUS.md")]
        public float WheelbaseMetres = 6f;

        [Tooltip("Full lock at the road wheel, degrees. estimate")]
        public float MaxSteerAngleDeg = 35f;

        [Tooltip("How fast the road wheel turns at a standstill, degrees per second. Five turns lock to lock at one and " +
                 "a half turns a second gives ~20; a driver who anticipates a bend needs less lock, and the bicycle " +
                 "model has no tyre slip to forgive, so 45 stands for both. docs/BUS.md. estimate")]
        public float SteerRateDegPerSec = 45f;

        [Tooltip("Speed (m/s) at which the steering rate halves. Lower = heavier steering. placeholder")]
        public float SteerHeavinessSpeed = 8f;

        [Tooltip("Brake wear the bus starts the prototype with, 0..1. placeholder")]
        [Range(0f, 1f)] public float StartingBrakeWear = 0.5f;

        [Tooltip("Wear added per (brake input × m/s × second) of real driving. 1.5e-4 makes one stop from 36 km/h " +
                 "cost 0.002, so a racing day of 150 stops adds 0.3. The sandbox divides by MoneyScale so its " +
                 "short day wears like a real one. placeholder")]
        public float WearPerMetreSecondBraked = 0.00015f;

        [Tooltip("Wear left after a roadside brake service. placeholder")]
        [Range(0f, 1f)] public float WearAfterService = 0.05f;

        [Tooltip("Above this wear the helper mentions the brakes. placeholder")]
        [Range(0f, 1f)] public float SoftBrakesAbove = 0.7f;

        [Tooltip("Passengers aboard at the start of the prototype. placeholder")]
        public int StartingPassengers = 30;

        [Tooltip("Off the corridor (past the kerb by this many metres) the bus is in the market stalls and " +
                 "loses speed fast. The world enforces, not the UI. placeholder")]
        public float OffRoadToleranceMetres = 2f;
        public float OffRoadDecelMs2 = 4f;

        [Header("The rollover (RESEARCH: the Fraser film)")]
        [Tooltip("Further off the road than this (beyond the tolerance) at speed, the railing catches the wheels, metres. placeholder")]
        public float OffRoadRolloverMetres = 1.5f;
        [Tooltip("Speed at or above which leaving the road tips the bus, m/s. placeholder")]
        public float RolloverSpeedMs = 8f;
        [Tooltip("Lateral acceleration that tips a top-heavy twenty tons, m/s². Real buses go at ~0.4–0.5 g, but the " +
                 "bicycle model has no tyre slip, so 0.6 g here stands for that. placeholder")]
        public float RolloverLateralAccelMs2 = 6f;
    }

    /// <summary>
    /// How generic traffic is kept around the player. The "chaos actors" of the tech plan.
    /// </summary>
    [Serializable]
    public sealed class SpawnSettings
    {
        [Tooltip("Vehicles kept alive around the player. placeholder")]
        public int VehiclesAround = 50;

        [Tooltip("Traffic is spawned up to this far ahead of the player, metres. placeholder")]
        public float SpawnAheadMetres = 250f;

        [Tooltip("...and up to this far behind, metres. placeholder")]
        public float SpawnBehindMetres = 120f;

        [Tooltip("Nothing spawns closer to the player than this, metres. placeholder")]
        public float SpawnClearanceMetres = 30f;

        [Header("Class mix (relative weights). RESEARCH: rickshaws fill every gap; buses ~1 in 4 crashes.")]
        public float RickshawWeight = 40f;
        public float CngWeight = 20f;
        public float CarWeight = 25f;
        public float BusWeight = 10f;
        public float TruckWeight = 5f;

        [Tooltip("Vehicles kept on the oncoming carriageway, as a fraction of VehiclesAround. placeholder")]
        [Range(0f, 2f)] public float OncomingFraction = 0.7f;

        [Tooltip("Width of the strip between the two carriageways, metres. placeholder")]
        public float MedianMetres = 1.5f;

        [Tooltip("Vehicles kept queued or rolling on each cross street. placeholder")]
        public int CrossVehiclesPerJunction = 5;

        [Tooltip("Pedestrians kept on the kerbs around the player. placeholder")]
        public int PedestriansAround = 14;

        [Tooltip("Pedestrians are placed from this far behind to SpawnAheadMetres ahead. placeholder")]
        public float PedestrianBehindMetres = 30f;

        [Tooltip("How far a pedestrian stands from the road edge while waiting, metres. placeholder")]
        public float KerbOffsetMetres = 1.0f;

        [Tooltip("Share of pedestrians placed near a junction or a stand rather than anywhere along the road. " +
                 "docs/STREET_CONTROL.md §6: the road is the crossing, and the crossing is where the people are. placeholder")]
        [Range(0f, 1f)] public float PedestrianClusterShare = 0.6f;

        [Tooltip("How far either side of a junction or stand a clustered pedestrian may stand, metres. placeholder")]
        public float PedestrianClusterMetres = 40f;
    }

    /// <summary>
    /// Money and the clock (RESEARCH.md: pay system, bribes and extortion, police). Real figures where
    /// the research has them; per-day fixed costs are scaled to the sandbox's short day.
    /// </summary>
    [Serializable]
    public sealed class EconomySettings
    {
        [Header("The day")]
        [Tooltip("How long a shift lasts in the sandbox, seconds. A real shift is 12–17 h; this is the prototype's day. placeholder")]
        public float DayLengthSeconds = 900f;

        [Tooltip("Shift hours the sandbox day stands for, for the clock. RESEARCH: 12–14 h routine.")]
        public float ShiftHours = 14f;

        [Tooltip("Clock hour the shift starts at. RESEARCH: Khurshid drives 6 am to 11 pm.")]
        public float ShiftStartHour = 6f;

        [Tooltip("Every Tk amount except fares and fuel (zoma, roadside payments, cases, repairs, food, bed) is " +
                 "multiplied by this, so a short sandbox day stays a fair fight. Fares are per passenger and fuel " +
                 "per km, so they scale with the day on their own. 1 = the real day. placeholder")]
        public float MoneyScale = 0.1f;

        [Header("The owner")]
        [Tooltip("The daily deposit, Tk. RESEARCH: Tk 3,000–5,000 depending on route; city bus ~3,000.")]
        public float ZomaTk = 3000f;

        [Header("Fuel")]
        [Tooltip("Diesel, Tk per litre: 135 since 21 Sep 2026 (was 115). docs/BUS.md")]
        public float DieselTkPerLitre = 135f;

        [Tooltip("City bus fuel economy, km per litre. Operators quote 3–4 for an AK1J in Dhaka traffic. docs/BUS.md. estimate")]
        public float BusKmPerLitre = 3.5f;

        [Header("Roadside payments (RESEARCH: 8–10 points per route; lineman, sergeant, party man)")]
        [Tooltip("The lineman at the stand, per trip, Tk. placeholder")]
        public float LinemanTkPerTrip = 50f;
        [Tooltip("Which zone is the stand (the trip point). 0 = the first zone.")]
        public int LinemanZoneIndex = 0;

        [Tooltip("The party man at his stand, per trip, Tk. placeholder")]
        public float PartyManTkPerTrip = 30f;
        [Tooltip("Which zone the party man works.")]
        public int PartyManZoneIndex = 3;

        [Tooltip("What the sergeant asks for, Tk. Negotiable in the full game. placeholder")]
        public float SergeantDemandTk = 300f;
        [Tooltip("Chance the sergeant steps out after you ran a cane. placeholder")]
        [Range(0f, 1f)] public float SergeantChanceAfterCaneRun = 0.6f;
        [Tooltip("Officer.SergeantStopChance is per shift; per junction pass it is multiplied by this. placeholder")]
        [Range(0f, 1f)] public float SergeantChancePerPassFactor = 0.25f;

        [Header("Police boxes, drive days, the yard, the cameras (docs/STREET_CONTROL.md §3–4)")]
        [Tooltip("Chance a sergeant on duty at a police box stops you as you pass, per pass, before papers and dents. placeholder")]
        [Range(0f, 1f)] public float CheckpointStopChance = 0.15f;

        [Tooltip("Chance a given police box has a sergeant on duty today. placeholder")]
        [Range(0f, 1f)] public float SergeantOnDutyChance = 0.6f;

        [Tooltip("Chance that today is a drive day (special drive, 1,400–2,300 cases across the city). placeholder")]
        [Range(0f, 1f)] public float DriveDayChance = 0.2f;

        [Tooltip("On a drive day stop chances are multiplied by this. placeholder")]
        public float DriveDayFactor = 2.5f;

        [Tooltip("On a drive day the sergeant asks this many times his usual figure: he has a target too. placeholder")]
        public float DriveDayDemandFactor = 1.5f;

        [Tooltip("Refusing a sergeant with no valid papers: chance the bus goes to the dumping yard. placeholder")]
        [Range(0f, 1f)] public float SeizeChanceWithoutPapers = 0.5f;

        [Tooltip("Days the bus sits in the yard. Workers protest months; they ask for ten. placeholder")]
        public int DumpingDays = 3;

        [Tooltip("What a camera case costs the owner, Tk. RESEARCH: ~Tk 2,000 for a red light.")]
        public float CameraFineTk = 2000f;

        [Tooltip("From this day the first junction has a camera (the story hook: cameras appearing along the route). 0 = never. placeholder")]
        public int CameraFromDay = 6;

        [Tooltip("Chance the sergeant steps out after a junction you passed on the wrong side. placeholder")]
        [Range(0f, 1f)] public float WrongSideSergeantChance = 0.5f;

        [Tooltip("A case when you refuse, Tk. RESEARCH: wrong-side driving Tk 3,000 per case.")]
        public float CaseTk = 3000f;
        [Tooltip("How long the paperwork holds the bus after a case, seconds. placeholder")]
        public float CaseDelaySeconds = 120f;

        [Header("Damage and the one hard rule")]
        [Tooltip("Contact slower than this (relative speed, m/s) is paint and mirrors: a dent, no bill. " +
                 "RESEARCH: light contact is common and cosmetic. placeholder")]
        public float CosmeticContactMs = 1.5f;

        [Tooltip("What a harder scrape costs the crew, Tk. placeholder")]
        public float ScrapeRepairTk = 100f;
        [Tooltip("What a passenger hurt at the door costs the crew on the spot, Tk. placeholder")]
        public float InjuryCompensationTk = 2000f;

        [Tooltip("How long the crowd holds the bus after an injury, seconds. placeholder")]
        public float InjuryHoldSeconds = 60f;

        [Tooltip("Injuries in one day before the police end it. RESEARCH: only injuries escalate. placeholder")]
        public int InjuriesBeforeArrest = 2;

        [Tooltip("The case after hitting a person, Tk. RESEARCH: non-bailable; the day's money is gone too. placeholder")]
        public float PersonHitCaseTk = 20000f;

        [Header("Repairs and papers (RESEARCH: maintenance rarely happens; certificates are bought)")]
        [Tooltip("A roadside brake service, Tk. placeholder")]
        public float BrakeServiceTk = 4000f;

        [Tooltip("A fitness certificate without the inspection, Tk. placeholder")]
        public float FitnessTk = 3000f;

        [Tooltip("Days the paper is good for. placeholder")]
        public int FitnessDays = 7;

        [Tooltip("With valid papers the sergeant's 'papers' stops happen this often (× the base chance). " +
                 "Cane runs and wrong side are not helped. RESEARCH: a fit bus with clean papers lowers how often. placeholder")]
        [Range(0f, 1f)] public float PapersSergeantFactor = 0.3f;

        [Tooltip("Each unfixed dent raises the sergeant's 'papers' chance by this factor. placeholder")]
        public float DentSergeantFactor = 0.05f;

        [Header("The rollover")]
        [Tooltip("What the men with the ropes and the tractor ask, Tk. placeholder")]
        public float RopesTk = 5000f;
        [Tooltip("How long it takes to drag the bus back onto its wheels, seconds. placeholder")]
        public float RightingSeconds = 180f;
        [Tooltip("Share of those aboard hurt in the tumble. placeholder")]
        [Range(0f, 1f)] public float TumbleHurtFraction = 0.3f;
        [Tooltip("Dents a rollover adds. placeholder")]
        public int RolloverDentsAdded = 10;
        [Tooltip("Brake wear a rollover adds (something always bends). placeholder")]
        public float RolloverBrakeWear = 0.05f;
        [Tooltip("Days the crew carries its injuries into the next shifts. placeholder")]
        public int CrewInjuryDays = 2;

        [Header("The household")]
        [Tooltip("What the crew spends on food per day, Tk. placeholder")]
        public float FoodTkPerDay = 300f;
        [Tooltip("A bed for the night, Tk; the bus floor is free. placeholder")]
        public float BedTk = 200f;
    }

    /// <summary>
    /// The driver's body (RESEARCH.md, Sleep, shifts and health; Fatigue mechanic). Shown by the
    /// world, never by a bar.
    /// </summary>
    [Serializable]
    public sealed class FatigueSettings
    {
        [Tooltip("Fatigue gained per shift hour. 0.075 reaches 1.0 after about 13 h. RESEARCH: 12–14 h routine. placeholder")]
        public float RisePerShiftHour = 0.075f;

        [Tooltip("Reaction delay at fatigue 1, seconds: inputs reach the wheel this late. placeholder")]
        public float ReactionDelayMaxSeconds = 0.6f;

        [Tooltip("Vision starts to tunnel above this fatigue. placeholder")]
        [Range(0f, 1f)] public float TunnelAbove = 0.5f;

        [Tooltip("Micro-sleeps start above this fatigue. placeholder")]
        [Range(0f, 1f)] public float MicroSleepAbove = 0.7f;

        [Tooltip("Chance per second of a micro-sleep at fatigue 1 (scales down to 0 at the threshold). placeholder")]
        [Range(0f, 1f)] public float MicroSleepChancePerSecond = 0.08f;

        [Tooltip("How long the eyes close, seconds. RESEARCH: 'close the screen for a moment'. placeholder")]
        public float MicroSleepMinSeconds = 0.5f;
        public float MicroSleepMaxSeconds = 1.5f;

        [Header("Recovery")]
        [Tooltip("Fatigue the next shift starts with after a bed. placeholder")]
        [Range(0f, 1f)] public float AfterBed = 0.1f;
        [Tooltip("Lowest fatigue the bus floor can bring you to. RESEARCH: sleeping in the bus recovers less. placeholder")]
        [Range(0f, 1f)] public float AfterBusFloor = 0.35f;
        [Tooltip("Fraction of the day's fatigue the bus floor leaves you with. placeholder")]
        [Range(0f, 1f)] public float BusFloorKeeps = 0.6f;
        [Tooltip("Reaction delay multiplier while the crew is carrying injuries. placeholder")]
        public float InjuryReactionFactor = 1.6f;
        [Tooltip("Fatigue a shift starts with on top of the carried amount while injured. placeholder")]
        public float InjuryFatigueAdded = 0.2f;

        [Tooltip("Fatigue after a day off. RESEARCH: 7–9 h of sleep on the off day cuts violations. placeholder")]
        [Range(0f, 1f)] public float AfterRestDay = 0.05f;
    }

    /// <summary>When the crew speaks (RESEARCH.md: the crew teaches). Cooldowns keep them human.</summary>
    [Serializable]
    public sealed class VoiceSettings
    {
        [Tooltip("The helper calls a bus behind when it is within this distance, metres. placeholder")]
        public float BusBehindCloseMetres = 80f;
        [Tooltip("...and says 'easy' when the bus ahead is further than this, metres. placeholder")]
        public float BusAheadFarMetres = 300f;
        [Tooltip("Seconds between gap calls. placeholder")]
        public float GapCooldownSeconds = 15f;
        [Tooltip("The helper calls a crowd this far before the zone, metres. placeholder")]
        public float CrowdCallMetres = 70f;
        [Tooltip("...if at least this many are waiting. placeholder")]
        public int CrowdCallMinimum = 4;
        [Tooltip("Seconds between 'why aren't you honking'. placeholder")]
        public float SilenceCooldownSeconds = 25f;
        [Tooltip("Seconds between door warnings. placeholder")]
        public float DoorCooldownSeconds = 12f;
        [Tooltip("Below this speed with a clear road, passengers get restless, km/h. placeholder")]
        public float SlowKmh = 12f;
        [Tooltip("...after this many seconds. placeholder")]
        public float SlowSecondsBeforeComplaint = 12f;
        [Tooltip("Seconds between passenger complaints. placeholder")]
        public float PassengerCooldownSeconds = 25f;
    }

    // ------------------------------------------------------------------------------------------
    // Decision layer: what a rival bus wants. Built in Milestone 4; the numbers live here now.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// The five actions a rival bus scores every second (RESEARCH.md, utility AI table).
    /// Kept as an enum so weights can be stored per action in arrays and personalities.
    /// </summary>
    public enum BusAction
    {
        RaceForNextStop = 0,
        SkipTheStop = 1,
        WaitAndFill = 2,
        BlockThePlayer = 3,
        BackOff = 4
    }

    /// <summary>
    /// A named set of per-action multipliers. RESEARCH.md: "reckless: race ×1.5, back off ×0.5;
    /// cautious: reverse; spiteful: block ×2 plus memory of who cut him off".
    /// Personality is the numbers; this is where a named crew member gets his.
    /// </summary>
    [Serializable]
    public sealed class DriverPersonality
    {
        public string Name = "Default";

        [Tooltip("Base nerve of this personality, 0..1.")]
        [Range(0f, 1f)] public float Nerve = 0.5f;

        [Tooltip("Multiplier on 'race for next stop'.")]
        public float Race = 1f;

        [Tooltip("Multiplier on 'skip the stop'.")]
        public float Skip = 1f;

        [Tooltip("Multiplier on 'wait and fill'.")]
        public float Wait = 1f;

        [Tooltip("Multiplier on 'block the player'.")]
        public float Block = 1f;

        [Tooltip("Multiplier on 'back off'.")]
        public float BackOff = 1f;

        public float MultiplierFor(BusAction action)
        {
            switch (action)
            {
                case BusAction.RaceForNextStop: return Race;
                case BusAction.SkipTheStop: return Skip;
                case BusAction.WaitAndFill: return Wait;
                case BusAction.BlockThePlayer: return Block;
                case BusAction.BackOff: return BackOff;
                default: throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        // The three archetypes named in RESEARCH.md, plus a neutral one for generic traffic.
        public static DriverPersonality Default() => new DriverPersonality();

        public static DriverPersonality Reckless() => new DriverPersonality
        {
            Name = "Reckless", Nerve = 0.85f, Race = 1.5f, BackOff = 0.5f
        };

        public static DriverPersonality Cautious() => new DriverPersonality
        {
            Name = "Cautious", Nerve = 0.25f, Race = 0.5f, BackOff = 1.5f
        };

        public static DriverPersonality Spiteful() => new DriverPersonality
        {
            Name = "Spiteful", Nerve = 0.7f, Block = 2f
        };
    }

    /// <summary>
    /// Base weights for each action's score, before a personality multiplies them. The *inputs*
    /// (crowd size ahead, own load, rival distance, fatigue) are measured at runtime in Milestone 4;
    /// these weights say how much each input is worth.
    /// </summary>
    [Serializable]
    public sealed class UtilitySettings
    {
        [Header("Race for next stop: 'big crowd ahead, bus half empty, rival close behind'")]
        [Tooltip("Score per waiting passenger at the next stop. placeholder")]
        public float RacePerWaitingPassenger = 0.05f;
        [Tooltip("Score per empty seat on board. placeholder")]
        public float RacePerEmptySeat = 0.02f;
        [Tooltip("Score when a rival is within RivalCloseMetres behind. placeholder")]
        public float RaceRivalCloseBonus = 0.5f;
        [Tooltip("What counts as 'close behind', metres. placeholder")]
        public float RivalCloseMetres = 150f;

        [Header("Skip the stop: 'nearly full, small crowd'")]
        [Tooltip("Load fraction (0..1) above which the bus counts as nearly full. RESEARCH: the full/empty dial. placeholder")]
        [Range(0f, 1f)] public float NearlyFullLoad = 0.9f;
        [Tooltip("Crowd size at or below which a stop is not worth it. placeholder")]
        public int SmallCrowd = 2;
        [Tooltip("Score for skipping when both conditions hold. placeholder")]
        public float SkipBonus = 0.8f;

        [Header("Wait and fill: 'early in route, no rival near'")]
        [Tooltip("Fraction of the route (0..1) that counts as 'early'. RESEARCH: ~70% of waiting happens before the halfway mark.")]
        [Range(0f, 1f)] public float EarlyRouteFraction = 0.5f;
        [Tooltip("Score for waiting when early with no rival near. placeholder")]
        public float WaitBonus = 0.6f;

        [Header("Block the player: 'player about to overtake, driver's spite high'")]
        [Tooltip("Score when the player is overtaking, multiplied by grudge. placeholder")]
        public float BlockPerGrudgePoint = 0.2f;

        [Header("Back off: 'fatigue high, dangerous gap ahead'")]
        [Tooltip("Fatigue (0..1) above which backing off starts scoring. placeholder")]
        [Range(0f, 1f)] public float FatigueThreshold = 0.7f;
        [Tooltip("Score per unit of fatigue above the threshold. placeholder")]
        public float BackOffPerFatigue = 2f;
        [Tooltip("Score when the gap ahead is below this driver's critical gap. placeholder")]
        public float BackOffDangerousGapBonus = 1f;

        [Header("Carrying the actions out")]
        [Tooltip("Seconds between decisions. RESEARCH: 'Every second, score each action'.")]
        public float DecisionIntervalSeconds = 1f;
        [Tooltip("Desired speed as a factor of cruise while racing. placeholder")]
        public float RaceSpeedFactor = 1.3f;
        [Tooltip("Nerve added while racing or blocking. placeholder")]
        public float RaceNerveBoost = 0.2f;
        [Tooltip("Desired speed factor while skipping a stop. placeholder")]
        public float SkipSpeedFactor = 1.1f;
        [Tooltip("Desired speed factor while backing off. placeholder")]
        public float BackOffSpeedFactor = 0.7f;
        [Tooltip("Nerve removed while backing off. placeholder")]
        public float BackOffNerveDrop = 0.3f;
        [Tooltip("Longest a racing bus stays at a stop: grab and go, seconds. placeholder")]
        public float RaceDwellSeconds = 8f;
        [Tooltip("Longest a 'wait and fill' bus stays, seconds. RESEARCH: drivers deliberately wait at early stops. placeholder")]
        public float WaitDwellSeconds = 40f;
        [Tooltip("Comfortable braking when pulling into a stop, m/s². placeholder")]
        public float StopDecelMs2 = 1.5f;

        [Tooltip("Distance before a zone at which a bus that will stop starts pulling to the kerb, metres. placeholder")]
        public float ApproachMetres = 60f;
        [Tooltip("Other-company buses race when another bus is within this distance, metres. placeholder")]
        public float RaceWhenNearMetres = 60f;
        [Tooltip("...by this factor on their cruise speed. placeholder")]
        public float RaceWhenNearFactor = 1.25f;

        [Header("Personalities")]
        [Tooltip("Archetypes a named crew member can be assigned. Edit the multipliers here, not in code.")]
        public DriverPersonality[] Personalities =
        {
            DriverPersonality.Default(),
            DriverPersonality.Reckless(),
            DriverPersonality.Cautious(),
            DriverPersonality.Spiteful()
        };
    }

    /// <summary>
    /// Per-character memory. RESEARCH.md: "grudge toward player, trust, fatigue, money today.
    /// Events move them (player cuts him off: grudge +1; lets him through: grudge −1) ... grudge > 3
    /// picks cold lines".
    /// </summary>
    [Serializable]
    public sealed class MemorySettings
    {
        [Tooltip("Grudge change when the player cuts this driver off. RESEARCH: +1")]
        public int GrudgeWhenCutOff = 1;

        [Tooltip("Grudge change when the player lets this driver through. RESEARCH: −1")]
        public int GrudgeWhenLetThrough = -1;

        [Tooltip("Grudge above which terminal dialogue picks cold lines. RESEARCH: > 3")]
        public int ColdLinesAbove = 3;

        [Tooltip("Grudge can't go beyond this in either direction. placeholder")]
        public int GrudgeCap = 6;

        [Tooltip("Grudge moves this many points toward zero for every night that passes before the crews meet " +
                 "again (rest days and yard days count). Trust (a negative grudge) cools the same way. placeholder")]
        public int GrudgeDecayPerShift = 1;

        [Tooltip("The player within this distance ahead, in the same band, is 'in the way', metres. placeholder")]
        public float BlockRangeMetres = 25f;

        [Tooltip("Seconds of being held up before it counts as a cut-off. placeholder")]
        public float BlockedSecondsForGrudge = 3f;

        [Tooltip("Seconds before the same driver can take another grudge point. placeholder")]
        public float GrudgeCooldownSeconds = 10f;
    }

    // ------------------------------------------------------------------------------------------
    // Passengers
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// RESEARCH.md: "Boarding time 2 to 6 seconds per passenger, more with pushing ... student fast,
    /// elderly with sack slow, half-fare arguer costs conductor attention."
    /// </summary>
    [Serializable]
    public sealed class PassengerSettings
    {
        [Tooltip("Fastest boarding, seconds. RESEARCH: 2 s")]
        public float BoardingMinSeconds = 2f;

        [Tooltip("Slowest ordinary boarding, seconds. RESEARCH: 6 s")]
        public float BoardingMaxSeconds = 6f;

        [Tooltip("Boarding time of a student (the fast case), seconds. RESEARCH: 'student fast'. placeholder")]
        public float StudentSeconds = 2f;

        [Tooltip("Boarding time of an elderly passenger with a sack, seconds. RESEARCH: 'elderly with sack slow'. placeholder")]
        public float ElderlyWithSackSeconds = 6f;

        [Tooltip("Extra seconds added when people are pushing at the door. RESEARCH: 'more with pushing'. placeholder")]
        public float PushingPenaltySeconds = 2f;

        [Tooltip("Seconds of conductor attention one half-fare argument costs. placeholder")]
        public float HalfFareArgumentSeconds = 15f;

        [Tooltip("Load as a fraction of seats above which a waiting passenger refuses to board ('too full'). placeholder")]
        [Range(0f, 2f)] public float TooFullLoad = 1.3f;

        [Header("Doors and zones")]
        [Tooltip("Fastest the bus may roll while people get on or off, m/s. RESEARCH: picking up without stopping is common. placeholder")]
        public float DoorSpeedMs = 3f;

        [Tooltip("Above the door speed and up to this, people still jump on and off, m/s. RESEARCH: forced off running buses. placeholder")]
        public float JumpSpeedMs = 6f;

        [Tooltip("Boarding and alighting time factor while rolling: the helper pulls, nobody dawdles. placeholder")]
        [Range(0.2f, 1f)] public float MovingDoorTimeFactor = 0.7f;

        [Tooltip("Chance of a fall per m/s above walking pace, per person. 0.05 → 20% at 5 m/s. placeholder")]
        [Range(0f, 1f)] public float FallChancePerMs = 0.05f;

        [Tooltip("Getting off is this many times as risky as getting on. placeholder")]
        public float AlightFallFactor = 2f;

        [Tooltip("A fall at or above this speed is an injury, below it a stumble, m/s. placeholder")]
        public float InjurySpeedMs = 3f;

        [Tooltip("A bus within this distance of a zone's centre is working it, metres. placeholder")]
        public float ZoneHalfLengthMetres = 20f;

        [Tooltip("Seconds added per metre a passenger must walk out to a bus stopped away from the kerb. placeholder")]
        public float WalkToBusSecondsPerMetre = 0.4f;

        [Header("Demand")]
        [Tooltip("People arriving per minute at an ordinary zone. Low enough that a crowd is taken whole by the first " +
                 "door and the second bus finds the kerb empty: arriving first is what pays (RESEARCH: the race for " +
                 "the stop; PLAYTEST: 'arriving second barely costs' at 3). Tuning pass, 3 Oct 2026. placeholder")]
        public float BaseRatePerMinute = 1.2f;

        [Tooltip("Multiplier for hot zones (junctions, markets). RESEARCH: hot clusters at junctions. placeholder")]
        public float HotZoneRateMultiplier = 2.5f;

        [Tooltip("Crowd size at which newcomers give up and take a rickshaw. Tuning pass: 18. placeholder")]
        public int MaxWaiting = 18;

        [Tooltip("Share of passengers who are students (fast, half fare). placeholder")]
        [Range(0f, 1f)] public float StudentShare = 0.2f;
        [Tooltip("Share who are elderly with a sack (slow). placeholder")]
        [Range(0f, 1f)] public float ElderlyShare = 0.1f;
        [Tooltip("Share who argue the fare (slow, cost conductor attention). placeholder")]
        [Range(0f, 1f)] public float ArguerShare = 0.05f;

        [Header("Fares (RESEARCH: Ticket prices, Sep 2026)")]
        [Tooltip("Minimum fare, Tk. RESEARCH: Tk 10")]
        public float FareMinTk = 10f;
        [Tooltip("Fare per km per passenger, Tk. RESEARCH: Tk 2.70")]
        public float FarePerKmTk = 2.7f;
        [Tooltip("Student half fare, as a factor. RESEARCH: the classic fight.")]
        [Range(0f, 1f)] public float StudentFareFactor = 0.5f;

        public void Clamp()
        {
            BoardingMinSeconds = Mathf.Max(0.1f, BoardingMinSeconds);
            BoardingMaxSeconds = Mathf.Max(BoardingMinSeconds, BoardingMaxSeconds);
        }
    }

    // ------------------------------------------------------------------------------------------
    // Performance
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// RESEARCH.md: "full behaviours within 100–150 m of player; kinematic corridor-following beyond".
    /// These keep the prototype running on a MacBook Air.
    /// </summary>
    [Serializable]
    public sealed class PerformanceSettings
    {
        [Tooltip("Within this distance of the player, vehicles run the full steering and decision layers. RESEARCH: 100–150 m")]
        public float FullBehaviourRadiusMetres = 125f;

        [Tooltip("Beyond this distance vehicles are despawned entirely. placeholder")]
        public float DespawnRadiusMetres = 400f;

        [Tooltip("Hard cap on simultaneously simulated vehicles. placeholder")]
        public int MaxVehicles = 120;

        [Tooltip("Hard cap on simultaneously simulated pedestrians. placeholder")]
        public int MaxPedestrians = 200;

        public void Clamp()
        {
            FullBehaviourRadiusMetres = Mathf.Max(10f, FullBehaviourRadiusMetres);
            DespawnRadiusMetres = Mathf.Max(FullBehaviourRadiusMetres, DespawnRadiusMetres);
            MaxVehicles = Mathf.Max(0, MaxVehicles);
            MaxPedestrians = Mathf.Max(0, MaxPedestrians);
        }
    }
}
