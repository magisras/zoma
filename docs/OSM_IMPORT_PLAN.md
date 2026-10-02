# OSM corridor import plan — Mirpur 12 toward Azimpur, first 2–3 km as grey boxes

Milestone 1 of `README.md`. Goal: a Unity scene with the real road geometry and block massing of
the first stretch of the Mirpur 12–Azimpur route, as untextured grey meshes, light enough to run on
a MacBook Air M3 with the editor open. No traffic, no bus yet.

Pipeline: **OpenStreetMap → Blender (blosm add-on) → FBX → Unity**. Everything in it is free.
OSM data is ODbL-licensed: "© OpenStreetMap contributors" goes in the game credits and stays in
this file.

## 1. Which 2–3 km

The full route is ~15 km. The first chunk is the northern residential end, where the morning bus
fills up and most of the deliberate waiting happens (RESEARCH.md, "The full/empty dial"):

| Point | What it is | Approx. coordinates (lat, lon) |
| --- | --- | --- |
| Mirpur 12 | bus stand, route origin, Pallabi | 23.828, 90.364 |
| Mirpur 11 | junction, market crowd | 23.819, 90.366 |
| Mirpur 10 | the roundabout, metro station above, biggest junction on this stretch | 23.807, 90.368 |
| Kazipara | metro station, end of chunk 1 if we want 3 km | 23.799, 90.371 |

The road is Begum Rokeya Sarani (Rokeya Avenue). MRT Line 6 (the metro) runs on a viaduct down the
middle of it from Mirpur 12 south, which is the metro competition RESEARCH.md describes. The viaduct
piers are a real obstacle in the road and should be in the grey box.

Suggested bounding box, to confirm on openstreetmap.org before exporting:

```
north 23.832   south 23.796   west 90.356   east 90.378
```

That is roughly 4 km × 2.2 km, generous on the sides so junction side streets have somewhere to go.
If it imports too heavy, trim east/west to 90.360–90.375. The coordinates above are read off the
map by eye; treat them as "about here", not survey data.

## 2. Tools (all free)

| Tool | Version | Notes |
| --- | --- | --- |
| Blender | 4.2 LTS or newer | runs fine on the M3 Air |
| blosm (formerly blender-osm) | base version from GitHub, `vvoovv/blosm` | free. The premium version on Gumroad adds textured buildings and Google 3D tiles; we do **not** need it for grey boxes. It is a paid asset, so it's an "ask first" item if it ever comes up. |
| Unity | 6 LTS, URP | |

Install blosm: download the release zip, Blender > Edit > Preferences > Add-ons > Install, then set
its "data directory" to a folder outside the Unity project (e.g. `~/TwentyTonsArt/osm`). OSM files
and `.blend` working files stay out of the repo; only exported FBX goes in (later via Git LFS, see
`.gitattributes`).

## 3. Blender steps

1. **New file, metric, 1 unit = 1 m.** Delete the default cube. Blender is Z-up; Unity is Y-up.
   The FBX exporter handles this if we export with the settings in step 7.
2. **Import OSM.** N-panel > blosm tab. Mode "OpenStreetMap". Either type the bounding box from
   section 1 or use "select" (opens a browser map). Tick:
   - Buildings — on, "extrude" from `building:levels` (3 m per level default; blosm uses 3 m).
     Where OSM has no level tag blosm uses its default; set that default to 2 levels. Mirpur blocks
     are mostly 3–6 storeys of concrete, so grey boxes read right.
   - Highways (roads) — on, as **meshes** with width, so we get a drivable surface.
   - Railways — on, to catch the MRT viaduct alignment if mapped.
   - Water, forests, vegetation — off. Terrain — off (Dhaka is flat; a plane is enough).
   Click Import. Expect 10–60 s and a few thousand buildings.
3. **If the Overpass download fails or times out** (common): download the same bbox as a `.osm` file
   via https://overpass-api.de/api/map?bbox=90.356,23.796,90.378,23.832 (note: Overpass wants
   `west,south,east,north`) and import it with blosm's "file" mode instead.
4. **Clean the buildings.** Select all buildings, Join (Ctrl+J) per ~200 m block so Unity gets a few
   dozen meshes, not thousands. Remove all materials. Apply a Decimate (planar, 5°) if the triangle
   count is above ~300k. Target: whole chunk under 400k triangles.
5. **Clean the roads.** blosm produces one mesh per highway class. Join them, then split the result
   into chunks of ~250 m along the corridor (Edit mode, select, P > Selection). Chunks let Unity cull
   and later stream. Keep the main road, the junction side streets, and nothing narrower than
   `residential`.
6. **Centrelines for Milestone 3.** Traffic needs corridors (centreline + width), not just a road
   surface. Before deleting anything, select the main-road highway *curve* objects blosm created
   (import with "highways as curves" once more into a second collection if needed) and run the small
   script in section 6 to export them as CSV. This step is prep for Milestone 3; do it now so we
   don't redo the import.
7. **Export FBX**, two files: `Corridor01_Roads.fbx`, `Corridor01_Buildings.fbx`.
   Settings: Selected Objects, Scale 1.0, Apply Scalings = "FBX Units Scale", Forward = **-Z**,
   Up = **Y**, "Apply Transform" ticked (the experimental checkbox; it bakes the axis swap so Unity
   sees a clean rotation of 0), Mesh only, no animation. Set the Blender scene origin at the Mirpur 12
   end before exporting so Unity's origin is the start of the route.

## 4. Unity steps

1. Create the project from Unity Hub as **Universal 3D** (URP template). See `PROGRESS.md` for how
   that fits with this repo.
2. Drop the two FBX into `Assets/World/Corridor01/` (create the folder). Import settings: Scale
   Factor 1, Convert Units on, Generate Colliders **on** for roads (we drive on them), off for
   buildings (add a few box colliders by hand only where the bus can reach), Read/Write off, Mesh
   Compression medium, Generate Lightmap UVs off.
3. New scene `Assets/Scenes/Corridor01_Greybox.unity`. Drag both FBX in at (0,0,0). One
   directional light, no realtime shadows on buildings, a flat grey URP Lit material for everything
   (one material = one batch), a big ground plane at y = -0.05 so there are no holes between roads
   and blocks. Mark everything Static.
4. Scene budget for the laptops: < 400k triangles, < 150 draw calls after static batching, no
   post-processing yet. Check with the Stats overlay and the Frame Debugger.
5. Add placeholder empties where the demand zones and extortion points will go (Milestone 4/5):
   Mirpur 12 stand, Mirpur 11, Mirpur 10 roundabout, Kazipara. Just named GameObjects for now.
6. Add a `Credits` text asset or scene note: "Map data © OpenStreetMap contributors (ODbL)".

## 5. Scale and precision

At true scale a 3 km chunk sits comfortably inside float precision (problems start past ~10 km from
origin). For the full 15 km route later: chunk scenes of ~1 km loaded additively, plus a floating
origin that shifts the world back when the bus passes ~2 km from origin. Not needed for Milestone 1,
but keeping chunk meshes at ~250 m now makes that split cheap later.

## 6. Centreline export script (Blender, Python)

Paste into Blender's Scripting tab with the main-road curve objects selected. Writes one CSV per
curve with x, y, z in metres (Blender axes; the Unity importer swaps Y and Z). This is the input
for Unity Splines in Milestone 3.

```python
import bpy, csv, os

out_dir = os.path.expanduser("~/TwentyTonsArt/centrelines")
os.makedirs(out_dir, exist_ok=True)

for obj in bpy.context.selected_objects:
    if obj.type != "CURVE":
        continue
    path = os.path.join(out_dir, f"{obj.name}.csv")
    with open(path, "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["x", "y", "z"])
        for spline in obj.data.splines:
            points = spline.bezier_points if spline.type == "BEZIER" else spline.points
            for p in points:
                world = obj.matrix_world @ p.co.to_3d()
                w.writerow([round(world.x, 3), round(world.y, 3), round(world.z, 3)])
    print("wrote", path)
```

## 7. Hand-dressing (later, not Milestone 1)

Stops, markets and the roundabout get dressed from the Fraser ride-along and Mapillary street
photos (RESEARCH.md, "Map plan"). Mirpur 10 in particular: buses stop mid-road under the metro
station, rickshaws fill the ring. That is Milestone 4 material.

## Checklist

- [ ] Install Blender 4.2 LTS and blosm base
- [ ] Confirm bbox on openstreetmap.org; adjust section 1 if the road runs out of the box
- [ ] Import, clean, export two FBX (section 3)
- [ ] Export centreline CSVs (section 6) and keep them in `~/TwentyTonsArt/centrelines`
- [ ] Unity scene `Corridor01_Greybox` renders under budget on the MacBook Air
- [ ] "© OpenStreetMap contributors" in the scene and README
