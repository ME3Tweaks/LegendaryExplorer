using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Packages
{
    public static class ExportCreator
    {
        // This is technically not an export, but it's next to the method that makes sense for it to be next to
        /// <summary>
        /// Creates a package import, if it doesn't already exist as an export.
        /// </summary>
        /// <param name="pcc"></param>
        /// <param name="packageName"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static IEntry CreatePackageImport(this IMEPackage pcc, NameReference packageName, IEntry parent = null)
        {
            return CreateImport(pcc, "Package", packageName, parent);
        }

        /// <summary>
        /// Creates an import with the specified class and name, if it doesn't already exist as an object in the package.
        /// </summary>
        /// <param name="pcc"></param>
        /// <param name="packageName"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static IEntry CreateImport(this IMEPackage pcc, string className, NameReference objectName, IEntry parent = null)
        {
            var testName = parent != null ? NameReference.FromInstancedString($"{parent.InstancedFullPath}.{objectName.Instanced}") : objectName;
            var testEntry = pcc.FindEntry(testName, className);
            if (testEntry != null)
                return testEntry;

            var imp = new ImportEntry(pcc, parent, objectName)
            {
                ClassName = className,
                PackageFile = ImportEntry.GetPackageFile(pcc.Game, className),
            };

            pcc.AddImport(imp);
            return imp;
        }

        /// <summary>
        /// Creates a package export, if it doesn't already exist as an export.
        /// </summary>
        /// <param name="pcc"></param>
        /// <param name="packageName"></param>
        /// <param name="parent"></param>
        /// <param name="relinkResultsAvailable"></param>
        /// <returns></returns>
        public static ExportEntry CreatePackageExport(this IMEPackage pcc, NameReference packageName, IEntry parent = null, Action<List<EntryStringPair>> relinkResultsAvailable = null, PackageCache cache = null, bool forcedExport = true)
        {
            var testName = parent != null ? NameReference.FromInstancedString($"{parent.InstancedFullPath}.{packageName.Instanced}") : packageName;
            var testEntry = pcc.FindExport(testName, "Package");
            if (testEntry != null)
                return testEntry;

            var rop = new RelinkerOptionsPackage { ImportExportDependencies = true, Cache = cache };
            var exp = new ExportEntry(pcc, parent, packageName)
            {
                Class = EntryImporter.EnsureClassIsInFile(pcc, "Package", rop)
            };
            relinkResultsAvailable?.Invoke(rop.RelinkReport);
            exp.ObjectFlags |= UnrealFlags.EObjectFlags.Public;
            if (forcedExport)
            {
                exp.ExportFlags |= UnrealFlags.EExportFlags.ForcedExport;
            }

            pcc.AddExport(exp);
            return exp;
        }

        public static ExportEntry CreateExport(this IMEPackage pcc, NameReference name, string className, IEntry parent = null, Action<List<EntryStringPair>> relinkResultsAvailable = null,
            bool indexed = true,
            bool createWithStack = false,
            byte[] prePropBinary = null,
            PackageCache cache = null)
        {
            var rop = new RelinkerOptionsPackage() { ImportExportDependencies = true, Cache = cache };
            var exp = new ExportEntry(pcc, parent, indexed ? pcc.GetNextIndexedName(name) : name, prePropBinary)
            {
                Class = EntryImporter.EnsureClassIsInFile(pcc, className, rop)
            };
            if (parent is ImportEntry or ExportEntry { IsForcedExport: true })
            {
                // Import parents will always yield forced export since we are in a different package.
                exp.ExportFlags |= UnrealFlags.EExportFlags.ForcedExport;
            }

            relinkResultsAvailable?.Invoke(rop.RelinkReport);
            pcc.AddExport(exp);

            if (createWithStack)
            {
                exp.SetPrePropBinary(EntryImporter.CreateStack(pcc.Game, exp.Class.UIndex), true);
                exp.ObjectFlags |= UnrealFlags.EObjectFlags.HasStack;
            }
            return exp;
        }
    }
}
