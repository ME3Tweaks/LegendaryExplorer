using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3LibWV.UnrealClasses
{
    public class ModelComponent: _DXRenderableObject
    {
        public ModelComponent(PCCPackage Pcc, int index)
        {
            pcc = Pcc;
            MyIndex = index;
        }

        public override void Render(Device device)
        {
            
        }

        public override TreeNode ToTree()
        {
            TreeNode t = new TreeNode("E#" + MyIndex.ToString("d6") + " : " + pcc.GetObject(MyIndex + 1));
            t.Name = (MyIndex + 1).ToString();
            return t;
        }

        public override void SetSelection(bool s)
        {
            Selected = s;
        }

        public override float Process3DClick(Vector3 org, Vector3 dir, float max)
        {
            return -1f;
        }

    }
}
