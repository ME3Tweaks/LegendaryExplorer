using System.Collections.Generic;

namespace LegendaryExplorerCore.Packages.CloningImportingAndRelinking
{
    public static class EntryCloner
    {
        public static T CloneTree<T>(T entry, bool incrementIndex = true) where T : IEntry
        {
            var objectMap = new Dictionary<IEntry, IEntry>();
            T newRoot = CloneEntry(entry, objectMap, incrementIndex);
            var tree = new EntryTree(entry.FileRef);
            cloneTreeRecursive(entry, newRoot);
            Relinker.RelinkAll(objectMap);
            return newRoot;

            void cloneTreeRecursive(IEntry originalRootNode, IEntry newRootNode)
            {
                foreach (IEntry node in tree.GetDirectChildrenOf(originalRootNode.UIndex))
                {
                    IEntry newEntry = CloneEntry(node, objectMap);
                    newEntry.Parent = newRootNode;
                    cloneTreeRecursive(node, newEntry);
                }
            }
        }

        // incrementIndex should be removed or it'll screw up the name <-> entry map
        public static T CloneEntry<T>(T entry, Dictionary<IEntry, IEntry> objectMap = null, bool incrementIndex = false) where T : IEntry
        {
            IEntry newEntry = entry.Clone();
            if (incrementIndex)
            {
                newEntry.indexValue = newEntry.FileRef.GetNextIndexForInstancedName(newEntry);
            }

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
