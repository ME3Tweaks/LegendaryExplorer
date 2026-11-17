using LegendaryExplorer.Misc;
using LegendaryExplorer.Resources;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Windows.Input;
using Color = System.Windows.Media.Color;
using D2D = SharpDX.Direct2D1;
using DW = SharpDX.DirectWrite;
using LECTexture2D = LegendaryExplorerCore.Unreal.Classes.Texture2D;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

public partial class MeshRenderContext
{
    /// <summary>Opt-in for interactive level views. Material captures and other previews load full textures.</summary>
    public bool TextureStreamingEnabled { get; init; }
    //no texture's second mip is this large, so it selects the whole chain
    internal const float FullChainPixels = 16384;

    /// <summary>
    /// The size, in pixels, of a draw with these bounds, which decides how much of a texture's mip chain it needs
    /// </summary>
    public float TexturePixels(BoxSphereBounds bounds) => EstimateTexturePixels(Camera, Width, Height, bounds);

    internal static float EstimateTexturePixels(SceneCamera camera, int width, int height, BoxSphereBounds bounds)
    {
        float radius = bounds.SphereRadius;
        // Light functions have no mesh to estimate from, and are few enough to afford their whole mip chains.
        if (radius <= 0) return FullChainPixels;
        // Use the near side of the bounding sphere and one extra mip of detail to anticipate approach.
        // Material UV scaling isn't known here, so a mesh that samples part of a texture (a trim sheet) stays softer than it should until it's closer.
        float pixels = camera.IsOrthographic
            ? 2 * radius * Math.Max(1, width) / Math.Max(1, camera.OrthoWidth)
            : radius * Math.Max(1, height) / (Math.Max(1, Vector3.Distance(camera.EyePosition, bounds.Origin) - radius) * MathF.Tan(camera.FOV * 0.5f));
        return float.IsFinite(pixels) ? Math.Clamp(pixels * 2, 1, FullChainPixels) : FullChainPixels;
    }

    /// <summary>
    /// A texture loaded from an Unreal texture, and the formats its views should be created with.
    /// </summary>
    /// <param name="ViewFormat">Format for a view that reads the raw texel values (what LEX's shader expects)</param>
    /// <param name="SRGBViewFormat">If the texture holds sRGB-encoded color, a format for a view that converts to linear (what the game's shaders expect). Otherwise null.</param>
    public readonly record struct LoadedTexture(Texture2D Texture, Format ViewFormat, Format? SRGBViewFormat);

    /// <summary>Optional per-preview mip reader. Null results use the package's usual texture lookup.</summary>
    public Func<LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo, byte[]> TextureDataOverride { get; set; }

    private readonly Lock loadSync = new();
    private LegendaryExplorerCore.Unreal.Classes.TextureDataReadCache textureDataReads;
    private int loadDepth;

    /// <summary>
    /// Starts a load, which ends when the result is disposed. Texture archive readers, parsed meshes and
    /// resolved imports are reused until the outermost load ends, including on failure.
    /// </summary>
    public IDisposable BeginLoad()
    {
        lock (loadSync)
        {
            if (loadDepth++ == 0)
            {
                textureDataReads = new();
                StaticMeshes.SetLoading(true);
                PackageCache.SetLoading(true);
            }
            return new LoadScope(this);
        }
    }

    private sealed class LoadScope(MeshRenderContext owner) : IDisposable
    {
        private MeshRenderContext context = owner;
        public void Dispose()
        {
            var current = Interlocked.Exchange(ref context, null);
            if (current is null) return;
            lock (current.loadSync)
            {
                if (--current.loadDepth == 0)
                {
                    current.textureDataReads.Dispose();
                    current.textureDataReads = null;
                    current.StaticMeshes.SetLoading(false);
                    current.PackageCache.SetLoading(false);
                }
            }
        }
    }

    internal byte[] ReadTextureMip(LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo mip)
    {
        // Bake previews may supply unsaved mip data. Only a null override result reads disk.
        if (TextureDataOverride?.Invoke(mip) is { } data) return data;
        lock (loadSync)
        {
            if (textureDataReads is not null) return textureDataReads.ReadMip(mip, mip.Export.Game);
        }
        return LECTexture2D.GetTextureData(mip, mip.Export.Game);
    }

    public LoadedTexture LoadUnrealTexture(ExportEntry texture2DExport)
    {
        if (texture2DExport.ClassName is "TextureRenderTarget2D" or "TextureMovie")
        {
            return new LoadedTexture(CreateWhiteTexture(), Format.R8G8B8A8_UNorm, null);
        }
        return LoadUnrealTexture(new LECTexture2D(texture2DExport));
    }

    /// <summary>Loads an already parsed texture, for callers that had to parse it to decide how to load it.</summary>
    internal LoadedTexture LoadUnrealTexture(LECTexture2D unrealTexture)
    {
        var pixelFormat = LegendaryExplorerCore.Textures.Image.getPixelFormatType(unrealTexture.TextureFormat);
        var format = (Format)LegendaryExplorerCore.Textures.TexConverter.GetDXGIFormatForPixelFormat(pixelFormat);
        var srgbFormats = unrealTexture.SRGB ? RenderContextExtensions.GetSRGBFormats(format) : null;
        Texture2D texture = this.LoadUnrealMipChain(unrealTexture.Mips, unrealTexture.GetTopMip(), pixelFormat, typelessResource: srgbFormats is not null);
        return new LoadedTexture(texture, srgbFormats?.UNorm ?? format, srgbFormats?.SRGB);
    }

    public LoadedTexture LoadUnrealTextureCube(ExportEntry textureCubeExport, PackageCache packageCache = null)
    {
        if (textureCubeExport.ClassName != "TextureCube") throw new ArgumentException("Expected a TextureCube export.", nameof(textureCubeExport));

        var props = textureCubeExport.GetProperties();
        var faceTextures = new Fixed6<LECTexture2D>();
        Span<string> facePropNames = ["FacePosX", "FaceNegX", "FacePosY", "FaceNegY", "FacePosZ", "FaceNegZ"];
        for (int i = 0; i < 6; i++)
        {
            ObjectProperty faceProp = props.GetProp<ObjectProperty>(facePropNames[i]);
            if (faceProp is null)
            {
                return new LoadedTexture(CreateWhiteTextureCube(), Format.R8G8B8A8_UNorm, null);
            }
            faceTextures[i] = new(faceProp.ResolveToExport(textureCubeExport.FileRef, packageCache));
        }
        var pixelData = new Fixed6<byte[]>();

        var format = (Format)LegendaryExplorerCore.Textures.TexConverter.GetDXGIFormatForPixelFormat(
            LegendaryExplorerCore.Textures.Image.getPixelFormatType(faceTextures[0].Export.GetProperty<EnumProperty>("Format").Value.Name));
        // D3D requires equal faces, but cooked faces can differ (LE2's Bch_OCEAN_CUBEA has one 256px face).
        int size = faceTextures[0].Mips
            .Where(mip => mip.storageType != StorageTypes.empty && mip.width > 0 && mip.width == mip.height)
            .Select(mip => mip.width)
            .OrderByDescending(width => width)
            .FirstOrDefault(width => Enumerable.Range(0, 6).All(i => faceTextures[i].Mips
                .Any(mip => mip.storageType != StorageTypes.empty && mip.width == width && mip.height == width)));
        if (size == 0)
        {
            throw new InvalidDataException($"Cubemap {textureCubeExport.InstancedFullPath} has no square mip size shared by all six faces.");
        }
        for (int i = 0; i < 6; i++)
        {
            var faceFormat = (Format)LegendaryExplorerCore.Textures.TexConverter.GetDXGIFormatForPixelFormat(
                LegendaryExplorerCore.Textures.Image.getPixelFormatType(faceTextures[i].Export.GetProperty<EnumProperty>("Format").Value.Name));
            if (faceFormat != format)
            {
                throw new InvalidDataException($"Cubemap {textureCubeExport.InstancedFullPath} has inconsistent face formats.");
            }
            var mip = faceTextures[i].Mips.First(mip => mip.storageType != StorageTypes.empty && mip.width == size && mip.height == size);
            pixelData[i] = this.ReadTextureData(mip);
        }
        var srgbFormats = faceTextures[0].SRGB ? RenderContextExtensions.GetSRGBFormats(format) : null;
        uint textureSize = (uint)(format.IsCompressed() ? Math.Max(4, size) : size);
        Texture2D texture = this.LoadTextureCube(textureSize, format, pixelData, srgbFormats?.Typeless);
        return new LoadedTexture(texture, srgbFormats?.UNorm ?? format, srgbFormats?.SRGB);
    }
}
