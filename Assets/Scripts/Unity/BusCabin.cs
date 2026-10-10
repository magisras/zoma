using UnityEngine;

namespace TwentyTons.Unity
{
    /// <summary>
    /// The driver's cab, for the driver's seat view (CameraRig.View.Driver): what a bus game shows
    /// around a fixed head position. A dashboard shelf, the big thin wheel of an old bus tilted
    /// towards the driver and turning with the steering, the windscreen frame with its pillars and
    /// the centre divider of a two-piece screen, and the outside mirror on the right, which is a
    /// small camera rendering to a texture on a quad, flipped so it reads as a mirror. Dhaka local
    /// buses are right-hand drive; the helper's door is on the left. Primitives, no colliders, built
    /// at Play start under the bus body; the art pass replaces them. Cab layout from general
    /// knowledge of Bangladeshi local buses: to check against the photographs in S-026.
    /// </summary>
    public static class BusCabin
    {
        /// <summary>Build the cab; returns the wheel's pivot, which the drive turns with the steering.</summary>
        public static Transform Build(Transform bus, Vector3 driverEye, float busWidth)
        {
            Transform old = bus.Find("Cabin");
            if (old != null) Object.Destroy(old.gameObject);
            var cabin = new GameObject("Cabin").transform;
            cabin.SetParent(bus, false);
            Material dark = Make(new Color(0.16f, 0.16f, 0.17f)), steel = Make(new Color(0.55f, 0.55f, 0.52f)), dash = Make(new Color(0.30f, 0.28f, 0.24f));

            float ex = driverEye.x, ey = driverEye.y, ez = driverEye.z;
            // The dashboard shelf: from the knees to just below the screen, the width of the cab's right half.
            Part(cabin, "Dash", dash, new Vector3(ex * 0.5f + 0.2f, ey - 0.75f, ez + 0.95f), new Vector3(busWidth * 0.5f + 0.3f, 0.35f, 0.5f));
            Part(cabin, "Dash lip", dark, new Vector3(ex * 0.5f + 0.2f, ey - 0.55f, ez + 0.72f), new Vector3(busWidth * 0.5f + 0.3f, 0.06f, 0.08f));
            // The windscreen frame: the two pillars, the top rail, the centre divider.
            float screenZ = ez + 1.2f;
            Part(cabin, "Pillar R", dark, new Vector3(busWidth * 0.5f - 0.06f, ey + 0.3f, screenZ), new Vector3(0.12f, 1.6f, 0.12f));
            Part(cabin, "Pillar L", dark, new Vector3(-busWidth * 0.5f + 0.06f, ey + 0.3f, screenZ), new Vector3(0.12f, 1.6f, 0.12f));
            Part(cabin, "Top rail", dark, new Vector3(0f, ey + 0.95f, screenZ), new Vector3(busWidth, 0.14f, 0.12f));
            Part(cabin, "Divider", dark, new Vector3(0f, ey + 0.3f, screenZ), new Vector3(0.05f, 1.3f, 0.05f));
            Part(cabin, "Sill", dark, new Vector3(0f, ey - 0.4f, screenZ), new Vector3(busWidth, 0.1f, 0.12f));

            // The wheel: a pivot tilted back towards the driver, the rim a thin ring, the hub, three spokes.
            var pivot = new GameObject("Wheel").transform;
            pivot.SetParent(cabin, false);
            pivot.localPosition = new Vector3(ex, ey - 0.5f, ez + 0.55f);
            pivot.localRotation = Quaternion.Euler(-55f, 0f, 0f);          // the old bus wheel lies back
            float radius = 0.26f;
            Ring(pivot, "Rim", dark, radius, 0.03f);
            Part(pivot, "Hub", dark, Vector3.zero, new Vector3(0.12f, 0.04f, 0.12f));
            for (int i = 0; i < 3; i++)
            {
                var spoke = Part(pivot, "Spoke", steel, Vector3.zero, new Vector3(0.025f, 0.02f, radius));
                spoke.localRotation = Quaternion.Euler(0f, i * 120f, 0f);
                spoke.localPosition = spoke.localRotation * new Vector3(0f, 0f, radius * 0.5f);
            }
            Part(cabin, "Column", dark, new Vector3(ex, ey - 0.85f, ez + 0.75f), new Vector3(0.06f, 0.6f, 0.06f)).localRotation = Quaternion.Euler(-55f, 0f, 0f);

            // The outside mirror on the right: a stalk, the glass as a quad showing a camera looking back.
            // Forward of the pillar, where the driver sees it through the right of the screen (about 35° off centre).
            Vector3 mirrorAt = new Vector3(busWidth * 0.5f + 0.35f, ey + 0.05f, ez + 1.0f);
            Part(cabin, "Mirror arm", dark, new Vector3(busWidth * 0.5f + 0.17f, ey + 0.05f, ez + 1.0f), new Vector3(0.36f, 0.03f, 0.03f));
            Part(cabin, "Mirror back", dark, mirrorAt + new Vector3(0f, 0f, 0.02f), new Vector3(0.28f, 0.42f, 0.03f));
            var glass = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glass.name = "Mirror glass";
            Object.Destroy(glass.GetComponent<Collider>());
            glass.transform.SetParent(cabin, false);
            glass.transform.localPosition = mirrorAt;
            glass.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);            // a quad faces -z; turned to face the driver inside
            glass.transform.localScale = new Vector3(0.26f, 0.4f, 1f);
            var mirror = glass.AddComponent<MirrorView>();
            mirror.Yaw = 180f - 12f;                                                     // looking back and a little out
            return pivot;
        }

        private static Transform Part(Transform parent, string name, Material m, Vector3 centre, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>A thin ring in the pivot's XZ plane from short cubes.</summary>
        private static void Ring(Transform parent, string name, Material m, float radius, float thickness)
        {
            const int n = 24;
            float seg = 2f * Mathf.PI * radius / n * 1.1f;
            for (int i = 0; i < n; i++)
            {
                float a = i * 360f / n;
                var piece = Part(parent, name, m, Vector3.zero, new Vector3(thickness, thickness, seg));
                piece.localRotation = Quaternion.Euler(0f, a, 0f);
                piece.localPosition = piece.localRotation * new Vector3(radius, 0f, 0f);   // its length runs along the tangent
            }
        }

        private static Material Make(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.35f);
            return m;
        }
    }

    /// <summary>
    /// A mirror: a camera at the glass looking back, drawn onto the glass with the texture flipped
    /// in x, which gives the mirror image.
    /// </summary>
    public sealed class MirrorView : MonoBehaviour
    {
        public float Yaw = 180f;
        public float FieldOfView = 38f;

        private void Start()
        {
            var rt = new RenderTexture(256, 384, 16) { name = "Mirror" };
            var camGo = new GameObject("Mirror camera");
            camGo.transform.SetParent(transform.parent, false);
            camGo.transform.localPosition = transform.localPosition;
            camGo.transform.localRotation = Quaternion.Euler(0f, Yaw, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.fieldOfView = FieldOfView;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 600f;
            cam.depth = -10f;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetTexture("_BaseMap", rt);
            mat.SetTextureScale("_BaseMap", new Vector2(-1f, 1f));                      // the mirror image
            mat.SetTextureOffset("_BaseMap", new Vector2(1f, 0f));
            GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
