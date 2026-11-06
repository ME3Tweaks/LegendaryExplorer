using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>Shared defaults for lighting. References are resolved in their declaring package, never copied as foreign indices.</summary>
internal static class EffectiveLightingProperties
{
    internal static ExportEntry Resolve(IEntry entry, PackageCache cache) => entry switch
    {
        ExportEntry export => export,
        ImportEntry import => cache is PreviewPackageCache preview ? preview.ResolveImport(import) : EntryImporter.ResolveImport(import, cache),
        _ => null
    };

    /// <summary>
    /// Each package cache's class default objects and their properties, which every object of a class reads. Read once, until
    /// <see cref="InvalidateDefaults"/> says the cache's packages changed. Guarded by itself
    /// </summary>
    private sealed class ClassDefaults
    {
        public readonly Dictionary<IEntry, ExportEntry> Objects = [];
        public readonly Dictionary<ExportEntry, PropertyCollection> Properties = [];
        //how many times they were forgotten, so that what was read before isn't added back after
        public int Generation;
    }

    private static readonly ConditionalWeakTable<PackageCache, ClassDefaults> DefaultsByCache = new();

    /// <summary>Forgets the class defaults read through the cache, after any of the packages they come from (or that resolve imports) changed</summary>
    public static void InvalidateDefaults(PackageCache cache)
    {
        if (cache is null || !DefaultsByCache.TryGetValue(cache, out var defaults)) return;
        lock (defaults)
        {
            defaults.Objects.Clear();
            defaults.Properties.Clear();
            defaults.Generation++;
        }
    }

    internal static ExportEntry ClassDefault(ExportEntry export, PackageCache cache) => ClassDefaultOf(export.Class, cache);

    /// <summary>The class default object of <paramref name="type"/>, which may be another package's class (a draft's)</summary>
    internal static ExportEntry ClassDefault(IEntry type, PackageCache cache) => ClassDefaultOf(type, cache);

    private static ExportEntry ClassDefaultOf(IEntry type, PackageCache cache) =>
        type is not null ? Cached(cache, d => d.Objects, type, () => Resolve(type, cache) is { } resolved
            ? resolved.FileRef.FindExport("Default__" + resolved.ObjectName.Instanced) : null) : null;

    /// <summary>The class default object's properties, read once per package cache. Never modified: callers copy what they take</summary>
    private static PropertyCollection ClassDefaultProperties(ExportEntry cdo, PackageCache cache) =>
        Cached(cache, d => d.Properties, cdo, () => cdo.GetCondensedProperties(cache, resolveImports: true, mergeStructs: true));

    /// <summary>
    /// The cache's value for the key, read outside the lock (it reads packages) if there's none. A value read while the cache was invalidated isn't kept
    /// </summary>
    private static TValue Cached<TKey, TValue>(PackageCache cache, Func<ClassDefaults, Dictionary<TKey, TValue>> table, TKey key, Func<TValue> read)
    {
        if (cache is null) return read();
        var defaults = DefaultsByCache.GetValue(cache, static _ => new ClassDefaults());
        int generation;
        lock (defaults)
        {
            if (table(defaults).TryGetValue(key, out var known)) return known;
            generation = defaults.Generation;
        }
        var value = read();
        lock (defaults)
        {
            if (defaults.Generation == generation) table(defaults)[key] = value;
        }
        return value;
    }

    /// <summary>A property the object's class default object has, for one the object and its archetypes don't set. Null if it has none</summary>
    public static T ClassDefaultProperty<T>(ExportEntry export, string name, PackageCache cache) where T : Property =>
        ClassDefaultProperty<T>(export.Class, export, name, cache);

    /// <summary>The same for an object of class <paramref name="type"/> that may have no export yet (a draft); <paramref name="export"/> is its export, if any</summary>
    public static T ClassDefaultProperty<T>(IEntry type, ExportEntry export, string name, PackageCache cache) where T : Property =>
        ClassDefaultOf(type, cache) is { } cdo && cdo != export ? ClassDefaultProperties(cdo, cache).GetProp<T>(name)?.DeepClone() as T : null;

    public static PropertyCollection Read(ExportEntry export, PackageCache cache) => Read(export, cache, static _ => true);

    /// <summary>
    /// <see cref="Read"/> without the properties that hold references, which are indices into the object's own package: the values an object of
    /// another package (a draft) takes from it, its template
    /// </summary>
    internal static PropertyCollection ReadValues(ExportEntry export, PackageCache cache)
    {
        var props = Read(export, cache);
        props.RemoveAll(ContainsReference);
        return props;
    }

    public static PropertyCollection ReadPrimitive(ExportEntry export, PackageCache cache) =>
        Read(export, cache, static p => p.Name.Name is "LightingChannels" or "bAcceptsLights" or "bAcceptsDynamicLights"
            or "bUsePrecomputedShadows" or "CastShadow" or "bCastDynamicShadow" or "bCastHiddenShadow"
            or "bCastShadowAsTwoSided" or "bSelfShadowOnly" or "LightmassSettings" or "HiddenGame");

    private static PropertyCollection Read(ExportEntry export, PackageCache cache, Func<Property, bool> inherited)
    {
        var props = export.GetCondensedProperties(cache, resolveImports: true, mergeStructs: true);
        if (ClassDefault(export, cache) is { } cdo && cdo != export)
            Merge(props, ClassDefaultProperties(cdo, cache), inherited);
        return props;
    }

    //Defaults are copied in, since they're cached and callers may modify what they get
    private static void Merge(PropertyCollection target, PropertyCollection defaults, Func<Property, bool> inherited)
    {
        foreach (var prop in defaults)
        {
            if (!inherited(prop)) continue;
            var existing = target.GetProp<Property>(prop.Name, prop.StaticArrayIndex);
            if (existing is StructProperty { IsImmutable: false } t && prop is StructProperty { IsImmutable: false } d && t.StructType == d.StructType)
                Merge(t.Properties, d.Properties, static _ => true);
            else if (existing == null && !ContainsReference(prop)) target.Add(prop.DeepClone());
        }
    }

    private static bool ContainsReference(Property prop) => prop switch
    {
        ObjectProperty or DelegateProperty => true,
        StructProperty s => s.Properties.Any(ContainsReference),
        ImmutableByteArrayProperty => false,
        ArrayPropertyBase a => a.Properties.Any(ContainsReference),
        _ => false
    };

    public static (ObjectProperty Property, IMEPackage Package) Reference(ExportEntry export, string name, PackageCache cache, PropertyCollection effective = null) =>
        Inherited<ObjectProperty>(export, name, cache, effective);

    /// <summary>
    /// A property holding references, as the object has it or inherits it from its archetypes or class defaults, with the package its references are
    /// indices into. Condensed properties leave out other packages' references, which would be wrong in the object's package
    /// </summary>
    /// <param name="effective">The object's properties with any uncommitted override, which wins</param>
    public static (T Property, IMEPackage Package) Inherited<T>(ExportEntry export, string name, PackageCache cache, PropertyCollection effective = null)
        where T : Property
    {
        if (effective?.GetProp<T>(name) is { } overridden) return (overridden, export.FileRef);
        var visited = new HashSet<ExportEntry>();
        var current = export;
        while (current != null && visited.Add(current))
        {
            if (current.GetProperty<T>(name) is { } property) return (property, current.FileRef);
            current = Resolve(current.Archetype, cache);
        }
        current = ClassDefault(export, cache);
        while (current != null && visited.Add(current))
        {
            if (current.GetProperty<T>(name) is { } property) return (property, current.FileRef);
            current = Resolve(current.Archetype, cache);
        }
        return default;
    }

    public static bool HasReference(ExportEntry export, string name, PackageCache cache, PropertyCollection effective = null) => Reference(export, name, cache, effective).Property is { Value: not 0 };

    public static ExportEntry ResolveReference(ExportEntry export, string name, PackageCache cache, PropertyCollection effective = null)
    {
        var (property, package) = Reference(export, name, cache, effective);
        return property == null ? null : Resolve(property.ResolveToEntry(package), cache);
    }

    /// <summary>
    /// Whether a primitive is lit by a light environment, which the level's lights then don't affect directly (ULightComponent::AffectsPrimitive
    /// ignores a disabled one). Shared by the preview and the bake, so they agree on which instance's bEnabled counts.
    /// Characters whose light environment can't be found (it's set by class defaults that can't be read) are taken to have one.
    /// </summary>
    /// <param name="effective">The component's properties, including inherited ones and any uncommitted override, including an explicit None</param>
    /// <param name="owner">The actor that owns the component</param>
    /// <param name="environment">The LightEnvironment component used. Null for a character with its class's default one</param>
    /// <returns>False if the light environment can't be read, so the primitive is lit like any other</returns>
    public static bool UsesLightEnvironment(ExportEntry component, PropertyCollection effective, ExportEntry owner, PackageCache cache, out ExportEntry environment)
    {
        environment = null;
        try
        {
            var (property, package) = Reference(component, "LightEnvironment", cache);
            if (effective.GetProp<ObjectProperty>("LightEnvironment") is { } edited)
                (property, package) = (edited, component.FileRef);
            if (property is not { Value: not 0 })
                return property is null && owner is not null && IsCharacterClass(owner);
            environment = Resolve(property.ResolveToEntry(package), cache);
            //A light environment that belongs to another actor placed in a level is deliberately shared with it. Any other that isn't the actor's own
            //was inherited from an archetype (at any depth) or class defaults, and belongs to the actor like everything else it inherits: it has its own instance
            if (owner is not null && environment is not null && environment.Parent != owner && !IsPlacedActor(environment.Parent)
                && FindInstancedComponent(owner, environment) is { } own)
            {
                environment = own;
            }
            //one that can't be found (an import whose package isn't available) is a character's class default one, as above; anything else is lit
            //like other meshes, rather than losing the level's lights
            if (environment is null) return owner is not null && IsCharacterClass(owner);
            return Read(environment, cache).GetProp<BoolProperty>("bEnabled") is not { Value: false };
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Could not resolve the light environment of {component.InstancedFullPath}, so it's lit like other meshes: {e.Message}");
            environment = null;
            return false;
        }

        //Pawns, stunt actors and SFXSkeletalMeshActors have a BioDynamicLightEnvironmentComponent in their class defaults
        static bool IsCharacterClass(ExportEntry actor) =>
            GlobalUnrealObjectInfo.IsA(actor.ClassName, "Pawn", actor.Game)
            || GlobalUnrealObjectInfo.IsA(actor.ClassName, "SFXStuntActor", actor.Game)
            || GlobalUnrealObjectInfo.IsA(actor.ClassName, "SFXSkeletalMeshActor", actor.Game);

        //the actor's instance of a template component: its subobject of the same name, or else the first of the same class
        static ExportEntry FindInstancedComponent(ExportEntry actor, ExportEntry template)
        {
            ExportEntry sameClass = null;
            foreach (ExportEntry child in actor.GetChildren<ExportEntry>())
            {
                if (child.ClassName != template.ClassName)
                {
                    continue;
                }
                if (child.ObjectName == template.ObjectName)
                {
                    return child;
                }
                sameClass ??= child;
            }
            return sameClass;
        }
    }

    /// <summary>An actor in a level (whose outer is the level), rather than an archetype or class default object</summary>
    internal static bool IsPlacedActor(IEntry entry) => entry is ExportEntry { IsDefaultObject: false, Parent: ExportEntry { ClassName: "Level" } };
}
