using System.Collections.Generic;
using TwentyTons.Core;
using UnityEngine;

namespace TwentyTons.Sandbox
{
    /// <summary>
    /// A made-up ring road of about 1.5 km with grey boxes along it and two cross streets with
    /// officers, so there is something to drive round before the real Mirpur corridor arrives from
    /// OpenStreetMap (docs/OSM_IMPORT_PLAN.md). The road is one carriageway, 10 m wide: three lanes'
    /// worth of lane-free road. A loop means endless driving and, later, laps as trips.
    /// </summary>
    public static class SandboxWorld
    {
        public const float RoadWidth = 10f;
        public const float MedianMetres = 1.5f;
        public const float CrossWidth = 8f;
        /// <summary>From the main centreline to the oncoming centreline: both half widths and the median.</summary>
        public const float OncomingOffset = RoadWidth + MedianMetres;
        public const float PavementMetres = 3f;
        public const float CrossStreetLength = 240f;

        /// <summary>Main-road S positions of the cross streets. Chosen on the long straights.</summary>
        public static readonly float[] JunctionS = { 260f, 1010f };

        /// <summary>Where people wait, with fictional names. Hot ones sit just before the junctions.</summary>
        public static readonly string[] ZoneNames = { "Stand", "Block 11", "Roundabout", "Market", "Kazipara", "School" };
        public static readonly float[] ZoneS = { 60f, 235f, 420f, 760f, 985f, 1250f };
        public static readonly bool[] ZoneHot = { true, true, false, true, true, false };

        public static Corridor BuildCorridor(SeededRandom random)
        {
            // A rounded rectangle, 520 × 200 m, walked anticlockwise so +lateral (right) is the outside.
            var points = new List<Vector3>();
            AddStraight(points, new Vector3(0, 0, 0), new Vector3(0, 0, 1), 480f, random);
            AddArc(points, new Vector3(0, 0, 480f), 0f, 100f, random);
            AddStraight(points, new Vector3(200f, 0, 480f), new Vector3(0, 0, -1), 480f, random);
            AddArc(points, new Vector3(200f, 0, 0f), Mathf.PI, 100f, random);
            return new Corridor(points, RoadWidth, closed: true, name: "ring");
        }

        private static void AddStraight(List<Vector3> points, Vector3 from, Vector3 dir, float length, SeededRandom random)
        {
            for (float s = 0f; s < length; s += 40f)
            {
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                float wobble = s > 0f ? random.Range(-3f, 3f) : 0f;     // a little bend, never a straight ruler
                points.Add(from + dir * s + side * wobble);
            }
        }

        private static void AddArc(List<Vector3> points, Vector3 start, float startAngle, float radius, SeededRandom random)
        {
            // Semicircle to the right of travel, centre offset from the start.
            Vector3 centre = start + new Vector3(radius, 0f, 0f);
            for (int i = 0; i <= 8; i++)
            {
                float a = startAngle + Mathf.PI * i / 8f;
                points.Add(centre + new Vector3(-Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
        }

        /// <summary>
        /// The oncoming carriageway: the same ring walked the other way, to the right of the main road
        /// (left-hand traffic: oncoming is on your right). Same points, offset, reversed.
        /// </summary>
        public static Corridor BuildOncoming(Corridor main)
        {
            var points = new List<Vector3>();
            for (float s = main.Length; s > 0f; s -= 25f)
            {
                points.Add(main.PositionAt(s, OncomingOffset));
            }
            return new Corridor(points, RoadWidth, closed: true, name: "oncoming");
        }

        /// <summary>A cross street through the main road at s, perpendicular, centred on it.</summary>
        public static Corridor BuildCrossStreet(Corridor main, float s, int index)
        {
            // Centred between the two carriageways, so it reaches equally far either side.
            Vector3 centre = main.PositionAt(s, OncomingOffset * 0.5f);
            Vector3 right = main.RightAt(s);
            var points = new List<Vector3>
            {
                centre - right * (CrossStreetLength * 0.5f),
                centre + right * (CrossStreetLength * 0.5f),
            };
            return new Corridor(points, CrossWidth, false, "cross" + index);
        }

        /// <summary>
        /// Building boxes as a flat float array for the renderer: x, z, yaw, length (along the road),
        /// width (across), height, repeated. Both sides, with a gap at every cross street.
        /// </summary>
        public static float[] BuildBuildings(Corridor corridor, SeededRandom random)
        {
            var data = new List<float>();
            for (int side = -1; side <= 1; side += 2)
            {
                float s = 5f;
                while (s < corridor.Length - 25f)
                {
                    float length = random.Range(10f, 22f);
                    if (NearJunction(s, length))
                    {
                        s += 4f;
                        continue;
                    }
                    float width = random.Range(10f, 20f);
                    float height = random.Range(2, 8) * 3f;             // 2–7 storeys of concrete
                    float setBack = PavementMetres + random.Range(0f, 2f);
                    // The right side sits beyond the oncoming carriageway.
                    float beyond = side > 0 ? OncomingOffset : 0f;
                    float lateral = side * (corridor.HalfWidth + setBack + width * 0.5f + beyond);
                    float mid = s + length * 0.5f;
                    Vector3 centre = corridor.PositionAt(mid, lateral);

                    data.Add(centre.x); data.Add(centre.z); data.Add(corridor.YawAt(mid));
                    data.Add(length); data.Add(width); data.Add(height);

                    s += length + random.Range(1.5f, 4f);
                }
            }
            return data.ToArray();
        }

        private static bool NearJunction(float s, float length)
        {
            foreach (float js in JunctionS)
            {
                if (s + length > js - CrossWidth * 0.5f - 6f && s < js + CrossWidth * 0.5f + 6f) return true;
            }
            return false;
        }

        /// <summary>Road edge polyline for the renderer: x, z pairs. Closed loops repeat the first point.</summary>
        public static float[] RoadEdge(Corridor corridor, float lateral)
        {
            var data = new List<float>();
            for (float s = 0f; s <= corridor.Length; s += 5f)
            {
                Vector3 p = corridor.PositionAt(s, lateral);
                data.Add(p.x); data.Add(p.z);
            }
            if (corridor.Closed)
            {
                Vector3 p = corridor.PositionAt(0f, lateral);
                data.Add(p.x); data.Add(p.z);
            }
            return data.ToArray();
        }
    }
}
