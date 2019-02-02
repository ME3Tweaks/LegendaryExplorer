/**
 * This file is from Mass Effect 3 Mod Manager Command Line Tools
 * https://github.com/Mgamerz/ModManagerCommandLineTools
 * (c) Mgamerz 2019
 */

using ME3Explorer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffect3ModManagerCmdLine
{
    public class TreeNode
    {
        private readonly TreeNodeList _children = new TreeNodeList();
        public Interpreter.NodeType Tag { get; set; }
        public string Text { get; set; }
        public string Name { get; set; }

        public TreeNode(string text)
        {
            Text = text;
            _children.containingnode = this;
        }

        public TreeNode()
        {
            _children.containingnode = this;
        }

        public TreeNode this[int i]
        {
            get { return _children[i]; }
        }

        public TreeNode Parent { get; set; }

        public TreeNodeList Children
        {
            get { return _children; }
        }

        public TreeNodeList Nodes
        {
            get { return _children; }
        }

        public TreeNode Add(string value)
        {
            var node = new TreeNode(value) { Parent = this };
            _children.Add(node);
            return node;
        }

        public TreeNode Add(TreeNode node)
        {
            node.Parent = this;
            _children.Add(node);
            return node;
        }

        public TreeNode[] AddChildren(params string[] values)
        {
            return values.Select(Add).ToArray();
        }

        public bool RemoveChild(TreeNode node)
        {
            return _children.Remove(node);
        }

        public TreeNode LastNode
        {
            get
            {
                return _children.Last();
            }
        }

        public void Remove()
        {
            Parent._children.Remove(this);
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
            if (Children.Count > 1000 && Tag == Interpreter.NodeType.ArrayProperty)
            {
                str.Write(" > 1000, (" + Children.Count + ") suppressed.");
                return;
            }

            if (Tag == Interpreter.NodeType.ArrayProperty && (Text.Contains("LookupTable") || Text.Contains("CompressedTrackOffsets")))
            {
                str.Write(" - suppressed by data dumper.");
                return;
            }
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i].Tag == Interpreter.NodeType.None)
                {
                    continue;
                }
                str.Write("\n");
                Children[i].PrintPretty(indent, str, i == Children.Count - 1 || (i == Children.Count - 2 && Children[Children.Count - 1].Tag == Interpreter.NodeType.None));
            }
            return;
        }
    }
}
