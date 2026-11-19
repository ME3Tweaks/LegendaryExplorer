using System;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Input;
using FontAwesome5;
using SharpDX.Direct3D11;
using LegendaryExplorerCore.Unreal;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Threading;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Resource = SharpDX.Direct3D11.Resource;
using LegendaryExplorerCore.Gammtek;
using Texture2D = SharpDX.Direct3D11.Texture2D;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

using LECTexture2D = LegendaryExplorerCore.Unreal.Classes.Texture2D;

public static class RenderContextExtensions
{
    /// <param name="resourceFormat">Format to create the texture with, if different from <paramref name="format"/>. (eg. a typeless format, so that it can have both UNORM and sRGB views)</param>
    public static unsafe Texture2D LoadTexture(this RenderContext renderContext, uint width, uint height, Format format, byte[] pixelData, Format? resourceFormat = null)
    {
        Texture2DDescription texture2DDescription = GetTextureDescription(width, height, format, false, out int pitch);
        texture2DDescription.Format = resourceFormat ?? format;
        fixed (byte* pixelDataPointer = pixelData)
        {
            return new Texture2D(renderContext.Device, texture2DDescription, new SharpDX.DataRectangle((IntPtr)pixelDataPointer, pitch));
        }
    }

    private static Texture2DDescription GetTextureDescription(uint width, uint height, Format format, bool isCube, out int pitch)
    {
        pitch = (int)(format.SizeOfInBits() * width / 8);
        if (format.IsCompressed())
        {
            // Pitch calculation for compressed formats from https://docs.microsoft.com/en-us/windows/win32/direct3ddds/dx-graphics-dds-pguide
            int blockSize = 16;
            if (format is Format.BC1_UNorm or Format.BC1_UNorm_SRgb or Format.BC4_SNorm or Format.BC4_UNorm)
            {
                blockSize = 8;
            }
            pitch = (int)(Math.Max(1, ((width + 3) / 4)) * blockSize);
        }

        var texture2DDescription = new Texture2DDescription
        {
            Width = (int)width,
            Height = (int)height,
            ArraySize = isCube ? 6 : 1,
            BindFlags = BindFlags.ShaderResource,
            Usage = isCube ? ResourceUsage.Default : ResourceUsage.Immutable,

            CpuAccessFlags = CpuAccessFlags.None,
            Format = format,
            MipLevels = 1,
            OptionFlags = isCube ? ResourceOptionFlags.TextureCube : ResourceOptionFlags.None,
            SampleDescription = new SampleDescription(1, 0),
        };
        return texture2DDescription;
    }

    public static Texture2D LoadTextureCube(this RenderContext renderContext, uint size, Format format, Fixed6<byte[]> faceData, Format? resourceFormat = null)
    {
        if (size == 0 || size > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(size));
        int requiredBytes = GetMipByteSize(format, (int)size, (int)size, out _);
        // UpdateSubresource takes a pointer, so a short face would read past the array.
        for (int i = 0; i < faceData.Length; i++)
        {
            if (faceData[i] is null || faceData[i].Length < requiredBytes)
            {
                throw new ArgumentException($"Cubemap face {i} needs {requiredBytes} bytes for {size}x{size} {format}, but has {faceData[i]?.Length ?? 0}.", nameof(faceData));
            }
        }
        Texture2DDescription texture2DDescription = GetTextureDescription(size, size, format, true, out int pitch);
        texture2DDescription.Format = resourceFormat ?? format;
        var tex = new Texture2D(renderContext.Device, texture2DDescription);
        try
        {
            for (int i = 0; i < faceData.Length; i++)
            {
                renderContext.Device.ImmediateContext.UpdateSubresource(faceData[i], tex, i, pitch);
            }
            return tex;
        }
        catch
        {
            tex.Dispose();
            throw;
        }
    }

    public static unsafe Texture2D LoadTextureFromFile(this RenderContext renderContext, string filename)
    {
        var pixelFormat = LegendaryExplorerCore.Textures.PixelFormat.ARGB;
        byte[] pixelData = LegendaryExplorerCore.Textures.TexConverter.LoadTexture(filename, out uint width, out uint height, ref pixelFormat); // NEEDS WAY TO HAVE ALPHA AS BLACK!
        Format format = (Format)LegendaryExplorerCore.Textures.TexConverter.GetDXGIFormatForPixelFormat(pixelFormat);
        return renderContext.LoadTexture(width, height, format, pixelData);
    }

    public static byte[] ReadTextureData(this RenderContext renderContext, LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo mip)
        => renderContext is MeshRenderContext meshContext ? meshContext.ReadTextureMip(mip) : LECTexture2D.GetTextureData(mip, mip.Export.Game);

    public static Texture2D LoadUnrealMip(this RenderContext renderContext, LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo mip, LegendaryExplorerCore.Textures.PixelFormat pixelFormat, bool typelessResource = false)
    {
        // Todo: Needs way to set black alpha
        var imagebytes = renderContext.ReadTextureData(mip);
        uint mipWidth = (uint)mip.width;
        uint mipHeight = (uint)mip.height;
        var mipFormat = (Format)LegendaryExplorerCore.Textures.TexConverter.GetDXGIFormatForPixelFormat(pixelFormat);
        if (mipFormat.IsCompressed())
        {
            mipWidth = (mipWidth < 4) ? 4 : mipWidth;
            mipHeight = (mipHeight < 4) ? 4 : mipHeight;
        }
        Format? resourceFormat = typelessResource ? GetSRGBFormats(mipFormat)?.Typeless : null;
        return renderContext.LoadTexture(mipWidth, mipHeight, mipFormat, imagebytes, resourceFormat);
    }

    /// <summary>
    /// Loads <paramref name="topMip"/> and as many of the mips below it as are available, so the texture can be minified without aliasing.
    /// </summary>
    /// <param name="mips">All of the texture's mips, largest first</param>
    public static Texture2D LoadUnrealMipChain(this RenderContext renderContext, IReadOnlyList<LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo> mips,
        LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo topMip, LegendaryExplorerCore.Textures.PixelFormat pixelFormat, bool typelessResource = false)
    {
        var format = (Format)LegendaryExplorerCore.Textures.TexConverter.GetDXGIFormatForPixelFormat(pixelFormat);
        bool isCompressed = format.IsCompressed();
        int topWidth = topMip.width;
        int topHeight = topMip.height;
        //block-compressed textures must have dimensions that are multiples of 4. Tiny ones are padded, and can't have mips below them
        if (isCompressed && (topWidth % 4 != 0 || topHeight % 4 != 0))
        {
            return renderContext.LoadUnrealMip(topMip, pixelFormat, typelessResource);
        }

        int topIndex = -1;
        for (int i = 0; i < mips.Count; i++)
        {
            if (mips[i] == topMip) topIndex = i;
        }
        List<byte[]> levels = ReadMipChain(format, topWidth, topHeight, topIndex < 0 ? 0 : mips.Count - topIndex, level =>
        {
            var mip = mips[topIndex + level];
            return IsChainLevel(mip, level, topMip) ? renderContext.ReadTextureData(mip) : null;
        });
        if (levels.Count == 0)
        {
            return renderContext.LoadUnrealMip(topMip, pixelFormat, typelessResource);
        }
        return CreateMipChain(renderContext.Device, format, typelessResource ? GetSRGBFormats(format)?.Typeless : null, topWidth, topHeight, levels);
    }

    /// <summary>
    /// Whether a mip is the given level of the chain below <paramref name="top"/>
    /// </summary>
    internal static bool IsChainLevel(LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo mip, int level, LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo top) =>
        mip.storageType is not StorageTypes.empty && mip.width == Math.Max(1, top.width >> level) && mip.height == Math.Max(1, top.height >> level);

    /// <summary>
    /// Reads as many levels of a mip chain as are available. The top level is what a draw shows up close, so a missing
    /// or bad level below it only costs the levels from there on, which only matters when the texture is minified.
    /// </summary>
    /// <param name="read">Reads a level of the chain. Null ends the chain</param>
    /// <returns>The complete levels, largest first. Empty if the top level's data is missing or too short. An exception reading it isn't caught</returns>
    internal static List<byte[]> ReadMipChain(Format format, int topWidth, int topHeight, int levelCount, Func<int, byte[]> read)
    {
        var levels = new List<byte[]>(levelCount);
        for (int level = 0; level < levelCount; level++)
        {
            byte[] data;
            try
            {
                data = read(level);
            }
            catch (Exception e) when (level > 0 && e is not OperationCanceledException)
            {
                Debug.WriteLine($"Could not read level {level} of a {topWidth}x{topHeight} mip chain: {e}");
                break;
            }
            if (data is null || data.Length < GetMipByteSize(format, Math.Max(1, topWidth >> level), Math.Max(1, topHeight >> level), out _))
            {
                break;
            }
            levels.Add(data);
        }
        return levels;
    }

    /// <summary>
    /// Creates a texture from complete mips, largest first. Only uses the device, so this is safe on a worker thread.
    /// </summary>
    /// <param name="resourceFormat">Format to create the texture with, if different from <paramref name="format"/></param>
    internal static Texture2D CreateMipChain(Device device, Format format, Format? resourceFormat, int width, int height, IReadOnlyList<byte[]> levels)
    {
        Texture2DDescription desc = GetTextureDescription((uint)width, (uint)height, format, false, out _);
        desc.MipLevels = levels.Count;
        desc.Format = resourceFormat ?? format;

        var handles = new System.Runtime.InteropServices.GCHandle[levels.Count];
        var rects = new SharpDX.DataRectangle[levels.Count];
        try
        {
            for (int level = 0; level < levels.Count; level++)
            {
                handles[level] = System.Runtime.InteropServices.GCHandle.Alloc(levels[level], System.Runtime.InteropServices.GCHandleType.Pinned);
                GetMipByteSize(format, Math.Max(1, width >> level), Math.Max(1, height >> level), out int pitch);
                rects[level] = new SharpDX.DataRectangle(handles[level].AddrOfPinnedObject(), pitch);
            }
            return new Texture2D(device, desc, rects);
        }
        finally
        {
            foreach (var handle in handles)
            {
                if (handle.IsAllocated) handle.Free();
            }
        }
    }

    internal static int GetMipByteSize(Format format, int width, int height, out int pitch)
    {
        if (format.IsCompressed())
        {
            int blockSize = format is Format.BC1_UNorm or Format.BC1_UNorm_SRgb or Format.BC4_SNorm or Format.BC4_UNorm ? 8 : 16;
            int blocksWide = Math.Max(1, (width + 3) / 4);
            int blocksHigh = Math.Max(1, (height + 3) / 4);
            pitch = blocksWide * blockSize;
            return pitch * blocksHigh;
        }
        pitch = format.SizeOfInBits() * width / 8;
        return pitch * height;
    }

    /// <summary>
    /// For formats that have an sRGB variant, returns the typeless format a texture must be created with to have both UNORM and sRGB views.
    /// Returns null for formats without an sRGB variant (eg. BC4/BC5, which are only used for non-color data).
    /// </summary>
    public static (Format Typeless, Format UNorm, Format SRGB)? GetSRGBFormats(Format format) => format switch
    {
        Format.BC1_UNorm or Format.BC1_UNorm_SRgb => (Format.BC1_Typeless, Format.BC1_UNorm, Format.BC1_UNorm_SRgb),
        Format.BC2_UNorm or Format.BC2_UNorm_SRgb => (Format.BC2_Typeless, Format.BC2_UNorm, Format.BC2_UNorm_SRgb),
        Format.BC3_UNorm or Format.BC3_UNorm_SRgb => (Format.BC3_Typeless, Format.BC3_UNorm, Format.BC3_UNorm_SRgb),
        Format.BC7_UNorm or Format.BC7_UNorm_SRgb => (Format.BC7_Typeless, Format.BC7_UNorm, Format.BC7_UNorm_SRgb),
        Format.R8G8B8A8_UNorm or Format.R8G8B8A8_UNorm_SRgb => (Format.R8G8B8A8_Typeless, Format.R8G8B8A8_UNorm, Format.R8G8B8A8_UNorm_SRgb),
        Format.B8G8R8A8_UNorm or Format.B8G8R8A8_UNorm_SRgb => (Format.B8G8R8A8_Typeless, Format.B8G8R8A8_UNorm, Format.B8G8R8A8_UNorm_SRgb),
        Format.B8G8R8X8_UNorm or Format.B8G8R8X8_UNorm_SRgb => (Format.B8G8R8X8_Typeless, Format.B8G8R8X8_UNorm, Format.B8G8R8X8_UNorm_SRgb),
        _ => null
    };

    public static Mesh<WorldVertex> GetMeshFromAggGeom(this RenderContext renderContext, StructProperty aggGeom)
    {
        if (aggGeom?.GetProp<ArrayProperty<StructProperty>>("ConvexElems") is ArrayProperty<StructProperty> convexElems)
        {
            var vertices = new List<WorldVertex>();
            var triangles = new List<Triangle>();
            int vertTotal = 0;
            foreach (StructProperty convexElem in convexElems)
            {
                var faceTriData = convexElem.GetProp<ArrayProperty<IntProperty>>("FaceTriData");
                for (int i = 0; i < faceTriData.Count; i += 3)
                {
                    triangles.Add(new Triangle((uint)(faceTriData[i].Value + vertTotal), (uint)(faceTriData[i + 1].Value + vertTotal), (uint)(faceTriData[i + 2].Value + vertTotal)));
                }

                var vertexData = convexElem.GetProp<ArrayProperty<StructProperty>>("VertexData");
                foreach (StructProperty vertex in vertexData)
                {
                    float x = vertex.GetProp<FloatProperty>("X").Value;
                    float y = vertex.GetProp<FloatProperty>("Y").Value;
                    float z = vertex.GetProp<FloatProperty>("Z").Value;
                    vertices.Add(new WorldVertex(new Vector3(x, y, z), Vector4.Zero, Vector2.Zero));
                    ++vertTotal;
                }
            }

            return new Mesh<WorldVertex>(renderContext.Device, triangles, vertices);
        }

        return null;
    }
}
