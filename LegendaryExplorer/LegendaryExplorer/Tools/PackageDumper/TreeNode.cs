/**
 * This file is from Mass Effect 3 Mod Manager Command Line Tools
 * https://github.com/Mgamerz/ModManagerCommandLineTools
 * (c) Mgamerz 2019
 */

using LegendaryExplorer.UserControls.ExportLoaderControls;
using System.IO;
using System.Linq;

namespace LegendaryExplorer.Tools.PackageDumper
{
    public class TreeNode
    {
        public BinaryInterpreterWPF.NodeType Tag { get; set; }
        public string Text { get; set; }
        public string Name { get; set; }

        public TreeNode(string text)
        {
            Text = text;
            Children.containingnode = this;
        }

        public TreeNode()
        {
            Children.containingnode = this;
        }

        public TreeNode this[int i] => Children[i];

        public TreeNode Parent { get; set; }

        public TreeNodeList Children { get; } = new TreeNodeList();

        public TreeNodeList Nodes => Children;

        public TreeNode Add(string value)
        {
            var node = new TreeNode(value) { Parent = this };
            Children.Add(node);
            return node;
        }

        public TreeNode Add(TreeNode node)
        {
            node.Parent = this;
            Children.Add(node);
            return node;
        }

        public TreeNode[] AddChildren(params string[] values)
        {
            return values.Select(Add).ToArray();
        }

        public bool RemoveChild(TreeNode node)
        {
            return Children.Remove(node);
        }

        public TreeNode LastNode => Children.Last();

        public void Remove()
        {
            Parent.Children.Remove(this);
        }

        public void PrintPretty(string indent, StreamWriter str, bool last)
        {
            str.Write(indent);
            if (last)
            {
                str.Write("└─");
                indent += "  ";
            }
            else
            {
                str.Write("├─");
                indent += "| ";
            }
            str.Write(Text);
            if (Children.Count > 1000 && Tag == BinaryInterpreterWPF.NodeType.ArrayProperty)
            {
                str.Write($" > 1000, ({Children.Count}) suppressed.");
                return;
            }

            if (Tag == BinaryInterpreterWPF.NodeType.ArrayProperty && (Text.Contains("LookupTable") || Text.Contains("CompressedTrackOffsets")))
            {
                str.Write(" - suppressed by data dumper.");
                return;
            }
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i].Tag == BinaryInterpreterWPF.NodeType.None)
                {
                    continue;
                }
                str.Write("\n");
                Children[i].PrintPretty(indent, str, i == Children.Count - 1 || (i == Children.Count - 2 && Children[Children.Count - 1].Tag == BinaryInterpreterWPF.NodeType.None));
            }
        }
    }
}
