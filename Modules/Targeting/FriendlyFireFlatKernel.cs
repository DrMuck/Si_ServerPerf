/*
 Pure part of FriendlyFire_Flat (see FriendlyFireFlat.cs): the conservative cone pre-test. Unity-free (System.Math only),
 compiled unchanged into the mod and into the offline test (Tests\FriendlyFlatTests.csproj).

 Vanilla's cone test (1.0.17 Target.cs:700-711), per entry that passed the box and distance tests:
     vector2 /= magnitude;  magnitude -= num;  if (magnitude < 0f) magnitude = 0f;
     num4 = Vector3.Angle(direction, vector2);                                    // acos, degrees
     num5 = Mathf.Abs(Mathf.Atan2(target.Owner.PhysicalRadius + num, magnitude) * 57.29578f);
     if (num4 - num5 > spread) continue;
 i.e. reject iff  theta > s + alpha  with theta = angle(direction, vector2), alpha = atan2(|R + num|, m'), s = spread.
 For s in [0, 90] deg and alpha in [0, 90] deg, s + alpha lies in [0, 180] where cos is strictly decreasing, so
     theta > s + alpha   <=>   cos(theta) < cos(s + alpha) = cos s * m'/h - sin s * |R'|/h,   h = sqrt(m'^2 + R'^2).
 Rejects() returns true only if  cos(theta) < cos(s + alpha) - CosMargin  (computed in double, no acos / atan2).

 Why that implies vanilla's float test rejects too (margin argument):
   * |d acos/dc| >= 1, so a cos margin of 1e-4 is an angle margin >= 1e-4 rad = 0.0057 deg.
   * Vanilla's float cosine (vector2 / magnitude, sqrMagnitude products, sqrt, Dot, divide) differs from the exact one
     by <= ~1.5e-6 (a few float ulps of values <= 1); ours (double, float magnitude in the denominator) by <= ~3e-7.
     Both << 1e-4, so vanilla's acos argument is still below cos(s + alpha) - 0.98e-4 -> acos >= s + alpha + 0.98e-4 rad.
   * The remaining float steps (cast of acos / atan2 to float, * 57.29578f vs 180/pi, the subtraction) cost
     <= ~1e-4 deg in total; 0.0056 deg of margin remains, so num4 - num5 > spread holds in vanilla's arithmetic.
   * NaN anywhere (stale entry radius = NaN, 0/0) makes the comparison false = keep; vanilla then decides.
 Guards (Prepare / Rejects return "keep" outside them, vanilla's own code then runs on the entry): spread in [0, 90],
 maxDist finite and <= 1e6, num finite, |direction|^2 in [1e-6, 1e6] (vanilla's Angle never hits its 1e-15 branch and
 no float overflow), magnitude >= 1e-3 (no float underflow in vanilla's normalisation).
 The offline test checks "Rejects => vanilla rejects" on random and adversarial (boundary +- 1e-6 deg) cases.
*/

using System;

namespace SiServerPerf.Modules.Targeting
{
    internal struct FlatConeQuery
    {
        public bool On;
        public double Dx, Dy, Dz, InvDLen, Cs, Ss;
    }

    internal static class FlatCone
    {
        internal const double CosMargin = 1e-4;

        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        // Per call. maxDist and num as vanilla has them after "maxDist += num".
        public static FlatConeQuery Prepare(float dx, float dy, float dz, float spread, float maxDist, float num)
        {
            FlatConeQuery q = default(FlatConeQuery);
            if (!(spread >= 0f && spread <= 90f) || !Finite(maxDist) || maxDist > 1e6f || !Finite(num)) return q;
            if (!Finite(dx) || !Finite(dy) || !Finite(dz)) return q;
            double ddx = dx, ddy = dy, ddz = dz;
            double dd = ddx * ddx + ddy * ddy + ddz * ddz;
            if (!(dd >= 1e-6 && dd <= 1e6)) return q;
            double s = spread * (Math.PI / 180.0);
            q.Dx = ddx; q.Dy = ddy; q.Dz = ddz;
            q.InvDLen = 1.0 / Math.Sqrt(dd);
            q.Cs = Math.Cos(s);
            q.Ss = Math.Sin(s);
            q.On = true;
            return q;
        }

        // Per entry that passed vanilla's box and distance tests. vx/vy/vz = vanilla's vector2 (LOS position - fromPoint,
        // before normalisation), magnitude = vector2.magnitude, radius = Owner.PhysicalRadius (NaN = unknown -> keep).
        // true = vanilla's cone test certainly rejects this entry ("continue"); false = let vanilla's code decide.
        public static bool Rejects(ref FlatConeQuery q, float vx, float vy, float vz, float magnitude, float radius, float num)
        {
            if (!q.On || !(magnitude >= 1e-3f)) return false;
            float m1 = magnitude - num;          // vanilla: magnitude -= num; clamp at 0
            if (m1 < 0f) m1 = 0f;
            float r1 = radius + num;             // vanilla: target.Owner.PhysicalRadius + num
            double m = m1, ar = Math.Abs((double)r1);
            double h = Math.Sqrt(m * m + ar * ar);
            double c = (q.Dx * vx + q.Dy * vy + q.Dz * vz) * q.InvDLen / magnitude;
            double bound = (q.Cs * m - q.Ss * ar) / h - CosMargin;
            return c < bound;                    // NaN (radius unknown, h == 0) -> false -> keep
        }
    }
}
