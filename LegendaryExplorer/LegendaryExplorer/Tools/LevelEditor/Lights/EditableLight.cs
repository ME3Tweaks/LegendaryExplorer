using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// The light component settings the level editor edits, as UE3 resolves them: including inherited values, and LightComponent's defaults for unset ones
/// </summary>
public readonly record struct LightSettings(
    bool Enabled,
    float Brightness,
    byte R, byte G, byte B, byte A,
    float Radius,
    float FalloffExponent,
    float InnerConeAngle,
    float OuterConeAngle,
    uint Channels,
    bool CastShadows,
    bool CastStaticShadows,
    bool CastDynamicShadows)
{
    /// <param name="props">The component's properties, including inherited ones (condensed, with merged structs)</param>
    public static LightSettings Read(PropertyCollection props)
    {
        StructProperty color = props.GetProp<StructProperty>("LightColor");
        byte ColorChannel(string name) => color?.GetProp<ByteProperty>(name)?.Value ?? 255;
        //the same defaults SceneLight uses
        return new LightSettings(
            props.GetProp<BoolProperty>("bEnabled") is not { Value: false },
            props.GetProp<FloatProperty>("Brightness")?.Value ?? 1,
            ColorChannel("R"), ColorChannel("G"), ColorChannel("B"), ColorChannel("A"),
            props.GetProp<FloatProperty>("Radius")?.Value ?? 1024,
            props.GetProp<FloatProperty>("FalloffExponent")?.Value ?? 2,
            props.GetProp<FloatProperty>("InnerConeAngle")?.Value ?? 0,
            props.GetProp<FloatProperty>("OuterConeAngle")?.Value ?? 44,
            LightingChannels.FromProperty(props.GetProp<StructProperty>("LightingChannels"), LightingChannels.LightDefault, isInitialized: true,
                LightingChannels.LightDefault).Channels,
            props.GetProp<BoolProperty>("CastShadows") is not { Value: false },
            props.GetProp<BoolProperty>("CastStaticShadows") is not { Value: false },
            props.GetProp<BoolProperty>("CastDynamicShadows") is not { Value: false });
    }
}

/// <summary>
/// One lighting channel of an <see cref="EditableLight"/>, for a checkbox
/// </summary>
public sealed class LightChannelOption : NotifyPropertyChangedBase
{
    private readonly EditableLight Light;
    internal int Bit { get; }
    public string Name { get; }

    internal LightChannelOption(EditableLight light, string name, int bit)
    {
        Light = light;
        Name = name;
        Bit = bit;
    }

    public bool IsChecked
    {
        get => (Light.Settings.Channels & 1u << Bit) != 0;
        set => Light.Apply(Light.Settings with { Channels = value ? Light.Settings.Channels | 1u << Bit : Light.Settings.Channels & ~(1u << Bit) });
    }

    internal void Refresh() => OnPropertyChanged(nameof(IsChecked));
}

/// <summary>
/// A light component's settings as edited in the level editor. Edits aren't written to the package until they're committed;
/// until then, previews use them in place of the saved values (see <see cref="GetPreviewOverride"/>)
/// </summary>
public sealed class EditableLight : NotifyPropertyChangedBase
{
    private readonly ActorProxy Owner;
    public ExportEntry Component { get; }
    public SceneLightType Type { get; }

    private readonly PackageCache PackageCache;
    //the component's translation relative to its owner, which SceneLight applies
    private Vector3 ComponentTranslation;
    //the component's resolved LightingChannels, whose layout is kept when the channels are written
    private StructProperty ResolvedChannels;

    private LightSettings settings;
    private LightSettings cleanSettings;

    /// <summary>
    /// The current (possibly uncommitted) settings
    /// </summary>
    public LightSettings Settings => settings;

    public bool HasChanges => settings != cleanSettings;

    public IReadOnlyList<LightChannelOption> Channels { get; }

    /// <summary>
    /// Raised after the settings change, including by undo/redo
    /// </summary>
    internal event Action Changed;

    //a draft's: all of its component's values until a commit makes it. Null for a component's
    private readonly PropertyCollection draftValues;

    /// <param name="draftValues">For a draft, whose <paramref name="component"/> only stands for the one its commit makes: that component's values</param>
    internal EditableLight(ActorProxy owner, ExportEntry component, SceneLightType type, PackageCache packageCache, PropertyCollection draftValues = null)
    {
        Owner = owner;
        Component = component;
        Type = type;
        PackageCache = packageCache;
        this.draftValues = draftValues;
        settings = cleanSettings = ReadComponent();
        //only offer the channels this game's struct has, so that every checked channel can be saved.
        //Matched by instanced name, since names like Unnamed_3 are stored as a name and a number
        HashSet<string> structFields = ChannelFieldsByGame.GetOrAdd(component.Game,
            _ => DefaultChannelStruct()?.Select(field => field.Name.Instanced).ToHashSet());
        Channels = LightingChannels.Names
            .Select((name, bit) => (name, bit))
            .Where(channel => structFields is null || structFields.Contains(channel.name))
            .Select(channel => new LightChannelOption(this, channel.name, channel.bit))
            .ToArray();
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<MEGame, HashSet<string>> ChannelFieldsByGame = new();

    /// <returns>The component's saved settings</returns>
    private LightSettings ReadComponent()
    {
        PropertyCollection props = draftValues ?? EffectiveLightingProperties.Read(Component, PackageCache);
        ComponentTranslation = props.GetProp<StructProperty>("Translation") is { } translation ? CommonStructs.GetVector3(translation) : Vector3.Zero;
        ResolvedChannels = props.GetProp<StructProperty>("LightingChannels");
        return LightSettings.Read(props);
    }

    /// <summary>
    /// Rereads the component after it's written, by a commit or another tool. Uncommitted edits are kept, now relative to the new saved settings
    /// </summary>
    internal void Reload()
    {
        bool hadChanges = HasChanges;
        cleanSettings = ReadComponent();
        if (hadChanges)
        {
            OnPropertyChanged(nameof(HasChanges));
            Changed?.Invoke();
        }
        else if (settings != cleanSettings)
        {
            SetSettings(cleanSettings);
        }
    }

    private PropertyCollection DefaultChannelStruct()
    {
        try
        {
            return GlobalUnrealObjectInfo.getDefaultStructValue(Component.Game, "LightingChannelContainer", true, Component.FileRef);
        }
        catch
        {
            return null;
        }
    }

    #region Bindable settings

    public bool IsReadOnly => Owner.IsReadOnly;

    /// <summary>
    /// Its proxy was replaced or closed
    /// </summary>
    internal bool IsOrphaned => Owner.IsDisposed;

    /// <summary>The proxy whose light this is</summary>
    internal ActorProxy OwnerActor => Owner;
    public bool HasRadius => Type is SceneLightType.Point or SceneLightType.Spot;
    public bool IsSpot => Type is SceneLightType.Spot;
    public string TypeName => Type switch
    {
        SceneLightType.Point => "Point light",
        SceneLightType.Spot => "Spot light",
        SceneLightType.Directional => "Directional light",
        SceneLightType.Sky => "Sky light",
        _ => Type.ToString()
    };

    public bool IsEnabled
    {
        get => settings.Enabled;
        set => Apply(settings with { Enabled = value });
    }

    public float Brightness
    {
        get => settings.Brightness;
        set => Apply(settings with { Brightness = value });
    }

    public System.Windows.Media.Color Color
    {
        get => System.Windows.Media.Color.FromRgb(settings.R, settings.G, settings.B);
        set => Apply(settings with { R = value.R, G = value.G, B = value.B });
    }

    public float Radius
    {
        get => settings.Radius;
        set => Apply(settings with { Radius = Math.Max(0, value) });
    }

    public float FalloffExponent
    {
        get => settings.FalloffExponent;
        set => Apply(settings with { FalloffExponent = Math.Max(0.01f, value) });
    }

    /// <summary>
    /// Degrees. Raising it past the outer angle raises the outer angle with it
    /// </summary>
    public float InnerConeAngle
    {
        get => settings.InnerConeAngle;
        set
        {
            value = Math.Clamp(value, 0, 89);
            Apply(settings with { InnerConeAngle = value, OuterConeAngle = Math.Max(value, settings.OuterConeAngle) });
        }
    }

    /// <summary>
    /// Degrees. Lowering it past the inner angle lowers the inner angle with it
    /// </summary>
    public float OuterConeAngle
    {
        get => settings.OuterConeAngle;
        set
        {
            value = Math.Clamp(value, 0, 89);
            Apply(settings with { OuterConeAngle = value, InnerConeAngle = Math.Min(value, settings.InnerConeAngle) });
        }
    }

    public bool CastShadows
    {
        get => settings.CastShadows;
        set => Apply(settings with { CastShadows = value });
    }

    public bool CastStaticShadows
    {
        get => settings.CastStaticShadows;
        set => Apply(settings with { CastStaticShadows = value });
    }

    public bool CastDynamicShadows
    {
        get => settings.CastDynamicShadows;
        set => Apply(settings with { CastDynamicShadows = value });
    }

    private static readonly string[] SettingNames =
    [
        nameof(Settings), nameof(IsEnabled), nameof(Brightness), nameof(Color), nameof(Radius), nameof(FalloffExponent),
        nameof(InnerConeAngle), nameof(OuterConeAngle), nameof(CastShadows), nameof(CastStaticShadows), nameof(CastDynamicShadows), nameof(HasChanges)
    ];

    #endregion

    /// <summary>
    /// Changes the settings, unless the owner is read-only. Used by the bindable properties, gizmos, and undo/redo
    /// </summary>
    public void Apply(LightSettings value)
    {
        if (value == settings || Owner.IsReadOnly) return;
        SetSettings(value);
    }

    private void SetSettings(LightSettings value)
    {
        settings = value;
        foreach (string name in SettingNames) OnPropertyChanged(name);
        foreach (LightChannelOption channel in Channels) channel.Refresh();
        Changed?.Invoke();
    }

    /// <summary>
    /// Where the light is, and the direction a spot or directional light shines in, for an owner transform. Matches <see cref="SceneLight"/>
    /// </summary>
    public (Vector3 Position, Vector3 Direction) GetPlacement(Matrix4x4 ownerTransform)
    {
        var axis = new Vector3(ownerTransform.M11, ownerTransform.M12, ownerTransform.M13);
        return (Vector3.Transform(ComponentTranslation, ownerTransform), axis.LengthSquared() > 1e-8f ? Vector3.Normalize(axis) : Vector3.UnitX);
    }

    /// <summary>
    /// The edited component properties: only those that differ from the saved settings
    /// </summary>
    public List<Property> GetChangedProperties()
    {
        LightSettings s = settings, c = cleanSettings;
        var changed = new List<Property>();
        if (s.Enabled != c.Enabled) changed.Add(new BoolProperty(s.Enabled, "bEnabled"));
        if (s.Brightness != c.Brightness) changed.Add(new FloatProperty(s.Brightness, "Brightness"));
        if ((s.R, s.G, s.B, s.A) != (c.R, c.G, c.B, c.A))
            changed.Add(CommonStructs.ColorProp(System.Drawing.Color.FromArgb(s.A, s.R, s.G, s.B), "LightColor"));
        if (s.Radius != c.Radius) changed.Add(new FloatProperty(s.Radius, "Radius"));
        if (s.FalloffExponent != c.FalloffExponent) changed.Add(new FloatProperty(s.FalloffExponent, "FalloffExponent"));
        if (s.InnerConeAngle != c.InnerConeAngle) changed.Add(new FloatProperty(s.InnerConeAngle, "InnerConeAngle"));
        if (s.OuterConeAngle != c.OuterConeAngle) changed.Add(new FloatProperty(s.OuterConeAngle, "OuterConeAngle"));
        if (s.Channels != c.Channels) changed.Add(CreateChannelsProperty(s.Channels));
        if (s.CastShadows != c.CastShadows) changed.Add(new BoolProperty(s.CastShadows, "CastShadows"));
        if (s.CastStaticShadows != c.CastStaticShadows) changed.Add(new BoolProperty(s.CastStaticShadows, "CastStaticShadows"));
        if (s.CastDynamicShadows != c.CastDynamicShadows) changed.Add(new BoolProperty(s.CastDynamicShadows, "CastDynamicShadows"));
        return changed;
    }

    /// <summary>
    /// A complete LightingChannels struct, so that no channel is inherited from an archetype
    /// </summary>
    private StructProperty CreateChannelsProperty(uint channels)
    {
        //the game's struct, so that the fields and their order match its definition
        PropertyCollection fields = DefaultChannelStruct();
        if (fields is null)
        {
            fields = [new BoolProperty(true, "bInitialized")];
            foreach (LightChannelOption channel in Channels)
            {
                //Unnamed_1 is the name Unnamed with a number, as the game's field is
                fields.Add(new BoolProperty(false, NameReference.FromInstancedString(channel.Name)));
            }
        }
        foreach (BoolProperty field in fields.OfType<BoolProperty>())
        {
            string name = field.Name.Instanced;
            if (name == "bInitialized")
            {
                field.Value = true;
            }
            else if (Channels.FirstOrDefault(channel => channel.Name == name) is { } channel)
            {
                field.Value = (channels & 1u << channel.Bit) != 0;
            }
        }
        string structType = ResolvedChannels?.StructType ?? "LightingChannelContainer";
        return new StructProperty(structType, fields, "LightingChannels", GlobalUnrealObjectInfo.IsImmutable(structType, Component.Game));
    }

    /// <summary>
    /// Uncommitted edits for previews. Null if there are none
    /// </summary>
    /// <param name="editedOwnerTransform">The owner's edited transform, or null if it isn't edited</param>
    public LightPreviewOverride GetPreviewOverride(Matrix4x4? editedOwnerTransform) =>
        editedOwnerTransform is null && !HasChanges ? null : new LightPreviewOverride(editedOwnerTransform, GetChangedProperties());

    /// <summary>Whether <see cref="Component"/> only stands for the component a draft's commit makes</summary>
    internal bool IsDraft => draftValues is not null;

    /// <summary>For a draft: a copy of its component's values, without its uncommitted settings (<see cref="GetChangedProperties"/>)</summary>
    internal PropertyCollection CopyDraftValues() => draftValues.DeepClone();

    /// <summary>For a draft: the properties its commit makes its component with, its GUIDs and the settings it changed from its template's</summary>
    internal PropertyCollection NewComponentProperties() =>
        [draftValues.GetProp<StructProperty>("LightGuid"), draftValues.GetProp<StructProperty>("LightmapGuid"), .. GetChangedProperties()];

    /// <summary>
    /// For a copy's light, drawn from its source's saved values (a draft's): takes on the settings <paramref name="source"/>, its source's light, is shown
    /// with, pending on the copy relative to the same saved values (as <see cref="EditableActorLighting.Seed"/> does)
    /// </summary>
    internal void Seed(EditableLight source)
    {
        if (!IsDraft) throw new InvalidOperationException("Only a copy's light starts from another's settings.");
        if (source.cleanSettings != cleanSettings) throw new InvalidOperationException("The light's settings can't be copied: its saved values aren't its source's.");
        SetSettings(source.Settings);
    }

    /// <summary>
    /// Writes the edited settings to the component. They're still pending until <see cref="MarkClean"/>, so an abandoned commit leaves them as they were
    /// </summary>
    /// <param name="parentToWorld">If the owner was moved, its new transform, which updates the component's cached copy of it (if it has one)</param>
    /// <param name="component">For a copy's light (a draft's): its copy of its source's component, whose saved values its settings are relative to</param>
    public void Commit(Matrix4x4? parentToWorld, ExportEntry component = null)
    {
        if (IsDraft && component is null) throw new InvalidOperationException("A new light's component is made by its actor's commit.");
        component ??= Component;
        List<Property> changed = GetChangedProperties();
        PropertyCollection props = component.GetProperties();
        if (parentToWorld is { } transform && props.GetProp<StructProperty>("CachedParentToWorld") is not null)
        {
            changed.Add(CommonStructs.MatrixProp(transform, "CachedParentToWorld"));
        }
        if (changed.Count > 0)
        {
            foreach (Property property in changed)
            {
                props.AddOrReplaceProp(property);
            }
            component.WriteProperties(props);
        }
    }

    public void MarkClean()
    {
        cleanSettings = settings;
        OnPropertyChanged(nameof(HasChanges));
    }

    /// <summary>
    /// For the viewport: the light's color at full brightness, gamma-encoded, or gray if it's disabled
    /// </summary>
    public Vector4 DisplayColor
    {
        get
        {
            if (!settings.Enabled) return new Vector4(0.45f, 0.45f, 0.45f, 1);
            float max = Math.Max(settings.R, Math.Max(settings.G, settings.B));
            return max <= 0 ? new Vector4(0.2f, 0.2f, 0.2f, 1) : new Vector4(settings.R / max, settings.G / max, settings.B / max, 1);
        }
    }

    internal static IEnumerable<string> ChangedFieldNames(LightSettings before, LightSettings after)
    {
        if (before.Enabled != after.Enabled) yield return "Enabled";
        if (before.Brightness != after.Brightness) yield return "Brightness";
        if ((before.R, before.G, before.B, before.A) != (after.R, after.G, after.B, after.A)) yield return "Color";
        if (before.Radius != after.Radius) yield return "Radius";
        if (before.FalloffExponent != after.FalloffExponent) yield return "Falloff";
        if (before.InnerConeAngle != after.InnerConeAngle) yield return "Inner Cone";
        if (before.OuterConeAngle != after.OuterConeAngle) yield return "Outer Cone";
        if (before.Channels != after.Channels) yield return "Channels";
        if (before.CastShadows != after.CastShadows || before.CastStaticShadows != after.CastStaticShadows || before.CastDynamicShadows != after.CastDynamicShadows)
            yield return "Shadows";
    }
}

/// <summary>
/// An edit of a light's settings. Consecutive edits of the same settings of one light, such as dragging a color or a value, are merged
/// </summary>
public class LightSettingsAction : IMergeableUndoAction
{
    private readonly ActorRef Owner;
    private readonly LightSettings Before;
    private LightSettings After;
    private DateTime LastEdit;
    private readonly string[] ChangedFields;

    public string Description { get; }
    public IEnumerable<AssetReference> Assets => [];

    public LightSettingsAction(EditableLight light, LightSettings before, LightSettings after, string actorName)
    {
        Owner = new ActorRef(light.OwnerActor);
        Before = before;
        After = after;
        LastEdit = DateTime.UtcNow;
        ChangedFields = EditableLight.ChangedFieldNames(before, after).ToArray();
        Description = $"Edit {actorName} {string.Join(", ", ChangedFields)}";
    }

    //the light of whichever view draws its actor now
    private EditableLight Light => ((ILightProxy)Owner.View).Light;
    public void Undo() => Light.Apply(Before);
    public void Redo() => Light.Apply(After);
    public bool IsOrphaned => Owner.IsOrphaned;
    public IReadOnlyCollection<ActorId> Subjects => [Owner.Id];

    public bool TryMerge(IUndoAction next)
    {
        if (next is not LightSettingsAction edit || !edit.Owner.RefersTo(Owner) || edit.Before != After || (edit.LastEdit - LastEdit).TotalSeconds > 1
            || !edit.ChangedFields.SequenceEqual(ChangedFields))
            return false;
        After = edit.After;
        LastEdit = edit.LastEdit;
        return true;
    }
}
