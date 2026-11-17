using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using ClassFlags = LegendaryExplorerCore.Unreal.UnrealFlags.EClassFlags;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// How new actors of <see cref="Classes"/> are made. Each is a class whose placed actors are known to be made this way, drawn by a view that writes all their
/// values: there's no general recipe, and a class that isn't listed can't be placed
/// </summary>
/// <param name="Category">Where its classes are listed for placing</param>
/// <param name="Classes">The exact classes it makes: a subclass needs checking against it first</param>
/// <param name="Draft">Makes the view of a new one, drawn and edited with nothing written until a commit</param>
internal sealed record ActorRecipe(PlacementCategory Category, IReadOnlyList<string> Classes, ActorRecipe.MakeDraft Draft)
{
    /// <summary>What each of <see cref="Classes"/> is, in a line, for the list it's placed from</summary>
    internal required IReadOnlyDictionary<string, string> Descriptions { get; init; }

    /// <param name="asset">The <see cref="Asset"/> chosen for it, an entry of its package or another file's, if it needs one</param>
    /// <param name="template">The subobject of its class default that <see cref="ComponentProperty"/> refers to, if it has one</param>
    internal delegate ActorProxy MakeDraft(IActorEditorContext editor, ActorRecord record, PropertyCollection properties, AssetReference asset, ExportEntry template);

    /// <summary>The kind of asset to choose before placing one, or null if it needs none</summary>
    internal AssetKind? Asset { get; init; }
    /// <summary>The class of <see cref="Asset"/>, or null</summary>
    /// <summary>The property of its class default that refers to the component its own is made from, or null if it has none</summary>
    internal string ComponentProperty { get; init; }
    /// <summary>What else that component has to be, if anything</summary>
    internal Func<ExportEntry, bool> AcceptsComponent { get; init; }
    internal bool IsSpline { get; init; }
    /// <summary>
    /// Whether a new one faces the surface it's placed on, backed off it a unit, as UE3's editor places decals (UEditorEngine::UseActorFactory's
    /// bUseSurfaceOrientation, for DecalActorBase's subclasses, and UActorFactoryDecal::CreateActor)
    /// </summary>
    internal bool FacesSurface { get; init; }
    /// <summary>What a new one has of its own besides its location, rotation and Tag, made afresh for each, if anything</summary>
    internal Func<IEnumerable<Property>> InitialProperties { get; init; }
}

/// <summary>
/// New actors made from their class's defaults, placed as drafts: drawn and edited with nothing written until a commit creates their exports
/// (<see cref="ActorProxy.Materialize"/>). Only classes with a recipe can be placed
/// </summary>
public static class ActorCreation
{
    private static ActorClassInfo MarkerInfo(ActorRecord record) => ActorClassification.Get(record.Class.ObjectName.Name, record.Package.Game);

    /// <summary>
    /// A new actor has its location and its Tag (its class's name, as UE3's UWorld::SpawnActor sets it) and nothing else of its own. Spline points start
    /// unconnected, and a loft with no DeformMesh until one is assigned. A static mesh actor has the mesh chosen for it, and an emitter the particle
    /// system, in a component made from its class default's (<see cref="ComponentTemplate"/>), and a standalone light has its class default's light. A volume
    /// is a box (<see cref="BrushProxy.NewBoxSize"/>), made as cooked volumes are: a hull, cached PhysX data if it has rigid-body collision, and the level's
    /// empty brush
    /// </summary>
    /// <remarks>In the order they're listed for placing</remarks>
    private static readonly ActorRecipe[] Recipes =
    [
        new(PlacementCategory.StaticMeshes, ["StaticMeshActor"],
            (editor, record, properties, mesh, template) => new StaticMeshActorProxy(editor, record, properties, mesh, template))
        {
            Asset = AssetKind.StaticMesh, ComponentProperty = "StaticMeshComponent",
            Descriptions = new Dictionary<string, string> { ["StaticMeshActor"] = "A mesh fixed in place, lit by the level's baked lighting" }
        },
        new(PlacementCategory.Dynamics, ["InterpActor"],
            (editor, record, properties, mesh, template) => new DynamicSMActorProxy(editor, record, properties, mesh, template))
        {
            Asset = AssetKind.StaticMesh, ComponentProperty = "StaticMeshComponent",
            Descriptions = new Dictionary<string, string> { ["InterpActor"] = "A mesh Matinee and Kismet can move, such as a door. Lit at runtime unless bUsePrecomputedShadows is set" }
        },
        new(PlacementCategory.Lights, LightActorProxy.StandaloneClasses, (editor, record, properties, _, template) => new LightActorProxy(editor, record, properties, template))
        {
            ComponentProperty = "LightComponent", AcceptsComponent = light => SceneLight.GetLightType(light) is not null,
            Descriptions = new Dictionary<string, string>
            {
                ["PointLight"] = "Shines in every direction. Baked, and can't change in game",
                ["PointLightMovable"] = "Shines in every direction. Can move in game, so it lights and shadows at runtime",
                ["PointLightToggleable"] = "Shines in every direction. Kismet can turn it on and off; its shadows are baked",
                ["SpotLight"] = "A cone of light. Baked, and can't change in game",
                ["SpotLightMovable"] = "A cone of light. Can move in game, so it lights and shadows at runtime",
                ["SpotLightToggleable"] = "A cone of light. Kismet can turn it on and off; its shadows are baked",
                ["DirectionalLight"] = "Light from one direction across the level, as sunlight. Baked, and can't change in game",
                ["DirectionalLightToggleable"] = "Light from one direction across the level, as sunlight. Kismet can turn it on and off; its shadows are baked"
            }
        },
        new(PlacementCategory.Volumes, ["BlockingVolume", "TriggerVolume", "BioTriggerVolume", "LightmassImportanceVolume"],
            (editor, record, properties, _, template) => new BrushProxy(editor, record, properties, template))
        {
            ComponentProperty = "BrushComponent", AcceptsComponent = component => component.ClassName == "BrushComponent",
            Descriptions = new Dictionary<string, string>
            {
                ["BlockingVolume"] = "An invisible box that blocks players and physics",
                ["TriggerVolume"] = "An invisible box that fires Kismet's Touched events",
                ["BioTriggerVolume"] = "A trigger volume that can be disabled, or set to fire once (Enabled, OneShot)",
                ["LightmassImportanceVolume"] = "Where a lighting bake spends its detail: bounced light is worked out finely inside it, coarsely outside. Nothing in game"
            }
        },
        // Placed disabled, so that it changes nothing until it's set up: its class default's Settings override every setting (each bOverride_ is set) with
        // the engine's stock values, and the camera takes the settings of the highest-priority enabled volume it's in (AWorldInfo::GetPostProcessSettings)
        new(PlacementCategory.Volumes, ["PostProcessVolume"], (editor, record, properties, _, template) => new BrushProxy(editor, record, properties, template))
        {
            ComponentProperty = "BrushComponent", AcceptsComponent = component => component.ClassName == "BrushComponent",
            InitialProperties = () => [new BoolProperty(false, "bEnabled")],
            Descriptions = new Dictionary<string, string>
            {
                ["PostProcessVolume"] = "Changes post-processing (bloom, depth of field, color) for a camera inside it, once enabled (bEnabled). The highest Priority wins"
            }
        },
        new(PlacementCategory.Effects, ["Emitter"],
            (editor, record, properties, system, template) => new EmitterDraftProxy(editor, record, properties, MarkerInfo(record), system, template))
        {
            Asset = AssetKind.ParticleSystem, ComponentProperty = "ParticleSystemComponent",
            Descriptions = new Dictionary<string, string> { ["Emitter"] = "A particle effect" }
        },
        new(PlacementCategory.Effects, ["DecalActor"],
            (editor, record, properties, material, template) => new DecalActorProxy(editor, record, properties, material, template))
        {
            Asset = AssetKind.DecalMaterial, ComponentProperty = "Decal", AcceptsComponent = component => component.ClassName == "DecalComponent", FacesSurface = true,
            Descriptions = new Dictionary<string, string> { ["DecalActor"] = "A material projected onto the static meshes in its box, as a stain or a sign" }
        },
        new(PlacementCategory.Splines, ["SplineActor", "SplineLoftActor"], (editor, record, properties, _, _) => new SplineActorProxy(editor, record, properties))
        {
            IsSpline = true,
            Descriptions = new Dictionary<string, string>
            {
                ["SplineActor"] = "A spline point, to connect to others",
                ["SplineLoftActor"] = "A spline point that bends a mesh along its connections"
            }
        },
        new(PlacementCategory.Markers, ["Note", "TargetPoint"], (editor, record, properties, _, _) => new EditorActorProxy(editor, record, properties, MarkerInfo(record)))
        {
            Descriptions = new Dictionary<string, string>
            {
                ["Note"] = "Text for other editors. Not shown in game",
                ["TargetPoint"] = "A position for Kismet and scripts to use. Not shown in game"
            }
        }
    ];

    /// <summary>The classes with a recipe, as they're listed for placing</summary>
    public static IReadOnlyList<PlaceableClass> PlaceableClasses { get; } =
        [.. Recipes.SelectMany(recipe => recipe.Classes.Select(c => new PlaceableClass(c, recipe.Category, recipe.Descriptions[c], recipe.Asset)))];

    /// <summary>The classes with a recipe</summary>
    public static IReadOnlyList<string> RecipeClasses { get; } = [.. PlaceableClasses.Select(placeable => placeable.ClassName)];

    private static ActorRecipe RecipeFor(string className) => Recipes.FirstOrDefault(recipe => recipe.Classes.Contains(className));

    /// <summary>The class of the asset to choose for a new <paramref name="className"/>, or null if it needs none</summary>

    /// <summary>The kind of asset to choose for a new <paramref name="className"/>, or null if it needs none</summary>
    public static AssetKind? AssetKindOf(string className) => RecipeFor(className)?.Asset;

    /// <summary>Why a new <paramref name="className"/> can't be placed in <paramref name="file"/> now, or null</summary>
    public static string PlacementRefusal(OpenLevelFile file, string className, PackageCache cache) => PlacementRefusal(file, className, cache, out _);

    private static string PlacementRefusal(OpenLevelFile file, string className, PackageCache cache, out IEntry actorClass) =>
        Refusal(file, className, cache, out actorClass) ?? EligibilityRefusal(actorClass, cache);

    /// <summary>
    /// Why the editor wouldn't place a new <paramref name="actorClass"/>, or null: UEditorEngine::AddActor refuses a class that's Abstract, Transient or not
    /// Placeable, and UWorld::SpawnActor one that's Deprecated. Navigation points are left to Pathfinding Editor. These are the concrete class's own
    /// flags, since Abstract isn't inherited. It's no check on an upgraded collection member, which is an actor the game already has
    /// </summary>
    public static string EligibilityRefusal(IEntry actorClass, PackageCache cache)
    {
        string name = actorClass.ObjectName.Instanced;
        try
        {
            if (EffectiveLightingProperties.Resolve(actorClass, cache) is not { IsClass: true } resolved) return $"{name}'s class couldn't be found.";
            var flags = ObjectBinary.From<UClass>(resolved).ClassFlags;
            if (!flags.HasFlag(ClassFlags.Placeable)) return $"{name} isn't placeable.";
            if (flags.HasFlag(ClassFlags.Abstract)) return $"{name} is abstract.";
            if (flags.HasFlag(ClassFlags.Transient)) return $"{name} is transient in {resolved.Game}.";
            if (flags.HasFlag(ClassFlags.Deprecated)) return $"{name} is deprecated.";
            if (resolved.InheritsFrom("NavigationPoint")) return $"{name} is a navigation point: use Pathfinding Editor.";
            return null;
        }
        catch (Exception error) when (error is not OutOfMemoryException) { return $"{name}'s class couldn't be read: {error.Message}"; }
    }

    /// <summary>Why a new <paramref name="className"/> couldn't be made in <paramref name="file"/> now, or null: all upgrading a collection's member needs</summary>
    public static string Refusal(OpenLevelFile file, string className, PackageCache cache) => Refusal(file, className, cache, out _);

    /// <param name="actorClass">Its class, if it can be made</param>
    internal static string Refusal(OpenLevelFile file, string className, PackageCache cache, out IEntry actorClass)
    {
        actorClass = null;
        if (RecipeFor(className) is not { } recipe) return $"There's no recipe for a new {className}.";
        if (FileRefusal(file) is { } refusal) return refusal;
        try { actorClass = FindClass(file.Package, className, cache); }
        catch (Exception error) when (error is not OutOfMemoryException) { return $"Its class couldn't be read: {error.Message}"; }
        if (actorClass is null) return $"{file.Package.Game} has no {className} class.";
        if (recipe.ComponentProperty is { } property
            && (ComponentTemplate(actorClass, property, cache) is not { } template || recipe.AcceptsComponent?.Invoke(template) == false))
            return $"{className}'s class default has no {property} to make its own from.";
        return ImportRefusal(actorClass, file.Package);
    }

    /// <summary>
    /// The subobject of <paramref name="actorClass"/>'s class default that its <paramref name="property"/> refers to: the template a new actor's
    /// component is made from, as an instance of the class is (UObject::InstanceComponentTemplates). Null if it has none
    /// </summary>
    internal static ExportEntry ComponentTemplate(IEntry actorClass, string property, PackageCache cache) =>
        EffectiveLightingProperties.ClassDefault(actorClass, cache) is { } cdo
        && cdo.GetProperty<ObjectProperty>(property)?.ResolveToEntry(cdo.FileRef) is ExportEntry template && template.Parent == cdo
            ? template
            : null;

    /// <summary>Why nothing can be placed in <paramref name="file"/> now, or null</summary>
    public static string FileRefusal(OpenLevelFile file)
    {
        if (file is null) return "Choose a level to place it in.";
        if (file.IsReadOnly) return $"{file.FileName} is read-only.";
        if (!file.Package.Game.IsLEGame()) return "Only the Legendary Edition's actors can be created.";
        return null;
    }

    /// <summary>
    /// Why <paramref name="package"/> can't import <paramref name="actorClass"/>, which a commit does if it's defined in another package, or null.
    /// A level imports its actors' classes from the game's own packages: never copies a class's implementation into itself
    /// </summary>
    internal static string ImportRefusal(IEntry actorClass, IMEPackage package)
    {
        if (actorClass.FileRef == package) return null;
        // An export is defined in its file; an import (as of a class default's component's class) is the package at its root's
        string file = actorClass is ImportEntry import ? $"{import.GetRootName()}.pcc" : Path.GetFileName(actorClass.FileRef.FilePath);
        return EntryImporter.IsSafeToImportFrom(file, package) ? null : $"{actorClass.ObjectName} is defined in {file}, which {package.FileNameNoExtension} can't import from.";
    }

    /// <summary>
    /// <paramref name="package"/>'s entry for <paramref name="source"/>, an object of a package it can import from (a class, or a class default's
    /// subobject): the object itself if it's the package's own, else its import, added with its outers if the package has none. Nothing is copied.
    /// Not EntryImporter.EnsureClassIsInFile, which would copy a class into a package that can't import it, and swallows every failure, which a
    /// commit must see to roll back
    /// </summary>
    internal static IEntry ImportOf(IEntry source, IMEPackage package)
    {
        if (source.FileRef == package) return source;
        if (ImportRefusal(source, package) is { } refusal) throw new InvalidOperationException(refusal);
        // An import of the source's package (SFXGame's of Engine.BrushComponent, the class of one of its class defaults' components) is of the same object
        // for this one, at the same path; an export is under the package it's in
        IEntry outer = source.Parent is { } parent ? ImportOf(parent, package)
            : source is ImportEntry ? null
            : package.GetEntryOrAddImport(Path.GetFileNameWithoutExtension(source.FileRef.FilePath), "Package");
        if (package.Imports.FirstOrDefault(i => i.Parent == outer && i.ObjectName == source.ObjectName && i.ClassName == source.ClassName) is { } existing)
            return existing;
        var import = new ImportEntry(package, outer, source.ObjectName)
        {
            ClassName = source.ClassName,
            //the package its class is in: Core for a class
            PackageFile = source switch
            {
                ImportEntry sourceImport => sourceImport.PackageFile,
                ExportEntry { Class: ImportEntry classImport } => classImport.GetRootName(),
                ExportEntry { Class: ExportEntry classExport } => Path.GetFileNameWithoutExtension(classExport.FileRef.FilePath),
                _ => "Core"
            }
        };
        package.AddImport(import);
        return import;
    }

    /// <summary>
    /// Whether a new actor of <paramref name="actorClass"/> snaps to the grid where it's placed: its class default's bEdShouldSnap (of the recipe
    /// classes', SplineLoftActor's and StaticMeshActorBase's set it), as UE3's Constraints.Snap reads it. (ClassDefaultProperty is the general lookup
    /// of a class default's property through its superclasses)
    /// </summary>
    private static bool SnapsToGrid(IEntry actorClass, PackageCache cache) =>
        EffectiveLightingProperties.ClassDefaultProperty<BoolProperty>(actorClass, null, "bEdShouldSnap", cache)?.Value == true;

    /// <summary>
    /// The class a new actor in <paramref name="package"/> is made of: the package's own import of the game's class, or its own class of that name,
    /// or else the game's class export, which a commit imports. Nothing is added to the package
    /// </summary>
    internal static IEntry FindClass(IMEPackage package, string className, PackageCache cache)
    {
        var info = GlobalUnrealObjectInfo.GetClassOrStructInfo(package.Game, className) is { pccPath: not null } known ? known : null;
        //the game's class, from the package that defines it
        string gameClassPath = info is null ? null : $"{Path.GetFileNameWithoutExtension(info.pccPath)}.{className}";
        if (gameClassPath is not null && package.Imports.FirstOrDefault(i => i.IsClass && i.InstancedFullPath == gameClassPath) is { } import) return import;
        if (package.Exports.FirstOrDefault(e => e.IsClass && e.ObjectName.Instanced == className) is { } export) return export;
        if (info is null) return null;
        if (MEDirectories.GetBioGamePath(package.Game) is not { } bioGame)
            throw new InvalidOperationException($"{package.Game}'s game folder isn't set, so its {className} class can't be read.");
        if (!cache.TryGetCachedPackage(Path.Combine(bioGame, info.pccPath), true, out var classPackage) || classPackage is null) return null;
        return classPackage.TryGetUExport(info.exportIndex, out var classExport) && classExport.IsClass && classExport.ObjectName.Instanced == className
            ? classExport
            : classPackage.Exports.FirstOrDefault(e => e.IsClass && e.ObjectName.Instanced == className);
    }

    /// <summary>
    /// Places a new <paramref name="className"/> in <paramref name="file"/> at <paramref name="location"/> and selects it, as one undo step for the
    /// caller to record. Its package is untouched until Commit Changes
    /// </summary>
    /// <param name="gridSize">The grid it's snapped to if its class asks to be (<see cref="SnapsToGrid(IEntry, PackageCache)"/>), or 0 for none</param>
    /// <param name="asset">For a class with an <see cref="AssetKindOf">asset kind</see>: what it uses, an entry of <paramref name="file"/>'s package or another file's export</param>
    /// <param name="surfaceNormal">The normal of the surface it's placed on, facing the camera, if it's known: a decal faces it (<see cref="ActorRecipe.FacesSurface"/>)</param>
    public static IUndoAction Place(IActorEditorContext editor, OpenLevelFile file, string className, Vector3 location, float gridSize = 0, AssetReference asset = null,
        Vector3? surfaceNormal = null)
    {
        var cache = editor.RenderContext.PackageCache;
        if (PlacementRefusal(file, className, cache, out var actorClass) is { } refusal) throw new InvalidOperationException(refusal);
        var recipe = RecipeFor(className);
        if (recipe.Asset is { } kind)
        {
            if (asset is null || asset.Entry is { } entry && entry.FileRef != file.Package || asset.Find(cache) is not { } found || !found.IsA(AssetPicking.ClassOf(kind)))
                throw new ArgumentException($"A {className} needs a {AssetPicking.NameOf(kind)}: of {file.FileName}, or another file of {file.Package.Game}'s.", nameof(asset));
            if (AssetPicking.ProblemOf(kind, found, cache) is { } problem) throw new ArgumentException(problem, nameof(asset));
        }
        if (editor.RenderContext.IsDragging || editor.RenderContext.IsEditingScene) throw new InvalidOperationException("Finish the current scene operation first.");
        if (!float.IsFinite(location.X) || !float.IsFinite(location.Y) || !float.IsFinite(location.Z)) throw new ArgumentException("The location isn't finite.", nameof(location));
        if (surfaceNormal is { } n && !(float.IsFinite(n.X) && float.IsFinite(n.Y) && float.IsFinite(n.Z)))
            throw new ArgumentException("The surface's normal isn't finite.", nameof(surfaceNormal));
        // FSnappingConstraints::Snap: appRound(Location / Grid) * Grid, once, after the push-out (UnEdViewport.cpp's PrivateAddActor, which a viewport
        // click that adds an actor uses: see LevelEditorRenderContext.PlacementAt)
        if (gridSize > 0 && SnapsToGrid(actorClass, cache))
            location = new Vector3(AppRound(location.X / gridSize), AppRound(location.Y / gridSize), AppRound(location.Z / gridSize)) * gridSize;
        // UActorFactoryDecal::CreateActor, given UseActorFactory's ClickPlane.Rotation(): facing the other way, (-Rotation->Vector()).Rotation(), and a unit
        // off the surface, Location + Rotation->Vector(). With no surface (a backdrop click), ClickPlane is zero, whose rotation is too: so it faces -X,
        // a unit along +X, as UE3's editor places it
        Rotator rotation = default;
        if (recipe.FacesSurface)
        {
            Vector3 facing = Direction(Rotation(surfaceNormal ?? Vector3.Zero));
            rotation = Rotation(-facing);
            location += facing;
        }
        var package = file.Package;
        var props = new PropertyCollection
        {
            CommonStructs.Vector3Prop(location, ActorProxy.LocationPropertyName(package.Game)),
            new NameProperty(className, "Tag")
        };
        if (!rotation.IsZero) props.Add(CommonStructs.RotatorProp(rotation, "Rotation"));
        if (recipe.InitialProperties is { } initial) props.AddRange(initial());
        var registry = editor.RenderContext.Registry;
        var record = registry.CreateDraft(package, actorClass, registry.ProposeName(package, className),
            isSpline: recipe.IsSpline, isLoft: className == "SplineLoftActor");
        var selection = editor.RenderContext.SelectedActor;
        ActorProxy draft = null;
        try
        {
            draft = recipe.Draft(editor, record, props, asset, recipe.ComponentProperty is { } property ? ComponentTemplate(actorClass, property, cache) : null);
            draft.OwningFile = file;
            using var edit = new SceneEdit(editor);
            edit.Attach(draft);
            edit.Select(draft);
            edit.Complete();
        }
        catch
        {
            draft?.Dispose();
            registry.Discard(record.Id);
            throw;
        }
        return new PlaceAction(draft, selection);
    }

    /// <summary>
    /// A copy of <paramref name="draft"/>, a placed draft, made as it was by its recipe: of its class, with its asset and its own values, as <paramref name="record"/>.
    /// Not attached; its transform and edits are its caller's to carry (<see cref="ActorCloning"/>)
    /// </summary>
    internal static ActorProxy CopyDraft(IActorEditorContext editor, ActorRecord record, ActorProxy draft)
    {
        if (!draft.IsDraft || draft.CloneSource is not null || RecipeFor(draft.ClassName) is not { } recipe)
            throw new InvalidOperationException($"{draft.ActorName} isn't a placed actor's draft.");
        var template = recipe.ComponentProperty is { } property ? ComponentTemplate(record.Class, property, editor.RenderContext.PackageCache) : null;
        // (as its package refers to it, if it's another level's copy)
        return recipe.Draft(editor, record, draft.CopyOwnValues(), draft.DraftAsset?.In(record.Package, editor.PackageCache), template);
    }

    /// <summary>Whether <paramref name="className"/> has a recipe, so that a placed draft of it can be copied</summary>
    internal static bool HasRecipe(string className) => RecipeFor(className) is not null;

    // Not Rotator.FromDirectionVector and GetDirectionalVector: these round as appRound does and use GMath's sine table, which decide how a decal on a floor
    // or ceiling is turned

    /// <summary>UE3's FVector::Rotation: the direction's yaw and pitch, rounded to 65535ths of a turn (MAXWORD), with no roll</summary>
    private static Rotator Rotation(Vector3 direction) =>
        new(RotationUnits(MathF.Atan2(direction.Z, MathF.Sqrt(direction.X * direction.X + direction.Y * direction.Y))), RotationUnits(MathF.Atan2(direction.Y, direction.X)), 0);

    // appAtan2(...) * (FLOAT)MAXWORD / (2.f*PI): a float times a float, divided by a double (PI is one), passed to appRound as a float. A decal placed on
    // nothing turns half a turn, where that decides it
    private static int RotationUnits(float radians) => AppRound((float)(radians * 65535f / (2.0 * Math.PI)));

    /// <summary>
    /// UE3's appRound: _mm_cvt_ss2si(F + F + 0.5f) >> 1. The conversion rounds 2F + 0.5 to the nearest integer (an even one at a half), and the shift halves
    /// it rounding down, so a half rounds up: -32767.5 to -32767, -32767.502 (as a float) to -32768
    /// </summary>
    private static int AppRound(float f) => (int)MathF.Round(f + f + 0.5f, MidpointRounding.ToEven) >> 1;

    /// <summary>
    /// UE3's FRotator::Vector: FRotationMatrix's X axis, from GMath's table of the sines of 16384 angles (as floats). Its residue at a right angle decides
    /// which way a decal facing straight up or down is turned
    /// </summary>
    private static Vector3 Direction(Rotator rotation)
    {
        float cosPitch = CosTab(rotation.Pitch), cosYaw = CosTab(rotation.Yaw);
        return new Vector3(cosPitch * cosYaw, cosPitch * SinTab(rotation.Yaw), SinTab(rotation.Pitch));

        // FGlobalMath's table: appSin((FLOAT)i * 2.f * PI / (FLOAT)NUM_ANGLES), whose angle is a double (PI is one) cut to a float
        static float SinTab(int angle) => MathF.Sin((float)(((angle >> 2) & 16383) * 2f * Math.PI / 16384f));
        static float CosTab(int angle) => SinTab(angle + 16384);
    }

    /// <summary>
    /// A placed draft (or a copy, <see cref="ActorCloning"/>). Undoing it takes the draft out of the scene, which this step then holds; redoing it attaches
    /// the same draft again. The draft is disposed once nothing holds it (<see cref="ActorProxy.RetainForHistory"/>) unless it's drawn
    /// </summary>
    internal sealed class PlaceAction : IUndoAction, IDisposable
    {
        private readonly ActorProxy draft;
        private readonly ActorProxy selection;
        private readonly IDisposable retained;
        private readonly string description;

        /// <param name="description">What the step is called, if it isn't Place and the draft's name</param>
        public PlaceAction(ActorProxy draft, ActorProxy selection, string description = null)
        {
            this.draft = draft;
            this.selection = selection;
            this.description = description;
            retained = draft.RetainForHistory();
        }

        public string Description => description ?? $"Place {draft.ActorName}";
        public IEnumerable<AssetReference> Assets => [];
        public bool IsOrphaned => draft.IsDisposed;
        public IReadOnlyCollection<ActorId> Subjects => [draft.Id];

        public void Undo()
        {
            using var edit = new SceneEdit(draft.Editor);
            edit.Detach(draft);
            edit.Reselect(selection);
            edit.Complete();
        }

        public void Redo()
        {
            using var edit = new SceneEdit(draft.Editor);
            edit.Attach(draft);
            edit.Select(draft);
            edit.Complete();
        }

        public void Dispose() => retained.Dispose();
    }
}
