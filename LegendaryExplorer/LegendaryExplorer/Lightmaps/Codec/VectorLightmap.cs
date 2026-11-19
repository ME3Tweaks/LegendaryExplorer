using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Lightmaps.Codec;

/// <summary>
/// BioWare's Vector lightmap (LMT_3 per vertex, LMT_4 texture): one colour and one dominant light
/// direction per sample. The pixel shader lights with C * saturate(N . L) (plus a Phong highlight on L).
///
/// LMT_4 stores L in world space and does not normalise it (its length acts as directionality after the
/// half-resolution direction texture is filtered); LMT_3 stores L in tangent space and the pixel shader
/// normalises it. Both quantize direction components first to the byte grid x = b / 127.5 - 1.
/// </summary>
public static class VectorLightmap
{
    public struct Sample
    {
        public Vector3 Colour;
        public Vector3 Direction;

        public Sample(Vector3 colour, Vector3 direction) { Colour = colour; Direction = direction; }
    }

    /// <summary>Diffuse lighting the LE3 shader produces for a normal in the same space as the direction.</summary>
    public static Vector3 Evaluate(in Sample s, Vector3 normal, bool normaliseDirection)
    {
        var l = normaliseDirection && s.Direction != Vector3.Zero ? Vector3.Normalize(s.Direction) : s.Direction;
        return s.Colour * Math.Clamp(Vector3.Dot(normal, l), 0f, 1f);
    }

    // ---- direction component grid shared by LMT_3 and LMT_4 ----

    public static byte ToDirectionByte(float x) => (byte)Math.Clamp((int)MathF.Round((x + 1f) * 127.5f), 0, 255);
    public static float FromDirectionByte(int b) => b / 127.5f - 1f;

    // ---- LMT_3: per-vertex bytes ----

    /// <summary>
    /// LMT_3 vertex stream bytes (memory order b0..b3; LEX prints b0..b2 as R,G,B). Colour rgb = (b0,b1,b2),
    /// gamma 2.2 times a per-channel scale. Direction xyz = (b2,b1,b0) on the fixed grid. Alphas are 255.
    /// </summary>
    public static Sample DecodeVertex(byte c0, byte c1, byte c2, byte d0, byte d1, byte d2, Vector3 colourScale) =>
        new(Quantization.Decode(c0, c1, c2, colourScale, ByteEncoding.Gamma22),
            new Vector3(FromDirectionByte(d2), FromDirectionByte(d1), FromDirectionByte(d0)));

    public static ((byte, byte, byte) Colour, (byte, byte, byte) Direction) EncodeVertex(in Sample s, Vector3 colourScale)
    {
        var colour = Quantization.Encode(s.Colour, colourScale, ByteEncoding.Gamma22);
        var d = s.Direction;
        return (colour, (ToDirectionByte(d.Z), ToDirectionByte(d.Y), ToDirectionByte(d.X)));
    }

    /// <summary>LMT_3's colour scale: the per-channel maximum over the lightmap (no floor, unlike the directional types).</summary>
    public static Vector3 ChooseVertexColourScale(IEnumerable<Sample> samples) =>
        Quantization.ChooseScale(samples.Select(s => s.Colour));

    // ---- LMT_4: per-allocation direction range ----

    /// <summary>
    /// LMT_4's direction texture stores each channel stretched over the allocation's range of grid bytes:
    /// with q the grid byte of a component and [qMin, qMax] its range over the allocation,
    /// stored = (q - qMin) / (qMax - qMin) * 255, and the shader decodes stored / 255 * Scale + Bias with
    /// Scale = 2 (qMax - qMin) / 255 and Bias = (2 qMin - 255) / 255. Observed LE3 scales/biases are exact
    /// multiples of 1/255 of this form.
    /// </summary>
    public readonly record struct DirectionRange(Vector3 Scale, Vector3 Bias)
    {
        public static DirectionRange Choose(IEnumerable<Vector3> directions)
        {
            var qMin = new Vector3(255); var qMax = Vector3.Zero;
            foreach (var d in directions)
            {
                var q = new Vector3(ToDirectionByte(d.X), ToDirectionByte(d.Y), ToDirectionByte(d.Z));
                qMin = Vector3.Min(qMin, q); qMax = Vector3.Max(qMax, q);
            }
            if (qMin.X > qMax.X) qMin = qMax = new Vector3(127); // empty allocation
            return new DirectionRange(2f * (qMax - qMin) / 255f, (2f * qMin - new Vector3(255)) / 255f);
        }

        public Vector3 Decode(byte r, byte g, byte b) => new Vector3(r, g, b) / 255f * Scale + Bias;

        public (byte R, byte G, byte B) Encode(Vector3 d)
        {
            var f = (d - Bias) / Vector3.Max(Scale, new Vector3(1e-6f));
            return (Unit(f.X), Unit(f.Y), Unit(f.Z));
        }

        static byte Unit(float f) => (byte)Math.Clamp((int)MathF.Round(f * 255f), 0, 255);
    }

    /// <summary>LMT_4's colour scale: per-channel maximum, bias 0, no floor.</summary>
    public static Vector3 ChooseTextureColourScale(IEnumerable<Vector3> colours) => Quantization.ChooseScale(colours);
}
