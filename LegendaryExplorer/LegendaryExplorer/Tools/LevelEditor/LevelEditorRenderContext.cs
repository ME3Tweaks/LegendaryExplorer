using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.SharpDX;
using LegendaryExplorerCore.Unreal.BinaryConverters;
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
    public List<UIElement> DrawList_UI = [];

    private USparseArray<IHitProxy> HitProxies = [];

    public readonly Widget TransformWidget;

    public readonly LightGizmo LightGizmo = new();

    /// <summary>
    /// Lines and meshes drawn on top of the scene: widgets, gizmos, etc
    /// </summary>
    public readonly BatchedPrimitives UIPrimitives = new();

    /// <summary>
    /// Lines and meshes drawn into the scene, hidden behind its geometry
    /// </summary>
    public readonly BatchedPrimitives WorldPrimitives = new(depthTested: true);

    /// <summary>
    /// Camera-facing sprites drawn into the scene, such as light icons
    /// </summary>
    public readonly SpriteBatch Sprites = new();

    public readonly LightSprites LightSprites = new();

    /// <summary>
    /// The view's right and up directions in world space, for drawing things that face the camera. Correct in the top-down view too
    /// </summary>
    public (Vector3 Right, Vector3 Up) GetViewAxes()
    {
        Matrix4x4 view = Camera.ViewMatrix;
        var right = new Vector3(view.M11, view.M21, view.M31);
        var up = new Vector3(view.M12, view.M22, view.M32);
        return (right.LengthSquared() > 0 ? Vector3.Normalize(right) : Vector3.UnitX, up.LengthSquared() > 0 ? Vector3.Normalize(up) : Vector3.UnitZ);
    }

    //keep in sync with the tint of highlighted meshes in LevelEditorShader.hlsl
    public static readonly Vector4 HighlightColor = new(1f, 0.62f, 0.12f, 1);

    /// <summary>
    /// Mesh components the selected light is eligible to light. Drawn tinted and outlined
    /// </summary>
    public HashSet<PrimitiveComponentProxy> HighlightedComponents { get; } = [];

    /// <summary>
    /// Light components eligible to light the selected mesh. Their icons are outlined
    /// </summary>
    public HashSet<LegendaryExplorerCore.Packages.ExportEntry> HighlightedLights { get; } = [];

    /// <summary>
    /// Outlines a highlighted component's bounding box
    /// </summary>
    public void DrawHighlightBounds(BoxSphereBounds bounds)
    {
        Vector3 o = bounds.Origin, e = bounds.BoxExtent;
        Span<Vector3> corners = stackalloc Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            corners[i] = o + new Vector3((i & 1) == 0 ? -e.X : e.X, (i & 2) == 0 ? -e.Y : e.Y, (i & 4) == 0 ? -e.Z : e.Z);
        }
        for (int i = 0; i < 8; i++)
        {
            for (int bit = 1; bit < 8; bit <<= 1)
            {
                if ((i & bit) == 0) WorldPrimitives.AddLine(corners[i], corners[i | bit], HighlightColor, 0xFFFFFF);
            }
        }
    }

    /// <summary>
    /// The size of a pixel, in world units, at a point's depth
    /// </summary>
    public float WorldUnitsPerPixel(Vector3 point)
    {
        if (Camera.IsOrthographic || Width <= 0)
        {
            return Camera.OrthoWidth / Math.Max(Width, 1);
        }
        return WorldToScreen(point).W * 2 / (Width * Camera.ProjectionMatrix.M11);
    }

    /// <summary>
    /// The ray through a pixel of the viewport, from the near plane into the scene
    /// </summary>
    public (Vector3 Origin, Vector3 Direction) GetPixelRay(int x, int y)
    {
        Matrix4x4.Invert(Camera.ViewProjectionMatrix, out Matrix4x4 screenToWorld);
        float ndcX = 2f * (x + 0.5f) / Width - 1;
        float ndcY = 1 - 2f * (y + 0.5f) / Height;
        //reversed depth: the near plane is at 1
        Vector4 near = Vector4.Transform(new Vector4(ndcX, ndcY, 1, 1), screenToWorld);
        Vector4 far = Vector4.Transform(new Vector4(ndcX, ndcY, 0.5f, 1), screenToWorld);
        var origin = new Vector3(near.X, near.Y, near.Z) / near.W;
        Vector3 direction = new Vector3(far.X, far.Y, far.Z) / far.W - origin;
        return (origin, direction.LengthSquared() > 0 ? Vector3.Normalize(direction) : Camera.CameraForward);
    }

    private readonly Dictionary<string, OutlinerCategoryState> _categories = [];

    public OutlinerCategoryState GetCategoryState(string category)
    {
        if (!_categories.TryGetValue(category, out var state))
        {
            _categories[category] = state = new OutlinerCategoryState(category);
        }
        return state;
    }

    public override bool IsActorVisible(ActorProxy actor)
        => GetCategoryState(actor.Category).IsVisible && (!actor.IsHidden || ShowHidden);

    private bool IsHitProxyVisible(IHitProxy hitProxy) => hitProxy switch
    {
        ActorProxy actor => IsActorVisible(actor),
        AxisHitProxy => TransformWidget.Attach is { } actor && IsActorVisible(actor),
        LightHandleHitProxy => LightGizmo.Attach is ActorProxy light && IsActorVisible(light),
        _ => true
    };

    /// <summary>
    /// Show actors and components that aren't rendered in game (bHidden or HiddenGame)
    /// </summary>
    public bool ShowHidden;

    private bool IsReadOnly;

    public LevelEditorRenderContext(bool readOnly = false) : base(sharePackages: !readOnly)
    {
        BackgroundColor = System.Windows.Media.Color.FromRgb(0x99, 0x99, 0x99);
        Camera.FirstPerson = true;
        TransformWidget = new Widget();
        IsReadOnly = readOnly;
    }

    const int HitTestSize = 3;

    public override bool MouseUp(MouseButtons button, int x, int y)
    {
        if (TransformWidget.IsDragging)
        {
            TransformWidget.EndDrag();
            return true;
        }
        if (LightGizmo.IsDragging)
        {
            LightGizmo.EndDrag();
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
        if (LightGizmo.IsDragging && Mouse.LeftButton is MouseButtonState.Released)
        {
            LightGizmo.EndDrag();
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
                case LightHandleHitProxy handleProxy when button is MouseButtons.Left:
                    LightGizmo.BeginDrag(handleProxy.Handle);
                    if (LightGizmo.IsDragging) return true;
                    break;
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
        if (LightGizmo.IsDragging)
        {
            //failsafe if mouseup event was not captured
            if (Mouse.LeftButton is MouseButtonState.Released)
            {
                LightGizmo.EndDrag();
                return true;
            }
            LightGizmo.Drag(this, x, y);
            return true;
        }
        if (base.MouseMove(x, y)) return true;

        if (HitBufferView is not null)
        {
            IHitProxy selected = GetHitProxy(x, y);

            TransformWidget.CurrentAxis = EWidgetAxis.None;
            LightGizmo.HoveredHandle = LightHandle.None;
            switch (selected)
            {
                case AxisHitProxy axisProxy:
                    TransformWidget.CurrentAxis = axisProxy.Axis;
                    break;
                case LightHandleHitProxy handleProxy:
                    LightGizmo.HoveredHandle = handleProxy.Handle;
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
            if (HitProxies.TryGetAt(indexes[i], out IHitProxy hitProxy)
                && IsHitProxyVisible(hitProxy)
                && (selected is null || selected.HitPriority < hitProxy.HitPriority))
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
        WorldPrimitives.Render(this);
        Sprites.Render(this);
        foreach (UIElement uiElem in DrawList_UI)
        {
            if (uiElem == TransformWidget && TransformWidget.Attach is { } actor && !IsActorVisible(actor)) continue;
            uiElem.Draw(this);
        }
        UIPrimitives.Render(this);
    }

    public void LoadActors(IList<ActorProxy> actors)
    {
        DrawList_3D.AddRange(actors);
        foreach (var actor in actors)
        {
            actor.HitID = HitProxies.Add(actor);
        }
        if (!DrawList_UI.Contains(LightGizmo))
        {
            //before the transform widget, so that the widget is drawn over the light's radius
            DrawList_UI.Insert(0, LightGizmo);
            LightGizmo.GetHandleHitProxies(ref HitProxies);
        }
        if (!IsReadOnly && !DrawList_UI.Contains(TransformWidget))
        {
            DrawList_UI.Add(TransformWidget);
            TransformWidget.GetAxisHitProxies(ref HitProxies);
        }
    }

    public void UnloadActors(IList<ActorProxy> actors)
    {
        foreach (var actor in actors)
        {
            RemoveActor(actor);
        }
    }

    public void UnloadLevel()
    {
        EmptyCaches();
        ClearLights();
        HitProxies.Reset();
        DrawList_3D.DisposeAndClear();
        DrawList_UI.Clear();
        TransformWidget.Attach = null;
        LightGizmo.Attach = null;
        HighlightedComponents.Clear();
        HighlightedLights.Clear();
    }

    public override void CreateSizeDependentResources(int width, int height, Texture2D newBackBuffer)
    {
        base.CreateSizeDependentResources(width, height, newBackBuffer);
    }

    public override void DisposeSizeDependentResources()
    {
        base.DisposeSizeDependentResources();
    }

    public override void DisposeResources()
    {
        LightSprites.Dispose();
        //its batches are keyed by the sprite textures just disposed
        Sprites.Clear();
        base.DisposeResources();
    }

    internal void RemoveActor(ActorProxy actor)
    {
        if (DrawList_3D.Remove(actor))
        {
            HitProxies.RemoveAt(actor.HitID);
        }
    }

    internal void AddActor(ActorProxy actor)
    {
        if (!DrawList_3D.Contains(actor))
        {
            DrawList_3D.Add(actor);
            actor.HitID = HitProxies.Add(actor);
        }
    }
}
