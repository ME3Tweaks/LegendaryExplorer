using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Misc;

namespace LegendaryExplorerCore.Packages.CloningImportingAndRelinking
{
    public static class EntryCloner
    {
        public static T CloneTree<T>(T entry, bool incrementIndex = true) where T : IEntry
        {
            var objectMap = new ListenableDictionary<IEntry, IEntry>();
            T newRoot = CloneEntry(entry, objectMap, incrementIndex);
            EntryTree tree = entry.FileRef.Tree;
            var stack = new Stack<(IEntry, IEntry)>();
            stack.Push((entry, newRoot));
            while (stack.TryPop(out var pair))
            {
                (IEntry originalRootNode, IEntry newRootNode) = pair;
                foreach (IEntry node in tree.GetDirectChildrenOf(originalRootNode.UIndex))
                {
                    IEntry newEntry = CloneEntry(node, objectMap, false, newRootNode.UIndex);
                    stack.Push((node, newEntry));
                }
            }
            Relinker.RelinkAll(new RelinkerOptionsPackage {CrossPackageMap = objectMap});
            return newRoot;
        }
        
        public static T CloneEntry<T>(T entry, IDictionary<IEntry, IEntry> objectMap = null, bool incrementIndex = true, int newParentUIndex = int.MaxValue) where T : IEntry
        {
            bool shouldIncrement = incrementIndex && entry is ExportEntry; // Why is this only for exports?
            IEntry newEntry = entry.Clone(shouldIncrement, newParentUIndex);

            switch (newEntry)
            {
                case ExportEntry export:
                    entry.FileRef.AddExport(export);
                    if (objectMap != null)
                    {
                        objectMap[entry] = export;
                    }
                    break;
                case ImportEntry import:
                    entry.FileRef.AddImport(import);
                    //Imports are not relinked when cloning
                    break;
            }

            return (T)newEntry;
        }
    }
}
