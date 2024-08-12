using System;
using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D11;

namespace LegendaryExplorer.UserControls.SharedToolControls.Scene3D
{
    /// <summary>
    /// An Effect contains a Pixel Shader, Vertex Shader, Constant Buffer, and and Input Layout for rendering objects with Direct3D.
    /// </summary>
    /// <typeparam name="ConstantBufferData">The structure that will hold the data in the only constant buffer.</typeparam>
    // I may have gone slightly overboard with the generics here, but hey, it's very flexible!
    public class Effect<ConstantBufferData, Vertex> : IDisposable where ConstantBufferData : struct where Vertex : VertexBase, new()
    {
        private const string VertexShaderEntrypoint = "VSMain";
        private const string PixelShaderEntrypoint = "PSMain";
        public VertexShader VertexShader { get; }
        public PixelShader PixelShader { get; }
        public SharpDX.Direct3D11.Buffer ConstantBuffer { get; }
        public InputLayout InputLayout { get; }

        public Effect(SharpDX.Direct3D11.Device Device, string ShaderCode)
        {
            // Load vertex shader
            SharpDX.D3DCompiler.CompilationResult result = SharpDX.D3DCompiler.ShaderBytecode.Compile(ShaderCode, VertexShaderEntrypoint, "vs_5_0");
            SharpDX.D3DCompiler.ShaderBytecode vsb = result.Bytecode;
            VertexShader = new VertexShader(Device, vsb);

            // Load pixel shader
            result = SharpDX.D3DCompiler.ShaderBytecode.Compile(ShaderCode, PixelShaderEntrypoint, "ps_5_0");
            SharpDX.D3DCompiler.ShaderBytecode psb = result.Bytecode;
            PixelShader = new PixelShader(Device, psb);
            psb.Dispose();

            // Create constant buffer
            ConstantBuffer = new SharpDX.Direct3D11.Buffer(Device, Utilities.SizeOf<ConstantBufferData>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

            // Create input layout. This tells the input-assembler stage how to map items from our vertex structures into vertices for the vertex shader.
            // It is validated against the vertex shader bytecode because it needs to match properly.
            InputLayout = new InputLayout(Device, vsb, new Vertex().InputElements);
            vsb.Dispose();
        }

        /// <summary>
        /// Sets the context Input Layout, Pixel Shader, and Vertex Shader in preperation for drawing with this effect.
        /// </summary>
        /// <param name="Context"></param>
        public void PrepDraw(DeviceContext Context)
        {
            Context.InputAssembler.InputLayout = InputLayout;
            Context.VertexShader.Set(VertexShader);
            Context.VertexShader.SetConstantBuffer(0, ConstantBuffer);
            Context.PixelShader.Set(PixelShader);
            Context.PixelShader.SetConstantBuffer(0, ConstantBuffer);
        }

        public void RenderObject(DeviceContext Context, ConstantBufferData ConstantData, Mesh<Vertex> Mesh, int indexstart, int indexcount, params ShaderResourceView[] Textures)
        {
            // Push new data into the shaders' constant buffer
            Context.UpdateSubresource(ref ConstantData, ConstantBuffer);

            // Setup buffers for rendering
            Context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(Mesh.VertexBuffer, new Vertex().VertexLength, 0));
            Context.InputAssembler.SetIndexBuffer(Mesh.IndexBuffer, Format.R32_UInt, 0);

            // Set the textures
            for (int i = 0; i < Textures.Length; i++)
            {
                Context.PixelShader.SetShaderResource(i, Textures[i]);
            }

            // Draw!!!
            Context.DrawIndexed(indexcount, indexstart, 0);
        }

        public void RenderObject(DeviceContext context, ConstantBufferData ConstantData, Mesh<Vertex> Mesh, params ShaderResourceView[] Textures)
        {
            RenderObject(context, ConstantData, Mesh, 0, Mesh.Triangles.Count * 3, Textures);
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
