using System;
using System.Collections.Generic;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3ExplorerCore.Unreal;

namespace ME3ExplorerCore.Packages
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
        /// <param name="zeroIndexed"></param>
        /// <returns></returns>
        public static ExportEntry CreatePackageExport(IMEPackage pcc, string packageName, IEntry parent = null, Action<List<EntryStringPair>> relinkResultsAvailable = null, bool zeroIndexed = false)
        {
            var exp = new ExportEntry(pcc)
            {
                ObjectName = zeroIndexed ? new NameReference(packageName, 0) : pcc.GetNextIndexedName(packageName),
                Class = EntryImporter.EnsureClassIsInFile(pcc, "Package", RelinkResultsAvailable: relinkResultsAvailable),
                Parent = parent
            };
            exp.ObjectFlags |= UnrealFlags.EObjectFlags.Public;
            exp.ExportFlags |= UnrealFlags.EExportFlags.ForcedExport;
            pcc.AddExport(exp);
            exp.indexValue = 0; // Why would you want this not index starting at 0
            return exp;
        }

        public static ExportEntry CreateExport(IMEPackage pcc, string name, string className, IEntry parent = null, Action<List<EntryStringPair>> relinkResultsAvailable = null)
        {
            var exp = new ExportEntry(pcc)
            {
                ObjectName = pcc.GetNextIndexedName(name),
                Class = EntryImporter.EnsureClassIsInFile(pcc, className, RelinkResultsAvailable: relinkResultsAvailable),
                Parent = parent
            };
            pcc.AddExport(exp);
            return exp;
        }
    }
}
