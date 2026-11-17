using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// One of a StaticLightCollectionActor's lights. Its transform is stored in the collection's binary
/// </summary>
public class StaticLightCollectionComponentProxy : CollectionActorComponentProxy, ILightProxy
{
    public EditableLight Light { get; }

    public StaticLightCollectionComponentProxy(IActorEditorContext context, ExportEntry lightComponent, StaticLightCollectionActor collection, int index, SceneLightType type)
        : base(context, collection, lightComponent, index)
    {
        Light = new EditableLight(this, lightComponent, type, context.PackageCache);
        Light.Changed += () =>
        {
            UpdateDirty();
            Editor?.OnLightEdited(this);
        };
    }

    public Matrix4x4? EditedOwnerTransform => IsTransformDirty ? LocalToWorld : null;

    protected override void UpdateLocalToWorld()
    {
        base.UpdateLocalToWorld();
        if (Light is not null) Editor?.OnLightEdited(this);
    }

    protected override bool HasMemberChanges() => base.HasMemberChanges() || Light.HasChanges;

    public override void MarkClean()
    {
        Light.MarkClean();
        base.MarkClean();
    }

    public override void Render(LevelEditorRenderContext context, RenderPass pass)
    {
        if (pass is RenderPass.Base) LightProxyVisuals.DrawIcon(context, this, Light);
    }

    public override BoxSphereBounds GetBounds() => LightActorProxy.LightBounds(Light.GetPlacement(LocalToWorld).Position);

    public override int HitPriority => IHitProxy.WireFramePriority;

    /// <summary>
    /// An edit of only the light component, such as committing its settings, is reread in place. Rebuilding it would rebuild its whole collection,
    /// discarding the other lights' uncommitted edits
    /// </summary>
    public override bool TryRefreshPreview(IMEPackage package, HashSet<int> updatedExports, bool force = false) =>
        LightProxyRefresh.TryRefresh(this, Light, package, updatedExports);
}
