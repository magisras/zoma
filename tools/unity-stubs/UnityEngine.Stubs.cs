// Minimal stand-ins for the UnityEngine types our scripts touch, so the code can be compiled and
// unit-tested with a plain C# compiler (Mono's mcs) when Unity is not available: in CI, in a
// cloud session, or on a laptop without the editor open.
//
// This is NOT UnityEngine. It only has to be *shape-compatible* for compiling and testing logic.
// Add a type here only when a script under Assets/ needs it; keep behaviour-free except for pure
// maths. Unity itself never sees this file (it lives outside Assets/).

using System;

namespace UnityEngine
{
    public class Object { public string name; }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    public class MonoBehaviour : Object { }

    // Inspector attributes. Unity reads them; we just need them to exist.
    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string tooltip) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string header) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeFieldAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class CreateAssetMenuAttribute : Attribute { public string fileName; public string menuName; public int order; }

    public static class Mathf
    {
        public const float Epsilon = 1.4e-45f;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Abs(float v) => Math.Abs(v);
        public static float Pow(float b, float e) => (float)Math.Pow(b, e);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static bool Approximately(float a, float b) => Math.Abs(a - b) < 1e-5f;
    }
}
