using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Numerics;

namespace LegendaryExplorer.Lightmaps.Codec;

/// <summary>
/// Managed BC1/DXT1 codec. Kept in-process and deterministic so round-trip tests don't depend on a native
/// compressor's version. Pixels are RGB bytes, 3 per texel, row-major. Lightmap shaders read only RGB, so
/// 3-colour mode's black/transparent index is usable as plain black.
/// </summary>
public static class Dxt1
{
    public const int BlockBytes = 8;

    public static int EncodedSize(int width, int height) => Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * BlockBytes;

    public static byte[] Decode(ReadOnlySpan<byte> blocks, int width, int height)
    {
        var rgb = new byte[width * height * 3];
        int bw = Math.Max(1, (width + 3) / 4), bh = Math.Max(1, (height + 3) / 4);
        Span<Vector3> palette = stackalloc Vector3[4];
        for (int by = 0; by < bh; by++)
        for (int bx = 0; bx < bw; bx++)
        {
            var block = blocks.Slice((by * bw + bx) * BlockBytes, BlockBytes);
            ushort c0 = (ushort)(block[0] | block[1] << 8), c1 = (ushort)(block[2] | block[3] << 8);
            uint indices = (uint)(block[4] | block[5] << 8 | block[6] << 16 | block[7] << 24);
            BuildPalette(c0, c1, palette);
            for (int i = 0; i < 16; i++)
            {
                int x = bx * 4 + (i & 3), y = by * 4 + (i >> 2);
                if (x >= width || y >= height) continue;
                var c = palette[(int)(indices >> (2 * i)) & 3];
                int o = (y * width + x) * 3;
                rgb[o] = (byte)MathF.Round(c.X); rgb[o + 1] = (byte)MathF.Round(c.Y); rgb[o + 2] = (byte)MathF.Round(c.Z);
            }
        }
        return rgb;
    }

    public static byte[] Encode(ReadOnlySpan<byte> rgb, int width, int height)
    {
        int bw = Math.Max(1, (width + 3) / 4), bh = Math.Max(1, (height + 3) / 4);
        var output = new byte[bw * bh * BlockBytes];
        var input = rgb.ToArray();
        Parallel.For(0, bh, by =>
        {
            Span<Vector3> px = stackalloc Vector3[16];
            for (int bx = 0; bx < bw; bx++)
            {
                for (int i = 0; i < 16; i++)
                {
                    // Edge blocks of small mips repeat the last row/column.
                    int x = Math.Min(bx * 4 + (i & 3), width - 1), y = Math.Min(by * 4 + (i >> 2), height - 1);
                    int o = (y * width + x) * 3;
                    px[i] = new Vector3(input[o], input[o + 1], input[o + 2]);
                }
                EncodeBlock(px, output.AsSpan((by * bw + bx) * BlockBytes, BlockBytes));
            }
        });
        return output;
    }

    static void BuildPalette(ushort c0, ushort c1, Span<Vector3> palette)
    {
        palette[0] = Expand565(c0);
        palette[1] = Expand565(c1);
        // Interpolants are rounded to bytes here, not only when decoding, so the encoder scores candidates
        // against exactly what the decoder will produce.
        if (c0 > c1)
        {
            palette[2] = Round((2 * palette[0] + palette[1]) / 3);
            palette[3] = Round((palette[0] + 2 * palette[1]) / 3);
        }
        else
        {
            palette[2] = Round((palette[0] + palette[1]) / 2);
            palette[3] = Vector3.Zero;
        }
    }

    static Vector3 Round(Vector3 v) => new(MathF.Round(v.X), MathF.Round(v.Y), MathF.Round(v.Z));

    static Vector3 Expand565(ushort c)
    {
        int r = c >> 11 & 31, g = c >> 5 & 63, b = c & 31;
        return new Vector3(r << 3 | r >> 2, g << 2 | g >> 4, b << 3 | b >> 2);
    }

    static ushort Pack565(Vector3 c)
    {
        int r = Math.Clamp((int)MathF.Round(c.X * 31 / 255f), 0, 31);
        int g = Math.Clamp((int)MathF.Round(c.Y * 63 / 255f), 0, 63);
        int b = Math.Clamp((int)MathF.Round(c.Z * 31 / 255f), 0, 31);
        return (ushort)(r << 11 | g << 5 | b);
    }

    internal static string ExplainBlock(ReadOnlySpan<byte> rgb16, ushort refC0, ushort refC1)
    {
        Span<Vector3> px = stackalloc Vector3[16];
        for (int i = 0; i < 16; i++) px[i] = new Vector3(rgb16[i * 3], rgb16[i * 3 + 1], rgb16[i * 3 + 2]);
        Span<byte> dst = stackalloc byte[8];
        EncodeBlock(px, dst);
        ushort c0 = (ushort)(dst[0] | dst[1] << 8), c1 = (ushort)(dst[2] | dst[3] << 8);
        static string F(ushort c) => $"({c >> 11 & 31},{c >> 5 & 63},{c & 31})";
        var sb = new System.Text.StringBuilder();
        sb.Append($"ref {F(refC0)} {F(refC1)} err {Evaluate(px, refC0, refC1)} | ours {F(c0)} {F(c1)} err {Evaluate(px, c0, c1)} | px:");
        for (int i = 0; i < 16; i++) sb.Append($" {px[i].X},{px[i].Y},{px[i].Z}");
        return sb.ToString();
    }

    const int RefineCount = 4;
    const int MaxPositionalColours = 6;

    /// <summary>Weight of endpoint 0 at each opaque palette position, per mode.</summary>
    static readonly float[][] PalettePositions = [[1f, 0f, 2f / 3f, 1f / 3f], [1f, 0f, 0.5f]];

    sealed record ChannelTables((int, int)[] R, (int, int)[] G, (int, int)[] B);

    /// <summary>
    /// Per channel and 8-bit value, the endpoint pair whose 2/3 point (4-colour) or midpoint (3-colour)
    /// decodes to that value most closely, preferring close endpoints.
    /// </summary>
    static readonly ChannelTables[] SingleColourTables =
    [
        new(BuildTable(5, 2f / 3f), BuildTable(6, 2f / 3f), BuildTable(5, 2f / 3f)),
        new(BuildTable(5, 0.5f), BuildTable(6, 0.5f), BuildTable(5, 0.5f)),
    ];

    static (int, int)[] BuildTable(int bits, float weight0)
    {
        int levels = 1 << bits;
        int Expand(int e) => bits == 5 ? e << 3 | e >> 2 : e << 2 | e >> 4;
        var table = new (int, int)[256];
        for (int v = 0; v < 256; v++)
        {
            int bestErr = int.MaxValue, bestSpread = int.MaxValue;
            for (int e0 = 0; e0 < levels; e0++)
            for (int e1 = 0; e1 < levels; e1++)
            {
                int decoded = (int)MathF.Round(weight0 * Expand(e0) + (1 - weight0) * Expand(e1));
                int err = Math.Abs(decoded - v), spread = Math.Abs(e0 - e1);
                if (err < bestErr || err == bestErr && spread < bestSpread) { bestErr = err; bestSpread = spread; table[v] = (e0, e1); }
            }
        }
        return table;
    }

    struct Candidate
    {
        public ushort C0, C1;
        public float Error;
    }

    /// <summary>
    /// Encodes one block, trying both BC1 modes: 4-colour (c0 &gt; c1: endpoints plus the 1/3 and 2/3
    /// points) and 3-colour (c0 &lt;= c1: endpoints, midpoint and black). Endpoint seeds come from a
    /// principal-axis range fit and from every pair of the block's own colours; the best seeds are refined
    /// by least squares and then by +-1 steps on each 565 component. nvtt, which compressed LE3's
    /// lightmaps, chose 3-colour mode for 27-65% of their blocks, so both modes are needed to match it.
    /// </summary>
    static void EncodeBlock(ReadOnlySpan<Vector3> px, Span<byte> dst)
    {
        Span<Candidate> best = stackalloc Candidate[RefineCount];
        for (int i = 0; i < best.Length; i++) best[i].Error = float.MaxValue;

        RangeFit(px, out var hi, out var lo);
        Offer(px, Pack565(hi), Pack565(lo), best);

        Span<ushort> packed = stackalloc ushort[16];
        int distinct = 0;
        for (int i = 0; i < 16; i++)
        {
            ushort c = Pack565(px[i]);
            if (packed[..distinct].IndexOf(c) < 0) packed[distinct++] = c;
        }
        for (int a = 0; a < distinct; a++)
        for (int b = a; b < distinct; b++)
            Offer(px, packed[a], packed[b], best);

        // Endpoint pairs that reproduce the block's mean colour exactly as an interpolant. Covers flat
        // blocks, which 565 endpoints alone usually can't hit.
        var mean = Vector3.Zero;
        foreach (var p in px) mean += p;
        mean = Round(mean / 16);
        foreach (var table in SingleColourTables)
        {
            var (r0, r1) = table.R[(int)mean.X];
            var (g0, g1) = table.G[(int)mean.Y];
            var (b0, b1) = table.B[(int)mean.Z];
            Offer(px, (ushort)(r0 << 11 | g0 << 5 | b0), (ushort)(r1 << 11 | g1 << 5 | b1), best);
        }

        // Blocks with few colours (typically already DXT-compressed data): assume each pair of colours sits
        // at particular palette positions and solve for the endpoints that put them there.
        Span<Vector3> colours = stackalloc Vector3[16];
        int colourCount = 0;
        for (int i = 0; i < 16 && colourCount <= MaxPositionalColours; i++)
            if (colours[..colourCount].IndexOf(px[i]) < 0) colours[colourCount++] = px[i];
        if (colourCount <= MaxPositionalColours)
        {
            for (int a = 0; a < colourCount; a++)
            for (int b = a + 1; b < colourCount; b++)
            foreach (var positions in PalettePositions)
            for (int pa = 0; pa < positions.Length; pa++)
            for (int pb = 0; pb < positions.Length; pb++)
            {
                if (pa == pb) continue;
                // A = c1 + tA (c0 - c1), B = c1 + tB (c0 - c1).
                float tA = positions[pa], tB = positions[pb];
                var d = (colours[a] - colours[b]) / (tA - tB);
                var c1 = colours[a] - tA * d;
                Offer(px, Pack565(Vector3.Clamp(c1 + d, Vector3.Zero, new Vector3(255))), Pack565(Vector3.Clamp(c1, Vector3.Zero, new Vector3(255))), best);
            }
        }

        var winner = best[0];
        foreach (var seed in best)
        {
            if (winner.Error == 0) break;
            if (seed.Error == float.MaxValue) continue;
            var refined = Refine(px, seed);
            if (refined.Error < winner.Error) winner = refined;
        }

        Span<byte> idx = stackalloc byte[16];
        Span<Vector3> palette = stackalloc Vector3[4];
        BuildPalette(winner.C0, winner.C1, palette);
        AssignIndices(px, palette, idx);

        dst[0] = (byte)winner.C0; dst[1] = (byte)(winner.C0 >> 8);
        dst[2] = (byte)winner.C1; dst[3] = (byte)(winner.C1 >> 8);
        uint bits = 0;
        for (int i = 0; i < 16; i++) bits |= (uint)idx[i] << (2 * i);
        dst[4] = (byte)bits; dst[5] = (byte)(bits >> 8); dst[6] = (byte)(bits >> 16); dst[7] = (byte)(bits >> 24);
    }

    /// <summary>Evaluates an endpoint pair in both modes and keeps it if it ranks among the best seeds.</summary>
    static void Offer(ReadOnlySpan<Vector3> px, ushort a, ushort b, Span<Candidate> best)
    {
        ushort hi = Math.Max(a, b), lo = Math.Min(a, b);
        if (hi != lo) Insert(best, new Candidate { C0 = hi, C1 = lo, Error = Evaluate(px, hi, lo) });
        Insert(best, new Candidate { C0 = lo, C1 = hi, Error = Evaluate(px, lo, hi) });
    }

    static void Insert(Span<Candidate> best, Candidate c)
    {
        for (int i = 0; i < best.Length; i++)
        {
            if (best[i].C0 == c.C0 && best[i].C1 == c.C1) return;
            if (c.Error < best[i].Error)
            {
                for (int j = best.Length - 1; j > i; j--) best[j] = best[j - 1];
                best[i] = c;
                return;
            }
        }
    }

    static float Evaluate(ReadOnlySpan<Vector3> px, ushort c0, ushort c1)
    {
        Span<Vector3> palette = stackalloc Vector3[4];
        Span<byte> idx = stackalloc byte[16];
        BuildPalette(c0, c1, palette);
        return AssignIndices(px, palette, idx);
    }

    static Candidate Refine(ReadOnlySpan<Vector3> px, Candidate c)
    {
        Span<Vector3> palette = stackalloc Vector3[4];
        Span<byte> idx = stackalloc byte[16];
        bool fourColour = c.C0 > c.C1;

        // Least squares on the endpoints for the current index assignment, keeping the mode.
        for (int iter = 0; iter < 2; iter++)
        {
            BuildPalette(c.C0, c.C1, palette);
            AssignIndices(px, palette, idx);
            if (!SolveEndpoints(px, idx, fourColour, out var e0, out var e1)) break;
            ushort p0 = Pack565(e0), p1 = Pack565(e1);
            if (fourColour ? p0 <= p1 : p0 > p1) break;
            float err = Evaluate(px, p0, p1);
            if (err >= c.Error) break;
            c = new Candidate { C0 = p0, C1 = p1, Error = err };
        }

        // +-1 steps on each of the six 565 components, keeping the mode.
        bool improved = true;
        while (improved && c.Error > 0)
        {
            improved = false;
            for (int comp = 0; comp < 6; comp++)
            for (int step = -1; step <= 1; step += 2)
            {
                ushort n0 = c.C0, n1 = c.C1;
                if (comp < 3) n0 = Nudge(n0, comp, step); else n1 = Nudge(n1, comp - 3, step);
                if (fourColour ? n0 <= n1 : n0 > n1) continue;
                float err = Evaluate(px, n0, n1);
                if (err < c.Error) { c = new Candidate { C0 = n0, C1 = n1, Error = err }; improved = true; }
            }
        }
        return c;
    }

    static ushort Nudge(ushort c, int component, int step)
    {
        int r = c >> 11 & 31, g = c >> 5 & 63, b = c & 31;
        switch (component)
        {
            case 0: r = Math.Clamp(r + step, 0, 31); break;
            case 1: g = Math.Clamp(g + step, 0, 63); break;
            default: b = Math.Clamp(b + step, 0, 31); break;
        }
        return (ushort)(r << 11 | g << 5 | b);
    }

    static void RangeFit(ReadOnlySpan<Vector3> px, out Vector3 hi, out Vector3 lo)
    {
        var mean = Vector3.Zero;
        foreach (var p in px) mean += p;
        mean /= 16;

        // Principal axis by power iteration on the covariance.
        float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
        foreach (var p in px)
        {
            var d = p - mean;
            xx += d.X * d.X; xy += d.X * d.Y; xz += d.X * d.Z; yy += d.Y * d.Y; yz += d.Y * d.Z; zz += d.Z * d.Z;
        }
        var axis = new Vector3(1, 1, 1);
        for (int i = 0; i < 8; i++)
        {
            axis = new Vector3(xx * axis.X + xy * axis.Y + xz * axis.Z, xy * axis.X + yy * axis.Y + yz * axis.Z, xz * axis.X + yz * axis.Y + zz * axis.Z);
            float len = axis.Length();
            if (len < 1e-6f) { hi = lo = mean; return; }
            axis /= len;
        }
        float tMin = float.MaxValue, tMax = float.MinValue;
        foreach (var p in px)
        {
            float t = Vector3.Dot(p - mean, axis);
            tMin = Math.Min(tMin, t); tMax = Math.Max(tMax, t);
        }
        hi = mean + axis * tMax; lo = mean + axis * tMin;
    }

    static float AssignIndices(ReadOnlySpan<Vector3> px, ReadOnlySpan<Vector3> palette, Span<byte> idx)
    {
        float total = 0;
        for (int i = 0; i < 16; i++)
        {
            int best = 0; float bestD = float.MaxValue;
            for (int k = 0; k < 4; k++)
            {
                float d = Vector3.DistanceSquared(px[i], palette[k]);
                if (d < bestD) { bestD = d; best = k; }
            }
            idx[i] = (byte)best; total += bestD;
        }
        return total;
    }

    /// <summary>Solves for the endpoints minimising squared error given an index assignment.</summary>
    static bool SolveEndpoints(ReadOnlySpan<Vector3> px, ReadOnlySpan<byte> idx, bool fourColour, out Vector3 c0, out Vector3 c1)
    {
        // Weight of endpoint 0 per index. 3-colour index 3 is fixed black and takes no part.
        ReadOnlySpan<float> w4 = [1f, 0f, 2f / 3f, 1f / 3f];
        ReadOnlySpan<float> w3 = [1f, 0f, 0.5f, float.NaN];
        var w0 = fourColour ? w4 : w3;
        float aa = 0, ab = 0, bb = 0;
        Vector3 ax = Vector3.Zero, bx = Vector3.Zero;
        for (int i = 0; i < 16; i++)
        {
            float a = w0[idx[i]];
            if (float.IsNaN(a)) continue;
            float b = 1 - a;
            aa += a * a; ab += a * b; bb += b * b;
            ax += a * px[i]; bx += b * px[i];
        }
        float det = aa * bb - ab * ab;
        if (MathF.Abs(det) < 1e-6f) { c0 = c1 = default; return false; }
        c0 = Vector3.Clamp((ax * bb - bx * ab) / det, Vector3.Zero, new Vector3(255));
        c1 = Vector3.Clamp((bx * aa - ax * ab) / det, Vector3.Zero, new Vector3(255));
        return true;
    }
}

public static class Dxt1Debug
{
    /// <summary>For diagnostics: the encoder's choice for a block versus a given reference encoding.</summary>
    public static string Explain(ReadOnlySpan<byte> rgbBlock16, ushort refC0, ushort refC1) => Dxt1.ExplainBlock(rgbBlock16, refC0, refC1);
}
