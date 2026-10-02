using System.Collections.Generic;
using TwentyTons.Core;
using UnityEngine;

namespace TwentyTons.Sandbox
{
    /// <summary>
    /// A made-up 1.2 km road with grey boxes along it, so there is something to drive through before
    /// the real Mirpur corridor arrives from OpenStreetMap (docs/OSM_IMPORT_PLAN.md). The corridor is
    /// one carriageway, 10 m wide: three lanes' worth of lane-free road.
    /// </summary>
    public static class SandboxWorld
    {
        public const float RoadWidth = 10f;
        public const float PavementMetres = 3f;

        public static Corridor BuildCorridor(SeededRandom random)
        {
            var points = new List<Vector3>();
            Vector3 p = Vector3.zero;
            float heading = 0f;                       // radians, 0 = +Z
            points.Add(p);
            for (int i = 0; i < 24; i++)
            {
                // Gentle wander with a pull back toward straight, so it never loops.
                heading += random.Range(-6f, 6f) * Mathf.Deg2Rad - heading * 0.15f;
                p += new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading)) * 50f;
                points.Add(p);
            }
            return new Corridor(points, RoadWidth);
        }

        /// <summary>
        /// Building boxes as a flat float array for the renderer: x, z, yaw, length (along the road),
        /// width (across), height, repeated. Both sides, with a side-street gap now and then.
        /// </summary>
        public static float[] BuildBuildings(Corridor corridor, SeededRandom random)
        {
            var data = new List<float>();
            for (int side = -1; side <= 1; side += 2)
            {
                float s = 5f;
                float nextSideStreet = random.Range(80f, 160f);
                while (s < corridor.Length - 25f)
                {
                    if (s > nextSideStreet)
                    {
                        s += 12f;                                       // a side street
                        nextSideStreet = s + random.Range(100f, 200f);
                        continue;
                    }
                    float length = random.Range(10f, 22f);
                    float width = random.Range(10f, 20f);
                    float height = random.Range(2, 8) * 3f;             // 2–7 storeys of concrete
                    float setBack = PavementMetres + random.Range(0f, 2f);
                    float lateral = side * (corridor.HalfWidth + setBack + width * 0.5f);
                    float mid = s + length * 0.5f;
                    Vector3 centre = corridor.PositionAt(mid, lateral);

                    data.Add(centre.x); data.Add(centre.z); data.Add(corridor.YawAt(mid));
                    data.Add(length); data.Add(width); data.Add(height);

                    s += length + random.Range(1.5f, 4f);
                }
            }
            return data.ToArray();
        }

        /// <summary>Road edge polylines for the renderer: x, z pairs, left edge then right edge.</summary>
        public static float[] RoadEdge(Corridor corridor, float lateral)
        {
            var data = new List<float>();
            for (float s = 0f; s <= corridor.Length; s += 5f)
            {
                Vector3 p = corridor.PositionAt(s, lateral);
                data.Add(p.x); data.Add(p.z);
            }
            return data.ToArray();
        }
    }
}
