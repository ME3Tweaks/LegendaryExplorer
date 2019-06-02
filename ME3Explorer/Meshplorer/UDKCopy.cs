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
using ME3Explorer.Packages;

namespace ME3Explorer.Meshplorer
{
    public partial class UDKCopy : Form
    {
        public UDKPackage udk;
        public List<int> Objects;
        ME3Package pcc;
        int SelectedObject;
        int SelectedLOD;

        public UDKCopy(ME3Package _pcc, int obj, int lod)
        {
            pcc = _pcc;
            SelectedObject = obj;
            SelectedLOD = lod;
            InitializeComponent();
        }

        private void openUDKPackageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
             d.Filter = "*.u;*.upk;*.udk|*.u;*.upk;*.udk";
             if (d.ShowDialog() == DialogResult.OK)
             {
                 udk = new UDKPackage(d.FileName);
                 Objects = new List<int>();
                 for (int i = 0; i < udk.ExportCount; i++)
                     if (udk.Exports[i].ClassName == "SkeletalMesh")
                         Objects.Add(i);
                 RefreshLists();
             }
        }

        public void RefreshLists()
        {
            MeshListBox.Items.Clear();
            foreach (int Idx in Objects)
                MeshListBox.Items.Add(Idx + " : " + udk.Exports[Idx].ObjectName);
        }

        private void MeshListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = MeshListBox.SelectedIndex;
            if (n == -1)
                return;
            UDKExplorer.UDK.Classes.SkeletalMesh skmudk = new UDKExplorer.UDK.Classes.SkeletalMesh(udk, Objects[n]);
            LODListBox.Items.Clear();
            for (int i = 0; i < skmudk.LODModels.Count; i++)
                LODListBox.Items.Add("LOD " + i);
        }

        private void importLODToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = MeshListBox.SelectedIndex;
            if (n == -1)
                return;
            int m = LODListBox.SelectedIndex;
            if (m == -1)
                return;
            SkeletalMesh skm = new SkeletalMesh(pcc, SelectedObject);
            SkeletalMesh.LODModelStruct lodpcc = skm.LODModels[SelectedLOD];
            UDKExplorer.UDK.Classes.SkeletalMesh skmudk = new UDKExplorer.UDK.Classes.SkeletalMesh(udk, Objects[n]);
            if (skm.Bones.Count != skmudk.Bones.Count)
            {
                if (MessageBox.Show("Your imported mesh has a different count of Bones! This would crash your game, proceed?", "UDK Skeleton Import", MessageBoxButtons.YesNo) == DialogResult.No)
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
                string udkb = udk.getNameEntry(skmudk.Bones[i].Name);
                bool found = false;
                for (int j = 0; j < skm.Bones.Count; j++)
                {
                    string pccb = pcc.getNameEntry(skm.Bones[j].Name);
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
            skm.LODModels[SelectedLOD] = lodpcc;
            SerializingContainer con = new SerializingContainer();
            con.Memory = new MemoryStream();
            con.isLoading = false;
            skm.Serialize(con);
            int end = skm.GetPropertyEnd();
            MemoryStream mem = new MemoryStream();
            mem.Write(pcc.Exports[SelectedObject].Data, 0, end);
            mem.Write(con.Memory.ToArray(), 0, (int)con.Memory.Length);
            pcc.Exports[SelectedObject].Data = mem.ToArray();
            MessageBox.Show("Done");
            Close();
        }

        private void ImportMeshToolStripMenuItem_Click(object sender, System.EventArgs e)
        {
            if (MeshListBox.SelectedIndex == -1)
                return;

            SkeletalMesh newMesh = new SkeletalMesh();
            SkeletalMesh oldMesh = new SkeletalMesh(pcc, SelectedObject);
            UDKExplorer.UDK.Classes.SkeletalMesh sourceMesh = new UDKExplorer.UDK.Classes.SkeletalMesh(udk, Objects[MeshListBox.SelectedIndex]);

            // Fill out data in newMesh
            newMesh.MyIndex = SelectedObject;
            newMesh.Owner = pcc;
            newMesh.Flags = (int)((ulong)pcc.Exports[SelectedObject].ObjectFlags >> 32); // Keep the flags from the overwritten mesh
            
            // Bounding
            newMesh.Bounding = new SkeletalMesh.BoundingStruct(sourceMesh.Bounding);
            
            // Materials
            newMesh.Materials = new List<int>();
            foreach (int materialObjectIndex in sourceMesh.Materials)
            {
                newMesh.Materials.Add(0); // Don't bother copying IDs that will be invalid anyways.
            }
            // The materials are all entered as zero, nothing we can do now.
            /*for (int i = 0; i < newMesh.Materials.Count; i++)
            {
                newMesh.MatInsts.Add(new MaterialInstanceConstant(pcc, newMesh.Materials[i] - 1));
            }*/
            
            // OrgRot
            newMesh.Origin = sourceMesh.Origin;
            newMesh.Rotation = sourceMesh.Rotation;

            // Bones
            newMesh.Bones = new List<SkeletalMesh.BoneStruct>();
            foreach (UDKExplorer.UDK.Classes.SkeletalMesh.BoneStruct bone in sourceMesh.Bones)
            {
                newMesh.Bones.Add(SkeletalMesh.BoneStruct.ImportFromUDK(bone, udk, pcc));
            }
            newMesh.SkeletonDepth = sourceMesh.SkeletonDepth;

            // LODs
            newMesh.LODModels = new List<SkeletalMesh.LODModelStruct>();
            foreach (UDKExplorer.UDK.Classes.SkeletalMesh.LODModelStruct lod in sourceMesh.LODModels)
            {
                SkeletalMesh.LODModelStruct newLOD = new SkeletalMesh.LODModelStruct();

                newLOD.Sections = new List<SkeletalMesh.SectionStruct>();
                foreach (UDKExplorer.UDK.Classes.SkeletalMesh.SectionStruct secudk in lod.Sections)
                {
                    SkeletalMesh.SectionStruct secpcc = new SkeletalMesh.SectionStruct();
                    secpcc.BaseIndex = secudk.BaseIndex;
                    secpcc.ChunkIndex = secudk.ChunkIndex;
                    secpcc.MaterialIndex = secudk.MaterialIndex;
                    secpcc.NumTriangles = secudk.NumTriangles;
                    newLOD.Sections.Add(secpcc);
                }
                newLOD.IndexBuffer = new SkeletalMesh.MultiSizeIndexContainerStruct();
                newLOD.IndexBuffer.IndexCount = lod.IndexBuffer.IndexCount;
                newLOD.IndexBuffer.IndexSize = lod.IndexBuffer.IndexSize;
                newLOD.IndexBuffer.Indexes = new List<ushort>();
                foreach (ushort Idx in lod.IndexBuffer.Indexes)
                    newLOD.IndexBuffer.Indexes.Add(Idx);
                // TODO: Unk1
                newLOD.Unk1 = 0;
                /* We don't need to change bone indexes, because the skeleton is coming along for the ride.
                
                List<int> BoneMap = new List<int>(); // Maps source bone i to existing bone index BoneMap[i]
                for (int i = 0; i < sourceMesh.Bones.Count; i++)
                {
                    string sourceName = udk.GetName(sourceMesh.Bones[i].Name);
                    bool found = false;
                    for (int j = 0; j < skm.Bones.Count; j++)
                    {
                        string pccb = pcc.getNameEntry(skm.Bones[j].Name);
                        if (pccb == sourceName)
                        {
                            found = true;
                            BoneMap.Add(j);
                            if (MPOpt.SKM_importbones)
                            {
                                SkeletalMesh.BoneStruct bpcc = skm.Bones[j];
                                UDKExplorer.UDK.Classes.SkeletalMesh.BoneStruct budk = sourceMesh.Bones[i];
                                bpcc.Orientation = budk.Orientation;
                                bpcc.Position = budk.Position;
                                skm.Bones[j] = bpcc;
                            }
                        }
                    }
                    if (!found)
                    {
                        BoneMap.Add(0);
                    }
                }*/

                newLOD.ActiveBones = new List<ushort>();
                foreach (ushort Idx in lod.ActiveBones)
                    newLOD.ActiveBones.Add(/*(ushort)BoneMap[Idx]*/Idx);
                // TODO: Unk2
                newLOD.Unk2 = 0;
                newLOD.Chunks = new List<SkeletalMesh.SkelMeshChunkStruct>();
                foreach (UDKExplorer.UDK.Classes.SkeletalMesh.SkelMeshChunkStruct chunkudk in lod.Chunks)
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
                        chunkpcc.BoneMap.Add(/*(ushort)BoneMap[Idx]*/Idx);
                    newLOD.Chunks.Add(chunkpcc);
                }
                newLOD.Size = lod.Size;
                newLOD.NumVertices = lod.NumVertices;
                // TODO: Unk3
                newLOD.Unk3 = 0;
                newLOD.RequiredBones = new List<byte>();
                foreach (byte b in lod.RequiredBones)
                    newLOD.RequiredBones.Add(b);
                newLOD.RawPointIndicesFlag = lod.RawPointIndicesFlag;
                newLOD.RawPointIndicesCount = lod.RawPointIndicesCount;
                newLOD.RawPointIndicesSize = lod.RawPointIndicesSize;
                newLOD.RawPointIndicesOffset = lod.RawPointIndicesOffset;
                newLOD.RawPointIndices = new List<int>();
                foreach (int i in lod.RawPointIndices)
                    newLOD.RawPointIndices.Add(i);
                newLOD.NumTexCoords = lod.NumTexCoords;
                newLOD.VertexBufferGPUSkin = new SkeletalMesh.VertexBufferGPUSkinStruct();
                newLOD.VertexBufferGPUSkin.NumTexCoords = lod.VertexBufferGPUSkin.NumTexCoords;
                newLOD.VertexBufferGPUSkin.Extension = lod.VertexBufferGPUSkin.Extension;
                newLOD.VertexBufferGPUSkin.Origin = lod.VertexBufferGPUSkin.Origin;
                newLOD.VertexBufferGPUSkin.VertexSize = lod.VertexBufferGPUSkin.VertexSize;
                newLOD.VertexBufferGPUSkin.Vertices = new List<SkeletalMesh.GPUSkinVertexStruct>();
                foreach (UDKExplorer.UDK.Classes.SkeletalMesh.GPUSkinVertexStruct vudk in lod.VertexBufferGPUSkin.Vertices)
                {
                    SkeletalMesh.GPUSkinVertexStruct vpcc = new SkeletalMesh.GPUSkinVertexStruct();
                    vpcc.TangentX = vudk.TangentX;
                    vpcc.TangentZ = vudk.TangentZ;
                    vpcc.Position = vudk.Position;
                    vpcc.InfluenceBones = vudk.InfluenceBones;
                    vpcc.InfluenceWeights = vudk.InfluenceWeights;
                    vpcc.U = vudk.U;
                    vpcc.V = vudk.V;
                    newLOD.VertexBufferGPUSkin.Vertices.Add(vpcc);
                }
                // TODO: Unk4
                newLOD.Unk4 = 0;
                //skm.LODModels[SelectedLOD] = newLOD;
                newMesh.LODModels.Add(newLOD);
            }

            // Tail
            newMesh.TailNames = new List<SkeletalMesh.TailNamesStruct>();
            for (int i = 0; i < newMesh.Bones.Count; i++)
                newMesh.TailNames.Add(new SkeletalMesh.TailNamesStruct(newMesh.Bones[i].Name, i));
            newMesh.Unk1 = oldMesh.Unk1;
            newMesh.Unk2 = oldMesh.Unk2;
            newMesh.Unk3 = new List<int>();
            foreach (int i in oldMesh.Unk3)
                newMesh.Unk3.Add(i);

            // Write mesh data to temporary buffer
            SerializingContainer serializer = new SerializingContainer();
            serializer.Memory = new MemoryStream();
            serializer.isLoading = false;
            newMesh.Serialize(serializer);

            // Copy in the properties from the old mesh
            int propertyEnd = newMesh.GetPropertyEnd();
            MemoryStream finalData = new MemoryStream();
            finalData.Write(pcc.Exports[SelectedObject].Data, 0, propertyEnd);

            // Copy in the data from the new mesh
            finalData.Write(serializer.Memory.ToArray(), 0, (int)serializer.Memory.Length);

            // Upload final data
            pcc.Exports[SelectedObject].Data = finalData.ToArray();
            MessageBox.Show("Mesh replaced.");
            Close();
        }
    }
}
