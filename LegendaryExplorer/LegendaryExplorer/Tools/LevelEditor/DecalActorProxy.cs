using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>Cooked surfaces and an editable projector helper. No projection or package binary is rewritten.</summary>
public sealed class DecalActorProxy : EditorActorProxy
{
    private readonly DecalComponentProxy decal;
    private readonly Matrix4x4 cookedActorTransform;
    public override string Category => "Decals";
    public int SortOrder => decal?.SortOrder ?? 0;
    internal bool ProjectionMoved => LocalToWorld != cookedActorTransform;
    public override ActorCoverage Coverage => decal?.GetCoverage() ?? base.Coverage;
    internal void BindReceivers(LevelEditorRenderContext context) => decal?.BindReceivers(context);

    public DecalActorProxy(IActorEditorContext context, ExportEntry export)
        : base(context, export, new(ActorKind.Decal, "Effects helpers", "Decal projector helper; no usable cooked surface."))
    {
        cookedActorTransform = LocalToWorld;
        var component = EffectiveLightingProperties.ResolveReference(export, "Decal", context.PackageCache, Properties);
        if (component is not null)
        {
            decal = new DecalComponentProxy(context.RenderContext, component, this);
            Components.Add(decal);
        }
    }

    public override void Render(LevelEditorRenderContext context, RenderPass pass)
    {
        base.Render(context, pass);
        if (pass == RenderPass.Base && context.IsActorSelected(this)
            && Matrix4x4.Invert(cookedActorTransform, out var inverse))
            decal?.DrawProjector(context, inverse * LocalToWorld);
        if (pass == RenderPass.Decals && (context.IsActorSelected(this)
            || context.GetCategoryState("Decals").IsVisible && (!IsHidden || context.ShowHidden)))
            decal?.Render(context, pass);
    }

    public override BoxSphereBounds GetBounds() => ProjectionMoved ? base.GetBounds() : decal?.SurfaceBounds ?? base.GetBounds();

    protected override void UpdateLocalToWorld()
    {
        base.UpdateLocalToWorld();
        if (decal is not null) NotifyCoverageChanged();
    }

    public override bool TryRefreshPreview(IMEPackage package, HashSet<int> exports, bool force = false)
    {
        if (package == Pcc && exports.Contains(Export.UIndex)
            || decal is not null && package == decal.Export.FileRef && exports.Contains(decal.Export.UIndex)) return false;
        if (decal?.Refresh(package, exports, force) == true) { NotifyCoverageChanged(); return true; }
        return false;
    }
}

/// <summary>Each receiver owns one cached GPU mesh; no per-frame vertex rebuild or package writes.</summary>
public sealed class DecalComponentProxy : PrimitiveComponentProxy
{
    private readonly DecalActorProxy owner;
    private DecalProjection projection;
    private ExportEntry material;
    private string failure;
    private string lastCoverage;
    private readonly List<Receiver> receivers = [];
    private sealed class Receiver(StaticReceiverData data, ExportEntry export)
    {
        public readonly StaticReceiverData Data = data;
        public readonly ExportEntry Export = export;
        public MeshComponentProxy Source;
        public ExportEntry Geometry;
        public Matrix4x4 OriginalTransform;
        public byte[] OriginalData;
        public ModelPreview<LEVertex> Mesh;
        public string Failure;
        public bool Invalidated;
    }
    public int SortOrder => projection?.SortOrder ?? 0;
    public BoxSphereBounds? SurfaceBounds => receivers.Where(r => r.Mesh is not null)
        .Select(r => (BoxSphereBounds?)r.Mesh.LODs[0].Mesh.TransformedBounds).Aggregate((BoxSphereBounds?)null,
            (a, b) => a is { } bounds ? bounds.Union(b.Value) : b);

    public DecalComponentProxy(MeshRenderContext context, ExportEntry export, DecalActorProxy actor) : base(context, export, actor)
    {
        owner = actor;
        try
        {
            Properties = EffectiveLightingProperties.Read(export, context.PackageCache);
            if (!export.Game.IsLEGame()) throw new NotSupportedException("Cooked decal preview currently supports LE1, LE2 and LE3.");
            projection = DecalProjection.Read(Properties);
            if (actor.Export.IsA("DecalActorMovable") || Properties.GetProp<BoolProperty>("bMovableDecal") is { Value: true })
                throw new NotSupportedException("Movable decal reprojection is not implemented.");
            if (export.Parent?.Parent?.ClassName != "Level")
                throw new NotSupportedException("Prefab/template decal receiver remapping is not implemented.");
            material = EffectiveLightingProperties.ResolveReference(export, "DecalMaterial", context.PackageCache, Properties);
            if (material is null) throw new NotSupportedException("Decal material could not be resolved.");
            foreach (var data in export.GetBinaryData<DecalComponent>().StaticReceivers)
            {
                var target = EffectiveLightingProperties.Resolve(export.FileRef.GetEntry(data.Component), context.PackageCache);
                var receiver = new Receiver(data, target);
                receivers.Add(receiver);
                if (target?.ClassName != "StaticMeshComponent") receiver.Failure = $"Unsupported receiver: {target?.ClassName ?? "unresolved"}.";
                else if (data.Vertices.Length == 0 || data.NumTriangles == 0) receiver.Failure = "No cooked triangles.";
                else if (!ValidGeometry(data)) receiver.Failure = "Malformed cooked triangle data.";
                else if (data.InstanceIndex is not (0 or -1)) receiver.Failure = "Instanced receiver is not supported.";
            }
            if (receivers.Count == 0) failure = "No cooked static receivers; reprojection is not implemented.";
        }
        catch (Exception e) when (e is not OutOfMemoryException) { failure = e.Message; }
    }

    internal static bool ValidGeometry(StaticReceiverData data) => data.NumTriangles <= int.MaxValue / 3
        && data.Indices.Length == (long)data.NumTriangles * 3
        && data.Indices.All(i => i < data.Vertices.Length)
        && data.Vertices.All(v => DecalProjection.Finite(v.Position));

    private bool Stale(Receiver r) => owner.ProjectionMoved || r.Invalidated
        || r.Source is { } source && (source.Actor.IsDisposed || !source.Actor.Components.Contains(source)
            || source.LocalToWorld != r.OriginalTransform);

    internal void BindReceivers(LevelEditorRenderContext context)
    {
        foreach (var r in receivers.Where(r => r.Failure is null && r.Source is null))
        {
            r.Source = context.FindDecalReceiver(r.Export);
            if (r.Source is null) continue;
            r.OriginalTransform = r.Source.LocalToWorld;
            r.OriginalData = r.Export.Data.ToArray();
            r.Geometry = EffectiveLightingProperties.ResolveReference(r.Export, "StaticMesh", context.PackageCache);
            if (r.Source.Actor.IsTransformDirty) r.Invalidated = true;
        }
    }

    internal void DrawProjector(LevelEditorRenderContext context, Matrix4x4 delta)
    {
        if (projection is not { } p) return;
        // Keep the selection guides depth-tested so scene geometry obscures them.
        // Decals project orthographically, so retain parallel sides rather than drawing a cone.
        const int noHitId = 0xFFFFFF;
        var lines = context.WorldPrimitives;
        var color = LevelEditorRenderContext.HighlightColor;
        Span<Vector3> corners = stackalloc Vector3[8];
        for (int i = 0; i < 8; i++)
            corners[i] = Vector3.Transform(p.Location + p.Tangent * p.Width * ((i & 1) == 0 ? -0.5f : 0.5f)
                + p.Binormal * p.Height * ((i & 2) == 0 ? -0.5f : 0.5f)
                - p.Normal * ((i & 4) == 0 ? p.NearPlane : p.FarPlane), delta);
        for (int i = 0; i < 8; i++)
            for (int bit = 1; bit <= 4; bit <<= 1)
                if ((i & bit) == 0) lines.AddLine(corners[i], corners[i | bit], color, noHitId);

        // Tie the actor's transform handle to the projection volume even when its origin is offset.
        for (int i = 0; i < 4; i++)
            lines.AddLine(owner.LocalToWorld.Translation, corners[i], color with { W = 0.45f }, noHitId);

        Vector3 start = Vector3.Transform(p.Location, delta);
        Vector3 tip = Vector3.Transform(p.Location - p.Normal * p.FarPlane, delta);
        Vector3 direction = tip - start;
        float length = direction.Length();
        if (length > 0.001f)
        {
            direction /= length;
            float head = Math.Min(length * 0.25f, 14 * context.WorldUnitsPerPixel(tip));
            var (u, v) = LightProxyVisuals.Perpendiculars(direction);
            lines.AddLine(start, tip, color, noHitId);
            foreach (var side in new[] { u, -u, v, -v })
                lines.AddLine(tip, tip - direction * head + side * (head * 0.45f), color, noHitId);
        }
    }

    public ActorCoverage GetCoverage()
    {
        string reason = failure;
        if (owner.ProjectionMoved) reason = "Projector moved; cooked surfaces are invalid until rebuilt. Undo restores the original preview.";
        if (reason is not null) return new(ActorRepresentation.EditorMarker, reason);
        int loaded = receivers.Count(r => r.Mesh is not null && !Stale(r)
            && r.Mesh.GetRenderProxies().All(m => m.CanRenderWithGameShaders));
        string[] issues = receivers.Select(r => Stale(r) ? "Receiver changed; cooked projection is invalid."
            : r.Source is null && r.Failure is null ? "Receiver is not loaded or has not been prepared."
            : r.Failure ?? r.Mesh?.GetRenderProxies().Select(m => m.LoadedGameShaderError).FirstOrDefault(s => s is not null))
            .Where(s => s is not null).Distinct().ToArray();
        return new(loaded > 0 ? ActorRepresentation.PartialScene : ActorRepresentation.EditorMarker,
            $"Cooked static surfaces: {loaded}/{receivers.Count} ready. " +
            (issues.Length > 0 ? string.Join(" ", issues) + " " : "") +
            "Requires visible loaded receivers and game shaders. Dynamic lighting/shadows and reprojection are not implemented.");
    }

    public override void Render(MeshRenderContext renderContext, RenderPass pass)
    {
        if (renderContext is not LevelEditorRenderContext context) return;
        if (failure is not null || owner.ProjectionMoved || owner.Editor is null || !context.IsComponentVisible(this)) return;
        foreach (var r in receivers)
        {
            if (r.Failure is not null || r.Invalidated) continue;
            if (r.Source is null)
            {
                BindReceivers(context);
                if (r.Source is null || r.Invalidated) continue;
            }
            if (Stale(r) || !r.Source.HasPreviewMesh) continue;
            if (!context.IsActorSelected(owner) && !context.IsComponentVisible(r.Source)) continue;
            try
            {
                if (r.Mesh is null) Build(r, context);
                r.Mesh?.Render(RenderPass.Decals, context, 0);
            }
            catch (Exception e) when (e is not OutOfMemoryException) { r.Failure = $"Cooked receiver preview failed: {e.Message}"; }
        }
        string current = GetCoverage().Reason;
        if (current != lastCoverage) { lastCoverage = current; owner.NotifyCoverageChanged(); }
    }

    private void Build(Receiver r, MeshRenderContext context)
    {
        var matrix = r.OriginalTransform;
        if (!Matrix4x4.Invert(matrix, out var inverse)) throw new NotSupportedException("Singular receiver transform.");
        var normalMatrix = Matrix4x4.Transpose(inverse);
        var vertices = new List<LEVertex>(r.Data.Vertices.Length);
        foreach (var vertex in r.Data.Vertices)
        {
            Vector3 position = Vector3.Transform(vertex.Position, matrix);
            Vector3 tangent = Vector3.Normalize(Vector3.TransformNormal((Vector3)vertex.TangentX, matrix));
            Vector4 n = (Vector4)vertex.TangentZ;
            Vector3 normal = Vector3.Normalize(Vector3.TransformNormal(new(n.X, n.Y, n.Z), normalMatrix));
            if (!DecalProjection.Finite(position) || !DecalProjection.Finite(tangent) || !DecalProjection.Finite(normal))
                throw new NotSupportedException("Invalid cooked vertex basis.");
            Fixed4<Vector4> uv = default;
            uv[0] = new(projection.ProjectUV(position), 0, 0);
            vertices.Add((LEVertex)LEVertex.Create(position, tangent, new(normal, n.W * MathF.Sign(matrix.GetDeterminant())), uv));
        }
        List<Triangle> triangles = [];
        for (int i = 0; i < r.Data.Indices.Length; i += 3)
        {
            uint a = r.Data.Indices[i], b = r.Data.Indices[i+1], c = r.Data.Indices[i+2];
            triangles.Add(matrix.GetDeterminant() < 0 ? new(a, c, b) : new(a, b, c));
        }
        var mesh = new Mesh<LEVertex>(context.Device, triangles, vertices) { Decal = projection };
        MeshStaticLighting lighting = null;
        try
        {
            lighting = MeshStaticLighting.CreateDecal(context, Export, r.Data);
            r.Mesh = ModelPreview<LEVertex>.CreateDecal(context, mesh, material, lighting);
        }
        catch { mesh.Dispose(); lighting?.Dispose(); throw; }
    }

    public bool Refresh(IMEPackage package, HashSet<int> exports, bool force)
    {
        bool changed = false;
        foreach (var r in receivers)
        {
            if (r.Export is null) continue;
            bool geometryChanged = r.Geometry?.FileRef == package && exports.Contains(r.Geometry.UIndex);
            if (force || r.Export.FileRef == package && (exports.Contains(r.Export.UIndex) || exports.Contains(r.Export.idxLink)))
            {
                if (r.OriginalData is not null && !r.OriginalData.SequenceEqual(r.Export.Data)) r.Invalidated = true;
                if (r.Export.FileRef == package && exports.Contains(r.Export.idxLink)) r.Invalidated = true;
                changed = true;
            }
            if (geometryChanged) { r.Invalidated = true; changed = true; }
            if (force)
            {
                r.Mesh?.Dispose();
                r.Mesh = null;
            }
        }
        return changed;
    }

    public override bool TestUIndexes(HashSet<int> exports) => base.TestUIndexes(exports)
        || receivers.Any(r => r.Export?.FileRef == Export.FileRef && (exports.Contains(r.Export.UIndex) || exports.Contains(r.Export.idxLink)))
        || material?.FileRef == Export.FileRef && exports.Contains(material.UIndex);

    protected override void Dispose(bool disposing)
    {
        if (disposing) { foreach (var r in receivers) r.Mesh?.Dispose(); receivers.Clear(); }
        base.Dispose(disposing);
    }
}
