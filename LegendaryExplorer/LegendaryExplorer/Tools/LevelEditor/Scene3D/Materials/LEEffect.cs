using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

public unsafe class LEEffect : IDisposable
{
    private readonly SharpDX.Direct3D11.Buffer VertexShaderGlobals;
    private readonly SharpDX.Direct3D11.Buffer VertexShaderConstants;
    private readonly SharpDX.Direct3D11.Buffer PixelShaderGlobals;
    private readonly SharpDX.Direct3D11.Buffer PixelShaderConstants;

    private readonly void* VertexShaderConstantBufferAlloc;
    private readonly void* PixelShaderConstantBufferAlloc;

    //for writing to the hit test render target, which game shaders don't do
    private readonly SharpDX.Direct3D11.Buffer HitProxyConstantBuffer;
    private readonly PixelShader HitProxyPixelShader;
    private readonly BlendState HitProxyBlendState;
    private readonly DepthStencilState HitProxyDepthState;
    private const int HIT_PROXY_CONSTANT_BUFFER_SLOT = 3;

    // Reset whenever something else changes the context's bindings, see PixelShaderResourceBindings.
    public readonly PixelShaderResourceBindings PixelShaderResources = new();

    public Matrix4x4 ViewMatrix { get; private set; }
    public Matrix4x4 ViewProjectionMatrix { get; private set; }
    public Matrix4x4 InverseViewProjectionMatrix { get; private set; }
    /// <summary>
    /// What game shaders get as CameraWorldPos: the world-space position the view is from, or for an orthographic view the direction towards its
    /// camera (see <see cref="SceneCamera.ViewOrigin"/>)
    /// </summary>
    public Vector3 ViewOrigin { get; private set; }
    /// <summary>
    /// FSceneView::PreViewTranslation, -<see cref="ViewOrigin"/> for a perspective view and zero for an orthographic one. Game vertex shaders work in
    /// this translated world space: positions given to them (LocalToWorld, light positions, shadow projections) include it, and materials rebuild
    /// world positions by adding CameraWorldPos back
    /// </summary>
    public Vector3 PreViewTranslation { get; private set; }
    /// <summary>From translated world space (see <see cref="PreViewTranslation"/>) to clip space</summary>
    public Matrix4x4 TranslatedViewProjectionMatrix { get; private set; }
    public LEPSConstants SharedPixelConstants { get; private set; }

    private bool disposedValue;

    public const int CONSTANT_BUFFER_MAX_SIZE = 2560;

    public Span<byte> VertexShaderConstantBuffer => new(VertexShaderConstantBufferAlloc, CONSTANT_BUFFER_MAX_SIZE);
    public Span<byte> PixelShaderConstantBuffer => new(PixelShaderConstantBufferAlloc, CONSTANT_BUFFER_MAX_SIZE);

    public LEEffect(SharpDX.Direct3D11.Device device, string levelEditorShaderCode)
    {
        // Create constant buffer
        VertexShaderGlobals = new SharpDX.Direct3D11.Buffer(device, CONSTANT_BUFFER_MAX_SIZE, ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        PixelShaderGlobals = new SharpDX.Direct3D11.Buffer(device, CONSTANT_BUFFER_MAX_SIZE, ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        VertexShaderConstants = new SharpDX.Direct3D11.Buffer(device, Utilities.SizeOf<LEVSConstants>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        PixelShaderConstants = new SharpDX.Direct3D11.Buffer(device, Utilities.SizeOf<LEPSConstants>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

        VertexShaderConstantBufferAlloc = NativeMemory.Alloc(CONSTANT_BUFFER_MAX_SIZE);
        PixelShaderConstantBufferAlloc = NativeMemory.Alloc(CONSTANT_BUFFER_MAX_SIZE);

        HitProxyConstantBuffer = new SharpDX.Direct3D11.Buffer(device, Utilities.SizeOf<LEHitProxyConstants>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        using (var psBytecode = SharpDX.D3DCompiler.ShaderBytecode.Compile(levelEditorShaderCode, "PSMainHitProxy", "ps_5_0").Bytecode)
        {
            HitProxyPixelShader = new PixelShader(device, psBytecode);
        }
        var hitBlendDesc = new BlendStateDescription
        {
            AlphaToCoverageEnable = false,
            IndependentBlendEnable = true,
        };
        //scene color: replace alpha with a marker that this pixel was rendered with game shaders
        hitBlendDesc.RenderTarget[0] = new RenderTargetBlendDescription
        {
            IsBlendEnabled = false,
            SourceBlend = BlendOption.One,
            DestinationBlend = BlendOption.Zero,
            BlendOperation = BlendOperation.Add,
            SourceAlphaBlend = BlendOption.One,
            DestinationAlphaBlend = BlendOption.Zero,
            AlphaBlendOperation = BlendOperation.Add,
            RenderTargetWriteMask = ColorWriteMaskFlags.Alpha
        };
        //hit test target: overwrite
        hitBlendDesc.RenderTarget[1] = new RenderTargetBlendDescription
        {
            IsBlendEnabled = false,
            SourceBlend = BlendOption.One,
            DestinationBlend = BlendOption.Zero,
            BlendOperation = BlendOperation.Add,
            SourceAlphaBlend = BlendOption.One,
            DestinationAlphaBlend = BlendOption.Zero,
            AlphaBlendOperation = BlendOperation.Add,
            RenderTargetWriteMask = ColorWriteMaskFlags.All
        };
        HitProxyBlendState = new BlendState(device, hitBlendDesc);
        //The same vertex shader is used, so depth will be identical. Only draw where the mesh is the frontmost surface.
        HitProxyDepthState = new DepthStencilState(device, new DepthStencilStateDescription
        {
            IsDepthEnabled = true,
            DepthWriteMask = DepthWriteMask.Zero,
            DepthComparison = Comparison.GreaterEqual, //depth is reversed, see SceneCamera.ProjectionMatrix
            IsStencilEnabled = false
        });
    }

    /// <summary>
    /// Sets the context Input Layout, Pixel Shader, Vertex Shader, and BlendState in preperation for drawing with this effect.
    /// </summary>
    public void PrepDraw(DeviceContext context, VertexShader vs, PixelShader ps, InputLayout inputLayout, BlendState blendState)
    {
        context.OutputMerger.SetBlendState(blendState);
        context.InputAssembler.InputLayout = inputLayout;
        context.VertexShader.Set(vs);
        context.VertexShader.SetConstantBuffer(0, VertexShaderGlobals);
        context.VertexShader.SetConstantBuffer(1, VertexShaderConstants);
        context.PixelShader.Set(ps);
        context.PixelShader.SetConstantBuffer(0, PixelShaderGlobals);
        context.PixelShader.SetConstantBuffer(1, VertexShaderConstants);
        context.PixelShader.SetConstantBuffer(2, PixelShaderConstants);
    }

    /// <summary>
    /// Updates view constants once per frame. These buffers belong to this effect and
    /// remain valid even when another effect temporarily changes the context's bindings.
    /// </summary>
    public void BeginFrame(MeshRenderContext context) =>
        BeginFrame(context.ImmediateContext, context.Camera.ViewMatrix, context.Camera.ProjectionMatrix, context.Camera.ViewOrigin);

    /// <summary>
    /// Updates the view constants for an arbitrary view, e.g. one that isn't of the scene camera
    /// </summary>
    /// <param name="viewOrigin">FSceneView::ViewOrigin: an eye position with W 1, or for an orthographic view the direction towards its camera with W 0</param>
    public void BeginFrame(DeviceContext deviceContext, Matrix4x4 view, Matrix4x4 projection, Vector4 viewOrigin)
    {
        ViewMatrix = view;
        ViewProjectionMatrix = ViewMatrix * projection;
        Matrix4x4.Invert(ViewMatrix, out Matrix4x4 inverseView);
        Matrix4x4.Invert(projection, out Matrix4x4 inverseProjection);
        InverseViewProjectionMatrix = inverseProjection * inverseView;
        //FSceneView translates world space to a perspective view's origin; an orthographic one's is a direction, so it isn't translated
        ViewOrigin = new Vector3(viewOrigin.X, viewOrigin.Y, viewOrigin.Z);
        PreViewTranslation = viewOrigin.W > 0 ? -ViewOrigin : Vector3.Zero;
        TranslatedViewProjectionMatrix = Matrix4x4.CreateTranslation(-PreViewTranslation) * ViewProjectionMatrix;
        //as SetViewParameters sets them: the view origin is translated too. Vertex shaders' camera vector is CameraPosition.xyz - WorldPosition * CameraPosition.w,
        //so an orthographic view's is the same everywhere
        var vertexConstants = new LEVSConstants
        {
            ViewProjectionMatrix = TranslatedViewProjectionMatrix,
            CameraPosition = new Vector4(ViewOrigin + PreViewTranslation, viewOrigin.W),
            PreViewTranslation = new Vector4(PreViewTranslation, 0)
        };
        // The projection already uses reversed depth.
        float depthMul = projection.M33;
        float depthAdd = projection.M43;
        var pixelConstants = new LEPSConstants
        {
            //from clip space to texture coordinates. (H/2 + GPixelCenterOffset) / H, where the offset is 0 for D3D11, whose pixel centers are at half texels
            ScreenPositionScaleBias = new Vector4(0.5f, -0.5f, 0.5f, 0.5f),
            MinZ_MaxZRatio = new Vector4(depthAdd, depthMul, 1f / depthAdd, depthMul / depthAdd),
            DynamicScale = Vector4.One
        };
        SharedPixelConstants = pixelConstants;
        deviceContext.UpdateSubresource(ref vertexConstants, VertexShaderConstants);
        deviceContext.UpdateSubresource(ref pixelConstants, PixelShaderConstants);
    }

    public void RenderObject(DeviceContext context, Mesh<LEVertex> mesh, int indexstart, int indexcount)
    {
        if (mesh.Vertices.Count is 0)
        {
            return;
        }
        RenderObject(context, mesh.VertexBuffer, LEVertex.Stride, mesh.IndexBuffer, Format.R32_UInt, indexstart, indexcount);
    }

    /// <summary>Draws indexed triangles from the given buffers (bound at slot 0), with the shaders set by <see cref="PrepDraw"/> and this effect's parameters</summary>
    public void RenderObject(DeviceContext context, SharpDX.Direct3D11.Buffer vertexBuffer, int stride, SharpDX.Direct3D11.Buffer indexBuffer, Format indexFormat,
        int indexstart, int indexcount)
    {
        PixelShaderResources.Apply(context.PixelShader);
        // Push per-draw data. View constants were uploaded by BeginFrame.
        //TODO: copy only the portion that is used
        context.UpdateSubresource(VertexShaderGlobals, 0, null, (IntPtr)VertexShaderConstantBufferAlloc, 0, 0);
        context.UpdateSubresource(PixelShaderGlobals, 0, null, (IntPtr)PixelShaderConstantBufferAlloc, 0, 0);

        // Setup buffers for rendering
        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertexBuffer, stride, 0));
        context.InputAssembler.SetIndexBuffer(indexBuffer, indexFormat, 0);

        // Draw!!!
        context.DrawIndexed(indexcount, indexstart, 0);
    }

    /// <summary>
    /// Draws one triangle that covers the render target, with the shaders set by <see cref="PrepDraw"/> and the pixel shader parameters in <see cref="PixelShaderConstantBuffer"/>.
    /// The vertex shader must make the triangle's vertices from SV_VertexID, since no vertex buffer is bound.
    /// </summary>
    public void RenderFullscreen(DeviceContext context)
    {
        PixelShaderResources.Apply(context.PixelShader);
        context.UpdateSubresource(PixelShaderGlobals, 0, null, (IntPtr)PixelShaderConstantBufferAlloc, 0, 0);
        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        context.Draw(3, 0);
    }

    /// <summary>
    /// Redraws the indices just drawn by <see cref="RenderObject"/>, writing to the hit test render target (and adding the selection highlight, if selected).
    /// Must be called immediately after <see cref="RenderObject"/>, as it relies on the vertex shader, input layout, and buffers it set.
    /// Leaves its own depth state set; the caller must restore theirs.
    /// </summary>
    public void RenderHitProxy(DeviceContext context, LEHitProxyConstants hitProxyConstants, int indexstart, int indexcount)
    {
        context.UpdateSubresource(ref hitProxyConstants, HitProxyConstantBuffer);
        context.PixelShader.Set(HitProxyPixelShader);
        context.PixelShader.SetConstantBuffer(HIT_PROXY_CONSTANT_BUFFER_SLOT, HitProxyConstantBuffer);
        context.OutputMerger.SetBlendState(HitProxyBlendState);
        context.OutputMerger.SetDepthStencilState(HitProxyDepthState);

        context.DrawIndexed(indexcount, indexstart, 0);
    }

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                VertexShaderGlobals.Dispose();
                VertexShaderConstants.Dispose();
                PixelShaderGlobals.Dispose();
                PixelShaderConstants.Dispose();
                HitProxyConstantBuffer.Dispose();
                HitProxyPixelShader.Dispose();
                HitProxyBlendState.Dispose();
                HitProxyDepthState.Dispose();
            }

            NativeMemory.Free(VertexShaderConstantBufferAlloc);
            NativeMemory.Free(PixelShaderConstantBufferAlloc);
            disposedValue = true;
        }
    }

    ~LEEffect()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}


[StructLayout(LayoutKind.Explicit)]
public struct LEVSConstants
{
    [FieldOffset(16 * 0)] public Matrix4x4 ViewProjectionMatrix;
    [FieldOffset(16 * 4)] public Vector4 CameraPosition;
    [FieldOffset(16 * 5)] public Vector4 PreViewTranslation;
}

[StructLayout(LayoutKind.Sequential)]
public struct LEHitProxyConstants
{
    public Vector3 HitProxyID;
    public RenderContext.ShaderFlags Flags;
}

[StructLayout(LayoutKind.Explicit)]
public struct LEPSConstants
{
    [FieldOffset(16 * 0)] public Vector4 ScreenPositionScaleBias;
    [FieldOffset(16 * 1)] public Vector4 MinZ_MaxZRatio;
    [FieldOffset(16 * 2)] public Vector4 DynamicScale;
}