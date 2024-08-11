using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorer.UserControls.SharedToolControls.Scene3D;
using SharpDX.Direct3D11;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.TextureViewer
{
    public class TextureRenderContext : RenderContext
    {
        [Flags]
        public enum TextureViewFlags : int
        {
            None = 0,
            /// <summary>
            /// If normals should have the third color channel populated
            /// </summary>
            ReconstructNormalZ = 1 << 0,
            /// <summary>
            /// If alpha channel should be set to black, so textures can properly be viewed
            /// </summary>
            AlphaAsBlack = 1 << 1,
            EnableRedChannel = 1 << 2,
            EnableGreenChannel = 1 << 3,
            EnableBlueChannel = 1 << 4,
            EnableAlphaChannel = 1 << 5,
        }

        // WARNING: Constant buffers must be a multiple of 16 bytes long
        public struct TextureViewConstants
        {
            public Matrix4x4 Projection;
            public Matrix4x4 View;
            public int Mip;
            public TextureViewFlags Flags;
            public int TextureWidth;
            public int TextureHeight;
        }

        private RenderTargetView BackbufferRTV = null;
        private SamplerState TextureSampler = null;
        private VertexShader TextureVertexShader = null;
        private PixelShader TexturePixelShader = null;
        private ShaderResourceView TextureRTV = null;
        public TextureViewConstants Constants = new TextureViewConstants() { Flags = TextureViewFlags.EnableRedChannel | TextureViewFlags.EnableGreenChannel | TextureViewFlags.EnableBlueChannel | TextureViewFlags.EnableAlphaChannel };
        private SharpDX.Direct3D11.Buffer ConstantBuffer = null;

        private SharpDX.Direct3D11.Texture2D _texture = null;
        public SharpDX.Direct3D11.Texture2D Texture
        {
            get => this._texture;
            set
            {
                if (this.TextureRTV != null)
                {
                    this.TextureRTV.Dispose();
                    this.TextureRTV = null;
                }
                this._texture = value;
                if (this.Texture != null)
                {
                    this.TextureRTV = new ShaderResourceView(this.Device, this.Texture);
                    this.Constants.TextureWidth = value.Description.Width;
                    this.Constants.TextureHeight = value.Description.Height;
                }
            }
        }
        public float ScaleFactor { get; set; } = -1.0f; // -1 means scale to fit
        public Vector2 CameraCenter { get; set; } = Vector2.Zero;
        public int CurrentMip // NOTE: The texture export loader passes each mip as its own texture, meaning that we always want mip 0 of the given texture.
        {
            get => this.Constants.Mip;
            set => this.Constants.Mip = value;
        }
        public Vector4 BackgroundColor { get; set; } = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);

        public override void CreateResources()
        {
            base.CreateResources();
            this.TextureSampler = new SamplerState(this.Device, new SamplerStateDescription() { AddressU = TextureAddressMode.Wrap, AddressV = TextureAddressMode.Wrap, AddressW = TextureAddressMode.Wrap, Filter = Filter.MinLinearMagMipPoint, MaximumLod = Single.MaxValue, MinimumLod = 0, MipLodBias = 0 });
            this.ConstantBuffer = new SharpDX.Direct3D11.Buffer(this.Device, SharpDX.Utilities.SizeOf<TextureViewConstants>(), ResourceUsage.Dynamic, BindFlags.ConstantBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, 0);

            // Load shaders
            string textureShader = LegendaryExplorer.Resources.EmbeddedResources.TextureShader;
            SharpDX.D3DCompiler.CompilationResult result = SharpDX.D3DCompiler.ShaderBytecode.Compile(textureShader, "VSMain", "vs_5_0");
            SharpDX.D3DCompiler.ShaderBytecode vsbytecode = result.Bytecode;
            this.TextureVertexShader = new VertexShader(Device, vsbytecode);
            vsbytecode.Dispose();

            // Load pixel shader
            result = SharpDX.D3DCompiler.ShaderBytecode.Compile(textureShader, "PSMain", "ps_5_0");
            SharpDX.D3DCompiler.ShaderBytecode psbytecode = result.Bytecode;
            this.TexturePixelShader = new PixelShader(Device, psbytecode);
            psbytecode.Dispose();

            // Set render state (this is a pretty simple D3D component so we can set it once and forget it)
            this.ImmediateContext.PixelShader.SetSampler(0, this.TextureSampler);
            this.ImmediateContext.PixelShader.SetShader(this.TexturePixelShader, null, 0);
            this.ImmediateContext.VertexShader.SetShader(this.TextureVertexShader, null, 0);
            this.ImmediateContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleStrip;
            this.ImmediateContext.OutputMerger.SetBlendState(this.AlphaBlendState);
        }

        public override void CreateSizeDependentResources(int width, int height, SharpDX.Direct3D11.Texture2D newBackbuffer)
        {
            base.CreateSizeDependentResources(width, height, newBackbuffer);
            this.BackbufferRTV = new RenderTargetView(this.Device, this.Backbuffer);
            this.ImmediateContext.Rasterizer.SetViewport(new SharpDX.Mathematics.Interop.RawViewportF() { X = 0, Y = 0, Width = width, Height = height, MinDepth = 0.0f, MaxDepth = 1.0f });
        }

        public override void Render()
        {
            float textureRatio = (float)this.Constants.TextureWidth / this.Constants.TextureHeight;
            float viewportRatio = (float)this.Width / this.Height;
            if (textureRatio > viewportRatio) // When the texture's aspect ratio is greater than the viewport, that means that the texture width is the limiting factor and so the image should be scaled to fit horizontally
            {
                this.Constants.Projection = Matrix4x4.CreateOrthographic(1.0f, 1.0f / viewportRatio, -1.0f, 1.0f);
            }
            else
            {
                this.Constants.Projection = Matrix4x4.CreateOrthographic(viewportRatio, 1.0f, -1.0f, 1.0f);
            }

            this.ImmediateContext.OutputMerger.SetRenderTargets(this.BackbufferRTV);
            this.ImmediateContext.ClearRenderTargetView(this.BackbufferRTV, new SharpDX.Mathematics.Interop.RawColor4(this.BackgroundColor.X, this.BackgroundColor.Y, this.BackgroundColor.Z, this.BackgroundColor.W));

            float scale = 1.0f;
            if (this.ScaleFactor > 0.0f)
            {
                float smallSize = this.Width <= this.Height ? this.Width : this.Height;
                scale = this.Texture.Description.Height / smallSize * this.ScaleFactor;
            }
            else
            {
                if (textureRatio > viewportRatio)
                {
                    scale = 1.0f / textureRatio;
                }
                // else leave scale at 1.0
            }

            this.Constants.View = Matrix4x4.CreateTranslation(-this.CameraCenter.X, -this.CameraCenter.Y, 0.0f) * Matrix4x4.CreateScale(scale);
            SharpDX.DataBox constantBox = this.ImmediateContext.MapSubresource(this.ConstantBuffer, 0, MapMode.WriteDiscard, MapFlags.None);
            System.Runtime.InteropServices.Marshal.StructureToPtr(this.Constants, constantBox.DataPointer, false);
            this.ImmediateContext.UnmapSubresource(this.ConstantBuffer, 0);
            this.ImmediateContext.PixelShader.SetShaderResource(0, this.TextureRTV);
            this.ImmediateContext.VertexShader.SetConstantBuffer(0, this.ConstantBuffer);
            this.ImmediateContext.PixelShader.SetConstantBuffer(0, this.ConstantBuffer);
            this.ImmediateContext.Draw(4, 0);

            base.Render();
        }

        public override void Update(float timestep)
        {
            // Nothing to do here
        }

        public override void DisposeSizeDependentResources()
        {
            this.BackbufferRTV.Dispose();
            base.DisposeSizeDependentResources();
        }

        public override void DisposeResources()
        {
            this.ConstantBuffer.Dispose();
            this.TextureSampler.Dispose();
            base.DisposeResources();
        }
    }
}
