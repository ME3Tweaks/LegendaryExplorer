using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.Diagnostics;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.Packages
{
    public static class PackageResynthesizer
    {
        private const string RESYNTH_TEMP_NAME = "PortingTEMP_RESYNTH_LEC";
        private enum ESynthesisMode
        {
            /// <summary>
            /// Stubbing out exports
            /// </summary>
            Synth_Stubbing,
            /// <summary>
            /// Resolving classes
            /// </summary>
            Synth_Resolving,
            /// <summary>
            /// Transferring data
            /// </summary>
            Synth_Transferring,
        }

        /// <summary>
        /// Reconstructs a package file in a more sensible layout.
        /// </summary>
        /// <param name="package">Package file to reconstruct</param>
        /// <param name="dumpUnreferencedObjects">Only ports objects referenced by ObjectReferencer and TheWorld, which will dump unreferenced objects.</param>
        public static IMEPackage ResynthesizePackage(IMEPackage package, PackageCache cache, bool dumpUnreferencedObjects = false)
        {
            // Keep a reference to the original package: DumpUnreferencedObjects below reassigns `package` to a
            // reduced intermediate that never carries SeekFreeShaderCache (it isn't reachable from TheWorld or
            // ObjectReferencer), so the shader cache always needs to be sourced from here, not from `package`.
            var sourcePackage = package;
            if (dumpUnreferencedObjects)
            {
                package = DumpUnreferencedObjects(package, cache);
            }
            var hasDuplicates = package.HasDuplicateObjects(true);
            if (hasDuplicates)
            {
                LECLog.Warning($"Cannot resynthesisize {package.FileNameNoExtension}: Package has duplicate named objects");
                return package;
            }

            var newPackage = MEPackageHandler.CreateMemoryEmptyPackage(package.FileNameNoExtension + "_RESYNTHING.pcc", package.Game);
            newPackage.setFlags(package.Flags);

            if (package.Game.IsMEGame())
            {
                (newPackage as MEPackage).AdditionalPackagesToCook.ReplaceAll((package as MEPackage).AdditionalPackagesToCook);
            }

            if (newPackage.LECLTagData != null)
            {
                newPackage.LECLTagData.Copy(package.LECLTagData);
            }
            // I considered using EntryTree but it doesn't seem very suited for reordering. 
            // Too confusing for me <_>

            // Step 0: Convert imports to exports where necessary.
            foreach (var entry in package.Imports.Where(x => x.ClassName == "Package" && EntryOrdering.HasExportChildren(x)).ToList())
            {
                var origName = entry.ObjectName;
                var import = EntryImporter.ResolveImport(entry, cache); // Should we cache here?
                entry.ObjectName = RESYNTH_TEMP_NAME; // Rename so we can safely re-add under the original name.

                IEntry newEntry;
                if (import != null)
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, import, package, entry.Parent, true,
                        new RelinkerOptionsPackage(), out newEntry);
                    if (newEntry is ImportEntry)
                    {
                        Debugger.Break(); // This should not be an import...
                    }
                }
                else
                {
                    // Could not resolve the import to real export data (e.g. source package not loaded/found).
                    // It still has export children locally (per HasExportChildren above), so stub it out as a
                    // plain Package export instead of leaving it as an import.
                    newEntry = ExportCreator.CreatePackageExport(package, origName, entry.Parent);
                }

                foreach (var obj in entry.GetChildren())
                {
                    obj.Parent = newEntry;
                }
                EntryPruner.TrashEntryAndDescendants(entry); // Get rid of the original before we port
            }

            // Step 1: Port names
            foreach (var name in package.Names.OrderBy(x => x))
            {
                if (name == RESYNTH_TEMP_NAME)
                    continue; // Don't add this name.
                newPackage.FindNameOrAdd(name);
            }

            // Step 2: Ensure all classes and structs have their info in the class and struct DB
            // Inventory classes
            foreach (var e in package.Exports.Where(x => x.IsClass || x.ClassName == "ScriptStruct"))
            {
                if (e.IsClass && GlobalUnrealObjectInfo.GetClasses(package.Game).ContainsKey(e.ObjectName.Instanced))
                    continue; // This class is already inventoried

                if (GlobalUnrealObjectInfo.GetStructs(package.Game).ContainsKey(e.ObjectName.Instanced))
                    continue; // This struct is already inventoried


                Debug.WriteLine($@"Generating class info for {e.ClassName} {e.InstancedFullPath}");
                GlobalUnrealObjectInfo.generateClassInfo(e);
            }

            // Step 3: Create imports and stub out exports
            // SeekFreeShaderCache is excluded from this walk (see EntryOrdering) and ported separately below,
            // once newPackage's real material set is completely known.
            var ordering = new EntryOrdering(null, package);
            PortOrdering(ordering, newPackage, null, ESynthesisMode.Synth_Stubbing);
            PortOrdering(ordering, newPackage, null, ESynthesisMode.Synth_Resolving);
            PortOrdering(ordering, newPackage, null, ESynthesisMode.Synth_Transferring);
            newPackage.SetInternalFilepath(package.FilePath);

            // Done as the very last step, against the fully-built package, so the decision reflects reality:
            // compacting any earlier (e.g. against a package that's still partway through being reconstructed,
            // which is what this used to do inside DumpUnreferencedObjects) risks judging against an incomplete
            // material set and wiping shaders that are still genuinely used in the final output.
            PortAndCompactShaderCache(sourcePackage, newPackage, cache);

            //12/24/2025 - Fix bad forced export
            PackageDiags.FixBadForcedExport(package);
            return newPackage;
        }

        /// <summary>
        /// Ports SeekFreeShaderCache into the fully-reconstructed destination package, then compacts it against
        /// the destination's real (final) material set and drops it entirely if nothing survives compaction.
        /// </summary>
        private static void PortAndCompactShaderCache(IMEPackage sourcePackage, IMEPackage destPackage, PackageCache cache)
        {
            var seekFreeShaderCache = sourcePackage.FindExport("SeekFreeShaderCache");
            if (seekFreeShaderCache == null)
                return;

            // Porting the cache is speculative - if it compacts down to nothing we're going to throw it away.
            // Snapshot the name/import tables first so we can splice out exactly what the port adds if that
            // happens; otherwise trashing the export leaves its class/name chain (e.g. Engine.ShaderCache) behind
            // as unreferenced entries, which defeats the point of dumping unreferenced data in the first place.
            var namesBeforePort = destPackage.Names.ToList();
            var importsBeforePort = destPackage.Imports.ToList();

            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies,
                seekFreeShaderCache, destPackage, null, true, new RelinkerOptionsPackage() { Cache = cache },
                out var portedCache);

            // Once it is ported over, compact it against destPackage's real, final material set to remove any
            // useless data in it.
            ShaderCacheManipulator.CompactSeekFreeShaderCaches(destPackage);

            // If after compaction it has no shader maps, delete it.
            var compactedCache = (ExportEntry)portedCache;
            if (compactedCache.GetBinaryData<ShaderCache>().MaterialShaderMaps.Count == 0)
            {
                // Trash first, before touching the name/import tables: TrashEntryAndDescendants needs to read
                // them (via GetAllDescendants -> RebuildLookupTable) to walk descendants and rename the entry, so
                // reverting the tables first corrupts that read (the export's own name index would already be
                // out of range). Trashing a trailing, childless export like this one (it was just ported in dead
                // last, and SeekFreeShaderCache has no children of its own) also fully removes it from the
                // package via the cleanup already built into TrashEntries/RemoveTrailingTrash - once that's
                // happened, everything the port (and trashing itself) added to the tables is genuinely
                // unreferenced, so it's safe to truncate straight back to their pre-port length.
                EntryPruner.TrashEntryAndDescendants(compactedCache);

                if (!destPackage.Exports.Contains(compactedCache))
                {
                    destPackage.restoreNames(namesBeforePort);
                    destPackage.restoreImports(importsBeforePort);
                }
            }
        }

        public static IMEPackage DumpUnreferencedObjects(IMEPackage package, PackageCache cache)
        {
            // Package must be inventoried before dumping objects because otherwise we will try to resolve array types
            // when package may not yet be fully setup, which causes problems.

            foreach (var exp in package.Exports.Where(x => x.ClassName is "ScriptStruct" or "Class"))
            {
                GlobalUnrealObjectInfo.generateClassInfo(exp, exp.ClassName == "ScriptStruct", cache);
            }

            var newPackage = MEPackageHandler.CreateMemoryEmptyPackage("RebuildingPackage.pcc", package.Game);

            var theWorld = package.FindExport("TheWorld");
            if (theWorld != null)
            {
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, theWorld, newPackage, null, true, new RelinkerOptionsPackage() { Cache = cache }, out var portedWorld);
            }
            else
            {
                // Not level package
                foreach (var objReferencer in package.Exports.Where(x => x.Parent == null && x.ClassName == "ObjectReferencer"))
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies,
                        objReferencer, newPackage, null, true, new RelinkerOptionsPackage() { Cache = cache },
                        out var portedReferencer);
                }
            }

            // Note: SeekFreeShaderCache is intentionally NOT ported here. It isn't reachable from TheWorld or
            // ObjectReferencer (nothing references it via a normal UObject property), and deciding what to keep in
            // it requires the package's real, final material set - so it's ported and compacted once, centrally,
            // by ResynthesizePackage.PortAndCompactShaderCache after the whole package is built.

            CopyPackageHeader(package, newPackage);

            newPackage.SetInternalFilepath(package.FilePath);
            return newPackage;
        }

        /// <summary>
        /// Copies package header data over to a new package
        /// </summary>
        /// <param name="package"></param>
        /// <param name="newPackage"></param>
        private static void CopyPackageHeader(IMEPackage package, IMEPackage newPackage)
        {
            newPackage.setFlags(package.Flags);
            newPackage.PackageGuid = package.PackageGuid;
            if (package is MEPackage mePackage && newPackage is MEPackage meNewPackage)
            {
                meNewPackage.AdditionalPackagesToCook.AddRange(mePackage.AdditionalPackagesToCook);
            }
        }

        private static void PortOrdering(EntryOrdering ordering, IMEPackage newPackage, IEntry parent, ESynthesisMode mode, List<ImportEntry> importsToConvert = null)
        {
            IEntry newEntry = null;
            if (ordering.Entry != null)
            {
                if (mode == ESynthesisMode.Synth_Stubbing)
                {
                    if (ordering.Entry is ImportEntry oImp)
                    {
                        if (ordering.ConvertToExport)
                        {
                            newEntry = ExportCreator.CreatePackageExport(newPackage, ordering.Entry.ObjectName, parent);
                            (newEntry as ExportEntry).SetForcedExportFlag(true); // Import to export is always a forced export
                        }
                        else
                        {
                            ImportEntry imp = new ImportEntry(newPackage, parent, ordering.Entry.ObjectName)
                            {
                                PackageFile = oImp.PackageFile,
                                ClassName = oImp.ClassName
                            };
                            // Core and Core.Package will probably already be added
                            // We don't want to re-add them so we see if they exist first
                            newEntry = newPackage.FindEntry(imp.InstancedFullPath);
                            if (newEntry == null)
                            {
                                newPackage.AddImport(imp);
                                newEntry = imp;
                            }
                        }
                    }
                    else if (ordering.Entry is ExportEntry oExp)
                    {
                        newEntry = ExportCreator.CreatePackageExport(newPackage, ordering.Entry.ObjectName, parent);
                        (newEntry as ExportEntry).SetForcedExportFlag(oExp.IsForcedExport); // Match ForcedExport for lookups
                    }
                }
                else if (mode == ESynthesisMode.Synth_Resolving)
                {
                    // Transfer classes and replace data WITHOUT RELINK
                    if (ordering.Entry is ExportEntry oExp && IsClassSubObj(oExp))
                    {
                        var destExp = newPackage.FindExport(ordering.Entry.InstancedFullPath);
                        // Update class
                        if (oExp.Class != null) // Class is not class
                        {
                            destExp.Class = destExp.FileRef.FindEntry(oExp.Class.InstancedFullPath);
                        }
                        else
                        {
                            destExp.Class = null; // Change from Package to Class
                        }
                        
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingular,
                            ordering.Entry, newPackage, destExp, false, // no relink
                            new RelinkerOptionsPackage() { RelinkAllowDifferingClassesInRelink = true }, out _);
                    }
                }
                else if (mode == ESynthesisMode.Synth_Transferring)
                {
                    // Don't filter out class
                    if (ordering.Entry is ExportEntry oExp)
                    {
                        var destExp = newPackage.FindExport(oExp.InstancedFullPath);

                        // Update class, archetype, superclass
                        if (oExp.Class != null) // Class is not class
                        {
                            destExp.Class = destExp.FileRef.FindEntry(oExp.Class.InstancedFullPath);
                        }
                        else
                        {
                            destExp.Class = null; // Change from Package to Class
                        }

                        // Superclass
                        if (oExp.SuperClass != null) // SuperClass is not null
                        {
                            destExp.SuperClass = destExp.FileRef.FindEntry(oExp.SuperClass.InstancedFullPath);
                        }

                        // Archetype
                        if (oExp.Archetype != null) // Archetype is not null
                        {
                            destExp.Archetype = destExp.FileRef.FindEntry(oExp.Archetype.InstancedFullPath);
                        }

                        destExp.ObjectFlags = oExp.ObjectFlags;
                        destExp.ExportFlags = oExp.ExportFlags;
                        destExp.PackageFlags = oExp.PackageFlags;
                        destExp.GenerationNetObjectCount = oExp.GenerationNetObjectCount;
                        destExp.PackageGUID = oExp.PackageGUID;

                        // Update data
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingular,
                            ordering.Entry, newPackage, destExp, true,
                            new RelinkerOptionsPackage() { RelinkAllowDifferingClassesInRelink = true, ImportExportDependencies = false }, out _);


                    }
                }
            }

            foreach (var o in ordering.Children)
            {
                PortOrdering(o, newPackage, newEntry, mode);
            }
        }

        private static bool IsClassSubObj(ExportEntry oExp)
        {
            if (oExp.ClassName is "Class" or "Function" or "State")
                return true;
            if (oExp.Parent != null && oExp.Parent.ClassName is "Class" or "Function" or "State")
                return true;
            return false;
        }
    }

    /// <summary>
    /// Describes a set of imports and exports in order
    /// </summary>
    internal class EntryOrdering
    {
        /// <summary>
        /// The entry this ordering represents. If null, this is the root
        /// </summary>
        public IEntry Entry { get; set; }

        /// <summary>
        /// If this entry (an import), upon porting, should be converted to export instead
        /// </summary>
        public bool ConvertToExport { get; set; }

        public List<EntryOrdering> Children { get; }

        public EntryOrdering(IEntry entry, IMEPackage package)
        {
            if (entry == null && package == null)
                throw new Exception("Bad setup for children ordering");

            Entry = entry;
            // This might no longer be necessary
            //ConvertToExport = entry is ImportEntry ientry && HasExportChildren(ientry);

            package ??= entry.FileRef;
            // SeekFreeShaderCache is excluded here - ResynthesizePackage ports and compacts it separately, once
            // the rest of the package (and therefore its real material set) is fully built. See PortAndCompactShaderCache.
            var exports = package.Exports.Where(x => x.Parent == entry && !x.IsTrash() && x.ObjectName != "SeekFreeShaderCache").ToList();
            var imports = package.Imports.Where(x => x.Parent == entry && !x.IsTrash()).ToList();

            exports.Sort(new EntrySorter());
            imports.Sort(new EntrySorter());

            // yeah this is gross.
            var temp = imports.OfType<IEntry>().ToList();
            OrderClasses(temp);
            imports = temp.OfType<ImportEntry>().ToList();

            Children = new List<EntryOrdering>();

            // Imports first so they port over first
            var impChildren = new List<EntryOrdering>();
            var conversions = new List<EntryOrdering>();

            foreach (var imp in imports)
            {
                var ordering = new EntryOrdering(imp, package);
                if (ordering.ConvertToExport)
                    conversions.Add(ordering); // Will be converted to an export instead
                else
                    impChildren.Add(ordering);
            }

            var expChildren = new List<EntryOrdering>();
            var expEntries = exports.Concat(conversions.Select(x => x.Entry)).ToList();
            expEntries.Sort(new EntrySorter());
            OrderClasses(expEntries);

            foreach (var exp in expEntries)
            {
                expChildren.Add(new EntryOrdering(exp, package));
            }

            Children.AddRange(impChildren);
            Children.AddRange(expChildren);
        }

        /// <summary>
        /// Used to determine if a package export can be an import vs an export. Export children indicate the package export should be an export for consistency.
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        internal static bool HasExportChildren(IEntry entry)
        {
            if (entry.FileRef.Exports.Any(x => x.Parent == entry))
                return true;

            foreach (var imp in entry.FileRef.Imports.Where(x => x.Parent == entry))
            {
                return HasExportChildren(imp);
            }

            return false; // No children of us is an export
        }

        private void OrderClasses(List<IEntry> entries)
        {
            var classes = entries.Where(x => x.IsClass).ToList(); // Must use .ToList() to prevent concurrent modification
            foreach (var cls in classes)
            {
                if (cls is ExportEntry exp)
                {
                    var clsDefaultsUIndex = ObjectBinary.From<UClass>(exp).Defaults;

                    var classDefaults = cls.FileRef.GetEntry(clsDefaultsUIndex);
                    entries.Remove(classDefaults);
                    var newIdx = entries.IndexOf(cls) + 1;
                    entries.Insert(newIdx, classDefaults);
                }
                else
                {
                    // It's an import
                    var defaultName = $"Default__{cls.ObjectName.Instanced}";
                    var defImport = entries.FirstOrDefault(x => x.ObjectName.Instanced == defaultName);
                    if (defImport != null)
                    {
                        entries.Remove(defImport);
                        var newIdx = entries.IndexOf(cls) + 1;
                        entries.Insert(newIdx, defImport);
                    }
                }
            }
        }
    }

    internal class EntrySorter : IComparer<IEntry>
    {
        private int SpecialOrderExport(ExportEntry exp)
        {
            int score = 0;
            if (!exp.IsDefaultObject)
            {
                if (exp.ObjectName == "SeekFreeShaderCache")
                    return -100; // Should go first as porting other objects may modify this
                if (exp.ObjectName == "ObjectReferencer")
                    return -11; // Should go earlier
                if (exp.ObjectName.Name.StartsWith("PersistentLevel")) // Go above LSKs
                    return -10;

                if (exp.ObjectName.Name.StartsWith("DirectionalMaxComponent"))
                    return -9;
                if (exp.ObjectName.Name.StartsWith("LightMapVector"))
                    return -8;
                if (exp.ObjectName.Name.StartsWith("NormalizedAverageColor"))
                    return -7;
                if (exp.ObjectName.Name == "TheWorld")
                    return 10; // Goes at the end
            }

            return score;
        }

        private int SpecialOrderImport(ImportEntry imp)
        {
            int score = 0;
            // No special scoring currently
            //if (!imp.IsDefaultObject)
            //{
            //    if (imp.ObjectName == "ObjectReferencer")
            //        return -10; // Should go earlier
            //    if (imp.ObjectName.Name.StartsWith("PersistentLevel")) // Go above LSKs
            //        return -10;

            //    if (imp.ObjectName.Name.StartsWith("DirectionalMaxComponent"))
            //        return -9;
            //    if (imp.ObjectName.Name.StartsWith("LightMapVector"))
            //        return -8;
            //    if (imp.ObjectName.Name.StartsWith("NormalizedAverageColor"))
            //        return -7;
            //    if (imp.ObjectName.Name == "TheWorld")
            //        return 10; // Goes at the end
            //}

            return score;
        }

        public int Compare(IEntry? x, IEntry? y)
        {
            {
                if (x is ExportEntry exp1 && y is ExportEntry exp2)
                {
                    var score1 = SpecialOrderExport(exp1);
                    var score2 = SpecialOrderExport(exp2);

                    if (score1 != score2)
                        return score1.CompareTo(score2); // One of the items has a special ordering value assigned to it

                    // No special ordering
                    return String.Compare(exp1.ObjectName.Instanced, exp2.ObjectName.Instanced,
                        StringComparison.InvariantCultureIgnoreCase);
                }

                if (x is ImportEntry imp1 && y is ImportEntry imp2)
                {
                    var score1 = SpecialOrderImport(imp1);
                    var score2 = SpecialOrderImport(imp2);

                    if (score1 != score2)
                        return score1.CompareTo(score2); // One of the items has a special ordering value assigned to it

                    // No special ordering
                    return String.Compare(imp1.ObjectName.Instanced, imp2.ObjectName.Instanced,
                        StringComparison.InvariantCultureIgnoreCase);
                }
            }
            {
                if (x is ImportEntry imp1 && y is ExportEntry exp2)
                {
                    var score1 = SpecialOrderImport(imp1);
                    var score2 = SpecialOrderExport(exp2);

                    if (score1 != score2)
                        return score1.CompareTo(score2); // One of the items has a special ordering value assigned to it

                    // No special ordering
                    return String.Compare(imp1.ObjectName.Instanced, exp2.ObjectName.Instanced,
                        StringComparison.InvariantCultureIgnoreCase);
                }
            }
            {
                if (x is ExportEntry exp1 && y is ImportEntry imp2)
                {
                    var score1 = SpecialOrderExport(exp1);
                    var score2 = SpecialOrderImport(imp2);

                    if (score1 != score2)
                        return score1.CompareTo(score2); // One of the items has a special ordering value assigned to it

                    // No special ordering
                    return String.Compare(exp1.ObjectName.Instanced, imp2.ObjectName.Instanced,
                        StringComparison.InvariantCultureIgnoreCase);
                }
            }
            return 0;
        }
    }
}