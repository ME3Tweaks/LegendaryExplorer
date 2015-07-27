using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3Explorer.UnrealHelper
{
    public class ULevel
    {        
        public byte[] memory;
        public Device device;
        public PCCFile pcc;
        public UPropertyReader UPR;
        public List<UPropertyReader.Property> props;
        public List<LevelObject> LObjects;
        public List<UStaticMeshComponent> UStatComp;
        public List<UStaticMeshActor> UStatActors;
        public List<UStaticMeshCollectionActor> UStatColl;
        public List<UInterpActor> UIntAct;
        public List<UStaticMesh> UStat;
        public List<TextureEntry> Tex;

        public RichTextBox rtb;

        public struct LevelProperties
        {
            public int source;
            //For StaticMesh
            //From:
            //0 - StaticMeshComponent
            //
            //For StaticMeshComponent
            //From:
            //0 - StaticMeshActor
            //1 - StaticMeshCollectionActor
            //2 - InterpActor
            public int sourceentry;
            public ULevel.GlobalTransform GT;
            public int textureindex;
        }

        public struct TextureEntry
        {
            public Texture tex;
            public int index;
            public string name;
        }

        public struct GlobalTransform
        {
            public Vector3 loc;
            public Vector3 rot;
            public Vector3 scl;
            public Matrix m;
            public float scl2;
        }
        public struct LevelObject
        {
            public int entry;
            public string name;
            public string cls;
        }

        public struct DirectXObject
        {
            public CustomVertex.PositionNormalTextured[] verts;
            public Vector3 pos;
            public Vector3 tfpos;
            public Vector3 rot;
            public Vector3 box;
            public Vector3 boxorg;
            public float r;
            public float tfr;
            public Matrix m;
            public string name;
            public bool visible;
            public int objindex;
        }

        public ULevel(byte[] mem, PCCFile Pcc)
        {
            memory = mem;
            pcc = Pcc;
        }

#region Deserialize

        public void Deserialize()
        {
            props = UPR.readProperties(memory, "Level", pcc.names, 4);
            int offset = props[props.Count - 1].offset + props[props.Count - 1].raw.Length + 4;
            ReadObjects(offset);
        }

        public void ReadObjects(int off)
        {
            int count = BitConverter.ToInt32(memory, off);
            LObjects = new List<LevelObject>();
            int pos = off + 4;
            for (int i = 0; i < count; i++)
            {
                LevelObject l = new LevelObject();
                l.entry = BitConverter.ToInt32(memory, pos) -1;
                l.cls = "";
                l.name = "";
                if (l.entry >= 0 && l.entry < pcc.Export.Length)
                {
                    l.name = pcc.names[pcc.Export[l.entry].Name];
                    l.cls = pcc.getClassName(pcc.Export[l.entry].Class);
                }
                LObjects.Add(l);
                pos += 4;
            }
        }

        public void TextOut(string s)
        {
            if (rtb != null)
            {
                rtb.AppendText(s);
                rtb.SelectionStart = rtb.Text.Length; 
                //rtb.ScrollToCaret();   
            }
            Application.DoEvents();   
        }

        public void LoadLevelObjects(Device d, RichTextBox Rtb=null)
        {
            rtb = Rtb;
            device = d;
            UStatComp = new List<UStaticMeshComponent>();
            UStatActors = new List<UStaticMeshActor>();
            UStatColl = new List<UStaticMeshCollectionActor>();
            UIntAct = new List<UInterpActor>();
            UStat = new List<UStaticMesh>();
            Tex = new List<TextureEntry>();
            TextOut("Loading Level Objects...\n");
            for(int i=0;i<LObjects.Count;i++)
                switch(LObjects[i].cls)
                {
                    case "StaticMeshActor":
                        UStaticMeshActor t = new UStaticMeshActor(pcc.EntryToBuff(LObjects[i].entry), pcc.names);
                        t.UPR = UPR;
                        t.Deserialize();
                        LevelProperties LP = new LevelProperties();
                        LP.source = 0;
                        LP.sourceentry = LObjects[i].entry;
                        t.LP = LP;
                        UStatActors.Add(t);
                        break;                    
                    case "StaticMeshCollectionActor":
                        UStaticMeshCollectionActor t2 = new UStaticMeshCollectionActor(pcc.EntryToBuff(LObjects[i].entry), pcc);
                        t2.UPR = UPR;
                        t2.Deserialize();
                        LevelProperties LP2 = new LevelProperties();
                        LP2.source = 1;
                        LP2.sourceentry = LObjects[i].entry;
                        t2.LP = LP2;
                        UStatColl.Add(t2);
                        break;
                    case "InterpActor":
                        UInterpActor t3 = new UInterpActor(pcc.EntryToBuff(LObjects[i].entry), pcc.names);
                        t3.UPR = UPR;
                        t3.Deserialize();
                        LevelProperties LP3 = new LevelProperties();
                        LP3.source = 2;
                        LP3.sourceentry = LObjects[i].entry;
                        t3.LP = LP3;
                        UIntAct.Add(t3);
                        break;
                }
            TextOut("Loading Static Mesh Actors...\n");
            LoadStaticMeshActors();
            TextOut("Loading Static Mesh Collection Actors...\n");
            LoadStaticMeshCollectionActors();
            TextOut("Loading Interp Actors...\n");
            LoadInterpActors();
            TextOut("Loading Static Mesh Components...\n");
            LoadStaticMeshComponents();
            TextOut("Loading Static Meshes...\n");
            LoadStaticMesh();
            TextOut("Loading Textures...\n");
            LoadTextures();
            TextOut("\nDone");
        }

        public void LoadStaticMeshActors()
        {
            for (int i = 0; i < UStatActors.Count; i++)
            {
                int entry = UPR.FindObjectProperty("StaticMeshComponent", UStatActors[i].props);
                if (entry >= 0 && entry < pcc.Header.ExportCount)
                {
                    UStaticMeshComponent t = new UStaticMeshComponent(pcc.EntryToBuff(entry), pcc.names);
                    t.UPR = UPR;
                    t.Deserialize();
                    LevelProperties LP = UStatActors[i].LP;
                    GlobalTransform g = new GlobalTransform();
                    if (i == 21)
                    {
                    }
                    entry = UPR.FindProperty("location", UStatActors[i].props);
                    if (entry != -1) g.loc = PropToVector3(UStatActors[i].props[entry + 1].raw);
                    entry = UPR.FindProperty("Rotator", UStatActors[i].props);
                    if (entry != -1) g.rot = PropToCYPRVector3(UStatActors[i].props[entry].raw);
                    entry = UPR.FindProperty("DrawScale3D", UStatActors[i].props);
                    if (entry != -1) g.scl = PropToVector3(UStatActors[i].props[entry + 1].raw);
                    else g.scl = new Vector3(1, 1, 1);
                    entry = UPR.FindProperty("DrawScale", UStatActors[i].props);
                    if (entry != -1) g.scl2 = BitConverter.ToSingle(UStatActors[i].props[entry + 1].raw, UStatActors[i].props[entry + 1].raw.Length - 4);
                    else g.scl2 = 1;
                    LP.GT = g;
                    t.LP = LP;
                    t.index = entry;
                    UStatComp.Add(t);
                    
                }
            }
        }
        public void LoadStaticMeshCollectionActors()
        {
            for (int i = 0; i < UStatColl.Count; i++)
                for (int j = 0; j < UStatColl[i].Entries.Count; j++)
                    if (UStatColl[i].Entries[j] >= 0 && UStatColl[i].Entries[j] < pcc.Header.ExportCount)
                    {
                        UStaticMeshComponent t = new UStaticMeshComponent(pcc.EntryToBuff(UStatColl[i].Entries[j]), pcc.names);
                        t.UPR = UPR;
                        t.Deserialize();
                        LevelProperties LP = UStatColl[i].LP;
                        GlobalTransform g = new GlobalTransform();
                        g.m = UStatColl[i].Matrices[j];
                        g.scl = new Vector3(1, 1, 1);
                        g.scl2 = 1;
                        LP.GT = g;
                        t.LP = LP;
                        t.index = UStatColl[i].Entries[j];
                        t.index2 = j;
                        t.index3 = i;
                        UStatComp.Add(t);
                    }
        }
        public void LoadInterpActors()
        {
            for (int i = 0; i < UIntAct.Count; i++)
            {
                int entry = UPR.FindObjectProperty("StaticMeshComponent", UIntAct[i].props);
                if (entry >= 0 && entry < pcc.Header.ExportCount)
                {
                    UStaticMeshComponent t = new UStaticMeshComponent(pcc.EntryToBuff(entry), pcc.names);
                    t.UPR = UPR;
                    t.Deserialize();
                    t.index = entry;
                    LevelProperties LP = UIntAct[i].LP;
                    GlobalTransform g = new GlobalTransform();
                    entry = UPR.FindProperty("location", UIntAct[i].props);
                    if (entry != -1) g.loc = PropToVector3(UIntAct[i].props[entry + 1].raw);
                    entry = UPR.FindProperty("Rotator", UIntAct[i].props);
                    if (entry != -1) g.rot = PropToCYPRVector3(UIntAct[i].props[entry].raw);
                    entry = UPR.FindProperty("DrawScale3D", UIntAct[i].props);
                    if (entry != -1) g.scl = PropToVector3(UIntAct[i].props[entry + 1].raw);
                    else g.scl = new Vector3(1, 1, 1);
                    entry = UPR.FindProperty("DrawScale", UIntAct[i].props);
                    if (entry != -1) g.scl2 = BitConverter.ToSingle(UIntAct[i].props[entry + 1].raw, UIntAct[i].props[entry + 1].raw.Length - 4);
                    else g.scl2 = 1;
                    LP.GT = g;
                    t.LP = LP;
                    
                    UStatComp.Add(t);
                }
            }
        }
        public void LoadStaticMeshComponents()
        {
            for (int i = 0; i < UStatComp.Count; i++)
            {
                int entry = UPR.FindObjectProperty("StaticMesh", UStatComp[i].props);
                if (entry >= 0 && entry < pcc.Header.ExportCount)
                    if (pcc.getClassName(pcc.Export[entry].Class) == "StaticMesh")
                    {
                        UStaticMesh t = new UStaticMesh(pcc.EntryToBuff(entry), pcc.names);
                        t.ReadProperties(4);
                        t.index = entry;
                        t.index2 = UStatComp[i].index;
                        t.index3 = i;
                        entry = UPR.FindProperty("Scale3D", UStatComp[i].props);
                        LevelProperties LP = UStatComp[i].LP;
                        GlobalTransform g = UStatComp[i].LP.GT;
                        if (entry != -1)
                        {
                            Vector3 v = PropToVector3(UStatComp[i].props[entry + 1].raw);
                            g.scl.X *= v.X;
                            g.scl.Y *= v.Y;
                            g.scl.Z *= v.Z;
                        }
                        g.scl *= g.scl2;
                        LP.GT = g;
                        t.LP = LP;
                        UStat.Add(t);
                    }
            }
        }
        public void LoadStaticMesh()
        {
            for (int i = 0; i < UStat.Count; i++)
            {
                DirectXObject t = new DirectXObject();
                if (UStat[i].LP.GT.loc != new Vector3(0, 0, 0) || UStat[i].LP.GT.rot != new Vector3(0, 0, 0))
                    t.m = Matrix.Scaling(UStat[i].LP.GT.scl) *
                          Matrix.RotationYawPitchRoll(UStat[i].LP.GT.rot.X, UStat[i].LP.GT.rot.Y, UStat[i].LP.GT.rot.Z) *
                          Matrix.Translation(UStat[i].LP.GT.loc);
                else
                    t.m = Matrix.Scaling(UStat[i].LP.GT.scl) * UStat[i].LP.GT.m;
                t.verts = UStat[i].ExportDirectX(Color.White.ToArgb());
                if (t.verts.Length == 0)
                    continue;
                t.boxorg = new Vector3(t.verts[0].X, t.verts[0].Y, t.verts[0].Z); ;
                t.box = new Vector3(t.verts[0].X, t.verts[0].Y, t.verts[0].Z); ;
                for (int j = 0; j < t.verts.Length; j++)
                {
                    t.boxorg.X = Math.Min(t.boxorg.X, t.verts[j].X);
                    t.boxorg.Y = Math.Min(t.boxorg.Y, t.verts[j].Y);
                    t.boxorg.Z = Math.Min(t.boxorg.Z, t.verts[j].Z);
                    t.box.X = Math.Max(t.box.X, t.verts[j].X);
                    t.box.Y = Math.Max(t.box.Y, t.verts[j].Y);
                    t.box.Z = Math.Max(t.box.Z, t.verts[j].Z);
                }
                t.box -= t.boxorg;
                t.box *= 0.5f;
                t.r = (float)Math.Sqrt(t.box.X * t.box.X + t.box.Y * t.box.Y + t.box.Z * t.box.Z);
                t.box *= 2;
                Vector3 pos = t.boxorg + t.box * 0.5f;
                t.tfpos = Vector3.TransformCoordinate(pos, t.m);
                pos = t.tfpos;
                Vector3 boxorg = pos;
                Vector3 box = pos;
                for (int j = 0; j < t.verts.Length; j++)
                {
                    boxorg.X = Math.Min(boxorg.X, Vector3.TransformCoordinate(t.verts[j].Position, t.m).X);
                    boxorg.Y = Math.Min(boxorg.Y, Vector3.TransformCoordinate(t.verts[j].Position, t.m).Y);
                    boxorg.Z = Math.Min(boxorg.Z, Vector3.TransformCoordinate(t.verts[j].Position, t.m).Z);
                    box.X = Math.Max(box.X, Vector3.TransformCoordinate(t.verts[j].Position, t.m).X);
                    box.Y = Math.Max(box.Y, Vector3.TransformCoordinate(t.verts[j].Position, t.m).Y);
                    box.Z = Math.Max(box.Z, Vector3.TransformCoordinate(t.verts[j].Position, t.m).Z);
                }
                box -= boxorg;
                box *= 0.5f;
                t.tfr = (float)Math.Sqrt(box.X * box.X + box.Y * box.Y + box.Z * box.Z);
                t.name = pcc.names[pcc.Export[(int)UStat[i].index].Name];
                t.name = t.name.Substring(0, t.name.Length-1);
                if (t.name.ToLower().Contains("volumetric") ||
                    t.name.ToLower().Contains("spheremesh"))
                    t.visible = false;
                else
                    t.visible = true;
                t.objindex = (int)UStat[i].index;
                UStat[i].DirectX = t;
            }
        }
        public void LoadTextures()
        {
            DialogResult r = MessageBox.Show("Load textures too?","", MessageBoxButtons.YesNo);
            if (r == DialogResult.No)
            {
                for (int i = 0; i < UStat.Count; i++)
                    UStat[i].LP.textureindex = -1;
                return;
            }
            r = MessageBox.Show("Load from tfcs?", "", MessageBoxButtons.YesNo);
            string ptex = "", pctex = "";
            if (r == DialogResult.Yes)
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "Textures.tfc|Textures.tfc";
                if (d.ShowDialog() == DialogResult.OK)
                    ptex = d.FileName;
                d.Filter = "CharTextures.tfc|CharTextures.tfc";
                if (d.ShowDialog() == DialogResult.OK)
                    pctex = d.FileName;
            }
            int count = 50;
            int counttex = 0;
            int countmat = 0;
            for (int i = 0; i < UStat.Count; i++)
                if (UStat[i].Materials == null || UStat[i].Materials.Count() == 0)
                {
                    if (count < 49)
                        count++;
                    else
                    {
                        count = 0;
                        TextOut("\n" + i.ToString() + "\t");
                    }
                    TextOut("x");
                    UStat[i].LP.textureindex = -1;
                }
                else
                {                    
                    if (count < 49)
                        count++;
                    else
                    {
                        count = 0;
                        TextOut("\n" + i.ToString() + "\t");
                    }
                    UStat[i].LP.textureindex = -1;
                    int stype = -1;//0=MIC 1=MAT
                    int index = -1;
                    for (int k = 0; k < UStat[i].Materials.Count; k++)
                    {
                        byte[] buff = UStat[i].Materials[k].raw;                        
                        for (int j = 0; j < 11; j++)
                        {
                            int idx = BitConverter.ToInt32(buff, j * 4);
                            if (idx > 0 && idx < pcc.Header.ExportCount)
                            {
                                string s = pcc.getClassName(pcc.Export[idx].Class);
                                if (s == "MaterialInstanceConstant")
                                {
                                    index = idx;
                                    stype = 0;
                                    break;
                                }
                                if (s == "Material")
                                {
                                    index = idx;
                                    stype = 1;
                                    break;
                                }
                            }
                        }
                    }
                    if (index < 0)
                    {
                        TextOut("?");
                        continue;
                    }
                    #region MIC
                    if (stype == 0)
                    {
                        UnrealObject UOb = new UnrealObject(pcc.EntryToBuff(index), pcc.getClassName(pcc.Export[index].Class), pcc.names);
                        string l = Path.GetDirectoryName(Application.ExecutablePath);
                        UOb.UUkn.UPR = UPR;
                        UOb.UUkn.Deserialize();
                        int entry = UPR.FindProperty("TextureParameterValues", UOb.UUkn.Properties);
                        if (entry != -1)
                        {
                            entry += 4;
                            index = -1;
                            bool stopme = false;
                            while (entry < UOb.UUkn.Properties.Count() && UOb.UUkn.Properties[entry].name == "ParameterValue" && !stopme)
                            {
                                entry++;
                                int n = BitConverter.ToInt32(UOb.UUkn.Properties[entry].raw, UOb.UUkn.Properties[entry].raw.Length - 4) - 1;
                                if (n > 0)
                                {
                                    index = n;
                                    if (pcc.names[pcc.Export[n].Name].ToLower().Contains("diff"))
                                        stopme = true;
                                }
                                entry += 5;
                            }
                            if (index != -1)
                            {
                                bool found = false;
                                for (int j = 0; j < Tex.Count(); j++)
                                    if (index == Tex[j].index)
                                    {
                                        UStat[i].LP.textureindex = j;
                                        TextOut("f");
                                        found = true;
                                        break;
                                    }
                                if (!found)
                                {
                                    UOb = new UnrealObject(pcc.EntryToBuff(index), "Texture2D", pcc.names);
                                    TextureEntry t = new TextureEntry();
                                    int tfc = -1;
                                    for (int j = 0; j < UOb.UTex2D.TextureTFCs.Count; j++)
                                        if (UOb.UTex2D.TextureTFCs[j].size > 0 &&
                                            UOb.UTex2D.TextureTFCs[j].offset > 0 &&
                                            UOb.UTex2D.TextureTFCs[j].size != UOb.UTex2D.TextureTFCs[j].offset)
                                        {
                                            tfc = j;
                                            break;
                                        }
                                    t.tex = UOb.UTex2D.ExportToTexture(device, tfc, ptex, pctex);
                                    t.index = index;
                                    t.name = pcc.names[pcc.Export[t.index].Name];
                                    t.name = t.name.Substring(0, t.name.Length - 1);
                                    Tex.Add(t);
                                    UStat[i].LP.textureindex = Tex.Count - 1;
                                    TextOut("l");
                                    counttex++;
                                }
                            }
                            else TextOut("?");
                        }
                        else TextOut("?");
                    }
                    #endregion
                    if (stype == 1)
                    {
                        UnrealObject UOb = new UnrealObject(pcc.EntryToBuff(index), pcc.getClassName(pcc.Export[index].Class), pcc.names);
                        string l = Path.GetDirectoryName(Application.ExecutablePath);
                        UOb.UMat.UPR = UPR;
                        UOb.UMat.Deserialize();
                        int entry = UPR.FindProperty("Expressions", UOb.UMat.props);
                        if (entry == 0)
                        {
                            entry ++;
                            int len = UOb.UMat.props[1].raw.Length;
                            int cnt = (len - 20) / 4;
                            index =-1;
                            for(int j=0;j<cnt;j++)
                            {
                                index = BitConverter.ToInt32(UOb.UMat.props[1].raw, 20 + j * 4)-1;
                                if (pcc.getClassName(pcc.Export[index].Class) == "MaterialExpressionTextureSampleParamter2D")
                                    break;
                            }
                            if (index == -1)
                            {
                                TextOut("?");
                                continue;
                            }
                            UnrealObject UUOb2 = new UnrealObject(pcc.EntryToBuff(index), "MaterialExpressionTextureSampleParameter2D",pcc.names);
                            UUOb2.UUkn.UPR = UPR;
                            UUOb2.UUkn.Deserialize();
                            index = -1;
                            for (int j = 0; j < UUOb2.UUkn.Properties.Count; j++)
                                if (UUOb2.UUkn.Properties[j].name == "Texture")
                                    index = PropToInt(UUOb2.UUkn.Properties[j].raw);
                            if (index != -1 && !(index<0))
                            {
                                index--;
                                bool found = false;
                                for (int j = 0; j < Tex.Count(); j++)
                                    if (index == Tex[j].index)
                                    {
                                        UStat[i].LP.textureindex = j;
                                        TextOut("F");
                                        found = true;
                                        break;
                                    }
                                if (!found)
                                {
                                    UOb = new UnrealObject(pcc.EntryToBuff(index), "Texture2D", pcc.names);
                                    TextureEntry t = new TextureEntry();
                                    int tfc = -1;
                                    for (int j = 0; j < UOb.UTex2D.TextureTFCs.Count; j++)
                                        if (UOb.UTex2D.TextureTFCs[j].size > 0 &&
                                            UOb.UTex2D.TextureTFCs[j].offset > 0 &&
                                            UOb.UTex2D.TextureTFCs[j].size != UOb.UTex2D.TextureTFCs[j].offset)
                                        {
                                            tfc = j;
                                            break;
                                        }
                                    t.tex = UOb.UTex2D.ExportToTexture(device, tfc, ptex, pctex);
                                    t.index = index;
                                    t.name = pcc.names[pcc.Export[t.index].Name];
                                    t.name = t.name.Substring(0, t.name.Length - 1);
                                    Tex.Add(t);
                                    UStat[i].LP.textureindex = Tex.Count - 1;
                                    TextOut("M");
                                    countmat++;
                                }
                            }
                            else TextOut("?");
                        }
                        else TextOut("?");
                    }
                }
            TextOut("\nLoaded Textures: " + counttex.ToString());
            TextOut("\nLoaded Materials: " + countmat.ToString());
        }

#endregion

        #region Helpers

        public Vector3 MatrixToVec3(Matrix m)
        {
            return new Vector3(m.M41, m.M42, m.M43);
        }

        public int PropToInt(byte[] buff)
        {
            if (buff == null || buff.Length < 4)
                return -1;
            return BitConverter.ToInt32(buff, buff.Length - 4);
        }

        public Vector3 PropToVector3(byte[] buff)
        {
            Vector3 v = new Vector3(0, 0, 0);
            if (buff == null || buff.Length < 12)
                return v;
            v.X = BitConverter.ToSingle(buff, buff.Length - 12);
            v.Y = BitConverter.ToSingle(buff, buff.Length - 8);
            v.Z = BitConverter.ToSingle(buff, buff.Length - 4);
            return v;
        }
        public Vector3 PropToIntVector3(byte[] buff)
        {
            Vector3 v = new Vector3(0, 0, 0);
            if (buff == null || buff.Length < 12)
                return v;
            v.Y = BitConverter.ToInt32(buff, buff.Length - 12);
            v.X = BitConverter.ToInt32(buff, buff.Length - 8);
            v.Z = BitConverter.ToInt32(buff, buff.Length - 4);
            v *= (3.1415f / 65536f);
            return v;
        }

        public Vector3 PropToCYPRVector3(byte[] buff)//Compressed YawPitchRoll
        {
            Vector3 v = new Vector3(0, 0, 0);
            if (buff == null || buff.Length < 12)
                return v;
            UMath mat = new UMath();
            UMath.Rotator r = mat.PropToRotator(buff);
            v = mat.RotatorToVector(r);
            return v;
        }

        

        public TreeNode ExportToTree()
        {
            TreeNode t = new TreeNode("Level");            
            t.Nodes.Add(PropsToTree());
            t.Nodes.Add(ObjectsToTree());
            return t;
        }

        public TreeNode PropsToTree()
        {
            TreeNode ret = new TreeNode("Properties");
            for (int i = 0; i < props.Count; i++)
            {
                TreeNode t = new TreeNode(props[i].name);
                TreeNode t2 = new TreeNode(props[i].value);
                t.Nodes.Add(t2);
                ret.Nodes.Add(t);
            }
            return ret;
        }

        public TreeNode ObjectsToTree()
        {
            TreeNode t = new TreeNode("Level Objects");
            for (int i = 0; i < LObjects.Count; i++)
            {
                TreeNode t2 = new TreeNode(LObjects[i].name);
                TreeNode t3 = new TreeNode("Entry: " + LObjects[i].entry.ToString());
                TreeNode t4 = new TreeNode("Class: " + LObjects[i].cls);
                t2.Nodes.Add(t3);
                t2.Nodes.Add(t4);
                t.Nodes.Add(t2);
            }
            return t;
        }

#endregion
    }
}
