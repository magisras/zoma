#!/usr/bin/env bash
# Download the route corridor (Mirpur 12 to Azimpur) from OpenStreetMap through the Overpass API.
#   ./tools/osm/fetch.sh [out.osm]
# The query is built by greybox.py from its MARKERS: roads within 600 m and buildings within 300 m
# of the line through them, not a bounding box (the box around the whole route is half of Dhaka).
# Overpass allows two slots per address; a 429 or an HTML answer means wait a minute and retry.
# The old /api/map?bbox= endpoint answers 406 now; the interpreter with a query is the way.
# Map data (c) OpenStreetMap contributors, ODbL.
set -euo pipefail
cd "$(dirname "$0")/../.."
OUT=${1:-route.osm}
QUERY=$(python3 tools/osm/greybox.py --query)
# Two public servers, three tries each: the main one answers "too busy" at times.
for TRY in 1 2 3; do
  for URL in https://overpass-api.de/api/interpreter https://overpass.kumi.systems/api/interpreter; do
    curl -sS -m 900 -A "TwentyTons-greybox/0.1" -o "$OUT" --data-urlencode "data=$QUERY" "$URL" || true
    if grep -q "<osm" "$OUT" && grep -q "</osm>" "$OUT"; then
      echo "$OUT: $(grep -c '<way' "$OUT") ways, $(du -h "$OUT" | cut -f1) from $URL"
      exit 0
    fi
    echo "$URL: $(grep -o 'Error</strong>: [^<]*' "$OUT" | head -1)"; sleep 60
  done
done
echo "no server answered, see $OUT"; exit 1
