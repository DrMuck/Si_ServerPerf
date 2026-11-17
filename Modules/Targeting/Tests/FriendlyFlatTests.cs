#if FRIENDLYFLAT_OFFLINE_TESTS
// Offline test of FlatCone (FriendlyFireFlatKernel.cs). Vanilla reference = 1.0.17 Target.cs:685-711 transliterated
// with Unity's managed implementations (UnityEngine.CoreModule 1.0.17, ilspycmd):
//   Vector3 - Vector3 / Vector3 / float  component-wise float ops
//   Vector3.magnitude                    (float)Math.Sqrt(x * x + y * y + z * z)
//   Vector3.Angle(from, to)              num = (float)Math.Sqrt(from.sqrMagnitude * to.sqrMagnitude); if (num < 1E-15f) 0;
//                                        Mathf.Clamp(Dot(from, to) / num, -1f, 1f); (float)Math.Acos(num2) * 57.29578f
//   Mathf.Atan2(y, x)                    (float)Math.Atan2(y, x);  Mathf.Abs = Math.Abs(float)
// Property: FlatCone.Rejects(...) == true  =>  vanilla's "num4 - num5 > spread" is true (the entry is "continue"d).
using System;
using System.Collections.Generic;
using SiServerPerf.Modules.Targeting;

static class FriendlyFlatTests
{
    struct V { public float x, y, z; public V(float a, float b, float c) { x = a; y = b; z = c; } }

    static V Sub(V a, V b) => new V(a.x - b.x, a.y - b.y, a.z - b.z);
    static V Div(V a, float d) => new V(a.x / d, a.y / d, a.z / d);
    static float Mag(V v) => (float)Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
    static float Sq(V v) => v.x * v.x + v.y * v.y + v.z * v.z;
    static float Dot(V a, V b) => a.x * b.x + a.y * b.y + a.z * b.z;
    static float Clamp(float value, float min, float max) { if (value < min) value = min; else if (value > max) value = max; return value; }
    static float Angle(V from, V to)
    {
        float num = (float)Math.Sqrt(Sq(from) * Sq(to));
        if (num < 1E-15f) return 0f;
        float num2 = Clamp(Dot(from, to) / num, -1f, 1f);
        return (float)Math.Acos(num2) * 57.29578f;
    }

    // vanilla :700-711 for one entry that passed :686-694
    static bool VanillaConeRejects(V direction, V vector2, float magnitude, float radius, float num, float spread)
    {
        vector2 = Div(vector2, magnitude);
        magnitude -= num;
        if (magnitude < 0f) magnitude = 0f;
        float num4 = Angle(direction, vector2);
        float num5 = Math.Abs((float)Math.Atan2(radius + num, magnitude) * 57.29578f);
        return num4 - num5 > spread;
    }

    static Random R;
    static double U(double a, double b) => a + (b - a) * R.NextDouble();
    static double LogU(double a, double b) => Math.Exp(U(Math.Log(a), Math.Log(b)));
    static double[] UnitD()
    {
        while (true)
        {
            double x = U(-1, 1), y = U(-1, 1), z = U(-1, 1), l = Math.Sqrt(x * x + y * y + z * z);
            if (l > 1e-3 && l <= 1) return new[] { x / l, y / l, z / l };
        }
    }

    static long s_Cases, s_Rejects, s_VanillaRejects, s_Violations;
    static int s_Logged;

    static void Check(string kind, V dir, V v, float radius, float num, float spread, float maxDist)
    {
        float magnitude = Mag(v);
        FlatConeQuery q = FlatCone.Prepare(dir.x, dir.y, dir.z, spread, maxDist, num);
        bool flat = FlatCone.Rejects(ref q, v.x, v.y, v.z, magnitude, radius, num);
        bool van = VanillaConeRejects(dir, v, magnitude, radius, num, spread);
        s_Cases++;
        if (flat) s_Rejects++;
        if (van) s_VanillaRejects++;
        if (flat && !van)
        {
            s_Violations++;
            if (s_Logged++ < 20)
                Console.WriteLine($"VIOLATION {kind}: dir ({dir.x:R},{dir.y:R},{dir.z:R}) v ({v.x:R},{v.y:R},{v.z:R}) |v| {magnitude:R} R {radius:R} num {num:R} spread {spread:R} maxDist {maxDist:R}");
        }
    }

    static V Dir()
    {
        double[] d = UnitD();
        double len = R.NextDouble() < 0.8 ? 1.0 : U(0.3, 3.0);
        return new V((float)(d[0] * len), (float)(d[1] * len), (float)(d[2] * len));
    }

    static float Spread()
    {
        double p = R.NextDouble();
        if (p < 0.6) return (float)U(0, 5);
        if (p < 0.9) return (float)U(5, 90);
        if (p < 0.95) return 0f;
        return (float)U(-10, 200);   // outside the guard: must keep
    }

    static float Num() => R.NextDouble() < 0.5 ? 0f : (float)U(0, 20);
    static float Radius() => (float)LogU(0.2, 40);

    // rotate unit d by angle t (rad) about a random axis perpendicular to d
    static double[] Rotate(double[] d, double t)
    {
        double[] a = UnitD();
        double dp = a[0] * d[0] + a[1] * d[1] + a[2] * d[2];
        double[] p = { a[0] - dp * d[0], a[1] - dp * d[1], a[2] - dp * d[2] };
        double pl = Math.Sqrt(p[0] * p[0] + p[1] * p[1] + p[2] * p[2]);
        if (pl < 1e-6) return Rotate(d, t);
        for (int i = 0; i < 3; i++) p[i] /= pl;
        double c = Math.Cos(t), s = Math.Sin(t);
        return new[] { d[0] * c + p[0] * s, d[1] * c + p[1] * s, d[2] * c + p[2] * s };
    }

    static int Main(string[] args)
    {
        int cases = args.Length > 0 ? int.Parse(args[0]) : 2000000;
        int seed = args.Length > 1 ? int.Parse(args[1]) : 12345;
        R = new Random(seed);
        Console.WriteLine($"FriendlyFlatTests: {cases} cases per family, seed {seed}, CosMargin {FlatCone.CosMargin}");

        // A: uniform random geometry
        for (int i = 0; i < cases; i++)
        {
            V dir = Dir();
            double[] u = UnitD();
            double m = LogU(1e-4, 5000);
            V v = new V((float)(u[0] * m), (float)(u[1] * m), (float)(u[2] * m));
            float mag = Mag(v);
            Check("random", dir, v, Radius(), Num(), Spread(), mag * (float)U(1, 2));
        }
        long aCases = s_Cases, aRej = s_Rejects, aVan = s_VanillaRejects;

        // B: adversarial, placed at vanilla's boundary theta = s + alpha + delta
        double[] deltas = { -1e-2, -1e-3, -1e-5, 0, 1e-7, 1e-6, 1e-5, 1e-4, 1e-3, 3e-3, 5e-3, 5.5e-3, 5.7e-3, 6e-3, 7e-3, 1e-2, 0.1 };
        for (int i = 0; i < cases; i++)
        {
            double[] dd = UnitD();
            double len = R.NextDouble() < 0.8 ? 1.0 : U(0.3, 3.0);
            V dir = new V((float)(dd[0] * len), (float)(dd[1] * len), (float)(dd[2] * len));
            double[] dn = { dir.x / len, dir.y / len, dir.z / len };
            float spread = R.NextDouble() < 0.2 ? 0f : (float)(R.NextDouble() < 0.7 ? U(0, 5) : U(0, 90));
            float num = Num(), radius = R.NextDouble() < 0.1 ? 0f : Radius();
            double m = LogU(1e-3, 5000);
            double m1 = Math.Max(0.0, m - num);
            double alpha = Math.Atan2(Math.Abs(radius + num), m1) * 180.0 / Math.PI;
            double theta = spread + alpha + deltas[R.Next(deltas.Length)];
            if (theta < 0 || theta > 180) continue;
            double[] w = Rotate(dn, theta * Math.PI / 180.0);
            V v = new V((float)(w[0] * m), (float)(w[1] * m), (float)(w[2] * m));
            Check("boundary", dir, v, radius, num, spread, Mag(v) * (float)U(1, 2));
        }
        long bCases = s_Cases - aCases, bRej = s_Rejects - aRej, bVan = s_VanillaRejects - aVan;

        // C: degenerate inputs (must keep, or at least never reject what vanilla keeps)
        float[] specials = { 0f, 1e-30f, 1e-8f, 1e-3f, 1f, 1e6f, 1e19f, float.MaxValue, float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f };
        for (int i = 0; i < cases / 4; i++)
        {
            float Pick() => R.NextDouble() < 0.3 ? specials[R.Next(specials.Length)] * (R.NextDouble() < 0.5 ? 1 : -1) : (float)U(-100, 100);
            V dir = R.NextDouble() < 0.5 ? Dir() : new V(Pick(), Pick(), Pick());
            V v = new V(Pick(), Pick(), Pick());
            float radius = R.NextDouble() < 0.3 ? specials[R.Next(specials.Length)] : Radius();
            float num = R.NextDouble() < 0.2 ? specials[R.Next(specials.Length)] : Num();
            float spread = R.NextDouble() < 0.2 ? specials[R.Next(specials.Length)] : Spread();
            float maxDist = R.NextDouble() < 0.2 ? specials[R.Next(specials.Length)] : Mag(v) * 1.5f;
            Check("degenerate", dir, v, radius, num, spread, maxDist);
        }

        Console.WriteLine($"  random     {aCases,9} cases: vanilla rejects {aVan,9}, pre-test rejects {aRej,9} ({(aVan > 0 ? 100.0 * aRej / aVan : 0):0.00}% of vanilla's)");
        Console.WriteLine($"  boundary   {bCases,9} cases: vanilla rejects {bVan,9}, pre-test rejects {bRej,9} ({(bVan > 0 ? 100.0 * bRej / bVan : 0):0.00}% of vanilla's)");
        Console.WriteLine($"  all        {s_Cases,9} cases, violations (pre-test rejects, vanilla keeps): {s_Violations}");

        // D: list walk simulation, vanilla loop (:677-794 with an opaque tail) vs flat loop
        int walkFail = 0, walks = Math.Max(1000, cases / 100);
        long tailsV = 0, tailsF = 0, entries = 0, inRange = 0, skipped = 0;
        for (int t = 0; t < walks; t++)
        {
            int n = R.Next(1, 260);
            V from = new V((float)U(-3000, 3000), (float)U(0, 300), (float)U(-3000, 3000));
            V dir = Dir();
            float spread = (float)U(0, 6), num = Num();
            float maxDist = (float)U(20, 400) + num;
            var pos = new V[n]; var rad = new float[n]; var ignore = new bool[n]; var tailHit = new bool[n];
            double cluster = U(20, 300);
            for (int i = 0; i < n; i++)
            {
                double[] u = UnitD();
                double m = U(0, cluster);
                pos[i] = new V(from.x + (float)(u[0] * m), from.y + (float)(u[1] * m * 0.2), from.z + (float)(u[2] * m));
                rad[i] = R.NextDouble() < 0.02 ? float.NaN : Radius();       // NaN = stale entry
                ignore[i] = R.NextDouble() < 0.1;                             // ignoreTarget / ignore level / unrepairable
                tailHit[i] = R.NextDouble() < 0.3;                            // damage / bounds / raycast outcome
            }
            var seqV = new List<int>(); var seqF = new List<int>();
            int resV = -1, resF = -1;
            for (int i = 0; i < n; i++)
            {
                V v2 = Sub(pos[i], from);
                if (Math.Abs(v2.x) > maxDist || Math.Abs(v2.y) > maxDist || Math.Abs(v2.z) > maxDist) continue;
                float mag = Mag(v2);
                if (mag > maxDist) continue;
                if (ignore[i]) continue;
                if (float.IsNaN(rad[i])) { seqV.Add(i); resV = i; break; }    // vanilla throws here: stop
                if (VanillaConeRejects(dir, v2, mag, rad[i], num, spread)) continue;
                seqV.Add(i);
                if (tailHit[i]) { resV = i; break; }
            }
            FlatConeQuery q = FlatCone.Prepare(dir.x, dir.y, dir.z, spread, maxDist, num);
            for (int i = 0; i < n; i++)
            {
                entries++;
                V v2 = Sub(pos[i], from);
                if (Math.Abs(v2.x) > maxDist || Math.Abs(v2.y) > maxDist || Math.Abs(v2.z) > maxDist) continue;
                float mag = Mag(v2);
                if (mag > maxDist) continue;
                inRange++;
                if (FlatCone.Rejects(ref q, v2.x, v2.y, v2.z, mag, rad[i], num)) { skipped++; continue; }
                // from here vanilla's code, verbatim
                if (ignore[i]) continue;
                if (float.IsNaN(rad[i])) { seqF.Add(i); resF = i; break; }
                if (VanillaConeRejects(dir, v2, mag, rad[i], num, spread)) continue;
                seqF.Add(i);
                if (tailHit[i]) { resF = i; break; }
            }
            tailsV += seqV.Count; tailsF += seqF.Count;
            bool same = resV == resF && seqV.Count == seqF.Count;
            for (int k = 0; same && k < seqV.Count; k++) same = seqV[k] == seqF[k];
            if (!same && walkFail++ < 10) Console.WriteLine($"WALK MISMATCH trial {t}: vanilla first hit {resV}, flat {resF}, tails {seqV.Count} vs {seqF.Count}");
        }
        Console.WriteLine($"  walks      {walks,9}: entries {entries}, in range {inRange}, cone-skipped {skipped} ({(inRange > 0 ? 100.0 * skipped / inRange : 0):0.0}%), " +
                          $"tail entries vanilla {tailsV} flat {tailsF}, mismatches {walkFail}");

        bool ok = s_Violations == 0 && walkFail == 0;
        Console.WriteLine(ok ? "ALL PASSED" : "FAILED");
        return ok ? 0 : 1;
    }
}
#endif
