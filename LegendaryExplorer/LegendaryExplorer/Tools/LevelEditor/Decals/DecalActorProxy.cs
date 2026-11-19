using System;
using System.Collections.Generic;
using System.Numerics;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>Editable static decal projector, with an uncommitted receiver-mesh preview.</summary>
public sealed class DecalActorProxy : EditorActorProxy
{
    private readonly DecalComponentProxy decal;
    internal TransformSnapshot ProjectionBaseline;
    public EditableDecal Decal { get; }

    //a material is written only when it's changed (one inherited from an archetype in another package isn't), and by a placed decal's commit. A copy's is
    //its source's, which its clone has, inherited or not
    internal override IEnumerable<AssetReference> AssetsToCopy =>
        Decal is { Settings.Material: { } material } && (IsDraft && CloneSource is null || Decal.MaterialChanged) ? [material] : [];

    internal override Action UseCopiedAssets(Func<AssetReference, AssetReference> copyOf)
    {
        var settings = Decal.Settings;
        if (settings.Material is not { } material) return () => { };
        Decal.Restore(settings with { Material = copyOf(material) });
        return () => Decal.Restore(settings);
    }
    public bool CanReproject => decal?.EditUnsupportedReason is null && decal is not null;
    public string DecalEditingStatus => decal?.EditingStatus ?? "No decal component.";
    public override string Category => "Decals";
    internal override AssetReference DraftAsset => IsDraft && CloneSource is null ? Decal?.Settings.Material : null;
    public int SortOrder => decal?.SortOrder ?? 0;
    public IMEPackage ProjectionPackage => decal?.ProjectionPackage ?? Export.FileRef;
    internal DecalComponentProxy DecalComponent => decal;
    public override ActorCoverage Coverage => decal?.GetCoverage() ?? base.Coverage;
    internal void BindReceivers(LevelEditorRenderContext context) => decal?.BindReceivers(context);

    private static readonly ActorClassInfo Info = new(ActorKind.Decal, "Effects helpers", "Decal projector helper; no usable cooked surface.");

    //a draft's: the class default's component its own is made from, or a copy's, its saved source's, which its commit clones
    private readonly ExportEntry componentTemplate;

    /// <summary>
    /// A new decal (a draft) drawing <paramref name="material"/>, an entry of its package or another file's material. Its component is drawn as the commit
    /// makes it: from <paramref name="componentTemplate"/>'s values, projected onto the meshes of its own file in its box wherever it's put. Its package is
    /// untouched until then. A copy starts with its source's saved component's settings and Filter: its box is its source's saved width, height and planes,
    /// scaled as its source's is from where it was saved (<see cref="ProjectionBaseline"/>), and its frame is worked out from where it's put
    /// </summary>
    /// <param name="keepsFilter">Whether it has its template's Filter: not a copy in another level, since the actors its source's lists are its source's level's</param>
    internal DecalActorProxy(IActorEditorContext context, ActorRecord draft, PropertyCollection properties, AssetReference material, ExportEntry componentTemplate,
        bool keepsFilter = true)
        : base(context, draft, properties, Info)
    {
        this.componentTemplate = componentTemplate;
        ProjectionBaseline = SnapshotTransform();
        var values = EffectiveLightingProperties.ReadValues(componentTemplate, context.PackageCache);
        if (!keepsFilter) values.RemoveNamedProperty("FilterMode");
        // None already exists in every package. Pending properties bypass serialization, so even the package's name table stays untouched
        var preview = new ExportEntry(Pcc, null, "None", new byte[8], binary: LegendaryExplorerCore.Unreal.BinaryConverters.DecalComponent.Create());
        decal = new DecalComponentProxy(context.RenderContext, preview, this, values, material)
        {
            ActorPropertyName = nameof(Decal)
        };
        Components.Add(decal);
        // (one without a Filter has its stand-in's, which has none)
        Decal = new EditableDecal(this, decal.Properties, material, filterFrom: keepsFilter ? componentTemplate : null);
    }

    public DecalActorProxy(IActorEditorContext context, ExportEntry export)
        : base(context, export, Info)
    {
        ProjectionBaseline = SnapshotTransform();
        var component = EffectiveLightingProperties.ResolveReference(export, "Decal", context.PackageCache, Properties);
        if (component is not null)
        {
            decal = new DecalComponentProxy(context.RenderContext, component, this);
            Components.Add(decal);
            Decal = new EditableDecal(this, decal.Properties, EditableDecal.MaterialOf(component, context.PackageCache));
        }
    }

    public override void Render(LevelEditorRenderContext context, RenderPass pass)
    {
        base.Render(context, pass);
        if (pass == RenderPass.Base && context.IsActorSelected(this)) decal?.DrawProjector(context, Matrix4x4.Identity);
        if (pass == RenderPass.Decals && (context.IsActorSelected(this)
            || context.GetCategoryState("Decals").IsVisible && (!IsHidden || context.ShowHidden)))
            decal?.Render(context, pass);
    }

    public override BoxSphereBounds GetBounds() => decal?.SurfaceBounds ?? base.GetBounds();

    public override void UpdateScene(LevelEditorRenderContext context, float deltaTime)
    {
        base.UpdateScene(context, deltaTime);
        decal?.EnsureProjection(context);
    }

    protected override bool HasUncommittedChanges() => base.HasUncommittedChanges() || Decal?.HasChanges == true
        || decal?.HasReprojectedReceivers == true || decal?.HasReceiverChanges == true;
    internal void RefreshDecalDirty()
    {
        decal?.UpdateNeededMembers();
        // Which meshes the Filter can list depends on where the projection lands
        Decal?.RefreshFilterCandidateStates();
        UpdateDirty();
        NotifyCoverageChanged();
        OnPropertyChanged(nameof(DecalEditingStatus));
    }
    internal void OnDecalEdited(DecalSettings before, DecalSettings after, string property)
    {
        RefreshDecalDirty();
        Editor?.OnDecalEdited(this, before, after, property);
    }
    public void PrepareDecalCommit()
    {
        if (IsReadOnly || decal is null) return;
        decal.EnsureProjection(Editor.RenderContext);
        if (IsDirty) decal.ValidateCommit();
    }
    public override void CommitChanges(PackageCache packageCache = null)
    {
        // A copy is written by CommitClone, onto its clone of its source's component: never here, where a new one would be made for it
        ThrowIfCopy();
        // Its export was just made (Materialize): its component is made with it, as shipping levels' decals' are (see NewComponent), with its material,
        // projection and receivers (CommitProjection). The actor refers to it as its Decal, with no Components array
        if (Materialized && componentTemplate is not null)
        {
            // (in a copied actor's level, it lands on nothing)
            bool captured = OwningFile is { IsCapture: true };
            if (!captured) PrepareDecalCommit();
            var component = NewComponent(componentTemplate, [new ObjectProperty(Decal.Settings.Material.EntryIn(Pcc), "DecalMaterial")],
                LegendaryExplorerCore.Unreal.BinaryConverters.DecalComponent.Create());
            if (captured) decal.CommitUnprojected(component);
            else decal.CommitProjection(component);
            Properties.AddOrReplaceProp(new ObjectProperty(component, nameof(Decal)));
            base.CommitChanges(packageCache);
            return;
        }
        if (IsReadOnly || !IsDirty) return;
        PrepareDecalCommit();
        if (ProjectionPackage != Export.FileRef)
            throw new InvalidOperationException("Commit this decal through the Level Editor to move it to its receiving package.");
        decal?.CommitProjection();
        base.CommitChanges(packageCache);
    }
    /// <summary>
    /// A copy's component is its clone of its source's, whose receivers are where its source is: they're replaced with its own projection's, as a new decal's
    /// are written (<see cref="DecalComponentProxy.CommitProjection"/>), with its settings and Filter. Its material is its source's unless it's been changed
    /// </summary>
    private protected override void CommitCloneEdits(IReadOnlyDictionary<ExportEntry, ExportEntry> copies)
    {
        var component = copies.GetValueOrDefault(componentTemplate)
            ?? throw new InvalidOperationException($"{ActorName} has no copy of {componentTemplate.ObjectName.Instanced}.");
        if (OwningFile is { IsCapture: true })
        {
            decal.CommitUnprojected(component);
            return;
        }
        PrepareDecalCommit();
        decal.CommitProjection(component);
    }

    private protected override void OnPendingDeletionChanged() => decal?.UpdateNeededMembers();
    //(as a draft that's undone or deleted is)
    private protected override void OnOutOfSceneChanged() => decal?.UpdateNeededMembers();
    /// <summary>Lets go of what a commit deleted, which it took out of this decal's receivers and Filter (<see cref="ActorDeletionPlan"/>)</summary>
    internal void ForgetDeleted(IReadOnlySet<IEntry> deleted) => decal?.ForgetDeleted(deleted);
    public override void MarkClean()
    {
        decal?.AcceptProjection();
        ProjectionBaseline = SnapshotTransform();
        base.MarkClean();
    }

    protected override void UpdateLocalToWorld()
    {
        base.UpdateLocalToWorld();
        if (decal is not null) NotifyCoverageChanged();
    }

    public override bool TryRefreshPreview(IMEPackage package, HashSet<int> exports, bool force = false)
    {
        if (!IsDraft && (package == Pcc && exports.Contains(Export.UIndex)
            || decal is not null && package == decal.Export.FileRef && exports.Contains(decal.Export.UIndex))) return false;
        // An edit of its material is redrawn in place too: a refresh in place keeps it from being made again from its export (and a draft has none)
        bool changed = decal?.Refresh(package, exports, force) == true;
        changed |= decal?.RedrawIfMaterialChanged(package, exports) == true;
        if (changed) NotifyCoverageChanged();
        return changed;
    }
}
