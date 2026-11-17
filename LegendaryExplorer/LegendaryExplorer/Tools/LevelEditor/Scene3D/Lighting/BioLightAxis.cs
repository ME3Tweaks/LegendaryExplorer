using System;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// The Bio key light axis search, identical in LE2 and LE3. RVAs (seeds / worker / search / gradient):
/// LE2 0x18ddc0 / 0x5d0d0 / 0x6f7a0 / 0x6f390; LE3 0x1a4170 / 0x71510 / 0x874e0 / 0x870d0.
/// Its seeds come from the previous direction, so the result depends on that state. The game may search asynchronously; this settles immediately.
/// </summary>
internal sealed class BioLightAxisState
{
    //SH changes below this summed squared difference reuse the cached direction
    private const float ReuseThreshold = 1f / 65536;

    private SHVector CachedFunction;
    private Vector3 CachedDirection;
    private bool HasResult;

    /// <summary>The last extracted key direction, which seeds the next search</summary>
    public Vector3 PreviousDirection;

    public Vector3 FindKeyDirection(in SHVector luminance)
    {
        if (HasResult)
        {
            float difference = 0;
            for (int i = 0; i < SHVector.NumCoefficients; i++)
            {
                float delta = luminance[i] - CachedFunction[i];
                difference += delta * delta;
            }
            if (difference < ReuseThreshold)
                return CachedDirection;
        }
        CachedFunction = luminance;
        CachedDirection = Search(luminance, PreviousDirection, out HasResult);
        return CachedDirection;
    }

    /// <summary>A fresh search from the previous direction, as every role but the non-cinematic key does (LE2 0x18ddc0)</summary>
    public Vector3 FindDirection(in SHVector luminance) => Search(luminance, PreviousDirection, out _);

    /// <summary>The six seeds and the worker: the highest strictly positive result, otherwise zero</summary>
    internal static Vector3[] Seeds(Vector3 previous)
    {
        //A previous direction no longer than 0.01 falls back to +X
        Vector3 d = previous.LengthSquared() > 1e-4f ? previous : Vector3.UnitX;
        //Two perpendiculars from the native cross products (not renormalized)
        Vector3 a = new(d.X, -d.Y, 0);
        if (MathF.Abs(-d.Y) < 1e-4f && MathF.Abs(d.X) < 1e-4f)
            a = new Vector3(0, d.Z, -d.X);
        a /= a.Length();
        Vector3 e1 = new(a.Z * d.Y - a.X * d.Z, a.Y * d.Z - a.Z * d.X, a.X * d.X - a.Y * d.Y);
        Vector3 e2 = new(e1.Z * d.Y - e1.Y * d.Z, e1.X * d.Z - e1.Z * d.X, e1.Y * d.X - e1.X * d.Y);
        return [d, -d, e1, -e1, e2, -e2];
    }

    internal static Vector3 Search(in SHVector function, Vector3 previous, out bool found)
    {
        Vector3[] seeds = Seeds(previous);
        float best = 0;
        Vector3 bestDirection = Vector3.Zero;
        foreach (Vector3 seed in seeds)
        {
            Vector3 direction = Ascend(function, seed, out float value);
            if (best < value)
                (best, bestDirection) = (value, direction);
        }
        found = best > 0;
        return found ? bestDirection : Vector3.Zero;
    }

    /// <summary>
    /// Up to 25 iterations of up to 5 attempts; a failed attempt quarters the step for good. Only strictly higher values are accepted
    /// </summary>
    internal static Vector3 Ascend(in SHVector function, Vector3 seed, out float value)
    {
        Vector3 direction = seed;
        value = SHVector.BasisFunction(direction).Dot(function);
        float step = 1;
        for (int iteration = 0; iteration < 25; iteration++)
        {
            Vector3 gradient = TangentGradient(function, direction);
            int attempt = 0;
            for (; attempt < 5; attempt++, step *= 0.25f)
            {
                Vector3 delta = gradient * step;
                if (delta.LengthSquared() < 1e-8f)
                    return direction;
                Vector3 candidate = Normalize(direction + delta);
                float candidateValue = SHVector.BasisFunction(candidate).Dot(function);
                if (value < candidateValue)
                {
                    (direction, value) = (candidate, candidateValue);
                    break;
                }
            }
            if (attempt == 5)
                return direction;
        }
        return direction;
    }

    //FVector::SafeNormal with its 1e-8 threshold, keeping an exactly unit vector
    private static Vector3 Normalize(Vector3 v)
    {
        float lengthSquared = v.LengthSquared();
        if (lengthSquared == 1) return v;
        return lengthSquared >= 1e-8f ? v * (1 / MathF.Sqrt(lengthSquared)) : Vector3.Zero;
    }

    /// <summary>The polynomial gradient projected onto the sphere's tangent plane, (r² G - v (v·G)) / r³</summary>
    internal static Vector3 TangentGradient(in SHVector f, Vector3 v)
    {
        float x = v.X, y = v.Y, z = v.Z;
        var g = new Vector3(
            -0.48860299f * f[3] + 1.0925480f * y * f[4] - 1.0925480f * z * f[7] + 1.0925480f * x * f[8],
            -0.48860299f * f[1] + 1.0925480f * x * f[4] - 1.0925480f * z * f[5] - 1.0925480f * y * f[8],
            0.48860299f * f[2] - 1.0925480f * y * f[5] + 1.8923519f * z * f[6] - 1.0925480f * x * f[7]);
        float r2 = v.LengthSquared();
        float r3 = MathF.Sqrt(r2) * r2;
        return (g * r2 - v * Vector3.Dot(v, g)) / r3;
    }
}
