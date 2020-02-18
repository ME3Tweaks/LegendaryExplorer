using Gammtek.Conduit.Extensions.IO;
using ME3Explorer.Unreal.Classes;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer.Soundplorer
{
    public class ISBankEntry
    {
        public string FileName { get; set; }
        public bool isPCM = false;
        public bool isOgg = false;
        public uint numberOfChannels = 0;
        public uint sampleRate = 0;
        public uint DataOffset;
        public uint HeaderOffset;
        public int CodecID = -1;
        internal int CodecID2 = -1;
        public byte[] DataAsStored { get; set; }

        public string DisplayString
        {
            get
            {
                string retstr = FileName + " - Data offset: 0x" + DataOffset.ToString("X8");
                var codec = getCodecStr();
                if (codec != null)
                {
                    retstr += " - Codec: " + codec;
                }
                return retstr;
            }
        }

        internal MemoryStream GetWaveStream()
        {
            //string outPath = Path.Combine(path, currentFileName);

            if (isOgg)
            {
                string basePath = System.IO.Path.GetTempPath() + "ME3EXP_SOUND_" + Guid.NewGuid().ToString() + ".ogg";
                File.WriteAllBytes(basePath, DataAsStored);
                MemoryStream waveStream = WwiseStream.ConvertOggToWave(basePath);
                return waveStream;
            }
            else
            if (isPCM) //research shows both ogg and pcm can be set... somehow
            {
                Debug.WriteLine("PCM FILE");
                return null;
            }
            else
            {
                switch (CodecID2)
                {
                    case 0x3F4CCCCD:
                        int headerSize = 52;
                        MemoryStream ms = new MemoryStream();
                        //WAVE HEADER
                        ms.WriteBytes(Encoding.ASCII.GetBytes("RIFF"));
                        ms.WriteInt32(headerSize - 8); //size - header is 52 bytes, - 8 for RIFF and this part. we will update this later though.
                        ms.WriteBytes(Encoding.ASCII.GetBytes("WAVE"));
                        ms.WriteBytes(Encoding.ASCII.GetBytes("fmt "));
                        ms.WriteUInt32(16); //Chunk size

                        ms.WriteUInt16(1); //Wave Format PCM
                        ms.WriteUInt16((ushort)numberOfChannels);

                        ms.WriteUInt32(sampleRate);
                        ms.WriteUInt32(sampleRate * numberOfChannels * 2); //originally is value / 8, but the input was 16 so this will always be * 2 //byterate

                        ms.WriteUInt16((ushort)(numberOfChannels * 2)); //BlockAlign (channels * bitrate/8, so 16/2 = 2) (2 bytes)
                        ms.WriteUInt16((ushort)(16)); //16 bits per sample 


                        ms.WriteBytes(Encoding.ASCII.GetBytes("data"));
                        long dataSizePosition = ms.Position;
                        ms.WriteUInt32(0); //data len = this will have to be updated later, i think
                        ms.Write(DataAsStored, 0, DataAsStored.Length);
                        //XboxADPCMDecoder decoder = new XboxADPCMDecoder(numberOfChannels);
                        /*                        MemoryStream xboxADPCMStream = new MemoryStream(DataAsStored);
                                                MemoryStream decodedStream = KoopsAudioDecoder.Decode(xboxADPCMStream);
                                                decodedStream.Position = 0;
                                                decodedStream.CopyTo(ms);

                                                File.WriteAllBytes(@"C:\users\public\xbox_decodeddata.wav", decodedStream.ToArray());
                                                */
                        //update sizes
                        ms.Seek(dataSizePosition, SeekOrigin.Begin);
                        ms.WriteUInt32((uint)DataAsStored.Length);

                        ms.Seek(4, SeekOrigin.Begin);
                        ms.WriteUInt32((uint)ms.Length - 8);
                        return ms;
                }
                return null;
            }


            /* OLD XBOX CODE (doesn't work for this game)
            int headerSize = 52;
            MemoryStream ms = new MemoryStream();
            //WAVE HEADER
            ms.WriteBytes(Encoding.ASCII.GetBytes("RIFF"));
            ms.WriteInt32(headerSize - 8); //size - header is 52 bytes, - 8 for RIFF and this part.
            ms.WriteBytes(Encoding.ASCII.GetBytes("WAVE"));
            ms.WriteBytes(Encoding.ASCII.GetBytes("fmt "));
            ms.WriteUInt32(16); //Chunk size

            ms.WriteUInt16(1); //Wave Format PCM
            ms.WriteUInt16((ushort)numberOfChannels);

            ms.WriteUInt32(sampleRate);
            ms.WriteUInt32(sampleRate * numberOfChannels * 2); //originally is value / 8, but the input was 16 so this will always be * 2 //byterate

            ms.WriteUInt16((ushort)(numberOfChannels * 2)); //BlockAlign (channels * bitrate/8, so 16/2 = 2) (2 bytes)
            ms.WriteUInt16((ushort)(16)); //16 bits per sample 


            ms.WriteBytes(Encoding.ASCII.GetBytes("data"));
            long dataSizePosition = ms.Position;
            ms.WriteUInt32(0); //data len = this will have to be updated later, i think

            //XboxADPCMDecoder decoder = new XboxADPCMDecoder(numberOfChannels);
            MemoryStream xboxADPCMStream = new MemoryStream(DataAsStored);
            MemoryStream decodedStream = KoopsAudioDecoder.Decode(xboxADPCMStream);
            decodedStream.Position = 0;
            decodedStream.CopyTo(ms);

            File.WriteAllBytes(@"C:\users\public\xbox_decodeddata.wav", decodedStream.ToArray());

            //update sizes
            ms.Seek(dataSizePosition, SeekOrigin.Begin);
            ms.WriteUInt32((uint)decodedStream.Length);

            ms.Seek(4, SeekOrigin.Begin);
            ms.WriteUInt32((uint)ms.Length - 8);
            decodedStream.Dispose();
            return ms;*/
        }

        internal string GetTextSummary()
        {
            string str = "";
            //Debug.WriteLine("Sound #" + currentCounter);
            str += FileName + "\n";
            str += "Sample Rate: " + sampleRate + "\n";
            str += "Channels: " + numberOfChannels + "\n";
            var codec = getCodecStr();
            if (codec != null)
            {
                str += $"Codec: {codec}\n";
            }
            str += "Has Data: " + (DataAsStored != null);
            return str;

        }

        private string getCodecStr()
        {
            switch (CodecID)
            {
                case -1: return null;
                case 0: return "PCM";
                case 1: return "Xbox IMA";
                case 2: return "Vorbis";
                case 5: return "Sony MSF container"; //only for PS3 files, but we'll just document it here anyways
                default: return $"Unknown codec ID ({CodecID})";
            }
        }

        public TimeSpan? GetLength()
        {
            if (!isOgg && !isPCM)
            {
                try
                {
                    MemoryStream ms = GetWaveStream();
                    ms.Position = 0;
                    WaveFileReader wf = new WaveFileReader(ms);
                    return wf.TotalTime;
                }
                catch
                {
                    return null;
                }
            }
            if (isOgg)
            {

                WaveFileReader wf = new WaveFileReader(GetWaveStream());
                return wf.TotalTime;
            }
            return new TimeSpan(0);
        }
    }
}
