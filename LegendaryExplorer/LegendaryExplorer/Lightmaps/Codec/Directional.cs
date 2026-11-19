using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Lightmaps.Codec;

/// <summary>
/// The stock UE3 directional lightmap used by LMT_1D (per vertex) and LMT_2D (texture).
///
/// Lighting is gathered as three RGB irradiances E_i, one per HL2 basis direction B_i in tangent space
/// (Lightmass: E_i = sum over incident light of colour * max(0, dir . B_i)). It is stored as two
/// coefficients (UE3 FGatheredLightSample::ConvertToLightSample):
///   T0 = NormalizedAverageColor = mean(E_i) / max_channel(mean(E_i))
///   T1 = DirectionalMaxComponent, channel i = max_channel(E_i)
/// and the pixel shader reconstructs E_i as T0 * T1_i.
/// </summary>
public static class Directional
{
    public static readonly Vector3[] Basis =
    [
        new(0f, MathF.Sqrt(6f) / 3f, 1f / MathF.Sqrt(3f)),
        new(-1f / MathF.Sqrt(2f), -1f / MathF.Sqrt(6f), 1f / MathF.Sqrt(3f)),
        new(1f / MathF.Sqrt(2f), -1f / MathF.Sqrt(6f), 1f / MathF.Sqrt(3f)),
    ];

    /// <summary>Per-sample lighting: RGB irradiance along each of the three basis directions.</summary>
    public struct Sample
    {
        public Vector3 E0, E1, E2;

        public Vector3 this[int i] => i switch { 0 => E0, 1 => E1, _ => E2 };

        public Sample(Vector3 e0, Vector3 e1, Vector3 e2) { E0 = e0; E1 = e1; E2 = e2; }
    }

    public static (Vector3 Colour, Vector3 MaxComponents) Split(in Sample s)
    {
        var average = (s.E0 + s.E1 + s.E2) / 3f;
        float maxAverage = MaxChannel(average);
        var colour = maxAverage > 0f ? average / maxAverage : Vector3.Zero;
        var maxComponents = new Vector3(MaxChannel(s.E0), MaxChannel(s.E1), MaxChannel(s.E2));
        return (colour, maxComponents);
    }

    public static Sample Combine(Vector3 colour, Vector3 maxComponents) =>
        new(colour * maxComponents.X, colour * maxComponents.Y, colour * maxComponents.Z);

    /// <summary>Diffuse irradiance the LE3 shader produces for a tangent-space normal.</summary>
    public static Vector3 Evaluate(in Sample s, Vector3 tangentNormal)
    {
        var result = Vector3.Zero;
        for (int i = 0; i < 3; i++)
        {
            float w = MathF.Max(1e-4f, Square(Math.Clamp(Vector3.Dot(tangentNormal, Basis[i]), 0f, 1f)));
            result += s[i] * w;
        }
        return result;
    }

    static float MaxChannel(Vector3 v) => MathF.Max(v.X, MathF.Max(v.Y, v.Z));
    static float Square(float x) => x * x;
}
