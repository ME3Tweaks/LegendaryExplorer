using System.Collections.Generic;
using ME3Explorer;

namespace ME3ExplorerCore.Packages.CloningImportingAndRelinking
{
    public static class EntryCloner
    {
        public static T CloneTree<T>(T entry) where T : IEntry
        {
            var objectMap = new Dictionary<IEntry, IEntry>();
            T newRoot = CloneEntry(entry, objectMap);
            EntryTree tree = new EntryTree(entry.FileRef);
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

        public static T CloneEntry<T>(T entry, Dictionary<IEntry, IEntry> objectMap = null) where T : IEntry
        {
            IEntry newEntry = entry.Clone();
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
