#!/usr/bin/env python3
"""OpenStreetMap extract -> grey-box meshes and road centrelines for Unity (README milestone 1).

Replaces the Blender/blosm route in docs/OSM_IMPORT_PLAN.md with something repeatable that runs
from a terminal in a few seconds. Standard library only.

    python3 tools/osm/greybox.py mirpur.osm Assets/World/Corridor01

Reads the .osm XML (download: tools/osm/fetch.sh) and writes into the output folder:
    Roads.obj         flat ribbons, one object per 250 m grid cell, at y = 0
    Buildings.obj     extruded footprints, one object per 250 m grid cell, roofs at 3 m a level
    Rail.obj          the MRT Line 6 viaduct along the main road: a deck 12 m up, piers in the median
    Markings.obj      lane lines: solid edges, dashed lane lines, a double line down the middle
    Kerbs.obj         raised pavements beside the main roads and the median barrier under the piers
    centrelines.json  every drivable road as a polyline in metres, with class, width, name and the
                      junction nodes it shares: the input for the traffic corridors (milestone 3)
    markers.json      the named points of the route (stands, junctions) in local metres
    route.json        the main road from the stand to the last marker as one polyline: the shortest
                      path over primary ways, with the kerb-to-kerb width; the bus's first corridor

Coordinates: local metres, origin at the Mirpur 12 stand, x east, y up, z north. OBJ files are
right-handed and Unity mirrors x on import, so the files store -x; in Unity x is east again.
Faces are wound counter-clockwise seen from outside in the x-z shoelace sense, which Unity
turns into outward normals after its mirror.
Map data (c) OpenStreetMap contributors, ODbL. Keep the credit in the game.
"""
import json
import math
import os
import sys
import xml.etree.ElementTree as ET

# ---------------------------------------------------------------- what to keep and how wide

# Origin of the local frame: the Mirpur 12 bus stand (docs/OSM_IMPORT_PLAN.md, section 1).
ORIGIN_LAT, ORIGIN_LON = 23.828, 90.364

# Road classes the bus or the traffic can use, and a kerb-to-kerb width in metres when OSM
# gives none. Rokeya Sarani (primary) is two carriageways of three lanes with the metro piers
# in the median; the number is for the whole road surface, median included.
ROAD_WIDTH = {
    "primary": 24.0, "primary_link": 10.0,
    "secondary": 16.0, "secondary_link": 8.0,
    "tertiary": 12.0, "tertiary_link": 8.0,
    "residential": 7.0, "unclassified": 7.0, "living_street": 5.0,
}
LANE_WIDTH = 3.3            # when OSM has a lanes= tag and no width=
LEVEL_HEIGHT = 3.0          # metres per storey (blosm's default too)
DEFAULT_LEVELS = 2          # Mirpur blocks without a levels tag: low concrete houses
CELL = 250.0                # metres; one mesh per cell so Unity can cull and later stream

# The route's named points (docs/OSM_IMPORT_PLAN.md, section 1): read off the map, "about here".
MARKERS = [
    ("Mirpur 12 stand", 23.828, 90.364),
    ("Mirpur 11", 23.819, 90.366),
    ("Mirpur 10 roundabout", 23.807, 90.368),
    ("Kazipara", 23.799, 90.371),
]

# Rail: the viaduct deck and its piers.
RAIL_DECK_HEIGHT = 12.0
RAIL_DECK_WIDTH = 10.0
RAIL_DECK_THICKNESS = 2.0
RAIL_PIER_EVERY = 30.0
RAIL_PIER_SIZE = 2.0


# ---------------------------------------------------------------- geometry helpers

def to_local(lat, lon):
    """Equirectangular projection around the origin: good to centimetres over a few km."""
    x = (lon - ORIGIN_LON) * 111320.0 * math.cos(math.radians(ORIGIN_LAT))
    z = (lat - ORIGIN_LAT) * 110574.0
    return x, z


def polygon_area(points):
    """Signed area (shoelace); positive when counter-clockwise in the x-z plane seen from above."""
    a = 0.0
    for i in range(len(points)):
        x1, z1 = points[i]
        x2, z2 = points[(i + 1) % len(points)]
        a += x1 * z2 - x2 * z1
    return a / 2.0


def ear_clip(points):
    """Triangulate a simple polygon (list of (x, z)) into index triples. Ear clipping, O(n^2):
    footprints have a dozen corners, so speed does not matter; robustness to slightly odd
    footprints does, hence the fallback to a fan when no ear is found."""
    n = len(points)
    if n < 3:
        return []
    idx = list(range(n))
    if polygon_area(points) < 0:
        idx.reverse()
    tris = []

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    def inside(p, a, b, c):
        return cross(a, b, p) >= 0 and cross(b, c, p) >= 0 and cross(c, a, p) >= 0

    guard = 0
    while len(idx) > 3 and guard < 10 * n:
        guard += 1
        found = False
        for i in range(len(idx)):
            i0, i1, i2 = idx[i - 1], idx[i], idx[(i + 1) % len(idx)]
            a, b, c = points[i0], points[i1], points[i2]
            if cross(a, b, c) <= 1e-9:
                continue                      # reflex corner, not an ear
            if any(inside(points[j], a, b, c) for j in idx if j not in (i0, i1, i2)):
                continue
            tris.append((i0, i1, i2))
            del idx[i]
            found = True
            break
        if not found:
            break                             # degenerate polygon: fan what is left
    if len(idx) >= 3:
        for i in range(1, len(idx) - 1):
            tris.append((idx[0], idx[i], idx[i + 1]))
    return tris


def ribbon(points, width):
    """A flat strip along a polyline with mitred joints. Returns (vertices, quads) where each
    vertex is (x, y, z) and each quad is four vertex indices, counter-clockwise from above."""
    half = width / 2.0
    pts = [p for i, p in enumerate(points) if i == 0 or (p[0] - points[i - 1][0]) ** 2 + (p[1] - points[i - 1][1]) ** 2 > 0.01]
    if len(pts) < 2:
        return [], []
    verts = []
    for i, (x, z) in enumerate(pts):
        # Direction at this point: the segment, or the mean of the two at a joint.
        if i == 0:
            dx, dz = pts[1][0] - x, pts[1][1] - z
        elif i == len(pts) - 1:
            dx, dz = x - pts[i - 1][0], z - pts[i - 1][1]
        else:
            d1 = (x - pts[i - 1][0], z - pts[i - 1][1])
            d2 = (pts[i + 1][0] - x, pts[i + 1][1] - z)
            l1 = math.hypot(*d1) or 1.0
            l2 = math.hypot(*d2) or 1.0
            dx, dz = d1[0] / l1 + d2[0] / l2, d1[1] / l1 + d2[1] / l2
        length = math.hypot(dx, dz) or 1.0
        nx, nz = -dz / length, dx / length          # left-hand normal in the x-z plane
        # A mitre at a sharp bend stretches to infinity; cap the extension at 1.5x the half width.
        if 0 < i < len(pts) - 1:
            d1 = (x - pts[i - 1][0], z - pts[i - 1][1])
            l1 = math.hypot(*d1) or 1.0
            cos_half = abs(nx * (-d1[1] / l1) + nz * (d1[0] / l1))
            scale = min(1.5, 1.0 / max(cos_half, 1e-3))
        else:
            scale = 1.0
        verts.append((x + nx * half * scale, 0.0, z + nz * half * scale))
        verts.append((x - nx * half * scale, 0.0, z - nz * half * scale))
    quads = []
    for i in range(len(pts) - 1):
        l0, r0, l1, r1 = 2 * i, 2 * i + 1, 2 * i + 2, 2 * i + 3
        quads.append((r0, r1, l1, l0))
    return verts, quads


def offset_ribbon(points, offset, width, y=0.0):
    """A ribbon whose centre runs `offset` metres to the left (+) or right (-) of the polyline,
    at height y. Built from the plain ribbon by sliding its two edges."""
    verts, quads = ribbon(points, 2.0)         # unit-ish ribbon: its edges give the normal direction
    out = []
    for i in range(0, len(verts), 2):
        lx, _, lz = verts[i]
        rx, _, rz = verts[i + 1]
        cx, cz = (lx + rx) / 2.0, (lz + rz) / 2.0
        nx, nz = (lx - rx) / 2.0, (lz - rz) / 2.0        # left-hand unit normal (ribbon was 2 m wide)
        out.append((cx + nx * (offset + width / 2.0), y, cz + nz * (offset + width / 2.0)))
        out.append((cx + nx * (offset - width / 2.0), y, cz + nz * (offset - width / 2.0)))
    return out, quads


def box_ribbon(points, offset, width, height):
    """A raised strip: a ribbon's top at `height` plus its two long sides down to the ground."""
    top, quads = offset_ribbon(points, offset, width, height)
    base = [(x, 0.0, z) for x, _, z in top]
    m = len(top)
    faces = list(quads)
    for i in range(0, m - 2, 2):
        faces.append((i, m + i, m + i + 2, i + 2))             # left side, outward
        faces.append((i + 3, m + i + 3, m + i + 1, i + 1))     # right side, outward
    return top + base, faces


def dashed(points, dash=3.0, gap=6.0):
    """Split a polyline into short polylines: `dash` metres on, `gap` metres off."""
    pieces, piece, on, left = [], [], True, dash
    for i in range(len(points) - 1):
        (x0, z0), (x1, z1) = points[i], points[i + 1]
        seg = math.hypot(x1 - x0, z1 - z0)
        t = 0.0
        if on:
            piece.append((x0, z0))
        while seg - t > left:
            t += left
            f = t / seg
            p = (x0 + (x1 - x0) * f, z0 + (z1 - z0) * f)
            if on:
                piece.append(p)
                pieces.append(piece)
                piece = []
            else:
                piece = [p]
            on = not on
            left = dash if on else gap
        left -= seg - t
        if on and (x1, z1) != (piece[-1] if piece else None):
            piece.append((x1, z1))
    if on and len(piece) > 1:
        pieces.append(piece)
    return pieces


# ---------------------------------------------------------------- OBJ writing

class ObjWriter:
    """Collects objects (name -> faces) and writes one OBJ. Faces are counter-clockwise when
    seen from outside in the right-handed frame; x is negated on write (see the module note)."""

    def __init__(self):
        self.objects = {}      # name -> (verts, faces); faces are index tuples into verts
        self.triangles = 0

    def add(self, name, verts, faces):
        v, f = self.objects.setdefault(name, ([], []))
        base = len(v)
        v.extend(verts)
        f.extend(tuple(base + i for i in face) for face in faces)
        self.triangles += sum(len(face) - 2 for face in faces)

    def write(self, path):
        with open(path, "w") as out:
            out.write("# Twenty Tons grey box. Map data (c) OpenStreetMap contributors, ODbL.\n")
            offset = 1
            for name in sorted(self.objects):
                verts, faces = self.objects[name]
                if not faces:
                    continue
                out.write(f"o {name}\n")
                for x, y, z in verts:
                    out.write(f"v {-x:.2f} {y:.2f} {z:.2f}\n")
                for face in faces:
                    # Negating x mirrors the mesh and Unity mirrors it back on import; the winding
                    # survives both, so faces go out as built. (Reversing them here was tried first:
                    # every road faced down and every wall faced in. Checked in the editor: a mesh's
                    # calculated normals should come out "up" for roads and roofs.)
                    out.write("f " + " ".join(str(offset + i) for i in face) + "\n")
                offset += len(verts)


def cell_name(prefix, x, z):
    return f"{prefix}_{int(math.floor(x / CELL)):+d}_{int(math.floor(z / CELL)):+d}"


# ---------------------------------------------------------------- the extract

def load(path):
    nodes, ways = {}, []
    for _, el in ET.iterparse(path, events=("end",)):
        if el.tag == "node":
            nodes[el.get("id")] = (float(el.get("lat")), float(el.get("lon")))
            el.clear()
        elif el.tag == "way":
            refs = [nd.get("ref") for nd in el.findall("nd")]
            tags = {t.get("k"): t.get("v") for t in el.findall("tag")}
            ways.append((el.get("id"), refs, tags))
            el.clear()
    return nodes, ways


KERB_HEIGHT = 0.15
LINE_WIDTH = 0.15
MEDIAN_WIDTH = 3.0
MEDIAN_HEIGHT = 0.6
MARK_Y = 0.02          # paint sits just above the road so the two do not fight for the pixel


def add_markings(markings, pts, width, cls, mx, mz):
    """What a driver reads: solid edge lines, dashed lane lines three lanes a carriageway on the
    main road, a double line down the middle of a two-way street."""
    name = cell_name("Lines", mx, mz)
    half = width / 2.0
    for side in (+1, -1):
        v, f = offset_ribbon(pts, side * (half - 0.3), LINE_WIDTH, MARK_Y)
        markings.add(name, v, f)
    if cls in ("primary", "trunk"):
        # Two carriageways either side of the median; lanes of a third of each carriageway.
        lane = (half - MEDIAN_WIDTH / 2.0) / 3.0
        for side in (+1, -1):
            for k in (1, 2):
                for piece in dashed(pts):
                    v, f = offset_ribbon(piece, side * (MEDIAN_WIDTH / 2.0 + lane * k), LINE_WIDTH, MARK_Y)
                    markings.add(name, v, f)
    else:
        for off in (0.2, -0.2):
            v, f = offset_ribbon(pts, off, LINE_WIDTH, MARK_Y)
            markings.add(name, v, f)
        if width >= 10.0:
            for side in (+1, -1):
                for piece in dashed(pts):
                    v, f = offset_ribbon(piece, side * half / 2.0, LINE_WIDTH, MARK_Y)
                    markings.add(name, v, f)


def road_width(tags):
    cls = tags.get("highway")
    try:
        if "width" in tags:
            return float(tags["width"].split()[0])
        if "lanes" in tags:
            return max(ROAD_WIDTH.get(cls, 7.0), int(tags["lanes"]) * LANE_WIDTH)
    except ValueError:
        pass
    return ROAD_WIDTH.get(cls, 7.0)


def building_height(tags):
    try:
        if "height" in tags:
            return float(tags["height"].split()[0])
        if "building:levels" in tags:
            return max(1, int(float(tags["building:levels"]))) * LEVEL_HEIGHT
    except ValueError:
        pass
    return DEFAULT_LEVELS * LEVEL_HEIGHT


def main_road_route(centrelines, junctions):
    """The bus's road: shortest path along primary ways from the first marker to the last, as one
    polyline. Dijkstra over the ways' nodes; a few thousand of them, so plain lists do."""
    import heapq
    graph = {}
    pos = {}
    for way in centrelines:
        if way["class"] not in ("primary", "primary_link", "secondary", "trunk"):
            continue
        for a, b, pa, pb in zip(way["nodes"], way["nodes"][1:], way["points"], way["points"][1:]):
            pos[a], pos[b] = tuple(pa), tuple(pb)
            d = math.dist(pa, pb)
            graph.setdefault(a, []).append((b, d))
            graph.setdefault(b, []).append((a, d))

    def nearest(x, z):
        return min(pos, key=lambda n: (pos[n][0] - x) ** 2 + (pos[n][1] - z) ** 2)

    start = nearest(*to_local(MARKERS[0][1], MARKERS[0][2]))
    goal = nearest(*to_local(MARKERS[-1][1], MARKERS[-1][2]))
    dist, prev, heap = {start: 0.0}, {}, [(0.0, start)]
    while heap:
        d, n = heapq.heappop(heap)
        if n == goal:
            break
        if d > dist.get(n, float("inf")):
            continue
        for m, w in graph.get(n, []):
            nd = d + w
            if nd < dist.get(m, float("inf")):
                dist[m], prev[m] = nd, n
                heapq.heappush(heap, (nd, m))
    path, n = [], goal
    while n in prev:
        path.append(n)
        n = prev[n]
    path.append(start)
    path.reverse()
    points = [[round(pos[n][0], 2), round(pos[n][1], 2)] for n in path]
    widths = [w["width"] for w in centrelines if w["class"] == "primary"]
    # "xz" is the same polyline flat (x0, z0, x1, z1, ...): Unity's JsonUtility reads float[] but not nested lists.
    return {"points": points, "xz": [c for p in points for c in p], "length": dist.get(goal, 0.0), "width": max(widths) if widths else 24.0,
            "attribution": "Map data (c) OpenStreetMap contributors, ODbL"}


def main(osm_path, out_dir, building_radius):
    os.makedirs(out_dir, exist_ok=True)
    nodes, ways = load(osm_path)
    local = {nid: to_local(lat, lon) for nid, (lat, lon) in nodes.items()}

    # Roads and centrelines.
    roads = ObjWriter()
    markings = ObjWriter()
    kerbs = ObjWriter()
    centrelines = []
    node_use = {}
    for wid, refs, tags in ways:
        if tags.get("highway") not in ROAD_WIDTH:
            continue
        pts = [local[r] for r in refs if r in local]
        if len(pts) < 2:
            continue
        for r in refs:
            node_use[r] = node_use.get(r, 0) + 1
        width = road_width(tags)
        verts, quads = ribbon(pts, width)
        # The cell of the way's midpoint: a way crossing a cell border goes to one of them whole.
        mx, mz = pts[len(pts) // 2]
        roads.add(cell_name("Roads", mx, mz), verts, quads)
        add_markings(markings, pts, width, tags["highway"], mx, mz)
        if tags["highway"] in ("primary", "secondary", "tertiary"):
            # Pavements: 2 m wide, a kerb high, both sides of the roads the bus uses.
            for side in (+1, -1):
                v, f = box_ribbon(pts, side * (width / 2.0 + 1.0), 2.0, KERB_HEIGHT)
                kerbs.add(cell_name("Pavement", mx, mz), v, f)
        centrelines.append({
            "id": wid, "class": tags["highway"], "name": tags.get("name", ""),
            "name_bn": tags.get("name:bn", ""), "width": width,
            "lanes": tags.get("lanes", ""), "oneway": tags.get("oneway", "no"),
            "nodes": refs, "points": [[round(x, 2), round(z, 2)] for x, z in pts],
        })
    junctions = {r: list(local[r]) for r, n in node_use.items() if n >= 3 and r in local}

    # Road surfaces as segments in a 50 m grid, so a footprint can ask cheaply whether it stands on
    # a road. Mapped footprints do overlap mapped roads here and there, and a wall across the
    # carriageway is worse than a missing house.
    GRID = 50.0
    road_grid = {}
    for way in centrelines:
        # Only the roads the bus drives: in the lanes the map's footprints and 7 m default widths
        # overlap all the time, and dropping those houses hollows the city out (7,800 of 18,800 went).
        if way["class"] not in ("primary", "primary_link", "secondary", "secondary_link", "tertiary", "tertiary_link"):
            continue
        half = way["width"] / 2.0 + 0.5
        for (x0, z0), (x1, z1) in zip(way["points"], way["points"][1:]):
            for gx in range(int(min(x0, x1) // GRID) - 1, int(max(x0, x1) // GRID) + 2):
                for gz in range(int(min(z0, z1) // GRID) - 1, int(max(z0, z1) // GRID) + 2):
                    road_grid.setdefault((gx, gz), []).append((x0, z0, x1, z1, half))

    def on_a_road(x, z):
        for (x0, z0, x1, z1, half) in road_grid.get((int(x // GRID), int(z // GRID)), ()):
            dx, dz = x1 - x0, z1 - z0
            l2 = dx * dx + dz * dz
            t = 0.0 if l2 == 0 else max(0.0, min(1.0, ((x - x0) * dx + (z - z0) * dz) / l2))
            px, pz = x0 + dx * t, z0 + dz * t
            if (px - x) ** 2 + (pz - z) ** 2 < half * half:
                return True
        return False

    def footprint_on_a_road(pts):
        # Corners and edge midpoints: a long wall across a road has no corner on it.
        for i, (x, z) in enumerate(pts):
            if on_a_road(x, z):
                return True
            nx, nz = pts[(i + 1) % len(pts)]
            if on_a_road((x + nx) / 2.0, (z + nz) / 2.0):
                return True
        return False

    # Buildings near the main road only: the whole box is 36,000 footprints, far past the scene
    # budget, and the bus never sees past the second row. --radius widens it.
    primary = [c for c in centrelines if c["class"] in ("primary", "primary_link")]
    spine = [tuple(p) for c in primary for p in c["points"]]

    def near_spine(x, z):
        r2 = building_radius * building_radius
        for sx, sz in spine:
            if (sx - x) ** 2 + (sz - z) ** 2 < r2:
                return True
        return False

    buildings = ObjWriter()
    kept = skipped = on_road = 0
    for wid, refs, tags in ways:
        if "building" not in tags or len(refs) < 4 or refs[0] != refs[-1]:
            continue
        pts = [local[r] for r in refs[:-1] if r in local]
        if len(pts) < 3:
            continue
        cx = sum(p[0] for p in pts) / len(pts)
        cz = sum(p[1] for p in pts) / len(pts)
        if not near_spine(cx, cz):
            skipped += 1
            continue
        if footprint_on_a_road(pts):
            on_road += 1
            continue
        if polygon_area(pts) < 0:
            pts.reverse()
        h = building_height(tags)
        n = len(pts)
        verts = [(x, 0.0, z) for x, z in pts] + [(x, h, z) for x, z in pts]
        faces = []
        for i in range(n):
            j = (i + 1) % n
            faces.append((i, j, n + j, n + i))                     # wall, outward
        faces.extend((n + a, n + b, n + c) for a, b, c in ear_clip(pts))   # roof, upward
        buildings.add(cell_name("Blocks", cx, cz), verts, faces)
        kept += 1

    # The metro viaduct runs down the middle of the main road on this stretch (RESEARCH.md: MRT Line 6
    # on Rokeya Sarani), so it is built along the road's own centreline, not the mapped railway, which
    # sits a few metres off it: deck, piers every 30 m, and the median barrier between the carriageways.
    route = main_road_route(centrelines, junctions)
    rail = ObjWriter()
    rpts = [tuple(p) for p in route["points"]]
    if len(rpts) >= 2:
        top, quads = offset_ribbon(rpts, 0.0, RAIL_DECK_WIDTH, RAIL_DECK_HEIGHT)
        bottom = [(x, RAIL_DECK_HEIGHT - RAIL_DECK_THICKNESS, z) for x, _, z in top]
        m = len(top)
        faces = list(quads)                                            # top, seen from above
        faces += [tuple(m + i for i in reversed(q)) for q in quads]    # bottom, seen from below
        for i in range(0, m - 2, 2):
            faces.append((i, m + i, m + i + 2, i + 2))                 # left side
            faces.append((i + 3, m + i + 3, m + i + 1, i + 1))         # right side
        rail.add("Viaduct", top + bottom, faces)
        walked = 0.0
        for i in range(len(rpts) - 1):
            (x0, z0), (x1, z1) = rpts[i], rpts[i + 1]
            seg = math.hypot(x1 - x0, z1 - z0)
            t = walked
            while t < seg:
                f = t / seg
                px, pz = x0 + (x1 - x0) * f, z0 + (z1 - z0) * f
                sz = RAIL_PIER_SIZE / 2.0
                base = [(px - sz, 0.0, pz - sz), (px + sz, 0.0, pz - sz), (px + sz, 0.0, pz + sz), (px - sz, 0.0, pz + sz)]
                topv = [(x, RAIL_DECK_HEIGHT - RAIL_DECK_THICKNESS, z) for x, _, z in base]
                pf = [(0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
                rail.add("Piers", base + topv, pf)
                t += RAIL_PIER_EVERY
            walked = t - seg
        v, f = box_ribbon(rpts, 0.0, MEDIAN_WIDTH, MEDIAN_HEIGHT)
        kerbs.add("Median", v, f)

    roads.write(os.path.join(out_dir, "Roads.obj"))
    buildings.write(os.path.join(out_dir, "Buildings.obj"))
    rail.write(os.path.join(out_dir, "Rail.obj"))
    markings.write(os.path.join(out_dir, "Markings.obj"))
    kerbs.write(os.path.join(out_dir, "Kerbs.obj"))
    with open(os.path.join(out_dir, "centrelines.json"), "w") as f:
        json.dump({"origin": {"lat": ORIGIN_LAT, "lon": ORIGIN_LON}, "frame": "x east, z north, metres",
                   "attribution": "Map data (c) OpenStreetMap contributors, ODbL",
                   "roads": centrelines, "junctions": junctions}, f)
    with open(os.path.join(out_dir, "route.json"), "w") as f:
        json.dump(route, f)
    with open(os.path.join(out_dir, "markers.json"), "w") as f:
        json.dump([{"name": n, "x": round(to_local(la, lo)[0], 1), "z": round(to_local(la, lo)[1], 1)} for n, la, lo in MARKERS], f, indent=1)

    print(f"roads: {len(centrelines)} ways, {roads.triangles} triangles in {len(roads.objects)} cells; {len(junctions)} junction nodes")
    print(f"buildings: {kept} kept within {building_radius:.0f} m of the main road ({skipped} beyond, {on_road} dropped for standing on a road), {buildings.triangles} triangles in {len(buildings.objects)} cells")
    print(f"rail: {rail.triangles} triangles; markings {markings.triangles}; kerbs and median {kerbs.triangles}")
    print(f"route: {len(route['points'])} points, {route['length']:.0f} m, width {route['width']:.0f} m")
    print(f"total {roads.triangles + buildings.triangles + rail.triangles + markings.triangles + kerbs.triangles} triangles (budget 400k, docs/OSM_IMPORT_PLAN.md)")


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(2)
    radius = float(sys.argv[3]) if len(sys.argv) > 3 else 300.0
    main(sys.argv[1], sys.argv[2], radius)
