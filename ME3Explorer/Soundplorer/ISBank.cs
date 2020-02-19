using Gammtek.Conduit.Extensions.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer.Soundplorer
{
    class ISBank
    {
        private string Filepath;
        public List<ISBankEntry> BankEntries = new List<ISBankEntry>();
        public ISBank(string isbPath)
        {
            Filepath = isbPath;
            ParseBank(new MemoryStream(File.ReadAllBytes(isbPath)), false);
        }

        public ISBank(byte[] binData, bool isEmbedded)
        {
            MemoryStream ms = new MemoryStream(binData);
            ParseBank(ms, isEmbedded);
        }

        private void ParseBank(MemoryStream ms, bool isEmbedded)
        {
            int counter = 1;
            //Skip RIFF, isbftitl
            long dataStartPosition = ms.Position;
            string shouldBeRiff = ms.ReadString(4, false);
            if (shouldBeRiff != "RIFF")
            {
                Debug.WriteLine("Not a RIFF!");
            }
            uint riffSize = ms.ReadUInt32();
            var riffType = ms.ReadString(8, false); //technically not type, its just how this file format works
            ISBankEntry isbEntry = null;
            uint blocksize = 0;
            int currentCounter = counter;
            bool endOfFile = false;

            if (isEmbedded && riffType != "isbftitl")
            {
                //its an icbftitl, which never has data.
                ms.Seek(riffSize - 8, SeekOrigin.Current); //skip it
            }
            else
            {
                //get full data
                var pos = ms.Position;
                ms.Position -= 8; //go back 8
                var fulldata = new byte[riffSize + 8];
                ms.Read(fulldata, 0, fulldata.Length);
                ms.Position = pos; //reset
                isbEntry = new ISBankEntry(); //start of a new file
                isbEntry.FullData = fulldata;

                blocksize = ms.ReadUInt32(); //size of isfbtitl chunk
                ms.Seek(blocksize, SeekOrigin.Current); //skip it
                
            }
            //todo change to this
            //  while AudioFile.Position <> BundleReader.OffsetsArray[FileNo] + BundleReader.FileSizesArray[FileNo] do
            while (ms.Position < ms.Length && !endOfFile)
            {
                blocksize = 0; //reset
                if (currentCounter != counter)
                {
                    //Debug.WriteLine("Sound #" + currentCounter);
                    //Debug.WriteLine(currentFileName);
                    //Debug.WriteLine("Sample Rate: " + sampleRate);
                    //Debug.WriteLine("Channels: " + pcChannels);
                    //Debug.WriteLine("Is Ogg: " + isOgg);
                    //Debug.WriteLine("Is PCM: " + isPCM);
                    if (isbEntry != null)
                    {
                        BankEntries.Add(isbEntry);
                    }
                    //Debug.WriteLine(isbEntry.GetTextSummary());
                    //Debug.WriteLine("=======================");
                    isbEntry = new ISBankEntry();
                    currentCounter = counter;
                }

                string blockName = ms.ReadString(4);
                //Debug.WriteLine(blockName + " at " + (ms.Position - 4).ToString("X8"));
                switch (blockName)
                {
                    case "LIST":
                        ms.Seek(4, SeekOrigin.Current); //list block size
                        ms.Seek(8, SeekOrigin.Current); //Seek past ''isbftitl'' bytes
                        blocksize = ms.ReadUInt32(); //size of block
                        string tempStr = ""; //we have to build it manually because of how they chose to store it in a weird non-ASCII/unicode way
                        bool endOfStr = false;
                        for (int i = 0; i < blocksize / 2; i++)
                        {
                            short value = ms.ReadInt16();
                            if (value != 0 && !endOfStr)
                            {
                                tempStr += (char)value;
                            }
                            else
                            {
                                //used to skip the rest of the block
                                endOfStr = true;
                            }
                        }
                        isbEntry.FileName = tempStr;
                        break;
                    case "sinf":
                        var chunksize = ms.ReadInt32();
                        var pos = ms.Position;
                        ms.ReadInt64(); //skip 8
                        isbEntry.sampleRate = ms.ReadUInt32();
                        isbEntry.pcmBytes = ms.ReadUInt32();
                        isbEntry.bps = ms.ReadInt16();
                        ms.Position = pos + chunksize; //skip to next chunk
                        break;
                    case "chnk":
                        ms.Seek(4, SeekOrigin.Current);
                        isbEntry.numberOfChannels = ms.ReadUInt32();
                        break;
                    case "cmpi":
                        //Codec/compression index
                        var size = ms.ReadInt32();
                        pos = ms.Position;
                        isbEntry.CodecID = ms.ReadInt32();
                        isbEntry.CodecID2 = ms.ReadInt32();
                        ms.Position = pos + size;
                        break;
                    case "data":
                        counter++;
                        blocksize = ms.ReadUInt32(); //size of block

                        string encodedType = ms.ReadString(4, false);
                        isbEntry.isOgg = encodedType == "OggS";
                        ms.Seek(-4, SeekOrigin.Current); //go to block start
                        isbEntry.DataOffset = (uint)ms.Position;

                        MemoryStream data = new MemoryStream();
                        ms.CopyToEx(data, (int)blocksize);
                        data.Position = 0;
                        var str = data.ReadString(4, false);
                        isbEntry.DataAsStored = data.ToArray();
                        break;
                    case "RIFF":
                        //this is the start of a new file.
                        riffSize = ms.ReadUInt32(); //size of isfbtitl chunk

                        counter++;
                        riffType = ms.ReadString(8, false); //technically not type, its just how this file format works
                        if (riffType != "isbftitl")
                        {
                            //its an icbftitl, which never has data.
                            ms.Seek(riffSize - 8, SeekOrigin.Current); //skip it
                        }

                        blocksize = ms.ReadUInt32(); //size of isfbtitl chunk
                        ms.Seek(blocksize, SeekOrigin.Current); //skip it
                        break;
                    default:
                        //skip the block
                        blocksize = ms.ReadUInt32(); //size of block
                        //Debug.WriteLine("Skipping block of size " + blocksize + " at 0x" + ms.Position.ToString("X5"));
                        ms.Seek(blocksize, SeekOrigin.Current); //skip it
                        break;
                }
                if (blocksize % 2 != 0)
                {
                    ms.Seek(1, SeekOrigin.Current); //byte align
                }
            }
            if (isbEntry != null && isbEntry.DataAsStored != null)
            {
                BankEntries.Add(isbEntry);
            }
            isbEntry = new ISBankEntry();
        }
    }
}