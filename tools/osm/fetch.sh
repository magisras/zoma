#!/usr/bin/env bash
# Download the Mirpur 12 -> Kazipara box from OpenStreetMap through the Overpass API.
#   ./tools/osm/fetch.sh [out.osm]
# The box is docs/OSM_IMPORT_PLAN.md section 1: south 23.796, west 90.356, north 23.832, east 90.378.
# Overpass allows two slots per address; a 429 or an HTML answer means wait a minute and retry.
# The old /api/map?bbox= endpoint answers 406 now; the interpreter with a query is the way.
# Map data (c) OpenStreetMap contributors, ODbL.
set -euo pipefail
OUT=${1:-mirpur.osm}
QUERY='[out:xml][timeout:300];(way["highway"](23.796,90.356,23.832,90.378);way["building"](23.796,90.356,23.832,90.378);way["railway"](23.796,90.356,23.832,90.378););(._;>;);out body;'
curl -sS -m 600 -A "TwentyTons-greybox/0.1" -o "$OUT" --data-urlencode "data=$QUERY" https://overpass-api.de/api/interpreter
if ! grep -q "<osm" "$OUT"; then echo "not an OSM answer, see $OUT"; exit 1; fi
echo "$OUT: $(grep -c '<way' "$OUT") ways"
