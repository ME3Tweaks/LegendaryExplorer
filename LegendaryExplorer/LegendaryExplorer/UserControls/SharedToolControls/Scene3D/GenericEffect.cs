using System;
using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D11;
using System.Runtime.InteropServices;
using Matrix3x3 = SharpDX.Matrix3x3;
using float3x3 = SharpDX.Matrix3x3;
using float4x4 = System.Numerics.Matrix4x4;
using float2 = System.Numerics.Vector2;
using float3 = System.Numerics.Vector3;
using float4 = System.Numerics.Vector4;

namespace LegendaryExplorer.UserControls.SharedToolControls.Scene3D
{

    /// <summary>
    /// An Effect contains a Pixel Shader, Vertex Shader, Constant Buffer, and and Input Layout for rendering objects with Direct3D.
    /// </summary>
    /// <typeparam name="ConstantBufferData">The structure that will hold the data in the only constant buffer.</typeparam>
    /// <typeparam name="Vertex"></typeparam>
    // I may have gone slightly overboard with the generics here, but hey, it's very flexible!
    public class GenericEffect<ConstantBufferData, Vertex> : IDisposable where ConstantBufferData : struct where Vertex : IVertexBase
    {
        private const string VERTEX_SHADER_ENTRYPOINT = "VSMain";
        private const string PIXEL_SHADER_ENTRYPOINT = "PSMain";
        public VertexShader VertexShader { get; }
        public PixelShader PixelShader { get; }
        public SharpDX.Direct3D11.Buffer ConstantBuffer { get; }
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
            InputLayout = new InputLayout(device, vsb, Vertex.InputElements);
            vsb.Dispose();
        }

        /// <summary>
        /// Sets the context Input Layout, Pixel Shader, and Vertex Shader in preperation for drawing with this effect.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="blendState"></param>
        public void PrepDraw(DeviceContext context, BlendState blendState)
        {
            context.OutputMerger.SetBlendState(blendState);
            context.InputAssembler.InputLayout = InputLayout;
            context.VertexShader.Set(VertexShader);
            context.VertexShader.SetConstantBuffer(0, ConstantBuffer);
            context.PixelShader.Set(PixelShader);
            context.PixelShader.SetConstantBuffer(0, ConstantBuffer);
        }

        public void RenderObject(DeviceContext context, ConstantBufferData constantData, Mesh<Vertex> mesh, int indexstart, int indexcount, params ShaderResourceView[] textures)
        {
            // Push new data into the shaders' constant buffer
            context.UpdateSubresource(ref constantData, ConstantBuffer);

            // Setup buffers for rendering
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(mesh.VertexBuffer, Vertex.Stride, 0));
            context.InputAssembler.SetIndexBuffer(mesh.IndexBuffer, Format.R32_UInt, 0);

            // Set the textures
            for (int i = 0; i < textures.Length; i++)
            {
                context.PixelShader.SetShaderResource(i, textures[i]);
            }

            // Draw!!!
            context.DrawIndexed(indexcount, indexstart, 0);
        }

        public void RenderObject(DeviceContext context, ConstantBufferData constantData, Mesh<Vertex> mesh, params ShaderResourceView[] textures)
        {
            RenderObject(context, constantData, mesh, 0, mesh.Triangles.Count * 3, textures);
        }

        public void Dispose()
        {
            VertexShader.Dispose();
            PixelShader.Dispose();
            InputLayout.Dispose();
            ConstantBuffer.Dispose();
        }
    }
}
