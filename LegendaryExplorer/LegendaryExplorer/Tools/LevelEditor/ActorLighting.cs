using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

public readonly record struct ActorLightingSettings(int? LightEnvironment, uint Channels, bool AcceptsLights,
    bool AcceptsDynamicLights, bool CastShadow, bool CastDynamicShadow, bool UsePrecomputedShadows);

public sealed record LightEnvironmentOption(int? UIndex, string Name, bool IsSelectable = true);

public sealed class ActorLightingChannel : NotifyPropertyChangedBase
{
    private readonly EditableActorLighting Owner;
    internal int Bit { get; }
    public string Name { get; }

    internal ActorLightingChannel(EditableActorLighting owner, string name, int bit) => (Owner, Name, Bit) = (owner, name, bit);
    public bool IsChecked
    {
        get => (Owner.Settings.Channels & 1u << Bit) != 0;
        set => Owner.Apply(Owner.Settings with { Channels = value ? Owner.Settings.Channels | 1u << Bit : Owner.Settings.Channels & ~(1u << Bit) });
    }
    internal void Refresh() => OnPropertyChanged(nameof(IsChecked));
}

/// <summary>Pending primitive lighting edits. The package is only changed by Commit; mesh reloads overlay these edits.</summary>
public sealed class EditableActorLighting : NotifyPropertyChangedBase
{
    internal MeshComponentProxy Mesh { get; private set; }
    private readonly MeshRenderContext Context;
    private ActorLightingSettings settings, cleanSettings;
    private readonly PropertyCollection ChannelFields;
    private PropertyCollection SavedProperties;
    public ExportEntry Component => Mesh.Export;
    public string ComponentName => $"{Component.ObjectName.Instanced} (#{Component.UIndex})";
    public string ActorPropertyName => Mesh.ActorPropertyName ?? "Mesh component";
    public string MeshName
    {
        get
        {
            string propertyName = Component.IsA("SkeletalMeshComponent") ? "SkeletalMesh" : "StaticMesh";
            var (property, package) = EffectiveLightingProperties.Reference(Component, propertyName, Context.PackageCache);
            return property is { Value: not 0 }
                ? property.ResolveToEntry(package)?.ObjectName.Instanced ?? "Unresolved mesh"
                : "None";
        }
    }
    public bool IsReadOnly => Mesh.Actor is null || Mesh.Actor.IsReadOnly;
    internal bool IsOrphaned => Mesh.Actor is null || Mesh.Actor.IsDisposed;
    internal ActorId? OwnerId => Mesh.Actor?.Id;
    public ActorLightingSettings Settings => settings;
    public bool HasChanges => settings != cleanSettings;
    public IReadOnlyList<ActorLightingChannel> Channels { get; }
    public IReadOnlyList<LightEnvironmentOption> Environments { get; private set; }

    internal ExportEntry EnvironmentOwner => Component.Parent is ExportEntry owner && owner.IsA("Actor") ? owner : null;
    public bool CanCreateEnvironment => !IsReadOnly && !IsOrphaned && EffectiveLightingProperties.IsPlacedActor(EnvironmentOwner);
    public bool CanEditEnvironment => !IsReadOnly && !IsOrphaned && EditableEnvironment is not null;

    internal ExportEntry EditableEnvironment
    {
        get
        {
            if (!CanAssignEnvironment(settings.LightEnvironment)) return null;
            var effective = SavedProperties.DeepClone();
            foreach (var property in GetChangedProperties()) effective.AddOrReplaceProp(property);
            EffectiveLightingProperties.UsesLightEnvironment(Component, effective, EnvironmentOwner, Context.PackageCache, out var environment);
            // Never edit an inherited template or a dependency package through the actor inspector.
            return EnvironmentOwner is { } owner && environment?.FileRef == Component.FileRef && environment.Parent == owner
                && environment.IsA("LightEnvironmentComponent") ? environment : null;
        }
    }

    internal ExportEntry CreateEnvironment(bool bio)
    {
        if (!CanCreateEnvironment) return null;
        var environment = ActorLightEnvironment.Create(EnvironmentOwner, bio, Context.PackageCache);
        RefreshEnvironments();
        Apply(settings with { LightEnvironment = environment.UIndex });
        return environment;
    }

    internal EditableActorLighting(MeshComponentProxy mesh, MeshRenderContext context)
    {
        Mesh = mesh;
        Context = context;
        SavedProperties = mesh.Properties.DeepClone();
        settings = cleanSettings = Read(SavedProperties);
        try
        {
            ChannelFields = GlobalUnrealObjectInfo.getDefaultStructValue(Component.Game, "LightingChannelContainer", true, Component.FileRef);
        }
        catch { /* An unavailable struct definition leaves only the known serialized channels editable. */ }
        ChannelFields ??= mesh.Properties.GetProp<StructProperty>("LightingChannels")?.Properties.DeepClone();
        Channels = LightingChannels.Names.Select((name, bit) => (name, bit))
            .Where(channel => ChannelFields?.Any(field => field.Name.Instanced == channel.name) == true)
            .Select(channel => new ActorLightingChannel(this, channel.name, channel.bit)).ToArray();
        RefreshEnvironments();
    }

    /// <summary>Keeps the inspector and undo actions attached when a spline connection replaces its mesh preview.</summary>
    internal void Rebind(MeshComponentProxy mesh)
    {
        Mesh = mesh;
        ReloadAndApply(mesh.Properties);
    }

    private ActorLightingSettings Read(PropertyCollection props)
    {
        bool usesEnvironment = Context.ResolveLightEnvironment(Component, props, Mesh.Actor?.Export, out _);
        bool precomputed = props.GetProp<BoolProperty>("bUsePrecomputedShadows")?.Value ?? HasBakedLighting();
        return new ActorLightingSettings(props.GetProp<ObjectProperty>("LightEnvironment")?.Value,
            LightingChannels.FromProperty(props.GetProp<StructProperty>("LightingChannels"), default, false,
                Mesh is StaticMeshComponentProxy && !usesEnvironment ? LightingChannels.StaticPrimitiveDefault : LightingChannels.DynamicPrimitiveDefault).Channels,
            LightAssignment.AcceptsLights(props),
            props.GetProp<BoolProperty>("bAcceptsDynamicLights") is not { Value: false },
            props.GetProp<BoolProperty>("CastShadow") is not { Value: false },
            props.GetProp<BoolProperty>("bCastDynamicShadow") is not { Value: false }, precomputed);
    }

    private bool HasBakedLighting()
    {
        if (Mesh is not StaticMeshComponentProxy) return false;
        if (Component.Parent?.ClassName == "StaticMeshCollectionActor") return true;
        try
        {
            return Component.GetBinaryData<StaticMeshComponent>().LODData.Any(lod =>
                lod.LightMap is { LightMapType: not ELightMapType.LMT_None } || lod.ShadowMaps.Length > 0 || lod.ShadowVertexBuffers.Length > 0);
        }
        catch { return false; }
    }

    internal void RefreshEnvironments()
    {
        var options = new List<LightEnvironmentOption>();
        if (cleanSettings.LightEnvironment is null || settings.LightEnvironment is null)
            options.Add(new(null, "Inherited (archetype / class lighting)"));
        options.Add(new(0, "None (direct lighting)"));
        var owners = GetEnvironmentOwners();
        foreach (IEntry entry in Component.FileRef.Exports.Cast<IEntry>().Concat(Component.FileRef.Imports))
        {
            if (IsOwnedEnvironment(entry, owners))
                options.Add(new(entry.UIndex, $"#{entry.UIndex} {entry.InstancedFullPath}"));
        }
        if (settings.LightEnvironment is { } current && options.All(option => option.UIndex != current))
            options.Add(new(current, $"Invalid ownership or unresolved: #{current} {Component.FileRef.GetEntry(current)?.InstancedFullPath}", false));
        Environments = options;
        OnPropertyChanged(nameof(Environments));
        OnPropertyChanged(nameof(Environment));
        OnPropertyChanged(nameof(CanEditEnvironment));
    }

    public LightEnvironmentOption Environment
    {
        get => Environments.FirstOrDefault(option => option.UIndex == settings.LightEnvironment);
        set
        {
            if (value is { IsSelectable: true } && CanAssignEnvironment(value.UIndex))
                Apply(settings with { LightEnvironment = value.UIndex });
        }
    }

    // Only this actor's subobjects and those of its archetype chain are valid assignments.
    // Resolve imports for identity checks, but keep all selectable indices in the mesh's package.
    private HashSet<ExportEntry> GetEnvironmentOwners()
    {
        var owners = new HashSet<ExportEntry>();
        var owner = EnvironmentOwner;
        while (owner is not null && owners.Add(owner))
        {
            try
            {
                owner = EffectiveLightingProperties.Resolve(owner.Archetype, Context.PackageCache)
                    ?? EffectiveLightingProperties.ClassDefault(owner, Context.PackageCache);
            }
            catch { break; } // An unavailable archetype does not make unrelated components valid.
        }
        return owners;
    }

    private bool IsOwnedEnvironment(IEntry entry, HashSet<ExportEntry> owners)
    {
        if (entry is null || !entry.IsA("LightEnvironmentComponent")) return false;
        try
        {
            var environment = EffectiveLightingProperties.Resolve(entry, Context.PackageCache);
            return environment is { IsDefaultObject: false, Parent: ExportEntry owner } && owners.Contains(owner);
        }
        catch { return false; } // An unresolved import cannot be offered as an assignment.
    }

    private bool CanAssignEnvironment(int? index) => index is null ? cleanSettings.LightEnvironment is null
        : index == 0 || IsOwnedEnvironment(Component.FileRef.GetEntry(index.Value), GetEnvironmentOwners());
    public bool AcceptsLights { get => settings.AcceptsLights; set => Apply(settings with { AcceptsLights = value }); }
    public bool AcceptsDynamicLights { get => settings.AcceptsDynamicLights; set => Apply(settings with { AcceptsDynamicLights = value }); }
    public bool CastShadow { get => settings.CastShadow; set => Apply(settings with { CastShadow = value }); }
    public bool CastDynamicShadow { get => settings.CastDynamicShadow; set => Apply(settings with { CastDynamicShadow = value }); }
    public bool UsePrecomputedShadows { get => settings.UsePrecomputedShadows; set => Apply(settings with { UsePrecomputedShadows = value }); }

    public void Apply(ActorLightingSettings value)
    {
        if (IsReadOnly || IsOrphaned || value == settings) return;
        // Undo may restore the original reference, even when the file already contained an invalid assignment.
        if (value.LightEnvironment != settings.LightEnvironment && value.LightEnvironment != cleanSettings.LightEnvironment
            && !CanAssignEnvironment(value.LightEnvironment)) return;
        var before = settings;
        settings = value;
        // Resolve runtime defaults before recording history, so environment changes and undo agree with the preview.
        var effective = SavedProperties.DeepClone();
        foreach (var property in GetChangedProperties()) effective.AddOrReplaceProp(property);
        settings = Read(effective);
        if (settings == before) return;
        NotifySettings();
        Mesh.Actor.OnActorLightingEdited(Mesh, before, settings);
    }

    private void NotifySettings()
    {
        OnPropertyChanged(string.Empty);
        foreach (var channel in Channels) channel.Refresh();
    }

    public List<Property> GetChangedProperties()
    {
        var result = new List<Property>();
        var s = settings;
        var c = cleanSettings;
        if (s.LightEnvironment != c.LightEnvironment && s.LightEnvironment is { } environment)
            result.Add(new ObjectProperty(environment, "LightEnvironment"));
        if (s.Channels != c.Channels)
        {
            var fields = ChannelFields.DeepClone();
            fields.AddOrReplaceProp(new BoolProperty(true, "bInitialized"));
            foreach (var channel in Channels)
                //a channel such as Unnamed_1 is the name Unnamed with a number, as the struct's field is: from the string itself it'd be another name,
                //added alongside the field rather than replacing it
                fields.AddOrReplaceProp(new BoolProperty((s.Channels & 1u << channel.Bit) != 0, NameReference.FromInstancedString(channel.Name)));
            result.Add(new StructProperty("LightingChannelContainer", fields, "LightingChannels",
                GlobalUnrealObjectInfo.IsImmutable("LightingChannelContainer", Component.Game)));
        }
        if (s.AcceptsLights != c.AcceptsLights) result.Add(new BoolProperty(s.AcceptsLights, "bAcceptsLights"));
        if (s.AcceptsDynamicLights != c.AcceptsDynamicLights) result.Add(new BoolProperty(s.AcceptsDynamicLights, "bAcceptsDynamicLights"));
        if (s.CastShadow != c.CastShadow) result.Add(new BoolProperty(s.CastShadow, "CastShadow"));
        if (s.CastDynamicShadow != c.CastDynamicShadow) result.Add(new BoolProperty(s.CastDynamicShadow, "bCastDynamicShadow"));
        if (s.UsePrecomputedShadows != c.UsePrecomputedShadows) result.Add(new BoolProperty(s.UsePrecomputedShadows, "bUsePrecomputedShadows"));
        return result;
    }

    /// <summary>Reread external edits, keeping only fields with pending local edits.</summary>
    internal void ReloadAndApply(PropertyCollection properties)
    {
        var pending = GetChangedProperties();
        SavedProperties = properties.DeepClone();
        cleanSettings = Read(properties);
        foreach (var property in pending) properties.AddOrReplaceProp(property);
        settings = Read(properties);
        RefreshEnvironments();
        NotifySettings();
        Mesh.Actor?.RefreshLightingDirty();
    }

    /// <summary>Writes and accepts the pending edits</summary>
    public void Commit()
    {
        Write();
        MarkClean();
    }

    /// <summary>
    /// Writes the pending edits to <paramref name="component"/> (by default the mesh's own), leaving them pending until <see cref="MarkClean"/>,
    /// so an abandoned commit leaves them as they were. A spline connection's new mesh is written to the export the commit creates for it
    /// </summary>
    internal void Write(ExportEntry component = null)
    {
        component ??= Component;
        var changed = GetChangedProperties();
        if (changed.Count is 0) return;
        if (settings.LightEnvironment != cleanSettings.LightEnvironment && !CanAssignEnvironment(settings.LightEnvironment))
            throw new InvalidOperationException("The light environment must belong to this actor or its archetype.");
        if (settings.LightEnvironment != cleanSettings.LightEnvironment && settings.LightEnvironment is > 0
            && EnvironmentOwner is { } owner && component.FileRef.TryGetUExport(settings.LightEnvironment.Value, out var environment)
            && environment.Parent == owner && environment.IsA("LightEnvironmentComponent"))
        {
            ActorLightEnvironment.Register(owner, environment);
            //the actor writes its own properties too (a commit may do it after this), so it must not write its older Components back
            Mesh.Actor?.OnComponentsWritten(owner);
        }
        var props = component.GetProperties();
        foreach (var property in changed) props.AddOrReplaceProp(property);
        component.WriteProperties(props);
    }

    /// <summary>Accepts the written edits as the saved settings</summary>
    internal void MarkClean()
    {
        SavedProperties = EffectiveLightingProperties.ReadPrimitive(Component, Context.PackageCache);
        cleanSettings = settings;
        RefreshEnvironments();
        NotifySettings();
    }
}

/// <summary>Creates actor-owned components; registration is deferred until the assignment is committed.</summary>
internal static class ActorLightEnvironment
{
    internal static ExportEntry Create(ExportEntry owner, bool bio, PackageCache cache = null)
    {
        if (!EffectiveLightingProperties.IsPlacedActor(owner) || !owner.IsA("Actor"))
            throw new ArgumentException("A light environment must belong to a placed actor.", nameof(owner));
        string className = bio ? "BioDynamicLightEnvironmentComponent" : "DynamicLightEnvironmentComponent";
        var environment = owner.FileRef.CreateExport(className, className, owner, prePropBinary: new byte[8], cache: cache);
        environment.WriteProperties(new PropertyCollection { new BoolProperty(true, "bEnabled") });
        return environment;
    }

    internal static void Register(ExportEntry owner, ExportEntry environment)
    {
        var properties = owner.GetProperties();
        var components = properties.GetProp<ArrayProperty<ObjectProperty>>("Components") ?? new ArrayProperty<ObjectProperty>("Components");
        if (components.Any(component => component.Value == environment.UIndex)) return;
        components.Add(new ObjectProperty(environment));
        properties.AddOrReplaceProp(components);
        owner.WriteProperties(properties);
    }
}

public sealed class ActorLightingAction(EditableActorLighting lighting, ActorLightingSettings before, ActorLightingSettings after) : IUndoAction
{
    public string Description => $"Edit {lighting.ComponentName} lighting";
    public bool IsOrphaned => lighting.IsOrphaned;
    //a model with no actor isn't known to be independent of anything
    public IReadOnlyCollection<ActorId> Subjects => lighting.OwnerId is { } owner ? [owner] : null;
    public void Undo() => lighting.Apply(before);
    public void Redo() => lighting.Apply(after);
}
