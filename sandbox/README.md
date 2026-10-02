# Browser sandbox

The simulation core from `Assets/Scripts` compiled to WebAssembly and drawn as grey boxes with
three.js, so driving logic and NPC maths can be tried without Unity. Nothing is duplicated: the
project includes the game's C# files by path, plus the UnityEngine stub from `tools/unity-stubs`.

## Run it

```
cd sandbox
dotnet run            # prints a http://localhost:PORT, open it
```

Needs the .NET 8 SDK (`brew install --cask dotnet-sdk` on the Mac). First run downloads a few
packages from nuget.org.

Or publish a static site and serve it with anything:

```
dotnet publish -c Release -o out
python3 -m http.server -d out/wwwroot 8080      # then http://localhost:8080
```

## Controls

W/↑ throttle · S/↓ brake · A/D or ←/→ steer · H or space horn (hold for a blast) · E door ·
1/2 pay or refuse the sergeant · C camera · T tuning panel · P autopilot (the careful driver) ·
O be the helper (the ostad drives; E calls the stop, W hurry, S easy) · R restart with a new seed.
On phones: on-screen buttons, and the HUD starts folded to the speed; tap it (or the hud button) to open it.

A day lasts 15 minutes (slider). At the end: the ledger, where to sleep, work or rest tomorrow.

## Headless

`./tools/headless.sh 1 150 25` runs the same world for 150 s with the careful driver capped at
25 km/h and prints the metrics. Change the seed for a different day.

## What to look at

- Headway in seconds, colour-coded. Dhaka drivers hold 0.5–1.5 s; Western sims 4–6 s.
- Pale blue vehicles are yielding; red ones are bluffing ("I'm not moving"); a flash is a horn.
- Pedestrians (tall thin boxes) step out in front of cars with a hand up and hesitate for buses.
  Hit one and the day is over.
- Officers at the two cross streets: green cane along the road = you may go. Nobody stops you
  running it; cross traffic in the box does. Unless the constable has the rope out across your
  approach: then nobody leaks, and your bus brakes for it whatever you press. The signal pole
  beside him is dark, as at Mirpur 10; from day 6 a camera appears on the first junction and a
  cane run there is an SMS to the owner, on your ledger that night.
- Police boxes (blue huts on the pavement): a sergeant standing beside one is on duty today and may
  step out for your papers. On a drive day (the conductor says so) he stops more and asks more.
- Zones: tall thin boxes waiting on the kerb; stop slow, open the door (E), they board one by one.
  The two darker-gold buses are your own company's crews; the HUD shows what they are doing.
- The sergeant after a cane run, the wrong side or at a box: pay, or take the case and sit.
  Refuse with no papers and the bus can go to the dumping yard: the day ends and days pass without
  a bus. Fatigue arrives late in the day as slow hands, a narrowing view and the screen going dark
  for a moment.
- Subtitles are the crew. They are the manual.
- The tuning panel edits the live `TuningTable` numbers. "Feels random" means nerve too high or
  gaps too small; "polite driving still wins" means rivals too timid (RESEARCH.md, Tuning).

Three.js is MIT licensed; see `wwwroot/vendor/three-LICENSE.txt`.
