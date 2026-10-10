using UnityEngine;

namespace TwentyTons.Unity
{
    /// <summary>
    /// The driver's cab, for the driver's seat view (CameraRig.View.Driver): what a bus game shows
    /// around a fixed head position, laid out from the owner's ride-along frame (S-026.8). The
    /// painted dash the width of the cab with the binnacle in front of the wheel, the black car-type
    /// wheel lying back and turning with the steering, the one-piece screen in its thick frame with
    /// the name band across the top, the big inside mirror hanging from the screen, the outside
    /// mirror on the right, the engine cowl under its cloth between the driver and the door. Mirrors
    /// are small cameras rendering to a texture on a quad, flipped so they read as mirrors. Dhaka
    /// local buses are right-hand drive; the helper's door is on the left. Primitives, no colliders,
    /// built at Play start under the bus body; the art pass replaces them.
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
            // What the ride-along frame shows (S-026.8): everything painted orange-red metal, a one-piece
            // screen with a thick frame and a painted name band across its top, the big inside mirror
            // hanging from the top of the screen, a black car-type wheel lying back, a raised binnacle
            // in front of it under a red cloth, a paper taped to the dash, the engine cowl under a cloth
            // between the driver and the door, bare metal floor.
            Material paint = Make(new Color(0.72f, 0.33f, 0.14f)), dark = Make(new Color(0.12f, 0.12f, 0.12f)), rubber = Make(new Color(0.08f, 0.08f, 0.09f));
            Material cloth = Make(new Color(0.55f, 0.12f, 0.14f)), band = Make(new Color(0.10f, 0.35f, 0.38f)), paper = Make(new Color(0.92f, 0.90f, 0.82f));

            float ex = driverEye.x, ey = driverEye.y, ez = driverEye.z, hw = busWidth * 0.5f;
            float screenZ = ez + 1.2f;
            // The dash: a painted box the whole width of the cab, its top well below the eyes.
            Part(cabin, "Dash", paint, new Vector3(0f, ey - 0.98f, ez + 0.85f), new Vector3(busWidth - 0.1f, 0.5f, 0.7f));
            // The binnacle in front of the wheel, under its red cloth.
            Part(cabin, "Binnacle", cloth, new Vector3(ex, ey - 0.64f, ez + 1.05f), new Vector3(0.55f, 0.2f, 0.3f));
            // The waybill taped to the dash, left of the wheel.
            Part(cabin, "Paper", paper, new Vector3(ex - 0.55f, ey - 0.66f, ez + 1.0f), new Vector3(0.2f, 0.14f, 0.01f)).localRotation = Quaternion.Euler(-30f, 0f, 0f);
            // The screen frame: thick pillars, the top rail with the painted band below it, the sill.
            Part(cabin, "Pillar R", paint, new Vector3(hw - 0.08f, ey + 0.3f, screenZ), new Vector3(0.16f, 1.7f, 0.14f));
            Part(cabin, "Pillar L", paint, new Vector3(-hw + 0.08f, ey + 0.3f, screenZ), new Vector3(0.16f, 1.7f, 0.14f));
            Part(cabin, "Top rail", paint, new Vector3(0f, ey + 1.0f, screenZ), new Vector3(busWidth, 0.16f, 0.14f));
            Part(cabin, "Name band", band, new Vector3(0f, ey + 0.78f, screenZ - 0.02f), new Vector3(busWidth - 0.3f, 0.28f, 0.01f));
            Part(cabin, "Sill", paint, new Vector3(0f, ey - 0.68f, screenZ), new Vector3(busWidth, 0.12f, 0.14f));
            // The switch panel taped to the dash left of the binnacle (S-026.9): a white board with four rows of
            // six, two of toggles and two of push buttons: the bus's own wiring for lights, horns and wipers.
            var panel = Part(cabin, "Switch panel", paper, new Vector3(ex - 0.6f, ey - 0.7f, ez + 0.98f), new Vector3(0.34f, 0.26f, 0.01f));
            panel.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 6; col++)
                {
                    bool toggle = row % 2 == 1;
                    var knob = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    knob.name = toggle ? "Toggle" : "Button";
                    Object.Destroy(knob.GetComponent<Collider>());
                    knob.transform.SetParent(panel, false);
                    knob.transform.localPosition = new Vector3(-0.38f + col * 0.152f, 0.33f - row * 0.22f, -0.6f);
                    knob.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    knob.transform.localScale = toggle ? new Vector3(0.04f, 1.2f, 0.06f) : new Vector3(0.07f, 0.6f, 0.09f);
                    knob.GetComponent<MeshRenderer>().sharedMaterial = toggle ? Make(new Color(0.6f, 0.6f, 0.62f)) : dark;
                    knob.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            // The rod along the base of the screen, and the cubby on the left with a bottle in it.
            Part(cabin, "Rod", dark, new Vector3(0f, ey - 0.56f, ez + 1.1f), new Vector3(busWidth - 0.4f, 0.025f, 0.025f));
            Part(cabin, "Cubby", dark, new Vector3(ex - 1.1f, ey - 0.8f, ez + 1.0f), new Vector3(0.35f, 0.22f, 0.3f));
            var bottle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bottle.name = "Bottle";
            Object.Destroy(bottle.GetComponent<Collider>());
            bottle.transform.SetParent(cabin, false);
            bottle.transform.localPosition = new Vector3(ex - 1.1f, ey - 0.62f, ez + 1.0f);
            bottle.transform.localScale = new Vector3(0.08f, 0.13f, 0.08f);
            bottle.GetComponent<MeshRenderer>().sharedMaterial = Make(new Color(0.8f, 0.85f, 0.9f));
            // The bench along the left wall of the cab (S-026.10): red vinyl, facing inward across the cowl,
            // so passengers sit beside the driver. More people in the bus.
            Material vinyl = Make(new Color(0.62f, 0.14f, 0.16f));
            Part(cabin, "Bench seat", vinyl, new Vector3(-hw + 0.3f, ey - 1.45f, ez + 0.1f), new Vector3(0.45f, 0.12f, 1.7f));
            Part(cabin, "Bench box", paint, new Vector3(-hw + 0.3f, ey - 1.7f, ez + 0.1f), new Vector3(0.45f, 0.4f, 1.7f));
            Part(cabin, "Bench back", vinyl, new Vector3(-hw + 0.1f, ey - 1.15f, ez + 0.1f), new Vector3(0.08f, 0.5f, 1.7f));
            // The engine cowl between the driver and the door, under a cloth.
            Part(cabin, "Engine cowl", cloth, new Vector3(ex - 1.0f, ey - 1.3f, ez + 0.3f), new Vector3(0.9f, 0.75f, 1.5f));

            // The wheel: black, lying back towards the driver, low and close. A pivot, a ring, a hub, spokes.
            var pivot = new GameObject("Wheel").transform;
            pivot.SetParent(cabin, false);
            pivot.localPosition = new Vector3(ex, ey - 0.68f, ez + 0.55f);
            pivot.localRotation = Quaternion.Euler(-55f, 0f, 0f);
            float radius = 0.22f;
            Ring(pivot, "Rim", rubber, radius, 0.035f);
            Part(pivot, "Hub", rubber, Vector3.zero, new Vector3(0.16f, 0.05f, 0.14f));
            for (int i = 0; i < 3; i++)
            {
                var spoke = Part(pivot, "Spoke", rubber, Vector3.zero, new Vector3(0.04f, 0.025f, radius));
                spoke.localRotation = Quaternion.Euler(0f, 60f + i * 120f, 0f);
                spoke.localPosition = spoke.localRotation * new Vector3(0f, 0f, radius * 0.5f);
            }
            Part(cabin, "Column", dark, new Vector3(ex, ey - 0.95f, ez + 0.78f), new Vector3(0.06f, 0.7f, 0.06f)).localRotation = Quaternion.Euler(-55f, 0f, 0f);

            // The inside mirror: a big rectangle hanging from the top of the screen, right of centre, looking back into the bus.
            Vector3 insideAt = new Vector3(ex - 0.35f, ey + 0.45f, ez + 0.95f);
            Part(cabin, "Inside mirror stalk", dark, new Vector3(ex - 0.35f, ey + 0.68f, screenZ - 0.12f), new Vector3(0.03f, 0.3f, 0.03f)).localRotation = Quaternion.Euler(35f, 0f, 0f);
            Part(cabin, "Inside mirror back", dark, insideAt + new Vector3(0f, 0f, 0.02f), new Vector3(0.36f, 0.22f, 0.02f));
            Mirror(cabin, "Inside mirror glass", insideAt, Quaternion.Euler(8f, 10f, 0f), new Vector3(0.34f, 0.2f, 1f), 180f + 6f, 60f);

            // The outside mirror on the right, on its arm forward of the pillar, looking back and a little out.
            Vector3 mirrorAt = new Vector3(hw + 0.35f, ey + 0.05f, ez + 1.0f);
            Part(cabin, "Mirror arm", dark, new Vector3(hw + 0.17f, ey + 0.05f, ez + 1.0f), new Vector3(0.36f, 0.03f, 0.03f));
            Part(cabin, "Mirror back", dark, mirrorAt + new Vector3(0f, 0f, 0.02f), new Vector3(0.28f, 0.42f, 0.03f));
            Mirror(cabin, "Mirror glass", mirrorAt, Quaternion.Euler(0f, 30f, 0f), new Vector3(0.26f, 0.4f, 1f), 180f - 12f, 38f);
            return pivot;
        }

        /// <summary>A mirror glass: a quad (which faces -z) turned to face the driver, with its camera looking back.</summary>
        private static void Mirror(Transform parent, string name, Vector3 at, Quaternion facing, Vector3 size, float yaw, float fov)
        {
            var glass = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glass.name = name;
            Object.Destroy(glass.GetComponent<Collider>());
            glass.transform.SetParent(parent, false);
            glass.transform.localPosition = at;
            glass.transform.localRotation = facing;
            glass.transform.localScale = size;
            var mirror = glass.AddComponent<MirrorView>();
            mirror.Yaw = yaw;
            mirror.FieldOfView = fov;
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
