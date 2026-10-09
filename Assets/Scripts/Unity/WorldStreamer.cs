using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwentyTons.Unity
{
    /// <summary>
    /// Loads the world a kilometre at a time. The route is cut into chunks (tools/osm/greybox.py,
    /// one folder and one scene each); the chunks within LoadWithin of the bus are loaded additively,
    /// the ones beyond UnloadBeyond are unloaded. The gap between the two keeps a chunk from flapping
    /// at its border. A bus runs the route out and back all day, so this works in both directions.
    /// Each chunk's stretch of the route, sampled every 50 m, is the index (chunks.json).
    /// </summary>
    public sealed class WorldStreamer : MonoBehaviour
    {
        public TextAsset ChunksJson;
        public string[] ScenePaths;                 // one per chunk, in chunk order, all in Build Settings
        public Transform Target;                    // the bus
        public float LoadWithin = 1200f, UnloadBeyond = 1700f, Every = 0.5f;
        public static WorldStreamer Instance;

        [System.Serializable] private class ChunkFile { public ChunkEntry[] chunks; }
        [System.Serializable] private class ChunkEntry { public int index; public string folder; public float s0, s1; public float[] xz; }

        private ChunkEntry[] _chunks = new ChunkEntry[0];
        private readonly Dictionary<int, bool> _pending = new Dictionary<int, bool>();   // chunk -> true = loading, false = unloading
        private float _next;

        private void Awake()
        {
            Instance = this;
            if (ChunksJson != null) _chunks = JsonUtility.FromJson<ChunkFile>(ChunksJson.text).chunks ?? _chunks;
        }

        /// <summary>The first chunks load before the first physics step, so the bus has a road under it.</summary>
        private void Start() { Refresh(true); }

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + Every;
            Refresh(false);
        }

        /// <summary>Load and unload around the target. now = load synchronously and never unload: at
        /// start-up, and in scripted tests, where no frame runs to finish an unload.</summary>
        public void Refresh(bool now)
        {
            Vector3 p = Target != null ? Target.position : Vector3.zero;
            for (int i = 0; i < _chunks.Length && i < ScenePaths.Length; i++)
            {
                Scene scene = SceneManager.GetSceneByPath(ScenePaths[i]);
                bool loaded = scene.IsValid() && scene.isLoaded;
                // A load or unload finishes at the next frame, synchronous or not: until the scene reports
                // the state asked for, ask nothing more of it. Asking twice loads the scene twice.
                if (_pending.TryGetValue(i, out bool wantLoaded))
                {
                    if (loaded != wantLoaded) continue;
                    _pending.Remove(i);
                }
                float d = Distance(_chunks[i], p);
                if (!loaded && d < LoadWithin)
                {
                    _pending[i] = true;
                    if (now) SceneManager.LoadScene(ScenePaths[i], LoadSceneMode.Additive);
                    else SceneManager.LoadSceneAsync(ScenePaths[i], LoadSceneMode.Additive);
                }
                else if (loaded && d > UnloadBeyond && !now)
                {
                    _pending[i] = false;
                    SceneManager.UnloadSceneAsync(scene);
                }
            }
        }

        /// <summary>Is the ground under this point loaded? (Scripted drives ask before stepping.)</summary>
        public bool IsLoadedAt(Vector3 p)
        {
            for (int i = 0; i < _chunks.Length && i < ScenePaths.Length; i++)
            {
                if (Distance(_chunks[i], p) > 60f) continue;
                Scene scene = SceneManager.GetSceneByPath(ScenePaths[i]);
                if (!scene.IsValid() || !scene.isLoaded) return false;
            }
            return true;
        }

        /// <summary>Which chunks are loaded, for the readout: "chunks 3 4 5".</summary>
        public string Status
        {
            get
            {
                var s = new System.Text.StringBuilder("chunks");
                for (int i = 0; i < ScenePaths.Length; i++)
                {
                    Scene scene = SceneManager.GetSceneByPath(ScenePaths[i]);
                    if (scene.IsValid() && scene.isLoaded) s.Append(' ').Append(i);
                }
                return s.ToString();
            }
        }

        /// <summary>Distance from a point to the chunk's route samples (within 25 m of the line).</summary>
        private static float Distance(ChunkEntry c, Vector3 p)
        {
            float best = float.MaxValue;
            for (int k = 0; k + 1 < c.xz.Length; k += 2)
            {
                float dx = c.xz[k] - p.x, dz = c.xz[k + 1] - p.z;
                float d = dx * dx + dz * dz;
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }
    }
}
