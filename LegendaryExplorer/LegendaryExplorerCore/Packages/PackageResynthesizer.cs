using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
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
        public static IMEPackage ResynthesizePackage(IMEPackage package, PackageCache cache)
        {
            var hasDuplicates = package.HasDuplicateObjects();
            if (hasDuplicates)
            {
                LECLog.Warning("Cannot resynthesisize package: Package has duplicate named objects");
                return package;
            }

            var newPackage = MEPackageHandler.CreateMemoryEmptyPackage(package.FilePath, package.Game);
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
                var import = EntryImporter.ResolveImport(entry, cache); // Should we cache here?
                if (import != null)
                {
                    var origName = entry.ObjectName;
                    entry.ObjectName = RESYNTH_TEMP_NAME; // Rename so we can safely re-import.

                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, import, package, entry.Parent, true,
                        new RelinkerOptionsPackage(), out var newEntry);
                    if (newEntry is ImportEntry)
                    {
                        Debugger.Break(); // This should not be an import...
                    }
                    foreach (var obj in entry.GetChildren())
                    {
                        obj.Parent = newEntry;
                    }
                    EntryPruner.TrashEntryAndDescendants(entry); // Get rid of the original before we port
                }
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
            var ordering = new EntryOrdering(null, package);
            PortOrdering(ordering, newPackage, null, ESynthesisMode.Synth_Stubbing);
            PortOrdering(ordering, newPackage, null, ESynthesisMode.Synth_Resolving);
            PortOrdering(ordering, newPackage, null, ESynthesisMode.Synth_Transferring);
            return newPackage;
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
                    else if (ordering.Entry is ExportEntry)
                    {
                        newEntry = ExportCreator.CreatePackageExport(newPackage, ordering.Entry.ObjectName, parent);
                    }
                }
                else if (mode == ESynthesisMode.Synth_Resolving)
                {
                    // Transfer classes
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
                            ordering.Entry,
                            newPackage, destExp, true, new RelinkerOptionsPackage() { RelinkAllowDifferingClassesInRelink = true }, out _);
                    }
                }
                else if (mode == ESynthesisMode.Synth_Transferring)
                {
                    // Don't filter out class
                    if (ordering.Entry is ExportEntry oExp)
                    {
                        var destExp = newPackage.FindExport(ordering.Entry.InstancedFullPath);

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
                            ordering.Entry,
                            newPackage, destExp, true, new RelinkerOptionsPackage() { RelinkAllowDifferingClassesInRelink = true }, out _);
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
            var exports = package.Exports.Where(x => x.Parent == entry && !x.IsTrash()).ToList();
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
                if (exp.ObjectName == "ObjectReferencer")
                    return -10; // Should go earlier
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