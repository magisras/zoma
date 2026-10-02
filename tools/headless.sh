#!/usr/bin/env bash
# Run the sandbox world without a browser: ./tools/headless.sh [seed] [seconds] [speedCapKmh]
# Needs tools/check.sh to have built tools/.cache/build first (it does the game compile).
set -euo pipefail
cd "$(dirname "$0")/.."
OUT=tools/.cache/build
mcs -nologo -target:exe -out:"$OUT/Headless.exe" -r:"$OUT/UnityEngine.dll" -r:"$OUT/TwentyTons.dll" \
    sandbox/SandboxWorld.cs sandbox/ScriptedDriver.cs tools/headless/Program.cs
mono "$OUT/Headless.exe" "$@"
