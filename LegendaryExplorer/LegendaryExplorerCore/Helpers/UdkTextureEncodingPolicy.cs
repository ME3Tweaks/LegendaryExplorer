using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;

namespace LegendaryExplorerCore.Helpers;

public sealed record UdkTextureEncodingDecision(string SourceFormat, string TargetFormat,
    string TargetCompression, bool IsNormal, bool ReconstructZ)
{
    public bool ChangesPixels => SourceFormat != TargetFormat;
}

/// <summary>UDK-compatible texture intermediates without another lossy block conversion.</summary>
public static class UdkTextureEncodingPolicy
{
    public static bool IsNormalCompression(string compression) => compression?.ToUpperInvariant() is
        "TC_NORMALMAP" or "TC_NORMALMAPHQ" or "TC_NORMALMAPALPHA" or "TC_NORMALMAPUNCOMPRESSED"
        or "TC_NORMALMAPBC5" or "TC_NORMALMAPBC7";

    public static bool IsTwoChannelNormal(string format, string compression) =>
        IsNormalCompression(compression) && format is "PF_V8U8" or "PF_BC5" or "PF_NormalMap_HQ" or "PF_NormalMap_LQ";

    /// <summary>Rebuilds Z from XY in an ARGB image: X in byte 2, Y in byte 1, Z written to byte 0.</summary>
    public static void ReconstructNormalZ(Image image)
    {
        if (image.pixelFormat != PixelFormat.ARGB) throw new ArgumentException("Normal reconstruction requires ARGB pixels.", nameof(image));
        foreach (var mip in image.mipMaps)
        for (int i = 0; i + 3 < mip.data.Length; i += 4)
        {
            double x = mip.data[i + 2] / 255d * 2 - 1, y = mip.data[i + 1] / 255d * 2 - 1;
            double z = Math.Sqrt(Math.Max(0, 1 - x * x - y * y));
            mip.data[i] = (byte)Math.Clamp(Math.Round((z + 1) * 127.5), 0, 255);
        }
    }

    public static UdkTextureEncodingDecision Plan(string format, string compression, bool requireThreeChannels = false)
    {
        bool normal = IsNormalCompression(compression);
        if (requireThreeChannels)
        {
            if (!IsTwoChannelNormal(format, compression))
                throw new InvalidOperationException("An XYZ sampler intermediate requires a declared two-channel normal.");
            return new(format, "PF_A8R8G8B8", "TC_Normalmap", true, true);
        }
        if (format != "PF_BC7") return new(format, format, compression, normal, false);
        string targetCompression = normal ? "TC_Normalmap" : compression is null or "TC_BC7" ? "TC_Default" : compression;
        return new(format, "PF_A8R8G8B8", targetCompression, normal, false);
    }

    public static UdkTextureEncodingDecision Plan(ExportEntry texture, bool requireThreeChannels = false)
    {
        var props = texture.GetProperties();
        return Plan(props.GetProp<EnumProperty>("Format")?.Value.Name,
            props.GetProp<EnumProperty>("CompressionSettings")?.Value.Name, requireThreeChannels);
    }

    public static void PrepareProperties(PropertyCollection props, UdkTextureEncodingDecision decision)
    {
        if (!decision.ChangesPixels) return;
        props.AddOrReplaceProp(new EnumProperty(decision.TargetFormat, "EPixelFormat", MEGame.UDK, "Format"));
        props.AddOrReplaceProp(new EnumProperty(decision.TargetCompression, "TextureCompressionSettings", MEGame.UDK, "CompressionSettings"));
        if (decision.IsNormal)
        {
            props.AddOrReplaceProp(new BoolProperty(false, "SRGB"));
            // Alpha is written too: some two-channel sources carry UnpackMin[3] = -1, which is not the
            // signed RGB normal range (alpha 0..1) that NormalCompressionHelper and the shader expect.
            if (decision.ReconstructZ)
                for (int lane = 0; lane < 4; lane++)
                {
                    props.AddOrReplaceProp(new FloatProperty(lane < 3 ? -1f : 0f, "UnpackMin") { StaticArrayIndex = lane });
                    props.AddOrReplaceProp(new FloatProperty(1f, "UnpackMax") { StaticArrayIndex = lane });
                }
        }
        props.AddOrReplaceProp(new BoolProperty(true, "CompressionNone"));
    }

    /// <summary>Decode every existing mip independently, retaining alpha and authored lower-mip data.</summary>
    public static Image CreateImage(ExportEntry texture, UdkTextureEncodingDecision decision)
    {
        if (!decision.ChangesPixels) throw new InvalidOperationException("No UDK pixel conversion was planned.");
        var source = new Texture2D(texture);
        var sourceFormat = Image.getPixelFormatType(decision.SourceFormat);
        var mips = source.Mips.Where(m => m.storageType != StorageTypes.empty).Select(m =>
        {
            var rgba = Image.LoadFromRaw(Texture2D.GetTextureData(m, texture.Game), sourceFormat, m.width, m.height);
            rgba.correctMips(PixelFormat.ARGB);
            if (decision.ReconstructZ) ReconstructNormalZ(rgba);
            return rgba.mipMaps[0];
        }).ToList();
        if (mips.Count == 0) throw new InvalidOperationException($"No resident or resolvable mips: {texture.InstancedFullPath}.");
        return new Image(mips, PixelFormat.ARGB);
    }
}
