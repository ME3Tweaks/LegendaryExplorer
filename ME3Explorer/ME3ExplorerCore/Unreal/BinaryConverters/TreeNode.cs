using System.Collections;
using System.Collections.Generic;

namespace ME3ExplorerCore.Unreal.BinaryConverters
{
    public class TreeNode<T, U> : IEnumerable<U>
    {
        public readonly T Data;
        public readonly List<U> Children;

        public TreeNode(T data)
        {
            Data = data;
            Children = new List<U>();
        }

        public void Add(U item) => Children.Add(item);

        public IEnumerator<U> GetEnumerator() => Children.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
