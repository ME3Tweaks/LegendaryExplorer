using System;
using System.Collections.Generic;
using System.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Packages
{
    public static class ExportCreator
    {
        /// <summary>
        /// Creates a package export, if it doesn't already exist as an export.
        /// </summary>
        /// <param name="pcc"></param>
        /// <param name="packageName"></param>
        /// <param name="parent"></param>
        /// <param name="relinkResultsAvailable"></param>
        /// <returns></returns>
        public static ExportEntry CreatePackageExport(IMEPackage pcc, NameReference packageName, IEntry parent = null, Action<List<EntryStringPair>> relinkResultsAvailable = null, PackageCache cache = null, bool forcedExport = true)
        {
            var testName = parent != null ? NameReference.FromInstancedString($"{parent.ParentInstancedFullPath}.{packageName.Instanced}") : packageName;
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

        public static ExportEntry CreateExport(IMEPackage pcc, NameReference name, string className, IEntry parent = null, Action<List<EntryStringPair>> relinkResultsAvailable = null, 
            bool indexed = true, 
            bool createWithStack = false,
            byte[] prePropBinary = null)
        {
            var rop = new RelinkerOptionsPackage() { ImportExportDependencies = true };
            var exp = new ExportEntry(pcc, parent, indexed ? pcc.GetNextIndexedName(name) : name, prePropBinary)
            {
                Class = EntryImporter.EnsureClassIsInFile(pcc, className, rop)
            };
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
