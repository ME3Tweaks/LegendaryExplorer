using System.Collections.Generic;
using System.Linq;

namespace LegendaryExplorerCore.Packages.CloningImportingAndRelinking
{
    public static class EntryCloner
    {
        public static T CloneTree<T>(T entry, bool incrementIndex = true) where T : IEntry
        {
            var objectMap = new Dictionary<IEntry, IEntry>();
            T newRoot = CloneEntry(entry, objectMap, incrementIndex);
            cloneTreeRecursive(entry, newRoot);
            Relinker.RelinkAll(objectMap);
            return newRoot;

            void cloneTreeRecursive(IEntry originalRootNode, IEntry newRootNode)
            {
                foreach (IEntry node in originalRootNode.GetChildren().ToList())
                {
                    IEntry newEntry = CloneEntry(node, objectMap, false);
                    newEntry.Parent = newRootNode;
                    cloneTreeRecursive(node, newEntry);
                }
            }
        }
        
        public static T CloneEntry<T>(T entry, Dictionary<IEntry, IEntry> objectMap = null, bool incrementIndex = true) where T : IEntry
        {
            bool shouldIncrement = incrementIndex && entry is ExportEntry;
            IEntry newEntry = entry.Clone(shouldIncrement);

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
