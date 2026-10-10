# Progress

One entry per session. The next session reads this first.

## Session 4 — 8 Oct 2026 — Milestone 1: the corridor as grey boxes in Unity

**Step 27 — Unity project and the Mirpur 12 → Kazipara grey box** (done)

- The repo is now a Unity 6.3 project (6000.3.22f1, URP): `ProjectSettings/`, `Packages/manifest.json`
  (template packages minus Timeline, Visual Scripting, Collab, Multiplayer Center) and `Assets/Settings/`
  (the URP assets) taken from a fresh Universal 3D template; every existing file got its `.meta`.
  The core scripts and the test assembly compile in the editor unchanged.
- `tools/osm/fetch.sh` downloads the box of `docs/OSM_IMPORT_PLAN.md` from Overpass (the `/api/map`
  endpoint answers 406 now; the interpreter with a query works). `tools/osm/greybox.py` turns it into
  `Assets/World/Corridor01/`: `Roads.obj`, `Buildings.obj`, `Rail.obj`, `centrelines.json`,
  `markers.json`. No Blender in the loop; the plan's Blender route stays documented as an alternative.
- `Assets/Editor/GreyboxSceneBuilder.cs` builds `Assets/Scenes/Corridor01_Greybox.unity` headless or
  from the menu: ground, sun, four grey URP materials, mesh colliders on roads, blocks and the viaduct,
  the four route markers, the ODbL credit object, a camera at the stand looking south.
- Numbers: 1,719 road ways, 18,792 blocks within 300 m of the main road, the MRT viaduct with piers
  every 30 m, 237k triangles, 4 renderers (Unity merged each OBJ's cells into one mesh; fine at this
  size, split later if culling or streaming needs it). Route length stand → Kazipara 3.2 km.
- Not done yet in milestone 1: eyes on it. The scene has not been looked at in the editor; expect
  the usual first-look fixes (face winding, road widths by class, pier spacing against the real
  piers, buildings overlapping the road where OSM footprints do).

**Step 28 — first drive in Unity: the core bus model on the real road** (done, not yet felt)

- `Assets/Scripts/Unity/` (own assembly `TwentyTons.Unity`, Unity-only, excluded from `check.sh` and the
  sandbox): `PlayerBusDrive` moves a grey box with the core `BusController` (engine power, air brakes
  with lag and wear, heavy steering, load mass) along the main road from `route.json`; keyboard via the
  Input System; an IMGUI readout of speed, air, brakes, riders, lateral acceleration. `ChaseCamera`.
- `tools/osm/greybox.py` now also writes `route.json`: the shortest path over primary ways from the
  stand to Kazipara, 69 points, 3,326 m, as the bus's first corridor.
- `GreyboxSceneBuilder` adds the bus, creates `Assets/Data/TuningTable.asset` (the one table) and
  wires the camera. Keys: W/S, A/D, space, [ ] riders, R reset.
- Kinematic only: no collisions with anything, the road is a drawing. Milestone 2 proper (wheel
  physics, mass transfer, the worn-brake feel under a real suspension) is still ahead; this is to drive
  the street tonight and decide what the physics must reproduce.

**Step 29 — eyes on it: the CLI drives the open editor, renders views, the grey box was inside out** (done)

- `com.unity.pipeline` (Unity's CLI bridge) is in the manifest: `unity command eval '<C#>'` runs in the open
  editor, so scenes rebuild and reimport without quitting Unity. `Assets/Editor/SceneShots.cs` renders
  the scene to PNG from a chase, top-down or free viewpoint; the terminal session reads the picture.
- First look: every face pointed inward (roads and roofs down, walls in): the OBJ writer reversed faces to
  compensate for Unity's x mirror, which was wrong. Checked by counting imported normals (19,381 down,
  0 up on the roads), fixed in `greybox.py`, confirmed in the renders.
- The bus now starts 8 m left of the centreline, in the left carriageway beside the pier line; the OSM
  railway runs a few metres west of the road centreline, so the deck covers the western half of the
  road from above. Real alignment to check against photographs later.

**Step 30 — a street you can read: markings, pavements, median, posts; walls stop the bus** (done)

- Owner, first drive: "not clear where to drive, no actual road or markings, everything just grey
  boxes" and "I go through the buildings that are on the street". Both fixed.
- `greybox.py` writes `Markings.obj` (solid edge lines, dashed lane lines three a carriageway on the
  main road, a double line down two-way streets) and `Kerbs.obj` (2 m pavements a kerb high beside the
  roads the bus uses, a 0.6 m median barrier down the main road). The viaduct is now built along the
  road's own centreline, not the mapped railway a few metres off it, so deck, piers and median agree.
- Footprints that stand on a main road are dropped (1,009 of 18,800); on the lanes they are kept, since
  7 m default widths overlap the map's houses everywhere and dropping them hollowed the city out.
- Materials with contrast: dark asphalt, pale paint, warm blocks, dirt ground. A 40 m green post at each
  route marker so the next stop shows from down the road. 347k triangles, still under 400k.
- `PlayerBusDrive.StopAtWalls`: an overlap check of the bus's box after each move, from 0.3 m up; a hit
  undoes the move and stops the bus. Blocks, piers and the median are walls; paint and kerbs are not.
  Not a crash model, only the end of ghosting. The builder now places the bus at its start lane.

**Step 31 — drive-tested from the terminal: the corridor, the pedals, the walls** (done)

- `PlayerBusDrive` has scripted pedals (`Scripted`, `ScriptThrottle/Brake/Steer/Reverse`), `Tick` and
  `Simulate(seconds, …)` so a terminal session drives it at 60 Hz through `unity command eval`; the
  editor does not tick Play mode while its window is behind. Snippets and how-to: `tools/osm/drivetests/`.
- Pedals (empty bus, worn brakes 50 %, air 40 % at the start): 0–40 km/h in 7.6 s (9.9 s with 90 riders);
  stop from 40 km/h: 29.6 m worn, 13.7 m new brakes with full air, 22.6 m worn with 90 riders (shorter
  only because the longer run built more air: the model's brake decel does not scale with mass; a
  tuning question for the owner). Full lock at 40 km/h: 43° of yaw in 1.5 s at the 7 m/s² tyre limit.
- Collisions: the first rule undid the move, so a bus that touched the median could never move again.
  Now the bus is pushed out of what it hits (`Physics.ComputePenetration`) and stopped; `X` reverses
  at a walking pace. Verified: hit the median at 15 km/h, backed off, drove on.
- The full route, with an aim point 8 m plus one second ahead: every stop found a real fault in the
  grey box, each fixed in `greybox.py`:
  Rokeya Sarani is two one-way carriageways 20 m apart (each had been drawn 24 m wide with its own
  median; now 8–10 m, pavement on the kerb side, median and viaduct on the midline between them);
  the median search matched a cross street at Kalshi Road (now parallel, right-hand, abeam only, with
  the offset carried through junctions and the barrier opening at main crossings); mapped node jitter
  put 2 m jogs in the carriageway (main roads smoothed at the source, the route stitched from the same
  points, Douglas-Peucker to keep the triangle count); and the Mirpur 10 metro station, a 70 × 180 m
  `building=train_station` with `layer=3`, stood across the road (elevated buildings now sit on the deck).
- Result: Mirpur 12 stand to Kazipara, 3.2 km, 4 min 19 s at a 45 km/h cap, lane held within a metre,
  2.8 s off the carriageway at the Mirpur 10 bend. 332k triangles.

**Step 32 — a Mirpur skin on the grey box, procedural, no assets** (done, first pass)

- `Assets/World/Shaders/Surface.shader`: one URP shader, a `_Mode` per material. Facade mode paints a
  window row per storey (storeys and a per-block seed travel in the mesh's texture coordinates from
  `greybox.py`), roller shutters and signboards on shop rows, gates and barred windows on house rows,
  slab lines, the concrete frame's columns, damp from the pavement, streaks from the roof, a tint per
  block from a palette of Mirpur walls (raw concrete, lime wash, pale yellow, salmon, sky, brick).
  Asphalt, pavement slabs, formwork concrete with rust on the piers, and dirt are the other modes.
  Everything is hashed from world position: no textures, nothing downloaded, nothing to license.
- Found the hard way: the Mac's shader compiler process dies, without a message, on a noise function
  called inside a branch, and on a ternary between vector swizzles. All noise is now computed before
  the branches, which only pick colours. Bisected with `ShaderUtil` and the editor log's IPC count.
- Lighting is main light with shadows plus 0.45 of the sky ambient. Still a bit bright and uniform;
  the owner decides what Mirpur should feel like before more time goes here. Next candidates: rooftop
  water tanks and stair heads, AC units, hanging wires, a sky with haze, dust on the lens.

**Step 33 — the kerb is mountable** (done)

- Owner: "why I cannot go on curb, I just stuck and bus don't move". The sandbox's off-road rule
  (past the corridor the bus is in the market stalls, 4 m/s² of drag against 1.8 of engine) was
  reaching the pavement. Now the drive's corridor is the carriageway plus 2.5 m of pavement a side;
  mounting the kerb costs 1.5 m/s and holds the bus to 12 km/h while a wheel is on it; the stalls'
  drag starts beyond. Tested: mount at 33 km/h, crawl along, back to the lane. A bus nosed into a
  building still stops dead and needs X to back off; the readout says ON THE PAVEMENT.

**Step 34 — the wheel works in reverse** (done)

- Owner: "when I go in reverse, why steering don't work?" The core model turns the front wheels only
  inside its forward step, which reverse skips, so the angle froze. Reverse now turns the wheel with
  the same rate rule. Tested: 4 s back with the wheel left swings the nose 33°, right swings it back.

**Step 35 — the wrong side is road** (done)

- Owner: "I am stuck on the sidewalk, I cannot get into the opposite street". The drive's corridor was
  the carriageway plus pavements, so the median gap and the oncoming carriageway counted as off the
  road (the stalls' drag) and as pavement (the crawl). `greybox.py` now writes `farEdge` per route
  point (the far kerb beyond the oncoming carriageway, from the median line); the drive feeds it to
  the core model's `roadFarEdge` and uses it for the pavement rule. Tested: 15 km/h into the Kalshi
  Road gap, across the median, onto the oncoming carriageway at 25 km/h. Past the far kerb the crawl
  and the buildings apply, as on our own side.
- The median barrier itself stays a wall except at the three main crossings: a bus crosses at a gap,
  as it does on Rokeya Sarani. Gaps are 28 m; turn inside them.

**Step 36 — off the road is a crawl, and scrubbing tyres cost speed** (done)

- Owner stuck a second time: on the dirt between two blocks beyond the far pavement, 20 riders aboard,
  nothing touching the bus. The sandbox's off-road drag (4 m/s²) beat the engine (1.8), a dead stop.
  `OffRoadDecelMs2` is 1.2 now, in the table and in `Assets/Data/TuningTable.asset`, and the drive caps
  off-road speed at 8 km/h; the readout says OFF THE ROAD. Stalls and people will be real obstacles.
- That broke `FullLockAtCitySpeedScrubsTheTyresAndDoesNotTip`: the test had passed only because the
  drag stopped the bus once its full-lock circle left the road; without it the engine pushed the circle
  past 36 km/h and the tipping rule fired. The honest fix is in the model: scrubbing front tyres bleed
  speed (`TyreScrubDecelMs2` 2.5 at full scrub), so a full-lock turn at city speed slows, as a bus does.
  119 tests pass; the 58 km/h swerve still tips.

**Step 37 — milestone 2, first cut: the bus is a body on springs** (done, needs the owner's hands)

- `PhysicsBus`: a 12 t rigid body on four wheel colliders, suspension from the table (`SuspensionHz` 1.0,
  `SuspensionDamping` 0.14, travel 0.28 m, centre of mass 1.4 m up and higher with standing riders).
  The core `BusController` now exposes `Forces()` and `TurnWheels()`, so the same engine, air-brake lag,
  wear and heavy steering drive the wheels' torques and angles; the body is read back into the Agent.
  `BusBody`: a blocky bus (shell, window band, windscreen, open left door, bumper, roof rail, four
  tyres) until a mesh exists. `CameraRig`: C cycles chase, driver's seat (right-hand side), door step.
- Measured: 0-38 km/h in 8 s empty, 34 km/h with 90 riders (17.9 t); a full-lock swerve at 35 km/h
  leans 8° (the outer springs bottom: travel 0.28 over a 1.95 m track) at 3.9 m/s²; a hard stop from
  30 km/h in 2.5 s with the nose down; 0.9 m median barrier stops the body dead (0.6 let the raycast
  wheels ride over it; the collider's floor is 0.3 m); a 15 cm kerb is climbed at speed with a 10° lurch.
- Not yet: damage, the rollover from physics (the core's check is not wired to the body), engine sound,
  the lingering wallow (needs the owner's judgement on `SuspensionDamping`), real Ackermann, dual rear
  tyres. The kinematic drive remains for a bus without a `PhysicsBus` and for the sandbox.

## 9 Oct 2026

**Step 38 — the bus tips the way a real one does: tyres first, then a trip** (done)

- Owner: "I think I am too easy to make bus fall on its side. Do real buses have such bad balance?"
  No: on dry tarmac a loaded bus holds about 0.65 g sideways and ploughs wide before it tips at about
  0.7 g; nearly every real rollover is tripped (docs/BUS.md §5). The first physics body had tyres at
  about 1 g and the centre of mass at 1.4 m, so the body went over with the tyres still holding.
- Now: `TyreSidewaysGrip` 0.65 on the wheel colliders' friction curve, an anti-roll bar per axle
  (`AntiRollNewtonsPerMetre` 60k), centre of mass 1.1 m (asset updated). Full lock at 40, 50 and
  55 km/h, empty and with 90 riders: 8-11° of lean, ploughs, stays up. Three swerves at 45 with 60
  riders: 8°, stays up.
- The trip: raycast wheels climb a kerb smoothly, so the tripped rollover is a rule from the core
  (`RolloverSpeedMs` 36 km/h, `OffRoadRolloverMetres`) plus `KerbTripSidewaysMs` 2.5: hit the kerb
  sideways at speed and `PhysicsBus.Trip` throws the body over physically. Measured: shallow mount at
  45 climbs (9°); sideways at 25 climbs (11°); sideways at 45 with 60 riders goes to 35° and comes
  back; sideways at 50 with 90 riders goes over. R rights it for now; the rope and the men come later.

**Step 39 — reverse is a gear** (done)

- Owner: "when I get on curb, reverse not always work". In the physics body reverse was a negative
  torque scaled by the core engine's output, and the core gives nothing without the throttle, so X
  alone did nothing. X now opens the throttle (reverse gear, half torque, walking pace). Tested: onto
  the pavement at 25 km/h, stop, X with the wheel turned, back on the road in six seconds. The
  readout shows the signed speed, negative when backing.

**Step 40 — the anti-roll bar pushed the wrong way** (done)

- Owner: "before bus was fluid and reacted to turns, now it just tilts and is not fluid". The bar
  added in Step 38 had its sign reversed: it pushed the compressed side down and amplified the lean,
  so the body sat tilted. Fixed. Now a 1.5 s swerve at 40 km/h with 40 riders builds 0.4 → 6.6° of
  lean and, wheel straight, rocks back through +1.5° and settles in about 2 s: the wallow of soft,
  tired dampers. Full lock at 50 with 90 riders: 11.6°, stays up. Sideways into the kerb at 50 with 90:
  over. The Step 38 lean figures were measured with the bar backwards and are superseded.

**Step 41 — the kerb trip needs a load and a real sideways hit** (done)

- Owner: "I just hit a curb and fell over, this does not make sense." The record: empty bus, 40 km/h,
  34° onto the kerb, tripped by a 2.5 m/s sideways threshold that ignored the load. No real empty bus
  goes over on a 15 cm kerb like that. The threshold is now `KerbTripSidewaysMs` 4.5 for a full bus,
  60 % higher for an empty one, and every trip is logged (`TRIP: …` in the editor log, with speed,
  sideways speed, angle, threshold, riders) so the next surprise can be read instead of guessed.
- Measured from the kerb lane at full lock, 40 to 55 km/h, empty or 90 riders: the bus climbs the
  kerb with 5-10° of lurch and stays up, because from the kerb lane it cannot reach the kerb at more
  than about 15°. A trip now needs a steep hit from further out across the carriageway, loaded.
- Note for next time: a public field on a MonoBehaviour is saved in the scene, so changing its
  default in C# changes nothing until the scene is rebuilt. That hid the new threshold for one run.

**Step 42 — the bus cannot climb the median; T tows it back** (done)

- Owner: "look I'm stuck": high-centred on the median barrier, one front wheel on top, the opposite
  rear in the air, the body resting on the concrete. Three things, all found by instrumented runs:
  a wheel ray that finds the top of a wall climbs it, and a body lifted a little by a contact finds
  it; a hollow mesh obstacle shorter than the body has its top face inside the body's box the moment
  they overlap horizontally, and that face lifts the box (so 1.1 and 2.6 m walls were both climbed);
  the 1.1 m barrier's own collider was that first lift. Now: the median is drawn only (`Median.obj`,
  no collider); an invisible 4.5 m wall over it (`Walls.obj`) does the stopping; wheels are on a layer
  that ignores it (`Wheels` × `NoWheels` off in the matrix, set by `PhysicsBus`); the body's collider
  floor is 0.2 m with a slippery skin; no block is shorter than the body (3.8 m). Rammed at 35, 50 and
  55 km/h, full lock, empty and with 90 riders: body stays down, all wheels on the road, reverses off.
- `T` tows the bus back onto the kerb lane at the same point of the route, standing: the people and
  the truck of a real beaching, to be charged by the ledger later. The readout names it.

## 10 Oct 2026

**Step 43 — the whole route, Mirpur 12 to Azimpur, streamed a kilometre at a time** (done)

- Owner: "what's the plan for the recurring 12 km map?" and "I agree with your recommendations":
  the full corridor, cut into chunks, streamed around the bus, meshes regenerated rather than
  stored. The route measures 13.6 km: Rokeya Sarani, Khamar Bari Road, Kazi Nazrul Islam Avenue,
  Karwan Bazar, Shahbagh, TSC, Nilkhet, Mirpur Road to the Azimpur stand (S-007.6's pickups as the
  markers, 16 of them, within 20 m of the stands at both ends).
- `tools/osm/fetch.sh` downloads a corridor along the markers (roads within 600 m, buildings within
  300 m), not a box: 18k ways, 11 MB, with a second Overpass server and retries, since the first
  answered "too busy". `tools/osm/greybox.py` stitches the route through every marker in order (one
  Dijkstra over (markers passed, node) states, so a candidate node on the wrong carriageway cannot
  dead-end a leg), keeps buildings near the route only, and writes `Chunk_NN/` folders of 1 km along
  the route plus `chunks.json` (each chunk's stretch sampled every 50 m). 14 chunks, 10k–52k
  triangles each, 350k in all.
- `GreyboxSceneBuilder` makes one scene per chunk under `Assets/Scenes/Chunks/` and the base scene
  (sun, ground sized to the route, markers, bus, camera, streamer); all are in Build Settings.
  `WorldStreamer` loads the chunks within 1,200 m of the bus additively and unloads beyond 1,700 m,
  both directions, so the out-and-back day works. Verified by teleport: at Farmgate chunks 6–9 are
  in and 0–1 out; back at the stand the reverse. One rule learned: a scene load, synchronous or not,
  only reports loaded at the next frame, and asking again loads it again (400 copies of one chunk
  before the bookkeeping was added).
- `make world` rebuilds everything from the download; the meshes, chunk scenes and `centrelines.json`
  are gitignored (the owner's choice over Git LFS). `route.json`, `chunks.json`, `markers.json` stay.
- Found by driving the whole route by script (`tools/osm/drivetests/fullroute.cs`): trunk roads were
  not in the generator's width table, so Kazi Nazrul Islam Avenue did not exist; roundabouts (closed
  ways) were stitched the long way round and back; a node shared by two ways put a 6 m hairpin in the
  route, and the median wall built from it stood square across the lane at Mirpur 10; the median
  offset could step sideways across the lane, now limited to a quarter metre per metre of road;
  a pavement ran on across the next road's carriageway at every junction, now clipped where it lies
  on a road. After these: 13.6 km in 1,237 s at up to 45 km/h, max roll 8°, three tows, all at
  sharp junction corners where the script driver (not the geometry) overshoots. Photos at Farmgate,
  Shahbagh and Azimpur render clean 11 km from the origin, so no floating origin yet.
- Not done: the far end is still only grey boxes with the Mirpur skin (zones for Agargaon, Farmgate,
  Shahbagh and Azimpur are a later step); bus stops along the route from OSM; the return leg as a
  trip in the day loop.

**Step 44 — the way back, the stops, and the day as legs** (done)

- Owner: "lets go" on the plan's next step. `tools/osm/greybox.py` now runs its route search a second
  time with the markers reversed, so the way back from Azimpur comes out on the other carriageway
  wherever the road is dual (`route_back.json`, 13,585 m, the same length to the metre). The Overpass
  download asks for bus stop nodes too (`highway=bus_stop`, bus platforms, stations); `stops.json`
  holds the 9 OSM stops that lie on the kerb side of a leg, plus the research's 16 named pickups and
  stands (S-007.6, RESEARCH.md) as the hot ones, each with its distance along both legs and a point
  for a sign on each leg's kerb. The research markers were read off the map by eye and sit up to
  280 m off the road (Taltola, Shewrapara, Shahbagh); their S is the projection, the sign stands on
  the kerb at that S. These are milestone 4's demand zones; `TrafficSim.AddZone` takes the same three
  things (name, S, hot).
- `PlayerBusDrive` drives legs: out on `route.json`, back on `route_back.json`, each with its own far
  kerb. Standing within 80 m of the end of the leg turns the leg around (`WatchStands`); the driver
  turns the bus. Back at Mirpur 12 is one trip. The readout shows the trip, the leg, the next stop and
  its distance, and the note at the stand for ten seconds. `RouteStops` reads the file; the scene
  builder puts a post and a board on the kerb at every stop (41 signs, hot ones bigger) and wires
  both routes and the stops to the bus.
- Found by driving both legs by script (`fullroute.cs`, now with a `LEG`): a median wall stood in
  the lane of the way back at Azimpur, where the two carriageways are 8 m apart and the median
  matcher, which only knew primary roads, had carried an offset from up the road. The matcher now
  takes any one-way main road, and builds no barrier where the gap has no room for one (84 of 308
  dual route points, Mirpur Road through Nilkhet and Azimpur among them: a painted line there,
  crossing to the wrong side open). The chunk writer now deletes a chunk's file when the generator
  no longer builds that kind there, or the stale mesh stays in the scene: that wall survived one
  rebuild that way. MRT piers stood in the lane too, at Mirpur 10 round the roundabout, where the
  median line was carried, and inside the roundabout's other arc, 11 m from ours, where it was
  matched. The pier line is now the median where it was found and stands clear of both legs'
  carriageways, else the mapped metro line where that is clear (at Khamarbari the metro line runs
  along the outbound centreline in OSM, so it is not), else the median line with no pier at that
  point: the deck alone. The barrier gets the same rule. Clearance is measured from the carriageway's
  own width at that point (`route.json` now carries `widthAt`; the one `width` is the narrowest way
  on the route, 8 m, while Kazi Nazrul Islam Avenue is 10.5 m): a pier that cleared the narrow width
  stood in the outer lane at Karwan Bazar and sent the bus crawling along the kerb for eight
  minutes. 300 piers now against 373, and barriers at 189 of 308 dual route points: on Rokeya
  Sarani OSM draws the carriageways 12.4 m apart, which leaves no 3 m barrier between 10.5 m
  carriageways; the piers there stand on the mapped metro line instead.
- Result of the script drives after the fixes, every chunk loaded: out, 13,585 m in 1,248 s, top
  45 km/h, max roll 8°, one tow, at the sharp Azimpur corner where the script driver runs 6 m wide
  onto the pavement (Step 43's Mirpur 10 and Nilkhet tows are gone with the pier and the barriers
  that stood in the road); back, 13,584 m in 1,189 s, top 45 km/h, max roll 15°, no tow. At Azimpur
  the leg turned to the way back; at Mirpur 12 the leg turned again and trip 1 was counted. 41 stop
  signs stand on the kerbs.
- Not done: the stops are signs and a name in the readout, not crowds (milestone 3's people and
  milestone 4's zones come from the same file); the lineman and the ledger per trip are in the
  sandbox's Economy, not yet wired to the Unity day; the scripted driver still clips the median
  wall at the Mirpur 10 roundabout exit on both legs.

**Step 45 — the street is alive on the real road: the sim, people and traffic, the day as one loop, zone skins** (done)

- Owner: "lets do all of it" (the sim wired in, people and rickshaws, zone skins; the play-test is
  theirs). The core `TrafficSim` now runs behind the physics bus (`StreetSim`): crowds grow at the
  stops and board at the open door, two named crews of the owner's company (Rafiq, Jamal) race for
  them, generic traffic and other buses fill the road around the player, people cross anywhere, the
  sergeant's boxes stand at Mirpur 10, Agargaon, Farmgate and Shahbagh both ways, the ledger counts
  fares, fuel, the lineman per trip and the party man at Karwan Bazar. The bus body is the authority:
  each physics step the player's Agent is written from the body, the sim steps the world and applies
  the delayed, possibly frozen, hands to the BusController the body drives with
  (`TrafficSim.ExternalPlayer` skips the kinematic step and the core's rollover check; the physics
  body tips itself and tells the core, so the crowd and the ropes follow). What the sim does to the
  player's speed, a truck in the nose or a sergeant's hand, comes back as a cap on the body.
- The road is one closed corridor: out on `route.json`, back on `route_back.json`, joined at the
  stands, 27.2 km round. The stops of both legs are zones along it (41), a trip is a lap, the leg is
  where you are. The projection keeps its leg: the two carriageways are a few metres apart at the
  stands and on dual stretches, so the nearest line is not the answer (the way back flipped onto the
  way out at the Azimpur corner); the bus changes leg only when the other leg is closer and it is
  heading that leg's way, the U-turn at the stand (`Corridor.Project` over a stretch, with memory in
  `PlayerBusDrive.ProjectPlayer`).
- `StreetView` draws the sim: vehicles as boxes of their shape coloured by class (the company's
  buses in the player's own colour), people as capsules, the crowd at each stop on the kerb. No
  colliders: the bus meets them through the sim's contact rules. Real bodies are the art pass.
- The readout: the clock, fares, paid out, the zoma, the crew's take, trips; aboard and waiting at
  the next stop; the own-company bus ahead and behind; the sergeant's demand (P pay, N refuse);
  the ropes after a rollover; the last three ledger lines; the day's end (R: a new day). H is the horn.
- Zone skins in `Surface.shader`: five bands of world z along the route change the facades' palette,
  the share of shops and signboards and the grime: Mirpur as before; Agargaon concrete and white
  with few shops; Farmgate and Karwan Bazar shops and signboards on every ground floor, grimy; the
  university stretch red brick and old white; Nilkhet and Azimpur lime and pastel, shops, the
  dampest walls. The characters are the builder's reading, to check against street photographs.
- Tests: `fullroute.cs` drives the road alone (street off): out 1,246 s, back 1,189 s, no tow either
  way, the leg turning at each stand. `street.cs` drives the first 3.6 km with the street alive and
  a driver who brakes for the gap ahead and for people on the carriageway: 774 s, seven stops
  worked, Tk 40 of fares, Rafiq first at four crowds, three contacts (two hard), two near misses,
  nobody hurt. A second run of the same test ended the day at the Mirpur 10 roundabout after 575 s:
  the driver cut the corner onto the pavement at 28 km/h and knocked three people down, Tk 6,000
  each on the spot and the bus seized at the second, Tk 19,000 of cases against Tk 90 of fares. The first version
  of that driver stood twenty minutes at Mirpur 12 because it counted the crowd on the pavement as
  people in its lane; the sim's own earlier run killed someone at Mirpur 11 within ninety seconds
  with a driver that does not look. That is the game: the street punishes a driver that does not
  look, and the test driver is crude.
- The police hand: `greybox.py` writes `crossings.json`, the cross streets with an officer. OSM
  splits ways at junctions, so each street is stitched from the arms leaving the node (the two most
  opposite arms make a crossing, one arm a T run into the junction), 150 m either side, and cut
  against each leg's centreline for the S on the leg and on the street. Six of them on the way
  out, four on the way back (Agargaon Link Road, Rokeya Sarani's own branch at Agargaon, Lake Road,
  a T at Kazi Nazrul Islam Avenue, Natun Eskaton Road, Minto Road); the residential side streets
  are not junctions, and a stub whose box sits within 30 m of its start is dropped, since cross
  traffic born inside the box blocked the main road for good (the first run jammed at 613 m behind
  twenty standing vehicles). Cross traffic is born only before the first stop line. `StreetSim` adds a `Junction` per cut, the back leg's mirroring
  the out leg's officer as the sandbox's two carriageways did; cross traffic spawns only at the
  junctions within 400 m of the player (`MaintainPopulation`), since a route has dozens where the
  ring had two. `StreetView` draws the officer facing the stream he lets through and the rope across
  the stop line when he has stretched it; the readout says when the cane is against you and how
  far the line is. Mirpur 10's roundabout and Farmgate's turn have no officer yet: they are not
  crossings of a main street in the map's terms.
- Placeholders for the owner: the day is three real hours (`StreetSim.DayLengthSeconds`), real money
  (`MoneyScale` 1, the zoma Tk 3,000), a trip's fuel over 13.6 km; the household between days is
  not wired.

## 8 Oct 2026 — Moved to the owner's Mac

- The cloud session was teleported into Claude Code on the MacBook Air; the game lives in
  `~/Code/zuma`. Installed Mono via Homebrew and the .NET 8 SDK per-user (`~/.dotnet`, no sudo);
  recipe in `docs/TESTING.md`.
- `tools/check.sh` used `mapfile`, which macOS's bash 3.2 lacks; replaced with a while-read loop.
  119 tests pass, `dotnet publish` of the sandbox builds, `tools/headless.sh` runs.
- Next is unchanged: rebuild the Dhaka autopilot on the crews' decision layer (see Step 26).

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

**Step 7 — the oncoming carriageway and the wrong side** (done)

- A second corridor runs the other way beside the main one (left-hand traffic: oncoming on your
  right, past a painted median). The player appears on it as a ghost agent with the same body
  and a negative speed, so its traffic sees a bus coming at them: head-on steering gives up the
  closing speed, and anything much lighter swerves out of the band at once ("size is right of
  way"). A head-on hit stops the bus; contacts through the ghost push the real player.
- Cross streets now cross both carriageways; the oncoming side has a mirrored junction that
  copies the officer's cane. The sergeant has a new reason: wrong side (the Tk 3,000 case).
- Metrics: wrong-side seconds. Helper shouts "Bus coming! Back!" when something heavy is coming
  while you are over there; the conductor suggests crossing over when stuck.
- Found by driving: once the keyboard had been touched, the stale frame kept overwriting the
  autopilot's pedals; inputs now apply only on the step they were pushed.
- Tests: 67.

**Step 8 — the Dhaka driver and the thesis report** (done)

- `ScriptedDriver` now has two policies: careful (as before) and Dhaka (0.6 s headway, horn,
  cut-ins into the freest band, runs the cane when the box is clear, takes the wrong side when
  stuck, grab-and-go stops). Both brake for a person in the way. P cycles them in the sandbox.
- `./tools/headless.sh --batch seeds seconds [crowdRate]` runs both over the same seeds and
  prints the research's own verdict. `docs/PLAYTEST.md` records the first result: polite driving
  still wins, by Tk 500 a day, and why (scrapes, serial boarding, uncontested crowds), with the
  tuning hypotheses to test.
- Found on the way: the Dhaka driver overshot every zone at 45 km/h (now slows in for a crowd),
  then spent its whole dwell unloading (dwell now counts from when boarding can begin).

**Step 9 — the helper's door** (done)

- One person gets off while another gets on: two slots at the door, each with its own clock.
- Rolling faster than a crawl but under the jump speed (6 m/s), people still get on and off,
  quicker (the helper pulls), with a fall chance per m/s above walking pace; getting off is twice
  as risky. A fall below 3 m/s is a stumble (back to the kerb, shaken); at or above it an injury:
  the crowd holds the bus for a minute and the crew pays on the spot; the second injury of the
  day is the police and the end of the day. Helper lines for both.
- HUD: both door slots and a falls row. Headless prints stumbles and injuries.
- Thesis batch after this step: careful −218 (fares 216), Dhaka −784 (fares 146, 23 scrapes).
  Loading in parallel lifted both; scrapes still decide it.
- Tests: 72.

**Step 10 — the bus as a character** (done)

- `BusCondition` lives in the household and rides the same bus every day: brake wear grows with
  how hard and how fast you brake (scaled like the money, so a sandbox day wears like a real
  one), every scrape leaves a dent, and the fitness certificate is a number on a paper with an
  expiry day. The controller's brakes fade with the wear.
- At the end of the day, before sleeping: service the brakes (Tk 4,000 scaled) or buy a
  certificate (Tk 3,000 scaled, seven days), both from savings, into debt if need be. Skipping
  is the loan against tomorrow the research describes.
- The sergeant's "papers" stops fall to 30 % with a valid certificate and rise with dents; a cane
  run or the wrong side is not helped by paper. Helper mentions soft brakes above 70 % wear.
- Tests: 76.

**Step 11 — the rollover** (done)

- Two ways to tip twenty tons: leave the road at 8 m/s or more (the railing catches the wheels;
  asleep at the wheel this is the drift the Fraser film shows), or corner above 0.6 g at speed.
  Everyone aboard tumbles: 30 % are hurt, all of them get out and leave. The bus lies on its
  side (drawn so), the crowd gathers, a man offers ropes and a tractor for Tk 5,000 scaled.
- Pay: three minutes of shouting, then back on its wheels and straight into service with a
  cracked windscreen, a bent door (people take longer at it), ten dents and worse brakes. Walk
  away: the day ends, the zoma is still owed, the damage is there tomorrow anyway.
- The crew carries the injuries for two shifts: slower hands and a tired start. Conductor line.
- Tests: 81.

**Step 12 — light contact is cosmetic** (done)

- Contact under 1.5 m/s of relative speed leaves a dent and no bill (RESEARCH: paint and mirrors
  are the norm); harder contact costs as before. `HardContacts` beside `Contacts` in the metrics,
  HUD and headless. Thesis batch: careful −154, Dhaka −650; see `docs/PLAYTEST.md`.
- Fixed: hidden HUD rows (the autopilot label) showed anyway because the flex rule beat the
  `hidden` attribute.

**Step 13 — the helper's seat** (done)

- O swaps seats: the ostad (the Dhaka policy) drives, the player is the helper. The ostad stops
  only where the helper calls (E, the door), leaves when the helper lets go, and takes the
  helper's hand as hurry (W, 50 km/h) or easy (S, 22 km/h). Riders who want off ride on unless
  the helper calls their stop. RESEARCH: "Act 1 as helper is the tutorial" — the player watches
  the hierarchy, the horn and the hand before being responsible for them.
- Verified in the browser: a scripted helper called Block 11, the ostad pulled in, Tk 125 of
  fares in two minutes with the ostad doing the driving.

**Step 14 — the voice lines as data** (done)

- `Core/VoiceLines.cs`: every line the helper, conductor, passengers and crews say, keyed by id,
  with the English subtitle, an empty Bangla column and a note for the writer saying when it is
  said and what it must do. `CrewVoice` refers to ids only. In Unity the table becomes an asset.
- Fixed: the Dhaka autopilot's horn was being reset by the key handler every step, so a blast
  counted as sixty presses a second.

**Step 15 — the street's control: signals, the rope, police boxes, drive days, the yard, cameras** (done)

- Asked: traffic lights, policemen on roads, crossings, active checking and stopping: how is it
  really done? Researched and written up in `docs/STREET_CONTROL.md` with sources: the signals are
  dark and the officer's cane runs the junction (central-spine pilots aside); constables rope
  closed approaches; sergeants at police boxes have targets and special drive days; refusing with
  bad papers can put the bus in the dumping yard for days; AI cameras went live on 7 May 2026 at
  8 junctions and SMS the owner; the road is the crossing, zebras and overbridges are scenery.
- In the core, each as a rule the world enforces, never a UI: `Junction.Signal` (scenery, three
  modes), `Junction.Roped` (no leakers; the player's bus brakes for it whatever the pedal says and
  its nose is held at the line), `Checkpoint` police boxes with a sergeant on duty by chance (the
  'papers' stop moves there from the junction), `Economy.DriveDay` (×2.5 stops, ×1.5 price, the
  conductor says so), seizure on a refusal without papers (`Household.YardDay`), `Junction.Camera`
  from day 6 on the first junction (an SMS case on the night's ledger), and pedestrians clustering
  near stands and junctions. Eight tests in `StreetControlTests`, 89 in all.
- Sandbox: signal poles (dark), a camera on its pole from day 6, ropes with a constable at the kerb
  end, blue police huts with a sergeant beside the manned ones, a 'street' HUD row, the seized
  day's card and the days in the yard. Headless prints junction states and the day's street.
- Checked: the Dhaka autopilot still covers ~1.2 km in 10 minutes with ropes and clusters on;
  the first four minutes are a start-of-day queue at the first junction either way.
- Found and fixed on the way: `.gitignore`'s Unity `*.csproj` rule had swallowed
  `sandbox/TwentyTons.Sandbox.csproj`, so a fresh clone could not run `make sandbox`.

**Step 16 — crews remember, cloud setup, a brief for testers** (done)

- Jamal ended day 1 with "Tomorrow I won't let you" and woke up with no memory: `Reset` built new
  brains. Now `Household.Crews` keeps each named crew's grudge with the day it was stored, like the
  bus's wear; `TrafficSim.RememberCrews` writes it before the day's world is thrown away and
  `RecallCrews` gives it back, cooled by `Memory.GrudgeDecayPerShift` (1) for every night since,
  rest and yard days included, never past zero; trust cools the same way. R mid-day keeps it, as it
  keeps the bus. Four tests in `CrewMemoryTests`, 93 in all.
- Taken from a parallel session's branch (`claude/wizardly-sagan-dipemu`): the session start hook
  that installs Mono in cloud containers; extended to install the .NET 8 SDK too, so the sandbox
  publishes without setup. Its other finding (the ignored project file) was already fixed here.
- The tuning panel starts hidden; T (or the phone's button) opens it. It is an instrument, not the game.
- `docs/TESTING.md`: how a separate session tests what is on `main` without Unity, what to look at,
  how to report, and what not to push.
- Phones: the HUD covered the road, so it now starts folded to the speed figure; a tap on it or the
  hud button opens it. The door button no longer overlaps the steering pad at 390px.
- The picture was mirrored: the core uses Unity's left-handed axes and three.js is right-handed, so
  the bus drove on the right with oncoming traffic on its left, and a right turn looked like a left
  turn. The page now draws everything in a group mirrored in x and negates the camera's x. Checked
  top-down: the kerb and the stands are on the left, a right turn goes right. Unity will not need this.
- "It keeps getting stuck" (phone): the bus was held at a constable's rope drawn one pixel thick.
  The rope is now a fat bright bar with a constable at each end and the cane is thick enough to read
  from the top-down view. New metrics `CaneWaitSeconds` / `RopeHeldSeconds` in the headless report:
  the Dhaka driver spends ~70 s of a 600 s run stopped at closed canes, most of it at ropes.

**Step 17 — a yield is a swerve, not a jump; the standoffs it uncovered** (done)

- Asked: a horn made nearby cars flick sideways by a few degrees. Two causes: a vehicle could
  "drift" sideways at 0.3 m/s while standing still, and its nose angle was computed straight from
  that sideways speed, so it snapped ~17° the instant a yield began and snapped back when it ended;
  contact resolution also shoved the lighter vehicle by the whole overlap in one frame. Now sideways
  speed is a state (`Agent.LateralVelocity`) that builds and dies under a lateral acceleration limit,
  is tied to forward speed by a crab ratio (full lock at a crawl, ~20° at speed), the nose follows it,
  and shoves are rate-limited and never push anyone off the road (the NPC gives way on a mass tie).
- Driven, not slid, a blocked car must angle out while it still has room: drivers now look for a
  freer band when something slow is within 3 s ahead, roll into it at a crawl (`AngleOutMs`), and
  close on slower traffic under a comfortable-braking cap instead of the headway rule alone.
- That honesty exposed four standoffs that sliding had hidden, each fixed by a rule the street has:
  people cross in front of a vehicle that has stopped for them (`StoppedForMeMetres`); a vehicle boxed
  in behind something standing cannot pull away, so people weave past it (`Agent.HeldAhead`); people
  standing on the kerb are not obstacles to a bus hugging it; and nobody closes on a person in their
  path the way they close on a car (`ClearanceAheadMetres`, the autopilots keep 6 m when standing).
- And one new way to kill someone: a bus swinging wide on a bend clipped people waiting with a
  shoulder at the kerb line. People now step back from a vehicle about to overhang the kerb and
  return when it has passed (`FlinchBackMetres`). Six careful days: 0 hits, 1.2–2.1 km, two in profit.
- Tests: `SwerveTests` (5), 98 in all. Headless days are the instrument for all of this; the
  `Dump` and junction/cross-street prints grew to make the chains readable.

**Step 18 — the first junction locked: blocking the box** (done)

- Asked: "I keep getting stuck at the same cross." A scripted hand on the wheel in the browser build
  showed it, and a new `--watch S0 S1` option in the headless runner showed why: the queue before the
  first junction did not move even with the cane open. One cross-street rickshaw sat inside the main
  road's box. The second carriageway's cross stop line lies inside the first carriageway's box (the
  median is narrower than the setback), so a rickshaw let into the first box stopped there for the
  second's cane; the main queue waited for it (a full box is physics); the oncoming stream never gave
  it a gap. Everyone sat, every lap, at the same junction.
- The street's rule: a cross vehicle that is through the first box is committed; it crosses the second
  whatever the cane says, no rope holds it, and the second carriageway's traffic stops at its line
  while a committed vehicle waits between the boxes (`Junction.Partner`, `CrossCommitted`). Test in
  `OncomingTests`; 99 tests. With the cane open the stretch before the junction now drains from 27
  vehicles to 10 within a phase.
- The one hit left in six careful days was a bus easing into a stand at 4 km/h, its flank pushing a
  person standing beside it; the rule counted any contact at over 0.5 m/s as a death. Now the nose
  at speed kills, a flank moving at someone fast or passing at speed kills, and a flank at a crawl
  shoves them clear (`Metrics.Brushes`). Six careful days: 0 hits, three in profit. 100 tests.
- Also learned on the way: the officer's first phase was already random, so a restart does not start
  closed; the long waits that remain are the real ones (a 20–90 s phase, a rope, the start-of-day
  column) and the stand at Block 11 just before the junction, where a bus loading for 25 s blocks the
  kerb band for everyone behind it who will not overtake.

**Step 19 — the first test report, worked through** (done)

- A separate test session wrote `docs/test-reports/2026-10-03_0535.md` (twelve findings, three pinned
  as `[Explicit]` tests) and a rule in `CLAUDE.md`: read every open report at the start of a session
  and mark it when dealt with. Dealt with here; the three pinned tests now pass and guard.
- Fixed: the rope pulled a vehicle already in the box back onto the line (now it pins only a nose at
  the line); the helper shouted "go, go" at a bus on its side or under a sergeant's hand; the end-of-
  day card showed the first day's ledger forever (keyed on the clock, every shift ends at 20:00; now
  keyed on the day); the autopilots never answered a rollover (they pay the ropes; the batch table has
  a rolls column); a crew said "today" about a grudge carried from earlier days (`terminal-carried`);
  the rope hold pulsed as the bus slowed (sticky until the cane turns); keyboard hints on touch
  screens; the `street` HUD row explained what the pole and the conductor already say (removed).
- The Dhaka driver now takes the wrong side when there is room: standing oncoming traffic counts as
  a wall only within 25 m. That exposed that its person check ignored people on the median, which it
  then drove through. Fixed: kerb and median people count when the bus is leaving the road on their side.
- People, like paint: a touch below walking pace or a graze of a few centimetres is a shout
  (`Metrics.Brushes`, once per person per second), a real impact kills. Before this, a bus creeping
  at 0.8 m/s into someone frozen at its nose, or a one-centimetre flank graze at 3 m/s, ended the day.
- Decided, not changed: the HUD's counters stay (the sandbox HUD is an instrument, not the game's
  UI); a parked bus still collects paid scrapes (that is Dhaka); the helper's seat out-earning both
  autopilots goes to the tuning pass.

**Step 20 — the bus, researched** (done)

- Asked: was the bus itself researched (engine, fuel, how fast it speeds up and slows)? It was not;
  its numbers were placeholders. `docs/BUS.md` now has it, with sources: the Hino AK1J is the common
  Dhaka chassis (7.96 l J08C diesel, 210 PS / 155 kW, 554 Nm, 14.2 t design weight) with a local
  steel body; Tata 1618 and Leyland Viking are the others; 36 seats local, 52–58 counter, standing
  unlimited in practice; diesel, Tk 135 since 21 Sep 2026 (CNG buses declared diesel again); 40 km/h
  city limit since the 2024 guideline; one bus in four without fitness; brakes fail from worn
  linings and from no air after a cold start (the Padma bus). Acceleration: a laden bus does 0–50 in
  14 s, sudden starts average 2.0 m/s², standing passengers fall above 2.0. Braking: 1.2–3 m/s² in
  service, ~5 in the R13 test, peak 0.35 s after the pedal.
- In the sim: 155 kW, 1.8 m/s² cap, 65 kg a passenger (twenty tons with ninety aboard), rolling
  0.15, drag 0.0003, steering 45°/s. New: `BrakeLagSeconds` (the drums follow the pedal in 0.35 s)
  and `AirPressure` (the day starts at 0.4, the compressor fills in 45 s, each application spends
  0.03, braking scales with it). The first stop after a cold start is soft; pumping in a jam empties
  the tanks. The rope hold and the autopilots reckon stopping distance with both. `BusTests` (3).
- First try charged air per second held, so the autopilots, standing on the pedal in queues, ran
  out of brakes and coasted into people (4 hits in 6 days). A held application costs nothing more;
  per application it is.
- Found underneath: the sandbox ring's second arc was drawn from the wrong centre, so the road jumped
  200 m sideways and closed with an 81° kink at the School stand, where every autopilot left the road
  and hit the kerb crowd, and which was the zigzag in the phone screenshot. Fixed: the ring is the
  rounded rectangle it was meant to be (1,588 m, not 1,949).
- Six days: no hits, 1.8–2.5 km, careful in profit on all three seeds tried.

**Step 21 — the second test report, worked through (findings 1–4)** (done)

- `docs/test-reports/2026-10-03_0817.md`: the retest closed ten of the first report's eleven fixes
  and found four more things, plus a finding 0 relayed as the owner's decision (polite driving must
  not win; batch acceptance stated). Findings 1–4 are dealt with here; finding 0 is the tuning pass,
  which waits for the owner's word in this chat before the economics move.
- The day card went stale after R on the card (same day number): keyed on day plus the restart count.
- The Dhaka driver never took the wrong side. Two causes: its check wanted 70 m of nothing on the
  oncoming side (never true with 35 vehicles on the loop), and the bus controller's off-road rule
  only knew the main road's edges, so the other carriageway was "market stalls" and the drag pinned
  the bus there at a standstill. Now: heavy traffic within the look, light traffic coming within
  35 m, or anything standing within 20 m says no; the far edge of the road is the oncoming
  carriageway's outer edge. It crosses when there is room (73 s on one seed), and comes back.
- The Dhaka driver rolled the bus on four days in six: its steering asked for full lock at speed.
  The autopilots now cap lock so the lateral acceleration stays at 0.6 of what tips the bus; a
  driver who has done the route for years does not tip it. No rollovers in the days since.
- Underneath, two older rules showed as wrong once the wrong side was real: the head-on test had
  passed only because the off-road drag stopped the bus, and on contact the "behind cannot go faster
  than in front" rule handed the truck the ghost's negative speed. Head-on contacts now resolve along
  the road only, the lighter vehicle is shoved by the mass ratio, and the bus keeps the share of its
  speed the mass ratio allows: a truck stops it, a rickshaw slows it. 106 tests.

**Step 22 — the tuning pass: the trap holds at the mean, not yet on every seed** (in progress)

- The owner confirmed finding 0 of test report 0817 in the build session: polite driving must not
  win. `docs/PLAYTEST.md` has the full account. In short: half the batch days were standoffs that
  froze a bus and the street behind it (five patterns, all fixed with "nobody waits forever" rules:
  geometric box check, braking-limited stop-line approach and back-off, crossers round standing
  vehicles, nose-beside rules, alongside rule, creep); the Dhaka driver's scrapes were the controller
  and the ring's eight-chord arcs (stopping-distance following, a mirror check, 32 chords, scrapes
  priced by how the two met); and the market was wrong (other companies' buses now take crowds,
  crowds at 3 a minute, traffic 25 around the player).
- Dhaka-only moves that stayed: door open as it rolls in and out with the helper holding the step at
  walking pace, canes respected on a drive day, the median crossed only where it is bare. Moves tried
  and dropped: leaving a nearly bare kerb for the chaser, passing a kerb a crew bus already loads.
- Result on `main`: Dhaka Tk +112 a day over careful at crowd rate 1.0 and +120 at 0.5, nobody hit,
  no rollovers, every day run to the end; Dhaka ahead on 4 of 6 seeds at each rate. The acceptance
  (5 of 6) is not yet met; the two losing seeds are second-door days at the Stand. 109 tests.
- Owner decision recorded in README: the game is played from the driver's seat (wheel, mirrors,
  the saloon in the interior mirror); on foot only when the body leaves the bus.

**Step 23 — the owner drives: no door, and a bus that does not tip** (done)

- "It rolls over far too often. Real buses take much steeper manoeuvres and never roll unless
  something extremely dangerous happens." The bicycle model had no tyres: the lock asked for any
  lateral acceleration and got it, and one frame above 6 m/s² at 29 km/h tipped twenty tons. Now
  the tyres hold 0.7 g and past that the front scrubs and the bus runs wide; an untripped rollover
  needs the lateral acceleration held above 6.5 m/s² for 0.8 s at 36 km/h or more; the tripped one
  (kerb, railing, asleep) stays. `docs/BUS.md` §5 has the static-stability figures. Two tests.
- "Have you seen the buses they have? They don't have doors at all, or they are always open so the
  passengers just jump in, jump out." The doorway is open all day: people get on whenever a bus is
  at a crawl (under `DoorSpeedMs`), get off up to the jump speed, at any company's bus, and "first
  door" means the first bus to arrive slow at the kerb. `SetDoor` is a no-op kept for old callers;
  the E key is the helper's call in the helper role; the HUD shows who is on the step. The careful
  autopilot stops when someone steps onto a moving bus, the Dhaka one holds walking pace with the
  helper on them. `docs/BUS.md` §6. The first batch without a door scooped people up at 20 km/h and
  dropped them (four Dhaka days ended by the second injury): boarding is a crawl-only thing now.
- Batch on this commit: at crowd rate 1.0 the acceptance holds (Dhaka ahead on 5 of 6 seeds, Tk +147 a
  day over careful, nobody hit, no rollovers); at 0.5 it does not (careful ahead on 4 of 6, Tk +82).
  `docs/PLAYTEST.md`. Next: the owner's approved getting-off and fare design (report 1131). 110 tests.

**Step 24 — people get out of the way; a hit under 30 km/h knocks down, not kills** (done)

- Owner: "too easy to kill someone; humans are not so unaware of a bus; at 20-40 km/h they see me
  and move, especially when I horn." Crossers now run (3.5 m/s) for the nearer edge of a vehicle's
  strip when it will reach them within 2.5 s, and a horn in their direction sets them running;
  running beats waiting for the next lane. A nose hit between 11 and 29 km/h knocks the person
  down: carried to the kerb, the crowd, Tk 8,000 (scaled) on the spot, a long hold, and it counts
  toward the second injury that ends the day; 29 km/h and up at the nose, or dragged along the
  flank, kills and ends the day as before. `docs/BUS.md` §7. Headless batch has a `down` column.
  `PedestrianDanger` tests (3). 113 tests.
- Batch with people running: at crowd rate 1.0 careful +482, Dhaka +587 (+105, ahead on 4 of 6), at 0.5
  careful +164, Dhaka +50; nobody knocked down, nobody hit, on either side, in 24 days. The autopilots
  never needed the new rules; a human at the wheel does.
- Then the owner's next two: "I should be able to push someone at slow speed, not kill" (the nose
  under 11 km/h shoves, see above) and "push lighter cars, rickshaws, bikes: what's the point of
  twenty tons otherwise". Nose to tail the two now share momentum by mass: a rickshaw is shoved along
  at the bus's pace and the bus barely slows; into a truck the bus is the one that slows. The old rule
  held the one behind to the one in front's speed whatever the masses. `MassTests` (2). 115 tests.
- Batch with the momentum rule: at crowd rate 1.0 careful +469, Dhaka +539 (+70, ahead on 5 of 6);
  at 0.5 careful +161, Dhaka +154. Nobody knocked down, nobody hit, in 24 days.

**Step 25 — the day is a trip; the eat line; the death curve** (done)

- Owner's decisions, 4 Oct: a sandbox day is charged like one trip of six (deposit, wages, food at a
  sixth; fuel and fares by the real kilometres a lap stands for; every event at its real taka), and
  the batch verdict is the eat line (careful must not clear the household's food, Dhaka must).
  `MoneyScale` is 1/6 and applies only to per-day amounts; `TripKm`, `FuelTkPerTrip`,
  `CrewWagesTkPerDay`, `CrewFoodTkPerDay`, `TrafficSim.DistanceScale`. Sergeant stops are rare now
  (once in a few trips) and crowds are 2 a minute. `docs/PLAYTEST.md` has the tables: careful +95,
  Dhaka −217 with one Tk 2,000 camera day (about +160 without it) at normal crowds; nobody eats at
  half crowds; everyone eats at 3 a minute. Careful clears the eat line by Tk 45: not met as written,
  the small honest levers are listed there for the owner.
- Owner's decision 4, researched (`docs/BUS.md` §7): under 15 km/h the nose bumps and nothing is
  owed; above it the person is knocked down and a logistic in impact speed decides death (3 % at 20,
  8 % at 30, 18 % at 40, 36 % at 50 km/h, a car curve shifted 5 km/h for the flat front); on the
  pavement or median a nudge is a hit, the curve counts 10 km/h faster and the money doubles. What
  the street does after a death or a bad injury is from the Bangla press: the crew runs, the crowd
  beats them and burns the bus, police seize it. 115 tests.

**Step 26 — the pack: three buses leave together and the first at the stop takes all** (done)

- Owner's correction, 4 Oct: buses never wait for passengers; they leave the terminal as a pack of three
  on one route and the whole competition is to be first at each stop. Confirmed from inside by a Sayedabad
  ride-along (two or three released together, no timetable, the first takes everything, a pass is possible
  only while the leader loads, the leader blocks on the road); the pack wears one livery but is three
  owners' buses on a daily gate pass, so only the crews race. Four more videos read; a machine
  transcription tool for Bangla ride-alongs is in `tools/transcribe/`. All in `docs/ROUTE_AND_TRIPS.md`.
- Built: the player and the two crews spawn 18 m apart just past the stand (headless and sandbox); three
  minutes of crowd on every kerb at the start (`InitialCrowdMinutes`, `Boarding.SeedCrowds`); no fishing
  (`WaitBonus` 0); the first door takes the whole kerb (`RaceDwellSeconds` 40, boarding 1.5–4 s a head);
  a crew bus passes a loading bus (`PassLoadingBus`), leaves a kerb with two or fewer on it when a route
  bus closes from behind (`PackLeaveMetres` 30, `PackHoldCrowd` 2), and blocks an overtaking crew bus as
  trade practice (`BlockBase`); a full bus still stops for those getting off (was a bug: 70 carried past in
  one day). Dhaka autopilot: passes a loading crew bus, drops its own people on the roll, stops outside a
  loader clear of its band, looks for a free band while still rolling and creeps out when boxed in, blocks a
  faster crew bus behind. Careful autopilot: holds walking pace instead of stopping for each newcomer once
  it is pulling away (it crept 40 m through a zone stopping for everyone, two minutes a kerb).
- Metrics: `StopsContested`, `StopsFirst` (nobody else's door took anyone here in the last minute),
  `LeadMetresSum`; `BusLoad.DoorBusySeconds` and standing time; zones remember whose door last took
  someone. The batch prints riders, first-door share and lead for the player and each crew bus, and a pack
  verdict line; the verbose log shows arrive/leave per zone and a line every 10 s at a kerb; the watch
  prints why the Dhaka autopilot is on the brake. The arrival stamp clears when the bus is away at any
  speed (it kept a stale claim on the kerb it had left).
- Result (`docs/PLAYTEST.md`, "The pack"): careful is third in its pack every run, 80 riders against the
  crews' 107, with no fine, camera or knock-down in any day. The Dhaka autopilot does not lead (51 against
  123): it loses the first kerb and the first junction and never gets the lead back. The instrument, not
  the model; next is a Dhaka policy on the crews' decision layer. Report 1131 (getting off) after that.
  119 tests.
- 5 Oct: everyone leaves the depot empty (owner; `StartingPassengers` 0, was the physics sandbox's 30) and
  the dwell cap is gone so the first door really takes the whole kerb (`RaceDwellSeconds` 180). `--trace`
  prints every route bus at every kerb: door order, waiting, off, on, aboard, stood. With it: the second
  door now gets nothing; careful is a distant third (40 riders against the crews' 118); the Dhaka autopilot
  51 against 98. `docs/PLAYTEST.md`.
- 5 Oct, from the owner reading the trace: the crews spawned at cruise speed into the standing player's tail
  (now standing), and the zone-approach brake stopped the Dhaka bus for a crowd another door owned (now
  honours the pass rule). The player clears the first kerb; the day then shows the fatigue system ending an
  empty, drifting driver's day with two knock-downs. The autopilot does not manage fatigue; a player must.

**Where this leaves the project**

Everything in README milestones 3–6 now exists as engine-free C# with tests, runs in the browser
sandbox and in the headless runner, and the street's own control (the cane, the rope, the box, the
camera) is in. Not yet done: the Unity side (project settings, scene, OSM
import, real bus physics, 3D assets), the Bangla column of `VoiceLines` and the recordings, the
mirror-moment scripting across days, and the tuning pass the thesis report asks for. All numbers are placeholders until the owner
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
