using System.IO;
using UnityEditor;
using UnityEngine;

namespace TwentyTons.EditorTools
{
    /// <summary>
    /// Renders the open scene to a PNG from a chosen viewpoint, so a terminal session can look at
    /// the grey box without a human at the screen: `unity command eval` calls these and the PNG is
    /// read back. Uses a throwaway camera; the scene is not changed.
    ///
    ///   unity command eval 'TwentyTons.EditorTools.SceneShots.Chase("/tmp/bus.png");'
    ///   unity command eval 'TwentyTons.EditorTools.SceneShots.TopDown(0, -300, 600, "/tmp/top.png");'
    ///   unity command eval 'TwentyTons.EditorTools.SceneShots.From(20, 8, -40, 0, -200, "/tmp/view.png");'
    /// </summary>
    public static class SceneShots
    {
        /// <summary>From behind the player bus, as the chase camera sees it.</summary>
        public static string Chase(string path, int width = 1280, int height = 720)
        {
            GameObject bus = GameObject.Find("Player bus");
            if (bus == null) return "no Player bus in the scene";
            Vector3 eye = bus.transform.position - bus.transform.forward * 16f + Vector3.up * 7f;
            Vector3 look = bus.transform.position + bus.transform.forward * 10f;
            return Render(eye, look, 60f, path, width, height);
        }

        /// <summary>Straight down on a point, from the given height.</summary>
        public static string TopDown(float x, float z, float height, string path, int width = 1280, int height_px = 1280)
        {
            return Render(new Vector3(x, height, z), new Vector3(x, 0f, z + 0.01f), 60f, path, width, height_px);
        }

        /// <summary>From an eye point to a target point on the ground.</summary>
        public static string From(float ex, float ey, float ez, float tx, float tz, string path, int width = 1280, int height = 720)
        {
            return Render(new Vector3(ex, ey, ez), new Vector3(tx, 0f, tz), 60f, path, width, height);
        }

        private static string Render(Vector3 eye, Vector3 look, float fov, string path, int width, int height)
        {
            var go = new GameObject("SceneShot camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.transform.position = eye;
            cam.transform.LookAt(look);
            cam.fieldOfView = fov;
            cam.farClipPlane = 6000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.7f, 0.9f);
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            return "wrote " + path;
        }
    }
}
