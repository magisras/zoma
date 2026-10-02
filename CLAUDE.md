# Twenty Tons

A serious, realistic Unity game about a Dhaka bus crew. You don't play a hero: you play people trapped
in a pay system that rewards racing and punishes caution.

**Read `README.md` and `RESEARCH.md` first.** They are the source of truth for design, economics and
the AI method. `PROGRESS.md` says where the last session stopped; read it before touching anything.

## Design principle

**Don't explain, force it.** The world enforces the street's rules; the UI never does. No tutorial
pop-ups, no "report rival" button, no score for lane discipline. If a rule matters, the road, the
crew and the ledger make the player feel it.

## Stack and targets

- Unity 6 LTS, URP, C#. Target: Steam, PC.
- Dev machines: MacBook Air M3 16 GB and Lenovo Slim 5. Keep scenes light (grey boxes, few draw
  calls, no terrain system, no HDRP yet). Avoid heavy editor features (no Enlighten bakes, no large
  Addressables builds, no 8K textures).
- Prototype first on current laptops; upgrade hardware only once the loop proves itself.

## How to work

- One milestone per session, in the order listed in `README.md`. Don't start the next one.
- Every gameplay number lives in the `TuningTable` ScriptableObject (`Assets/Scripts/Tuning/`).
  No magic numbers in behaviour code; add a field to the table instead, with a tooltip that says
  which `RESEARCH.md` line it comes from.
- Two AI layers, kept separate: *steering* (how a vehicle moves) and *decision* (what it wants).
  A bug in one must never break the other. No machine learning.
- Code is simple and well commented; the owner reads it to learn. Prefer a clear 20-line method to a
  clever 5-line one. Explain *why*, not *what*.
- **Ask before adding paid assets.** Free alternatives first (Vehicle Physics Pro Community Edition,
  Unity Splines, NavMesh, blosm base version).
- OpenStreetMap data is ODbL: keep "© OpenStreetMap contributors" in the credits and in
  `docs/OSM_IMPORT_PLAN.md`.
- Run `./tools/check.sh` before every commit that touches C#. It compiles `Assets/Scripts` and
  `Assets/Tests` with Mono against a UnityEngine stub and runs the tests with NUnitLite, so logic is
  verified without opening Unity. Keep simulation logic free of UnityEngine types where you can
  (pure C# in `Assets/Scripts/Core/`), so it stays testable this way; Unity-only code (MonoBehaviours,
  physics, rendering) is thin and lives beside it.
- The browser sandbox (`sandbox/`, `make sandbox`) compiles the same `Assets/Scripts` to WebAssembly
  and draws grey boxes with three.js. Use it to try driving and NPC logic without Unity. It must
  keep building: if a script needs a UnityEngine type the stub lacks, add the type to
  `tools/unity-stubs/`, never a `#if` in game code.
- At the end of every session: update `PROGRESS.md` (what shipped, what's next, open questions),
  commit, push. Summarise the same in the final message.

## Layout

```
Assets/
  Scripts/      C# by feature (Tuning/, later Core/ for engine-free simulation, Unity/ for adapters)
  Scenes/       one scene per corridor chunk or test bed
  Prefabs/      vehicles, props, grey-box blocks
  Data/         ScriptableObject instances (TuningTable.asset lives here)
  Tests/        EditMode tests (NUnit via Unity Test Framework)
docs/           plans and notes that are not research (OSM import, scene budget)
sandbox/        Blazor WebAssembly + three.js page that runs the core in a browser
tools/          check.sh (compile + test without Unity), UnityEngine stub, NUnitLite entry point
```

## Naming

- **corridor** = a road centreline + width that vehicles follow lane-free.
- **nerve** = a 0–1 number per driver; high nerve accepts a smaller gap and calls bluffs.
- **zoma** = the owner's fixed daily deposit. It never moves.
- **gap** = seconds between buses of the same company; "mind the gap" is the helper's job.
