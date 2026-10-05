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
    public static UdkTextureEncodingDecision Plan(string format, string compression, bool requireThreeChannels = false)
    {
        bool normal = TextureEncodingPolicy.IsNormalCompression(compression);
        if (requireThreeChannels)
        {
            if (!TextureEncodingPolicy.IsTwoChannelNormal(format, compression))
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
            if (decision.ReconstructZ)
                for (int lane = 0; lane < 3; lane++)
                {
                    props.AddOrReplaceProp(new FloatProperty(-1f, "UnpackMin") { StaticArrayIndex = lane });
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
            if (decision.ReconstructZ) TextureEncodingPolicy.ReconstructNormalZ(rgba);
            return rgba.mipMaps[0];
        }).ToList();
        if (mips.Count == 0) throw new InvalidOperationException($"No resident or resolvable mips: {texture.InstancedFullPath}.");
        return new Image(mips, PixelFormat.ARGB);
    }
}
