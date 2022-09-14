﻿using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Packages
{
    public static class ExportCreator
    {
        /// <summary>
        /// Creates a package export. The default implementation does not use zero index (it will start at 1). Usages should be investigate to see if this is ever useful, I don't think it is
        /// </summary>
        /// <param name="pcc"></param>
        /// <param name="packageName"></param>
        /// <param name="parent"></param>
        /// <param name="relinkResultsAvailable"></param>
        /// <returns></returns>
        public static ExportEntry CreatePackageExport(IMEPackage pcc, NameReference packageName, IEntry parent = null, Action<List<EntryStringPair>> relinkResultsAvailable = null)
        {
            var rop = new RelinkerOptionsPackage { ImportExportDependencies = true };
            var exp = new ExportEntry(pcc, parent, packageName)
            {
                Class = EntryImporter.EnsureClassIsInFile(pcc, "Package", rop)
            };
            relinkResultsAvailable?.Invoke(rop.RelinkReport);
            exp.ObjectFlags |= UnrealFlags.EObjectFlags.Public;
            exp.ExportFlags |= UnrealFlags.EExportFlags.ForcedExport;
            pcc.AddExport(exp);
            return exp;
        }

        public static ExportEntry CreateExport(IMEPackage pcc, string name, string className, IEntry parent = null, Action<List<EntryStringPair>> relinkResultsAvailable = null)
        {
            var rop = new RelinkerOptionsPackage() { ImportExportDependencies = true };
            var exp = new ExportEntry(pcc, parent, pcc.GetNextIndexedName(name))
            {
                Class = EntryImporter.EnsureClassIsInFile(pcc, className, rop)
            };
            relinkResultsAvailable?.Invoke(rop.RelinkReport);
            pcc.AddExport(exp);
            return exp;
        }
    }
}
