using System.Collections;
using System.Collections.Generic;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class TreeNode<T, U> : IEnumerable<U>
    {
        public readonly T Data;
        public readonly List<U> Children;

        public TreeNode(T data)
        {
            Data = data;
            Children = [];
        }

        public void Add(U item) => Children.Add(item);

        public IEnumerator<U> GetEnumerator() => Children.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public class TreeNode<T> : IEnumerable<TreeNode<T>>
    {
        public readonly T Data;
        public readonly List<TreeNode<T>> Children;

        public TreeNode(T data)
        {
            Data = data;
            Children = [];
        }

        public void Add(TreeNode<T> item) => Children.Add(item);

        public IEnumerator<TreeNode<T>> GetEnumerator() => Children.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
