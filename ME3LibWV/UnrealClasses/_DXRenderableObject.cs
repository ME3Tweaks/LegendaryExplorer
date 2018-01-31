using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3LibWV.UnrealClasses
{
    public abstract class _DXRenderableObject
    {
        public int MyIndex;
        public PCCPackage pcc;
        public List<PropertyReader.Property> Props;
        public bool Selected;

        public abstract TreeNode ToTree();
        public abstract void SetSelection(bool s);
    }
}
