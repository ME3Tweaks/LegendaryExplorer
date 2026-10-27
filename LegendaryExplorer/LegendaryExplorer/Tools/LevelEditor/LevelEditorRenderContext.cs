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
    private Dictionary<LegendaryExplorerCore.Packages.ExportEntry, MeshComponentProxy> decalReceivers;
    internal MeshComponentProxy FindDecalReceiver(LegendaryExplorerCore.Packages.ExportEntry export)
    {
        decalReceivers ??= DrawList_3D.SelectMany(a => a.Components).OfType<MeshComponentProxy>()
            .GroupBy(c => c.Export).ToDictionary(g => g.Key, g => g.First());
        if (export is null || !decalReceivers.TryGetValue(export, out var component) || component is null) return null;
        // In-place mesh refreshes can replace components without replacing the actor.
        if (!component.Actor.Components.Contains(component))
            decalReceivers[export] = component = component.Actor.Components.OfType<MeshComponentProxy>().FirstOrDefault(c => c.Export == export);
        return component;
    }
    public List<UIElement> DrawList_UI = [];

    private USparseArray<IHitProxy> HitProxies = [];

    public readonly Widget TransformWidget;

    public readonly LightGizmo LightGizmo = new();

    public readonly SplineTangentGizmo SplineGizmo = new();

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
    public readonly HelperSprites HelperSprites = new();
    public ActorProxy SelectedActor { get; set; }
    public const int MaxHelperMarkers = 4096;
    public const float HelperDrawDistance = 50000;
    private int helperMarkers;

    // A single shared sprite batch; limit submitted helpers rather than issuing per-actor draw calls.
    // The selected helper remains available even if the budget has been reached.
    internal bool TryDrawHelper(ActorProxy actor)
    {
        if (actor is DecalActorProxy && !IsActorSelected(actor) && !GetCategoryState("Effects helpers").IsVisible) return false;
        Vector3 position = actor.LocalToWorld.Translation;
        if (!IsActorVisible(actor) || !WorldToPixel(position, out var pixel)
            || pixel.X < -16 || pixel.Y < -16 || pixel.X > Width + 16 || pixel.Y > Height + 16) return false;
        if (!IsActorSelected(actor) && (helperMarkers >= MaxHelperMarkers
            || Vector3.DistanceSquared(position, Camera.Position) > HelperDrawDistance * HelperDrawDistance)) return false;
        helperMarkers++;
        return true;
    }

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
        //unprojected into view space, then turned into the world: inverting the whole view-projection loses several pixels of precision
        //tens of thousands of units from the world origin, where the near plane is a rounding error away from the eye
        Matrix4x4.Invert(Camera.ProjectionMatrix, out Matrix4x4 clipToView);
        float ndcX = 2f * (x + 0.5f) / Width - 1;
        float ndcY = 1 - 2f * (y + 0.5f) / Height;
        //reversed depth: the near plane is at 1
        Vector4 near = Vector4.Transform(new Vector4(ndcX, ndcY, 1, 1), clipToView);
        Vector4 far = Vector4.Transform(new Vector4(ndcX, ndcY, 0.5f, 1), clipToView);
        Vector3 nearView = new Vector3(near.X, near.Y, near.Z) / near.W;
        Vector3 farView = new Vector3(far.X, far.Y, far.Z) / far.W;
        //the view is a rotation and a translation, so its inverse rotation is its transpose
        Matrix4x4 view = Camera.ViewMatrix;
        Matrix4x4 viewToWorld = Matrix4x4.Transpose(view with { M41 = 0, M42 = 0, M43 = 0 });
        Vector3 origin = Vector3.TransformNormal(nearView - view.Translation, viewToWorld);
        Vector3 direction = Vector3.TransformNormal(farView - nearView, viewToWorld);
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

    public bool IsActorSelected(ActorProxy actor)
    {
        if (actor is null || SelectedActor is null) return false;
        return Contains(SelectedActor, actor);

        static bool Contains(ActorProxy candidate, ActorProxy target)
        {
            if (candidate == target) return true;
            if (candidate is PrefabInstanceProxy prefab)
                foreach (var child in prefab.Children)
                    if (Contains(child, target)) return true;
            return false;
        }
    }

    public override bool IsActorVisible(ActorProxy actor)
    {
        if (!actor.HasSpatialRepresentation) return false;
        if (IsActorSelected(actor)) return true;
        if (actor is DecalActorProxy)
            return GetCategoryState("Effects helpers").IsVisible || GetCategoryState("Decals").IsVisible;
        //helpers are shown by category, whatever bHidden says (plain spline points are editor-only)
        if (actor is EditorActorProxy or SplineActorProxy { IsLoft: false }) return GetCategoryState(actor.Category).IsVisible;
        return GetCategoryState(actor.Category).IsVisible && (!actor.IsHidden || ShowHidden);
    }

    public override bool IsComponentVisible(PrimitiveComponentProxy component)
        => IsActorSelected(component.Actor)
            || base.IsComponentVisible(component) && (!component.HiddenGame || ShowHidden);

    private bool IsHitProxyVisible(IHitProxy hitProxy) => hitProxy switch
    {
        ActorProxy actor => IsActorVisible(actor),
        AxisHitProxy => TransformWidget.Attach is { } actor && IsActorVisible(actor),
        LightHandleHitProxy => LightGizmo.Attach is ActorProxy light && IsActorVisible(light),
        SplineTangentHitProxy => SplineGizmo.Attach is { } loft && IsActorVisible(loft),
        SplinePointHitProxy point => IsActorVisible(point.Point),
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
    private ModifierKeys? clickModifiers;

    public override bool MouseUp(MouseButtons button, int x, int y) => MouseUp(button, x, y, Keyboard.Modifiers);

    /// <summary>Completes a viewport click with its mouse-down modifiers, including clicks on a spline actor's mesh.</summary>
    public bool MouseUp(MouseButtons button, int x, int y, ModifierKeys modifiers)
    {
        if (button is MouseButtons.Left)
        {
            modifiers = clickModifiers ?? modifiers;
            clickModifiers = null;
        }
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
        if (SplineGizmo.IsDragging)
        {
            SplineGizmo.EndDrag();
            return true;
        }
        if (base.MouseUp(button, x, y)) return true;

        if (HitBufferView is not null)
        {
            IHitProxy selected = GetHitProxy(x, y);

            TransformWidget.CurrentAxis = EWidgetAxis.None;
            if (button is MouseButtons.Left && EditSplineConnection(selected, modifiers)) return true;
            switch (selected)
            {
                case ActorProxy actor:
                    SelectActor?.Invoke(actor);
                    TransformWidget.Attach = actor;
                    break;
                case SplinePointHitProxy point:
                    SelectActor?.Invoke(point.Point);
                    TransformWidget.Attach = point.Point;
                    break;
                case AxisHitProxy axisProxy:
                    TransformWidget.CurrentAxis = axisProxy.Axis;
                    break;
            }
        }

        return false;
    }

    public override bool MouseDown(MouseButtons button, int x, int y) => MouseDown(button, x, y, Keyboard.Modifiers);

    /// <summary>Starts a viewport click, retaining Ctrl/Shift even if released before the mouse button.</summary>
    public bool MouseDown(MouseButtons button, int x, int y, ModifierKeys modifiers)
    {
        if (button is MouseButtons.Left) clickModifiers = modifiers;
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
        if (SplineGizmo.IsDragging && Mouse.LeftButton is MouseButtonState.Released)
        {
            SplineGizmo.EndDrag();
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
                case SplineTangentHitProxy tangentProxy when button is MouseButtons.Left:
                    SplineGizmo.BeginDrag(this, tangentProxy.Handle, x, y);
                    if (SplineGizmo.IsDragging) return true;
                    break;
            }
        }
        return base.MouseDown(button, x, y);
    }

    /// <summary>Handles Ctrl clicks on spline markers or meshes through the real hit buffer; Shift breaks instead of connecting or flipping.</summary>
    public bool EditSplineConnection(int x, int y, ModifierKeys modifiers) => EditSplineConnection(GetHitProxy(x, y), modifiers);

    private bool EditSplineConnection(IHitProxy hit, ModifierKeys modifiers)
    {
        var target = hit switch { SplinePointHitProxy point => point.Point, SplineActorProxy actor => actor, _ => null };
        if (!modifiers.HasFlag(ModifierKeys.Control) || SplineGizmo.Attach is not { } source
            || target is null) return false;
        if (modifiers.HasFlag(ModifierKeys.Shift)) source.BreakConnection(target);
        else source.ConnectOrFlip(target);
        return true;
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
        if (SplineGizmo.IsDragging)
        {
            if (Mouse.LeftButton is MouseButtonState.Released)
            {
                SplineGizmo.EndDrag();
                return true;
            }
            SplineGizmo.Drag(this, x, y);
            return true;
        }
        if (base.MouseMove(x, y)) return true;

        if (HitBufferView is not null)
        {
            IHitProxy selected = GetHitProxy(x, y);

            TransformWidget.CurrentAxis = EWidgetAxis.None;
            LightGizmo.HoveredHandle = LightHandle.None;
            SplineGizmo.HoveredHandle = SplineTangentHandle.None;
            switch (selected)
            {
                case AxisHitProxy axisProxy:
                    TransformWidget.CurrentAxis = axisProxy.Axis;
                    break;
                case LightHandleHitProxy handleProxy:
                    LightGizmo.HoveredHandle = handleProxy.Handle;
                    break;
                case SplineTangentHitProxy tangentProxy:
                    SplineGizmo.HoveredHandle = tangentProxy.Handle;
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
        helperMarkers = 0;
        foreach (UIElement uiElem in DrawList_UI)
        {
            if (uiElem == TransformWidget && TransformWidget.Attach is { } actor && !IsActorVisible(actor)) continue;
            uiElem.Draw(this);
        }
        UIPrimitives.Render(this);
    }

    public override void Update(float timestep)
    {
        // Pose before skinning; hidden actors still need current bounds for light environments.
        base.Update(timestep);
        foreach (ActorProxy actor in DrawList_3D)
        {
            actor.UpdateScene(this, timestep);
        }
    }

    public void LoadActors(IList<ActorProxy> actors)
    {
        decalReceivers = null;
        DrawList_3D.AddRange(actors);
        foreach (var decal in DrawList_3D.OfType<DecalActorProxy>()) decal.BindReceivers(this);
        foreach (var actor in actors)
        {
            AddHitProxies(actor);
        }
        if (!DrawList_UI.Contains(LightGizmo))
        {
            //before the transform widget, so that the widget is drawn over the light's radius
            DrawList_UI.Insert(0, LightGizmo);
            LightGizmo.GetHandleHitProxies(ref HitProxies);
        }
        //read-only views use it too, to select loft points; it has no handles for them
        if (!DrawList_UI.Contains(SplineGizmo))
        {
            DrawList_UI.Insert(0, SplineGizmo);
            SplineGizmo.GetHandleHitProxies(ref HitProxies);
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
        decalReceivers = null;
        EmptyCaches();
        ClearLights();
        HitProxies.Reset();
        DrawList_3D.DisposeAndClear();
        DrawList_UI.Clear();
        TransformWidget.Attach = null;
        SelectedActor = null;
        LightGizmo.Attach = null;
        SplineGizmo.Attach = null;
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
        HelperSprites.Dispose();
        //its batches are keyed by the sprite textures just disposed
        Sprites.Clear();
        base.DisposeResources();
    }

    internal void RemoveActor(ActorProxy actor)
    {
        decalReceivers = null;
        if (DrawList_3D.Remove(actor))
        {
            HitProxies.RemoveAt(actor.HitID);
            if (actor is SplineActorProxy spline) HitProxies.RemoveAt(spline.PointHitProxy.HitID);
            if (SelectedActor == actor) SelectedActor = null;
        }
    }

    private void AddHitProxies(ActorProxy actor)
    {
        actor.HitID = HitProxies.Add(actor);
        if (actor is SplineActorProxy spline) spline.PointHitProxy.HitID = HitProxies.Add(spline.PointHitProxy);
    }

    internal void AddActor(ActorProxy actor)
    {
        decalReceivers = null;
        if (!DrawList_3D.Contains(actor))
        {
            DrawList_3D.Add(actor);
            foreach (var decal in DrawList_3D.OfType<DecalActorProxy>()) decal.BindReceivers(this);
            AddHitProxies(actor);
        }
    }
}
