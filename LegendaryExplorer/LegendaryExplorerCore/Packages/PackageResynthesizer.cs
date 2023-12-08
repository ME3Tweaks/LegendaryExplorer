using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Packages
{
    public static class PackageResynthesizer
    {
        /// <summary>
        /// Reconstructs a package file in a more sensible layout.
        /// </summary>
        /// <param name="package"></param>
        public static void ResynthesizePackage(IMEPackage package)
        {
            var packTempName = Path.Combine(Directory.GetParent(package.FilePath).FullName, Path.GetFileNameWithoutExtension(package.FilePath) + "_TMP.pcc");
            var newPackage = MEPackageHandler.CreateAndOpenPackage(packTempName, package.Game);

            // I considered using EntryTree but it doesn't seem very suited for reordering. 
            // Too confusing for me <_>

            // Step 1: Port names
            foreach (var name in package.Names.OrderBy(x => x))
            {
                newPackage.FindNameOrAdd(name);
            }

            // Step 2: Create imports and stub out exports
            var ordering = new EntryOrdering(null, package);
            PortOrdering(ordering, newPackage, null, true);

            PortOrdering(ordering, newPackage, null, false);

            newPackage.Save();
        }

        private static void PortOrdering(EntryOrdering ordering, IMEPackage newPackage, IEntry parent, bool isStubbing)
        {
            IEntry newEntry = null;
            if (ordering.Entry != null)
            {
                if (isStubbing)
                {
                    if (ordering.Entry is ImportEntry oImp)
                    {
                        ImportEntry imp = new ImportEntry(newPackage, parent, ordering.Entry.ObjectName)
                        {
                            PackageFile = oImp.PackageFile,
                            ClassName = oImp.ClassName
                        };
                        newPackage.AddImport(imp);
                        newEntry = imp;
                    }
                    else if (ordering.Entry is ExportEntry)
                    {
                        newEntry = ExportCreator.CreatePackageExport(newPackage, ordering.Entry.ObjectName, parent);
                    }
                }
                else if (ordering.Entry is ExportEntry oExp)
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

                    destExp.ObjectFlags = oExp.ObjectFlags;

                    // Update data
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink, ordering.Entry,
                        newPackage, destExp, true, new RelinkerOptionsPackage(), out _);

                }
            }

            foreach (var o in ordering.Children)
            {
                PortOrdering(o, newPackage, newEntry, isStubbing);
            }
        }

        /// <summary>
        /// Used to determine if a package export can be an import vs an export. Export children indicate the package export should be an export for consistency.
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        private static bool HasExportChildren(IEntry entry)
        {
            if (entry.FileRef.Exports.Any(x => x.Parent == entry))
                return true;

            foreach (var imp in entry.FileRef.Imports.Where(x => x.Parent == entry))
            {
                return HasExportChildren(imp);
            }

            return false; // No children of us is an export
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

        public List<EntryOrdering> Children { get; }

        public EntryOrdering(IEntry entry, IMEPackage package)
        {
            if (entry == null && package == null)
                throw new Exception("Bad setup for children ordering");

            Entry = entry;
            package ??= entry.FileRef;
            var exports = package.Exports.Where(x => x.Parent == entry).ToList();
            var imports = package.Imports.Where(x => x.Parent == entry).ToList();

            exports.Sort(new EntrySorter());
            imports.Sort(new EntrySorter());

            OrderClasses(exports);
            OrderClasses(imports);

            Children = new List<EntryOrdering>();

            // Imports first so they port over first
            foreach (var imp in imports)
            {
                Children.Add(new EntryOrdering(imp, package));
            }
            foreach (var exp in exports)
            {
                Children.Add(new EntryOrdering(exp, package));
            }
        }

        private void OrderClasses(List<ExportEntry> exports)
        {
            var classes = exports.Where(x => x.IsClass).ToList(); // Must use .ToList() to prevent concurrent modification
            foreach (var cls in classes)
            {
                var clsDefaultsUIndex = ObjectBinary.From<UClass>(cls).Defaults;
                if (clsDefaultsUIndex < 0)
                {
                    // It's an import
                }
                else
                {
                    var classDefaults = cls.FileRef.GetUExport(clsDefaultsUIndex);
                    exports.Remove(classDefaults);
                    var newIdx = exports.IndexOf(cls) + 1;
                    exports.Insert(newIdx, classDefaults);
                }
            }
        }

        private void OrderClasses(List<ImportEntry> imports)
        {
            var classes = imports.Where(x => x.IsClass).ToList(); // Must use .ToList() to prevent concurrent modification
            foreach (var cls in classes)
            {
                var defaultName = $"Default__{cls.ObjectName.Instanced}";
                var defImport = imports.FirstOrDefault(x => x.ObjectName.Instanced == defaultName);
                if (defImport != null)
                {
                    imports.Remove(defImport);
                    var newIdx = imports.IndexOf(cls) + 1;
                    imports.Insert(newIdx, defImport);
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
            if (x is ExportEntry exp1 && y is ExportEntry exp2)
            {
                var score1 = SpecialOrderExport(exp1);
                var score2 = SpecialOrderExport(exp2);

                if (score1 != score2)
                    return score1.CompareTo(score2); // One of the items has a special ordering value assigned to it

                // No special ordering
                return String.Compare(exp1.ObjectName.Instanced, exp2.ObjectName.Instanced, StringComparison.InvariantCultureIgnoreCase);
            }

            if (x is ImportEntry imp1 && y is ImportEntry imp2)
            {
                var score1 = SpecialOrderImport(imp1);
                var score2 = SpecialOrderImport(imp2);

                if (score1 != score2)
                    return score1.CompareTo(score2); // One of the items has a special ordering value assigned to it

                // No special ordering
                return String.Compare(imp1.ObjectName.Instanced, imp2.ObjectName.Instanced, StringComparison.InvariantCultureIgnoreCase);
            }

            return 0;
        }
    }
}