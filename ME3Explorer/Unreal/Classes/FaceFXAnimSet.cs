using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Packages;

namespace ME3Explorer.Unreal.Classes
{
    public class FaceFXAnimSet
    {
        public IMEPackage pcc;
        public byte[] Memory;
        public IExportEntry entry;
        public int ReadEnd;
        public HeaderStruct Header;
        public DataAnimSetStruct Data;

        public class HeaderStruct
        {
            public uint Magic;
            public int unk1;
            public int unk2;
            public string Licensee;
            public string Project;
            public int unk3;
            public ushort unk4;
            public HNodeStruct[] Nodes;
            public string[] Names;
        }
        public class HNodeStruct
        {
            public int unk1;
            public int unk2;
            public string Name;
            public ushort unk3;
        }
        public class DataAnimSetStruct
        {
            public int unk1;
            public int unk2;
            public int unk3;
            public int unk4;
            public FaceFXLine[] Data;
        }
        public class FaceFXLine
        {
            public int Name;
            public NameRef[] animations;
            public ControlPoint[] points;
            public int[] numKeys;
            public float FadeInTime;
            public float FadeOutTime;
            public int unk2;
            public string path { get; set; }
            public string ID;
            public int unk3;

            public FaceFXLine Clone()
            {
                FaceFXLine line = this.MemberwiseClone() as FaceFXLine;
                line.animations = line.animations.Clone() as NameRef[];
                line.points = line.points.Clone() as ControlPoint[];
                line.numKeys = line.numKeys.Clone() as int[];
                return line;
            }
        }
        public struct NameRef
        {
            public int index;
            public int unk2;
        }
        public struct ControlPoint
        {
            public float time;
            public float weight;
            public float inTangent;
            public float leaveTangent;
        }

        public FaceFXAnimSet()
        {
        }
        public FaceFXAnimSet(IMEPackage Pcc, IExportEntry Entry)
        {
            BitConverter.IsLittleEndian = true;
            pcc = Pcc;
            entry = Entry;
            byte[] buff = entry.Data;
            List<PropertyReader.Property> props = PropertyReader.getPropList(entry);
            int start = props[props.Count - 1].offend + 4;
            Memory = new byte[buff.Length - start];
            for (int i = 0; i < buff.Length - start; i++)
                Memory[i] = buff[i + start];//Skip Props and size int
            MemoryStream m = new MemoryStream(Memory);
            SerializingContainer Container = new SerializingContainer(m);
            Container.isLoading = true;
            Serialize(Container);
        }

        public void Serialize(SerializingContainer Container)
        {
            SerializeHeader(Container);
            SerializeData(Container);
            ReadEnd = Container.GetPos();
        }        
        public void SerializeHeader(SerializingContainer Container)
        {
            if (Container.isLoading)
                Header = new HeaderStruct();
            Header.Magic = Container + Header.Magic;
            Header.unk1 = Container + Header.unk1;
            Header.unk2 = Container + Header.unk2;
            int count = 0;
            if (!Container.isLoading)
                count = Header.Licensee.Length;
            else
                Header.Licensee = "";
            Header.Licensee = SerializeString(Container, Header.Licensee);
            count = 0;
            if (!Container.isLoading)
                count = Header.Project.Length;
            else
                Header.Project = "";
            Header.Project = SerializeString(Container, Header.Project);
            Header.unk3 = Container + Header.unk3;
            Header.unk4 = Container + Header.unk4;
            count = 0;
            if (!Container.isLoading)
                count = Header.Nodes.Length;
            count = Container + count;
            if (Container.isLoading)
                Header.Nodes = new HNodeStruct[count];
            for (int i = 0; i < count; i++)
            {
                if (Container.isLoading)
                    Header.Nodes[i] = new HNodeStruct();
                HNodeStruct t = Header.Nodes[i];
                t.unk1 = Container + t.unk1;
                t.unk2 = Container + t.unk2;
                t.Name = SerializeString(Container, t.Name);
                t.unk3 = Container + t.unk3;
                Header.Nodes[i] = t;
            }
            count = 0;
            if (!Container.isLoading)
                count = Header.Names.Length;
            count = Container + count;
            if (Container.isLoading)
                Header.Names = new string[count];
            for (int i = 0; i < count; i++)
                Header.Names[i] = SerializeString(Container, Header.Names[i]);
        }
        public void SerializeData(SerializingContainer Container)
        {
            if (Container.isLoading)
                Data = new DataAnimSetStruct();
            Data.unk1 = Container + Data.unk1;
            Data.unk2 = Container + Data.unk2;
            Data.unk3 = Container + Data.unk3;
            Data.unk4 = Container + Data.unk4;
            int count = 0;
            if (!Container.isLoading)
                count = Data.Data.Length;
            count = Container + count;
            if (Container.isLoading)
                Data.Data = new FaceFXLine[count];
            for (int i = 0; i < count; i++)
            {
                if (Container.isLoading)
                    Data.Data[i] = new FaceFXLine();
                FaceFXLine d = Data.Data[i];
                d.Name = Container + d.Name;
                int count2 = 0;
                if (!Container.isLoading)
                    count2 = d.animations.Length;
                count2 = Container + count2;
                if (Container.isLoading)
                    d.animations = new NameRef[count2];
                for (int j = 0; j < count2; j++)
                {
                    if (Container.isLoading)
                        d.animations[j] = new NameRef();
                    NameRef u = d.animations[j];
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
                count2 = 0;
                if (!Container.isLoading)
                    count2 = d.numKeys.Length;
                count2 = Container + count2;
                if (Container.isLoading)
                    d.numKeys = new int[count2];
                for (int j = 0; j < count2; j++)
                    d.numKeys[j] = Container + d.numKeys[j];
                d.FadeInTime = Container + d.FadeInTime;
                d.FadeOutTime = Container + d.FadeOutTime;
                d.unk2 = Container + d.unk2;
                d.path = SerializeString(Container, d.path);
                d.ID = SerializeString(Container, d.ID);
                d.unk3 = Container + d.unk3;
                Data.Data[i] = d;
            }
        }

        public string SerializeString(SerializingContainer Container, string s)
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
            res.Nodes.Add("Magic : 0x" + Header.Magic.ToString("X8"));
            res.Nodes.Add("Unk1 : 0x" + Header.unk1.ToString("X8"));
            res.Nodes.Add("Unk2 : 0x" + Header.unk2.ToString("X8"));
            res.Nodes.Add("Licensee : " + Header.Licensee);
            res.Nodes.Add("Project : " + Header.Project);
            res.Nodes.Add("Unk3 : 0x" + Header.unk3.ToString("X8"));
            res.Nodes.Add("Unk4 : 0x" + Header.unk4.ToString("X4"));
            TreeNode t = new TreeNode("Nodes");
            int count = 0;
            foreach (HNodeStruct h in Header.Nodes)
                t.Nodes.Add((count++) + " : " + h.unk1.ToString("X8") + " " + h.unk2.ToString("X8") + " \"" + h.Name + "\" " + h.unk3.ToString("X4"));
            res.Nodes.Add(t);
            TreeNode t2 = new TreeNode("Names");
            count = 0;
            foreach (string s in Header.Names)
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
            foreach (FaceFXLine d in Data.Data)
            {
                TreeNode t3 = new TreeNode((count++) + " : ID(" + d.ID.Trim() + ") Path : " + d.path.Trim());
                t3.Nodes.Add("Name : 0x" + d.Name.ToString("X8") + " \"" + Header.Names[d.Name].Trim() + "\"");
                TreeNode t4 = new TreeNode("Animations");
                count2 = 0;
                foreach (NameRef u in d.animations)
                    t4.Nodes.Add((count2++) + " : " + u.index.ToString("X8") + " " + u.unk2.ToString("X8") + " \"" + Header.Names[u.index].Trim() + "\"");
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
                t3.Nodes.Add("Unk3 : 0x" + d.unk3.ToString("X8"));
                t2.Nodes.Add(t3);
            }
            res.Nodes.Add(t2);
            res.Expand();
            return res;
        }

        public TreeNode[] DataToTree2(FaceFXLine d)
        {
            TreeNode[] nodes = new TreeNode[7];
            nodes[0] = new TreeNode("Name : 0x" + d.Name.ToString("X8") + " \"" + Header.Names[d.Name].Trim() + "\"");
            nodes[1] = new TreeNode("FadeInTime : " + d.FadeInTime);
            nodes[2] = new TreeNode("FadeOutTime : " + d.FadeOutTime);
            nodes[3] = new TreeNode("Unk2 : 0x" + d.unk2.ToString("X8"));
            nodes[4] = new TreeNode("Path : " + d.path);
            nodes[5] = new TreeNode("ID : " + d.ID);
            nodes[6] = new TreeNode("Unk3 : 0x" + d.unk3.ToString("X8"));
            return nodes;
        }

        public void DumpToFile(string path)
        {
            BitConverter.IsLittleEndian = true;
            MemoryStream m = new MemoryStream();
            SerializingContainer Container = new SerializingContainer(m);
            Container.isLoading = false;
            Serialize(Container);
            m = Container.Memory;
            File.WriteAllBytes(path, m.ToArray());
        }

        public void Save()
        {
            BitConverter.IsLittleEndian = true;
            MemoryStream m = new MemoryStream();
            SerializingContainer Container = new SerializingContainer(m);
            Container.isLoading = false;
            Serialize(Container);
            m = Container.Memory;
            MemoryStream res = new MemoryStream();
            byte[] buff = entry.Data;
            List<PropertyReader.Property> props = PropertyReader.getPropList(entry);
            int start = props[props.Count - 1].offend;
            res.Write(buff, 0, start);
            res.Write(BitConverter.GetBytes((int)m.Length), 0, 4);
            res.Write(m.ToArray(), 0, (int)m.Length);
            entry.Data = res.ToArray();
        }

        public void CloneEntry(int n)
        {
            if (n < 0 || n >= Data.Data.Length)
                return;
            List<FaceFXLine> list = new List<FaceFXLine>();
            list.AddRange(Data.Data);
            list.Add(Data.Data[n]);
            Data.Data = list.ToArray();
        }
        public void RemoveEntry(int n)
        {
            if (n < 0 || n >= Data.Data.Length)
                return;
            List<FaceFXLine> list = new List<FaceFXLine>();
            list.AddRange(Data.Data);
            list.RemoveAt(n);
            Data.Data = list.ToArray();
        }

        public void MoveEntry(int n, int m)
        {
            if (n < 0 || n >= Data.Data.Length || m < 0 || m >= Data.Data.Length)
                return;
            List<FaceFXLine> list = new List<FaceFXLine>();
            for (int i = 0; i < Data.Data.Length; i++)
                if (i != n)
                    list.Add(Data.Data[i]);
            list.Insert(m, Data.Data[n]);
            Data.Data = list.ToArray();
        }

        public void AddName(string s)
        {
            List<string> list = new List<string>(Header.Names);
            list.Add(s);
            Header.Names = list.ToArray();
        }
    }
}
