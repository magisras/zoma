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
            Material ground = MakeMaterial(WorldDir + "/GreyboxGround.mat", new Color(0.62f, 0.62f, 0.60f));
            Material road = MakeMaterial(WorldDir + "/GreyboxRoad.mat", new Color(0.36f, 0.36f, 0.38f));
            Material block = MakeMaterial(WorldDir + "/GreyboxBlock.mat", new Color(0.72f, 0.70f, 0.66f));
            Material rail = MakeMaterial(WorldDir + "/GreyboxRail.mat", new Color(0.55f, 0.56f, 0.60f));

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

            // Route markers: stands and junctions, as named empties for milestones 4 and 5.
            var markers = new GameObject("Route markers");
            string markersJson = File.ReadAllText(WorldDir + "/markers.json");
            foreach (Marker m in JsonUtility.FromJson<MarkerList>("{\"items\":" + markersJson + "}").items)
            {
                var go = new GameObject(m.name);
                go.transform.SetParent(markers.transform);
                go.transform.position = new Vector3(m.x, 0f, m.z);
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

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Report();
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
