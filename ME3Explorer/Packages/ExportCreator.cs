using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;

namespace ME3Explorer.Packages
{
    public static class ExportCreator
    {
        public static ExportEntry CreatePackageExport(IMEPackage pcc, string packageName, IEntry parent = null)
        {
            var exp = new ExportEntry(pcc)
            {
                ObjectName = pcc.GetNextIndexedName(packageName),
                Class = EntryImporterExtended.EnsureClassIsInFile(pcc, "Package"),
                Parent = parent
            };
            exp.ObjectFlags |= UnrealFlags.EObjectFlags.Public;
            exp.ExportFlags |= UnrealFlags.EExportFlags.ForcedExport;
            pcc.AddExport(exp);
            exp.indexValue = 0; // Why would you want this not index starting at 0 (afaik only trash uses indexing > 0 for package exports)
            return exp;
        }

        public static ExportEntry CreateExport(IMEPackage pcc, string name, string className, IEntry parent = null)
        {
            var exp = new ExportEntry(pcc)
            {
                ObjectName = pcc.GetNextIndexedName(name),
                Class = EntryImporterExtended.EnsureClassIsInFile(pcc, className),
                Parent = parent
            };
            pcc.AddExport(exp);
            return exp;
        }
    }
}
