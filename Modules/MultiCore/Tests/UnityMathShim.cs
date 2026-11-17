#if MULTICORE_OFFLINE_TESTS
// Copies of the UnityEngine value-type math ScanKernel.cs uses, from the 1.0.17 UnityEngine.CoreModule (ilspycmd), so the
// kernel compiles unchanged outside Unity. Same expression shapes as Unity's managed implementations.
using System;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;

        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public static Vector3 zero => new Vector3(0f, 0f, 0f);

        public Vector3 normalized => Normalize(this);

        public static Vector3 Normalize(Vector3 value)
        {
            float num = Magnitude(value);
            if (num > 1E-05f)
            {
                return value / num;
            }
            return zero;
        }

        public static float Magnitude(Vector3 vector) => (float)Math.Sqrt(vector.x * vector.x + vector.y * vector.y + vector.z * vector.z);

        public static float Dot(Vector3 lhs, Vector3 rhs) => lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z;

        public static float Distance(Vector3 a, Vector3 b)
        {
            float num = a.x - b.x;
            float num2 = a.y - b.y;
            float num3 = a.z - b.z;
            return (float)Math.Sqrt(num * num + num2 * num2 + num3 * num3);
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);
    }

    public struct Quaternion
    {
        public float x, y, z, w;

        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }

        public static Vector3 operator *(Quaternion rotation, Vector3 point)
        {
            float num = rotation.x * 2f;
            float num2 = rotation.y * 2f;
            float num3 = rotation.z * 2f;
            float num4 = rotation.x * num;
            float num5 = rotation.y * num2;
            float num6 = rotation.z * num3;
            float num7 = rotation.x * num2;
            float num8 = rotation.x * num3;
            float num9 = rotation.y * num3;
            float num10 = rotation.w * num;
            float num11 = rotation.w * num2;
            float num12 = rotation.w * num3;
            Vector3 result = default(Vector3);
            result.x = (1f - (num5 + num6)) * point.x + (num7 - num12) * point.y + (num8 + num11) * point.z;
            result.y = (num7 + num12) * point.x + (1f - (num4 + num6)) * point.y + (num9 - num10) * point.z;
            result.z = (num8 - num11) * point.x + (num9 + num10) * point.y + (1f - (num4 + num5)) * point.z;
            return result;
        }
    }

    public static class Mathf
    {
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Max(float a, float b) => (a > b) ? a : b;
        public static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }
            if (value > 1f)
            {
                return 1f;
            }
            return value;
        }
    }
}
#endif
