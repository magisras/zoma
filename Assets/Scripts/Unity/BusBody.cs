using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Unity
{
    /// <summary>
    /// A blocky bus to look at until a real mesh exists: body, a lower cab front with a windscreen
    /// band, a window band along the sides, the door on the left (Bangladesh boards from the left),
    /// four wheels. Built from primitives, so it costs nothing and can be replaced by a model later;
    /// the physics and the drive never reference it.
    /// </summary>
    public static class BusBody
    {
        public static Transform[] Build(Transform bus, BusSettings b, VehicleShape shape, Material paint, Material glass, Material rubber, Material dark)
        {
            // Clear what an earlier build left.
            for (int i = bus.childCount - 1; i >= 0; i--)
            {
                Transform c = bus.GetChild(i);
                if (c.name.StartsWith("Body") || c.name.StartsWith("Tyre")) Object.DestroyImmediate(c.gameObject);
            }
            float floor = 0.45f, h = shape.Height, w = shape.Width, l = shape.Length;

            // Body shell.
            Part(bus, "Body shell", paint, new Vector3(0f, floor + (h - floor) * 0.5f, 0f), new Vector3(w, h - floor, l));
            // Windows: a dark band along each side and the windscreen, slightly proud of the shell.
            float bandY = floor + (h - floor) * 0.62f, bandH = (h - floor) * 0.32f;
            Part(bus, "Body windows L", glass, new Vector3(-w * 0.5f - 0.01f, bandY, -0.6f), new Vector3(0.02f, bandH, l * 0.78f));
            Part(bus, "Body windows R", glass, new Vector3(w * 0.5f + 0.01f, bandY, -0.6f), new Vector3(0.02f, bandH, l * 0.78f));
            Part(bus, "Body windscreen", glass, new Vector3(0f, bandY + 0.1f, l * 0.5f + 0.01f), new Vector3(w * 0.9f, bandH + 0.2f, 0.02f));
            Part(bus, "Body rear window", glass, new Vector3(0f, bandY, -l * 0.5f - 0.01f), new Vector3(w * 0.8f, bandH * 0.8f, 0.02f));
            // The door: left side, behind the front axle, open (no door, as in the research).
            Part(bus, "Body door", dark, new Vector3(-w * 0.5f - 0.02f, floor + (h - floor - 0.3f) * 0.5f, b.FrontAxleMetres - 1.6f), new Vector3(0.03f, h - floor - 0.3f, 1.1f));
            // Bumper and the roof rack bar: a little silhouette.
            Part(bus, "Body bumper", dark, new Vector3(0f, floor + 0.25f, l * 0.5f + 0.08f), new Vector3(w, 0.35f, 0.16f));
            Part(bus, "Body roof rail", dark, new Vector3(0f, h + 0.08f, -0.5f), new Vector3(w * 0.9f, 0.12f, l * 0.6f));

            // Wheels as flattened cylinders, placed by PhysicsBus from the colliders each frame.
            var meshes = new Transform[4];
            string[] names = { "Tyre FL", "Tyre FR", "Tyre RL", "Tyre RR" };
            for (int i = 0; i < 4; i++)
            {
                var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                t.name = names[i];
                Object.DestroyImmediate(t.GetComponent<Collider>());
                t.transform.SetParent(bus, false);
                // A cylinder primitive stands on Y; lay it on its side so the axle runs along X.
                var inner = t.transform;
                inner.localRotation = Quaternion.Euler(0f, 0f, 90f);
                inner.localScale = new Vector3(b.WheelRadiusMetres * 2f, 0.16f, b.WheelRadiusMetres * 2f);
                t.GetComponent<MeshRenderer>().sharedMaterial = rubber;
                // A pivot keeps the collider's pose (position + rotation) separate from the lying-down cylinder.
                var pivot = new GameObject(names[i] + " pivot").transform;
                pivot.SetParent(bus, false);
                inner.SetParent(pivot, false);
                meshes[i] = pivot;
            }
            return meshes;
        }

        private static void Part(Transform parent, string name, Material m, Vector3 centre, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());     // the physics body has its own single box
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
        }
    }
}
