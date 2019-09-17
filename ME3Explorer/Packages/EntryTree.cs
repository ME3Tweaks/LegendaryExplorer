using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ME3Explorer.Unreal.BinaryConverters;

namespace ME3Explorer.Packages
{
    public class EntryTree : IEnumerable<IEntry>
    {
        public EntryTree(IMEPackage pcc)
        {
            imports = pcc.Imports.Select(import => new TreeNode<IEntry, int>(import)).ToList();
            exports = pcc.Exports.Select(export => new TreeNode<IEntry, int>(export)).ToList();

            foreach (TreeNode<IEntry, int> node in exports.Concat(imports))
            {
                this[node.Data.idxLink]?.Children.Add(node.Data.UIndex);
            }
        }

        private readonly List<TreeNode<IEntry, int>> imports;
        private readonly List<TreeNode<IEntry, int>> exports;

        private TreeNode<IEntry, int> this[int index]
        {
            get
            {
                if (index > 0 && index <= exports.Count)
                {
                    return exports[index - 1];
                }

                index = -index - 1;
                if (index >= 0 && index < imports.Count)
                {
                    return imports[index];
                }

                return null;
            }
        }

        public int NumChildrenOf(IEntry entry) => this[entry.UIndex]?.Children.Count ?? 0;

        public int NumChildrenOf(int uIndex) => this[uIndex]?.Children.Count ?? 0;

        public IEnumerable<IEntry> GetDirectChildrenOf(IEntry entry) => GetDirectChildrenOf(entry.UIndex);

        public IEnumerable<IEntry> GetDirectChildrenOf(int uIndex)
        {
            foreach (int i in this[uIndex])
            {
                yield return this[i].Data;
            }
        }

        public List<IEntry> FlattenTreeOf(int uIndex)
        {
            var entries = new List<IEntry> {this[uIndex].Data};
            foreach (int i in this[uIndex])
            {
                entries.AddRange(FlattenTreeOf(i));
            }

            return entries;
        }

        public IEntry GetEntry(int uIndex)
        {
            return this[uIndex].Data;
        }

        public IEnumerator<IEntry> GetEnumerator()
        {
            foreach (TreeNode<IEntry, int> node in exports)
            {
                yield return node.Data;
            }
            foreach (TreeNode<IEntry, int> node in imports)
            {
                yield return node.Data;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}