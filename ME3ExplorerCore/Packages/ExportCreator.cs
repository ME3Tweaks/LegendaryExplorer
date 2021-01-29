using System;
using System.Collections.Generic;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3ExplorerCore.Unreal;

namespace ME3ExplorerCore.Packages
{
    public static class ExportCreator
    {
        public static ExportEntry CreatePackageExport(IMEPackage pcc, string packageName, IEntry parent = null, Action<List<EntryStringPair>> relinkResultsAvailable = null)
        {
            var exp = new ExportEntry(pcc)
            {
                ObjectName = new NameReference(packageName, 0), // I cna't think a single time where a non-zero instanced package could be useful, but i may be wrong someday.
                Class = EntryImporter.EnsureClassIsInFile(pcc, "Package", RelinkResultsAvailable: relinkResultsAvailable),
                Parent = parent
            };
            exp.ObjectFlags |= UnrealFlags.EObjectFlags.Public;
            exp.ExportFlags |= UnrealFlags.EExportFlags.ForcedExport;
            pcc.AddExport(exp);
            // Why would you want this not index starting at 0
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
