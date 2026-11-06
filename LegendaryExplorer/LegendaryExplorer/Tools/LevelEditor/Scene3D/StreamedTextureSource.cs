using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Texture2D = SharpDX.Direct3D11.Texture2D;
using Device = SharpDX.Direct3D11.Device;
using UnrealTexture = LegendaryExplorerCore.Unreal.Classes.Texture2D;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// An immutable description of a texture. Captured while packages are owned by the scene loader;
/// background reads never touch exports, package caches, the immediate context, or preview overrides.
/// </summary>
internal sealed class StreamedTextureSource
{
    private sealed record Mip(int Width, int Height, StorageTypes Storage, byte[] Data, int Size, int CompressedSize, int Offset, string Archive);
    private readonly Mip[] mips;
    private readonly MEGame game;
    private readonly string packagePath;
    private readonly Format format;
    private readonly (Format Typeless, Format UNorm, Format SRGB)? srgb;
    public int InitialMip { get; }

    private StreamedTextureSource(ExportEntry export, List<Mip> levels, int initial, Format format,
        (Format Typeless, Format UNorm, Format SRGB)? srgb)
    {
        game = export.Game;
        packagePath = export.FileRef.FilePath;
        mips = levels.ToArray();
        InitialMip = initial;
        this.format = format;
        this.srgb = srgb;
    }

    /// <param name="texture">The parsed texture, if it had to be parsed to decide. A full load can reuse it.</param>
    public static StreamedTextureSource TryCreate(ExportEntry export, out UnrealTexture texture)
    {
        texture = null;
        // Flipbook atlases, cube faces and non-streamable textures retain their existing loading behavior.
        if (export.ClassName is not ("Texture2D" or "LightMapTexture2D")) return null;
        texture = new UnrealTexture(export);
        if (texture.NeverStream) return null;
        var top = texture.GetTopMip();
        if (top is null || top.IsPackageStored) return null;
        var format = (Format)TexConverter.GetDXGIFormatForPixelFormat(Image.getPixelFormatType(texture.TextureFormat));
        List<Mip> levels = [];
        int initial = -1;
        foreach (var mip in texture.Mips)
        {
            if (levels.Count == 0 && mip != top) continue;
            int level = levels.Count;
            if (!RenderContextExtensions.IsChainLevel(mip, level, top)) break;
            if (mip.IsPackageStored && initial < 0) initial = level;
            // Require a complete package-resident tail: initial loading must never fall through to TFC I/O.
            if (initial >= 0 && !mip.IsPackageStored) return null;
            levels.Add(new(mip.width, mip.height, mip.storageType, mip.Mip,
                mip.uncompressedSize, mip.compressedSize, mip.externalOffset, mip.TextureCacheName));
        }
        if (initial <= 0) return null;
        // Any level down to the tail can become the largest one, which D3D requires to be whole blocks.
        if (format.IsCompressed() && levels.Take(initial + 1).Any(mip => mip.Width % 4 != 0 || mip.Height % 4 != 0)) return null;
        var srgb = texture.SRGB ? RenderContextExtensions.GetSRGBFormats(format) : null;
        return new StreamedTextureSource(export, levels, initial, format, srgb);
    }

    public int MipForPixels(float pixels)
    {
        int target = 0;
        for (int i = 1; i <= InitialMip && Math.Max(mips[i].Width, mips[i].Height) >= pixels; i++) target = i;
        return target;
    }

    public TextureResources Load(Device device, int firstMip, CancellationToken cancellation)
    {
        using var reads = new TextureDataReadCache();
        var top = mips[firstMip];
        var data = RenderContextExtensions.ReadMipChain(format, top.Width, top.Height, mips.Length - firstMip, level =>
        {
            cancellation.ThrowIfCancellationRequested();
            var mip = mips[firstMip + level];
            return reads.ReadMip(game, mip.Data, mip.Storage, mip.Size, mip.CompressedSize, mip.Offset, mip.Archive, packagePath);
        });
        cancellation.ThrowIfCancellationRequested();
        if (data.Count == 0) throw new InvalidDataException($"Incomplete texture mip {top.Width}x{top.Height}.");
        var texture = RenderContextExtensions.CreateMipChain(device, format, srgb?.Typeless, top.Width, top.Height, data);
        return TextureResources.Create(device, new(texture, srgb?.UNorm ?? format, srgb?.SRGB), false);
    }
}

/// <summary>Owns a texture and both color-space views, including partial-creation failure cleanup.</summary>
internal sealed class TextureResources : IDisposable
{
    public Texture2D Texture { get; private set; }
    public ShaderResourceView View { get; private set; }
    public ShaderResourceView LinearView { get; private set; }

    public static TextureResources Create(Device device, MeshRenderContext.LoadedTexture loaded, bool cube)
    {
        var result = new TextureResources { Texture = loaded.Texture };
        try
        {
            var desc = new ShaderResourceViewDescription { Format = loaded.ViewFormat,
                Dimension = cube ? ShaderResourceViewDimension.TextureCube : ShaderResourceViewDimension.Texture2D };
            if (cube) desc.TextureCube.MipLevels = -1;
            else desc.Texture2D.MipLevels = -1;
            result.View = new ShaderResourceView(device, loaded.Texture, desc);
            result.LinearView = result.View;
            if (loaded.SRGBViewFormat is Format srgb)
            {
                desc.Format = srgb;
                result.LinearView = new ShaderResourceView(device, loaded.Texture, desc);
            }
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    public void Dispose()
    {
        if (LinearView != View) LinearView?.Dispose();
        LinearView = null;
        View?.Dispose();
        View = null;
        Texture?.Dispose();
        Texture = null;
    }
}
