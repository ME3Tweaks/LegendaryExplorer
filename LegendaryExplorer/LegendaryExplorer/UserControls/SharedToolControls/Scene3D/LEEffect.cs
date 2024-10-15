using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Documents;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Vector4 = System.Numerics.Vector4;

namespace LegendaryExplorer.UserControls.SharedToolControls.Scene3D;

public unsafe class LEEffect : IDisposable
{
    private readonly SharpDX.Direct3D11.Buffer VertexShaderGlobals;
    private readonly SharpDX.Direct3D11.Buffer VertexShaderConstants;
    private readonly SharpDX.Direct3D11.Buffer PixelShaderGlobals;
    private readonly SharpDX.Direct3D11.Buffer PixelShaderConstants;

    private readonly void* VertexShaderConstantBufferAlloc;
    private readonly void* PixelShaderConstantBufferAlloc;

    private bool disposedValue;

    public const int CONSTANT_BUFFER_MAX_SIZE = 2560;

    public Span<byte> VertexShaderConstantBuffer => new(VertexShaderConstantBufferAlloc, CONSTANT_BUFFER_MAX_SIZE);
    public Span<byte> PixelShaderConstantBuffer => new(PixelShaderConstantBufferAlloc, CONSTANT_BUFFER_MAX_SIZE);

    public LEEffect(SharpDX.Direct3D11.Device device)
    {
        // Create constant buffer
        VertexShaderGlobals = new SharpDX.Direct3D11.Buffer(device, CONSTANT_BUFFER_MAX_SIZE, ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        PixelShaderGlobals = new SharpDX.Direct3D11.Buffer(device, CONSTANT_BUFFER_MAX_SIZE, ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        VertexShaderConstants = new SharpDX.Direct3D11.Buffer(device, Utilities.SizeOf<LEVSConstants>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        PixelShaderConstants = new SharpDX.Direct3D11.Buffer(device, Utilities.SizeOf<LEPSConstants>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

        VertexShaderConstantBufferAlloc = NativeMemory.Alloc(CONSTANT_BUFFER_MAX_SIZE);
        PixelShaderConstantBufferAlloc = NativeMemory.Alloc(CONSTANT_BUFFER_MAX_SIZE);
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
        // Push new data into the shaders' constant buffers
        context.UpdateSubresource(ref vsSharedConstants, VertexShaderConstants);
        context.UpdateSubresource(ref psSharedConstants, PixelShaderConstants);
        //TODO: copy only the portion that is used
        context.UpdateSubresource(VertexShaderGlobals, 0, null, (IntPtr)VertexShaderConstantBufferAlloc, 0, 0);
        context.UpdateSubresource(PixelShaderGlobals, 0, null, (IntPtr)PixelShaderConstantBufferAlloc, 0, 0);

        // Setup buffers for rendering
        context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(mesh.VertexBuffer, LEVertex.Stride, 0));
        context.InputAssembler.SetIndexBuffer(mesh.IndexBuffer, Format.R32_UInt, 0);

        // Draw!!!
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

[StructLayout(LayoutKind.Explicit)]
public struct LEPSConstants
{
    [FieldOffset(16 * 0)] public Vector4 ScreenPositionScaleBias;
    [FieldOffset(16 * 1)] public Vector4 MinZ_MaxZRatio;
    [FieldOffset(16 * 2)] public Vector4 DynamicScale;
}