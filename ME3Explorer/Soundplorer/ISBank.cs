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

                blocksize = ms.ReadUInt32(); //size of isfbtitl chunk
                ms.Seek(blocksize, SeekOrigin.Current); //skip it
                isbEntry = new ISBankEntry(); //start of a new file
                isbEntry.HeaderOffset = (uint)ms.Position;
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
                    isbEntry.HeaderOffset = (uint)ms.Position;
                    currentCounter = counter;
                }

                string blockName = ms.ReadString(4);
                //Debug.WriteLine(blockName + " at " + (ms.Position - 4).ToString("X8"));
                switch (blockName)
                {
                    case "LIST":
                        /*
                         *       AudioFile.Seek(4, sofromcurrent); //list block size
      AudioFile.Seek(8, sofromcurrent); //Seek past ''isbftitl'' bytes
      blocksize:=Audiofile.ReadDWord;

      TempString:='';
      //filename:='';
      for I := 0 to blocksize - 1 do
      begin
        TempString:=chr(Audiofile.ReadByte);
        if TempString=#0 then
        else
          //filename:=filename + TempString;
      end;
      continue;*/
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
                        //string str = ms.ReadString(blocksize);
                        isbEntry.FileName = tempStr;
                        break;
                    case "sinf":
                        /*    if blockname='sinf' then
    begin
      AudioFile.Seek(12, sofromcurrent);
      samplerate:=Audiofile.ReadDWord;
      AudioFile.Seek(8, sofromcurrent);
      continue;
    end;*/
                        ms.Seek(12, SeekOrigin.Current);
                        isbEntry.sampleRate = ms.ReadUInt32();
                        ms.Seek(8, SeekOrigin.Current);
                        break;
                    case "chnk":
                        /*
                         *     if blockname='chnk' then
    begin
      AudioFile.Seek(4, sofromcurrent);
      PCchannels:=Audiofile.ReadDWord;
      continue;
    end;*/
                        ms.Seek(4, SeekOrigin.Current);
                        isbEntry.numberOfChannels = ms.ReadUInt32();
                        break;
                    case "cmpi":
                        /*
    if blockname='cmpi' then
    begin
      AudioFile.Seek(24, sofromcurrent);
      if Audiofile.ReadDWord=1053609165 then
        IsPCM:=true
      else
        IsPCM:=false;
      continue;
    end;*/
                        ms.Seek(20, SeekOrigin.Current);
                        int pcmSignature1 = ms.ReadInt32();
                        int pcmSignature2 = ms.ReadInt32();

                        isbEntry.CodecID = pcmSignature1;
                        isbEntry.CodecID2 = pcmSignature2;

                        isbEntry.isPCM = pcmSignature2 == 1053609165;

                        break;
                    case "data":
                        counter++;

                        //Debug.WriteLine(counter + " Data for " + isbEntry.FileName);
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
                        //ms.Seek(blocksize, SeekOrigin.Current); //go to next block

                        /*
                         *     if blockname='data' then
    begin
      inc(Counter);
      blocksize:=Audiofile.ReadDWord;
      if blocksize mod 2 <> 0 then
        blocksize:=blocksize+1;

      if Audiofile.ReadBlockName='OggS' then
        IsOgg:=true
      else
        IsOgg:=false;

      AudioFile.Seek(-4, sofromcurrent);

      if Counter=FileNo then
      begin
        SavePcFile(filename, destdir, blocksize, IsOgg, IsPcm, False, PcChannels, SampleRate);
        application.processmessages;
        continue;
      end
      else
      begin
        Audiofile.Seek(blocksize, sofromcurrent);
        application.processmessages;
        continue;
      end;
    end;*/
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