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

W/↑ throttle · S/↓ brake · A/D or ←/→ steer · H or space horn (hold for a blast) · C camera ·
T tuning panel · R restart with a new seed.

## What to look at

- Headway in seconds, colour-coded. Dhaka drivers hold 0.5–1.5 s; Western sims 4–6 s.
- Pale blue vehicles are yielding; red ones are bluffing ("I'm not moving"); a flash is a horn.
- Pedestrians (tall thin boxes) step out in front of cars with a hand up and hesitate for buses.
  Hit one and the day is over.
- The tuning panel edits the live `TuningTable` numbers. "Feels random" means nerve too high or
  gaps too small; "polite driving still wins" means rivals too timid (RESEARCH.md, Tuning).

Three.js is MIT licensed; see `wwwroot/vendor/three-LICENSE.txt`.
