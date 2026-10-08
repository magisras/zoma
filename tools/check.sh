#!/usr/bin/env bash
# Compile every script under Assets/ against the UnityEngine stub and run the EditMode tests with
# NUnitLite under Mono. No Unity needed. Used in cloud sessions and usable on a laptop:
#   brew install mono        (Mac)      sudo apt install mono-mcs mono-runtime   (Linux)
#   ./tools/check.sh
# NUnit is fetched from nuget.org on first run into tools/.cache (gitignored).
set -euo pipefail
cd "$(dirname "$0")/.."

CACHE=tools/.cache
OUT=$CACHE/build
NUNIT_VER=3.14.0
mkdir -p "$OUT"

fetch_nuget() {  # name version -> extracts into $CACHE/<name>
  local name=$1 ver=$2 dir=$CACHE/$1
  if [ ! -d "$dir" ]; then
    echo "fetching $name $ver from nuget.org"
    curl -sSL -o "$CACHE/$name.zip" "https://api.nuget.org/v3-flatcontainer/$name/$ver/$name.$ver.nupkg"
    python3 -c "import zipfile,sys; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])" "$CACHE/$name.zip" "$dir"
    rm "$CACHE/$name.zip"
  fi
}
fetch_nuget nunit $NUNIT_VER
fetch_nuget nunitlite $NUNIT_VER
NUNIT_DLL=$CACHE/nunit/lib/net45/nunit.framework.dll
NUNITLITE_DLL=$CACHE/nunitlite/lib/net45/nunitlite.dll

# 1. The stub that stands in for UnityEngine.
mcs -nologo -target:library -out:"$OUT/UnityEngine.dll" tools/unity-stubs/*.cs

# 2. Game code: everything under Assets/Scripts (Unity compiles the same files via the asmdef).
# File lists are built with a while-read loop, not mapfile: macOS ships bash 3.2, which lacks it.
# Assets/Scripts/Unity holds the MonoBehaviour adapters: Unity-only, so neither this build nor the sandbox takes them.
GAME=()
while IFS= read -r f; do GAME+=("$f"); done < <(find Assets/Scripts -name '*.cs' -not -path 'Assets/Scripts/Unity/*' | sort)
mcs -nologo -warn:4 -target:library -out:"$OUT/TwentyTons.dll" -r:"$OUT/UnityEngine.dll" "${GAME[@]}"

# 3. Tests, built as a console exe with NUnitLite as the runner (no NUnit engine needed).
TESTS=()
while IFS= read -r f; do TESTS+=("$f"); done < <(find Assets/Tests -name '*.cs' | sort)
mcs -nologo -warn:4 -target:exe -out:"$OUT/TwentyTons.Tests.exe" \
    -r:"$OUT/UnityEngine.dll" -r:"$OUT/TwentyTons.dll" -r:"$NUNIT_DLL" -r:"$NUNITLITE_DLL" \
    "${TESTS[@]}" tools/nunitlite/Program.cs
cp "$NUNIT_DLL" "$NUNITLITE_DLL" "$OUT/"

# 4. Run. Exit code is the number of failed tests, so the script fails when a test does.
mono "$OUT/TwentyTons.Tests.exe" --noresult --noheader "$@"
