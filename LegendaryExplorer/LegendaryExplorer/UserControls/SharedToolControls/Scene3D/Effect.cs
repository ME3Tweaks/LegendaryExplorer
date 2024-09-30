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
    /// <typeparam name="TVertex"></typeparam>
    // I may have gone slightly overboard with the generics here, but hey, it's very flexible!
    public class Effect<ConstantBufferData, TVertex> : Effect<ConstantBufferData, ConstantBufferData, TVertex> where ConstantBufferData : struct where TVertex : IVertexBase, new()
    {
        private const string VERTEX_SHADER_ENTRYPOINT = "VSMain";
        private const string PIXEL_SHADER_ENTRYPOINT = "PSMain";
        public Effect(SharpDX.Direct3D11.Device device, string shaderCode) : base(device, shaderCode, PIXEL_SHADER_ENTRYPOINT, shaderCode, VERTEX_SHADER_ENTRYPOINT) { }

        public void RenderObject(DeviceContext context, ConstantBufferData constantData, Mesh<TVertex> mesh, int indexstart, int indexcount, params ShaderResourceView[] textures)
        {
            RenderObject(context, constantData, constantData, default, mesh, indexstart, indexcount, textures);
        }

        public void RenderObject(DeviceContext context, ConstantBufferData constantData, Mesh<TVertex> mesh, params ShaderResourceView[] textures)
        {
            RenderObject(context, constantData, constantData, default, mesh, textures);
        }
    }

    public class Effect<TVertCBuffer, TPixelCBuffer, TVertex> : IDisposable where TVertCBuffer : struct where TPixelCBuffer : struct where TVertex : IVertexBase, new()
    {
        private readonly VertexShader VertexShader;
        private readonly PixelShader PixelShader;
        private readonly SharpDX.Direct3D11.Buffer VertexShaderGlobals;
        private readonly SharpDX.Direct3D11.Buffer VertexShaderConstants;
        private readonly SharpDX.Direct3D11.Buffer PixelShaderGlobals;
        private readonly InputLayout InputLayout;

        public Effect(SharpDX.Direct3D11.Device device, string psCode, string psEntrypoint, string vsCode, string vsEntrypoint)
        {
            // Load vertex shader
            SharpDX.D3DCompiler.CompilationResult result = SharpDX.D3DCompiler.ShaderBytecode.Compile(vsCode, vsEntrypoint, "vs_5_0");
            SharpDX.D3DCompiler.ShaderBytecode vsb = result.Bytecode;
            VertexShader = new VertexShader(device, vsb);

            // Load pixel shader
            result = SharpDX.D3DCompiler.ShaderBytecode.Compile(psCode, psEntrypoint, "ps_5_0");
            SharpDX.D3DCompiler.ShaderBytecode psb = result.Bytecode;
            PixelShader = new PixelShader(device, psb);
            psb.Dispose();

            // Create constant buffer
            VertexShaderGlobals = new SharpDX.Direct3D11.Buffer(device, Utilities.SizeOf<TVertCBuffer>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            VertexShaderConstants = new SharpDX.Direct3D11.Buffer(device, Utilities.SizeOf<LEVSConstants>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            PixelShaderGlobals = new SharpDX.Direct3D11.Buffer(device, Utilities.SizeOf<TPixelCBuffer>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

            // Create input layout. This tells the input-assembler stage how to map items from our vertex structures into vertices for the vertex shader.
            // It is validated against the vertex shader bytecode because it needs to match properly.
            InputLayout = new InputLayout(device, vsb, TVertex.InputElements);
            vsb.Dispose();
        }

        /// <summary>
        /// Sets the context Input Layout, Pixel Shader, and Vertex Shader in preperation for drawing with this effect.
        /// </summary>
        /// <param name="context"></param>
        public void PrepDraw(DeviceContext context)
        {
            context.InputAssembler.InputLayout = InputLayout;
            context.VertexShader.Set(VertexShader);
            context.VertexShader.SetConstantBuffer(0, VertexShaderGlobals);
            context.VertexShader.SetConstantBuffer(1, VertexShaderConstants);
            context.PixelShader.Set(PixelShader);
            context.PixelShader.SetConstantBuffer(0, PixelShaderGlobals);
        }

        public void RenderObject(DeviceContext context, TVertCBuffer vsConstantData, TPixelCBuffer psConstantData, LEVSConstants vsSharedConstants, Mesh<TVertex> mesh, int indexstart, int indexcount, params ShaderResourceView[] textures)
        {
            // Push new data into the shaders' constant buffer
            context.UpdateSubresource(ref vsConstantData, VertexShaderGlobals);
            context.UpdateSubresource(ref vsSharedConstants, VertexShaderConstants);
            context.UpdateSubresource(ref psConstantData, PixelShaderGlobals);

            // Setup buffers for rendering
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(mesh.VertexBuffer, TVertex.VertexLength, 0));
            context.InputAssembler.SetIndexBuffer(mesh.IndexBuffer, Format.R32_UInt, 0);

            // Set the textures
            for (int i = 0; i < textures.Length; i++)
            {
                context.PixelShader.SetShaderResource(i, textures[i]);
            }

            // Draw!!!
            context.DrawIndexed(indexcount, indexstart, 0);
        }

        public void RenderObject(DeviceContext context, TVertCBuffer vsConstantData, TPixelCBuffer psConstantData, LEVSConstants vsSharedConstants, Mesh<TVertex> mesh, params ShaderResourceView[] textures)
        {
            RenderObject(context, vsConstantData, psConstantData, vsSharedConstants, mesh, 0, mesh.Triangles.Count * 3, textures);
        }

        public void Dispose()
        {
            VertexShader.Dispose();
            PixelShader.Dispose();
            InputLayout.Dispose();
            VertexShaderGlobals.Dispose();
            VertexShaderConstants.Dispose();
            PixelShaderGlobals.Dispose();
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct LEVSConstants
    {
        [FieldOffset(16 * 0)] public float4x4 ViewProjectionMatrix;
        [FieldOffset(16 * 4)] public float4 CameraPosition;
        [FieldOffset(16 * 5)] public float4 PreViewTranslation;
    }
}
