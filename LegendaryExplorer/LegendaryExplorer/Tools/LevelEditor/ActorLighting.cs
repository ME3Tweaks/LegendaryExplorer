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

public sealed record LightEnvironmentOption(int? UIndex, string Name);

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
    private readonly MeshComponentProxy Mesh;
    private readonly MeshRenderContext Context;
    private ActorLightingSettings settings, cleanSettings;
    private readonly PropertyCollection ChannelFields;
    private PropertyCollection SavedProperties;
    public ExportEntry Component => Mesh.Export;
    public string ComponentName => $"{Component.ObjectName.Instanced} (#{Component.UIndex})";
    public bool IsReadOnly => Mesh.Actor is null || Mesh.Actor.IsReadOnly;
    internal bool IsOrphaned => Mesh.Actor is null || Mesh.Actor.IsDisposed;
    public ActorLightingSettings Settings => settings;
    public bool HasChanges => settings != cleanSettings;
    public IReadOnlyList<ActorLightingChannel> Channels { get; }
    public IReadOnlyList<LightEnvironmentOption> Environments { get; private set; }

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

    private ActorLightingSettings Read(PropertyCollection props)
    {
        bool usesEnvironment = Context.ResolveLightEnvironment(Component, props, Mesh.Actor?.Export, out _);
        bool precomputed = props.GetProp<BoolProperty>("bUsePrecomputedShadows")?.Value ?? HasBakedLighting();
        return new ActorLightingSettings(props.GetProp<ObjectProperty>("LightEnvironment")?.Value,
            LightingChannels.FromProperty(props.GetProp<StructProperty>("LightingChannels"), default, false,
                Mesh is StaticMeshComponentProxy && !usesEnvironment ? LightingChannels.StaticPrimitiveDefault : LightingChannels.DynamicPrimitiveDefault).Channels,
            props.GetProp<BoolProperty>("bAcceptsLights") is not { Value: false },
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
        if (settings.LightEnvironment is null) options.Add(new(null, "Default (class lighting)"));
        options.Add(new(0, "None (direct lighting)"));
        foreach (IEntry entry in Component.FileRef.Exports.Cast<IEntry>().Concat(Component.FileRef.Imports))
        {
            if (entry is ExportEntry { IsDefaultObject: true }) continue;
            if (entry.ClassName == "LightEnvironmentComponent" || GlobalUnrealObjectInfo.IsA(entry.ClassName, "LightEnvironmentComponent", Component.Game))
                options.Add(new(entry.UIndex, $"#{entry.UIndex} {entry.InstancedFullPath}"));
        }
        if (settings.LightEnvironment is { } current && options.All(option => option.UIndex != current))
            options.Add(new(current, $"#{current} {Component.FileRef.GetEntry(current)?.InstancedFullPath ?? "Unresolved reference"}"));
        Environments = options;
        OnPropertyChanged(nameof(Environments));
        OnPropertyChanged(nameof(Environment));
    }

    public LightEnvironmentOption Environment
    {
        get => Environments.FirstOrDefault(option => option.UIndex == settings.LightEnvironment);
        set { if (value is not null) Apply(settings with { LightEnvironment = value.UIndex }); }
    }
    public bool AcceptsLights { get => settings.AcceptsLights; set => Apply(settings with { AcceptsLights = value }); }
    public bool AcceptsDynamicLights { get => settings.AcceptsDynamicLights; set => Apply(settings with { AcceptsDynamicLights = value }); }
    public bool CastShadow { get => settings.CastShadow; set => Apply(settings with { CastShadow = value }); }
    public bool CastDynamicShadow { get => settings.CastDynamicShadow; set => Apply(settings with { CastDynamicShadow = value }); }
    public bool UsePrecomputedShadows { get => settings.UsePrecomputedShadows; set => Apply(settings with { UsePrecomputedShadows = value }); }

    public void Apply(ActorLightingSettings value)
    {
        if (IsReadOnly || IsOrphaned || value == settings) return;
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
                fields.AddOrReplaceProp(new BoolProperty((s.Channels & 1u << channel.Bit) != 0, channel.Name));
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
        if (!Environments.Any(option => option.UIndex == settings.LightEnvironment)) RefreshEnvironments();
        NotifySettings();
        Mesh.Actor?.RefreshLightingDirty();
    }

    public void Commit()
    {
        var changed = GetChangedProperties();
        if (changed.Count > 0)
        {
            var props = Component.GetProperties();
            foreach (var property in changed) props.AddOrReplaceProp(property);
            Component.WriteProperties(props);
        }
        SavedProperties = EffectiveLightingProperties.ReadPrimitive(Component, Context.PackageCache);
        cleanSettings = settings;
        RefreshEnvironments();
        NotifySettings();
    }
}

public sealed class ActorLightingAction(EditableActorLighting lighting, ActorLightingSettings before, ActorLightingSettings after) : IUndoAction
{
    public string Description => $"Edit {lighting.ComponentName} lighting";
    public bool IsOrphaned => lighting.IsOrphaned;
    public void Undo() => lighting.Apply(before);
    public void Redo() => lighting.Apply(after);
}
