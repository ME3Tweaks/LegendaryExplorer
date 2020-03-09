using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Gammtek.Conduit.Extensions.IO;
using Gammtek.Conduit.IO;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using StreamHelpers;

namespace ME3Explorer.FaceFX
{
    public class ME3FaceFXAnimSet : IFaceFXAnimSet
    {
        IMEPackage pcc;
        public ExportEntry export;
        public ExportEntry Export => export;
        ME3HeaderStruct header;
        public HeaderStruct Header => header;
        public ME3DataAnimSetStruct Data { get; private set; }

        public ME3FaceFXAnimSet()
        {
        }
        public ME3FaceFXAnimSet(IMEPackage Pcc, ExportEntry Entry)
        {

            pcc = Pcc;
            export = Entry;
            int start = export.propsEnd() + 4;
            SerializingContainer Container = new SerializingContainer(new EndianReader(new MemoryStream(export.Data.Skip(start).ToArray())) { Endian = Pcc.Endian});
            Container.isLoading = true;
            Serialize(Container);
        }

        void Serialize(SerializingContainer Container)
        {
            SerializeHeader(Container);
            SerializeData(Container);
        }

        void SerializeHeader(SerializingContainer Container)
        {
            if (Container.isLoading)
                header = new ME3HeaderStruct();
            header.Magic = Container + header.Magic;
            header.unk1 = Container + header.unk1;
            header.unk2 = Container + header.unk2;
            int count = 0;
            if (!Container.isLoading)
                count = header.Licensee.Length;
            else
                header.Licensee = "";
            header.Licensee = SerializeString(Container, header.Licensee);
            count = 0;
            if (!Container.isLoading)
                count = header.Project.Length;
            else
                header.Project = "";
            header.Project = SerializeString(Container, header.Project);
            header.unk3 = Container + header.unk3;
            header.unk4 = Container + header.unk4;
            count = 0;
            if (!Container.isLoading)
                count = header.Nodes.Length;
            count = Container + count;
            if (Container.isLoading)
                header.Nodes = new HNodeStruct[count];
            for (int i = 0; i < count; i++)
            {
                if (Container.isLoading)
                    header.Nodes[i] = new HNodeStruct();
                HNodeStruct t = header.Nodes[i];
                t.unk1 = Container + t.unk1;
                t.unk2 = Container + t.unk2;
                t.Name = SerializeString(Container, t.Name);
                t.unk3 = Container + t.unk3;
                header.Nodes[i] = t;
            }
            count = 0;
            if (!Container.isLoading)
                count = header.Names.Length;
            count = Container + count;
            if (Container.isLoading)
                header.Names = new string[count];
            for (int i = 0; i < count; i++)
                header.Names[i] = SerializeString(Container, header.Names[i]);
        }

        void SerializeData(SerializingContainer Container)
        {
            if (Container.isLoading)
                Data = new ME3DataAnimSetStruct();
            Data.unk1 = Container + Data.unk1;
            Data.unk2 = Container + Data.unk2;
            Data.unk3 = Container + Data.unk3;
            Data.unk4 = Container + Data.unk4;
            int count = 0;
            if (!Container.isLoading)
                count = Data.Data.Length;
            count = Container + count;
            if (Container.isLoading)
                Data.Data = new ME3FaceFXLine[count];
            for (int i = 0; i < count; i++)
            {
                if (Container.isLoading)
                    Data.Data[i] = new ME3FaceFXLine();
                ME3FaceFXLine d = Data.Data[i];
                d.Name = Container + d.Name;
                if (Container.isLoading)
                {
                    d.NameAsString = header.Names[d.Name];
                }
                int count2 = 0;
                if (!Container.isLoading)
                    count2 = d.animations.Length;
                count2 = Container + count2;
                if (Container.isLoading)
                    d.animations = new ME3NameRef[count2];
                for (int j = 0; j < count2; j++)
                {
                    if (Container.isLoading)
                        d.animations[j] = new ME3NameRef();
                    ME3NameRef u = d.animations[j];
                    u.index = Container + u.index;
                    u.unk2 = Container + u.unk2;
                    d.animations[j] = u;
                }
                count2 = 0;
                if (!Container.isLoading)
                    count2 = d.points.Length;
                count2 = Container + count2;
                if (Container.isLoading)
                    d.points = new ControlPoint[count2];
                for (int j = 0; j < count2; j++)
                {
                    if (Container.isLoading)
                        d.points[j] = new ControlPoint();
                    ControlPoint u = d.points[j];
                    u.time = Container + u.time;
                    u.weight = Container + u.weight;
                    u.inTangent = Container + u.inTangent;
                    u.leaveTangent = Container + u.leaveTangent;
                    d.points[j] = u;
                }
                if (d.points.Length > 0)
                {
                    count2 = 0;
                    if (!Container.isLoading)
                        count2 = d.numKeys.Length;
                    count2 = Container + count2;
                    if (Container.isLoading)
                        d.numKeys = new int[count2];
                    for (int j = 0; j < count2; j++)
                        d.numKeys[j] = Container + d.numKeys[j];
                }
                else if (Container.isLoading)
                {
                    d.numKeys = new int[d.animations.Length];
                }
                d.FadeInTime = Container + d.FadeInTime;
                d.FadeOutTime = Container + d.FadeOutTime;
                d.unk2 = Container + d.unk2;
                d.path = SerializeString(Container, d.path);
                d.ID = SerializeString(Container, d.ID);
                d.index = Container + d.index;
                Data.Data[i] = d;
            }
        }

        string SerializeString(SerializingContainer Container, string s)
        {
            int len = 0;
            byte t = 0;
            if (Container.isLoading)
            {
                s = "";
                len = Container + len;
                for (int i = 0; i < len; i++)
                    s += (char)(Container + (byte)0);
            }
            else
            {
                len = s.Length;
                len = Container + len;
                foreach (char c in s)
                    t = Container + (byte)c;
            }
            return s;
        }

        public TreeNode HeaderToTree()
        {
            TreeNode res = new TreeNode("Header");
            res.Nodes.Add("Magic : 0x" + header.Magic.ToString("X8"));
            res.Nodes.Add("Unk1 : 0x" + header.unk1.ToString("X8"));
            res.Nodes.Add("Unk2 : 0x" + header.unk2.ToString("X8"));
            res.Nodes.Add("Licensee : " + header.Licensee);
            res.Nodes.Add("Project : " + header.Project);
            res.Nodes.Add("Unk3 : 0x" + header.unk3.ToString("X8"));
            res.Nodes.Add("Unk4 : 0x" + header.unk4.ToString("X4"));
            TreeNode t = new TreeNode("Nodes");
            int count = 0;
            foreach (HNodeStruct h in header.Nodes)
                t.Nodes.Add((count++) + " : " + h.unk1.ToString("X8") + " " + h.unk2.ToString("X8") + " \"" + h.Name + "\" " + h.unk3.ToString("X4"));
            res.Nodes.Add(t);
            TreeNode t2 = new TreeNode("Names");
            count = 0;
            foreach (string s in header.Names)
                t2.Nodes.Add((count++) + " : " + s);
            res.Nodes.Add(t2);
            res.Expand();
            return res;
        }

        public TreeNode DataToTree()
        {
            TreeNode res = new TreeNode("Data");
            TreeNode t = new TreeNode("Header");
            t.Nodes.Add(Data.unk1.ToString("X8"));
            t.Nodes.Add(Data.unk2.ToString("X8"));
            t.Nodes.Add(Data.unk3.ToString("X8"));
            t.Nodes.Add(Data.unk4.ToString("X8"));
            res.Nodes.Add(t);
            TreeNode t2 = new TreeNode("Entries");
            int count = 0;
            int count2 = 0;
            foreach (ME3FaceFXLine d in Data.Data)
            {
                TreeNode t3 = new TreeNode((count++) + " : ID(" + d.ID.Trim() + ") Path : " + d.path.Trim());
                t3.Nodes.Add("Name : 0x" + d.Name.ToString("X8") + " \"" + header.Names[d.Name].Trim() + "\"");
                TreeNode t4 = new TreeNode("Animations");
                count2 = 0;
                foreach (ME3NameRef u in d.animations)
                    t4.Nodes.Add((count2++) + " : " + u.index.ToString("X8") + " " + u.unk2.ToString("X8") + " \"" + header.Names[u.index].Trim() + "\"");
                t3.Nodes.Add(t4);
                TreeNode t5 = new TreeNode("Points");
                count2 = 0;
                foreach (ControlPoint u in d.points)
                    t5.Nodes.Add((count2++) + " : " + u.time + " ; " + u.weight + " ; " + u.inTangent + " ; " + u.leaveTangent);
                t3.Nodes.Add(t5);
                TreeNode t6 = new TreeNode("animLengths");
                count2 = 0;
                foreach (int u in d.numKeys)
                    t6.Nodes.Add((count2++) + " : " + u.ToString("X8"));
                t3.Nodes.Add(t6);
                t3.Nodes.Add("FadeInTime : " + d.FadeInTime);
                t3.Nodes.Add("FadeOutTime : " + d.FadeOutTime);
                t3.Nodes.Add("Unk2 : 0x" + d.unk2.ToString("X8"));
                t3.Nodes.Add("Path : " + d.path);
                t3.Nodes.Add("ID : " + d.ID);
                t3.Nodes.Add("Unk3 : 0x" + d.index.ToString("X8"));
                t2.Nodes.Add(t3);
            }
            res.Nodes.Add(t2);
            res.Expand();
            return res;
        }

        public TreeNode[] DataToTree2(ME3FaceFXLine d)
        {
            TreeNode[] nodes = new TreeNode[7];
            nodes[0] = new TreeNode("Name : 0x" + d.Name.ToString("X8") + " \"" + header.Names[d.Name].Trim() + "\"");
            nodes[1] = new TreeNode("FadeInTime : " + d.FadeInTime);
            nodes[2] = new TreeNode("FadeOutTime : " + d.FadeOutTime);
            nodes[3] = new TreeNode("Unk2 : 0x" + d.unk2.ToString("X8"));
            nodes[4] = new TreeNode("Path : " + d.path);
            nodes[5] = new TreeNode("ID : " + d.ID);
            nodes[6] = new TreeNode("Index : 0x" + d.index.ToString("X8"));
            return nodes;
        }

        public void DumpToFile(string path)
        {

            EndianReader e = new EndianReader(new MemoryStream()) { Endian = Export.FileRef.Endian };
            SerializingContainer Container = new SerializingContainer(e);
            Container.isLoading = false;
            Serialize(Container);
            Container.Memory.BaseStream.WriteToFile(path);
        }

        public void Save()
        {

            EndianReader e = new EndianReader(new MemoryStream()) { Endian = Export.FileRef.Endian };
            SerializingContainer Container = new SerializingContainer(e)
            {
                isLoading = false
            };
            Serialize(Container);
            MemoryStream res = new MemoryStream();
            int start = export.propsEnd();
            res.Write(export.Data, 0, start);
            res.WriteInt32((int)e.Length);
            res.WriteBytes(e.ToArray());
            res.WriteInt32(0);
            export.Data = res.ToArray();
        }

        public void CloneEntry(int n)
        {
            if (n < 0 || n >= Data.Data.Length)
                return;
            var list = new List<ME3FaceFXLine>();
            list.AddRange(Data.Data);
            list.Add(Data.Data[n]);
            Data.Data = list.ToArray();
        }
        public void RemoveEntry(int n)
        {
            if (n < 0 || n >= Data.Data.Length)
                return;
            var list = new List<ME3FaceFXLine>();
            list.AddRange(Data.Data);
            list.RemoveAt(n);
            Data.Data = list.ToArray();
        }

        public void MoveEntry(int n, int m)
        {
            if (n < 0 || n >= Data.Data.Length || m < 0 || m >= Data.Data.Length)
                return;
            List<ME3FaceFXLine> list = Data.Data.Where((_, i) => i != n).ToList();
            list.Insert(m, Data.Data[n]);
            Data.Data = list.ToArray();
        }

        public void AddName(string s)
        {
            var list = new List<string>(header.Names);
            list.Add(s);
            header.Names = list.ToArray();
        }

        public void FixNodeTable()
        {
            header.Nodes = ME3HeaderStruct.fullNodeTable;
        }
    }
}
