using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.SharpDX;
using LegendaryExplorerCore.Unreal.Collections;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LegendaryExplorer.Tools.LevelEditor;

public class LevelEditorRenderContext : MeshRenderContext
{
    public event Action<ActorProxy> SelectActor;
    public List<ActorProxy> DrawList_3D = [];
    public List<UIElement> DrawList_2D = [];

    private USparseArray<IHitProxy> HitProxies = [];

    public readonly Widget TransformWidget;

    public readonly BatchedPrimitives Primitives = new();

    public LevelEditorRenderContext() : base()
    {
        BackgroundColor = System.Windows.Media.Color.FromRgb(0x99, 0x99, 0x99);
        Camera.FirstPerson = true;
        TransformWidget = new Widget();
    }

    const int HitTestSize = 3;

    public override bool MouseUp(MouseButtons button, int x, int y)
    {
        if (TransformWidget.IsDragging)
        {
            TransformWidget.EndDrag();
            return true;
        }
        if (base.MouseUp(button, x, y)) return true;

        if (HitBufferView is not null)
        {
            IHitProxy selected = GetHitProxy(x, y);

            TransformWidget.CurrentAxis = EWidgetAxis.None;
            switch (selected)
            {
                case ActorProxy actor:
                    SelectActor?.Invoke(actor);
                    TransformWidget.Attach = actor;
                    break;
                case AxisHitProxy axisProxy:
                    TransformWidget.CurrentAxis = axisProxy.Axis;
                    break;
            }
        }

        return false;
    }

    public override bool MouseDown(MouseButtons button, int x, int y)
    {
        if (TransformWidget.IsDragging)
        {
            //failsafe if mouseup event was not captured
            if (Mouse.LeftButton is MouseButtonState.Released)
            {
                TransformWidget.EndDrag();
            }
        }
        if (HitBufferView is not null)
        {
            IHitProxy selected = GetHitProxy(x, y);

            TransformWidget.CurrentAxis = EWidgetAxis.None;
            switch (selected)
            {
                case AxisHitProxy axisProxy:
                    TransformWidget.CurrentAxis = axisProxy.Axis;
                    TransformWidget.BeginDrag(x, y);
                    return true;
            }
        }
        return base.MouseDown(button, x, y);
    }

    public override bool MouseMove(int x, int y)
    {
        if (TransformWidget.IsDragging)
        {
            //failsafe if mouseup event was not captured
            if (Mouse.LeftButton is MouseButtonState.Released)
            {
                TransformWidget.EndDrag();
                return true;
            }
            TransformWidget.Drag(this, x, y);
            return true;
        }
        if (base.MouseMove(x, y)) return true;

        if (HitBufferView is not null)
        {
            IHitProxy selected = GetHitProxy(x, y);

            TransformWidget.CurrentAxis = EWidgetAxis.None;
            switch (selected)
            {
                case AxisHitProxy axisProxy:
                    TransformWidget.CurrentAxis = axisProxy.Axis;
                    break;
            }
        }
        return false;
    }

    private IHitProxy GetHitProxy(int x, int y)
    {
        int minX = Math.Max(x - HitTestSize, 0);
        int maxX = Math.Min(x + HitTestSize, Width - 1);
        int minY = Math.Max(y - HitTestSize, 0);
        int maxY = Math.Min(y + HitTestSize, Height - 1);

        var hitData = ReadHitTestData(minX, minY, maxX, maxY);

        var indexes = MemoryMarshal.Cast<SharpDX.Color, int>(hitData);

        IHitProxy selected = null;

        for (int i = 0; i < indexes.Length; i += 1)
        {
            if (HitProxies.TryGetAt(indexes[i], out IHitProxy hitProxy) && (selected is null || selected.HitPriority < hitProxy.HitPriority))
            {
                selected = hitProxy;
            }
        }

        return selected;
    }

    private unsafe SharpDX.Color[] ReadHitTestData(int minX, int minY, int maxX, int maxY)
    {
        int sizeX = maxX - minX + 1;
        int sizeY = maxY - minY + 1;

        var data = new SharpDX.Color[sizeX * sizeY];


        using var tempTex = new Texture2D(Device, new Texture2DDescription
        {
            ArraySize = 1,
            BindFlags = BindFlags.None,
            CpuAccessFlags = CpuAccessFlags.Read,
            Format = Format.B8G8R8A8_UNorm,
            Height = sizeY,
            Width = sizeX,
            MipLevels = 1,
            OptionFlags = ResourceOptionFlags.None,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging
        });

        ImmediateContext.CopySubresourceRegion(HitBuffer, 0, new ResourceRegion(minX, minY, 0, maxX + 1, maxY + 1, 1), tempTex, 0);
        var lockedData = ImmediateContext.MapSubresource(tempTex, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

        for (int y = 0; y < sizeY; y++)
        {
            var srcSpan = new Span<SharpDX.Color>((lockedData.DataPointer + y * lockedData.RowPitch).ToPointer(), sizeX);
            var destSpan = data.AsSpan(sizeX * y, sizeX);
            for (int x = 0; x < sizeX; x++)
            {
                var srcColor = srcSpan[x];
                destSpan[x] = new(srcColor.B, srcColor.G, srcColor.R, (byte)0);
            }
        }

        ImmediateContext.UnmapSubresource(tempTex, 0);

        return data;
    }

    public void DrawUI()
    {
        foreach (UIElement uiElem in DrawList_2D)
        {
            uiElem.Draw(this);
        }
        Primitives.Render(this);
    }

    public void LoadLevel(IList<ActorProxy> actors)
    {
        DrawList_3D.AddRange(actors);
        foreach (var actor in actors)
        {
            actor.HitID = HitProxies.Add(actor);
        }
        DrawList_2D.Add(TransformWidget);
        TransformWidget.GetAxisHitProxies(ref HitProxies);
    }

    public void UnloadLevel()
    {
        EmptyCaches();
        HitProxies.Reset();
        DrawList_3D.DisposeAndClear();
        DrawList_2D.Clear();
        TransformWidget.Attach = null;
    }

    public override void CreateSizeDependentResources(int width, int height, Texture2D newBackBuffer)
    {
        base.CreateSizeDependentResources(width, height, newBackBuffer);
    }

    public override void DisposeSizeDependentResources()
    {
        base.DisposeSizeDependentResources();
    }
}
