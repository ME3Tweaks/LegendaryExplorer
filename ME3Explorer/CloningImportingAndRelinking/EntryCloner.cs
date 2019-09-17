using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using ME3Explorer.Unreal.BinaryConverters;

namespace ME3Explorer
{
    public static class EntryCloner
    {
        public static IEntry cloneTree(IEntry entry)
        {
            var objectMap = new Dictionary<IEntry, IEntry>();
            IEntry newRoot = cloneEntry(entry, objectMap);
            EntryTree tree = new EntryTree(entry.FileRef);
            cloneTreeRecursive(entry, newRoot);
            IMEPackage pcc = entry.FileRef;
            Relinker.RelinkAll(objectMap, pcc);
            return newRoot;

            void cloneTreeRecursive(IEntry originalRootNode, IEntry newRootNode)
            {
                foreach (IEntry node in tree.GetDirectChildrenOf(originalRootNode.UIndex))
                {
                    IEntry newEntry = cloneEntry(node, objectMap);
                    newEntry.Parent = newRootNode;
                    cloneTreeRecursive(node, newEntry);
                }
            }
        }

        public static IEntry cloneEntry(IEntry entry, Dictionary<IEntry, IEntry> objectMap = null)
        {
            IEntry newEntry;
            if (entry is ExportEntry export)
            {
                ExportEntry ent = export.Clone();
                entry.FileRef.addExport(ent);
                newEntry = ent;
                if (objectMap != null)
                {
                    objectMap[export] = ent;
                }
            }
            else
            {
                ImportEntry imp = ((ImportEntry)entry).Clone();
                entry.FileRef.addImport(imp);
                newEntry = imp;
                //Imports are not relinked when cloning
            }

            return newEntry;
        }
    }
}
