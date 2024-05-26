/**
 * This file is from Mass Effect 3 Mod Manager Command Line Tools
 * https://github.com/Mgamerz/ModManagerCommandLineTools
 * (c) Mgamerz 2019
 */

using System.Collections.Generic;

namespace LegendaryExplorer.Tools.PackageDumper
{
    public class TreeNodeList : List<TreeNode>
    {
        public TreeNode containingnode = null;
        public new void Add(TreeNode node)
        {
            node.Parent = this.containingnode;
            base.Add(node);
        }
    }
}
