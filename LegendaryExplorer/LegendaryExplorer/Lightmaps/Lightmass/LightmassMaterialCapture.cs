using System;
using System.Collections.Generic;
using System.Numerics;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Device = SharpDX.Direct3D11.Device;

namespace LegendaryExplorer.Lightmaps.Lightmass;

/// <summary>
/// A material rendered over its UV space with the game's own base pass shaders, as UE3's FLightmassMaterialRenderer renders a
/// material's properties into textures for Lightmass (LightmassRender.cpp). The game ships no material export shaders, so the
/// properties are recovered from the base pass without a light-map, which outputs Emissive + DiffuseColor * AmbientColor
/// (BasePassPixelShader.usf): rendered with an ambient color of 1 and of 0, the difference is the diffuse color.
/// </summary>
public sealed class CapturedMaterial
{
    public int Size;
    /// <summary>Ambient 1: emissive plus diffuse in RGB; the shader's alpha (the opacity, for translucent blend modes).</summary>
    public Vector4[] Lit;
    /// <summary>Ambient 0: the emissive color.</summary>
    public Vector4[] Unlit;
    /// <summary>False where the material clipped the texel (masked materials' opacity mask).</summary>
    public bool[] Covered;

    public Vector3 Emissive(int i) => new(Unlit[i].X, Unlit[i].Y, Unlit[i].Z);
    public Vector3 Diffuse(int i) => new(Lit[i].X - Unlit[i].X, Lit[i].Y - Unlit[i].Y, Lit[i].Z - Unlit[i].Z);
    public float Opacity(int i) => Lit[i].W;
}

/// <summary>
/// Renders materials for Lightmass (<see cref="CapturedMaterial"/>) on its own Direct3D device, offscreen. Not thread-safe: use it from
/// one thread. The quad it draws is the material's whole UV space ([0,1]², all texture coordinate channels), facing +Z, seen from
/// straight above, with white vertex colors, as UE3 draws it (its compiler also replaces the camera vector with +Z).
/// </summary>
public sealed class LightmassMaterialCapture : IDisposable
{
    /// <summary>What a texel the material clipped keeps: the shader never writes it.</summary>
    const float Unwritten = -1e30f;
    static readonly Vector3 EyePosition = new(0.5f, 0.5f, 1000f);

    readonly MeshRenderContext context;
    readonly Mesh<LEVertex> quad;
    readonly RasterizerState rasterizer;
    readonly DepthStencilState noDepth;
    readonly BlendState noBlending;
    readonly Dictionary<int, (Texture2D Target, RenderTargetView View, Texture2D Staging)> targets = new();
    int materialsSinceTextureFlush;

    /// <summary>World (x, y) is the UV: from u, v in [0,1] to the render target's x right and y down, z unused.</summary>
    static readonly Matrix4x4 UvProjection = new(
        2, 0, 0, 0,
        0, -2, 0, 0,
        0, 0, 1e-4f, 0,
        -1, 1, 0.5f, 1);

    public LightmassMaterialCapture()
    {
        context = new MeshRenderContext();
        context.CreateResources();
        context.UseGameShaders = true;
        context.Camera.FirstPerson = true;
        context.Camera.Position = EyePosition;
        Device device = context.Device;

        var corners = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        var vertices = new List<LEVertex>();
        foreach (var uv in corners)
        {
            Fixed4<Vector4> uvs = default;
            for (int i = 0; i < 4; i++) uvs[i] = new Vector4(uv, 0, 0);
            vertices.Add((LEVertex)LEVertex.Create(new Vector3(uv, 0), Vector3.UnitX, new Vector4(0, 0, 1, 1), uvs));
        }
        quad = new Mesh<LEVertex>(device, [new Triangle(0, 1, 2), new Triangle(0, 2, 3)], vertices);

        rasterizer = new RasterizerState(device, new RasterizerStateDescription { CullMode = CullMode.None, FillMode = FillMode.Solid, IsDepthClipEnabled = false });
        noDepth = new DepthStencilState(device, new DepthStencilStateDescription { IsDepthEnabled = false, DepthWriteMask = DepthWriteMask.Zero, IsStencilEnabled = false });
        var blend = new BlendStateDescription();
        blend.RenderTarget[0] = new RenderTargetBlendDescription { IsBlendEnabled = false, RenderTargetWriteMask = ColorWriteMaskFlags.All };
        noBlending = new BlendState(device, blend);
    }

    /// <summary>
    /// Renders a material at each of <paramref name="sizes"/> (square, in texels).
    /// </summary>
    /// <exception cref="InvalidOperationException">The material can't be rendered with the game's shaders (the message says why)</exception>
    public Dictionary<int, CapturedMaterial> Capture(IEntry material, IEnumerable<int> sizes)
    {
        var export = material as ExportEntry ?? (material is ImportEntry import ? EntryImporter.ResolveImport(import, context.PackageCache) : null)
            ?? throw new InvalidOperationException("the material couldn't be found");
        // Neighbouring meshes share textures, so the cache is kept between materials and only emptied every 65th material
        if (++materialsSinceTextureFlush > 64)
        {
            context.TextureCache.Dispose();
            materialsSinceTextureFlush = 0;
        }

        var proxy = new MaterialRenderProxy(context, export);
        var textureMap = new Dictionary<string, PreviewTextureCache.TextureEntry>();
        foreach (IEntry textureEntry in proxy.Textures)
        {
            if (!textureMap.ContainsKey(textureEntry.InstancedFullPath) && context.TextureCache.LoadTexture(textureEntry, context.PackageCache) is { } texture)
                textureMap.Add(textureEntry.InstancedFullPath, texture);
        }
        proxy.TextureMap = textureMap;
        proxy.LoadGameShaders();
        if (proxy.GameShaderError is { } error) throw new InvalidOperationException(error);
        if (proxy.NoLightMapVertexShader is not { } vertexShader || proxy.NoLightMapPixelShader is not { } pixelShader)
            throw new InvalidOperationException("the material has no base pass shaders without a light-map");

        var result = new Dictionary<int, CapturedMaterial>();
        foreach (int size in sizes)
        {
            if (result.ContainsKey(size)) continue;
            var captured = new CapturedMaterial
            {
                Size = size,
                Lit = Render(proxy, vertexShader, pixelShader, size, ambient: 1),
                Unlit = Render(proxy, vertexShader, pixelShader, size, ambient: 0),
            };
            captured.Covered = new bool[size * size];
            for (int i = 0; i < captured.Covered.Length; i++)
            {
                captured.Covered[i] = captured.Lit[i].X != Unwritten && captured.Unlit[i].X != Unwritten;
                if (!captured.Covered[i]) captured.Lit[i] = captured.Unlit[i] = Vector4.Zero;
            }
            Dilate(captured);
            result[size] = captured;
        }
        return result;
    }

    /// <summary>
    /// Gives clipped texels the colors of their nearest unclipped neighbours (UE3 renders the properties without clipping), so that sampling
    /// near the mask's edges doesn't blend in black. Their opacity stays 0.
    /// </summary>
    static void Dilate(CapturedMaterial c)
    {
        int size = c.Size;
        var filled = (bool[])c.Covered.Clone();
        var queued = new bool[filled.Length];
        // the UVs wrap
        int Neighbour(int i, int k) => k switch
        {
            0 => i - i % size + (i % size + size - 1) % size,
            1 => i - i % size + (i % size + 1) % size,
            2 => (i + filled.Length - size) % filled.Length,
            _ => (i + size) % filled.Length,
        };

        // Each ring is the unfilled texels next to a filled one; only the last ring's neighbours can be in the next.
        var ring = new List<int>();
        void QueueNeighbours(int i)
        {
            for (int k = 0; k < 4; k++)
            {
                int j = Neighbour(i, k);
                if (!filled[j] && !queued[j]) { queued[j] = true; ring.Add(j); }
            }
        }
        for (int i = 0; i < filled.Length; i++)
            if (filled[i]) QueueNeighbours(i);

        var values = new List<(Vector4 Lit, Vector4 Unlit)>();
        while (ring.Count > 0)
        {
            values.Clear();
            foreach (int i in ring)
            {
                Vector4 lit = default, unlit = default;
                int n = 0;
                for (int k = 0; k < 4; k++)
                {
                    int j = Neighbour(i, k);
                    if (!filled[j]) continue;
                    lit += c.Lit[j]; unlit += c.Unlit[j]; n++;
                }
                values.Add((lit / n, unlit / n));
            }
            var current = ring.ToArray();
            for (int k = 0; k < current.Length; k++)
            {
                c.Lit[current[k]] = values[k].Lit with { W = 0 };
                c.Unlit[current[k]] = values[k].Unlit with { W = 0 };
                filled[current[k]] = true;
            }
            ring.Clear();
            foreach (int i in current) QueueNeighbours(i);
        }
    }

    Vector4[] Render(MaterialRenderProxy proxy, Shader vertexShader, Shader pixelShader, int size, float ambient)
    {
        var (target, view, staging) = Target(size);
        DeviceContext dc = context.ImmediateContext;
        LEEffect effect = context.LEEffect;
        dc.OutputMerger.SetRenderTargets((DepthStencilView)null, view);
        dc.Rasterizer.SetViewport(0, 0, size, size);
        dc.Rasterizer.State = rasterizer;
        dc.OutputMerger.SetDepthStencilState(noDepth);
        dc.ClearRenderTargetView(view, new RawColor4(Unwritten, Unwritten, Unwritten, 0));
        effect.BeginFrame(dc, Matrix4x4.Identity, UvProjection, EyePosition);

        effect.PixelShaderResources.Reset();
        PixelShader ps = context.GetCachedPixelShader(pixelShader.Guid, pixelShader.ShaderByteCode);
        (VertexShader vs, InputLayout inputLayout) = context.GetCachedVertexShader(vertexShader.Guid, vertexShader.ShaderByteCode);
        effect.PrepDraw(dc, vs, ps, inputLayout, noBlending);
        dc.InputAssembler.SetVertexBuffers(MeshStaticLighting.VertexStreamSlot, new VertexBufferBinding(null, MeshStaticLighting.VertexStreamStride, 0));
        proxy.UpdateShaderParams(effect.VertexShaderConstantBuffer, effect.PixelShaderConstantBuffer, context, quad, vertexShader, pixelShader,
            staticLighting: null, usePreviewLighting: false);
        ShaderParameterSetters.WriteAmbientColorAndSkyFactor(pixelShader, effect.PixelShaderConstantBuffer, new LinearColor(ambient, ambient, ambient, 0));
        effect.RenderObject(dc, quad, 0, 6);
        context.RestoreDefaultSamplers();

        dc.CopyResource(target, staging);
        var box = dc.MapSubresource(staging, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
        var pixels = new Vector4[size * size];
        try
        {
            for (int y = 0; y < size; y++)
            {
                unsafe
                {
                    new ReadOnlySpan<Vector4>((byte*)box.DataPointer + (long)y * box.RowPitch, size).CopyTo(pixels.AsSpan(y * size, size));
                }
            }
        }
        finally
        {
            dc.UnmapSubresource(staging, 0);
        }
        return pixels;
    }

    (Texture2D, RenderTargetView, Texture2D) Target(int size)
    {
        if (targets.TryGetValue(size, out var t)) return t;
        var desc = new Texture2DDescription
        {
            Width = size, Height = size, MipLevels = 1, ArraySize = 1, Format = Format.R32G32B32A32_Float,
            SampleDescription = new SampleDescription(1, 0), Usage = ResourceUsage.Default, BindFlags = BindFlags.RenderTarget,
        };
        var target = new Texture2D(context.Device, desc);
        desc.Usage = ResourceUsage.Staging;
        desc.BindFlags = BindFlags.None;
        desc.CpuAccessFlags = CpuAccessFlags.Read;
        var staging = new Texture2D(context.Device, desc);
        return targets[size] = (target, new RenderTargetView(context.Device, target), staging);
    }

    public void Dispose()
    {
        foreach (var (target, view, staging) in targets.Values)
        {
            view.Dispose();
            target.Dispose();
            staging.Dispose();
        }
        targets.Clear();
        quad.Dispose();
        rasterizer.Dispose();
        noDepth.Dispose();
        noBlending.Dispose();
        context.DisposeResources();
    }
}
