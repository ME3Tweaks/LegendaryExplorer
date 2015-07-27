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
using KFreonLib.Debugging;

namespace ME3Explorer.Meshplorer
{
    public partial class UDKCopy : Form
    {
        public UDKExplorer.UDK.UDKObject udk;
        public List<int> Objects;

        public UDKCopy()
        {
            InitializeComponent();
        }

        private void openUDKPackageToolStripMenuItem_Click(object sender, EventArgs e)
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

        private void importLODToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int m = listBox2.SelectedIndex;
            if (m == -1)
                return;
            SkeletalMesh skm = new SkeletalMesh(MPOpt.pcc, MPOpt.SelectedObject);
            SkeletalMesh.LODModelStruct lodpcc = skm.LODModels[MPOpt.SelectedLOD];
            UDKExplorer.UDK.Classes.SkeletalMesh skmudk = new UDKExplorer.UDK.Classes.SkeletalMesh(udk, Objects[n]);
            if (skm.Bones.Count != skmudk.Bones.Count)
            {
                MessageBox.Show("Your imported mesh has a different count of Bones! This would crash your game, stopping now.");
                return;
            }
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
                    string pccb = MPOpt.pcc.getNameEntry(skm.Bones[j].Name);
                    if (pccb == udkb)
                    {
                        found = true;
                        BoneMap.Add(j);
                        if (MPOpt.SKM_importbones)
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
            skm.LODModels[MPOpt.SelectedLOD] = lodpcc;
            SerializingContainer con = new SerializingContainer();
            con.Memory = new MemoryStream();
            con.isLoading = false;
            skm.Serialize(con);
            int end = skm.GetPropertyEnd();
            MemoryStream mem = new MemoryStream();
            mem.Write(MPOpt.pcc.Exports[MPOpt.SelectedObject].Data, 0, end);
            mem.Write(con.Memory.ToArray(), 0, (int)con.Memory.Length);
            MPOpt.pcc.Exports[MPOpt.SelectedObject].Data = mem.ToArray();
            MPOpt.pcc.altSaveToFile(MPOpt.pcc.pccFileName, true);
            //SaveFileDialog d = new SaveFileDialog();
            //d.Filter = "*.bin|*.bin";
            //if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            //{
            //    FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
            //    fs.Write(mem.ToArray(), 0, (int)mem.Length);
            //    fs.Close();
                MessageBox.Show("Done");
            //}
        }
    }
}
