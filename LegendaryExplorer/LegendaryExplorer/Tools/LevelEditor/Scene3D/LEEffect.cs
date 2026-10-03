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
        //scene color: multiply in the selection highlight, and replace alpha with a marker that this pixel was rendered with game shaders
        hitBlendDesc.RenderTarget[0] = new RenderTargetBlendDescription
        {
            IsBlendEnabled = true,
            SourceBlend = BlendOption.DestinationColor,
            DestinationBlend = BlendOption.Zero,
            BlendOperation = BlendOperation.Add,
            SourceAlphaBlend = BlendOption.One,
            DestinationAlphaBlend = BlendOption.Zero,
            AlphaBlendOperation = BlendOperation.Add,
            RenderTargetWriteMask = ColorWriteMaskFlags.All
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
            DepthComparison = Comparison.LessEqual,
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

    public void RenderObject(DeviceContext context, LEVSConstants vsSharedConstants, LEPSConstants psSharedConstants, Mesh<LEVertex> mesh, int indexstart, int indexcount)
    {
        if (mesh.Vertices.Count is 0)
        {
            return;
        }
        // Push new data into the shaders' constant buffers
        context.UpdateSubresource(ref vsSharedConstants, VertexShaderConstants);
        context.UpdateSubresource(ref psSharedConstants, PixelShaderConstants);
        //TODO: copy only the portion that is used
        context.UpdateSubresource(VertexShaderGlobals, 0, null, (IntPtr)VertexShaderConstantBufferAlloc, 0, 0);
        context.UpdateSubresource(PixelShaderGlobals, 0, null, (IntPtr)PixelShaderConstantBufferAlloc, 0, 0);

        // Setup buffers for rendering
        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(mesh.VertexBuffer, LEVertex.Stride, 0));
        context.InputAssembler.SetIndexBuffer(mesh.IndexBuffer, Format.R32_UInt, 0);

        // Draw!!!
        context.DrawIndexed(indexcount, indexstart, 0);
    }

    /// <summary>
    /// Redraws the indices just drawn by <see cref="RenderObject"/>, writing to the hit test render target (and adding the selection highlight, if selected).
    /// Must be called immediately after <see cref="RenderObject"/>, as it relies on the vertex shader, input layout, and buffers it set.
    /// </summary>
    public void RenderHitProxy(DeviceContext context, LEHitProxyConstants hitProxyConstants, int indexstart, int indexcount)
    {
        context.UpdateSubresource(ref hitProxyConstants, HitProxyConstantBuffer);
        context.PixelShader.Set(HitProxyPixelShader);
        context.PixelShader.SetConstantBuffer(HIT_PROXY_CONSTANT_BUFFER_SLOT, HitProxyConstantBuffer);
        context.OutputMerger.SetBlendState(HitProxyBlendState);
        context.OutputMerger.SetDepthStencilState(HitProxyDepthState);

        context.DrawIndexed(indexcount, indexstart, 0);

        //restore the default
        context.OutputMerger.SetDepthStencilState(null);
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