using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
namespace ME3LibWV.UnrealClasses
{
    public class ModelComponent: _DXRenderableObject
    {
        public ModelComponent(PCCPackage Pcc, int index)
        {
            pcc = Pcc;
            MyIndex = index;
        }

        public override TreeNode ToTree()
        {
            TreeNode t = new TreeNode("E#" + MyIndex.ToString("d6") + " : " + pcc.getObjectName(MyIndex + 1));
            t.Name = (MyIndex + 1).ToString();
            return t;
        }

        public override void SetSelection(bool s)
        {
            Selected = s;
        }
    }
}
