using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// New actors made from their class's defaults, placed as drafts: drawn and edited with nothing written until a commit creates their exports
/// (<see cref="ActorProxy.Materialize"/>). Only classes with a recipe can be placed
/// </summary>
public static class ActorCreation
{
    /// <summary>
    /// The classes with a recipe. A new one has its location and its Tag (its class's name, as UE3's UWorld::SpawnActor sets it) and nothing else
    /// of its own; spline points start unconnected, and a loft with no DeformMesh until one is assigned. A static mesh actor has the mesh chosen for
    /// it, in a component made from its class default's (<see cref="ComponentTemplate"/>), and a standalone light has its class default's light
    /// </summary>
    public static IReadOnlyList<string> RecipeClasses { get; } =
        ["Note", "TargetPoint", "SplineActor", "SplineLoftActor", "StaticMeshActor", .. LightActorProxy.StandaloneClasses];

    /// <summary>Whether a new <paramref name="className"/> needs a mesh chosen for it</summary>
    public static bool NeedsMesh(string className) => className is "StaticMeshActor";

    /// <summary>Why a new <paramref name="className"/> can't be placed in <paramref name="file"/> now, or null</summary>
    public static string Refusal(OpenLevelFile file, string className, PackageCache cache) => Refusal(file, className, cache, out _);

    /// <param name="actorClass">Its class, if it can be placed</param>
    private static string Refusal(OpenLevelFile file, string className, PackageCache cache, out IEntry actorClass)
    {
        actorClass = null;
        if (!RecipeClasses.Contains(className)) return $"There's no recipe for a new {className}.";
        if (FileRefusal(file) is { } refusal) return refusal;
        try { actorClass = FindClass(file.Package, className, cache); }
        catch (Exception error) when (error is not OutOfMemoryException) { return $"Its class couldn't be read: {error.Message}"; }
        if (actorClass is null) return $"{file.Package.Game} has no {className} class.";
        if (NeedsMesh(className) && ComponentTemplate(actorClass, "StaticMeshComponent", cache) is null)
            return $"{className}'s class default has no StaticMeshComponent to make its component from.";
        if (LightActorProxy.StandaloneClasses.Contains(className)
            && (ComponentTemplate(actorClass, "LightComponent", cache) is not { } light || SceneLight.GetLightType(light) is null))
            return $"{className}'s class default has no light component to make its own from.";
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
    internal static string ImportRefusal(IEntry actorClass, IMEPackage package) =>
        actorClass.FileRef == package || EntryImporter.IsSafeToImportFrom(actorClass.FileRef.FilePath, package)
            ? null
            : $"{actorClass.ObjectName} is defined in {Path.GetFileName(actorClass.FileRef.FilePath)}, which {package.FileNameNoExtension} can't import from.";

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
        IEntry outer = source.Parent is { } parent ? ImportOf(parent, package)
            : package.GetEntryOrAddImport(Path.GetFileNameWithoutExtension(source.FileRef.FilePath), "Package");
        if (package.Imports.FirstOrDefault(i => i.Parent == outer && i.ObjectName == source.ObjectName && i.ClassName == source.ClassName) is { } existing)
            return existing;
        var import = new ImportEntry(package, outer, source.ObjectName)
        {
            ClassName = source.ClassName,
            //the package its class is in: Core for a class
            PackageFile = (source as ExportEntry)?.Class switch
            {
                ImportEntry classImport => classImport.GetRootName(),
                ExportEntry classExport => Path.GetFileNameWithoutExtension(classExport.FileRef.FilePath),
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
    /// <param name="mesh">For a class that <see cref="NeedsMesh"/>: the StaticMesh it draws, an entry of <paramref name="file"/>'s package</param>
    public static IUndoAction Place(IActorEditorContext editor, OpenLevelFile file, string className, Vector3 location, float gridSize = 0, IEntry mesh = null)
    {
        var cache = editor.RenderContext.PackageCache;
        if (Refusal(file, className, cache, out var actorClass) is { } refusal) throw new InvalidOperationException(refusal);
        if (NeedsMesh(className) && (mesh is null || mesh.FileRef != file.Package || !mesh.IsA("StaticMesh")))
            throw new ArgumentException($"A {className} needs a StaticMesh of {file.FileName}.", nameof(mesh));
        if (editor.RenderContext.IsDragging || editor.RenderContext.IsEditingScene) throw new InvalidOperationException("Finish the current scene operation first.");
        if (!float.IsFinite(location.X) || !float.IsFinite(location.Y) || !float.IsFinite(location.Z)) throw new ArgumentException("The location isn't finite.", nameof(location));
        //appRound, which rounds halves up
        if (gridSize > 0 && SnapsToGrid(actorClass, cache))
            location = new Vector3(MathF.Floor(location.X / gridSize + 0.5f), MathF.Floor(location.Y / gridSize + 0.5f), MathF.Floor(location.Z / gridSize + 0.5f)) * gridSize;
        var package = file.Package;
        var props = new PropertyCollection
        {
            CommonStructs.Vector3Prop(location, ActorProxy.LocationPropertyName(package.Game)),
            new NameProperty(className, "Tag")
        };
        var registry = editor.RenderContext.Registry;
        bool spline = className is "SplineActor" or "SplineLoftActor";
        var record = registry.CreateDraft(package, actorClass, registry.ProposeName(package, className),
            isSpline: spline, isLoft: className == "SplineLoftActor");
        var selection = editor.RenderContext.SelectedActor;
        ActorProxy draft = null;
        try
        {
            draft = spline ? new SplineActorProxy(editor, record, props)
                : NeedsMesh(className) ? new StaticMeshActorProxy(editor, record, props, mesh, ComponentTemplate(actorClass, "StaticMeshComponent", cache))
                : LightActorProxy.StandaloneClasses.Contains(className) ? new LightActorProxy(editor, record, props, ComponentTemplate(actorClass, "LightComponent", cache))
                : new EditorActorProxy(editor, record, props, ActorClassification.Get(className, package.Game));
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
    /// A placed draft. Undoing it takes the draft out of the scene, which this step then holds; redoing it attaches the same draft again.
    /// The draft is disposed once nothing holds it (<see cref="ActorProxy.RetainForHistory"/>) unless it's drawn
    /// </summary>
    private sealed class PlaceAction : IUndoAction, IDisposable
    {
        private readonly ActorProxy draft;
        private readonly ActorProxy selection;
        private readonly IDisposable retained;

        public PlaceAction(ActorProxy draft, ActorProxy selection)
        {
            this.draft = draft;
            this.selection = selection;
            retained = draft.RetainForHistory();
        }

        public string Description => $"Place {draft.ActorName}";
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
