# Twenty Tons

A serious, realistic game about a Dhaka bus crew: you don't play a hero, you play people trapped in a pay system that rewards racing and punishes caution.

- Engine: Unity (URP first, HDRP later) · Platform: Steam
- Single player (driver with AI crew) or 3-player co-op (driver, helper, conductor) on one bus
- Fictional city built from real Dhaka geography and economics; permanent consequences; no respawns

## Repository contents

- `CLAUDE.md` — the working brief for each coding session (principle, stack, rules)
- `PROGRESS.md` — what each session shipped and what comes next
- `Assets/` — the Unity 6 (URP) project; every gameplay number lives in `Assets/Scripts/Tuning/TuningTable.cs`
- `docs/OSM_IMPORT_PLAN.md` — how the Mirpur 12–Azimpur corridor goes from OpenStreetMap to grey boxes
- `RESEARCH.md` — the research backbone: pay system (zoma), fares, crew roles, sleep and health, bus costs, accidents, bribes and extortion, driving culture, passenger behaviour, crew testimony, sources, game mechanics derived from the research, traffic/character AI method, tech plan and prototype milestones.

## Prototype milestones

1. Export 2–3 km of the Mirpur 12–Azimpur corridor to Unity as grey boxes (OpenStreetMap → Blender blosm → Unity)
2. Drivable heavy bus with worn brakes and passenger-load mass
3. Custom lane-free traffic: corridors, mass/nerve yielding, horn event, simple cars and rickshaws
4. Two rival buses, five stops, waiting passengers choosing the first bus
5. Daily ledger: fares in, zoma and fuel out, extortion points
6. Fatigue meter and one-on, one-off shift rhythm
7. Playtest: is the race for the stop tense and uncomfortable in the right way?

## Design principle

Don't explain, force it. The world enforces the rules of the street; the UI never does.
