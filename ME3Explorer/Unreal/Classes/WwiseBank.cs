using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ME3Explorer.Packages;
using System.Diagnostics;
using Gammtek.Conduit.IO;
using StreamHelpers;

namespace ME3Explorer.Unreal.Classes
{
    public class WwiseBank
    {
        public byte[] memory;
        public int memsize;
        public ExportEntry export;
        public int ExportIndex;
        public IMEPackage pcc;
        public List<byte[]> Chunks;
        public List<byte[]> HIRCObjects;
        public int BinaryOffset;
        public byte[] didx_data;
        public byte[] data_data;

        public WwiseBank(ExportEntry export)
        {
            this.export = export;
            ExportIndex = export.Index;
            pcc = export.FileRef;
            memory = export.Data;
            memsize = memory.Length;
            Deserialize();
        }

        public void Deserialize()
        {
            EndianReader reader = new EndianReader(new MemoryStream(export.Data)) { Endian = export.FileRef.Endian };
            BinaryOffset = export.propsEnd() + (export.FileRef.Game == MEGame.ME2 ? 0x18 : 0x10);
            ReadChunks(reader);
        }

        public void ReadChunks(EndianReader reader)
        {
            int pos = BinaryOffset;
            reader.Position = BinaryOffset;
            Chunks = new List<byte[]>();
            while (reader.Position < reader.Length)
            {
                int start = pos;
                var chunkname = reader.BaseStream.ReadStringASCII(4);
                Debug.WriteLine("Reading chunk: " + chunkname);
                int size = reader.ReadInt32() + 8; //size of chunk is at +4, we add 8 as it includes header and size
                byte[] buff = new byte[size];
                Buffer.BlockCopy(memory, start, buff, 0, size);
                //                for (int i = 0; i < size; i++)
                //                  buff[i] = memory[start + i];
                Chunks.Add(buff);
                reader.Skip(size - 8);
            }
        }

        /// <summary>
        /// Gets DIDX information for this soundbank. 
        /// </summary>
        /// <returns>List of Tuple of ID, Offset, Datasize</returns>
        public List<(uint, int, int)> GetWEMFilesMetadata()
        {
            var returnData = new List<(uint, int, int)>();
            foreach (byte[] buff in Chunks)
            {
                if (GetID(buff) == "DIDX")
                {
                    //metadata
                    int lendata = BitConverter.ToInt32(buff, 0x4);
                    for (int i = 0; i < lendata / 0xC; i++)
                    {
                        uint wemID = BitConverter.ToUInt32(buff, 0x8 + i * 0xC);
                        int offset = BitConverter.ToInt32(buff, 0xC + i * 0xC);
                        int size = BitConverter.ToInt32(buff, 0x10 + i * 0xC);
                        returnData.Add((wemID, offset, size));
                    }
                    break;
                }
            }
            return returnData;
        }

        public string GetQuickScan()
        {
            string res = "";
            foreach (byte[] buff in Chunks)
            {
                res += "Found Chunk, ID:" + GetID(buff) + " len= " + buff.Length + " bytes\n";
                switch (GetID(buff))
                {
                    case "BKHD":
                        res += "...Version : " + BitConverter.ToInt32(buff, 0x8) + "\n";
                        res += "...ID : 0x" + BitConverter.ToInt32(buff, 0xC).ToString("X8") + "\n";
                        break;
                    case "DIDX":
                        byte[] data = GetChunk("DATA");
                        int lendata = BitConverter.ToInt32(buff, 0x4);
                        res += "...Embedded WEM Files : " + lendata / 0xC + "\n";
                        int dataStartOffset = lendata + 8; //data and datasize
                        for (int i = 0; i < lendata / 0xC; i++)
                        {
                            int startoffset = BitConverter.ToInt32(buff, 0xC + i * 0xC);
                            res += "......WEM(" + i + ") : ID (0x" + BitConverter.ToInt32(buff, 0x8 + i * 0xC).ToString("X8");

                            res += ") Start Offset(0x" + startoffset.ToString("X8");
                            int size = BitConverter.ToInt32(buff, 0x10 + i * 0xC);
                            res += ") Size(0x" + size.ToString("X8") + ")";
                            res += " End Offset(0x" + (startoffset + size).ToString("X8") + ")\n";
                        }
                        didx_data = buff;
                        break;
                    case "HIRC":
                        res += QuickScanHirc(buff);
                        break;
                    case "DATA":
                        data_data = buff;
                        res += "...Embedded WEM Files Data found\n";
                        break;
                    case "STID":
                        res += "...String Count : " + BitConverter.ToInt32(buff, 0xC) + "\n";
                        int count = BitConverter.ToInt32(buff, 0xC);
                        int pos = 0x10;
                        for (int i = 0; i < count; i++)
                        {
                            int ID = BitConverter.ToInt32(buff, pos);
                            byte len = buff[pos + 4];
                            string s = "";
                            for (int j = 0; j < len; j++)
                                s += "" + (char)buff[pos + 5 + j];
                            res += "...String(" + i + ") : ID(0x" + ID.ToString("X8") + ") " + s + "\n";
                            pos += 5 + len;
                        }
                        break;
                    default: res += "...not supported\n"; break;
                }
            }
            return res;
        }

        /// <summary>
        /// Gets a chunk by name.
        /// </summary>
        /// <param name="name">4 character chunk header (BKHD, DATA, DIDX, HIRC, etc.</param>
        /// <returns>byte[] of this chunk, or null if not found</returns>
        internal byte[] GetChunk(string name)
        {
            foreach (byte[] buff in Chunks)
            {
                if (GetID(buff) == name)
                {
                    return buff;
                }
            }
            return null;
        }

        public static string GetHircObjType(byte b)
        {
            string res = "";
            switch (b)
            {
                case HIRCObject.TYPE_SOUNDSFXVOICE:
                    res = "Sound SFX/Sound Voice";
                    break;
                case HIRCObject.TYPE_EVENTACTION:
                    res = "Event Action";
                    break;
                case HIRCObject.TYPE_EVENT:
                    res = "Event";
                    break;
                case 0x5:
                    res = "Random Container or Sequence Container";
                    break;
                case 0x7:
                    res = "Actor-Mixer";
                    break;
                case 0xA:
                    res = "Music Segment";
                    break;
                case 0xB:
                    res = "Music Track";
                    break;
                case 0xC:
                    res = "Music Switch Container";
                    break;
                case 0xD:
                    res = "Music Playlist Container";
                    break;
                case 0xE:
                    res = "Attenuation";
                    break;
                case 0x12:
                    res = "Effect";
                    break;
                case 0x13:
                    res = "Auxiliary Bus";
                    break;
            }
            return res;
        }

        //public string[] ActionTypes = {"Stop", "Pause", "Resume", "Play", "Trigger", "Mute", "UnMute", "Set Voice Pitch", "Reset Voice Pitch", "Set Voice Volume", "Reset Voice Volume", "Set Bus Volume", "Reset Bus Volume", "Set Voice Low-pass Filter", "Reset Voice Low-pass Filter", "Enable State" , "Disable State", "Set State", "Set Game Parameter", "Reset Game Parameter", "Set Switch", "Enable Bypass or Disable Bypass", "Reset Bypass Effect", "Break", "Seek"};
        //public string[] EventScopes = { "Game object: Switch or Trigger", "Global", "Game object: by ID", "Game object: State", "All", "All Except ID" };

        public string GetHircDesc(byte[] buff)
        {
            string res = "";
            res += "ID(" + buff[0].ToString("X2") + ") Size = " + BitConverter.ToInt32(buff, 1).ToString("X8") + " ";
            res += "*" + GetHircObjType(buff[0]);
            return res;
        }

        public class HIRCObject : NotifyPropertyChangedBase
        {
            public const byte TYPE_SOUNDSFXVOICE = 0x2;
            public const byte TYPE_EVENTACTION = 0x3;
            public const byte TYPE_EVENT = 0x4;

            private int index;
            public int Index { get { return index; } set { index = value; } }

            private int offset;
            public int Offset { get { return offset; } set { offset = value; } }

            private byte objtype;
            public byte ObjType { get { return objtype; } set { objtype = value; } }

            private int size;
            public int Size { get { return size; } set { size = value; } }

            private int id;
            public int ID { get { return id; } set { id = value; } }


            private byte stype;
            public byte SoundType { get { return stype; } set { stype = value; } }

            private int state;
            public int State { get { return state; } set { state = value; } }

            //typeinfo
            public int cnt, unk1, IDaudio, IDsource;//scope,atype;
            public List<int> eventIDs { get; set; }

            private byte[] _data;
            public byte[] Data
            {
                get => _data;
                internal set
                {
                    if (_data != null && value != null && _data.SequenceEqual(value))
                    {
                        return; //if the data is the same don't write it and trigger the side effects
                    }

                    bool isFirstLoad = _data == null;
                    _data = value;
                    if (!isFirstLoad)
                    {
                        DataChanged = true;
                    }
                }
            }

            private bool _dataChanged;

            public bool DataChanged
            {
                get => _dataChanged;
                internal set => SetProperty(ref _dataChanged, value);
            }
        }

        public List<HIRCObject> ParseHIRCObjects(byte[] buff)
        {
            int count = BitConverter.ToInt32(buff, 0x8);
            List<HIRCObject> hircObjects = new List<HIRCObject>(count);

            //string res = "...Count = " + count + "\n";
            int pos = 0xC;
            HIRCObjects = new List<byte[]>();
            for (int i = 0; i < count; i++)
            {
                HIRCObject ho = new HIRCObject();
                ho.Index = i;
                ho.Offset = pos;
                ho.ObjType = buff[pos];
                //for each HIRC object
                //res += "......" + i.ToString("D4") + " : @0x" + pos.ToString("X8") + " ";
                //byte type = buff[pos];
                //int size = 
                ho.Size = BitConverter.ToInt32(buff, pos + 1);
                //Console.WriteLine("QSH: " + size.ToString("X4"));
                ho.ID = BitConverter.ToInt32(buff, pos + 5);
                //int ID = BitConverter.ToInt32(buff, pos + 5);
                //res += "Type = 0x" + type.ToString("X2") + " ID(" + ID.ToString("X8") + ") Size = 0x" + size.ToString("X8") + " " + GetHircObjType(type) + "\n";
                //int cnt, unk1, state, IDaudio, IDsource, stype;//scope,atype;
                switch (ho.ObjType)
                {
                    case 0x2:   //*Sound SFX/Sound Voice
                        ho.unk1 = BitConverter.ToInt32(buff, pos + 9);
                        ho.State = BitConverter.ToInt32(buff, pos + 13);
                        ho.IDaudio = BitConverter.ToInt32(buff, pos + 17);
                        ho.IDsource = BitConverter.ToInt32(buff, pos + 21);
                        ho.SoundType = buff[pos + 25];
                        /*res += ".........Unk1  = " + unk1.ToString("X8") + "\n";
                        res += ".........State = " + state.ToString("X8") + " (0=embed, 1=streamed, 2=stream/prefetched)\n";
                        res += ".........ID Audio  = " + IDaudio.ToString("X8") + "\n";
                        res += ".........ID Source = " + IDsource.ToString("X8") + "\n";
                        if (stype == 0)
                            res += ".........Sound Type = Sound SFX\n";
                        else
                            res += ".........Sound Type = Sound Voice\n";*/
                        break;
                    //case 0x3: //Event Action
                    //    scope = buff[pos + 9];
                    //    res += ".........Scope = " + scope + "\n";
                    //    atype = buff[pos + 10];
                    //    res += ".........Action Type = " + atype + "\n";
                    //    res += ".........ref ID = " + BitConverter.ToInt32(buff, pos + 11).ToString("X8") + "\n";
                    //    cnt = buff[pos + 16];
                    //    res += ".........Parameter Count = " + cnt + "\n";
                    //    break;
                    case 0x4:   //*Event
                        ho.cnt = BitConverter.ToInt32(buff, pos + 9);
                        ho.eventIDs = new List<int>();
                        //res += ".........Count = " + cnt + "\n";
                        for (int j = 0; j < ho.cnt; j++)
                            ho.eventIDs.Add(BitConverter.ToInt32(buff, pos + j * 4 + 13));
                        //res += ".........ID = 0x" + .ToString("X8") + "\n";
                        break;
                }
                byte[] hircObjMemory = new byte[ho.Size + 5]; //size + 1byte type
                Buffer.BlockCopy(buff, ho.Offset, hircObjMemory, 0, hircObjMemory.Count());
                ho.Data = hircObjMemory;
                //byte[] tmp = new byte[size + 5];

                //could be block copied
                /*for (int j = 0; j < size + 5; j++)
                    tmp[j] = buff[pos + j];
                HIRCObjects.Add(tmp);*/
                hircObjects.Add(ho);
                pos += 5 + ho.Size;
            }
            return hircObjects;
        }

        public string QuickScanHirc(byte[] buff)
        {
            int count = BitConverter.ToInt32(buff, 0x8);
            string res = "...Count = " + count + "\n";
            int pos = 0xC;
            HIRCObjects = new List<byte[]>();
            for (int i = 0; i < count; i++)
            {
                //for each HIRC object
                res += "......" + i.ToString("D4") + " : @0x" + pos.ToString("X8") + " ";
                byte type = buff[pos];
                int size = BitConverter.ToInt32(buff, pos + 1);
                //Console.WriteLine("QSH: " + size.ToString("X4"));
                int ID = BitConverter.ToInt32(buff, pos + 5);
                res += "Type = 0x" + type.ToString("X2") + " ID(" + ID.ToString("X8") + ") Size = 0x" + size.ToString("X8") + " " + "*" + GetHircObjType(type) + "\n";
                int cnt, unk1, state, IDaudio, IDsource, stype;//scope,atype;
                switch (type)
                {
                    case 0x2:   //*Sound SFX/Sound Voice
                        unk1 = BitConverter.ToInt32(buff, pos + 9);
                        state = BitConverter.ToInt32(buff, pos + 13);
                        IDaudio = BitConverter.ToInt32(buff, pos + 17);
                        IDsource = BitConverter.ToInt32(buff, pos + 21);
                        stype = buff[pos + 25];
                        res += ".........Unk1  = " + unk1.ToString("X8") + "\n";
                        res += ".........State = " + state.ToString("X8") + " (0=embed, 1=streamed, 2=stream/prefetched)\n";
                        res += ".........ID Audio  = " + IDaudio.ToString("X8") + "\n";
                        res += ".........ID Source = " + IDsource.ToString("X8") + "\n";
                        if (stype == 0)
                            res += ".........Sound Type = Sound SFX\n";
                        else
                            res += ".........Sound Type = Sound Voice\n";
                        break;
                    //case 0x3:
                    //    scope = buff[pos + 9];
                    //    res += ".........Scope = " + scope + "\n";
                    //    atype = buff[pos + 10];
                    //    res += ".........Action Type = " + atype + "\n";
                    //    res += ".........ref ID = " + BitConverter.ToInt32(buff, pos + 11).ToString("X8") + "\n";
                    //    cnt = buff[pos + 16];
                    //    res += ".........Parameter Count = " + cnt + "\n";
                    //    break;
                    case 0x4:   //*Event
                        cnt = BitConverter.ToInt32(buff, pos + 9);
                        res += ".........Count = " + cnt + "\n";
                        for (int j = 0; j < cnt; j++)
                            res += ".........ID = 0x" + BitConverter.ToInt32(buff, pos + j * 4 + 13).ToString("X8") + "\n";
                        break;
                }
                byte[] tmp = new byte[size + 5];

                //could be block copied
                for (int j = 0; j < size + 5; j++)
                    tmp[j] = buff[pos + j];
                HIRCObjects.Add(tmp);
                pos += 5 + size;
            }
            return res;
        }

        public bool ExportAllWEMFiles(string path)
        {
            if (data_data == null || didx_data == null || data_data.Length == 0 || didx_data.Length == 0)
                return false;
            int len = didx_data.Length - 8;
            int count = len / 0xC;
            for (int i = 0; i < count; i++)
            {
                int id = BitConverter.ToInt32(didx_data, 0x8 + i * 0xC);
                int start = BitConverter.ToInt32(didx_data, 0xC + i * 0xC) + 0x8;
                int size = BitConverter.ToInt32(didx_data, 0x10 + i * 0xC);
                FileStream fs = new FileStream(Path.Combine(path, i.ToString("d4") + "_" + id.ToString("X8") + ".wem"), FileMode.Create, FileAccess.Write);
                fs.Write(data_data, start, size);
                fs.Close();
            }
            return true;
        }

        public string GetID(byte[] buff, int offset = 0)
        {
            return "" + (char)buff[offset] + (char)buff[offset + 1] + (char)buff[offset + 2] + (char)buff[offset + 3];
        }

        public void CloneHIRCObject(int n)
        {
            if (HIRCObjects == null || n < 0 || n >= HIRCObjects.Count)
                return;
            byte[] tmp = new byte[HIRCObjects[n].Length];
            for (int i = 0; i < HIRCObjects[n].Length; i++)
                tmp[i] = HIRCObjects[n][i];
            HIRCObjects.Add(tmp);
        }

        public byte[] RecreateBinary(List<byte[]> hircs = null)
        {

            MemoryStream res = new MemoryStream();
            res.Write(memory, 0, BinaryOffset);
            int size = 0;
            byte[] tmp;
            foreach (byte[] buff in Chunks)
                switch (GetID(buff))
                {
                    case "HIRC":
                        tmp = RecreateHIRC(buff, hircs);
                        size += tmp.Length;
                        res.Write(tmp, 0, tmp.Length);
                        break;
                    default:
                        size += buff.Length;
                        res.Write(buff, 0, buff.Length);
                        break;
                }
            res.Seek(BinaryOffset - 0xC, 0);
            res.Write(BitConverter.GetBytes(size), 0, 4);
            res.Write(BitConverter.GetBytes(size), 0, 4);
            return res.ToArray();
        }

        public byte[] RecreateHIRC(byte[] buff, List<byte[]> HIRCOverrides)
        {
            List<byte[]> hircs = HIRCOverrides ?? HIRCObjects;
            MemoryStream res = new MemoryStream();
            res.Write(buff, 0, 0x8);
            res.Write(BitConverter.GetBytes(hircs.Count), 0, 4);
            int size = 4;
            int index = 0;
            foreach (byte[] obj in hircs)
            {
                res.Write(obj, 0, obj.Length);
                size += obj.Length;
                Console.WriteLine(index + " = " + obj.Length.ToString("X4"));
                index++;
            }
            Console.WriteLine(size);

            res.Seek(0x4, 0);
            res.Write(BitConverter.GetBytes(size), 0, 4);
            return res.ToArray();
        }

        /// <summary>
        /// Replaces a WEM file with another. Updates the loaded export's data.
        /// </summary>
        public void UpdateDataChunk(List<EmbeddedWEMFile> wemFiles)
        {
            MemoryStream newBankBinaryStream = new MemoryStream();
            //Write Bank Header Chunk
            byte[] header = GetChunk("BKHD");
            newBankBinaryStream.Write(header, 0, header.Length);

            if (wemFiles.Count > 0)
            {
                //DIDX Chunk
                MemoryStream didxBlock = new MemoryStream();
                didxBlock.Write(Encoding.ASCII.GetBytes("DIDX"), 0, 4);
                didxBlock.Write(BitConverter.GetBytes(wemFiles.Count * 12), 0, 4); //12 bytes per entry

                //DATA Chunk
                MemoryStream dataBlock = new MemoryStream();
                dataBlock.Write(Encoding.ASCII.GetBytes("DATA"), 0, 4);
                dataBlock.Write(BitConverter.GetBytes(0), 0, 4); //we will seek back here and write this after

                foreach (EmbeddedWEMFile wem in wemFiles)
                {
                    while ((dataBlock.Position - 8) % 16 != 0)
                    {
                        dataBlock.WriteByte(0); //byte align to 16
                    }
                    int offset = (int)dataBlock.Position - 8; //remove DATA and size. This is effectively start offset
                    byte[] dataToWrite = wem.HasBeenFixed ? wem.OriginalWemData : wem.WemData;
                    didxBlock.Write(BitConverter.GetBytes(wem.Id), 0, 4); //Write ID
                    didxBlock.Write(BitConverter.GetBytes(offset), 0, 4); //Write Offset
                    didxBlock.Write(BitConverter.GetBytes(dataToWrite.Length), 0, 4); //Write Size

                    dataBlock.Write(dataToWrite, 0, dataToWrite.Length);
                }
                int dataSize = (int)dataBlock.Position - 8; //header, size
                dataBlock.Position = 4;
                dataBlock.Write(BitConverter.GetBytes(dataSize), 0, 4);

                didxBlock.Position = 0;
                dataBlock.Position = 0;

                didxBlock.CopyTo(newBankBinaryStream);
                dataBlock.CopyTo(newBankBinaryStream);
            }

            //Write the remaining chunks.
            List<byte[]> remainingChunks = Chunks.Where(x => GetID(x) != "BKHD" && GetID(x) != "DIDX" && GetID(x) != "DATA").ToList();
            foreach (byte[] chunk in remainingChunks)
            {
                newBankBinaryStream.Write(chunk, 0, chunk.Length);
            }

            newBankBinaryStream.Position = 0;
            //byte[] datax = newBankBinaryStream.ToArray();
            //File.WriteAllBytes(@"C:\users\public\test.bnk", datax);
            MemoryStream newExportData = new MemoryStream();
            newExportData.Write(export.Data, 0, export.propsEnd()); //all but binary data.
            //WwiseBank header (pre bank)
            newExportData.Write(BitConverter.GetBytes(0), 0, 4);
            newExportData.Write(BitConverter.GetBytes((int)newBankBinaryStream.Length), 0, 4);
            newExportData.Write(BitConverter.GetBytes((int)newBankBinaryStream.Length), 0, 4);
            newExportData.Write(BitConverter.GetBytes((int)newExportData.Position + export.DataOffset + 4), 0, 4);
            newBankBinaryStream.CopyTo(newExportData);
            export.Data = newExportData.ToArray();
        }
    }
}
