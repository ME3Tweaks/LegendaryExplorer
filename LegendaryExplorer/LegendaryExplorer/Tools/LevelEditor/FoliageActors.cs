using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// A FoliageFactory: its authoring volume, and the cooked instances of each of its FoliageComponents. The volume outline is drawn when the
/// actor is selected or volumes are shown.
/// </summary>
public sealed class FoliageFactoryProxy : BrushProxy
{
    public List<FoliageComponentProxy> FoliageComponents = [];

    public override string Category => "Foliage";
    public override int HitPriority => IHitProxy.StandardPriority;

    public FoliageFactoryProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        if (Properties.GetProp<ArrayProperty<StructProperty>>("Meshes") is not { } meshes) return;
        for (int i = 0; i < meshes.Count; i++)
        {
            if (meshes[i].GetProp<ObjectProperty>("Component")?.ResolveToEntry(Pcc) is not ExportEntry componentExport) continue;
            var component = new FoliageComponentProxy(context.RenderContext, componentExport, this) { ActorPropertyName = $"Meshes[{i}].Component" };
            FoliageComponents.Add(component);
            Components.Add(component);
        }
    }

    public override ActorCoverage Coverage
    {
        get
        {
            int instances = FoliageComponents.Sum(c => c.InstanceCount);
            int rendered = FoliageComponents.Count(c => c.HasPreviewMesh);
            if (rendered == 0)
                return new(ActorRepresentation.EditorMarker, instances == 0
                    ? "Authoring volume only: no cooked foliage instances. Procedural regeneration is not implemented."
                    : "Authoring volume only: the cooked instances' mesh could not be loaded.");
            return new(ActorRepresentation.PartialScene,
                $"{instances:N0} cooked instances in {rendered} of {FoliageComponents.Count} foliage components, with each component's mesh and material. "
                + "Partial: static pose (no sway), no distance thinning or draw radius, and the stored per-instance lighting is not applied. "
                + "Procedural regeneration is not implemented.");
        }
    }

    public override void Render(LevelEditorRenderContext context, RenderPass pass)
    {
        bool outline = context.IsActorSelected(this) || context.GetCategoryState("Volumes").IsVisible;
        foreach (var component in Components)
        {
            if (component is BrushComponentProxy && !outline || !context.IsComponentVisible(component)) continue;
            component.Render(context, pass);
        }
    }

    public override BoxSphereBounds GetBounds()
    {
        var drawn = FoliageComponents.Where(c => c.HasPreviewMesh).ToList();
        if (drawn.Count == 0) return base.GetBounds();
        var bounds = drawn[0].GetBounds();
        foreach (var c in drawn.Skip(1)) bounds = bounds.Union(c.GetBounds());
        return bounds;
    }
}

/// <summary>
/// A FoliageComponent's cooked LitInstances (StoredFoliageInstance: world-space location and scaled axes), drawn as one merged mesh of its
/// InstanceStaticMesh's LOD 0 with its Material on every section
/// </summary>
public sealed class FoliageComponentProxy : MeshComponentProxy
{
    private PreviewMeshDependencies Dependencies;
    public int InstanceCount { get; private set; }

    public FoliageComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent) : base(context, componentExport, parent)
    {
        LoadMesh();
    }

    // The instances are in world space
    protected override Matrix4x4 ParentToWorld => Matrix4x4.Identity;

    private void LoadMesh()
    {
        var context = RenderContext;
        Dependencies = new PreviewMeshDependencies(Export, context.PackageCache);
        var instances = ReadInstances(Properties);
        InstanceCount = instances.Count;
        if (instances.Count == 0 || EffectiveLightingProperties.ResolveReference(Export, "InstanceStaticMesh", context.PackageCache) is not { } meshExport) return;
        Dependencies.Track(meshExport);
        IEntry material = Properties.GetProp<ObjectProperty>("Material")?.ResolveToEntry(Export.FileRef);
        if (material is not null) Dependencies.Track(material);
        StaticMesh stm = meshExport.GetBinaryData<StaticMesh>();
        if (stm.LODModels.Length == 0 || context.Device is null) return;
        var (vertices, triangles) = BuildInstances(stm.LODModels[0], instances);
        if (triangles.Count == 0) return;
        Mesh = ModelPreview<LEVertex>.CreateSurface(context, new Mesh<LEVertex>(context.Device, triangles, vertices), material, null);
        MeshIFP = meshExport.InstancedFullPath;
        Mesh.UpdateLocalToWorld(Matrix4x4.Identity);
    }

    /// <summary>Each instance's local-to-world: rows XAxis, YAxis, ZAxis (scaled) and location</summary>
    public static List<Matrix4x4> ReadInstances(PropertyCollection props)
    {
        var result = new List<Matrix4x4>();
        foreach (StructProperty instance in props.GetProp<ArrayProperty<StructProperty>>("LitInstances") ?? Enumerable.Empty<StructProperty>())
        {
            Vector3 V(string name) => instance.GetProp<StructProperty>(name) is { } s ? CommonStructs.GetVector3(s) : Vector3.Zero;
            Vector3 x = V("XAxis"), y = V("YAxis"), z = V("ZAxis"), location = V("Location");
            result.Add(new Matrix4x4(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, location.X, location.Y, location.Z, 1));
        }
        return result;
    }

    internal static (List<LEVertex>, List<Triangle>) BuildInstances(StaticMeshRenderData lod, List<Matrix4x4> instances)
    {
        int numVertices = (int)lod.NumVertices;
        var vertices = new List<LEVertex>(numVertices * instances.Count);
        var triangles = new List<Triangle>(lod.IndexBuffer.Length / 3 * instances.Count);
        var buffer = lod.VertexBuffer;
        foreach (Matrix4x4 transform in instances)
        {
            uint first = (uint)vertices.Count;
            for (int i = 0; i < numVertices; i++)
            {
                var position = lod.PositionVertexBuffer.VertexData[i];
                var vertex = buffer.VertexData[i];
                Fixed4<Vector4> uvs = default;
                for (int j = 0; j < uvs.Length && j < (buffer.bUseFullPrecisionUVs ? vertex.FullPrecisionUVs.Length : vertex.HalfPrecisionUVs.Length); j++)
                    uvs[j] = new Vector4(buffer.bUseFullPrecisionUVs ? vertex.FullPrecisionUVs[j] : vertex.HalfPrecisionUVs[j], 0, 0);
                Vector4 normal = (Vector4)vertex.TangentZ;
                Vector3 n = SafeNormalize(Vector3.TransformNormal(new Vector3(normal.X, normal.Y, normal.Z), transform));
                vertices.Add((LEVertex)LEVertex.Create(Vector3.Transform(new Vector3(position.X, position.Y, position.Z), transform),
                    SafeNormalize(Vector3.TransformNormal((Vector3)vertex.TangentX, transform)), new Vector4(n, normal.W), uvs));
            }
            for (int i = 0; i + 2 < lod.IndexBuffer.Length; i += 3)
                triangles.Add(new Triangle(first + lod.IndexBuffer[i], first + lod.IndexBuffer[i + 1], first + lod.IndexBuffer[i + 2]));
        }
        return (vertices, triangles);
    }

    private static Vector3 SafeNormalize(Vector3 v) => v.LengthSquared() > 1e-20f ? Vector3.Normalize(v) : Vector3.Zero;

    public override bool RefreshPreview(IMEPackage package, IReadOnlySet<int> updatedExports, bool force = false)
    {
        if (!force && !Dependencies.IsAffectedBy(package, updatedExports)) return false;
        ReleaseMesh();
        MeshIFP = null;
        ReadProperties();
        LoadMesh();
        return true;
    }

    public override void Render(MeshRenderContext context, RenderPass pass)
    {
        if (pass is RenderPass.Collision || !context.IsComponentVisible(this)) return;
        Mesh?.Render(pass, context, 0);
    }
}
