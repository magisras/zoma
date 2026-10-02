# Progress

One entry per session. The next session reads this first.

## Session 3 — 2 Oct 2026 — Building the system in the sandbox

Working through README milestones 3–6 in the engine-free core, each step tested in `check.sh`,
in the headless runner and in the browser. Entries below are appended as steps land.

**Step 1 — loop corridor, officer junctions, cross traffic** (done)

- `Corridor` supports closed loops: `Wrap`, `DeltaS` (short way round). All along-road maths goes
  through it. Agents carry their corridor; queries only see the same corridor.
- `Junction`: a cross street meeting the main road; an officer alternates the open direction
  with random timing from `OfficerSettings`; stop lines before the box; a few leakers run the cane
  when it drops; the box being physically occupied blocks the other stream whatever the cane says.
- Cross traffic: each cross street keeps a stream of generic vehicles that queue and go.
- Contacts across corridors (inside the box) use capsule distance in world space.
- Sandbox world is now a ~1.5 km ring with two cross streets; the renderer draws them and the
  officer with a coloured cane. HUD counts "cane runs" by the player.
- `tools/headless.sh [seed] [seconds] [capKmh]`: runs the sandbox world without a browser with
  the careful `ScriptedDriver` and prints the metrics; the same driver is the sandbox autopilot (P).
- Found by driving: the follow controller was under-damped (ClosingGain 1.5 → 3), pedestrians
  stepped out in front of stationary buses about to leave (now wary within 15 m), and a scripted
  driver that doesn't steer drifts onto the pavement and runs over waiting pedestrians.
- Tests: 27.

**Step 2 — demand zones, passengers, doors, fares** (done)

- `Passenger` (kind, destination, boarding seconds, fare factor), `DemandZone` (kerb spot that
  fills at a rate; hot zones ×2.5; crowd caps at 25), `BusLoad` (riders aboard, door, step timer,
  fares taken) on any bus agent.
- `Boarding`: a bus works a zone when within 20 m, under 3 m/s, door open. Off first, then on,
  one at a time at each passenger's pace plus walking time if the bus stopped away from the kerb.
  The first open door takes the crowd; a bus over 1.3× seats is skipped. Fares from the chart:
  Tk 2.70/km, minimum Tk 10, students half. Riders carried past their zone ride on (counted).
- Player: door on E (touch: DOOR). HUD shows zone in reach and crowd, door state and who is on
  the step, passengers/seats, fares, missed stops. Crowds drawn as small boxes behind the kerb.
- Six zones on the ring with fictional names; hot ones before the junctions.
- `ScriptedDriver` (autopilot and headless) stops at zones, opens, leaves when done or after 25 s.
- Tests: 34. Headless 5-minute shift: ~0.6–0.9 km, 2–7 scrapes, Tk 55–90 of fares. Slow and
  cheap, as the research says a careful driver's day is.

**Step 3 — rival buses: utility AI, personalities, grudge memory** (done)

- `RivalBrain` (crew name, personality, own company, grudge, fatigue, money, current action and
  scores) on a bus agent; `RivalAI` scores the five actions every second from the situation
  (crowd at next zone, empty seats, rival close behind, nearly full, early in route, player about
  to overtake, fatigue, dangerous gap), multiplies by the personality and acts: racing raises
  desired speed and nerve, skipping passes the zone, waiting fills at a stop, blocking mirrors the
  player's lateral, backing off slows and loses nerve. Stopping pulls to the kerb, slows in
  gently, opens the door, leaves when done, after the dwell (8 s racing, 40 s waiting) or if passed.
- Memory: cut-off is an event (player enters the band right in front, +1), being held up behind
  the player for 3 s is +1, the player moving aside while ahead is −1, a scrape with the player
  is +1. Capped; cooldown between points.
- Other-company buses: race-when-near only.
- `StopsLost`: a crew's door takes people at a zone the player is approaching from within 150 m.
  Helper gap report ("Rafiq 255 m ahead · Jamal 20 m behind") in the HUD; crews row with action,
  load and grudge. Own-company livery a darker shade of the player's; door open = white.
- Two named crews in the sandbox and headless runner: Rafiq (reckless) ahead, Jamal (spiteful)
  behind. In a 5-minute headless shift Jamal sits behind the careful autopilot and ends at grudge 6.
- Tests: 44.

**Step 4 — the daily ledger, the clock, the roadside** (done)

- `Economy` on the sim: fuel per km at the pump price, a trip counted (and the lineman paid) each
  time the stand is passed, the party man paid at his stand, scrapes charged as repairs, the
  zoma fixed before the day starts, a day length with a clock that maps onto a 06:00–20:00 shift,
  the day ending on time or on a pedestrian hit (arrested: the day's fares are gone and a case
  follows). `Ledger` holds the equation and an event log.
- The sergeant: after a junction he may step out, far more likely if you ran the cane. The bus is
  held (the pedals are overridden) until you answer: pay, or take a case and sit through the
  paperwork. Keys 1/2 or the card's buttons; the autopilot pays.
- Money scale: every Tk amount except fares and fuel is multiplied by `MoneyScale` (0.1) so a
  15-minute sandbox day keeps the real ratios. Real figures stay in the table. Sliders for day
  length and zoma.
- Sandbox: clock and paid-out rows, the sergeant card, the end-of-day ledger card with "what the
  crew eats" and the day's events, "Next day" restarts.
- Headless full day, careful driver: fares Tk 120 against a scaled zoma of Tk 300, fuel 58,
  repairs 180 (18 scrapes, mostly rivals rear-ending the slow bus): the crew eats −Tk 451. The
  reckless crew took Tk 165 of fares in the same day. That is the mirror moment, in numbers.
- Tests: 51.

**Step 5 — fatigue and the day rhythm** (done)

- `FatigueState` rises per shift hour (1.0 after ~13 h). It shows through the body, never a bar:
  the player's inputs pass through an `InputDelay` line (0.6 s late at full fatigue; nothing
  arrives until it has had time to), vision tunnels above 0.5 (vignette in the renderer), and
  above 0.7 micro-sleeps close the eyes for 0.5–1.5 s with the hands frozen where they were
  (black screen; the bus keeps going). Crews tire on the same clock and start backing off.
- `Household` across days: a worked day's net goes to savings minus food; sleep on the bus floor
  (free, keeps 60 % of the day's fatigue, never below 0.35) or take a bed (Tk 200 scaled, down
  to 0.1); work tomorrow or rest (no zoma, no fares, food still, fatigue to 0.05). The end-of-day
  card asks both questions in turn and shows the running savings.
- Sandbox: fatigue slider for testing; HUD fatigue row marked as debug.
- Headless 15-minute day: fatigue 1.0 at the end with 17 micro-sleeps, which is the research's
  routine 14-hour shift in miniature. Likely too harsh for play; tune with the owner.
- Tests: 57.

**Step 6 — the crew's voices** (done)

- `CrewVoice`: scripted lines chosen from the simulation's state with a cooldown per trigger.
  Helper: the bus behind and how far, "easy" when the bus ahead is far, the crowd at the next
  zone, "why aren't you honking" after 20 s of silence with traffic ahead, the open door at
  speed, "wake up" after a micro-sleep. Conductor: the sergeant ("hiding the cash, pay him"),
  the half-fare arguer, the count every ten boardings. Passengers: "stop, I'm getting off
  here" when carried past, "faster" when crawling on a clear road, "go round him" when stuck.
  At the terminal each crew says one line by grudge and by how the day went (cold above 3).
- Subtitles in the sandbox for 4.5 s each; `VoiceSettings` for thresholds and cooldowns. Lines
  are English placeholders; Bangla comes with the recordings.
- Tests: 62.

**Where this leaves the project**

Everything in README milestones 3–6 now exists as engine-free C# with tests, runs in the browser
sandbox and in the headless runner. Not yet done: the Unity side (project settings, scene, OSM
import, real bus physics, 3D assets), the wrong-side-of-the-road corridor, passengers alighting
from a moving bus with falls, the rollover set piece, repairs and the fitness certificate, the
mirror-moment scripting, Bangla lines and audio. All numbers are placeholders until the owner
plays; the headless runner gives repeatable metrics for that tuning.

**How to try it**: `make sandbox` (or the artifact link), P for the careful autopilot to watch,
then drive yourself. `./tools/headless.sh 1 900 25` for a full day's numbers in seconds.

## Session 2 — 2 Oct 2026 — Simulation core and browser sandbox

**Shipped**

- `tools/check.sh`: compiles all scripts with Mono against a UnityEngine stub and runs the tests with
  NUnitLite. The cloud session has no Unity, so this is how logic is verified. 20/20 tests pass.
- `Assets/Scripts/Core/` (engine-free, used by Unity and the sandbox alike):
  - `Corridor`: centreline polyline + width; (S, Lateral) ↔ world; projection for the player's bus.
  - `Agent`, `VehicleShape`, `SeededRandom`.
  - `Steering`: follow at a nerve-scaled headway (0.5–1.5 s), seek a gap sideways when blocked,
    yield by mass unless nerve bluffs. Per-second chances scaled by dt.
  - `HornSystem`: cone broadcast; yield boost by relative mass; pedestrians pause.
  - `Pedestrians`: cross band by band, stop mid-road, hurry when inside a vehicle's strip; the hand
    (less margin for small vehicles, more for buses).
  - `BusController`: power-limited acceleration, brakes that fade with wear, steering that gets
    heavy with speed, bicycle model. Off the road the bus bogs down.
  - `TrafficSim`: steps everything, resolves contacts (sideswipe pushes aside, rear-ender shoves
    forward), ends the day when the player hits a person, keeps traffic and pedestrians populated
    around the player, records the RESEARCH.md playtest metrics (headway, near misses/min, horn
    presses, yields, scrapes).
- `TuningTable` grew sections: Bus, Spawn, and finer Gap / Nerve / Horn / Pedestrian numbers.
- `sandbox/`: Blazor WebAssembly project that includes `Assets/Scripts/**` by path and draws it
  with three.js (vendored r170). Chase and top-down cameras, HUD with the metrics, live tuning
  sliders (T), restart (R). Verified headless with Playwright: loads, drives, horns, scrapes,
  ends the day on a pedestrian hit. `make sandbox` to run; see `sandbox/README.md`.

**Observed in the first scripted drives** (worth a human look)

- Full throttle down the middle for 25 s: ~9 near misses, 2 scrapes, 5 horn presses moving 23
  vehicles. Headway hits 0.4 s against crossing pedestrians. Feels like the right kind of
  uncomfortable, but a human must judge.
- Pedestrians used to walk straight into the bus; now they cross strip by strip. Still naive
  about a bus that accelerates after they commit.

**Not done**

- No rival-bus decision layer yet (utility AI, Milestone 4); the weights sit in `TuningTable`.
- No passengers, stops, ledger or fatigue (Milestones 4–6).
- Unity side untouched since session 1: no ProjectSettings, no scene, no OSM import.

**Next**

1. Owner drives the sandbox and reports what feels wrong (see sandbox/README.md "What to look at").
2. Milestone 4 logic in the core: two rival buses with utility AI, five demand zones, passengers
   choosing the first bus; drawn in the sandbox as more boxes.
3. On the MacBook: open the project in Unity, run the Blender import (docs/OSM_IMPORT_PLAN.md).

## Session 1 — 2 Oct 2026 — Milestone 1 scaffolding

**Shipped**

- `CLAUDE.md`: the working brief (design principle, stack, how to work, naming).
- `README.md`, `RESEARCH.md`: committed as the source of truth.
- Unity folder layout: `Assets/Scripts`, `Scenes`, `Prefabs`, `Data`, `Tests/EditMode`, with
  assembly definitions so tests compile separately from game code.
- `Assets/Scripts/Tuning/TuningTable.cs`: the one ScriptableObject that holds every gameplay number
  from RESEARCH.md's "Traffic and character AI (method)": mass hierarchy, nerve, critical gap
  (0.5–1.5 s), horn (cone, strength, mass weighting), officer timing and leakers, pedestrian hand
  confidence, utility weights for the five rival-bus actions, the three personality archetypes,
  grudge memory, boarding times, performance radius. Every field has a tooltip naming its source;
  numbers not in the research are marked `placeholder`.
- `Assets/Scripts/Tuning/VehicleClass.cs`: enum sorted by mass.
- `Assets/Tests/EditMode/TuningTableTests.cs`: five sanity tests that pin the research figures.
- `docs/OSM_IMPORT_PLAN.md`: the Blender blosm → FBX → Unity plan for the first 2–3 km
  (Mirpur 12 → Mirpur 10 → Kazipara), with bbox, tool versions, budgets and a centreline export script.
- `tools/check.sh`: compiles all scripts with Mono against `tools/unity-stubs/` and runs the tests with
  NUnitLite. Verified here: 5/5 pass. This is how logic gets tested in cloud sessions without Unity.
- `.gitignore`, `.gitattributes` (LFS lines commented until git-lfs is installed), `.editorconfig`.

**Not done, by design**

- No Unity `ProjectSettings/` or `Packages/manifest.json` are committed: those are best generated by
  Unity itself (see "Opening the project" below). No `.meta` files yet; Unity writes them on first
  open and they should be committed then.
- No `TuningTable.asset` instance: create it via Create > Twenty Tons > Tuning Table into
  `Assets/Data/` once Unity is open. Its defaults are the research figures.
- The OSM import itself (Blender work) has not been run; it needs a desktop. The plan is ready.
- No traffic AI, no bus. That is Milestones 2–3.

**Opening the project (first time, on the MacBook)**

1. `git clone https://github.com/magisras/zoma TwentyTons && cd TwentyTons`
2. In Unity Hub: New project > Unity 6 LTS > **Universal 3D** template, name it `TwentyTons-template`,
   anywhere outside the repo. Let it open once, then quit.
3. Copy that template project's `ProjectSettings/` and `Packages/` folders into the repo root.
   (Hub refuses to create a project into a non-empty folder, hence the detour.)
4. Open the repo folder in Unity Hub (Add > Add project from disk). Unity imports `Assets/`, writes
   `.meta` files and `Library/` (ignored).
5. Package Manager > Unity Registry: install **Splines** (corridors, Milestone 3) and confirm
   **Test Framework** is present. Input System can wait for Milestone 2.
6. Window > General > Test Runner > EditMode > Run All. Five green tests.
7. Create the TuningTable asset (above). Commit `ProjectSettings/`, `Packages/`, the `.meta` files
   and the asset.

**Open questions for the owner**

- Confirm the first chunk: Mirpur 12 → Mirpur 10 (2.5 km) or on to Kazipara (3.3 km)?
- Git LFS: fine to install on both laptops before the first FBX lands?
- Unity version: which exact 6000.x is installed? Pin it in `ProjectSettings/ProjectVersion.txt`.

**Next session — Milestone 1 continued, then Milestone 2**

1. Run the Blender import on the MacBook following `docs/OSM_IMPORT_PLAN.md`; land the two FBX and
   the `Corridor01_Greybox` scene; check the triangle and draw-call budget.
2. Then Milestone 2: a drivable 20-ton bus with worn brakes and passenger-load mass, on Vehicle
   Physics Pro Community Edition (free). Brake wear, mass per passenger and steering heaviness get
   their own section in `TuningTable`.
