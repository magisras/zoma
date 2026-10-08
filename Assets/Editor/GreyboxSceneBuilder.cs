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
            ConfigureImport(WorldDir + "/Roads.obj");
            ConfigureImport(WorldDir + "/Buildings.obj");
            ConfigureImport(WorldDir + "/Rail.obj");
            ConfigureImport(WorldDir + "/Markings.obj");
            ConfigureImport(WorldDir + "/Kerbs.obj");
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Light: one directional, hard shadows off on buildings (they are flat grey anyway).
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            // Materials: one for the road surface, one for everything else, so each is a batch.
            // The procedural skin (Assets/World/Shaders/Surface.shader): a mode per surface, hashed from
            // world position, so the street reads as Mirpur with no textures and no assets to license.
            Material ground = MakeSurface(WorldDir + "/SkinDirt.mat", 4f);
            Material road = MakeSurface(WorldDir + "/SkinAsphalt.mat", 1f);
            Material block = MakeSurface(WorldDir + "/SkinFacade.mat", 0f);
            Material rail = MakeSurface(WorldDir + "/SkinConcrete.mat", 3f);
            Material paint = MakeMaterial(WorldDir + "/GreyboxPaint.mat", new Color(0.92f, 0.92f, 0.88f));
            Material kerb = MakeSurface(WorldDir + "/SkinPavement.mat", 2f);
            Material post = MakeMaterial(WorldDir + "/GreyboxPost.mat", new Color(0.20f, 0.70f, 0.35f));

            // Ground: a big plane a little under the roads so there are no holes between blocks.
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Ground";
            plane.transform.position = new Vector3(400f, -0.05f, -1600f);     // centred on the chunk
            plane.transform.localScale = new Vector3(500f, 1f, 500f);         // a Unity plane is 10 m
            plane.GetComponent<MeshRenderer>().sharedMaterial = ground;
            GameObjectUtility.SetStaticEditorFlags(plane, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

            Place(WorldDir + "/Roads.obj", "Roads", road, withColliders: true);
            Place(WorldDir + "/Buildings.obj", "Blocks", block, withColliders: true);
            Place(WorldDir + "/Rail.obj", "MRT Line 6 viaduct", rail, withColliders: true);
            Place(WorldDir + "/Markings.obj", "Lane markings", paint, withColliders: false);
            Place(WorldDir + "/Kerbs.obj", "Pavements and median", kerb, withColliders: true);

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

            AddPlayerBus(camGo);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Report();
        }

        /// <summary>
        /// Milestone 2, first cut: a grey box the keyboard drives with the core's bus model, and the
        /// chase camera on it. Also makes Assets/Data/TuningTable.asset if it does not exist yet, so
        /// every number the drive uses is the one table the project rules ask for.
        /// </summary>
        private static void AddPlayerBus(GameObject camera)
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
            Material glass = MakeMaterial(WorldDir + "/GreyboxBusGlass.mat", new Color(0.15f, 0.18f, 0.22f));
            Material rubber = MakeMaterial(WorldDir + "/GreyboxBusRubber.mat", new Color(0.08f, 0.08f, 0.08f));
            Material dark = MakeMaterial(WorldDir + "/GreyboxBusNose.mat", new Color(0.2f, 0.2f, 0.22f));
            var physics = bus.AddComponent<TwentyTons.Unity.PhysicsBus>();
            physics.Build(tuning.Bus, shape);
            physics.WheelMeshes = TwentyTons.Unity.BusBody.Build(bus.transform, tuning.Bus, shape, paint, glass, rubber, dark);
            var drive = bus.AddComponent<TwentyTons.Unity.PlayerBusDrive>();
            drive.Tuning = tuning;
            drive.RouteJson = route;
            drive.SetUp();                                             // stands at the start lane in the saved scene
            physics.UpdateWheelMeshes();

            var rig = camera.AddComponent<TwentyTons.Unity.CameraRig>();
            rig.Target = bus.transform;
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

        private static void Place(string path, string name, Material material, bool withColliders)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogError("missing " + path); return; }
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
            Debug.Log($"Corridor01 grey box: {renderers} renderers, {triangles} triangles (budget: 400k triangles, 150 draw calls after batching)");
        }

        [System.Serializable] private class Marker { public string name; public float x; public float z; }
        [System.Serializable] private class MarkerList { public List<Marker> items; }
    }
}
