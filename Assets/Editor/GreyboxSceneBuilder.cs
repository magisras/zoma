using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwentyTons.EditorTools
{
    /// <summary>
    /// Builds the milestone 1 scene from the grey-box files tools/osm/greybox.py wrote
    /// (docs/OSM_IMPORT_PLAN.md, section 4), so the scene is reproducible from the terminal:
    ///
    ///   unity run . -- -executeMethod TwentyTons.EditorTools.GreyboxSceneBuilder.Build
    ///
    /// or Twenty Tons > Build Corridor01 grey box in the editor. It makes the ground, the light, one
    /// grey material, places the road, block and viaduct meshes, adds colliders to what the bus
    /// touches, puts a named empty at each route marker, and saves Assets/Scenes/Corridor01_Greybox.
    /// Everything is static: the scene budget on the laptops is under 400k triangles and 150
    /// draw calls after static batching.
    /// </summary>
    public static class GreyboxSceneBuilder
    {
        private const string WorldDir = "Assets/World/Corridor01";
        private const string ScenePath = "Assets/Scenes/Corridor01_Greybox.unity";

        [MenuItem("Twenty Tons/Build Corridor01 grey box")]
        public static void Build()
        {
            // One folder per kilometre of route (tools/osm/greybox.py); each becomes a scene of its own
            // that WorldStreamer loads around the bus. The folders are not in git: make world.
            string[] chunkDirs = Directory.GetDirectories(WorldDir, "Chunk_*");
            System.Array.Sort(chunkDirs, string.CompareOrdinal);
            if (chunkDirs.Length == 0) { Debug.LogError("no " + WorldDir + "/Chunk_* folders: run make world first"); return; }
            string[] files = { "Roads.obj", "Buildings.obj", "Rail.obj", "Markings.obj", "Kerbs.obj", "Walls.obj", "Median.obj" };
            foreach (string dir in chunkDirs)
                foreach (string f in files)
                    if (File.Exists(dir + "/" + f)) ConfigureImport(dir.Replace('\\', '/') + "/" + f);
            EnsureLayer("Wheels");
            EnsureLayer("NoWheels");
            AssetDatabase.Refresh();

            // Materials: one per surface, so each is a batch. The procedural skin
            // (Assets/World/Shaders/Surface.shader): a mode per surface, hashed from world position,
            // so the street reads as Mirpur with no textures and no assets to license.
            Material ground = MakeSurface(WorldDir + "/SkinDirt.mat", 4f);
            Material road = MakeSurface(WorldDir + "/SkinAsphalt.mat", 1f);
            Material block = MakeSurface(WorldDir + "/SkinFacade.mat", 0f);
            Material rail = MakeSurface(WorldDir + "/SkinConcrete.mat", 3f);
            Material paint = MakeMaterial(WorldDir + "/GreyboxPaint.mat", new Color(0.92f, 0.92f, 0.88f));
            Material kerb = MakeSurface(WorldDir + "/SkinPavement.mat", 2f);
            Material post = MakeMaterial(WorldDir + "/GreyboxPost.mat", new Color(0.20f, 0.70f, 0.35f));

            // The chunk scenes: only the meshes; light, ground and the bus live in the base scene.
            Directory.CreateDirectory("Assets/Scenes/Chunks");
            var chunkScenes = new List<string>();
            int noWheels = LayerMask.NameToLayer("NoWheels");
            foreach (string dirRaw in chunkDirs)
            {
                string dir = dirRaw.Replace('\\', '/');
                string name = Path.GetFileName(dir);
                Scene chunk = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Place(dir + "/Roads.obj", "Roads", road, withColliders: true);
                Place(dir + "/Buildings.obj", "Blocks", block, withColliders: true);
                Place(dir + "/Rail.obj", "MRT Line 6 viaduct", rail, withColliders: true);
                Place(dir + "/Markings.obj", "Lane markings", paint, withColliders: false);
                Place(dir + "/Kerbs.obj", "Pavements", kerb, withColliders: true);
                // The median barrier and the invisible tall wall over it go on a layer the wheels ignore: a
                // wheel ray that finds the top of a barrier climbs it; the body still meets both (PhysicsBus
                // sets the matrix). The barrier itself is only drawn: with a collider its 1.1 m top face
                // lifts a body that has pushed into the wall by one step, and from there the body climbs.
                var median = Place(dir + "/Median.obj", "Median barrier", kerb, withColliders: false);
                if (median != null) foreach (var t in median.GetComponentsInChildren<Transform>()) t.gameObject.layer = noWheels;
                var walls = Place(dir + "/Walls.obj", "Invisible walls", kerb, withColliders: true);
                if (walls != null) foreach (var r in walls.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
                if (walls != null) foreach (var t in walls.GetComponentsInChildren<Transform>()) t.gameObject.layer = noWheels;
                string path = "Assets/Scenes/Chunks/Corridor01_" + name + ".unity";
                EditorSceneManager.SaveScene(chunk, path);
                chunkScenes.Add(path);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Light: one directional, hard shadows off on buildings (they are flat grey anyway).
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            // Ground: a big plane a little under the roads so there are no holes between blocks,
            // sized to the whole route with a margin.
            RouteBounds bounds = ReadRouteBounds(WorldDir + "/route.json");
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Ground";
            plane.transform.position = new Vector3(bounds.CentreX, -0.05f, bounds.CentreZ);
            plane.transform.localScale = new Vector3((bounds.SizeX + 2000f) / 10f, 1f, (bounds.SizeZ + 2000f) / 10f);   // a Unity plane is 10 m
            plane.GetComponent<MeshRenderer>().sharedMaterial = ground;
            GameObjectUtility.SetStaticEditorFlags(plane, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

            // Route markers: stands and junctions, as named empties for milestones 4 and 5.
            var markers = new GameObject("Route markers");
            string markersJson = File.ReadAllText(WorldDir + "/markers.json");
            foreach (Marker m in JsonUtility.FromJson<MarkerList>("{\"items\":" + markersJson + "}").items)
            {
                var go = new GameObject(m.name);
                go.transform.SetParent(markers.transform);
                go.transform.position = new Vector3(m.x, 0f, m.z);
                // A tall green post at each one, so the next stop shows from down the road.
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pole.name = "Post";
                Object.DestroyImmediate(pole.GetComponent<Collider>());
                pole.transform.SetParent(go.transform, false);
                pole.transform.localPosition = new Vector3(0f, 20f, 0f);
                pole.transform.localScale = new Vector3(2f, 40f, 2f);
                pole.GetComponent<MeshRenderer>().sharedMaterial = post;
            }

            // Bus stops (tools/osm/greybox.py, stops.json): a post and a board on the kerb at each stop
            // of each leg. A research pickup is on both kerbs of a dual road, so it gets a sign per leg;
            // an OSM stop node is one place. Hot stops (the research's pickups and the stands) have
            // the bigger board. Demand zones for milestone 4 start from the same file.
            var stopsRoot = new GameObject("Bus stops");
            Material sign = MakeMaterial(WorldDir + "/GreyboxSign.mat", new Color(0.95f, 0.78f, 0.18f));
            var stops = TwentyTons.Unity.RouteStops.Load(AssetDatabase.LoadAssetAtPath<TextAsset>(WorldDir + "/stops.json"));
            foreach (var st in stops.All)
            {
                for (int leg = 0; leg < 2; leg++)
                {
                    if (st.SOn(leg) < 0f) continue;
                    Vector3 at = st.SignOn(leg);
                    if (leg == 1 && st.SOn(0) >= 0f && Vector3.Distance(at, st.SignOn(0)) < 1f) continue;
                    var go = new GameObject((st.hot ? "Pickup " : "Stop ") + (st.name.Length > 0 ? st.name : st.nameBn) + (leg == 1 ? " (back)" : ""));
                    go.transform.SetParent(stopsRoot.transform);
                    go.transform.position = at;
                    var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pole.name = "Post";
                    Object.DestroyImmediate(pole.GetComponent<Collider>());
                    pole.transform.SetParent(go.transform, false);
                    pole.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                    pole.transform.localScale = new Vector3(0.12f, 2.8f, 0.12f);
                    pole.GetComponent<MeshRenderer>().sharedMaterial = post;
                    var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    board.name = "Board";
                    Object.DestroyImmediate(board.GetComponent<Collider>());
                    board.transform.SetParent(go.transform, false);
                    board.transform.localPosition = new Vector3(0f, st.hot ? 2.7f : 2.5f, 0f);
                    board.transform.localScale = st.hot ? new Vector3(1.2f, 0.8f, 0.06f) : new Vector3(0.8f, 0.5f, 0.06f);
                    board.GetComponent<MeshRenderer>().sharedMaterial = sign;
                    GameObjectUtility.SetStaticEditorFlags(pole, StaticEditorFlags.BatchingStatic);
                    GameObjectUtility.SetStaticEditorFlags(board, StaticEditorFlags.BatchingStatic);
                }
            }

            // The credit the ODbL licence asks for, in the scene where anyone opening it sees it.
            new GameObject("Map data (c) OpenStreetMap contributors, ODbL");

            // A camera at the stand looking down the route, so the scene opens on something.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.farClipPlane = 4000f;
            camGo.transform.position = new Vector3(0f, 40f, 60f);
            camGo.transform.LookAt(new Vector3(100f, 0f, -400f));

            GameObject bus = AddPlayerBus(camGo);

            // The streamer: the chunks around the bus, loaded as it drives.
            var streamer = new GameObject("World streamer").AddComponent<TwentyTons.Unity.WorldStreamer>();
            streamer.ChunksJson = AssetDatabase.LoadAssetAtPath<TextAsset>(WorldDir + "/chunks.json");
            streamer.ScenePaths = chunkScenes.ToArray();
            streamer.Target = bus.transform;

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            var buildScenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (string path in chunkScenes) buildScenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            // The first two chunks open beside the base scene, so the editor shows the stand, not an empty plane.
            for (int i = 0; i < 2 && i < chunkScenes.Count; i++) EditorSceneManager.OpenScene(chunkScenes[i], OpenSceneMode.Additive);
            Report();
        }

        private struct RouteBounds { public float CentreX, CentreZ, SizeX, SizeZ; }
        [System.Serializable] private class RouteXz { public float[] xz; }

        private static RouteBounds ReadRouteBounds(string path)
        {
            var r = JsonUtility.FromJson<RouteXz>(File.ReadAllText(path));
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            for (int i = 0; i + 1 < r.xz.Length; i += 2)
            {
                x0 = Mathf.Min(x0, r.xz[i]); x1 = Mathf.Max(x1, r.xz[i]);
                z0 = Mathf.Min(z0, r.xz[i + 1]); z1 = Mathf.Max(z1, r.xz[i + 1]);
            }
            return new RouteBounds { CentreX = (x0 + x1) * 0.5f, CentreZ = (z0 + z1) * 0.5f, SizeX = x1 - x0, SizeZ = z1 - z0 };
        }

        /// <summary>
        /// Milestone 2, first cut: a grey box the keyboard drives with the core's bus model, and the
        /// chase camera on it. Also makes Assets/Data/TuningTable.asset if it does not exist yet, so
        /// every number the drive uses is the one table the project rules ask for.
        /// </summary>
        private static GameObject AddPlayerBus(GameObject camera)
        {
            Directory.CreateDirectory("Assets/Data");
            const string tuningPath = "Assets/Data/TuningTable.asset";
            var tuning = AssetDatabase.LoadAssetAtPath<TwentyTons.Tuning.TuningTable>(tuningPath);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<TwentyTons.Tuning.TuningTable>();
                AssetDatabase.CreateAsset(tuning, tuningPath);
            }
            var route = AssetDatabase.LoadAssetAtPath<TextAsset>(WorldDir + "/route.json");
            var shape = TwentyTons.Core.VehicleShape.For(TwentyTons.Tuning.VehicleClass.Bus);

            // The bus: a rigid body on wheel colliders (PhysicsBus), a blocky body to look at (BusBody),
            // and the drive. Materials are plain colours; the real bus is a mesh for later.
            var bus = new GameObject("Player bus");
            Material paint = MakeMaterial(WorldDir + "/GreyboxBus.mat", new Color(0.85f, 0.55f, 0.15f));
            Material glass = MakeGlass(WorldDir + "/GreyboxBusGlass.mat", new Color(0.3f, 0.36f, 0.42f, 0.35f));
            Material rubber = MakeMaterial(WorldDir + "/GreyboxBusRubber.mat", new Color(0.08f, 0.08f, 0.08f));
            Material dark = MakeMaterial(WorldDir + "/GreyboxBusNose.mat", new Color(0.2f, 0.2f, 0.22f));
            var physics = bus.AddComponent<TwentyTons.Unity.PhysicsBus>();
            physics.Build(tuning.Bus, shape);
            physics.WheelMeshes = TwentyTons.Unity.BusBody.Build(bus.transform, tuning.Bus, shape, paint, glass, rubber, dark);
            var drive = bus.AddComponent<TwentyTons.Unity.PlayerBusDrive>();
            drive.Tuning = tuning;
            drive.RouteJson = route;
            drive.RouteBackJson = AssetDatabase.LoadAssetAtPath<TextAsset>(WorldDir + "/route_back.json");
            drive.StopsJson = AssetDatabase.LoadAssetAtPath<TextAsset>(WorldDir + "/stops.json");
            drive.SetUp();                                             // stands at the start lane in the saved scene
            physics.UpdateWheelMeshes();

            var rig = camera.AddComponent<TwentyTons.Unity.CameraRig>();
            rig.Target = bus.transform;
            return bus;
        }

        /// <summary>Make sure a named layer exists in the project's TagManager (first free user layer).</summary>
        private static void EnsureLayer(string name)
        {
            if (LayerMask.NameToLayer(name) >= 0) return;
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue)) { slot.stringValue = name; tagManager.ApplyModifiedProperties(); return; }
            }
            Debug.LogError("no free layer for " + name);
        }

        /// <summary>OBJ import settings from the plan: metres, no lightmap UVs, medium compression.</summary>
        private static void ConfigureImport(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) { Debug.LogError("missing " + path + ": run tools/osm/greybox.py first"); return; }
            importer.globalScale = 1f;
            importer.useFileScale = false;
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.generateSecondaryUV = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.addCollider = false;             // added per object below, roads and blocks only
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }

        private static Material MakeSurface(string path, float mode)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Twenty Tons/Surface");
            if (shader == null) { Debug.LogError("Twenty Tons/Surface shader not found; is Assets/World/Shaders imported?"); return MakeMaterial(path, Color.grey); }
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetFloat("_Mode", mode);
            mat.SetColor("_Tint", Color.white);
            return mat;
        }

        /// <summary>Tinted glass: URP Lit set up as transparent, so the driver and the helper can see out.</summary>
        private static Material MakeGlass(string path, Color color)
        {
            Material mat = MakeMaterial(path, color);
            mat.SetFloat("_Surface", 1f);                 // transparent
            mat.SetFloat("_Blend", 0f);                   // alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Smoothness", 0.8f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            return mat;
        }

        private static Material MakeMaterial(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.1f);
            return mat;
        }

        private static GameObject Place(string path, string name, Material material, bool withColliders)
        {
            if (!File.Exists(path)) return null;              // a chunk with no viaduct has no Rail.obj
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogError("missing " + path); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.position = Vector3.zero;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (withColliders) r.gameObject.AddComponent<MeshCollider>();
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
            return go;
        }

        /// <summary>What the scene costs: printed to the log, compared with the plan's budget.</summary>
        private static void Report()
        {
            long triangles = 0; int renderers = 0;
            foreach (var f in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (f.sharedMesh == null) continue;
                triangles += f.sharedMesh.triangles.Length / 3;
                renderers++;
            }
            Debug.Log($"Corridor01 grey box, base scene and the first two chunks: {renderers} renderers, {triangles} triangles (budget: 400k triangles, 150 draw calls after batching, for the three chunks loaded at a time)");
        }

        [System.Serializable] private class Marker { public string name; public float x; public float z; }
        [System.Serializable] private class MarkerList { public List<Marker> items; }
    }
}
