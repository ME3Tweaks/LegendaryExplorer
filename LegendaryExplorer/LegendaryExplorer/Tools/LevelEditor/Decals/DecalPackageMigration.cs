using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>Prepares actor transfers in isolated packages before changing either open level.</summary>
internal sealed class DecalPackageMigration : IDisposable
{
    internal sealed record Move(DecalActorProxy Actor, IMEPackage Destination, int ActorIndex);
    private readonly Dictionary<IMEPackage, PackageEdit> packages = [];
    //other files' materials are copied from: the commit's (AssetPorting.CopyIn)
    private readonly PackageCache sources;
    internal List<Move> Moves { get; } = [];

    private DecalPackageMigration(PackageCache sources) => this.sources = sources;

    /// <param name="sources">The cache other files' materials are copied from, opened from disk: the commit's (<see cref="ActorCommit.Commit"/>)</param>
    internal static DecalPackageMigration Prepare(IEnumerable<DecalActorProxy> actors, PackageCache sources)
    {
        var result = new DecalPackageMigration(sources);
        try
        {
            //(a new decal is made in its own package: it can't land in another's)
            foreach (var actor in actors.Where(a => a.ProjectionPackage != a.Package))
                result.PrepareActor(actor);
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    private PackageEdit Edit(IMEPackage package)
    {
        if (!packages.TryGetValue(package, out var edit)) packages.Add(package, edit = new PackageEdit(package));
        return edit;
    }

    private void PrepareActor(DecalActorProxy actor)
    {
        actor.DecalComponent.ValidateCommit();
        var destinationLevel = actor.Editor.GetDecalDestinationLevel(actor.ProjectionPackage)
            ?? throw new InvalidOperationException("The receiving level must be open and writable.");
        if (destinationLevel.Game != actor.Export.Game)
            throw new InvalidOperationException("A decal cannot move between games.");
        var sourceLevel = (ExportEntry)actor.Export.Parent;
        var source = Edit(actor.Export.FileRef);
        var destination = Edit(destinationLevel.FileRef);
        // Clear the donor's OLD receivers before relinking, or the importer would copy their actors too.
        using var donor = Clone(actor.Export.FileRef);
        var donorActor = donor.GetUExport(actor.Export.UIndex);
        var donorComponent = donor.GetUExport(actor.DecalComponent.Export.UIndex);
        donorComponent.WriteProperty(new ArrayProperty<StructProperty>("DecalReceivers"));
        // The pending filter is written with destination indices after import, just like the receiver list.
        donorComponent.WriteProperty(new ArrayProperty<ObjectProperty>("Filter"));
        // A pending material of the source package is an entry the donor (a copy of it) has at the same index, and the import carries it across. Another
        // file's is copied into the destination after the import, as a commit copies one in (AssetPorting), never into the package the decal is leaving;
        // until then the donor has none, so the import doesn't carry the old one across
        AssetReference external = null;
        if (actor.Decal.MaterialChanged)
        {
            var material = actor.Decal.Settings.Material;
            if (material is { IsExternal: true }) external = material;
            donorComponent.WriteProperty(new ObjectProperty(external is null ? material?.EntryIn(actor.Export.FileRef).UIndex ?? 0 : 0, "DecalMaterial"));
        }
        donorComponent.WriteBinary(DecalComponent.Create());
        var subtree = donorActor.GetAllDescendants().Append(donorActor).ToHashSet();
        if (subtree.GetEntriesThatReferenceThese().Values.Any(referrers => referrers.Keys.Any(e => !subtree.Contains(e) && e.UIndex != sourceLevel.UIndex)))
            throw new InvalidOperationException($"Cannot move {actor.Export.ObjectName}: another object references its actor or components.");
        // Avoid reusing an unrelated actor with the same path in the destination.
        donorActor.ObjectName = destination.Staged.GetNextIndexedName(donorActor.ObjectName.Name);
        var pose = actor.SnapshotTransform();
        var props = donorActor.GetProperties();
        props.AddOrReplaceProp(CommonStructs.Vector3Prop(pose.Location, ActorProxy.LocationPropertyName(actor.Export.Game)));
        props.AddOrReplaceProp(CommonStructs.RotatorProp(pose.Rotation, "Rotation"));
        props.AddOrReplaceProp(new FloatProperty(pose.DrawScale, "DrawScale"));
        props.AddOrReplaceProp(CommonStructs.Vector3Prop(pose.DrawScale3D, "DrawScale3D"));
        donorActor.WriteProperties(props);
        var targetLevel = destination.Staged.GetUExport(destinationLevel.UIndex);
        var options = new RelinkerOptionsPackage { Cache = actor.Editor.PackageCache, PortImportsMemorySafe = true };
        // A preseeded export-to-export map would relink the destination Level itself as donor data.
        // Override only references to it, leaving its existing actors and binary untouched.
        options.CustomRelinkUIndex = (IMEPackage incoming, ExportEntry relinking, ref int index, string property,
            string prefix, RelinkerOptionsPackage relinker, out EntryStringPair error) =>
        {
            error = null;
            if (incoming != donor) return false;
            if (index == sourceLevel.UIndex) { index = targetLevel.UIndex; return true; }
            if (donor.GetEntry(index) is { } reference && !subtree.Contains(reference)
                && reference.InstancedFullPath.StartsWith(sourceLevel.InstancedFullPath + ".", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Cannot move {actor.Export.ObjectName}: it depends on {reference.InstancedFullPath} in its current level.");
            return false;
        };
        var errors = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild,
            donorActor, destination.Staged, targetLevel, true, options, out var imported);
        if (errors.Count > 0 || imported is not ExportEntry newActor
            || !options.CrossPackageMap.TryGetValue(donorComponent, out var component) || component is not ExportEntry newComponent)
            throw new InvalidOperationException($"Could not relink {actor.Export.ObjectName} into the receiving package: {string.Join("; ", errors)}");
        if (external is not null)
            newComponent.WriteProperty(new ObjectProperty(AssetPorting.CopyFor(external, destination.Staged, sources, () => [actor]).UIndex, "DecalMaterial"));
        actor.DecalComponent.CommitProjection(newComponent);
        var targetBinary = targetLevel.GetBinaryData<Level>();
        targetBinary.Actors.Add(newActor.UIndex);
        targetLevel.WriteBinary(targetBinary);
        var oldLevel = source.Staged.GetUExport(sourceLevel.UIndex);
        var oldBinary = oldLevel.GetBinaryData<Level>();
        oldBinary.Actors.RemoveAll(i => i == actor.Export.UIndex);
        oldLevel.WriteBinary(oldBinary);
        // Preserve table slots while publishing the staged edits. Other open tools may retain entries by index.
        var oldExports = source.Staged.Exports.ToArray();
        var oldImports = source.Staged.Imports.ToArray();
        EntryPruner.TrashEntryAndDescendants(source.Staged.GetUExport(actor.Export.UIndex));
        for (int i = source.Staged.ExportCount; i < oldExports.Length; i++) source.Staged.AddExport(oldExports[i]);
        for (int i = source.Staged.ImportCount; i < oldImports.Length; i++) source.Staged.AddImport(oldImports[i]);
        Moves.Add(new(actor, actor.ProjectionPackage, newActor.UIndex));
    }

    internal void Apply()
    {
        // Every import, relink and serialization has already succeeded in the isolated copies.
        foreach (var edit in packages.Values) edit.CheckUnchanged();
        foreach (var edit in packages.Values) edit.Apply();
    }

    /// <summary>A copy of <paramref name="package"/> as it is in memory, outside the shared package cache, at its file's path (which what it imports is found from)</summary>
    internal static IMEPackage Clone(IMEPackage package)
    {
        using var stream = package.SaveToStream(compress: false);
        stream.Position = 0;
        return MEPackageHandler.OpenMEPackageFromStream(stream, associatedFilePath: package.FilePath, useSharedPackageCache: false);
    }

    public void Dispose() { foreach (var edit in packages.Values) edit.Staged.Dispose(); }

    private sealed class PackageEdit
    {
        private readonly IMEPackage original;
        internal IMEPackage Staged { get; }
        private readonly byte[][] headers, data, imports;
        private readonly string[] names;
        internal PackageEdit(IMEPackage package)
        {
            original = package;
            Staged = Clone(package);
            headers = Staged.Exports.Select(e => e.Header).ToArray();
            data = Staged.Exports.Select(e => e.Data).ToArray();
            imports = Staged.Imports.Select(e => e.Header).ToArray();
            names = Staged.Names.ToArray();
        }
        internal void CheckUnchanged()
        {
            if (original.ExportCount != headers.Length || original.ImportCount != imports.Length || !original.Names.SequenceEqual(names)
                || original.Exports.Where((e, i) => !e.Data.SequenceEqual(data[i])).Any())
                throw new InvalidOperationException("A package changed while preparing the decal move. Retry the commit.");
        }
        internal void Apply()
        {
            foreach (string name in Staged.Names.Skip(names.Length)) original.FindNameOrAdd(name);
            for (int i = 0; i < Staged.ImportCount; i++)
            {
                var entry = Staged.Imports[i];
                if (i >= imports.Length)
                {
                    using var reader = new EndianReader(entry.Header);
                    original.AddImport(new ImportEntry(original, reader));
                }
                else if (!entry.Header.SequenceEqual(imports[i])) original.Imports[i].Header = entry.Header;
            }
            // Append placeholders first: headers may point forward to another new export.
            for (int i = original.ExportCount; i < Staged.ExportCount; i++)
                original.AddExport(new ExportEntry(original, 0, "None"));
            for (int i = 0; i < Staged.ExportCount; i++)
            {
                var entry = Staged.Exports[i];
                if (i >= headers.Length || !entry.Header.SequenceEqual(headers[i])) original.Exports[i].Header = entry.Header;
                if (i >= data.Length || !entry.Data.SequenceEqual(data[i])) original.Exports[i].Data = entry.Data;
            }
        }
    }
}
