using System.Collections.Generic;
using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>
    /// A lane-free corridor: a centreline (polyline on the XZ plane) plus a width.
    ///
    /// RESEARCH.md (Steering): "Lane-free corridors: centreline + width, free lateral offset, no lanes."
    /// Every vehicle describes where it is in corridor coordinates:
    ///   S        = distance along the centreline from its start, in metres
    ///   Lateral  = offset to the right (+) or left (−) of the centreline, in metres
    /// That makes "who is ahead of me" a comparison of S values and "are we side by side" a
    /// comparison of Lateral values, which is all the steering maths needs. World positions are
    /// derived from (S, Lateral) for rendering.
    ///
    /// Yaw follows Unity's convention: radians around the Y axis, forward = (sin yaw, 0, cos yaw).
    /// Later the Unity side will build one of these from a Unity Spline; the maths stays here.
    /// </summary>
    public sealed class Corridor
    {
        public readonly List<Vector3> Points = new List<Vector3>();
        public readonly float Width;

        // _cumulative[i] = distance along the polyline at Points[i].
        private readonly float[] _cumulative;

        public Corridor(IList<Vector3> points, float width)
        {
            if (points.Count < 2) throw new System.ArgumentException("A corridor needs at least two points.");
            Width = width;
            Points.AddRange(points);
            _cumulative = new float[Points.Count];
            for (int i = 1; i < Points.Count; i++)
            {
                _cumulative[i] = _cumulative[i - 1] + Vector3.Distance(Points[i - 1], Points[i]);
            }
        }

        public float Length => _cumulative[_cumulative.Length - 1];
        public float HalfWidth => Width * 0.5f;

        /// <summary>World position for corridor coordinates.</summary>
        public Vector3 PositionAt(float s, float lateral)
        {
            int i = SegmentAt(s, out float t);
            Vector3 onLine = Vector3.Lerp(Points[i], Points[i + 1], t);
            return onLine + RightOfSegment(i) * lateral;
        }

        /// <summary>Unit direction of travel at s.</summary>
        public Vector3 TangentAt(float s)
        {
            int i = SegmentAt(s, out _);
            return (Points[i + 1] - Points[i]).normalized;
        }

        /// <summary>Unit vector pointing to the right of the direction of travel at s.</summary>
        public Vector3 RightAt(float s)
        {
            int i = SegmentAt(s, out _);
            return RightOfSegment(i);
        }

        /// <summary>Yaw (radians) of the direction of travel at s.</summary>
        public float YawAt(float s)
        {
            Vector3 t = TangentAt(s);
            return Mathf.Atan2(t.x, t.z);
        }

        /// <summary>
        /// Corridor coordinates of a world point: the nearest point on the polyline gives S, the
        /// signed distance to it gives Lateral. Used for the player's bus, which moves in world
        /// space and must be placed into the same coordinates as everyone else.
        /// </summary>
        public void Project(Vector3 world, out float s, out float lateral)
        {
            float bestDistanceSq = float.MaxValue;
            s = 0f;
            lateral = 0f;
            for (int i = 0; i < Points.Count - 1; i++)
            {
                Vector3 a = Points[i];
                Vector3 ab = Points[i + 1] - a;
                float lengthSq = ab.sqrMagnitude;
                if (lengthSq < 1e-6f) continue;

                // Parameter of the closest point on the segment, clamped to the segment.
                float t = Mathf.Clamp01(Vector3.Dot(world - a, ab) / lengthSq);
                Vector3 closest = a + ab * t;
                float distanceSq = (world - closest).sqrMagnitude;
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    s = _cumulative[i] + Mathf.Sqrt(lengthSq) * t;
                    lateral = Vector3.Dot(world - closest, RightOfSegment(i));
                }
            }
        }

        private Vector3 RightOfSegment(int i)
        {
            Vector3 tangent = (Points[i + 1] - Points[i]).normalized;
            // Cross(up, forward) gives right in a Y-up, left-handed frame like Unity's.
            return Vector3.Cross(Vector3.up, tangent);
        }

        /// <summary>Segment index containing s, plus how far along it (0..1). Clamped to the ends.</summary>
        private int SegmentAt(float s, out float t)
        {
            s = Mathf.Clamp(s, 0f, Length);
            // Linear scan is fine: corridors have tens of points, not thousands.
            for (int i = 0; i < _cumulative.Length - 1; i++)
            {
                if (s <= _cumulative[i + 1] || i == _cumulative.Length - 2)
                {
                    float segmentLength = _cumulative[i + 1] - _cumulative[i];
                    t = segmentLength > 1e-6f ? (s - _cumulative[i]) / segmentLength : 0f;
                    return i;
                }
            }
            t = 1f;
            return _cumulative.Length - 2;
        }
    }
}
