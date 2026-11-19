using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// A light actor (PointLight, SpotLight, DirectionalLight, SkyLight, and their movable, toggleable and dominant variants)
/// </summary>
public class LightActorProxy : ActorProxy, ILightProxy
{
    public EditableLight Light { get; }

    /// <summary>
    /// The light classes placed on their own, with one light component: not StaticLightCollectionActors (whose lights are their components),
    /// dominant lights or sky lights
    /// </summary>
    internal static IReadOnlyList<string> StandaloneClasses { get; } = ["PointLight", "PointLightMovable", "PointLightToggleable", "SpotLight",
        "SpotLightMovable", "SpotLightToggleable", "DirectionalLight", "DirectionalLightToggleable"];

    //a draft's: the light component its own is made from, its class default's, or for a copy, its saved actor's (its commit clones it)
    private readonly ExportEntry componentTemplate;

    /// <summary>
    /// A new standalone light (a draft). Its light is drawn as its commit makes its component: with <paramref name="componentTemplate"/>'s values
    /// and new GUIDs. Its package is untouched until then
    /// </summary>
    internal LightActorProxy(IActorEditorContext context, ActorRecord draft, PropertyCollection properties, ExportEntry componentTemplate)
        : base(context, draft, properties)
    {
        IsHidden = false;
        if (SceneLight.GetLightType(componentTemplate) is not { } type)
            throw new ArgumentException($"{componentTemplate.InstancedFullPath} isn't a light that previews support.", nameof(componentTemplate));
        this.componentTemplate = componentTemplate;
        // Its references are indices into the template's package. The standalone classes' templates have none (ActorCreationGameDataTests)
        var values = EffectiveLightingProperties.ReadValues(componentTemplate, context.PackageCache);
        //shadow-maps and light-maps refer to it by these
        values.AddOrReplaceProp(CommonStructs.GuidProp(Guid.NewGuid(), "LightGuid"));
        values.AddOrReplaceProp(CommonStructs.GuidProp(Guid.NewGuid(), "LightmapGuid"));
        // Stands for its component (identifying its light in the scene) without being added to the package, as a draft mesh's does
        var standIn = new ExportEntry(Pcc, null, "None", new byte[8], binary: LightComponent.Create());
        Light = new EditableLight(this, standIn, type, context.PackageCache, values);
        Light.Changed += OnLightChanged;
        //a draft is wholly uncommitted
        UpdateDirty();
    }

    private protected override bool WritesAllValuesWhenCreated => true;

    /// <summary>
    /// A copy's light is drawn from its source's component's values: its settings are written onto its copy of that component, with its cached owner
    /// transform, since the copy is elsewhere. Its GUIDs are the ones it was drawn with, which replace those its commit made it
    /// (<see cref="ActorCloning.Materialize"/>), so it's the same light before its commit as after
    /// </summary>
    private protected override void CommitCloneEdits(IReadOnlyDictionary<ExportEntry, ExportEntry> copies)
    {
        var component = copies.GetValueOrDefault(componentTemplate)
            ?? throw new InvalidOperationException($"{ActorName} has no copy of {componentTemplate.ObjectName.Instanced}.");
        var drawn = Light.CopyDraftValues();
        var properties = component.GetProperties();
        properties.AddOrReplaceProp(drawn.GetProp<StructProperty>("LightGuid"));
        properties.AddOrReplaceProp(drawn.GetProp<StructProperty>("LightmapGuid"));
        component.WriteProperties(properties);
        Light.Commit(LocalToWorld, component);
    }

    public LightActorProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        //bHidden doesn't stop a light from lighting, and lights aren't otherwise drawn
        IsHidden = false;
        if (GetLightComponent(actorExport) is not { } component || SceneLight.GetLightType(component) is not { } type)
        {
            throw new ArgumentException($"{actorExport.InstancedFullPath} has no light that previews support. Check {nameof(HasSupportedLight)} first");
        }
        Light = new EditableLight(this, component, type, context.PackageCache);
        Light.Changed += OnLightChanged;
    }

    /// <summary>
    /// The light component, as <see cref="SceneLight.LoadLevelLights"/> finds it
    /// </summary>
    internal static ExportEntry GetLightComponent(ExportEntry actorExport) =>
        actorExport.GetProperty<ObjectProperty>("LightComponent")?.ResolveToEntry(actorExport.FileRef) as ExportEntry;

    /// <summary>
    /// Whether the actor has a light component that previews support
    /// </summary>
    public static bool HasSupportedLight(ExportEntry actorExport) =>
        GetLightComponent(actorExport) is { } component && SceneLight.GetLightType(component) is not null;

    private void OnLightChanged()
    {
        UpdateDirty();
        Editor?.OnLightEdited(this);
    }

    public Matrix4x4? EditedOwnerTransform => IsTransformDirty ? LocalToWorld : null;

    protected override void UpdateLocalToWorld()
    {
        base.UpdateLocalToWorld();
        //null while the base constructor runs
        if (Light is not null) Editor?.OnLightEdited(this);
    }

    protected override bool HasUncommittedChanges() => base.HasUncommittedChanges() || Light.HasChanges;

    public override void MarkClean()
    {
        Light.MarkClean();
        base.MarkClean();
    }

    public override void CommitChanges(PackageCache packageCache = null)
    {
        //(a copy's component is its copy of its source's)
        ThrowIfCopy();
        // Its export was just made (Materialize): its component is made with it, as shipping levels' are (see NewComponent), with an empty
        // LightComponent binary. The actor refers to it as its LightComponent
        if (Materialized && componentTemplate is not null)
        {
            var component = NewComponent(componentTemplate, Light.NewComponentProperties(), LightComponent.Create());
            Properties.AddOrReplaceProp(new ObjectProperty(component, "LightComponent"));
            base.CommitChanges(packageCache);
            return;
        }
        bool moved = IsTransformDirty;
        if (moved)
        {
            base.CommitChanges(packageCache);
        }
        Light.Commit(moved ? LocalToWorld : null);
    }

    public override void Render(LevelEditorRenderContext context, RenderPass pass)
    {
        // Prefab light helpers are revealed with their selected owner and use its pick identity.
        if (pass is RenderPass.Base && (Editor is not null || context.IsActorSelected(this)))
            LightProxyVisuals.DrawIcon(context, this, Light);
    }

    public override BoxSphereBounds GetBounds() => LightBounds(Light.GetPlacement(LocalToWorld).Position);

    internal static BoxSphereBounds LightBounds(Vector3 position) => new()
    {
        Origin = position,
        BoxExtent = new Vector3(64),
        SphereRadius = 64
    };

    public override int HitPriority => IHitProxy.WireFramePriority;

    public override bool TestUIndexes(HashSet<int> uIndexes) => base.TestUIndexes(uIndexes) || uIndexes.Contains(Light.Component.UIndex);

    /// <summary>
    /// An edit of only the light component is reread in place, so the proxy (and its selection and undo history) survives it.
    /// Edits of the actor itself rebuild it
    /// </summary>
    public override bool TryRefreshPreview(IMEPackage package, HashSet<int> updatedExports, bool force = false) =>
        LightProxyRefresh.TryRefresh(this, Light, package, updatedExports);
}
