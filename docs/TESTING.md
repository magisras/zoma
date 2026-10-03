# Testing Twenty Tons without Unity

For a session (human or Claude) whose job is to try what is on `main` and report, not to build.
Everything below runs in a fresh cloud container or on a laptop; no Unity, no GPU.

## Setup

- Clone `github.com/magisras/zoma`, branch `main`.
- In a Claude Code cloud session the start hook (`.claude/hooks/session-start.sh`) installs Mono
  and the .NET 8 SDK from Ubuntu's packages. Microsoft's download host is blocked there; nuget.org
  is reachable. On a laptop: `brew install mono` and `brew install --cask dotnet-sdk`.
- Check: `mcs --version` and `dotnet --version` both answer.

## The three instruments

1. **Unit tests**: `./tools/check.sh`. Compiles the game code against the UnityEngine stub and runs
   every NUnit test. Expect all green; the count is in `PROGRESS.md`. A red test is a report, not
   something to fix: name it and paste the assertion.
2. **Headless days**: `./tools/headless.sh <seed> <seconds> <capKmh> [--dhaka] [-v]` runs one day in
   seconds and prints the metrics, the ledger, the junction states, the crew's lines and a scene dump.
   `./tools/headless.sh --batch 6 900` runs six days each with the careful and the Dhaka driver and
   prints the thesis table (does polite driving still win?). `docs/PLAYTEST.md` holds the numbers so
   far; append yours there in the same format.
3. **The browser sandbox**: `make sandbox-serve` publishes to `sandbox/out/wwwroot` and serves it on
   port 8080. Without a display, drive it with Playwright: Chromium is preinstalled in cloud
   sessions at `/opt/pw-browsers/chromium-*/chrome-linux/chrome`; launch it with
   `--use-gl=swiftshader --enable-unsafe-swiftshader`, wait for `#loading.hidden`, then either press
   keys (`P` twice for the Dhaka autopilot) or call the C# directly from `page.evaluate`:
   `DotNet.invokeMethod('TwentyTons.Sandbox', 'Tick', 0.1, keysBitmask)` returns the frame as JSON
   (`SandboxApi.FrameDto` lists every field). Key bits: up 1, down 2, left 4, right 8, horn 16,
   autopilot 32, door 64, pay 128, refuse 256, dhaka 512, helper 1024. Collect `pageerror` and
   console errors; any is a report.

## What to look at, in order

- Does `check.sh` pass, and does `headless.sh 1 900 25` finish with a ledger and no exception?
- In the sandbox: a full day with the Dhaka autopilot (P, P) to the end-of-day card, sleep, work
  tomorrow, day 2 loads. Then the same as the helper (O). Then by hand on a phone-sized viewport
  (390×844): the touch pads, the day card's buttons, the sergeant's two buttons.
- The street: does the bus stop at a rope and leave when the cane turns? Does a blue police hut with
  a figure beside it ever stop you? From day 6, is there a camera on the first junction, and does a
  cane run there show "Camera cases" on the night's ledger?
- The crews: cut Jamal off a few times, end the day, and check his grudge on day 2 is one point
  lower than at the end of day 1 (the HUD "crews" row shows it).
- Anything that feels like the UI explaining instead of the world forcing: that is a design bug
  (CLAUDE.md: "don't explain, force it").

## Reporting

Each test session writes one new file, `docs/test-reports/YYYY-MM-DD_HHMM.md` (UTC, the time
you write it), with the header in `docs/test-reports/README.md` and `Status: open`. Never edit
an older report: say what changed in your own.

Write findings as a list, most serious first: what you did (seed, keys, time), what happened, what
you expected, with the frame JSON or the headless line that shows it. End with what worked as this
brief says, so the builder knows it was checked.

A bug you can pin in the engine-free core goes in as an NUnit test. If it fails today, mark it
`[Explicit("Known bug: ...")]` so `check.sh` stays green, and name it in the report.

Commit straight to `main`, and only these: your report file, new tests, and the thesis numbers
appended to `docs/PLAYTEST.md` when you ran the batch. Run `./tools/check.sh` first. Never change
game code, the sandbox or tools: that is the builder's job, from your report.
