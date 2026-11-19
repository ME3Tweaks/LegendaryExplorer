using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using SharpDX.Direct3D11;
using Device = SharpDX.Direct3D11.Device;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// The level editor's light sprites, one per type of light. They're drawn white with a dark outline, so that the light's color can tint them.
/// Created on the render thread when first needed, since the device is single-threaded
/// </summary>
public sealed class LightSprites : IDisposable
{
    private const int SpriteSize = 64;
    private readonly Dictionary<SceneLightType, (Texture2D Texture, ShaderResourceView View)> Textures = [];

    public ShaderResourceView Get(Device device, SceneLightType type)
    {
        if (device is null) return null;
        if (!Textures.TryGetValue(type, out var sprite))
        {
            byte[] pixels = Draw(type);
            var description = new Texture2DDescription
            {
                Width = SpriteSize,
                Height = SpriteSize,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable,
                BindFlags = BindFlags.ShaderResource
            };
            Texture2D texture;
            unsafe
            {
                fixed (byte* p = pixels)
                {
                    texture = new Texture2D(device, description, new SharpDX.DataRectangle((nint)p, SpriteSize * 4));
                }
            }
            Textures[type] = sprite = (texture, new ShaderResourceView(device, texture));
        }
        return sprite.View;
    }

    /// <summary>
    /// Renders the sprite with WPF, as premultiplied BGRA
    /// </summary>
    private static byte[] Draw(SceneLightType type)
    {
        var fill = Brushes.White;
        var outline = new Pen(new SolidColorBrush(Color.FromRgb(20, 20, 20)), 4) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var thickLine = new Pen(fill, 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            var center = new Point(SpriteSize / 2.0, SpriteSize / 2.0);
            switch (type)
            {
                case SceneLightType.Point:
                {
                    //a light bulb
                    var bulb = new EllipseGeometry(new Point(32, 26), 17, 17);
                    var neck = new RectangleGeometry(new Rect(23, 38, 18, 16), 3, 3);
                    var shape = new CombinedGeometry(GeometryCombineMode.Union, bulb, neck);
                    dc.DrawGeometry(fill, outline, shape);
                    dc.DrawLine(outline, new Point(24, 46), new Point(40, 46));
                    dc.DrawLine(outline, new Point(26, 51), new Point(38, 51));
                    break;
                }
                case SceneLightType.Spot:
                {
                    //a lamp head and its beam
                    var beam = new StreamGeometry();
                    using (StreamGeometryContext g = beam.Open())
                    {
                        g.BeginFigure(new Point(22, 30), true, true);
                        g.LineTo(new Point(42, 30), true, false);
                        g.LineTo(new Point(58, 60), true, false);
                        g.LineTo(new Point(6, 60), true, false);
                    }
                    dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), outline, beam);
                    var head = new RectangleGeometry(new Rect(18, 6, 28, 26), 6, 6);
                    dc.DrawGeometry(fill, outline, head);
                    break;
                }
                case SceneLightType.Directional:
                {
                    //a sun
                    for (int i = 0; i < 8; i++)
                    {
                        double angle = i * Math.PI / 4;
                        var inner = new Point(center.X + Math.Cos(angle) * 20, center.Y + Math.Sin(angle) * 20);
                        var outer = new Point(center.X + Math.Cos(angle) * 29, center.Y + Math.Sin(angle) * 29);
                        dc.DrawLine(new Pen(outline.Brush, 9) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, inner, outer);
                        dc.DrawLine(thickLine, inner, outer);
                    }
                    dc.DrawEllipse(fill, outline, center, 14, 14);
                    break;
                }
                case SceneLightType.Sky:
                {
                    //a dome over the horizon
                    var dome = new StreamGeometry();
                    using (StreamGeometryContext g = dome.Open())
                    {
                        g.BeginFigure(new Point(6, 46), true, true);
                        g.ArcTo(new Point(58, 46), new Size(26, 26), 0, false, SweepDirection.Clockwise, true, false);
                    }
                    dc.DrawGeometry(fill, outline, dome);
                    dc.DrawLine(new Pen(outline.Brush, 6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, new Point(4, 54), new Point(60, 54));
                    break;
                }
            }
        }
        var bitmap = new RenderTargetBitmap(SpriteSize, SpriteSize, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var pixels = new byte[SpriteSize * SpriteSize * 4];
        bitmap.CopyPixels(pixels, SpriteSize * 4, 0);
        return pixels;
    }

    public void Dispose()
    {
        foreach ((Texture2D texture, ShaderResourceView view) in Textures.Values)
        {
            view.Dispose();
            texture.Dispose();
        }
        Textures.Clear();
    }
}
