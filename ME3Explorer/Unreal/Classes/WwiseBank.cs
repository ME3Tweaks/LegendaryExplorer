using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ME3Explorer.Packages;
using System.Diagnostics;

namespace ME3Explorer.Unreal.Classes
{
    public class WwiseBank
    {
        public byte[] memory;
        public int memsize;
        public IExportEntry export;
        public int MyIndex;
        public IMEPackage pcc;
        public List<byte[]> Chunks;
        public List<byte[]> HIRCObjects;
        public int BinaryOffset;
        public byte[] didx_data;
        public byte[] data_data;

        public WwiseBank(IExportEntry export)
        {
            this.export = export;
            MyIndex = export.Index;
            pcc = export.FileRef;
            memory = export.Data;
            memsize = memory.Length;
            Deserialize();
        }

        public void Deserialize()
        {
            BinaryOffset = export.propsEnd() + (export.FileRef.Game == MEGame.ME2 ? 0x18 : 0x10);
            ReadChunks();
        }

        public void ReadChunks()
        {
            int pos = BinaryOffset;
            Chunks = new List<byte[]>();
            while (pos < memory.Length)
            {
                int start = pos;
                Debug.WriteLine("Reading chunk: " + GetID(memory, start));
                int size = BitConverter.ToInt32(memory, start + 4) + 8; //size of chunk is at +4, we add 8 as it includes header and size
                byte[] buff = new byte[size];
                Buffer.BlockCopy(memory, start, buff, 0, size);
                //                for (int i = 0; i < size; i++)
                //                  buff[i] = memory[start + i];
                Chunks.Add(buff);
                pos += size;
            }
        }

        /// <summary>
        /// Gets DIDX information for this soundbank. 
        /// </summary>
        /// <returns>List of Tuple of ID, Offset, Datasize</returns>
        public List<Tuple<uint, int, int>> GetWEMFilesMetadata()
        {
            var returnData = new List<Tuple<uint, int, int>>();
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
                        returnData.Add(new Tuple<uint, int, int>(wemID, offset, size));
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
                        int lendata = BitConverter.ToInt32(buff, 0x4);
                        res += "...Embedded WEM Files : " + lendata / 0xC + "\n";
                        for (int i = 0; i < lendata / 0xC; i++)
                        {
                            res += "......WEM(" + i + ") : ID (0x" + BitConverter.ToInt32(buff, 0x8 + i * 0xC).ToString("X8");
                            res += ") Start Offset(0x" + BitConverter.ToInt32(buff, 0xC + i * 0xC).ToString("X8");
                            res += ") Size(0x" + BitConverter.ToInt32(buff, 0x10 + i * 0xC).ToString("X8") + ")\n";
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

        public string GetHircObjType(byte b)
        {
            string res = "";
            switch (b)
            {
                case 0x2:
                    res = "*Sound SFX/Sound Voice";
                    break;
                case 0x3:
                    res = "*Event Action";
                    break;
                case 0x4:
                    res = "*Event";
                    break;
                case 0x5:
                    res = "*Random Container or Sequence Container";
                    break;
                case 0x7:
                    res = "*Actor-Mixer";
                    break;
                case 0xA:
                    res = "*Music Segment";
                    break;
                case 0xB:
                    res = "*Music Track";
                    break;
                case 0xC:
                    res = "*Music Switch Container";
                    break;
                case 0xD:
                    res = "*Music Playlist Container";
                    break;
                case 0xE:
                    res = "*Attenuation";
                    break;
                case 0x12:
                    res = "*Effect";
                    break;
                case 0x13:
                    res = "*Auxiliary Bus";
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
            res += GetHircObjType(buff[0]);
            return res;
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
                Console.WriteLine("QSH: " + size.ToString("X4"));
                int ID = BitConverter.ToInt32(buff, pos + 5);
                res += "Type = 0x" + type.ToString("X2") + " ID(" + ID.ToString("X8") + ") Size = 0x" + size.ToString("X8") + " " + GetHircObjType(type) + "\n";
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

        public byte[] RecreateBinary()
        {

            MemoryStream res = new MemoryStream();
            res.Write(memory, 0, BinaryOffset);
            int size = 0;
            byte[] tmp;
            foreach (byte[] buff in Chunks)
                switch (GetID(buff))
                {
                    case "HIRC":
                        tmp = RecreateHIRC(buff);
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

        public byte[] RecreateHIRC(byte[] buff)
        {
            MemoryStream res = new MemoryStream();
            res.Write(buff, 0, 0x8);
            res.Write(BitConverter.GetBytes(HIRCObjects.Count), 0, 4);
            int size = 4;
            int index = 0;
            foreach (byte[] obj in HIRCObjects)
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
                    int offset = (int)dataBlock.Position - 8; //remove DATA and size
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
            newExportData.Write(export.Data, 0, BinaryOffset); //all but binary data.
            newBankBinaryStream.CopyTo(newExportData);
            export.Data = newExportData.ToArray();
        }
    }
}
