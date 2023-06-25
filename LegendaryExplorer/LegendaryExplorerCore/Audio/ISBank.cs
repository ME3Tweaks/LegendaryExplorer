using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorerCore.Audio
{
    [Obsolete("Use ISACTBankPair instead")]
    public class ISBank_DEPRECATED
    {
        private int TestISBOffset;
        private string Filepath;
        public List<ISBank_DEPRECATED> BankEntries = new List<ISBank_DEPRECATED>();
        public ISBank_DEPRECATED(string isbPath)
        {
            Filepath = isbPath;
            ParseBank(new EndianReader(new MemoryStream(File.ReadAllBytes(isbPath))), false);
        }

        public ISBank_DEPRECATED(byte[] binData)
        {
            MemoryStream ms = new MemoryStream(binData);
            TestISBOffset = ms.ReadInt32();
            ParseBank(new EndianReader(ms), true);
        }

        private void ParseBank(EndianReader ms, bool isEmbedded)
        {
            /*
            int numEntriesWithData = 1;
            //long dataStartPosition = ms.Position;
            //string shouldBeRiff = ms.ReadString(4, false);
            //if (shouldBeRiff != "RIFF")
            //{
            //    Debug.WriteLine("Not a RIFF!");
            //}
            //uint riffSize = ms.ReadUInt32();
            //var riffType = ms.ReadString(8, false); //technically not type, its just how this file format works
            //ISBankEntry isbEntry = null;
            //uint blocksize = 0;
            //int currentCounter = counter;
            //bool endOfFile = false;

            //if (isEmbedded && riffType != "isbftitl")
            //{
            //    //its an icbftitl, which never has data.
            //    ms.Seek(riffSize - 8, SeekOrigin.Current); //skip it
            //}
            //else
            //{
            //    //get full data
            //    var pos = ms.Position;
            //    ms.Position -= 8; //go back 8
            //    var fulldata = new byte[riffSize + 8];
            //    ms.Read(fulldata, 0, fulldata.Length);
            //    ms.Position = pos; //reset
            //    isbEntry = new ISBankEntry(); //start of a new file
            //    isbEntry.FullData = fulldata;

            //    blocksize = ms.ReadUInt32(); //size of isfbtitl chunk
            //    ms.Seek(blocksize, SeekOrigin.Current); //skip it
            //}
            ////todo change to this
            ////  while AudioFile.Position <> BundleReader.OffsetsArray[FileNo] + BundleReader.FileSizesArray[FileNo] do
            uint chunksize = 0;
            ISBank_DEPRECATED isbEntry = null;
            while (ms.BaseStream.Position < ms.BaseStream.Length)
            {
                chunksize = 0; //reset
                var chunkStartPos = ms.BaseStream.Position;
                string blockName = ms.ReadEndianASCIIString(4);
                //Debug.WriteLine($"{(ms.Position - 4).ToString("X8")}: {blockName}");
                switch (blockName)
                {
                    case "LIST":
                        chunksize = ms.ReadUInt32();
                        var dataType = ms.ReadEndianASCIIString(4);
                        var nextblockname2 = ms.ReadEndianASCIIString(4);
                        if (dataType == "fldr")
                        {
                            // Folder for organization. We are just going to skip this. Might parse in future if it's useful
                            ms.Seek(-4, SeekOrigin.Current); // Seek back since there will be a subheader here.
                            continue;
                        }

                        if (dataType == "samp" && nextblockname2 == "stat") //stat LE1 music_bank.isb
                        {
                            ms.Skip(ms.ReadInt32()); // Skip Stat
                            nextblockname2 = ms.ReadEndianASCIIString(4);
                        }

                        if (dataType == "samp" && nextblockname2 == "titl") //stat LE1 music_bank.isb
                        {
                            if (!isEmbedded)
                            {
                                //upcoming sample data
                                //add old ISB entry
                                if (isbEntry?.DataAsStored != null)
                                {
                                    BankEntries.Add(isbEntry);
                                }

                                isbEntry = new ISBank_DEPRECATED();
                            }
                        }
                        else
                        {
                            //maybe isb container, ignore
                            ms.BaseStream.Position = chunksize + 8 + chunkStartPos;
                            //Debug.WriteLine($"Skipping non-sample LIST at 0x{chunkStartPos:X8}");
                            continue;
                        }

                        chunksize = ms.ReadUInt32(); //size of block
                        string tempStr = ""; //we have to build it manually because of how they chose to store it in a weird non-ASCII/unicode way
                        bool endOfStr = false;
                        for (int i = 0; i < chunksize / 2; i++)
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
                        chunksize = ms.ReadUInt32();
                        var pos = ms.BaseStream.Position;
                        ms.ReadInt64(); //skip 8
                        isbEntry.sampleRate = ms.ReadUInt32(); // This is actually codec dependent. e.g. this can list 48K but actual samplerate is 44.1K
                        isbEntry.pcmBytes = ms.ReadUInt32();
                        isbEntry.bps = ms.ReadInt16();
                        ms.BaseStream.Position = pos + chunksize; //skip to next chunk
                        break;
                    case "chnk":
                        ms.Seek(4, SeekOrigin.Current);
                        isbEntry.numberOfChannels = ms.ReadUInt32();
                        break;
                    case "cmpi":
                        //Codec/compression index
                        var size = ms.ReadInt32();
                        pos = ms.BaseStream.Position;
                        isbEntry.CodecID = ms.ReadInt32();
                        isbEntry.CodecID2 = ms.ReadInt32();
                        ms.BaseStream.Position = pos + size;
                        break;
                    case "data":
                        numEntriesWithData++;
                        chunksize = ms.ReadUInt32(); //size of block
                        isbEntry.DataOffset = (uint)ms.BaseStream.Position;
                        MemoryStream data = new MemoryStream();
                        ms.BaseStream.CopyToEx(data, (int)chunksize);
                        data.Position = 0;
                        var str = data.ReadStringLatin1(4);
                        isbEntry.DataAsStored = data.ToArray();
                        break;
                    case "FFIR":
                    case "RIFF":
                        if (blockName == "FFIR")
                        {
                            ms.Endian = Endian.Big;
                        }
                        if (isEmbedded)
                        {
                            //EMBEDDED ISB
                            //this is the start of a new file.
                            var riffSize = ms.ReadUInt32(); //size of isfbtitl chunk
                            var riffType = ms.ReadEndianASCIIString(4); //type of ISB riff
                            var riffType2 = ms.ReadEndianASCIIString(4); //type of ISB riff

                            //Debug.WriteLine($"Riff type is {riffType}");

                            if (riffType != "isbf" && riffType2 == "titl")
                            {
                                //its an icbftitl, which never has data.
                                //Debug.WriteLine($"Skipping non isbf type");
                                ms.Seek(TestISBOffset, SeekOrigin.Begin);
                                //ms.Seek(riffSize - 8, SeekOrigin.Current); //skip it
                                continue; //skip
                            }

                            //add old ISB entry
                            if (isbEntry?.DataAsStored != null)
                            {
                                BankEntries.Add(isbEntry);
                            }

                            isbEntry = new ISBankEntry
                            {
                                FileEndianness = ms.Endian,
                                FullData = new byte[riffSize + 8]
                            };
                            pos = ms.BaseStream.Position;
                            ms.BaseStream.Position = ms.BaseStream.Position - 16;
                            ms.Read(isbEntry.FullData, 0, (int)riffSize + 8);
                            ms.BaseStream.Position = pos;
                            chunksize = ms.ReadUInt32(); //size of isfbtitl chunk
                            ms.Seek(chunksize, SeekOrigin.Current); //skip it
                        }
                        else
                        {
                            //ISB file - has external RIFF header and samptitl's separating each data section
                            var riffSize = ms.ReadUInt32(); //size of isfbtitl chunk
                            var riffType = ms.ReadEndianASCIIString(4); //type of ISB riff
                            var riffType2 = ms.ReadEndianASCIIString(4); //type of ISB riff
                            if (riffType != "isbf" && riffType2 != "titl" && riffType2 != "stat") // stat: LE1 music_bank.isb
                            {
                                //its an icbftitl, which never has data, or is not ISB
                                ms.Seek(riffSize - 8, SeekOrigin.Current); //skip it
                                continue; //skip
                            }

                            //can't do += here it seems
                            ms.Seek(ms.ReadInt32(), SeekOrigin.Current); //skip this title section

                            //we will continue to parse through ISB header until we find a LIST object for sample data
                        }

                        break;
                    default:
                        //skip the block
                        chunksize = ms.ReadUInt32(); //size of block
                        ///sDebug.WriteLine($"Skipping block {blockName} of size {chunksize} at 0x{ms.Position:X8}");
                        ms.Seek(chunksize, SeekOrigin.Current); //skip it
                        break;
                }
                if (chunksize % 2 != 0)
                {
                    ms.Seek(1, SeekOrigin.Current); //byte align
                }
            }
            if (isbEntry?.DataAsStored != null)
            {
                BankEntries.Add(isbEntry);
            }*/
        }
    }
}