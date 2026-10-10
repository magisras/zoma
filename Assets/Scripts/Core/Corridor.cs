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
    /// A corridor may be closed (a loop): then S wraps around and "ahead" is measured the short way
    /// round, see <see cref="DeltaS"/>. Every piece of code that compares two S values must go
    /// through DeltaS so loops and open roads behave the same.
    ///
    /// Yaw follows Unity's convention: radians around the Y axis, forward = (sin yaw, 0, cos yaw).
    /// Later the Unity side will build one of these from a Unity Spline; the maths stays here.
    /// </summary>
    public sealed class Corridor
    {
        public readonly string Name;
        public readonly List<Vector3> Points = new List<Vector3>();
        public readonly float Width;
        public readonly bool Closed;

        // _cumulative[i] = distance along the polyline at the start of segment i.
        private readonly float[] _cumulative;
        private readonly int _segmentCount;
        private readonly float _length;

        public Corridor(IList<Vector3> points, float width, bool closed = false, string name = "road")
        {
            if (points.Count < 2) throw new System.ArgumentException("A corridor needs at least two points.");
            Name = name;
            Width = width;
            Closed = closed;
            Points.AddRange(points);
            _segmentCount = closed ? Points.Count : Points.Count - 1;
            _cumulative = new float[_segmentCount + 1];
            for (int i = 0; i < _segmentCount; i++)
            {
                _cumulative[i + 1] = _cumulative[i] + Vector3.Distance(Points[i], Points[(i + 1) % Points.Count]);
            }
            _length = _cumulative[_segmentCount];
        }

        public float Length => _length;
        public float HalfWidth => Width * 0.5f;

        /// <summary>S brought into range: wrapped on a loop, clamped on an open road.</summary>
        public float Wrap(float s)
        {
            if (!Closed) return Mathf.Clamp(s, 0f, _length);
            s = Mathf.Repeat(s, _length);
            return s;
        }

        /// <summary>
        /// Signed distance along the road from one S to another: positive when <paramref name="to"/>
        /// is ahead. On a loop it is the short way round, in [−Length/2, Length/2).
        /// </summary>
        public float DeltaS(float from, float to)
        {
            float d = to - from;
            if (!Closed) return d;
            d = Mathf.Repeat(d + _length * 0.5f, _length) - _length * 0.5f;
            return d;
        }

        /// <summary>World position for corridor coordinates.</summary>
        public Vector3 PositionAt(float s, float lateral)
        {
            int i = SegmentAt(s, out float t);
            Vector3 onLine = Vector3.Lerp(Points[i], Points[(i + 1) % Points.Count], t);
            return onLine + RightOfSegment(i) * lateral;
        }

        /// <summary>Unit direction of travel at s.</summary>
        public Vector3 TangentAt(float s)
        {
            int i = SegmentAt(s, out _);
            return TangentOfSegment(i);
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
            for (int i = 0; i < _segmentCount; i++)
            {
                Vector3 a = Points[i];
                Vector3 ab = Points[(i + 1) % Points.Count] - a;
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

        /// <summary>
        /// The same projection, but only onto the stretch of road between two S values (segments that
        /// overlap it), with the plain distance to the closest point as well. A loop that runs out along
        /// one carriageway and back along the other has two stretches a few metres apart; the caller
        /// decides which one the vehicle is on, with memory and heading, instead of the nearest line.
        /// </summary>
        public void Project(Vector3 world, float sFrom, float sTo, out float s, out float lateral, out float distance)
        {
            float bestDistanceSq = float.MaxValue;
            s = sFrom;
            lateral = 0f;
            for (int i = 0; i < _segmentCount; i++)
            {
                if (_cumulative[i + 1] < sFrom || _cumulative[i] > sTo) continue;
                Vector3 a = Points[i];
                Vector3 ab = Points[(i + 1) % Points.Count] - a;
                float lengthSq = ab.sqrMagnitude;
                if (lengthSq < 1e-6f) continue;
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
            distance = bestDistanceSq < float.MaxValue ? Mathf.Sqrt(bestDistanceSq) : float.MaxValue;
        }

        /// <summary>Distance from a world point to the centreline, for "which corridor am I on?".</summary>
        public float DistanceTo(Vector3 world)
        {
            Project(world, out float s, out float lateral);
            return Mathf.Abs(lateral);
        }

        private Vector3 TangentOfSegment(int i)
        {
            return (Points[(i + 1) % Points.Count] - Points[i]).normalized;
        }

        private Vector3 RightOfSegment(int i)
        {
            // Cross(up, forward) gives right in a Y-up, left-handed frame like Unity's.
            return Vector3.Cross(Vector3.up, TangentOfSegment(i));
        }

        /// <summary>Segment index containing s, plus how far along it (0..1).</summary>
        private int SegmentAt(float s, out float t)
        {
            s = Wrap(s);
            // Linear scan is fine: corridors have tens of points, not thousands.
            for (int i = 0; i < _segmentCount; i++)
            {
                if (s <= _cumulative[i + 1] || i == _segmentCount - 1)
                {
                    float segmentLength = _cumulative[i + 1] - _cumulative[i];
                    t = segmentLength > 1e-6f ? Mathf.Clamp01((s - _cumulative[i]) / segmentLength) : 0f;
                    return i;
                }
            }
            t = 1f;
            return _segmentCount - 1;
        }
    }
}
