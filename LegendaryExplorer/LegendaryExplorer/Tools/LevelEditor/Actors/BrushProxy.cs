using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.Unreal.PhysX;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorer.Tools.LevelEditor;

//volumes
public class BrushProxy : ActorProxy
{
    public BrushComponentProxy BrushComponent;
    public override bool IsVolume => true;

    public BrushProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref BrushComponent);
    }
    public override int HitPriority => IHitProxy.WireFramePriority;

    /// <summary>The size of a new volume's box: UE3's default cube builder brush</summary>
    public const float NewBoxSize = 256;

    //a draft's: the class default's component its own is made from, and its collision (BrushAggGeom)
    private readonly ExportEntry componentTemplate;
    private readonly StructProperty collision;

    // The new box's hull, the same for every one, which PhysX works out once (one at a time, as its cooking library is global). A failure (as when it
    // can't be loaded) isn't kept, so the next placement tries again
    private static StructProperty newBoxHull;
    private static StructProperty NewBoxHull()
    {
        if (newBoxHull is null)
        {
            const float half = NewBoxSize / 2;
            var corners = Enumerable.Range(0, 8).Select(i => new Vector3((i & 1) == 0 ? -half : half, (i & 2) == 0 ? -half : half, (i & 4) == 0 ? -half : half)).ToArray();
            newBoxHull = KConvexElem.CreateFromHull(corners).GetStructProperty();
        }
        return newBoxHull.DeepClone();
    }

    /// <summary>
    /// A new volume (a draft): a box of <see cref="NewBoxSize"/> around its location, drawn as the commit makes its component, from
    /// <paramref name="componentTemplate"/>'s values with the box's hull. Its package is untouched until then
    /// </summary>
    internal BrushProxy(IActorEditorContext context, ActorRecord draft, PropertyCollection properties, ExportEntry componentTemplate) : base(context, draft, properties)
    {
        this.componentTemplate = componentTemplate;
        var cache = context.RenderContext.PackageCache;
        var aggGeom = GlobalUnrealObjectInfo.getDefaultStructValue(Pcc.Game, "KAggregateGeom", true, Pcc, cache);
        aggGeom.AddOrReplaceProp(new ArrayProperty<StructProperty>([NewBoxHull()], "ConvexElems"));
        collision = new StructProperty("KAggregateGeom", aggGeom, "BrushAggGeom");
        var componentProperties = EffectiveLightingProperties.ReadValues(componentTemplate, cache);
        componentProperties.AddOrReplaceProp(collision.DeepClone());
        // None already exists in every package. Pending properties bypass serialization, so even the package's name table stays untouched
        var preview = new ExportEntry(Pcc, null, "None", new byte[8], binary: LegendaryExplorerCore.Unreal.BinaryConverters.BrushComponent.Create());
        BrushComponent = new BrushComponentProxy(context.RenderContext, preview, this, componentProperties) { ActorPropertyName = nameof(BrushComponent) };
        Components.Add(BrushComponent);
        //a draft is wholly uncommitted
        UpdateDirty();
    }

    private protected override bool WritesAllValuesWhenCreated => true;

    public override void CommitChanges(PackageCache packageCache = null)
    {
        // Its export was just made (Materialize): its component is made with it, as shipping levels' volumes' are (see NewComponent), with its hull and
        // the level's empty brush, which cooked volumes share. The actor refers to them as its Brush, BrushComponent and CollisionComponent
        ExportEntry component = null;
        if (Materialized && componentTemplate is not null)
        {
            var brush = EmptyBrush(OwningFile.LevelExport, Export);
            component = NewComponent(componentTemplate, [new ObjectProperty(brush, "Brush"), collision.DeepClone(), new ObjectProperty(0, "ReplacementPrimitive")],
                LegendaryExplorerCore.Unreal.BinaryConverters.BrushComponent.Create());
            Properties.AddOrReplaceProp(new ObjectProperty(brush, "Brush"));
            Properties.AddOrReplaceProp(new ObjectProperty(component, nameof(BrushComponent)));
            Properties.AddOrReplaceProp(new ObjectProperty(component, "CollisionComponent"));
        }
        // Its cached PhysX data, which is cooked at the actor's scale, so a new scale makes it stale; its hull is unscaled, and its location and rotation are
        // applied at runtime (UE3's BuildPhysBrushData, MakeCachedConvexDataForAggGeom). With none, there's nothing to go stale: if it has rigid-body
        // collision, the game cooks it at load, at the scale it has then
        else if (IsScaleDirty
                 && Export.GetProperty<ObjectProperty>(nameof(BrushComponent))?.ResolveToEntry(Pcc) is ExportEntry own && HasCookedCollision(own))
        {
            component = own.Parent == Export ? own
                : throw new InvalidOperationException($"{Export.InstancedFullPath}'s BrushComponent isn't its own, so its collision can't be cooked at its new scale.");
        }
        // A new volume's: as a cooked blocking volume has it, if it has rigid-body collision (bDisableAllRigidBody, which Volume's component template sets and
        // BlockingVolume's clears). Without it, the game would rebuild the hull at load from the brush, which a cooked volume's is empty, and lose it
        bool cook = component is not null && (!Materialized || BrushComponent.Properties.GetProp<BoolProperty>("bDisableAllRigidBody")?.Value != true);
        base.CommitChanges(packageCache);
        // cooked at the scale the actor was just written with
        if (cook) CookCollision(component, packageCache ?? Editor?.RenderContext.PackageCache);
    }

    /// <summary>Whether <paramref name="component"/>, a BrushComponent, has cached PhysX data, which is cooked at its actor's scale</summary>
    internal static bool HasCookedCollision(ExportEntry component) =>
        component.GetBinaryData<LegendaryExplorerCore.Unreal.BinaryConverters.BrushComponent>().CachedPhysBrushData.CachedConvexElements.Length > 0;

    /// <summary>Cooks <paramref name="component"/>'s collision again, at the scale its actor has been written with</summary>
    internal static void CookCollision(ExportEntry component, PackageCache cache)
    {
        var binary = component.GetBinaryData<LegendaryExplorerCore.Unreal.BinaryConverters.BrushComponent>();
        binary.RegenCachedPhysBrushData(cache);
        component.WriteBinary(binary);
    }

    // A Model and its Polys as the cooker leaves them for a level's volumes to share: under the level, with no geometry, and no properties. The Polys is
    // its own Owner
    private const EObjectFlags BrushFlags = EObjectFlags.Transactional | EObjectFlags.LoadForClient | EObjectFlags.LoadForServer | EObjectFlags.LoadForEdit;

    /// <summary>
    /// The empty brush model that <paramref name="level"/>'s volumes other than <paramref name="volume"/> share, or a new one made as the cooker makes it if
    /// none does
    /// </summary>
    internal static ExportEntry EmptyBrush(ExportEntry level, ExportEntry volume)
    {
        var package = level.FileRef;
        foreach (var other in package.Exports.Where(e => e.Parent == level && e != volume && !e.IsDefaultObject && e.IsA("Volume")))
        {
            if (other.GetProperty<ObjectProperty>("Brush")?.ResolveToEntry(package) is ExportEntry { ClassName: "Model" } brush && brush.Parent == level
                && brush.GetBinaryData<Model>() is { Nodes.Length: 0 } shared
                && (shared.Polys == 0 || package.GetEntry(shared.Polys) is ExportEntry { ClassName: "Polys" } sharedPolys && sharedPolys.GetBinaryData<Polys>().Elements.Length == 0))
                return brush;
        }
        //(no NetIndex, as INDEX_NONE registers nothing)
        byte[] noNetIndex = BitConverter.GetBytes(-1);
        var polys = new ExportEntry(package, level, package.GetNextIndexedName("Polys"), noNetIndex)
        {
            Class = package.GetEntryOrAddImport("Engine.Polys", "Class"), ObjectFlags = BrushFlags
        };
        package.AddExport(polys);
        var polysBinary = Polys.Create();
        polysBinary.Owner = polys.UIndex;
        polys.WritePropertiesAndBinary([], polysBinary);
        var model = new ExportEntry(package, level, package.GetNextIndexedName("Model"), noNetIndex)
        {
            Class = package.GetEntryOrAddImport("Engine.Model", "Class"), ObjectFlags = BrushFlags
        };
        package.AddExport(model);
        var modelBinary = Model.Create();
        modelBinary.Self = model.UIndex;
        modelBinary.Polys = polys.UIndex;
        modelBinary.RootOutside = true;
        modelBinary.NumSharedSides = 4;
        modelBinary.LightingGuid = Guid.NewGuid();
        model.WritePropertiesAndBinary([], modelBinary);
        return model;
    }
}
