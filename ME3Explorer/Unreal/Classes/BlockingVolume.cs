using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using KFreonLib.Debugging;

namespace ME3Explorer.Unreal.Classes
{
    public class BlockingVolume
    {
#region Unreal Props
        //booleans
        public bool bAllowFluidSurfaceInteraction = false;
        public bool bBlockCamera = false;
        public bool bCanStepUpOn = false;
        public bool bCollideActors = false;
        public bool bHiddenEdGroup = false;
        public bool bInclusionaryList = false;
        public bool bPathColliding = false;
        public bool bSafeFall = false;
        public bool OverridePhysMat = false;
        //object index
        public int Brush;
        public int BrushComponent;
        public int CollisionComponent;
        //name index
        public int Tag;
        public int Group;
        //structs
        public Vector3 location;
        public Vector3 DrawScale3D = new Vector3(1, 1, 1);
#endregion

        public int MyIndex;
        public PCCObject pcc;
        public byte[] data;
        public List<PropertyReader.Property> Props;
        public BrushComponent brush;
        public Matrix MyMatrix;
        public bool isEdited = false;

        public BlockingVolume(PCCObject Pcc, int Index)
        {
            pcc = Pcc;
            MyIndex = Index;
            if (pcc.isExport(Index))
                data = pcc.Exports[Index].Data;
            Props = PropertyReader.getPropList(pcc, data);
            BitConverter.IsLittleEndian = true;
            Vector3 v;
            Tag = -1;
            Group = -1;
            foreach (PropertyReader.Property p in Props)
                switch (pcc.getNameEntry(p.Name))
                {
                    #region Props
                    case "bAllowFluidSurfaceInteraction":
                        if (p.raw[p.raw.Length - 1] == 1)
                            bAllowFluidSurfaceInteraction = true;
                        break;
                    case "bBlockCamera":
                        if (p.raw[p.raw.Length - 1] == 1)
                            bBlockCamera = true;
                        break;
                    case "bCanStepUpOn":
                        if (p.raw[p.raw.Length - 1] == 1)
                            bCanStepUpOn = true;
                        break;
                    case "bCollideActors":
                        if (p.raw[p.raw.Length - 1] == 1)
                            bCollideActors = true;
                        break;
                    case "bHiddenEdGroup":
                        if (p.raw[p.raw.Length - 1] == 1)
                            bHiddenEdGroup = true;
                        break;
                    case "bInclusionaryList":
                        if (p.raw[p.raw.Length - 1] == 1)
                            bInclusionaryList = true;
                        break;
                    case "bPathColliding":
                        if (p.raw[p.raw.Length - 1] == 1)
                            bPathColliding = true;
                        break;
                    case "bSafeFall":
                        if (p.raw[p.raw.Length - 1] == 1)
                            bSafeFall = true;
                        break;
                    case "OverridePhysMat":
                        if (p.raw[p.raw.Length - 1] == 1)
                            OverridePhysMat = true;
                        break;
                    case "Brush":
                        Brush = p.Value.IntValue;
                        break;
                    case "BrushComponent":
                        BrushComponent = p.Value.IntValue;
                        if (pcc.isExport(BrushComponent - 1) && pcc.Exports[BrushComponent - 1].ClassName == "BrushComponent")
                            brush = new BrushComponent(pcc, BrushComponent - 1);
                        break;
                    case "CollisionComponent":
                        CollisionComponent = p.Value.IntValue;
                        break;
                    case "Tag":
                        Tag = p.Value.IntValue;
                        break;
                    case "Group":
                        Group = p.Value.IntValue;
                        break;
                    case "location":
                        v.X = BitConverter.ToSingle(p.raw, p.raw.Length - 12);
                        v.Y = BitConverter.ToSingle(p.raw, p.raw.Length - 8);
                        v.Z = BitConverter.ToSingle(p.raw, p.raw.Length - 4);
                        location = v;
                        break;
                    case "DrawScale3D":                        
                        v.X = BitConverter.ToSingle(p.raw, p.raw.Length - 12);
                        v.Y = BitConverter.ToSingle(p.raw, p.raw.Length - 8);
                        v.Z = BitConverter.ToSingle(p.raw, p.raw.Length - 4);
                        DrawScale3D = v;
                        break;
                    #endregion
                }
            MyMatrix = Matrix.Scaling(DrawScale3D) * Matrix.Translation(location);
        }

        public void ProcessTreeClick(int[] path, bool AutoFocus)
        {
            if (brush != null)
                brush.SetSelection(true);
        }

        public void SetSelection(bool Selected)
        {
            if (brush != null)
                brush.SetSelection(Selected);
        }

        public void ApplyTransform(Matrix m)
        {
            if (brush != null && brush.isSelected)
            {
                isEdited = true;
                MyMatrix *= m;
            }
        }

        public void SaveChanges()
        {
            if (isEdited)
            {
                Matrix m = MyMatrix;
                Vector3 loc = new Vector3(m.M41, m.M42, m.M43);
                byte[] buff = Vector3ToBuff(loc);
                int f = -1;
                for (int i = 0; i < Props.Count; i++)
                    if (pcc.getNameEntry(Props[i].Name) == "location")
                    {
                        f = i;
                        break;
                    };
                if (f != -1)//has prop
                {
                    int off = Props[f].offend - 12;
                    for (int i = 0; i < 12; i++)
                        data[off + i] = buff[i];
                }
                else//have to add prop
                {
                    DebugOutput.PrintLn(MyIndex + " : cant find location property");
                }
                pcc.Exports[MyIndex].Data = data;
            }
        }

        public void CreateModJobs()
        {
            if (isEdited)
            {
                Matrix m = MyMatrix;
                Vector3 loc = new Vector3(m.M41, m.M42, m.M43);
                byte[] buff = Vector3ToBuff(loc);
                int f = -1;
                for (int i = 0; i < Props.Count; i++)
                    if (pcc.getNameEntry(Props[i].Name) == "location")
                    {
                        f = i;
                        break;
                    };
                if (f != -1)//has prop
                {
                    int off = Props[f].offend - 12;
                    for (int i = 0; i < 12; i++)
                        data[off + i] = buff[i];
                }
                else//have to add prop
                {
                    DebugOutput.PrintLn(MyIndex + " : cant find location property");
                }
                KFreonLib.Scripting.ModMaker.ModJob mj = new KFreonLib.Scripting.ModMaker.ModJob();
                string currfile = Path.GetFileName(pcc.pccFileName);
                mj.data = data;
                mj.Name = "Binary Replacement for file \"" + currfile + "\" in Object #" + MyIndex + " with " + data.Length + " bytes of data";
                string lc = Path.GetDirectoryName(Application.ExecutablePath);
                string template = System.IO.File.ReadAllText(lc + "\\exec\\JobTemplate_Binary2.txt");
                template = template.Replace("**m1**", MyIndex.ToString());
                template = template.Replace("**m2**", currfile);
                mj.Script = template;
                KFreonLib.Scripting.ModMaker.JobList.Add(mj);
                DebugOutput.PrintLn("Created Mod job : " + mj.Name);
            }
        }

        public byte[] Vector3ToBuff(Vector3 v)
        {
            MemoryStream m = new MemoryStream();
            BitConverter.IsLittleEndian = true;
            m.Write(BitConverter.GetBytes(v.X), 0, 4);
            m.Write(BitConverter.GetBytes(v.Y), 0, 4);
            m.Write(BitConverter.GetBytes(v.Z), 0, 4);
            return m.ToArray();
        }

        public void Render(Device device)
        {
            device.Transform.World = MyMatrix;
            if (brush != null)
                brush.Render(device);
        }

        public TreeNode ToTree()
        {
            TreeNode res = new TreeNode(pcc.Exports[MyIndex].ObjectName + "(#" + MyIndex + ")");
            res.Nodes.Add("bAllowFluidSurfaceInteraction : " + bAllowFluidSurfaceInteraction);
            res.Nodes.Add("bBlockCamera : " + bBlockCamera);
            res.Nodes.Add("bCanStepUpOn : " + bCanStepUpOn);
            res.Nodes.Add("bCollideActors : " + bCollideActors);
            res.Nodes.Add("bHiddenEdGroup : " + bHiddenEdGroup);
            res.Nodes.Add("bInclusionaryList : " + bInclusionaryList);
            res.Nodes.Add("bPathColliding : " + bPathColliding);
            res.Nodes.Add("bSafeFall : " + bSafeFall);
            res.Nodes.Add("OverridePhysMat : " + OverridePhysMat);
            res.Nodes.Add("Brush : #" + Brush);
            res.Nodes.Add("BrushComponent : #" + BrushComponent);
            res.Nodes.Add("CollisionComponent : #" + CollisionComponent);
            res.Nodes.Add("Tag : " + pcc.getNameEntry(Tag));
            res.Nodes.Add("Group : " + pcc.getNameEntry(Group));
            res.Nodes.Add("location : (" + location.X + "; " + location.Y + "; " + location.Z + ")");
            res.Nodes.Add("DrawScale3D : (" + DrawScale3D.X + "; " + DrawScale3D.Y + "; " + DrawScale3D.Z + ")");
            if (brush != null)
                res.Nodes.Add(brush.ToTree());
            return res;
        }

    }
}
