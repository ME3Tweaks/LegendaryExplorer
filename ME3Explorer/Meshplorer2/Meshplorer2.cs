using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using AmaroK86.MassEffect3;
using KFreonLib.Debugging;
using KFreonLib.MEDirectories;

namespace ME3Explorer.Meshplorer2
{
    public partial class Meshplorer2 : Form
    {
        public struct EntryStruct
        {
            public string Filename;
            public string DLCName;
            public string ObjectPath;
            public int Index;
            public bool isDLC;
            public bool isSkeletal;
        }

        public List<EntryStruct> Entries;
        public int DisplayStyle = 0; //0 = per file, 1 = per path
        public UDKExplorer.UDK.UDKObject udk;
        public List<int> Objects;

        public Meshplorer2()
        {
            InitializeComponent();
        }

        private void scanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DebugOutput.StartDebugger("Meshplorer2");
            int count = 0;
            timer1.Enabled = false;
            Entries = new List<EntryStruct>();
            bool ScanDLC = false;
            if (MessageBox.Show("Scan DLCs too?", "Meshplorer 2", MessageBoxButtons.YesNo) == DialogResult.Yes)
                ScanDLC = true;
            if (ScanDLC)
            {
                #region DLC Stuff
                string dirDLC = ME3Directory.DLCPath;
                if (!Directory.Exists(dirDLC))
                    DebugOutput.PrintLn("No DLC Folder found!");
                else
                {
                    string[] subdirs = Directory.GetDirectories(dirDLC);
                    string loc = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\";
                    Directory.CreateDirectory(loc + "temp");
                    foreach (string DLCpath in subdirs)
                        if (!DLCpath.StartsWith("__") && !DLCpath.StartsWith("DLC_UPD"))
                        {
                            string path = DLCpath + "\\CookedPCConsole\\Default.sfar";
                            DLCBase dlcbase;
                            try
                            {
                                dlcbase = new DLCBase(path);
                                count = 0;
                                pbar1.Maximum = dlcbase.fileList.Count;
                                foreach (sfarFile file in dlcbase.fileList)
                                    try
                                    {
                                        string filename = Path.GetFileName(file.fileName);
                                        if (Path.GetExtension(filename).ToLower().EndsWith(".pcc"))
                                        {
                                            using (Stream input = File.OpenRead(path), output = File.Create(loc + "temp\\" + filename))
                                            {
                                                AmaroK86.MassEffect3.DLCUnpack.DecompressEntry(file, input, output, dlcbase.CompressionScheme);
                                            }
                                            FileInfo f = new FileInfo(loc + "temp\\" + filename);
                                            DebugOutput.PrintLn("checking DLC: " + Path.GetFileName(DLCpath) + " File: " + filename + " Size: " + f.Length + " bytes", count % 3 == 0);
                                            PCCObject pcc = new PCCObject(loc + "temp\\" + filename);
                                            for (int i = 0; i < pcc.Exports.Count; i++)
                                                if (pcc.Exports[i].ClassName == "SkeletalMesh" ||
                                                    pcc.Exports[i].ClassName == "StaticMesh")
                                                {
                                                    EntryStruct ent = new EntryStruct();
                                                    ent.DLCName = Path.GetFileName(DLCpath);
                                                    ent.Filename = filename;
                                                    ent.Index = i;
                                                    ent.isDLC = true;
                                                    ent.ObjectPath = pcc.Exports[i].GetFullPath;
                                                    ent.isSkeletal = (pcc.Exports[i].ClassName == "SkeletalMesh");
                                                    Entries.Add(ent);
                                                }
                                            File.Delete(loc + "temp\\" + filename);
                                        }
                                        if (count % 3 == 0)
                                        {
                                            pbar1.Value = count;
                                            Application.DoEvents();
                                        }
                                        count++;
                                    }
                                    catch (Exception ex)
                                    {
                                        DebugOutput.PrintLn("=====ERROR=====\n" + ex.ToString() + "\n=====ERROR=====");
                                    }
                            }
                            catch (Exception ex)
                            {
                                DebugOutput.PrintLn("=====ERROR=====\n" + ex.ToString() + "\n=====ERROR=====");
                            }
                        }
                    Directory.Delete(loc + "temp", true);
                }
                #endregion
            }
            #region Basegame Stuff
            string dir = ME3Directory.cookedPath;
            string[] files = Directory.GetFiles(dir,"*.pcc");            
            pbar1.Maximum = files.Length - 1;
            foreach (string file in files)
            {
                DebugOutput.PrintLn("Scan file #" + count + " : " + file, count % 10 == 0);
                try
                {
                    PCCObject pcc = new PCCObject(file);
                    for (int i = 0; i < pcc.Exports.Count; i++)
                        if (pcc.Exports[i].ClassName == "SkeletalMesh" ||
                            pcc.Exports[i].ClassName == "StaticMesh")
                        {
                            EntryStruct ent = new EntryStruct();
                            ent.DLCName = "";
                            ent.Filename = Path.GetFileName(file);
                            ent.Index = i;
                            ent.isDLC = false;
                            ent.ObjectPath = pcc.Exports[i].GetFullPath;
                            ent.isSkeletal = (pcc.Exports[i].ClassName == "SkeletalMesh");
                            Entries.Add(ent);
                        }
                    if (count % 10 == 0)
                    {
                        Application.DoEvents();
                        pbar1.Value = count;
                    }
                    count++;
                }
                catch (Exception ex)
                {
                    DebugOutput.PrintLn("=====ERROR=====\n" + ex.ToString() + "\n=====ERROR=====");
                }
            }
            #endregion
            #region Sorting
            bool run = true;
            DebugOutput.PrintLn("=====Info=====\n\nSorting names...\n\n=====Info=====");
            count = 0;
            while (run)
            {
                run = false;
                for(int i=0;i<Entries.Count -1;i++)
                    if (Entries[i].Filename.CompareTo(Entries[i + 1].Filename) > 0)
                    {
                        EntryStruct tmp = Entries[i];
                        Entries[i] = Entries[i + 1];
                        Entries[i + 1] = tmp;
                        run = true;
                        if (count++ % 100 == 0)
                            Application.DoEvents();
                    }
            }
            #endregion
            TreeRefresh();
            timer1.Enabled = true;
        }

        public void TreeRefresh()
        {
            treeView1.Nodes.Clear();
            treeView2.Nodes.Clear();
            treeView1.Visible = false;
            Application.DoEvents();
            int count = 0;
            pbar1.Maximum = Entries.Count - 1;
            if (DisplayStyle == 0)
            {
                foreach (EntryStruct e in Entries)
                {
                    if (!e.isDLC)
                    {
                        int f = -1;
                        for (int i = 0; i < treeView1.Nodes.Count; i++)
                            if (treeView1.Nodes[i].Text == e.Filename)
                                f = i;
                        string pre = "SKM";
                        if (!e.isSkeletal)
                            pre = "STM";
                        if (f == -1)
                        {
                            TreeNode t = new TreeNode(e.Filename);
                            t.Nodes.Add(count.ToString(), pre + "#" + e.Index + " : " + e.ObjectPath);
                            treeView1.Nodes.Add(t);
                        }
                        else
                        {
                            treeView1.Nodes[f].Nodes.Add(count.ToString(), pre + "#" + e.Index + " : " + e.ObjectPath);
                        }
                        if (count % 100 == 0)
                        {
                            pbar1.Value = count;
                            Application.DoEvents();
                        }
                        count++;
                    }
                    else
                    {
                        int f = -1;
                        for (int i = 0; i < treeView1.Nodes.Count; i++)
                            if (treeView1.Nodes[i].Text == e.DLCName + "::" + e.Filename)
                                f = i;
                        string pre = "SKM";
                        if (!e.isSkeletal)
                            pre = "STM";
                        if (f == -1)
                        {
                            TreeNode t = new TreeNode(e.DLCName + "::" + e.Filename);
                            t.Nodes.Add(count.ToString(), pre + "#" + e.Index + " : " + e.ObjectPath);
                            treeView1.Nodes.Add(t);
                        }
                        else
                        {
                            treeView1.Nodes[f].Nodes.Add(count.ToString(), pre + "#" + e.Index + " : " + e.ObjectPath);
                        }
                        if (count % 100 == 0)
                        {
                            pbar1.Value = count;
                            Application.DoEvents();
                        }
                        count++;
                    }
                }
            }
            treeView1.Visible = true;
            pbar1.Value = 0;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            Renderer.Refresh();
        }

        private void Meshplorer2_Paint(object sender, PaintEventArgs e)
        {
            if (!Renderer.init || Renderer.device == null)
            {
                Renderer.InitializeGraphics(pb1);
                timer1.Enabled = true;
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.db|*.db";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                BitConverter.IsLittleEndian = true;
                fs.Write(BitConverter.GetBytes((int)Entries.Count), 0, 4);
                foreach(EntryStruct es in Entries)
                {
                    WriteString(fs, es.Filename);
                    WriteString(fs, es.DLCName);
                    WriteString(fs, es.ObjectPath);
                    fs.Write(BitConverter.GetBytes((int)es.Index), 0, 4);
                    if (es.isDLC)
                        fs.WriteByte(1);
                    else
                        fs.WriteByte(0);
                    if (es.isSkeletal)
                        fs.WriteByte(1);
                    else
                        fs.WriteByte(0);
                }
                fs.Close();
                MessageBox.Show("Done.");
            }
        }

        public void WriteString(FileStream fs, string s)
        {
            fs.Write(BitConverter.GetBytes((int)s.Length), 0, 4);
            fs.Write(GetBytes(s), 0, s.Length);
        }

        public string ReadString(FileStream fs)
        {
            string s = "";
            byte[] buff = new byte[4];
            for (int i = 0; i < 4; i++)
                buff[i] = (byte)fs.ReadByte();
            int count = BitConverter.ToInt32(buff, 0);
            buff = new byte[count];
            for (int i = 0; i < count; i++)
                buff[i] = (byte)fs.ReadByte();
            s = GetString(buff);
            return s;
        }

        public byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length];
            for (int i = 0; i < str.Length; i++)
                bytes[i] = (byte)str[i];
            return bytes;
        }

        public string GetString(byte[] bytes)
        {
            string s = "";
            for (int i = 0; i < bytes.Length; i++)
                s += (char)bytes[i];
            return s;
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (DisplayStyle == 0)
            {
                if (t.Parent == null || t.Name == "")
                    return;
                Renderer.STM = null;
                Renderer.SKM = null;
                Renderer.CamDistance = 10;
                Renderer.CamOffset = new Microsoft.DirectX.Vector3(0, 0, 0);
                try
                {
                    int i = 0;
                    if (Int32.TryParse(t.Name, out i))
                    {
                        EntryStruct en = Entries[i];
                        if (!en.isDLC)
                        {
                            PCCObject pcc = new PCCObject(ME3Directory.cookedPath + en.Filename);
                            if (en.isSkeletal)
                            {
                                Renderer.SKM = new SkeletalMesh(pcc, en.Index);
                                Renderer.CamDistance = Renderer.SKM.Bounding.r * 2.0f;
                                Renderer.CamOffset = Renderer.SKM.Bounding.origin;
                                treeView2.Nodes.Clear();
                                if (previewWithTreeToolStripMenuItem.Checked)
                                {
                                    treeView2.Visible = false;
                                    Application.DoEvents();
                                    treeView2.Nodes.Add(Renderer.SKM.ToTree());
                                    treeView2.Visible = true;
                                }
                            }
                            else
                            {

                            }
                        }
                        else
                        {
                            string loc = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\";
                            string dirDLC = ME3Directory.DLCPath;
                            dirDLC += en.DLCName;
                            dirDLC += "\\CookedPCConsole\\Default.sfar";
                            DLCBase dlc = new DLCBase(dirDLC);
                            foreach (sfarFile file in dlc.fileList)
                                try
                                {
                                    string filename = Path.GetFileName(file.fileName);
                                    if (Path.GetExtension(filename).ToLower().EndsWith(".pcc") && filename == en.Filename)
                                    {
                                        using (Stream input = File.OpenRead(dirDLC), output = File.Create(loc + filename))
                                        {
                                            AmaroK86.MassEffect3.DLCUnpack.DecompressEntry(file, input, output, dlc.CompressionScheme);
                                        }
                                        if (File.Exists(loc + filename))
                                        {
                                            try
                                            {
                                                PCCObject pcc = new PCCObject(loc + filename);
                                                if (en.isSkeletal)
                                                {
                                                    Renderer.SKM = new SkeletalMesh(pcc, en.Index);
                                                    Renderer.CamDistance = Renderer.SKM.Bounding.r * 2.0f;
                                                    Renderer.CamOffset = Renderer.SKM.Bounding.origin;
                                                    treeView2.Nodes.Clear();
                                                    if (previewWithTreeToolStripMenuItem.Checked)
                                                    {
                                                        treeView2.Visible = false;
                                                        Application.DoEvents();
                                                        treeView2.Nodes.Add(Renderer.SKM.ToTree());
                                                        treeView2.Visible = true;
                                                    }
                                                }
                                                else
                                                {

                                                }
                                            }
                                            catch (Exception)
                                            {
                                            }
                                            File.Delete(loc + filename);
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.db|*.db";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                timer1.Enabled = false;
                Entries = new List<EntryStruct>();
                FileStream fs = new FileStream(d.FileName, FileMode.Open, FileAccess.Read);
                int count = ReadInt(fs);                
                for (int i = 0; i < count; i++)
                {
                    EntryStruct en = new EntryStruct();
                    en.Filename = ReadString(fs);
                    en.DLCName = ReadString(fs);
                    en.ObjectPath = ReadString(fs);
                    en.Index = ReadInt(fs);
                    byte b = (byte)fs.ReadByte();
                    en.isDLC = b == 1;
                    b = (byte)fs.ReadByte();
                    en.isSkeletal = b == 1;
                    Entries.Add(en);
                }
                fs.Close();
                TreeRefresh();
                timer1.Enabled = true;
            }
        }

        public int ReadInt(FileStream fs)
        {
            byte[] buff = new byte[4];
            fs.Read(buff, 0, 4);
            return BitConverter.ToInt32(buff, 0);
        }

        private void Meshplorer2_Load(object sender, EventArgs e)
        {
            this.pb1.MouseWheel += new MouseEventHandler(MouseWheelHandler);
        }

        private void MouseWheelHandler(object sender, MouseEventArgs e)
        {
                if (e.Delta > 0)
                    Renderer.CamDistance /= 1.5f;
                else
                    Renderer.CamDistance *= 1.5f;
        }

        private void pb1_MouseHover(object sender, EventArgs e)
        {
            pb1.Focus();
        }

        private void rotateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Renderer.rotate = rotateToolStripMenuItem.Checked;
        }

        private void importFromUDKToolStripMenuItem_Click(object sender, EventArgs e)
        {
            globalTreeToolStripMenuItem.Visible =
            optionsToolStripMenuItem.Visible =
            transferToolStripMenuItem.Visible =
            splitContainer1.Visible = false;
            fileToolStripMenuItem.Visible =
            importLODToolStripMenuItem.Visible =
            splitContainer3.Visible = true;
        }

        private void importLODToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int m = listBox2.SelectedIndex;
            if (m == -1)
                return;
            TreeNode t1 = treeView1.SelectedNode;
            if (t1 == null || t1.Parent == null || t1.Name == "")
                return;
            PCCObject pcc = new PCCObject();
            SkeletalMesh skm = new SkeletalMesh();
            EntryStruct en;
            string loc = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\";
            if (DisplayStyle == 0)
            {
                int o = 0;
                if (!Int32.TryParse(t1.Name, out o))
                    return;
                en = Entries[o];                
                if (!en.isDLC)
                {
                    pcc = new PCCObject(ME3Directory.cookedPath + en.Filename);
                    if (en.isSkeletal)
                    {
                        skm = new SkeletalMesh(pcc, en.Index);
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {                    
                    string dirDLC = ME3Directory.DLCPath;
                    dirDLC += en.DLCName;
                    dirDLC += "\\CookedPCConsole\\Default.sfar";
                    DLCBase dlc = new DLCBase(dirDLC);
                    foreach (sfarFile file in dlc.fileList)
                        try
                        {
                            string filename = Path.GetFileName(file.fileName);
                            if (Path.GetExtension(filename).ToLower().EndsWith(".pcc") && filename == en.Filename)
                            {
                                if (File.Exists(loc + "dlc.pcc"))
                                    File.Delete(loc + "dlc.pcc");
                                using (Stream input = File.OpenRead(dirDLC), output = File.Create(loc + "dlc.pcc"))
                                {
                                    AmaroK86.MassEffect3.DLCUnpack.DecompressEntry(file, input, output, dlc.CompressionScheme);
                                }
                                if (File.Exists(loc + "dlc.pcc"))
                                {
                                    try
                                    {
                                        pcc = new PCCObject(loc + "dlc.pcc");
                                        if (en.isSkeletal)
                                        {
                                            skm = new SkeletalMesh(pcc, en.Index);
                                            break;
                                        }
                                        else
                                        {
                                            return;
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        return;
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            return;
                        }
                }
            }
            else
                return;
            if (!skm.Loaded || !pcc.Loaded)
                return;
            SkeletalMesh.LODModelStruct lodpcc = skm.LODModels[0];
            UDKExplorer.UDK.Classes.SkeletalMesh skmudk = new UDKExplorer.UDK.Classes.SkeletalMesh(udk, Objects[n]);
            UDKExplorer.UDK.Classes.SkeletalMesh.LODModelStruct lodudk = skmudk.LODModels[m];
            lodpcc.Sections = new List<SkeletalMesh.SectionStruct>();
            foreach (UDKExplorer.UDK.Classes.SkeletalMesh.SectionStruct secudk in lodudk.Sections)
            {
                SkeletalMesh.SectionStruct secpcc = new SkeletalMesh.SectionStruct();
                secpcc.BaseIndex = secudk.BaseIndex;
                secpcc.ChunkIndex = secudk.ChunkIndex;
                secpcc.MaterialIndex = secudk.MaterialIndex;
                secpcc.NumTriangles = secudk.NumTriangles;
                lodpcc.Sections.Add(secpcc);
            }
            lodpcc.IndexBuffer = new SkeletalMesh.MultiSizeIndexContainerStruct();
            lodpcc.IndexBuffer.IndexCount = lodudk.IndexBuffer.IndexCount;
            lodpcc.IndexBuffer.IndexSize = lodudk.IndexBuffer.IndexSize;
            lodpcc.IndexBuffer.Indexes = new List<ushort>();
            foreach (ushort Idx in lodudk.IndexBuffer.Indexes)
                lodpcc.IndexBuffer.Indexes.Add(Idx);
            List<int> BoneMap = new List<int>();
            for (int i = 0; i < skmudk.Bones.Count; i++)
            {
                string udkb = udk.GetName(skmudk.Bones[i].Name);
                bool found = false;
                for (int j = 0; j < skm.Bones.Count; j++)
                {
                    string pccb = pcc.getNameEntry(skm.Bones[j].Name);
                    if (pccb == udkb)
                    {
                        found = true;
                        BoneMap.Add(j);
                        if (importBonesToolStripMenuItem.Checked)
                        {
                            SkeletalMesh.BoneStruct bpcc = skm.Bones[j];
                            UDKExplorer.UDK.Classes.SkeletalMesh.BoneStruct budk = skmudk.Bones[i];
                            bpcc.Orientation = budk.Orientation;
                            bpcc.Position = budk.Position;
                            skm.Bones[j] = bpcc;
                        }
                    }
                }
                if (!found)
                {
                    DebugOutput.PrintLn("ERROR: Cant Match Bone \"" + udkb + "\"");
                    BoneMap.Add(0);
                }
            }

            lodpcc.ActiveBones = new List<ushort>();
            foreach (ushort Idx in lodudk.ActiveBones)
                lodpcc.ActiveBones.Add((ushort)BoneMap[Idx]);
            lodpcc.Chunks = new List<SkeletalMesh.SkelMeshChunkStruct>();
            foreach (UDKExplorer.UDK.Classes.SkeletalMesh.SkelMeshChunkStruct chunkudk in lodudk.Chunks)
            {
                SkeletalMesh.SkelMeshChunkStruct chunkpcc = new SkeletalMesh.SkelMeshChunkStruct();
                chunkpcc.BaseVertexIndex = chunkudk.BaseVertexIndex;
                chunkpcc.MaxBoneInfluences = chunkudk.MaxBoneInfluences;
                chunkpcc.NumRigidVertices = chunkudk.NumRigidVertices;
                chunkpcc.NumSoftVertices = chunkudk.NumSoftVertices;
                chunkpcc.BoneMap = new List<ushort>();
                chunkpcc.RiginSkinVertices = new List<SkeletalMesh.RigidSkinVertexStruct>();
                chunkpcc.SoftSkinVertices = new List<SkeletalMesh.SoftSkinVertexStruct>();
                foreach (ushort Idx in chunkudk.BoneMap)
                    chunkpcc.BoneMap.Add((ushort)BoneMap[Idx]);
                lodpcc.Chunks.Add(chunkpcc);
            }
            lodpcc.Size = lodudk.Size;
            lodpcc.NumVertices = lodudk.NumVertices;
            lodpcc.RequiredBones = new List<byte>();
            foreach (byte b in lodudk.RequiredBones)
                lodpcc.RequiredBones.Add(b);
            lodpcc.VertexBufferGPUSkin = new SkeletalMesh.VertexBufferGPUSkinStruct();
            lodpcc.VertexBufferGPUSkin.NumTexCoords = lodudk.VertexBufferGPUSkin.NumTexCoords;
            lodpcc.VertexBufferGPUSkin.Extension = lodudk.VertexBufferGPUSkin.Extension;
            lodpcc.VertexBufferGPUSkin.Origin = lodudk.VertexBufferGPUSkin.Origin;
            lodpcc.VertexBufferGPUSkin.VertexSize = lodudk.VertexBufferGPUSkin.VertexSize;
            lodpcc.VertexBufferGPUSkin.Vertices = new List<SkeletalMesh.GPUSkinVertexStruct>();
            foreach (UDKExplorer.UDK.Classes.SkeletalMesh.GPUSkinVertexStruct vudk in lodudk.VertexBufferGPUSkin.Vertices)
            {
                SkeletalMesh.GPUSkinVertexStruct vpcc = new SkeletalMesh.GPUSkinVertexStruct();
                vpcc.TangentX = vudk.TangentX;
                vpcc.TangentZ = vudk.TangentZ;
                vpcc.Position = vudk.Position;
                vpcc.InfluenceBones = vudk.InfluenceBones;
                vpcc.InfluenceWeights = vudk.InfluenceWeights;
                vpcc.U = vudk.U;
                vpcc.V = vudk.V;
                lodpcc.VertexBufferGPUSkin.Vertices.Add(vpcc);
            }
            for (int i = 0; i < skm.LODModels.Count; i++)
                skm.LODModels[i] = lodpcc;
            SerializingContainer con = new SerializingContainer();
            con.Memory = new MemoryStream();
            con.isLoading = false;
            skm.Serialize(con);
            int end = skm.GetPropertyEnd();
            MemoryStream mem = new MemoryStream();
            mem.Write(pcc.Exports[en.Index].Data, 0, end);
            mem.Write(con.Memory.ToArray(), 0, (int)con.Memory.Length);
            pcc.Exports[en.Index].Data = mem.ToArray();
            pcc.altSaveToFile(pcc.pccFileName, true);
            if (!en.isDLC)
                MessageBox.Show("Done");
            else
                MessageBox.Show("Done. The file is now in following folder, please replace it back to DLC :\n" + loc + "dlc.pcc");
            globalTreeToolStripMenuItem.Visible =
            optionsToolStripMenuItem.Visible =
            transferToolStripMenuItem.Visible =
            splitContainer1.Visible = true;
            fileToolStripMenuItem.Visible =
            importLODToolStripMenuItem.Visible =
            splitContainer3.Visible = false;
        }

        private void openUPKToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.u;*.upk;*.udk|*.u;*.upk;*.udk";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                udk = new UDKExplorer.UDK.UDKObject(d.FileName);
                Objects = new List<int>();
                for (int i = 0; i < udk.ExportCount; i++)
                    if (udk.GetClass(udk.Exports[i].clas) == "SkeletalMesh")
                        Objects.Add(i);
                RefreshLists();
            }
        }

        public void RefreshLists()
        {
            listBox1.Items.Clear();
            foreach (int Idx in Objects)
                listBox1.Items.Add(Idx + " : " + udk.GetName(udk.Exports[Idx].name));
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            UDKExplorer.UDK.Classes.SkeletalMesh skmudk = new UDKExplorer.UDK.Classes.SkeletalMesh(udk, Objects[n]);
            listBox2.Items.Clear();
            for (int i = 0; i < skmudk.LODModels.Count; i++)
                listBox2.Items.Add("LOD " + i);
        }

        private void importBonesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            importBonesToolStripMenuItem.Checked = !importBonesToolStripMenuItem.Checked;
        }

        public void FindNext()
        {
            string s = toolStripTextBox1.Text.ToLower();
            if (s == "")
                return;
            int startp = 0, startn = 0;
            if (treeView1.SelectedNode != null)
            {
                TreeNode t = treeView1.SelectedNode;
                if (t.Parent != null)
                {
                    startp = t.Parent.Index;
                    startn = t.Index + 1;
                }
            }
            for (int i = startp; i < treeView1.Nodes.Count; i++)
            {
                TreeNode p = treeView1.Nodes[i];
                for (int j = startn; j < p.Nodes.Count; j++)
                {
                    if (p.Nodes[j].Text.ToLower().Contains(s))
                    {
                        treeView1.SelectedNode = p.Nodes[j];
                        return;
                    }
                }
                startn = 0;
            }
        }

        private void toolStripButton1_Click_1(object sender, EventArgs e)
        {
            FindNext();
        }

        private void toolStripTextBox1_KeyPress_1(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
                FindNext();
        }
    }
}
