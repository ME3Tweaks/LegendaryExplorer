using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.UnrealScript.Language.Tree;

namespace LegendaryExplorerCore.UnrealScript.Compiling
{
    public static class CompilerUtils
    {
        public static bool TryGetTrash<T>(this IMEPackage pcc, out T entry) where T : class, IEntry
        {
            IEntry trashPackage = pcc.FindEntry(UnrealPackageFile.TrashPackageName, "Package");
            if (trashPackage is null)
            {
                entry = null;
                return false;
            }
            var trashChildren = trashPackage.GetChildren();
            bool hasChildren = false;
            foreach (IEntry trashChild in trashChildren)
            {
                if (trashChild is T tChild)
                {
                    entry = tChild;
                    return true;
                }
                hasChildren = true;
            }
            if (!hasChildren && trashPackage is T tPackage)
            {
                entry = tPackage;
                return true;
            }
            entry = null;
            return false;
        }

        public static IEntry ResolveSymbol(ASTNode node, IMEPackage pcc) =>
            node switch
            {
                Class cls => ResolveClass(cls, pcc),
                Struct strct => ResolveStruct(strct, pcc),
                State state => ResolveState(state, pcc),
                Function func => ResolveFunction(func, pcc),
                Enumeration @enum => ResolveEnum(@enum, pcc),
                StaticArrayType statArr => ResolveSymbol(statArr.ElementType, pcc),
                _ => throw new ArgumentOutOfRangeException(nameof(node))
            };

        public static IEntry ResolveEnum(Enumeration e, IMEPackage pcc) => pcc.GetEntryOrAddImport($"{ResolveSymbol(e.Outer, pcc).InstancedFullPath}.{e.Name}", "Enum");
        public static IEntry ResolveStruct(Struct s, IMEPackage pcc) => pcc.GetEntryOrAddImport($"{ResolveSymbol(s.Outer, pcc).InstancedFullPath}.{s.Name}", "ScriptStruct");
        public static IEntry ResolveFunction(Function f, IMEPackage pcc) => pcc.GetEntryOrAddImport($"{ResolveSymbol(f.Outer, pcc).InstancedFullPath}.{f.Name}", "Function");
        public static IEntry ResolveState(State s, IMEPackage pcc) => pcc.GetEntryOrAddImport($"{ResolveSymbol(s.Outer, pcc).InstancedFullPath}.{s.Name}", "State");

        public static IEntry ResolveClass(Class c, IMEPackage pcc)
        {
            var rop = new RelinkerOptionsPackage { ImportExportDependencies = true }; // Might need to disable cache here depending on if that is desirable
            if (!GlobalUnrealObjectInfo.GetClasses(pcc.Game).ContainsKey(c.Name) && c.FilePath is not null)
            {
                if (c.FilePath == pcc.FilePath)
                {
                    // It's part of the current package - e.g. we're adding a new class in porting
                    GlobalUnrealObjectInfo.generateClassInfo(pcc.GetUExport(c.UIndex));
                }
                else
                {
                    using IMEPackage classPcc = MEPackageHandler.OpenMEPackage(c.FilePath);
                    GlobalUnrealObjectInfo.generateClassInfo(classPcc.GetUExport(c.UIndex));
                }
            }
            var entry = EntryImporter.EnsureClassIsInFile(pcc, c.Name, rop);
            if (rop.RelinkReport.Any())
            {
                throw new Exception($"Unable to resolve class '{c.Name}'! There were relinker errors: {string.Join("\n\t", rop.RelinkReport.Select(pair => pair.Message))}");
            }
            return entry;
        }
    }
}