using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>
    /// Small geometric helpers. Vehicles on different corridors can't be compared in corridor
    /// coordinates, so inside a junction they are treated as capsules (a line from rear to front
    /// with the half width as radius) and tested in world space.
    /// </summary>
    public static class Geometry
    {
        /// <summary>
        /// Closest points between segments p1–q1 and p2–q2 (Ericson, Real-Time Collision Detection).
        /// Returns the distance between them.
        /// </summary>
        public static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 c1, out Vector3 c2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            const float eps = 1e-6f;

            if (a <= eps && e <= eps) { c1 = p1; c2 = p2; return (c1 - c2).magnitude; }
            if (a <= eps) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= eps) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom != 0f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
            return (c1 - c2).magnitude;
        }

        /// <summary>The segment from an agent's rear to its front, in world space.</summary>
        public static void Axis(Agent a, out Vector3 rear, out Vector3 front)
        {
            Vector3 forward = new Vector3(Mathf.Sin(a.Yaw), 0f, Mathf.Cos(a.Yaw));
            rear = a.Position - forward * a.HalfLength;
            front = a.Position + forward * a.HalfLength;
        }
    }
}
