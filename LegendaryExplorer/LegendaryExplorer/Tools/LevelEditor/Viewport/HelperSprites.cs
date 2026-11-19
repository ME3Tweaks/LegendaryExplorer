using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using Device = SharpDX.Direct3D11.Device;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>One shared diamond sprite for batched position helpers. Opaque centers carry the actor's hit ID.</summary>
public sealed class HelperSprites : IDisposable
{
    private Texture2D texture;
    private ShaderResourceView view;

    public ShaderResourceView Get(Device device)
    {
        if (device is null || view is not null) return view;
        const int size = 32;
        byte[] pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = Math.Abs(x - 15.5f) + Math.Abs(y - 15.5f);
            if (distance > 15) continue;
            int i = (y * size + x) * 4;
            byte value = distance > 12 ? (byte)25 : (byte)255;
            pixels[i] = pixels[i + 1] = pixels[i + 2] = value;
            pixels[i + 3] = 255;
        }
        unsafe
        {
            fixed (byte* p = pixels)
                texture = new Texture2D(device, new Texture2DDescription
                {
                    Width = size, Height = size, MipLevels = 1, ArraySize = 1,
                    Format = Format.B8G8R8A8_UNorm, SampleDescription = new(1, 0),
                    Usage = ResourceUsage.Immutable, BindFlags = BindFlags.ShaderResource
                }, new SharpDX.DataRectangle((nint)p, size * 4));
        }
        return view = new ShaderResourceView(device, texture);
    }

    public void Dispose()
    {
        view?.Dispose();
        texture?.Dispose();
        view = null;
        texture = null;
    }
}
