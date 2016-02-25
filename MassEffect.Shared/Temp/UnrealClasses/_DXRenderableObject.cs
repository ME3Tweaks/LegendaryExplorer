using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace MassEffect.Shared.Temp.UnrealClasses
{
    public abstract class _DXRenderableObject
    {
        public int MyIndex;
        public PCCPackage pcc;
        public List<PropertyReader.Property> Props;
        public bool Selected;

        public abstract void Render(Device device);
        public abstract TreeNode ToTree();
        public abstract float Process3DClick(Vector3 org, Vector3 dir, float lastdist);
        public abstract void SetSelection(bool s);
    }
}
