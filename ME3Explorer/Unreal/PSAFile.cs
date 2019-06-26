using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpDX;
using StreamHelpers;

namespace ME3Explorer.Unreal
{
    public class PSAFile
    {
        #region Declaration

        public struct PSAPoint
        {
            public float x;
            public float y;
            public float z;

            public PSAPoint(float _x, float _y, float _z)
            {
                x = _x;
                y = _y;
                z = _z;
            }

            public PSAPoint(byte[] raw, int offset)
            {
                
                x = BitConverter.ToSingle(raw, offset);
                y = BitConverter.ToSingle(raw, offset + 4);
                z = BitConverter.ToSingle(raw, offset + 8);
            }

            public PSAPoint(Vector3 v)
            {
                x = v.X;
                y = v.Y;
                z = v.Z;
            }

            public Vector3 ToVector3()
            {
                return new Vector3(x, y, z);
            }
        }

        public struct PSAQuad
        {
            public float w;
            public float x;
            public float y;
            public float z;

            public PSAQuad(float _w, float _x, float _y, float _z)
            {
                w = _w;
                x = _x;
                y = _y;
                z = _z;
            }

            public PSAQuad(Vector4 v)
            {
                w = v.W;
                x = v.X;
                y = v.Y;
                z = v.Z;
            }
            public PSAQuad(byte[] raw, int offset)
            {
                
                x = BitConverter.ToSingle(raw, offset);
                y = BitConverter.ToSingle(raw, offset + 4);
                z = BitConverter.ToSingle(raw, offset + 8);
                w = BitConverter.ToSingle(raw, offset + 12);
            }

            public Vector4 ToVector4()
            {
                return new Vector4(x, y, z, w);
            }
        }
        
        public struct PSABone
        {
            public string name;
            public int flags;
            public int childs;
            public int parent;
            public PSAQuad rotation;
            public PSAPoint location;
            public float length;
            public PSAPoint size;
            public int index; //for bone tree
        }

        public struct PSAAnimInfo
        {
            public byte[] raw;
            public string name;
            public string group;
            public int TotalBones;
            public int RootInclude;
            public int KeyCompressionStyle;
            public int KeyQuotum;
            public float KeyReduction;
            public float TrackTime;
            public float AnimRate;
            public int StartBone;
            public int FirstRawFrame;
            public int NumRawFrames;
        }

        public struct PSAAnimKeys
        {
            public byte[] raw;
            public PSAPoint location;
            public PSAQuad rotation;
            public float time;
        }

        public struct PSAData
        {
            public List<PSABone> Bones;
            public List<PSAAnimInfo> Infos;
            public List<PSAAnimKeys> Keys;
        }

        public struct ChunkHeader
        {
            public string name;
            public int flags;
            public int size;
            public int count;
        }

        public PSAData data;

#endregion

        public PSAFile()
        {
        }

        public void ImportPSA(string path)
        {
            data = new PSAData();
            
            FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read);
            do
            {
                ChunkHeader h = ReadHeader(file);                 
                switch (h.name)
                {

                    case "ANIMHEAD":
                        break;
                    case "BONENAMES":
                        ReadBones(file, h);
                        break;
                    case "ANIMINFO":
                        ReadAnimInfo(file, h);
                        break;
                    case "ANIMKEYS":
                        ReadAnimKeys(file, h);
                        break;
                    default:
                        file.Seek(h.size * h.count, SeekOrigin.Current);
                        break;
                }

            } while (file.Position < file.Length);
        }

        public void ExportPSA(string path)
        {
            
            MemoryStream m = new MemoryStream();
            WriteAnimHead(m);
            WriteBones(m);
            WriteInfos(m);
            WriteKeys(m);
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            fs.Write(m.ToArray(), 0, (int)m.Length);
            fs.Close();
        }

        public void WriteHeader(MemoryStream m, string name, int size, int count)
        {
            for (int i = 0; i < 20; i++)
                if (i < name.Length)
                    m.WriteByte((byte)name[i]);
                else
                    m.WriteByte(0);
            m.WriteInt32(0x1e83b9);
            m.WriteInt32(size);
            m.WriteInt32(count);
        }

        public void WriteAnimHead(MemoryStream m)
        {
            WriteHeader(m, "ANIMHEAD", 0, 0);
        }

        public void WriteBones(MemoryStream m)
        {
            WriteHeader(m, "BONENAMES", 0x78, data.Bones.Count);
            foreach (PSABone b in data.Bones)
            {
                for (int j = 0; j < 64; j++)
                {
                    if (j < b.name.Length)
                        m.WriteByte((byte) b.name[j]);
                    else
                        m.WriteByte(0);
                }

                m.WriteInt32(b.flags);
                m.WriteInt32(b.childs);
                m.WriteInt32(b.parent);
                for (int j = 0; j < 44; j++)
                    m.WriteByte(0);
            }
        }

        public void WriteInfos(MemoryStream m)
        {
            WriteHeader(m, "ANIMINFO", 0xa8, data.Infos.Count);
            foreach (PSAAnimInfo inf in data.Infos)
            {
                for (int i = 0; i < 64; i++)
                    if (i < inf.name.Length)
                        m.WriteByte((byte)inf.name[i]);
                    else
                        m.WriteByte(0);
                for (int i = 0; i < 64; i++)
                    if (i < inf.group.Length)
                        m.WriteByte((byte)inf.group[i]);
                    else
                        m.WriteByte(0);
                m.WriteInt32(inf.TotalBones);
                m.WriteInt32(inf.RootInclude);
                m.WriteInt32(inf.KeyCompressionStyle);
                m.WriteInt32(inf.KeyQuotum);
                m.WriteFloat(inf.KeyReduction);
                m.WriteFloat(inf.TrackTime);
                m.WriteFloat(inf.AnimRate);
                m.WriteInt32(inf.StartBone);
                m.WriteInt32(inf.FirstRawFrame);
                m.WriteInt32(inf.NumRawFrames);
            }
        }

        public void WriteKeys(MemoryStream m)
        {
            WriteHeader(m, "ANIMKEYS", 0x20, data.Keys.Count);
            foreach (PSAAnimKeys k in data.Keys)
            {
                m.WriteFloat(k.location.x);
                m.WriteFloat(k.location.y);
                m.WriteFloat(k.location.z);
                m.WriteFloat(k.rotation.x);
                m.WriteFloat(k.rotation.y);
                m.WriteFloat(k.rotation.z);
                m.WriteFloat(k.rotation.w);
                m.WriteFloat(k.time);
            }
        }

        public void ReadBones(FileStream fs,ChunkHeader h)
        {
            byte[] buffer;
            data.Bones= new List<PSABone>();
            for (int i = 0; i < h.count; i++)
            {
                PSABone b = new PSABone();
                buffer = new byte[64];
                fs.Read(buffer, 0, 64);
                b.name = "";
                for (int j = 0; j < 64; j++)
                    if (buffer[j] != 0)
                        b.name += (char)buffer[j];
                b.name = b.name.Trim();
                buffer = new byte[4];
                fs.Read(buffer, 0, 4);
                fs.Read(buffer, 0, 4);
                b.childs = BitConverter.ToInt32(buffer, 0);
                fs.Read(buffer, 0, 4);
                b.parent = BitConverter.ToInt32(buffer, 0);
                fs.Read(buffer, 0, 4);
                b.rotation.x = BitConverter.ToInt32(buffer, 0);
                fs.Read(buffer, 0, 4);
                b.rotation.y = BitConverter.ToInt32(buffer, 0);
                fs.Read(buffer, 0, 4);
                b.rotation.z = BitConverter.ToInt32(buffer, 0);
                fs.Read(buffer, 0, 4);
                b.rotation.w = BitConverter.ToInt32(buffer, 0);
                fs.Read(buffer, 0, 4);
                b.location.x = BitConverter.ToInt32(buffer, 0);
                fs.Read(buffer, 0, 4);
                b.location.y = BitConverter.ToInt32(buffer, 0);
                fs.Read(buffer, 0, 4);
                b.location.z = BitConverter.ToInt32(buffer, 0);
                fs.Read(buffer, 0, 4);
                fs.Read(buffer, 0, 4);
                fs.Read(buffer, 0, 4);
                fs.Read(buffer, 0, 4);
                data.Bones.Add(b);
            }
        }

        public void ReadAnimInfo(FileStream fs, ChunkHeader h)
        {
            
            data.Infos = new List<PSAAnimInfo>();
            for (int i = 0; i < h.count; i++)
            {
                PSAAnimInfo info = new PSAAnimInfo();
                byte[] buff = new byte[h.size];
                for (int j = 0; j < h.size; j++)
                    buff[j] = (byte)fs.ReadByte();
                info.raw = buff;
                info.name = "";
                for (int j = 0; j < 64; j++)
                    if (buff[j] != 0)
                        info.name += (char)buff[j];
                info.group = "";
                for (int j = 0; j < 64; j++)
                    if (buff[j + 64] != 0)
                        info.group += (char)buff[j + 64];
                info.TotalBones = BitConverter.ToInt32(buff, 128);
                info.RootInclude = BitConverter.ToInt32(buff, 132);
                info.KeyCompressionStyle = BitConverter.ToInt32(buff, 136);
                info.KeyQuotum = BitConverter.ToInt32(buff, 140);
                info.KeyReduction = BitConverter.ToSingle(buff, 144);
                info.TrackTime = BitConverter.ToSingle(buff, 148);
                info.AnimRate = BitConverter.ToSingle(buff, 152);
                info.StartBone = BitConverter.ToInt32(buff, 156);
                info.FirstRawFrame = BitConverter.ToInt32(buff, 160);
                info.NumRawFrames = BitConverter.ToInt32(buff, 164);
                data.Infos.Add(info);
            }
        }

        public void ReadAnimKeys(FileStream fs, ChunkHeader h)
        {
            data.Keys = new List<PSAAnimKeys>();
            
            for (int i = 0; i < h.count; i++)
            {
                PSAAnimKeys key = new PSAAnimKeys();
                byte[] buff = new byte[h.size];
                for (int j = 0; j < h.size; j++)
                    buff[j] = (byte)fs.ReadByte();
                key.raw = buff;
                key.location = new PSAPoint(buff, 0);
                key.rotation = new PSAQuad(buff, 12);
                key.time = BitConverter.ToSingle(buff, 28);
                data.Keys.Add(key);
            }
        }

        public ChunkHeader ReadHeader(FileStream fs)
        {
            ChunkHeader res = new ChunkHeader();
            res.name = "";
            for (int i = 0; i < 20; i++)
            {
                byte b = (byte)fs.ReadByte();
                if (b != 0)
                    res.name += (char)b;
            }
            byte[] buff = new byte[4];
            fs.Read(buff, 0, 4);
            res.flags = BitConverter.ToInt32(buff, 0);
            buff = new byte[4];
            fs.Read(buff, 0, 4);
            res.size = BitConverter.ToInt32(buff, 0);
            buff = new byte[4];
            fs.Read(buff, 0, 4);
            res.count = BitConverter.ToInt32(buff, 0);
            return res;
        }
    }
}
