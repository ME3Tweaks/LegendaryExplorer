using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using LegendaryExplorer.Lightmaps.Codec;

namespace LegendaryExplorer.Lightmaps.Lightmass;

/// <summary>
/// One mapping's lighting as Lightmass computed it, decoded from its quantized samples. Texture mappings are SizeX × SizeY texels
/// (padding ring included); vertex mappings are one sample per vertex (SizeY 1).
/// </summary>
public sealed class LightmassMappingResult
{
    public Guid Guid;
    public bool IsTextureMapping;
    public int SizeX, SizeY;
    public double ExecutionTime;
    /// <summary>
    /// Incident lighting along the three HL2 basis directions in tangent space (Lightmass's gathered directional coefficients:
    /// direct light scaled by DirectionalCoefficientScale·cos^DirectionalCoefficientFalloffPower, as ME3 was baked).
    /// </summary>
    public Directional.Sample[] Directional;
    /// <summary>Incident lighting along the surface normal (the simple coefficient), unscaled.</summary>
    public Vector3[] Simple;
    /// <summary>255 where the texel maps to a surface, 0 elsewhere. All 255 for vertex mappings.</summary>
    public byte[] Coverage;
    /// <summary>The lights whose direct lighting this lightmap holds (their LightmapGuids).</summary>
    public Guid[] LightGuids;
    /// <summary>A shadow map per light that Lightmass shadowed here rather than lighting into the lightmap.</summary>
    public List<LightmassShadowMap> ShadowMaps = [];
}

/// <summary>
/// One light's precomputed shadowing of a mapping. Texture mappings: SizeX × SizeY bytes, either visibility (gamma 2.2 encoded, as
/// ShadowMapTexture2D stores it) or, for <see cref="SignedDistanceField"/>, the distance to the nearest shadow edge (linear, 0.5 at the
/// edge). Vertex mappings: a linear visibility per vertex.
/// </summary>
public sealed class LightmassShadowMap
{
    /// <summary>The Lightmass light's GUID.</summary>
    public Guid LightGuid;
    public bool SignedDistanceField;
    public byte[] Values;
    /// <summary>255 where Lightmass kept the texel. It drops fully shadowed texels too, so it isn't the surface's coverage.</summary>
    public byte[] Coverage;
    public float[] Samples;
}

/// <summary>
/// Reads Lightmass's result channels (FLightmassSolverExporter::ExportResults, Exporter.cpp; read in UE3 by
/// FLightmassProcessor::ImportTextureMapping/ImportVertexMapping, Lightmass.cpp:5085). A channel can hold several mappings.
/// </summary>
public static class LightmassResults
{
    /// <summary>Number of stored coefficients per sample: normalized average colour, per-basis max, simple.</summary>
    const int Coefficients = 3;

    public static List<LightmassMappingResult> Read(string path, bool textureMapping)
    {
        var results = new List<LightmassMappingResult>();
        using var reader = new BinaryReader(File.OpenRead(path));
        uint count = reader.ReadUInt32();
        for (uint i = 0; i < count; i++)
            results.Add(textureMapping ? ReadTexture(reader) : ReadVertex(reader));
        return results;
    }

    static LightmassMappingResult ReadTexture(BinaryReader r)
    {
        var result = new LightmassMappingResult { Guid = new Guid(r.ReadBytes(16)), IsTextureMapping = true, ExecutionTime = r.ReadDouble() };
        // FLightMapData2DData (52)
        uint compressed = r.ReadUInt32(), uncompressed = r.ReadUInt32();
        var scale = ReadScale(r);
        result.SizeX = (int)r.ReadUInt32();
        result.SizeY = (int)r.ReadUInt32();
        int shadowMaps = r.ReadInt32(), distanceFieldShadowMaps = r.ReadInt32();
        result.LightGuids = ReadGuids(r, r.ReadInt32());
        Decode(result, Inflate(r, compressed, uncompressed), scale, result.SizeX * result.SizeY);
        r.ReadByte(); // PreviewEnvironmentShadowing
        // Shadow maps (light GUID, FShadowMapData2DData: compressed, uncompressed, SizeX, SizeY; FQuantizedShadowSampleData: visibility,
        // coverage), then signed distance field ones (same header; FQuantizedSignedDistanceFieldShadowSampleData: distance, penumbra, coverage)
        for (int s = 0; s < shadowMaps + distanceFieldShadowMaps; s++)
        {
            bool sdf = s >= shadowMaps;
            var shadow = new LightmassShadowMap { LightGuid = new Guid(r.ReadBytes(16)), SignedDistanceField = sdf };
            uint c = r.ReadUInt32(), u = r.ReadUInt32();
            int sx = (int)r.ReadUInt32(), sy = (int)r.ReadUInt32();
            var data = Inflate(r, c, u);
            int stride = sdf ? 3 : 2, n = sx * sy;
            if (data.Length < n * stride) throw new InvalidDataException($"shadow map of {result.Guid}: {data.Length} bytes for {sx}x{sy}");
            shadow.Values = new byte[n];
            shadow.Coverage = new byte[n];
            for (int i = 0; i < n; i++)
            {
                shadow.Values[i] = data[i * stride];
                shadow.Coverage[i] = data[i * stride + stride - 1];
            }
            if (sx == result.SizeX && sy == result.SizeY) result.ShadowMaps.Add(shadow);
        }
        return result;
    }

    static LightmassMappingResult ReadVertex(BinaryReader r)
    {
        var result = new LightmassMappingResult { Guid = new Guid(r.ReadBytes(16)), IsTextureMapping = false, ExecutionTime = r.ReadDouble() };
        // FLightMapData1DData (48)
        uint compressed = r.ReadUInt32(), uncompressed = r.ReadUInt32();
        var scale = ReadScale(r);
        result.SizeX = r.ReadInt32();
        result.SizeY = 1;
        int shadowMaps = r.ReadInt32();
        result.LightGuids = ReadGuids(r, r.ReadInt32());
        Decode(result, Inflate(r, compressed, uncompressed), scale, result.SizeX);
        Array.Fill(result.Coverage, (byte)255);
        r.ReadByte(); // PreviewEnvironmentShadowing
        // FShadowMapData1DData (compressed, uncompressed, NumSamples) followed by unquantized, uncompressed floats
        for (int s = 0; s < shadowMaps; s++)
        {
            var shadow = new LightmassShadowMap { LightGuid = new Guid(r.ReadBytes(16)) };
            r.ReadUInt32(); r.ReadUInt32();
            int samples = r.ReadInt32();
            shadow.Samples = new float[samples];
            for (int i = 0; i < samples; i++) shadow.Samples[i] = r.ReadSingle();
            if (samples == result.SizeX) result.ShadowMaps.Add(shadow);
        }
        return result;
    }

    static Vector3[] ReadScale(BinaryReader r)
    {
        var scale = new Vector3[Coefficients];
        for (int k = 0; k < Coefficients; k++) scale[k] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        return scale;
    }

    static Guid[] ReadGuids(BinaryReader r, int count)
    {
        var guids = new Guid[count];
        for (int i = 0; i < count; i++) guids[i] = new Guid(r.ReadBytes(16));
        return guids;
    }

    /// <summary>The quantized samples: zlib-compressed unless the compressed size is 0.</summary>
    static byte[] Inflate(BinaryReader r, uint compressed, uint uncompressed)
    {
        if (compressed == 0) return r.ReadBytes((int)uncompressed);
        var data = r.ReadBytes((int)compressed);
        var output = new byte[uncompressed];
        using var zlib = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress);
        zlib.ReadExactly(output);
        return output;
    }

    /// <summary>
    /// FQuantizedLightSampleData (10 bytes: coverage, then 3×RGB): byte = trunc(pow(value / scale, 1/2.2) · 255) (QuantizeLightSamples,
    /// LightmapData.cpp). Decoded at the middle of each byte's interval, which undoes the truncation's bias. The directional pair
    /// recombines as E_i = colour · max_i (Directional.Combine).
    /// </summary>
    static void Decode(LightmassMappingResult result, byte[] data, Vector3[] scale, int samples)
    {
        if (samples < 0 || data.Length < (long)samples * 10)
            throw new InvalidDataException($"lightmap of {result.Guid}: {data.Length} bytes for {result.SizeX}x{result.SizeY} samples");
        result.Directional = new Directional.Sample[samples];
        result.Simple = new Vector3[samples];
        result.Coverage = new byte[samples];
        for (int i = 0; i < samples; i++)
        {
            int o = i * 10;
            result.Coverage[i] = data[o];
            Vector3 C(int k) => new Vector3(Fraction(data[o + 1 + k * 3]), Fraction(data[o + 2 + k * 3]), Fraction(data[o + 3 + k * 3])) * scale[k];
            result.Directional[i] = Codec.Directional.Combine(C(0), C(1));
            result.Simple[i] = C(2);
        }
    }

    static readonly float[] Fractions = BuildFractions();

    static float[] BuildFractions()
    {
        var table = new float[256];
        for (int b = 1; b < 256; b++) table[b] = MathF.Pow(MathF.Min(1f, (b + 0.5f) / 255f), 2.2f);
        return table;
    }

    static float Fraction(byte b) => Fractions[b];
}
