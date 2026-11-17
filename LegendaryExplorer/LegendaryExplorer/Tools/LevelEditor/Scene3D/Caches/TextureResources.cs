using System;
using System.IO;
using System.Linq;
using System.Threading;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Texture2D = SharpDX.Direct3D11.Texture2D;
using Device = SharpDX.Direct3D11.Device;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>Owns a texture and both color-space views, including partial-creation failure cleanup.</summary>
internal sealed class TextureResources : IDisposable
{
    public Texture2D Texture { get; private set; }
    public ShaderResourceView View { get; private set; }
    public ShaderResourceView LinearView { get; private set; }

    public static TextureResources Create(Device device, MeshRenderContext.LoadedTexture loaded, bool cube)
    {
        var result = new TextureResources { Texture = loaded.Texture };
        try
        {
            var desc = new ShaderResourceViewDescription { Format = loaded.ViewFormat,
                Dimension = cube ? ShaderResourceViewDimension.TextureCube : ShaderResourceViewDimension.Texture2D };
            if (cube) desc.TextureCube.MipLevels = -1;
            else desc.Texture2D.MipLevels = -1;
            result.View = new ShaderResourceView(device, loaded.Texture, desc);
            result.LinearView = result.View;
            if (loaded.SRGBViewFormat is Format srgb)
            {
                desc.Format = srgb;
                result.LinearView = new ShaderResourceView(device, loaded.Texture, desc);
            }
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    public void Dispose()
    {
        if (LinearView != View) LinearView?.Dispose();
        LinearView = null;
        View?.Dispose();
        View = null;
        Texture?.Dispose();
        Texture = null;
    }
}
