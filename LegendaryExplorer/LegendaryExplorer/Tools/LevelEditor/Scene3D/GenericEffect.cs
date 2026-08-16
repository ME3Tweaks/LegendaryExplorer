using System;
using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D11;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// An Effect contains a Pixel Shader, Vertex Shader, Constant Buffer, and and Input Layout for rendering objects with Direct3D.
/// </summary>
/// <typeparam name="ConstantBufferData">The structure that will hold the data in the only constant buffer.</typeparam>
// I may have gone slightly overboard with the generics here, but hey, it's very flexible!
public sealed class GenericEffect<ConstantBufferData> : IDisposable where ConstantBufferData : struct
{
    private const string VERTEX_SHADER_ENTRYPOINT = "VSMain";
    private const string PIXEL_SHADER_ENTRYPOINT = "PSMain";
    public VertexShader VertexShader { get; }
    public PixelShader PixelShader { get; }
    public Buffer ConstantBuffer { get; }
    public InputLayout InputLayout { get; }

    public GenericEffect(SharpDX.Direct3D11.Device device, string shaderCode)
    {
        // Load vertex shader
        SharpDX.D3DCompiler.CompilationResult result = SharpDX.D3DCompiler.ShaderBytecode.Compile(shaderCode, VERTEX_SHADER_ENTRYPOINT, "vs_5_0");
        SharpDX.D3DCompiler.ShaderBytecode vsb = result.Bytecode;
        VertexShader = new VertexShader(device, vsb);

        // Load pixel shader
        result = SharpDX.D3DCompiler.ShaderBytecode.Compile(shaderCode, PIXEL_SHADER_ENTRYPOINT, "ps_5_0");
        SharpDX.D3DCompiler.ShaderBytecode psb = result.Bytecode;
        PixelShader = new PixelShader(device, psb);
        psb.Dispose();

        // Create constant buffer
        ConstantBuffer = new SharpDX.Direct3D11.Buffer(device, Utilities.SizeOf<ConstantBufferData>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

        // Create input layout. This tells the input-assembler stage how to map items from our vertex structures into vertices for the vertex shader.
        // It is validated against the vertex shader bytecode because it needs to match properly.
        InputLayout = new InputLayout(device, vsb, WorldVertex.InputElements);
        vsb.Dispose();
    }

    /// <summary>
    /// Sets the context Input Layout, Pixel Shader, and Vertex Shader in preperation for drawing with this effect.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="blendState"></param>
    public void PrepDraw(DeviceContext context, BlendState blendState, ConstantBufferData constantData)
    {
        context.OutputMerger.SetBlendState(blendState);
        context.InputAssembler.InputLayout = InputLayout;
        context.VertexShader.Set(VertexShader);
        context.VertexShader.SetConstantBuffer(0, ConstantBuffer);
        context.PixelShader.Set(PixelShader);
        context.PixelShader.SetConstantBuffer(0, ConstantBuffer);

        // Push new data into the shaders' constant buffer
        context.UpdateSubresource(ref constantData, ConstantBuffer);
    }

    public void RenderObject(DeviceContext context, Mesh<WorldVertex> mesh, int indexstart, int indexcount, params ReadOnlySpan<ShaderResourceView> textures)
    {
        if (mesh.Vertices.Count is 0)
        {
            return;
        }

        // Setup buffers for rendering
        context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(mesh.VertexBuffer, WorldVertex.Stride, 0));
        context.InputAssembler.SetIndexBuffer(mesh.IndexBuffer, Format.R32_UInt, 0);
        context.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;

        // Set the textures
        for (int i = 0; i < textures.Length; i++)
        {
            context.PixelShader.SetShaderResource(i, textures[i]);
        }

        // Draw!!!
        context.DrawIndexed(indexcount, indexstart, 0);
    }

    public void RenderObject(DeviceContext context, Mesh<WorldVertex> mesh, params ReadOnlySpan<ShaderResourceView> textures)
    {
        RenderObject(context, mesh, 0, mesh.Triangles.Count * 3, textures);
    }



    private Buffer DynamicVertexBuffer;
    private Buffer DynamicIndexBuffer;

    public unsafe void PrepPrimitiveBuffers(RenderContext context, Span<WorldVertex> lineVerts, Span<WorldVertex> meshVerts, Span<int> meshIndices)
    {
        if (lineVerts.Length > 0 || meshVerts.Length > 0)
        {
            int vertBufferLength = (lineVerts.Length + meshVerts.Length) * WorldVertex.Stride;
            int floatsPerVertex = WorldVertex.Stride / 4;

            if (DynamicVertexBuffer is null || DynamicVertexBuffer.Description.SizeInBytes < vertBufferLength)
            {
                DynamicVertexBuffer?.Dispose();
                DynamicVertexBuffer = new Buffer(context.Device, new BufferDescription(vertBufferLength, ResourceUsage.Dynamic, BindFlags.VertexBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, WorldVertex.Stride));
            }

            var vertBuffer = new Span<float>((void*)context.ImmediateContext.MapSubresource(DynamicVertexBuffer, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None).DataPointer, vertBufferLength / 4);
            int floatIdx = 0;
            for (int vertIdx = 0; vertIdx < lineVerts.Length; vertIdx++, floatIdx += floatsPerVertex)
            {
                lineVerts[vertIdx].ToFloats(vertBuffer[floatIdx..]);
            }
            for (int vertIdx = 0; vertIdx < meshVerts.Length; vertIdx++, floatIdx += floatsPerVertex)
            {
                meshVerts[vertIdx].ToFloats(vertBuffer[floatIdx..]);
            }

            context.ImmediateContext.UnmapSubresource(DynamicVertexBuffer, 0);
            context.ImmediateContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(DynamicVertexBuffer, WorldVertex.Stride, 0));
        }

        if (meshIndices.Length > 0)
        {
            int indexBufferLength = meshIndices.Length * 4;

            if (DynamicIndexBuffer is null || DynamicIndexBuffer.Description.SizeInBytes < indexBufferLength)
            {
                DynamicIndexBuffer?.Dispose();
                DynamicIndexBuffer = new Buffer(context.Device, new BufferDescription(indexBufferLength, ResourceUsage.Dynamic, BindFlags.IndexBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, 4));
            }

            var indexBuffer = new Span<int>((void*)context.ImmediateContext.MapSubresource(DynamicIndexBuffer, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None).DataPointer, indexBufferLength / 4);

            meshIndices.CopyTo(indexBuffer);

            context.ImmediateContext.UnmapSubresource(DynamicIndexBuffer, 0);
            context.ImmediateContext.InputAssembler.SetIndexBuffer(DynamicIndexBuffer, Format.R32_UInt, 0);
        }
    }

    public void RenderPrimitives(RenderContext context, SharpDX.Direct3D.PrimitiveTopology topology, int vertOffset, int vertCount)
    {
        context.ImmediateContext.InputAssembler.PrimitiveTopology = topology;
        context.ImmediateContext.Draw(vertCount, vertOffset);
    }

    public void RenderPrimitives(RenderContext context, SharpDX.Direct3D.PrimitiveTopology topology, int indexStart, int indexCount, int vertOffset)
    {
        context.ImmediateContext.InputAssembler.PrimitiveTopology = topology;
        context.ImmediateContext.DrawIndexed(indexCount, indexStart, vertOffset);
    }

    public void Dispose()
    {
        VertexShader.Dispose();
        PixelShader.Dispose();
        InputLayout.Dispose();
        ConstantBuffer.Dispose();
        DynamicVertexBuffer?.Dispose();
        DynamicIndexBuffer?.Dispose();
    }
}
