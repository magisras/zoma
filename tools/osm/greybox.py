#!/usr/bin/env python3
"""OpenStreetMap extract -> grey-box meshes and road centrelines for Unity (README milestone 1).

Replaces the Blender/blosm route in docs/OSM_IMPORT_PLAN.md with something repeatable that runs
from a terminal in a few seconds. Standard library only.

    python3 tools/osm/greybox.py mirpur.osm Assets/World/Corridor01

Reads the .osm XML (download: tools/osm/fetch.sh, the corridor along MARKERS) and writes into the
output folder one Chunk_NN/ per kilometre of route, each with:
    Roads.obj         flat ribbons, one object per 250 m grid cell, at y = 0
    Buildings.obj     extruded footprints within 300 m of the route, roofs at 3 m a level
    Rail.obj          the MRT Line 6 viaduct where the mapped metro follows the road: a deck 12 m up,
                      piers in the median
    Markings.obj      lane lines: solid edges, dashed lane lines, a double line down the middle
    Kerbs.obj         raised pavements beside the main roads, clipped where they would lie on a road
    Median.obj        the median barrier between the carriageways, its own mesh: in Unity it goes on a
                      layer the wheels ignore (the body still hits it), so a bus cannot climb it
    Walls.obj         invisible colliders: a tall wall over the median barrier, same layer
and beside them:
    centrelines.json  every drivable road as a polyline in metres, with class, width, name and the
                      junction nodes it shares: the input for the traffic corridors (milestone 3)
    markers.json      the named points of the route (stands, junctions) in local metres
    route.json        the route through the markers as one polyline: the shortest path over the main
                      roads respecting one-way flow, its kerb-to-kerb width, the far kerb per point and
                      which points are on a dual carriageway; the bus's corridor
    route_back.json   the same from Azimpur back to Mirpur 12, on the other carriageway where dual
    stops.json        the bus stops: OSM bus_stop nodes on the kerb side of each leg, plus the
                      research's named pickups and stands (hot), with S on each leg
    crossings.json    the cross streets with an officer: the crossing way 150 m either side, and where
                      it cuts each leg (TrafficSim.Junction)
    chunks.json       each chunk's stretch of the route sampled every 50 m: WorldStreamer's index
A Unity scene is made of each chunk folder and streamed around the bus (GreyboxSceneBuilder,
WorldStreamer). None of the meshes are in git: make world.

Coordinates: local metres, origin at the Mirpur 12 stand, x east, y up, z north. OBJ files are
right-handed and Unity mirrors x on import, so the files store -x; in Unity x is east again.
Faces are wound counter-clockwise seen from outside in the x-z shoelace sense, which Unity
turns into outward normals after its mirror.
Map data (c) OpenStreetMap contributors, ODbL. Keep the credit in the game.
"""
import json
import bisect
import glob
import math
import os
import shutil
import sys
import xml.etree.ElementTree as ET

# ---------------------------------------------------------------- what to keep and how wide

# Origin of the local frame: the Mirpur 12 bus stand (docs/OSM_IMPORT_PLAN.md, section 1).
ORIGIN_LAT, ORIGIN_LON = 23.828, 90.364

# Road classes the bus or the traffic can use, and a kerb-to-kerb width in metres when OSM
# gives none. Rokeya Sarani (primary) is two carriageways of three lanes with the metro piers
# in the median; the number is for the whole road surface, median included.
ROAD_WIDTH = {
    "trunk": 24.0, "trunk_link": 10.0,          # Kazi Nazrul Islam Avenue, Shahbag Road, Khamar Bari Road
    "primary": 24.0, "primary_link": 10.0,
    "secondary": 16.0, "secondary_link": 8.0,
    "tertiary": 12.0, "tertiary_link": 8.0,
    "residential": 7.0, "unclassified": 7.0, "living_street": 5.0,
}
LANE_WIDTH = 3.3            # when OSM has a lanes= tag and no width=
LEVEL_HEIGHT = 3.0          # metres per storey (blosm's default too)
DEFAULT_LEVELS = 2          # Mirpur blocks without a levels tag: low concrete houses
CELL = 250.0                # metres; one mesh per cell so Unity can cull
CHUNK = 1000.0              # metres along the route; one folder (one Unity scene) per chunk, streamed around the bus

# The route's named points, in order, read off the map, "about here" (docs/OSM_IMPORT_PLAN.md
# section 1 for the first four; the rest are the pickups of S-007.6 and the stands of RESEARCH.md:
# Mirpur 12 to Azimpur by Rokeya Sarani, Farmgate, Karwan Bazar, Shahbagh and Nilkhet). The route
# is stitched through them in this order, so a wrong point here sends the bus down a wrong road.
MARKERS = [
    ("Mirpur 12 stand", 23.828, 90.364),
    ("Mirpur 11", 23.819, 90.366),
    ("Mirpur 10 roundabout", 23.807, 90.368),
    ("Kazipara", 23.799, 90.371),
    ("Shewrapara", 23.790, 90.374),
    ("Taltola", 23.783, 90.376),
    ("Agargaon crossing", 23.778, 90.378),
    ("Chandrima Udyan box", 23.766, 90.384),
    ("Khamarbari", 23.7595, 90.386),
    ("Farmgate", 23.757, 90.390),
    ("Karwan Bazar fountain", 23.751, 90.393),
    ("Bangla Motor", 23.746, 90.395),
    ("Shahbagh", 23.7385, 90.394),
    ("TSC", 23.733, 90.396),
    ("Nilkhet", 23.733, 90.387),
    ("Azimpur stand", 23.7265, 90.384),
]
# Marker 0 is the first stand, the last marker the other; the bus runs between them all day.
CORRIDOR_ROADS = 600.0      # metres either side of the route line that the download covers
CORRIDOR_BUILDINGS = 300.0
CORRIDOR_STOPS = 60.0        # bus stops this close to the route line are the route's stops

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
    """A raised strip: a ribbon's top at `height` plus its two long sides down to the ground, and a
    cap at each end, both ways round so a body meets it from either side: the walls are hollow
    meshes, and a bus that drove into an open end sat trapped inside the wall (owner, 10 Oct)."""
    top, quads = offset_ribbon(points, offset, width, height)
    base = [(x, 0.0, z) for x, _, z in top]
    m = len(top)
    faces = list(quads)
    for i in range(0, m - 2, 2):
        faces.append((i, m + i, m + i + 2, i + 2))             # left side, outward
        faces.append((i + 3, m + i + 3, m + i + 1, i + 1))     # right side, outward
    for a, b in ((0, 1), (m - 1, m - 2)):                       # the start and the end
        faces.append((a, b, m + b, m + a))
        faces.append((m + a, m + b, b, a))
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
        self.objects = {}      # name -> (verts, faces, uvs, face_uv); faces are index tuples into verts
        self.triangles = 0

    def add(self, name, verts, faces, uv=None):
        """uv, when given, is one (u, v) for every face of this call: the facade shader reads
        u = the block's random seed and v = its storeys from the mesh's texture coordinates."""
        v, f, uvs, fuv = self.objects.setdefault(name, ([], [], [], []))
        base = len(v)
        v.extend(verts)
        f.extend(tuple(base + i for i in face) for face in faces)
        if uv is not None:
            uvs.append(uv)
        fuv.extend([len(uvs) - 1 if uv is not None else -1] * len(faces))
        self.triangles += sum(len(face) - 2 for face in faces)

    def write(self, path):
        with open(path, "w") as out:
            out.write("# Twenty Tons grey box. Map data (c) OpenStreetMap contributors, ODbL.\n")
            offset = 1
            uv_offset = 1
            for name in sorted(self.objects):
                verts, faces, uvs, fuv = self.objects[name]
                if not faces:
                    continue
                out.write(f"o {name}\n")
                for x, y, z in verts:
                    out.write(f"v {-x:.2f} {y:.2f} {z:.2f}\n")
                for u, w in uvs:
                    out.write(f"vt {u:.4f} {w:.2f}\n")
                for face, k in zip(faces, fuv):
                    # Negating x mirrors the mesh and Unity mirrors it back on import; the winding
                    # survives both, so faces go out as built. (Reversing them here was tried first:
                    # every road faced down and every wall faced in. Checked in the editor: a mesh's
                    # calculated normals should come out "up" for roads and roofs.)
                    if k >= 0:
                        out.write("f " + " ".join(f"{offset + i}/{uv_offset + k}" for i in face) + "\n")
                    else:
                        out.write("f " + " ".join(str(offset + i) for i in face) + "\n")
                offset += len(verts)
                uv_offset += len(uvs)


def cell_name(prefix, x, z):
    return f"{prefix}_{int(math.floor(x / CELL)):+d}_{int(math.floor(z / CELL)):+d}"


class Chunked:
    """One ObjWriter per route chunk; written as Chunk_NN/<file>. Unity makes a scene of each
    folder and loads the ones around the bus (WorldStreamer)."""

    def __init__(self, filename):
        self.filename = filename
        self.writers = {}

    def add(self, chunk, name, verts, faces, uv=None):
        self.writers.setdefault(chunk, ObjWriter()).add(name, verts, faces, uv)

    @property
    def triangles(self):
        return sum(w.triangles for w in self.writers.values())

    @property
    def objects(self):
        return [o for w in self.writers.values() for o in w.objects]

    def write(self, out_dir):
        for chunk, w in self.writers.items():
            folder = os.path.join(out_dir, f"Chunk_{chunk:02d}")
            os.makedirs(folder, exist_ok=True)
            w.write(os.path.join(folder, self.filename))
        # A chunk that has nothing of this kind any more loses the old file, or a stale mesh stays
        # in the scene: a median wall that the generator no longer builds stood in the lane at Azimpur.
        for folder in glob.glob(os.path.join(out_dir, "Chunk_[0-9][0-9]")):
            if int(folder[-2:]) not in self.writers:
                for stale in (os.path.join(folder, self.filename), os.path.join(folder, self.filename + ".meta")):
                    if os.path.exists(stale):
                        os.remove(stale)


# ---------------------------------------------------------------- the extract

def is_stop(tags):
    """An OSM bus stop: the classic tag, the public-transport platform for buses, or a station."""
    return (tags.get("highway") == "bus_stop" or tags.get("amenity") == "bus_station"
            or (tags.get("public_transport") == "platform" and tags.get("bus") == "yes"))


def load(path):
    nodes, ways, stops = {}, [], []
    for _, el in ET.iterparse(path, events=("end",)):
        if el.tag == "node":
            lat, lon = float(el.get("lat")), float(el.get("lon"))
            nodes[el.get("id")] = (lat, lon)
            if len(el):                                     # a tagged node: a bus stop, a shop, a crossing
                tags = {t.get("k"): t.get("v") for t in el.findall("tag")}
                if is_stop(tags):
                    stops.append((lat, lon, tags))
            el.clear()
        elif el.tag == "way":
            refs = [nd.get("ref") for nd in el.findall("nd")]
            tags = {t.get("k"): t.get("v") for t in el.findall("tag")}
            ways.append((el.get("id"), refs, tags))
            el.clear()
    return nodes, ways, stops


KERB_HEIGHT = 0.15
LINE_WIDTH = 0.15
MEDIAN_WIDTH = 3.0
WALL_HEIGHT = 4.5            # the invisible collider over the median: taller than the bus body (3.2 m + clearance).
                             # A hollow mesh shorter than the body has its top face inside the body's box the moment
                             # they overlap, and that face lifts the box: that is how the bus climbed 1.1 and 2.6 m walls.
MEDIAN_HEIGHT = 1.1          # a concrete barrier the body cannot ride up; 0.6 and 0.9 both let the physics bus climb onto it
MARK_Y = 0.02          # paint sits just above the road so the two do not fight for the pixel


def add_markings(markings, pts, width, cls, mx, mz, oneway=False):
    """What a driver reads: solid edge lines, dashed lane lines three lanes a carriageway on the
    main road, a double line down the middle of a two-way street."""
    name = cell_name("Lines", mx, mz)
    half = width / 2.0
    for side in (+1, -1):
        v, f = offset_ribbon(pts, side * (half - 0.3), LINE_WIDTH, MARK_Y)
        markings.add(name, v, f)
    if oneway:
        # One carriageway: dashed lines between its lanes, nothing down the middle.
        lanes = max(1, int(round(width / 3.5)))
        for k in range(1, lanes):
            for piece in dashed(pts):
                v, f = offset_ribbon(piece, -half + k * width / lanes, LINE_WIDTH, MARK_Y)
                markings.add(name, v, f)
        return
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


def is_oneway(tags):
    return tags.get("oneway") in ("yes", "true", "1", "-1")


def road_width(tags):
    """Kerb to kerb. A one-way primary or secondary way is one carriageway of a dual carriageway
    (Rokeya Sarani is mapped as two such ways about 20 m apart), so it gets a carriageway's width,
    not the whole road's."""
    cls = tags.get("highway")
    try:
        if "width" in tags:
            return float(tags["width"].split()[0])
        if is_oneway(tags) and cls in ("primary", "primary_link", "secondary", "secondary_link", "trunk", "trunk_link"):
            lanes = int(tags["lanes"]) if "lanes" in tags else 3
            return max(8.0, lanes * 3.5)
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


def body_safe(height):
    """No block shorter than the bus body (3.2 m plus clearance): see WALL_HEIGHT."""
    return max(height, 3.8)


def smooth_polyline(points, spacing=5.0, window=12.0, passes=3):
    """Resample a polyline every `spacing` metres and smooth it with a moving average over
    +-`window` metres, keeping the ends. Mapped node jitter puts 2 m jogs in a carriageway that
    a bus at 45 km/h cannot follow; the real road has none. Used for the route (the bus's corridor)
    and so for the median and the viaduct, not for the drawn road surface."""
    pts = [tuple(p) for p in points]
    if len(pts) < 3:
        return [[round(x, 2), round(z, 2)] for x, z in pts]
    dense = [pts[0]]
    for (x0, z0), (x1, z1) in zip(pts, pts[1:]):
        seg = math.hypot(x1 - x0, z1 - z0)
        n = max(1, int(seg / spacing))
        for k in range(1, n + 1):
            f = k / n
            dense.append((x0 + (x1 - x0) * f, z0 + (z1 - z0) * f))
    half = max(1, int(window / spacing))
    for _ in range(passes):
        out = [dense[0]]
        for i in range(1, len(dense) - 1):
            lo, hi = max(0, i - half), min(len(dense) - 1, i + half)
            xs = [dense[j][0] for j in range(lo, hi + 1)]
            zs = [dense[j][1] for j in range(lo, hi + 1)]
            out.append((sum(xs) / len(xs), sum(zs) / len(zs)))
        out.append(dense[-1])
        dense = out
    return [[round(x, 2), round(z, 2)] for x, z in simplify(dense, 0.12)]


def simplify(points, tolerance):
    """Douglas-Peucker: drop points that lie within `tolerance` metres of the line between their
    neighbours, so a smoothed polyline costs no more triangles than it needs."""
    if len(points) < 3:
        return list(points)
    (x0, z0), (x1, z1) = points[0], points[-1]
    dx, dz = x1 - x0, z1 - z0
    l2 = dx * dx + dz * dz
    worst, wd = 0, 0.0
    for i in range(1, len(points) - 1):
        x, z = points[i]
        if l2 == 0:
            d = math.hypot(x - x0, z - z0)
        else:
            t = max(0.0, min(1.0, ((x - x0) * dx + (z - z0) * dz) / l2))
            d = math.hypot(x - (x0 + dx * t), z - (z0 + dz * t))
        if d > wd:
            worst, wd = i, d
    if wd <= tolerance:
        return [points[0], points[-1]]
    return simplify(points[:worst + 1], tolerance)[:-1] + simplify(points[worst:], tolerance)


def main_road_route(centrelines, junctions, markers=None):
    """The bus's road: the shortest path along the main roads from marker to marker, in order, as one
    polyline. Dijkstra over the ways' nodes, one leg per pair of markers; a few thousand nodes, so
    plain lists do. Tertiary streets count double, so the path only takes one where no bigger road
    goes (the Dhaka University stretch between TSC and Nilkhet). With the markers reversed it
    gives the way back, on the other carriageway where the road is dual."""
    import heapq
    markers = markers or MARKERS
    graph = {}
    pos = {}
    hop = {}        # (a, b) -> (way, index of a, index of b) on that way's smoothed points
    for way in centrelines:
        if way["class"] not in ("primary", "primary_link", "secondary", "secondary_link", "trunk", "trunk_link", "tertiary"):
            continue
        penalty = 2.0 if way["class"] == "tertiary" else 1.0
        idx = way.get("node_index", {})
        nodes = [n for n in way["nodes"] if n in idx]
        for a, b in zip(nodes, nodes[1:]):
            pa, pb = way["points"][idx[a]], way["points"][idx[b]]
            pos[a], pos[b] = tuple(pa), tuple(pb)
            d = math.dist(pa, pb) * penalty
            graph.setdefault(a, []).append((b, d))
            hop[(a, b)] = (way, idx[a], idx[b])
            if not way.get("oneway_flow"):
                graph.setdefault(b, []).append((a, d))     # two-way: both directions
                hop[(b, a)] = (way, idx[b], idx[a])

    def nearest(x, z, within=350.0):
        """Every graph node within reach of a marker: in the centre a count limit fills up with the
        nodes of one side street before the junction the marker means."""
        return [n for n in pos if (pos[n][0] - x) ** 2 + (pos[n][1] - z) ** 2 < within * within]

    # Many candidate nodes at each marker, since the markers are read off the map by eye: the
    # nearest node may be on a side street or on the carriageway going the other way. One Dijkstra
    # over states (markers passed, node): passing a candidate of the next marker costs nothing and
    # moves to the next layer, so the shortest route that visits every marker in order falls out,
    # on whichever candidate nodes make it possible.
    candidates = [set(nearest(*to_local(la, lo))) for _, la, lo in markers]
    marker_xz = [to_local(la, lo) for _, la, lo in markers]
    last = len(markers) - 1
    # Starting and ending cost twice the distance to the stand's marker, so the route begins and
    # ends by the stands: at the plain distance, starting 300 m down the road is a tie.
    dist, prev, heap = {}, {}, []
    for n in candidates[0]:
        dist[(0, n)] = 2.0 * math.dist(pos[n], marker_xz[0])
        heap.append((dist[(0, n)], 0, n))
    heapq.heapify(heap)
    goal = None
    while heap:
        d, i, n = heapq.heappop(heap)
        if d > dist.get((i, n), float("inf")):
            continue
        if i == last:
            goal = (i, n)
            break
        if n in candidates[i + 1]:
            nd = d + (2.0 * math.dist(pos[n], marker_xz[last]) if i + 1 == last else 0.0)
            if nd < dist.get((i + 1, n), float("inf")):
                dist[(i + 1, n)], prev[(i + 1, n)] = nd, (i, n)
                heapq.heappush(heap, (nd, i + 1, n))
        for m, w in graph.get(n, []):
            nd = d + w
            if nd < dist.get((i, m), float("inf")):
                dist[(i, m)], prev[(i, m)] = nd, (i, n)
                heapq.heappush(heap, (nd, i, m))
    if goal is None:
        reached = max(i for i, _ in dist)
        raise SystemExit(f"no one-way-respecting path from {markers[reached][0]} to {markers[reached + 1][0]}: move a marker onto the road")
    length = dist[goal]
    path, state = [], goal
    while state in prev:
        if not path or path[-1] != state[1]:
            path.append(state[1])
        state = prev[state]
    if path[-1] != state[1]:
        path.append(state[1])
    path.reverse()
    # Stitch the path from the ways' own smoothed points, so the corridor is the drawn carriageway,
    # and remember per point whether the way is a one-way carriageway (a dual carriageway with a
    # median) or a two-way street.
    points = [list(pos[path[0]])]
    dual = [bool(hop[(path[0], path[1])][0].get("oneway_flow"))]
    width_at = [hop[(path[0], path[1])][0]["width"]]              # the carriageway's width at each point
    for a, b in zip(path, path[1:]):
        way, ia, ib = hop[(a, b)]
        if way["nodes"][0] == way["nodes"][-1]:
            # A closed way (a roundabout): its last point is its first, and a hop from the node before
            # the seam to the node after it goes forward across the seam, not backwards round the
            # whole circle. One-way (every Dhaka roundabout) goes with the flow; two-way the short way.
            m = len(way["points"]) - 1
            ia, ib = ia % m, ib % m
            seq = [(ia + k) % m for k in range(1, (ib - ia) % m + 1)]
            if not way.get("oneway_flow"):
                back = [(ia - k) % m for k in range(1, (ia - ib) % m + 1)]
                if len(back) < len(seq):
                    seq = back
        else:
            step = 1 if ib >= ia else -1
            seq = range(ia + step, ib + step, step)
        for k in seq:
            points.append(list(way["points"][k]))
            dual.append(bool(way.get("oneway_flow")))
            width_at.append(way["width"])
    points = [[round(x, 2), round(z, 2)] for x, z in points]
    # A node shared by two ways lands twice; a zero-length segment has no direction, and the
    # median line and the far kerb are built from directions.
    keep = [i for i in range(len(points)) if i == 0 or math.dist(points[i], points[i - 1]) >= 0.5]
    points = [points[i] for i in keep]
    dual = [dual[i] for i in keep]
    width_at = [width_at[i] for i in keep]
    # Where two ways meet, the smoothed point a node maps to can lie a few metres past the
    # junction, and the stitch runs out to it and back: a hairpin spike that put the median wall
    # across the lane at Mirpur 10. Drop a point the line returns from.
    i = 1
    while i < len(points) - 1:
        a, b, c = points[i - 1], points[i], points[i + 1]
        if math.dist(a, b) < 0.5 or math.dist(a, c) < max(math.dist(a, b), math.dist(b, c)) * 0.7:
            del points[i], dual[i], width_at[i]
            i = max(1, i - 1)
        else:
            i += 1
    on_path = set(path)
    widths = [w["width"] for w in centrelines if w["class"] == "primary" and on_path.intersection(w["nodes"])]
    # "xz" is the same polyline flat (x0, z0, x1, z1, ...): Unity's JsonUtility reads float[] but not nested lists.
    return {"points": points, "xz": [c for p in points for c in p], "length": length, "width": min(widths) if widths else 10.0,
            "widthAt": width_at, "dual": dual, "attribution": "Map data (c) OpenStreetMap contributors, ODbL"}


def arc_length(pts):
    """Distance along a polyline at each of its points, starting at 0."""
    along = [0.0]
    for p, q in zip(pts, pts[1:]):
        along.append(along[-1] + math.dist(p, q))
    return along


def far_edges(route, median_pts):
    """Per route point, how far right of the route line the far kerb is: on a two-way street the
    route's own far kerb; on a dual carriageway across the median to the other carriageway's kerb
    (the median line is halfway, so twice its offset). The drive treats everything up to it as
    road: crossing to the wrong side is the game, not a wall."""
    pts = [tuple(p) for p in route["points"]]
    dual = route["dual"]
    far = []
    for i, (x, z) in enumerate(pts):
        if not dual[i]:
            far.append(round(route["width"] / 2.0 + 2.5, 1))
            continue
        (ax, az), (bx, bz) = pts[max(0, i - 1)], pts[min(len(pts) - 1, i + 1)]
        dx, dz = bx - ax, bz - az
        l = math.hypot(dx, dz) or 1.0
        rx_, rz_ = dz / l, -dx / l
        off = (median_pts[i][0] - x) * rx_ + (median_pts[i][1] - z) * rz_
        far.append(round(off * 2.0 + route["width"] / 2.0 + 2.5, 1))
    return far


def project_on_route(pts, along, x, z):
    """Where a point falls on a route: (distance along, lateral: right positive, left negative, and
    how far it is from the line). The nearest segment wins."""
    best = None
    for i in range(len(pts) - 1):
        (ax, az), (bx, bz) = pts[i], pts[i + 1]
        dx, dz = bx - ax, bz - az
        l = math.hypot(dx, dz)
        if l < 1e-6:
            continue
        t = max(0.0, min(l, ((x - ax) * dx + (z - az) * dz) / l))
        px, pz = ax + dx / l * t, az + dz / l * t
        d = math.hypot(x - px, z - pz)
        if best is None or d < best[2]:
            lateral = ((x - ax) * dz - (z - az) * dx) / l       # right of the direction of travel
            best = (along[i] + t, lateral, d)
    return best


def point_at(pts, along, s, lateral):
    """The point at distance s along a polyline, lateral metres to the right of it."""
    i = max(0, min(len(pts) - 2, bisect.bisect_right(along, s) - 1))
    (ax, az), (bx, bz) = pts[i], pts[i + 1]
    l = math.hypot(bx - ax, bz - az) or 1.0
    t = max(0.0, min(1.0, (s - along[i]) / l))
    dx, dz = (bx - ax) / l, (bz - az) / l
    return (ax + dx * l * t + dz * lateral, az + dz * l * t - dx * lateral)


def route_stops(stop_nodes, route, route_back):
    """The stops of the route, on each leg: the OSM bus stop nodes on the left (kerb) side of the leg's
    carriageway (left-hand traffic; a stop across a dual road belongs to the other leg), plus the
    research's named pickups and stands (MARKERS, S-007.6), which are the hot ones. A stop's S on a
    leg is -1 where it is not on that leg. Stops are demand zones for TrafficSim (RESEARCH.md: 'demand
    zones along the road rather than fixed stops, with hot clusters at junctions')."""
    legs = []
    for r in (route, route_back):
        pts = [tuple(p) for p in r["points"]]
        legs.append((pts, arc_length(pts), r["width"] / 2.0))

    def on_leg(leg, x, z, both_sides):
        pts, along, half = legs[leg]
        hit = project_on_route(pts, along, x, z)
        if hit is None:
            return None
        s, lateral, d = hit
        if both_sides:
            # A research marker, read off the map by eye: anywhere near the road, either side, and
            # the stands at the ends of the line.
            return (s, lateral) if d < 350.0 else None
        if d > abs(lateral) + 1.0:                      # beyond the end of the line, not beside it
            return None
        # The kerb side: the pavement and 25 m of the block behind it (a stop drawn inside a bay or
        # a market), or inside the carriageway itself where the mapper put it mid-road.
        return (s, lateral) if -(half + 25.0) < lateral < 3.0 else None

    def entry(name, name_bn, x, z, hot, kind, source, out, back, on_kerb):
        """One stop. outX/outZ and backX/backZ are where its sign stands on each leg: an OSM node where
        the mapper put it; a research marker, read off the map by eye and often a block away, on the
        leg's kerb at its S."""
        st = {"name": name, "nameBn": name_bn, "x": round(x, 1), "z": round(z, 1), "hot": hot, "kind": kind, "source": source}
        for leg, hit in (("out", out), ("back", back)):
            pts, along, half = legs[0 if leg == "out" else 1]
            sx, sz = (point_at(pts, along, hit[0], -(half + 1.0)) if (hit and on_kerb) else (x, z))
            st[leg + "S"] = round(hit[0], 1) if hit else -1
            st[leg + "Lat"] = round(hit[1], 1) if hit else 0
            st[leg + "X"], st[leg + "Z"] = round(sx, 1), round(sz, 1)
        return st

    stops = []
    for i, (name, la, lo) in enumerate(MARKERS):
        x, z = to_local(la, lo)
        kind = "stand" if i in (0, len(MARKERS) - 1) else "pickup"
        stops.append(entry(name, "", x, z, True, kind, "research", on_leg(0, x, z, True), on_leg(1, x, z, True), True))
    for la, lo, tags in stop_nodes:
        x, z = to_local(la, lo)
        out, back = on_leg(0, x, z, False), on_leg(1, x, z, False)
        if out is None and back is None:
            continue
        # The same spot as a research pickup, or as a stop already kept on this leg: one stop, not two.
        twin = False
        for st in stops:
            for leg, hit in (("out", out), ("back", back)):
                if hit and st[leg + "S"] >= 0 and abs(st[leg + "S"] - hit[0]) < 40.0:
                    twin = True
                    if not st["nameBn"]:
                        st["nameBn"] = tags.get("name:bn", "")
                    if st["source"] == "osm" and not st["name"]:
                        st["name"] = tags.get("name", "")
        if twin:
            continue
        stops.append(entry(tags.get("name", ""), tags.get("name:bn", ""), x, z, False, "stop", "osm", out, back, False))
    stops.sort(key=lambda st: st["outS"] if st["outS"] >= 0 else 1e9 - st["backS"])
    return stops


def median_line(route, centrelines):
    """Where the median is: halfway between the route's carriageway and the opposite one, found as
    the nearest one-way primary segment that runs parallel to ours and lies to our right (left-hand
    traffic: the oncoming carriageway is on the right). Where none is found, at a junction say, the
    last offset is carried on, never the route line itself: that put piers in the bus's lane at
    Kalshi Road. The MRT piers stand on this line (RESEARCH.md: the viaduct in the middle of Rokeya
    Sarani). Returns the line and, per point, whether there is room for a barrier between the two
    carriageways: at Azimpur the pair is 8 m apart centre to centre (a painted line, no barrier),
    and a barrier built there stood in the lane of the way back."""
    others = []
    for w in centrelines:
        if w["class"] in ("primary", "trunk", "secondary", "primary_link", "trunk_link", "secondary_link") and w.get("oneway_flow"):
            others.extend(zip(w["points"], w["points"][1:]))
    route_pts = [tuple(p) for p in route["points"]]
    route_set = set(route_pts)
    n = len(route_pts)
    offsets = [None] * n
    for i, (x, z) in enumerate(route_pts):
        # Our direction here and the right-hand normal.
        (ax, az), (bx, bz) = route_pts[max(0, i - 1)], route_pts[min(n - 1, i + 1)]
        dx, dz = bx - ax, bz - az
        l = math.hypot(dx, dz) or 1.0
        dx, dz = dx / l, dz / l
        rx, rz = dz, -dx                        # right of travel in the x-z plane
        best, bd = None, 40.0
        for (px0, pz0), (px1, pz1) in others:
            if (px0, pz0) in route_set or (px1, pz1) in route_set:
                continue                        # our own carriageway
            sx, sz = px1 - px0, pz1 - pz0
            sl = math.hypot(sx, sz) or 1.0
            if abs((sx * dx + sz * dz) / sl) < 0.85:
                continue                        # not parallel: a cross street
            t = max(0.0, min(1.0, ((x - px0) * sx + (z - pz0) * sz) / (sl * sl)))
            qx, qz = px0 + sx * t, pz0 + sz * t
            right = (qx - x) * rx + (qz - z) * rz
            along = (qx - x) * dx + (qz - z) * dz
            if abs(along) > 15.0:
                continue                        # not abeam of us: a segment far down the road
            if right < route["width"] * 0.75 or right > bd:
                continue                        # on our left or inside our own carriageway (a roundabout's
                                                # far side), or further than the best so far
            best, bd = right, right
        offsets[i] = best / 2.0 if best is not None else None
    matched = [o is not None for o in offsets]       # found here, not carried from up the road
    # Room for a barrier: the gap between the carriageways holds it with a lane's margin either side.
    room = [None if o is None else (o * 2.0 >= route["width"] + MEDIAN_WIDTH + 1.0) for o in offsets]
    last = True
    for i in range(n):
        if room[i] is None:
            room[i] = last
        else:
            last = room[i]
    # Carry offsets across gaps: previous known, else next known, else 10 m (a carriageway apart).
    last = None
    for i in range(n):
        if offsets[i] is None:
            offsets[i] = last
        else:
            last = offsets[i]
    nxt = None
    for i in range(n - 1, -1, -1):
        if offsets[i] is None:
            offsets[i] = nxt if nxt is not None else 10.0
        else:
            nxt = offsets[i]
    # A median does not step sideways: where the matched carriageway jumps (a roundabout exit,
    # a slip road) the offset may change by at most a quarter metre per metre of road, forward and
    # back, so the barrier never crosses the bus's lane. (It did at Mirpur 10: an invisible wall
    # square across the road at 2,345 m.)
    RATE = 0.25
    for order in (range(1, n), range(n - 2, -1, -1)):
        for i in order:
            j = i - 1 if order.step > 0 else i + 1
            ds = math.dist(route_pts[i], route_pts[j]) * RATE
            offsets[i] = max(offsets[j] - ds, min(offsets[j] + ds, offsets[i]))
    out = []
    for i, (x, z) in enumerate(route_pts):
        (ax, az), (bx, bz) = route_pts[max(0, i - 1)], route_pts[min(n - 1, i + 1)]
        dx, dz = bx - ax, bz - az
        l = math.hypot(dx, dz) or 1.0
        rx, rz = dz / l, -dx / l
        out.append((x + rx * offsets[i], z + rz * offsets[i]))
    return out, room, matched


def segment_hit(a, b, c, d):
    """Where segment a-b crosses segment c-d: (t along a-b, u along c-d) in [0, 1], or None."""
    (ax, az), (bx, bz), (cx, cz), (dx, dz) = a, b, c, d
    rx, rz, sx, sz = bx - ax, bz - az, dx - cx, dz - cz
    den = rx * sz - rz * sx
    if abs(den) < 1e-9:
        return None
    qx, qz = cx - ax, cz - az
    t = (qx * sz - qz * sx) / den
    u = (qx * rz - qz * rx) / den
    return (t, u) if 0.0 <= t <= 1.0 and 0.0 <= u <= 1.0 else None


def route_crossings(cross, junctions, centrelines, route, route_back, rpts_route):
    """The cross streets with an officer (TrafficSim.Junction): for each junction node where a main
    street crosses the route (the `cross` set), the crossing street's polyline 150 m either side of
    the node, and where it cuts each leg's centreline (S on the leg, S on the cross street). OSM
    splits ways at junctions, so the street is stitched from the arms leaving the node: the two most
    opposite arms make a crossing, one arm a T, which is run into the junction and 20 m out. A dual
    carriageway is cut twice by the same street, once per leg; the same officer works both. Cross
    streets within 25 m of each other are one junction (the widest wins)."""
    legs = []
    for r in (route, route_back):
        pts = [tuple(p) for p in r["points"]]
        legs.append((pts, arc_length(pts)))
    by_node = {}
    for w in centrelines:
        if w["class"] in ("primary", "primary_link", "secondary", "secondary_link", "tertiary", "tertiary_link", "trunk", "trunk_link"):
            for nd in w["nodes"]:
                by_node.setdefault(nd, []).append(w)
    node_at = {tuple(xy): nd for nd, xy in junctions.items()}

    def route_dir(x, z):
        i = min(range(len(rpts_route)), key=lambda j: math.dist(rpts_route[j], (x, z)))
        (ax, az), (bx, bz) = rpts_route[max(0, i - 1)], rpts_route[min(len(rpts_route) - 1, i + 1)]
        l = math.hypot(bx - ax, bz - az) or 1.0
        return ((bx - ax) / l, (bz - az) / l)

    def walk(way, k, step, limit=150.0):
        """The street's points from index k of a way outward in one direction, up to limit metres,
        carrying on into the next way where this one ends (OSM splits a street at every junction):
        the way through the end node that keeps the direction best."""
        got, left, seen = [], limit, {way["id"]}
        pts = [tuple(p) for p in way["points"]]
        i = k
        while left > 0:
            if not (0 <= i + step < len(pts)):
                # The end of this way: carry on along another way through its end node.
                end_node = way["nodes"][0] if step < 0 else way["nodes"][-1]
                prev = got[-2] if len(got) >= 2 else (got[-1] if got else pts[k])
                here = pts[i]
                hx, hz = here[0] - prev[0], here[1] - prev[1]
                hl = math.hypot(hx, hz) or 1.0
                best = None
                for w2 in by_node.get(end_node, []):
                    if w2["id"] in seen:
                        continue
                    idx2 = w2.get("node_index", {})
                    if end_node not in idx2:
                        continue
                    p2 = [tuple(p) for p in w2["points"]]
                    k2 = idx2[end_node]
                    for step2 in (-1, 1):
                        if not (0 <= k2 + step2 < len(p2)):
                            continue
                        nx, nz = p2[k2 + step2][0] - here[0], p2[k2 + step2][1] - here[1]
                        nl = math.hypot(nx, nz) or 1.0
                        dot = (hx * nx + hz * nz) / (hl * nl)
                        if dot > 0.5 and (best is None or dot > best[0]):
                            best = (dot, w2, k2, step2, p2)
                if best is None:
                    break
                _, way, i, step, pts = best
                seen.add(way["id"])
                continue
            d = math.dist(pts[i], pts[i + step])
            if d > left:
                f = left / d
                got.append((pts[i][0] + (pts[i + step][0] - pts[i][0]) * f, pts[i][1] + (pts[i + step][1] - pts[i][1]) * f))
                break
            got.append(pts[i + step]); left -= d; i += step
        return got

    out = []
    for cx, cz in sorted(cross):
        nd = node_at.get((cx, cz))
        if nd is None:
            continue
        dx, dz = route_dir(cx, cz)
        arms = []                                   # (direction away from the node, points outward, way)
        for w in by_node.get(nd, []):
            idx = w.get("node_index", {})
            if nd not in idx:
                continue
            k = idx[nd]
            for step in (-1, 1):
                arm = walk(w, k, step)
                if len(arm) < 1 or math.dist(arm[0], (cx, cz)) < 1.0 and len(arm) < 2:
                    continue
                far = arm[min(len(arm) - 1, 1)] if math.dist(arm[0], (cx, cz)) < 3.0 else arm[0]
                l = math.hypot(far[0] - cx, far[1] - cz) or 1.0
                d = ((far[0] - cx) / l, (far[1] - cz) / l)
                if abs(d[0] * dx + d[1] * dz) > 0.7:
                    continue                        # along the route: our own road or its continuation
                arms.append((d, arm, w))
        if not arms:
            continue
        # A crossing: the two arms most opposite each other. Otherwise a T: one arm, into the junction.
        best = None
        for i in range(len(arms)):
            for j in range(i + 1, len(arms)):
                dot = arms[i][0][0] * arms[j][0][0] + arms[i][0][1] * arms[j][0][1]
                if dot < -0.5 and (best is None or dot < best[0]):
                    best = (dot, i, j)
        if best is not None:
            a, b = arms[best[1]], arms[best[2]]
            line = list(reversed(a[1])) + [(cx, cz)] + b[1]
            width = max(a[2]["width"], b[2]["width"])
            name = a[2]["name"] or b[2]["name"] or a[2]["name_bn"] or "cross street"
        else:
            a = max(arms, key=lambda t: t[2]["width"])
            tail = (cx - a[0][0] * 20.0, cz - a[0][1] * 20.0)
            line = list(reversed(a[1])) + [(cx, cz), tail]
            width = a[2]["width"]
            name = a[2]["name"] or a[2]["name_bn"] or "cross street"
        if arc_length(line)[-1] < 40.0:
            continue
        # Orient a crossing left to right across the way out: cross traffic reaches the out
        # carriageway first, as the sandbox's does (Junction.FirstOfPair). A T keeps its direction.
        rx_, rz_ = dz, -dx
        if best is not None and (line[-1][0] - line[0][0]) * rx_ + (line[-1][1] - line[0][1]) * rz_ < 0:
            line.reverse()
        along_line = arc_length(line)
        entry = {"name": name, "x": round(cx, 1), "z": round(cz, 1), "width": width, "tee": best is None,
                 "xz": [round(c, 1) for p in line for c in p]}
        for leg_name, (lpts, lalong) in zip(("out", "back"), legs):
            hit_s, hit_cs = -1.0, -1.0
            for i in range(len(lpts) - 1):
                if math.dist(lpts[i], (cx, cz)) > 120.0 and math.dist(lpts[i + 1], (cx, cz)) > 120.0:
                    continue
                for j in range(len(line) - 1):
                    h = segment_hit(lpts[i], lpts[i + 1], line[j], line[j + 1])
                    if h is not None:
                        t, u = h
                        hit_s = lalong[i] + t * math.dist(lpts[i], lpts[i + 1])
                        hit_cs = along_line[j] + u * math.dist(line[j], line[j + 1])
                        break
                if hit_s >= 0:
                    break
            entry[leg_name + "S"] = round(hit_s, 1)
            entry[leg_name + "CrossS"] = round(hit_cs, 1)
        if entry["outS"] < 0 and entry["backS"] < 0:
            continue
        # Cross traffic needs road before its first box to be born on: a street whose junction sits at
        # its start (a short stub the map ends at the next corner) is no junction for the sim.
        first_box = min(v for v in (entry["outCrossS"], entry["backCrossS"]) if v >= 0)
        if first_box < 30.0:
            continue
        twin = next((o for o in out if math.hypot(o["x"] - cx, o["z"] - cz) < 25.0), None)
        if twin is not None:
            if entry["width"] > twin["width"]:
                out.remove(twin); out.append(entry)
            continue
        out.append(entry)
    out.sort(key=lambda c: c["outS"] if c["outS"] >= 0 else 1e9 - c["backS"])
    return out


def cut_at_junctions(points, gaps, half_gap=14.0):
    """Split a polyline into pieces that stay clear of the given points: the median barrier opens
    where a street crosses, as a real one does. Pieces shorter than a bus are dropped."""
    pieces, piece = [], []
    def near(x, z):
        return any((x - gx) ** 2 + (z - gz) ** 2 < half_gap * half_gap for gx, gz in gaps)
    for i in range(len(points) - 1):
        (x0, z0), (x1, z1) = points[i], points[i + 1]
        seg = math.hypot(x1 - x0, z1 - z0)
        steps = max(1, int(seg / 4.0))
        for k in range(steps + (1 if i == len(points) - 2 else 0)):
            f = k / steps
            p = (x0 + (x1 - x0) * f, z0 + (z1 - z0) * f)
            if near(*p):
                if len(piece) > 1:
                    pieces.append(piece)
                piece = []
            else:
                piece.append(p)
    if len(piece) > 1:
        pieces.append(piece)
    return [pc for pc in pieces if math.dist(pc[0], pc[-1]) > 12.0]


def main(osm_path, out_dir, building_radius):
    os.makedirs(out_dir, exist_ok=True)
    nodes, ways, stop_nodes = load(osm_path)
    local = {nid: to_local(lat, lon) for nid, (lat, lon) in nodes.items()}

    # Roads and centrelines.
    road_ways = []
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
        main = tags["highway"] in ("primary", "primary_link", "secondary", "secondary_link", "tertiary", "tertiary_link", "trunk", "trunk_link")
        node_index = {}
        if main:
            # Mapped node jitter puts 2 m jogs in a carriageway; the real road has none. Smooth the main
            # roads (ends fixed, so junctions stay put) and remember where each node landed on the
            # smoothed line, so the route can be stitched from these same points.
            raw = pts
            pts = [tuple(q) for q in smooth_polyline(raw)]
            for r in refs:
                if r in local:
                    rx_, rz_ = local[r]
                    node_index[r] = min(range(len(pts)), key=lambda k: (pts[k][0] - rx_) ** 2 + (pts[k][1] - rz_) ** 2)
        oneway = is_oneway(tags)
        if tags.get("oneway") == "-1":
            pts.reverse()                              # drawn against the flow: make node order the flow
            refs = list(reversed(refs))
            node_index = {r: len(pts) - 1 - k for r, k in node_index.items()}
        road_ways.append((pts, width, tags, oneway))
        centrelines.append({
            "id": wid, "class": tags["highway"], "name": tags.get("name", ""), "oneway_flow": oneway,
            "name_bn": tags.get("name:bn", ""), "width": width,
            "lanes": tags.get("lanes", ""), "oneway": tags.get("oneway", "no"),
            "nodes": refs, "points": [[round(x, 2), round(z, 2)] for x, z in pts],
            "node_index": node_index,
        })
    junctions = {r: list(local[r]) for r, n in node_use.items() if n >= 3 and r in local}

    # The route first: everything else is kept, chunked and dressed by where it stands along it.
    route = main_road_route(centrelines, junctions)
    rpts_route = [tuple(p) for p in route["points"]]
    along = arc_length(rpts_route)
    route["along"] = [round(v, 1) for v in along]
    # The way back: the markers in reverse order, so the one-way rule puts it on the other carriageway.
    route_back = main_road_route(centrelines, junctions, list(reversed(MARKERS)))
    rpts_back = [tuple(p) for p in route_back["points"]]
    along_back = arc_length(rpts_back)
    route_back["along"] = [round(v, 1) for v in along_back]
    GRID = 50.0
    route_grid = {}
    for i, (x, z) in enumerate(rpts_route):
        route_grid.setdefault((int(x // GRID), int(z // GRID)), []).append(i)

    def nearest_route_point(x, z, radius):
        """Index of the nearest route point within radius, else None. Cells of the grid around."""
        r = int(radius // GRID) + 1
        gx, gz = int(x // GRID), int(z // GRID)
        best, bd = None, radius * radius
        for cx in range(gx - r, gx + r + 1):
            for cz in range(gz - r, gz + r + 1):
                for i in route_grid.get((cx, cz), ()):
                    px, pz = rpts_route[i]
                    d = (px - x) ** 2 + (pz - z) ** 2
                    if d < bd:
                        best, bd = i, d
        return best

    def chunk_of(x, z):
        i = nearest_route_point(x, z, 2000.0)
        return int(along[i] // CHUNK) if i is not None else 0

    def split_by_chunk(points):
        """A long ribbon (the viaduct deck, the median) cut where it crosses from one chunk to the
        next, the border point in both pieces, so each chunk's scene holds its own stretch."""
        pieces, piece, current = [], [], None
        for p in points:
            c = chunk_of(p[0], p[1])
            if current is None or c == current:
                piece.append(p)
            else:
                pieces.append((current, piece))
                piece = [piece[-1], p]
            current = c
        if len(piece) >= 2:
            pieces.append((current, piece))
        return pieces

    # Road surfaces as segments in a 50 m grid, so a footprint can ask cheaply whether it stands on
    # a road. Mapped footprints do overlap mapped roads here and there, and a wall across the
    # carriageway is worse than a missing house.
    road_grid = {}
    for way in centrelines:
        # Only the roads the bus drives: in the lanes the map's footprints and 7 m default widths
        # overlap all the time, and dropping those houses hollows the city out (7,800 of 18,800 went).
        if way["class"] not in ("primary", "primary_link", "secondary", "secondary_link", "tertiary", "tertiary_link", "trunk", "trunk_link"):
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

    roads = Chunked("Roads.obj")
    markings = Chunked("Markings.obj")
    kerbs = Chunked("Kerbs.obj")
    for pts, width, tags, oneway in road_ways:
        verts, quads = ribbon(pts, width)
        # The cell of the way's midpoint: a way crossing a cell border goes to one of them whole.
        mx, mz = pts[len(pts) // 2]
        chunk = chunk_of(mx, mz)
        roads.add(chunk, cell_name("Roads", mx, mz), verts, quads)
        m = ObjWriter()
        add_markings(m, pts, width, tags["highway"], mx, mz, oneway)
        for name, (v, f, _, _) in m.objects.items():
            markings.add(chunk, name, v, f)
        if tags["highway"] in ("primary", "secondary", "tertiary", "trunk"):
            # Pavements: 2 m wide, a kerb high, beside the roads the bus uses. A one-way carriageway
            # has its kerb on the left of the flow only (left-hand traffic); the median is on its right.
            # A pavement stops where it would lie on another road: a way's kerb strip ran on across
            # the next way's carriageway at every junction and the bus stood against it (Mirpur 10).
            for side in ((+1,) if oneway else (+1, -1)):
                line, _ = offset_ribbon(pts, side * (width / 2.0 + 1.0), 0.0)
                centre = [(x, z) for x, _, z in line[::2]]
                run = []
                for q in centre + [None]:
                    if q is not None and not on_a_road(*q):
                        run.append(q)
                        continue
                    if len(run) >= 2:
                        v, f = box_ribbon(run, 0.0, 2.0, KERB_HEIGHT)
                        kerbs.add(chunk, cell_name("Pavement", mx, mz), v, f)
                    run = []

    def footprint_on_a_road(pts):
        # Corners and edge midpoints: a long wall across a road has no corner on it.
        for i, (x, z) in enumerate(pts):
            if on_a_road(x, z):
                return True
            nx, nz = pts[(i + 1) % len(pts)]
            if on_a_road((x + nx) / 2.0, (z + nz) / 2.0):
                return True
        return False

    # Buildings near the route only: the download holds 15,000 footprints and the bus never sees
    # past the second row. --radius widens it.
    buildings = Chunked("Buildings.obj")
    kept = skipped = on_road = 0
    for wid, refs, tags in ways:
        if "building" not in tags or len(refs) < 4 or refs[0] != refs[-1]:
            continue
        pts = [local[r] for r in refs[:-1] if r in local]
        if len(pts) < 3:
            continue
        cx = sum(p[0] for p in pts) / len(pts)
        cz = sum(p[1] for p in pts) / len(pts)
        near = nearest_route_point(cx, cz, building_radius)
        if near is None:
            skipped += 1
            continue
        # An elevated structure (the metro stations: building=train_station, layer=3) straddles the
        # road at viaduct level; built on the deck, not on the carriageway.
        elevated = tags.get("building") == "train_station" or tags.get("layer", "0").lstrip("-").isdigit() and int(tags.get("layer", "0")) >= 1
        if not elevated and footprint_on_a_road(pts):
            on_road += 1
            continue
        if polygon_area(pts) < 0:
            pts.reverse()
        base = RAIL_DECK_HEIGHT if elevated else 0.0
        h = base + (8.0 if elevated else body_safe(building_height(tags)))
        n = len(pts)
        verts = [(x, base, z) for x, z in pts] + [(x, h, z) for x, z in pts]

        faces = []
        for i in range(n):
            j = (i + 1) % n
            faces.append((i, j, n + j, n + i))                     # wall, outward
        faces.extend((n + a, n + b, n + c) for a, b, c in ear_clip(pts))   # roof, upward
        faces.extend((c, b, a) for a, b, c in ear_clip(pts)) if elevated else None   # floor, downward
        storeys = 0.0 if elevated else round((h - base) / LEVEL_HEIGHT)
        seed = (int(wid) * 2654435761 % 1000) / 1000.0            # stable per building across runs
        buildings.add(int(along[near] // CHUNK), cell_name("Blocks", cx, cz), verts, faces, uv=(seed, storeys))
        kept += 1

    # The metro viaduct runs down the middle of the main road where the mapped metro line follows
    # it (RESEARCH.md: MRT Line 6 on Rokeya Sarani; it leaves the route at TSC for Motijheel), so it
    # is built along the road's own median line, not the mapped railway, which sits a few metres
    # off it: deck, piers every 30 m, and the median barrier between the carriageways. The barrier
    # exists only where the route is a dual carriageway.
    metro_grid = {}
    for wid, refs, tags in ways:
        if tags.get("railway") in ("subway", "light_rail", "rail", "construction"):
            for r in refs:
                if r in local:
                    x, z = local[r]
                    metro_grid.setdefault((int(x // GRID), int(z // GRID)), []).append((x, z))
    # Dense enough to ask per route point: resample the metro ways every 10 m.
    for wid, refs, tags in ways:
        if tags.get("railway") in ("subway", "light_rail", "rail", "construction"):
            pts = [local[r] for r in refs if r in local]
            for (x0, z0), (x1, z1) in zip(pts, pts[1:]):
                seg = math.hypot(x1 - x0, z1 - z0)
                for k in range(1, int(seg // 10.0)):
                    f = k * 10.0 / seg
                    x, z = x0 + (x1 - x0) * f, z0 + (z1 - z0) * f
                    metro_grid.setdefault((int(x // GRID), int(z // GRID)), []).append((x, z))

    def metro_point(x, z, within=45.0):
        """The nearest point of the mapped metro line within reach, or None."""
        gx, gz = int(x // GRID), int(z // GRID)
        best, bd = None, within * within
        for cx in range(gx - 1, gx + 2):
            for cz in range(gz - 1, gz + 2):
                for px, pz in metro_grid.get((cx, cz), ()):
                    d = (px - x) ** 2 + (pz - z) ** 2
                    if d < bd:
                        best, bd = (px, pz), d
        return best

    def metro_near(x, z, within=45.0):
        return metro_point(x, z, within) is not None

    def runs(flags):
        """Index ranges [i, j) where flags are true, so a ribbon is built piece by piece."""
        out, start = [], None
        for i, f in enumerate(list(flags) + [False]):
            if f and start is None:
                start = i
            elif not f and start is not None:
                out.append((start, i))
                start = None
        return out

    rail = Chunked("Rail.obj")
    walls = Chunked("Walls.obj")
    median = Chunked("Median.obj")
    rpts, room, matched = median_line(route, centrelines)
    dual = route["dual"]
    under_metro = [dual[i] and metro_near(x, z) for i, (x, z) in enumerate(rpts)]
    # Where the median was found between two carriageways the piers stand on it; where it was only
    # carried on (a roundabout, a junction) they stand on the mapped metro line instead: at Mirpur 10
    # the carried line crossed the way back round the roundabout and a pier stood in its lane.
    # The mapped line is not trusted onto a carriageway: at Khamarbari it runs along the outbound
    # centreline itself (one of the two is drawn wrong), and a pier there stood in the lane.
    legs_xz = [([tuple(p) for p in r["points"]], r["along"], r["widthAt"]) for r in (route, route_back)]

    def clear_of_the_road(x, z, margin):
        """Is a point this far beyond the edge of both legs' carriageways, at their width there? The
        route's one width is the narrowest way on it; Kazi Nazrul Islam Avenue is wider, and a pier
        that cleared the narrow width stood in its outer lane at Karwan Bazar."""
        for pts, along, width_at in legs_xz:
            hit = project_on_route(pts, along, x, z)
            if hit is None:
                continue
            here = width_at[max(0, min(len(width_at) - 1, bisect.bisect_right(along, hit[0]) - 1))]
            if hit[2] < here / 2.0 + margin:
                return False
        return True

    # The pier line: the median where it was found and stands clear of both carriageways, else the
    # metro line where that is clear, else the median line with no pier at that point (the deck
    # alone). Round the Mirpur 10 roundabout the two arcs are 11 m apart and a matched median
    # still put a pier inside the way back's arc.
    pier_pts = []
    for i, (x, z) in enumerate(rpts):
        mp = None if matched[i] and clear_of_the_road(x, z, RAIL_PIER_SIZE) else metro_point(x, z)
        pier_pts.append(mp if mp is not None and clear_of_the_road(*mp, RAIL_PIER_SIZE) else rpts[i])
    # The barrier likewise: no wall where the median line is inside the way back's carriageway.
    room = [r and clear_of_the_road(x, z, MEDIAN_WIDTH / 2.0 + 0.5) for r, (x, z) in zip(room, rpts)]
    piers = 0
    for i0, i1 in runs(under_metro):
        for chunk, piece in split_by_chunk(pier_pts[i0:i1]):
            top, quads = offset_ribbon(piece, 0.0, RAIL_DECK_WIDTH, RAIL_DECK_HEIGHT)
            bottom = [(x, RAIL_DECK_HEIGHT - RAIL_DECK_THICKNESS, z) for x, _, z in top]
            m = len(top)
            faces = list(quads)                                            # top, seen from above
            faces += [tuple(m + i for i in reversed(q)) for q in quads]    # bottom, seen from below
            for i in range(0, m - 2, 2):
                faces.append((i, m + i, m + i + 2, i + 2))                 # left side
                faces.append((i + 3, m + i + 3, m + i + 1, i + 1))         # right side
            rail.add(chunk, "Viaduct", top + bottom, faces)
        walked = 0.0
        piece = pier_pts[i0:i1]
        for i in range(len(piece) - 1):
            (x0, z0), (x1, z1) = piece[i], piece[i + 1]
            seg = math.hypot(x1 - x0, z1 - z0)
            t = walked
            while t < seg:
                f = t / seg
                px, pz = x0 + (x1 - x0) * f, z0 + (z1 - z0) * f
                if not clear_of_the_road(px, pz, RAIL_PIER_SIZE):
                    t += RAIL_PIER_EVERY
                    continue                                   # the deck crosses a carriageway here: no pier
                sz = RAIL_PIER_SIZE / 2.0
                base = [(px - sz, 0.0, pz - sz), (px + sz, 0.0, pz - sz), (px + sz, 0.0, pz + sz), (px - sz, 0.0, pz + sz)]
                topv = [(x, RAIL_DECK_HEIGHT - RAIL_DECK_THICKNESS, z) for x, _, z in base]
                pf = [(0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
                rail.add(chunk_of(px, pz), "Piers", base + topv, pf)
                piers += 1
                t += RAIL_PIER_EVERY
            walked = t - seg
    # The barrier opens where a main street crosses the route (a junction node of the route's
    # carriageway shared with a primary/secondary/tertiary way that is not part of the route).
    route_point_set = set(rpts_route)
    route_nodes = set()
    for w in centrelines:
        if w["class"] in ("primary", "trunk") and any(tuple(p) in route_point_set for p in w["points"]):
            route_nodes.update(w["nodes"])
    cross = set()
    for w in centrelines:
        if w["class"] not in ("primary", "primary_link", "secondary", "secondary_link", "tertiary", "tertiary_link"):
            continue
        shared = [nd for nd in w["nodes"] if nd in route_nodes]
        if shared and not all(nd in route_nodes for nd in w["nodes"]):
            for nd in shared:
                if nd in junctions:
                    cross.add(tuple(junctions[nd]))
    for i0, i1 in runs([d and r for d, r in zip(dual, room)]):
        for whole in cut_at_junctions(rpts[i0:i1], cross):
            for chunk, piece in split_by_chunk(whole):
                v, f = box_ribbon(piece, 0.0, MEDIAN_WIDTH, MEDIAN_HEIGHT)
                median.add(chunk, "Median", v, f)
                v, f = box_ribbon(piece, 0.0, MEDIAN_WIDTH, WALL_HEIGHT)
                walls.add(chunk, "MedianWall", v, f)
    crossings = route_crossings(cross, junctions, centrelines, route, route_back, rpts_route)
    with open(os.path.join(out_dir, "crossings.json"), "w") as f:
        json.dump({"attribution": "Map data (c) OpenStreetMap contributors, ODbL", "crossings": crossings}, f, indent=1, ensure_ascii=False)
    print(f"crossings: {len(crossings)} cross streets with an officer ({sum(1 for c in crossings if c['outS'] >= 0)} on the way out, {sum(1 for c in crossings if c['backS'] >= 0)} on the way back)")
    print(f"median barrier opens at {len(cross)} crossings, no room for one at {sum(1 for d, r in zip(dual, room) if d and not r)} of {sum(dual)} dual route points; viaduct over {sum(under_metro)} of {len(rpts)} route points, {piers} piers")

    for w in (roads, buildings, rail, markings, kerbs, walls, median):
        w.write(out_dir)
    with open(os.path.join(out_dir, "centrelines.json"), "w") as f:
        json.dump({"origin": {"lat": ORIGIN_LAT, "lon": ORIGIN_LON}, "frame": "x east, z north, metres",
                   "attribution": "Map data (c) OpenStreetMap contributors, ODbL",
                   "roads": centrelines, "junctions": junctions}, f)
    # Where the opposite carriageway is, per route point: the median line's offset to the right, and
    # the far kerb beyond it (the other carriageway is as wide as ours, plus its pavement). The drive
    # treats everything up to the far kerb as road: crossing to the wrong side is the game, not a wall.
    # On a two-way street the oncoming lane is inside our own width, so the far kerb is our own.
    route["farEdge"] = far_edges(route, rpts)
    with open(os.path.join(out_dir, "route.json"), "w") as f:
        json.dump(route, f)
    route_back["farEdge"] = far_edges(route_back, median_line(route_back, centrelines)[0])
    with open(os.path.join(out_dir, "route_back.json"), "w") as f:
        json.dump(route_back, f)
    stops = route_stops(stop_nodes, route, route_back)
    with open(os.path.join(out_dir, "stops.json"), "w") as f:
        json.dump({"attribution": "Map data (c) OpenStreetMap contributors, ODbL", "stops": stops}, f, indent=1, ensure_ascii=False)
    with open(os.path.join(out_dir, "markers.json"), "w") as f:
        json.dump([{"name": n, "x": round(to_local(la, lo)[0], 1), "z": round(to_local(la, lo)[1], 1)} for n, la, lo in MARKERS], f, indent=1)
    # The chunk index for the streamer: each chunk's stretch of the route resampled every 50 m, flat,
    # so a distance to the nearest sample is within 25 m of the distance to the line.
    samples = {}
    step = 50.0
    for i in range(len(rpts_route) - 1):
        (x0, z0), (x1, z1) = rpts_route[i], rpts_route[i + 1]
        seg = along[i + 1] - along[i]
        t = 0.0
        while t < seg or (i == len(rpts_route) - 2 and t <= seg):
            f = t / seg if seg else 0.0
            sv = along[i] + t
            samples.setdefault(int(sv // CHUNK), []).extend((round(x0 + (x1 - x0) * f, 1), round(z0 + (z1 - z0) * f, 1)))
            t += step
    chunks = [{"index": c, "folder": f"Chunk_{c:02d}", "s0": c * CHUNK, "s1": min((c + 1) * CHUNK, along[-1]), "xz": xz}
              for c, xz in sorted(samples.items())]
    with open(os.path.join(out_dir, "chunks.json"), "w") as f:
        json.dump({"chunks": chunks}, f)
    for folder in glob.glob(os.path.join(out_dir, "Chunk_[0-9][0-9]")):
        if int(folder[-2:]) >= len(chunks):
            shutil.rmtree(folder)
            if os.path.exists(folder + ".meta"):
                os.remove(folder + ".meta")

    print(f"roads: {len(centrelines)} ways, {roads.triangles} triangles in {len(roads.objects)} cells; {len(junctions)} junction nodes")
    print(f"buildings: {kept} kept within {building_radius:.0f} m of the route ({skipped} beyond, {on_road} dropped for standing on a road), {buildings.triangles} triangles in {len(buildings.objects)} cells")
    print(f"rail: {rail.triangles} triangles; markings {markings.triangles}; kerbs {kerbs.triangles}; median {median.triangles}")
    print(f"route: {len(route['points'])} points, {along[-1]:.0f} m, width {route['width']:.0f} m, {len(chunks)} chunks of {CHUNK:.0f} m")
    print(f"route back: {len(route_back['points'])} points, {along_back[-1]:.0f} m")
    print(f"stops: {len(stop_nodes)} OSM stop nodes in the download, {sum(1 for st in stops if st['source'] == 'osm')} on the route "
          f"({sum(1 for st in stops if st['outS'] >= 0)} out, {sum(1 for st in stops if st['backS'] >= 0)} back), "
          f"{sum(1 for st in stops if st['hot'])} hot from the research")
    for c in chunks:
        tri = sum(w.writers.get(c["index"], ObjWriter()).triangles for w in (roads, buildings, rail, markings, kerbs, median))
        print(f"  {c['folder']}: {c['s0']:.0f}-{c['s1']:.0f} m, {tri} triangles")
    print(f"total {roads.triangles + buildings.triangles + rail.triangles + markings.triangles + kerbs.triangles + median.triangles} triangles; three chunks loaded at a time (budget 400k, docs/OSM_IMPORT_PLAN.md)")


def overpass_query():
    """The Overpass download for tools/osm/fetch.sh: everything within a corridor along the markers,
    not a bounding box, since the box around a 15 km diagonal route would hold 200,000 footprints."""
    line = ",".join(f"{la},{lo}" for _, la, lo in MARKERS)
    return ("[out:xml][timeout:600];("
            f'way["highway"](around:{CORRIDOR_ROADS:.0f},{line});'
            f'way["building"](around:{CORRIDOR_BUILDINGS:.0f},{line});'
            f'way["railway"](around:{CORRIDOR_BUILDINGS:.0f},{line});'
            # Bus stops are nodes of their own, not part of any way: ask for them by name.
            f'node["highway"="bus_stop"](around:{CORRIDOR_STOPS:.0f},{line});'
            f'node["public_transport"="platform"]["bus"="yes"](around:{CORRIDOR_STOPS:.0f},{line});'
            f'node["amenity"="bus_station"](around:{CORRIDOR_STOPS:.0f},{line});'
            ");(._;>;);out body;")


if __name__ == "__main__":
    if len(sys.argv) == 2 and sys.argv[1] == "--query":
        print(overpass_query())
        sys.exit(0)
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(2)
    radius = float(sys.argv[3]) if len(sys.argv) > 3 else 300.0
    main(sys.argv[1], sys.argv[2], radius)
