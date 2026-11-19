using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Lightmaps.Codec;

/// <summary>
/// BioWare's Simple texture lightmap (LMT_6): one DXT1 colour atlas (<c>LightMapSimple_Colour</c>, sRGB-sampled) and no
/// direction. The pixel shader lights with <c>C = max(1e-4, abs(tex * LightMapScale + LightMapBias))</c>, times the
/// diffuse colour; there is no normal-map response (LE3_Lightmaps.md §6.5).
/// </summary>
public static class SimpleLightmap
{
    /// <summary>Colour the shader produces from a decoded texel.</summary>
    public static Vector3 Evaluate(Vector3 colour) =>
        Vector3.Max(new Vector3(1e-4f), Vector3.Abs(colour));

    /// <summary>
    /// Per-allocation colour range: stored bytes cover [min, max] per channel, so Bias = min and Scale = max - min
    /// (the floats are LightMapScale RGB, 1, LightMapBias RGB, 1). Observed on LMT_6: allocations with a nonzero bias
    /// still reach byte 0 and 255, and the biases are exact (k/255)^2.2 steps, i.e. minima of gamma-quantized data.
    /// </summary>
    public readonly record struct ColourRange(Vector3 Scale, Vector3 Bias)
    {
        public static ColourRange Choose(IEnumerable<Vector3> colours)
        {
            var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
            foreach (var c in colours) { min = Vector3.Min(min, c); max = Vector3.Max(max, c); }
            if (min.X == float.MaxValue) return new ColourRange(Vector3.Zero, Vector3.Zero);
            min = Vector3.Max(min, Vector3.Zero);
            max = Vector3.Min(max, new Vector3(Quantization.MaxLightIntensity));
            return new ColourRange(Vector3.Max(max - min, Vector3.Zero), min);
        }

        public Vector3 Decode(byte r, byte g, byte b, ByteEncoding enc) => Quantization.Decode(r, g, b, Scale, enc) + Bias;

        public (byte R, byte G, byte B) Encode(Vector3 colour, ByteEncoding enc) => Quantization.Encode(colour - Bias, Scale, enc);

        /// <summary>The eight floats as serialized: scale RGB, 1, bias RGB, 1.</summary>
        public float[] ToFloats() => [Scale.X, Scale.Y, Scale.Z, 1f, Bias.X, Bias.Y, Bias.Z, 1f];
    }
}
