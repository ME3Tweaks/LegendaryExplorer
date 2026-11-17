using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Lightmaps.Codec;

/// <summary>How a stored byte maps back to a [0,1] fraction of the coefficient's scale.</summary>
public enum ByteEncoding
{
    /// <summary>fraction = byte / 255. LMT_4's direction atlas (LE3_Lightmaps.md §6.3).</summary>
    Linear,
    /// <summary>fraction = (byte / 255)^2.2. Per-vertex lightmap streams, decoded by pow() in the vertex shader.</summary>
    Gamma22,
    /// <summary>
    /// The IEC 61966-2-1 sRGB curve, which D3D11 applies in the sampler for *_UNORM_SRGB formats (before filtering).
    /// Lightmap colour atlases keep UE3's default SRGB = TRUE (seam test, LE3_Lightmaps.md §16).
    /// </summary>
    Srgb,
}

/// <summary>Per-coefficient scale selection and byte quantization, following UE3's FLightMap1D/FLightMap2D.</summary>
public static class Quantization
{
    /// <summary>UE3 MAX_LIGHT_INTENSITY: scales are clamped to this.</summary>
    public const float MaxLightIntensity = 16f;
    const float Delta = 1e-5f;

    static readonly float[] SrgbToLinear = Enumerable.Range(0, 256).Select(b => SrgbDecode(b / 255f)).ToArray();

    public static float ToFraction(byte b, ByteEncoding enc)
    {
        float x = b / 255f;
        return enc switch
        {
            // The shaders compute pow(max(1e-4, x), 2.2); byte 0 is taken as exactly 0 here (the shader's
            // 1.6e-9 is invisible, but it would turn black samples white when renormalised).
            ByteEncoding.Gamma22 => b != 0 ? MathF.Pow(x, 2.2f) : 0f,
            ByteEncoding.Srgb => SrgbToLinear[b],
            _ => x,
        };
    }

    static float SrgbDecode(float x) => x <= 0.04045f ? x / 12.92f : MathF.Pow((x + 0.055f) / 1.055f, 2.4f);

    static float SrgbEncode(float x) => x <= 0.0031308f ? x * 12.92f : 1.055f * MathF.Pow(x, 1f / 2.4f) - 0.055f;

    /// <summary>
    /// Round-to-nearest. UE3 truncated (appTrunc), which biases every sample down by half a step;
    /// nearest is strictly more accurate and decodes identically.
    /// </summary>
    public static byte FromFraction(float fraction, ByteEncoding enc)
    {
        float x = Math.Clamp(fraction, 0f, 1f);
        if (enc == ByteEncoding.Gamma22) x = MathF.Pow(x, 1f / 2.2f);
        else if (enc == ByteEncoding.Srgb) x = SrgbEncode(x);
        return (byte)Math.Clamp((int)MathF.Round(x * 255f), 0, 255);
    }

    public static Vector3 Decode(byte r, byte g, byte b, Vector3 scale, ByteEncoding enc) =>
        new Vector3(ToFraction(r, enc), ToFraction(g, enc), ToFraction(b, enc)) * scale;

    public static (byte R, byte G, byte B) Encode(Vector3 value, Vector3 scale, ByteEncoding enc)
    {
        var inv = new Vector3(1f / MathF.Max(scale.X, Delta), 1f / MathF.Max(scale.Y, Delta), 1f / MathF.Max(scale.Z, Delta));
        var f = value * inv;
        return (FromFraction(f.X, enc), FromFraction(f.Y, enc), FromFraction(f.Z, enc));
    }

    /// <summary>
    /// UE3 picks the per-channel maximum over the allocation, clamped to MAX_LIGHT_INTENSITY. ME3's
    /// directional lightmaps additionally never scale below <paramref name="floor"/> (1.0): observed
    /// ScaleVectors are (1,1,1) unless a channel's data exceeds 1.
    /// </summary>
    public static Vector3 ChooseScale(IEnumerable<Vector3> values, float floor = 0f)
    {
        var max = new Vector3(floor);
        foreach (var v in values) max = Vector3.Max(max, v);
        return Vector3.Min(max, new Vector3(MaxLightIntensity));
    }
}
