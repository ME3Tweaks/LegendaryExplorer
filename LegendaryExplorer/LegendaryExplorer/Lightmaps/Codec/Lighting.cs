using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Lightmaps.Codec;

/// <summary>
/// Incident light at one sample as linear (L1) spherical harmonics of RGB radiance in world space:
/// L(w) = A + Bx w.x + By w.y + Bz w.z, each term an RGB colour. This is the form UE5's lightmaps store,
/// and the input the LE3 encoders convert from.
/// </summary>
public readonly record struct Lighting(Vector3 A, Vector3 Bx, Vector3 By, Vector3 Bz)
{
    /// <summary>
    /// Irradiance for a surface facing <paramref name="n"/>: the clamped-cosine convolution of L1 radiance,
    /// pi A + (2 pi / 3) B.n. Exact for L1 input, which is what Lightmass's per-basis gather computes (UE3
    /// FGatheredLightSample::PointLight sums colour * max(0, dir . basis)).
    /// </summary>
    public Vector3 Irradiance(Vector3 n) =>
        Vector3.Max(Vector3.Zero, MathF.PI * A + 2f * MathF.PI / 3f * (Bx * n.X + By * n.Y + Bz * n.Z));

    /// <summary>Luminance-weighted dominant incident direction (unit), or zero for purely ambient light.</summary>
    public Vector3 DominantDirection()
    {
        var d = new Vector3(Luminance(Bx), Luminance(By), Luminance(Bz));
        return d.LengthSquared() < 1e-12f ? Vector3.Zero : Vector3.Normalize(d);
    }

    /// <summary>L1 projection of a distant light of the given colour arriving from direction <paramref name="toLight"/>.</summary>
    public static Lighting Directional(Vector3 colour, Vector3 toLight)
    {
        // Radiance of a delta light projected onto Y00 and Y1m and reconstructed: A = c/(4 pi), B = 3c/(4 pi) d.
        // With this, Irradiance(d) = c, matching an unshadowed light head-on.
        var d = Vector3.Normalize(toLight);
        return new Lighting(colour / (4f * MathF.PI), 3f * colour * d.X / (4f * MathF.PI), 3f * colour * d.Y / (4f * MathF.PI), 3f * colour * d.Z / (4f * MathF.PI));
    }

    /// <summary>Uniform ambient light whose irradiance is <paramref name="colour"/> for every normal.</summary>
    public static Lighting Ambient(Vector3 colour) => new(colour / MathF.PI, Vector3.Zero, Vector3.Zero, Vector3.Zero);

    public static Lighting operator +(Lighting a, Lighting b) => new(a.A + b.A, a.Bx + b.Bx, a.By + b.By, a.Bz + b.Bz);

    public static float Luminance(Vector3 c) => 0.3f * c.X + 0.59f * c.Y + 0.11f * c.Z;
}

/// <summary>A surface point's tangent frame in world space (LE3's, not a re-imported mesh's).</summary>
public readonly record struct TangentFrame(Vector3 T, Vector3 B, Vector3 N)
{
    public Vector3 ToWorld(Vector3 tangentSpace) => T * tangentSpace.X + B * tangentSpace.Y + N * tangentSpace.Z;
    public Vector3 ToTangent(Vector3 world) => new(Vector3.Dot(world, T), Vector3.Dot(world, B), Vector3.Dot(world, N));
}

/// <summary>Converts lighting into each LE3 lightmap type's per-sample parameters.</summary>
public static class LightingConversion
{
    /// <summary>Stock directional (LMT_1D/2D): irradiance at the three HL2 basis normals, as Lightmass gathers it.</summary>
    public static Directional.Sample ToDirectional(in Lighting light, in TangentFrame frame) => new(
        light.Irradiance(Vector3.Normalize(frame.ToWorld(Codec.Directional.Basis[0]))),
        light.Irradiance(Vector3.Normalize(frame.ToWorld(Codec.Directional.Basis[1]))),
        light.Irradiance(Vector3.Normalize(frame.ToWorld(Codec.Directional.Basis[2]))));

    /// <summary>What the Vector colour is fitted to.</summary>
    public enum VectorTarget
    {
        /// <summary>
        /// The stock directional shader's output for the same lighting (LMT_2D/1D rendering of ToDirectional). The seam
        /// test (LE3_Lightmaps.md §16) shows BioWare's Vector lightmaps match this, not true irradiance.
        /// </summary>
        DirectionalLook,
        /// <summary>True irradiance E(n). Rejected by the seam test; kept for comparison.</summary>
        Irradiance,
    }

    /// <summary>
    /// Vector (LMT_3/LMT_4): the dominant incident direction, kept in the surface's upper hemisphere, and the
    /// colour that best fits the shader's C * saturate(N . L) to <paramref name="target"/> over normals within
    /// <paramref name="normalSpreadDegrees"/> of the surface normal (the range a normal map covers), times
    /// <paramref name="brightness"/> (BioWare's Vector/directional brightness ratio; see §16, pending the controls).
    /// Returns the direction in world space; LMT_3 needs it converted to tangent space.
    /// </summary>
    public static VectorLightmap.Sample ToVector(in Lighting light, in TangentFrame frame, float normalSpreadDegrees = 45f,
        VectorTarget target = VectorTarget.DirectionalLook, float brightness = 1f)
    {
        var n0 = frame.N;
        var l = light.DominantDirection();
        // BioWare's directions never point below the surface (tangent z >= 0); fold ambient-only or
        // back-facing cases onto the normal.
        if (l == Vector3.Zero || Vector3.Dot(l, n0) < 0.05f)
        {
            var tangential = l - Vector3.Dot(l, n0) * n0;
            l = tangential.LengthSquared() < 1e-8f ? n0 : Vector3.Normalize(Vector3.Normalize(tangential) * 0.9987f + n0 * 0.05f);
        }

        // Least squares for C over a fixed set of normals: C = sum(w Y(n)) / sum(w^2), w = saturate(n . L),
        // Y = the target's lighting for normal n.
        var directional = target == VectorTarget.DirectionalLook ? ToDirectional(light, frame) : default;
        var numerator = Vector3.Zero; float denominator = 0;
        foreach (var t in SpreadDirections(normalSpreadDegrees))
        {
            var n = Vector3.Normalize(frame.ToWorld(t));
            float w = Math.Clamp(Vector3.Dot(n, l), 0f, 1f);
            var y = target == VectorTarget.DirectionalLook ? Codec.Directional.Evaluate(directional, t) : light.Irradiance(n);
            numerator += w * y;
            denominator += w * w;
        }
        var colour = denominator > 1e-6f ? numerator / denominator * brightness : Vector3.Zero;
        return new VectorLightmap.Sample(colour, l);
    }

    /// <summary>Simple (LMT_6): irradiance at the surface normal.</summary>
    public static Vector3 ToSimple(in Lighting light, in TangentFrame frame) => light.Irradiance(frame.N);

    static readonly Dictionary<float, Vector3[]> SpreadCache = [];

    /// <summary>Tangent-space normals: the geometric normal plus rings out to the given angle.</summary>
    public static Vector3[] SpreadDirections(float degrees)
    {
        lock (SpreadCache)
        {
            if (SpreadCache.TryGetValue(degrees, out var cached)) return cached;
            var list = new List<Vector3> { Vector3.UnitZ };
            const int rings = 4, perRing = 12;
            for (int r = 1; r <= rings; r++)
            {
                float theta = degrees * MathF.PI / 180f * r / rings;
                for (int k = 0; k < perRing; k++)
                {
                    float phi = 2f * MathF.PI * (k + 0.5f * (r & 1)) / perRing;
                    list.Add(new Vector3(MathF.Sin(theta) * MathF.Cos(phi), MathF.Sin(theta) * MathF.Sin(phi), MathF.Cos(theta)));
                }
            }
            return SpreadCache[degrees] = list.ToArray();
        }
    }
}
