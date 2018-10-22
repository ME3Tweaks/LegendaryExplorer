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
    class ISACT_Parser
    {
        public static void ReadFile(string file)
        {
            byte[] inMemoryFile = File.ReadAllBytes(file);
            MemoryStream ms = new MemoryStream(inMemoryFile);
            ReadStream(ms);
            ms.Close();
        }

        public static void ReadStream(MemoryStream ms)
        {
            /*
             * var
  IsOgg, IsPcm: boolean;
  PcChannels, samplerate, i, blocksize, counter: integer;
  BlockName, TempString: string;
begin
  IsPcm:=false;
  PcChannels:=0;
  SampleRate:=0;
  Counter:=-1;

  AudioFile.Position:=0;
  AudioFile.Seek(16, sofromcurrent);

  blocksize:=Audiofile.ReadDWord; //text block
  AudioFile.Seek(blocksize, sofromcurrent); //seek past*/

            bool isPCM = false;
            bool isOgg = false;
            uint pcChannels = 0;
            uint sampleRate = 0;
            int counter = 1;
            string currentFileName = "";
            //Skip RIFF, isbftitl
            long dataStartPosition = ms.Position;
            ms.Position += 16;
            uint blocksize = ms.ReadUInt32(); //size of isfbtitl chunk
            ms.Seek(blocksize, SeekOrigin.Current); //skip it

            int currentCounter = counter;
            bool endOfFile = false;
            //todo change to this
            //  while AudioFile.Position <> BundleReader.OffsetsArray[FileNo] + BundleReader.FileSizesArray[FileNo] do
            while (ms.Position < ms.Length && !endOfFile)
            {
                if (currentCounter != counter)
                {
                    Debug.WriteLine("Sound #" + currentCounter);
                    Debug.WriteLine(currentFileName);
                    Debug.WriteLine("Sample Rate: " + sampleRate);
                    Debug.WriteLine("Channels: " + pcChannels);
                    Debug.WriteLine("Is Ogg: " + isOgg);
                    Debug.WriteLine("Is PCM: " + isPCM);
                    currentCounter = counter;
                }
                string blockName = ms.ReadString(4);
                Debug.WriteLine(blockName + " at " + (ms.Position - 4).ToString("X8"));

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
                        for (int i = 0; i < blocksize / 2; i++)
                        {
                            short value = ms.ReadInt16();
                            if (value != 0)
                            {
                                tempStr += (char)value;
                            }
                        }
                        //string str = ms.ReadString(blocksize);
                        currentFileName = tempStr;

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
                        sampleRate = ms.ReadUInt32();
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
                        pcChannels = ms.ReadUInt32();
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
                        ms.Seek(24, SeekOrigin.Current);
                        int pcmSignature = ms.ReadInt32();
                        isPCM = pcmSignature == 1053609165;
                        break;
                    case "data":
                        counter++;
                        blocksize = ms.ReadUInt32(); //size of block
                        if (blocksize % 2 != 0) ms.Seek(1, SeekOrigin.Current); //byte align
                        string encodedType = ms.ReadString(4);
                        isOgg = encodedType == "OggS";
                        ms.Seek(-4, SeekOrigin.Current); //go to block start
                        MemoryStream data = new MemoryStream();
                        ms.CopyToEx(data, (int)blocksize);
                        SavePcFile(currentFileName, @"C:\users\public\isbout", blocksize, isOgg, isPCM, pcChannels, sampleRate, data);
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
                        //this is the start of a new file in the bank. This is only used in embedded data
                        endOfFile = true;
                        ms.Seek(-4, SeekOrigin.Current);
                        break;
                    default:
                        //skip the block
                        blocksize = ms.ReadUInt32(); //size of block
                        ms.Seek(blocksize, SeekOrigin.Current); //skip it
                        break;
                }
            }
            //while (fs.Position != )
            System.Diagnostics.Debug.WriteLine("Found " + counter + " files");
        }

        private static void SavePcFile(string currentFileName, string path, uint blocksize, bool isOgg, bool isPCM, uint pcChannels, uint sampleRate, MemoryStream inData)
        {
            string outPath = Path.Combine(path, currentFileName);
            if (isPCM)
            {

            }
            else if (isOgg)
            {
                outPath = Path.GetFileNameWithoutExtension(outPath) + ".ogg";
                outPath = Path.Combine(path, currentFileName);
                File.WriteAllBytes(outPath, inData.ToArray());
            }
            else
            {
                int headerSize = 52;
                MemoryStream ms = new MemoryStream();
                //WAVE HEADER
                ms.WriteBytes(Encoding.ASCII.GetBytes("RIFF"));
                ms.WriteInt32(headerSize - 8); //size - header is 52 bytes, - 8 for RIFF and this part.
                ms.WriteBytes(Encoding.ASCII.GetBytes("WAVE"));
                ms.WriteBytes(Encoding.ASCII.GetBytes("fmt "));
                ms.WriteInt32(16); //wavelen
                ms.WriteInt32(1); //Wave Format PCM
                ms.WriteUInt32(pcChannels);
                ms.WriteUInt32(sampleRate);
                ms.WriteUInt32(pcChannels * 2); //originally is value / 8, but the input was 16 so this will always be * 2
                ms.WriteUInt32(pcChannels * 2 * sampleRate);
                ms.WriteBytes(Encoding.ASCII.GetBytes("data"));
                ms.WriteUInt32(0); //data len = this will have to be updated later, i think

                XboxADPCMDecoder decoder = new XboxADPCMDecoder(pcChannels);
                MemoryStream decodedStream = decoder.Decode(inData, 0, (int)blocksize);
                decodedStream.CopyTo(ms);
                decodedStream.Dispose();
                File.WriteAllBytes(outPath, ms.ToArray());
            }
        }
        //From Psychonauts Explorer
        /*
         * procedure TPsychoAudioDumper.ParsePcFile(FileNo: integer; Destdir, FileName: string);



  while AudioFile.Position <> BundleReader.OffsetsArray[FileNo] + BundleReader.FileSizesArray[FileNo] do
  begin
    if assigned(FOnProgress) then FOnProgress(audiofile.Size, audiofile.Position);
    blockname:=Audiofile.ReadBlockName;










    blocksize:=Audiofile.ReadDWord;
    AudioFile.Seek(blocksize, sofromcurrent);
  end;

end;*/
    }
}
