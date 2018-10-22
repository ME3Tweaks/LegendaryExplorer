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
        public uint pcChannels = 0;
        public uint sampleRate = 0;
        public uint DataOffset;
        public uint HeaderOffset;
        private byte[] dataAsStored;
        public byte[] DataAsStored
        {
            get
            {
                return dataAsStored;
            }
            set
            {
                dataAsStored = value;
            }
        }

        public string DisplayString
        {
            get
            {
                return FileName + " Has Data: " + (DataAsStored != null);
            }
        }

        internal MemoryStream GetWaveStream(MemoryStream xboxADPCMStream = null)
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
                MemoryStream decodedStream = decoder.Decode(xboxADPCMStream, 0, (int)xboxADPCMStream.Length);
                decodedStream.CopyTo(ms);
                decodedStream.Dispose();
                return ms;
            }
        }

        internal string GetTextSummary()
        {
            string str = "";
            //Debug.WriteLine("Sound #" + currentCounter);
            str += FileName + "\n";
            str += "Sample Rate: " + sampleRate + "\n";
            str += "Channels: " + pcChannels + "\n";
            str += "Is Ogg: " + isOgg + "\n";
            str += "Is PCM: " + isPCM + "\n";
            str += "Has Data: " + (DataAsStored != null);
            return str;

        }

        public TimeSpan? GetLength()
        {
            if (!isOgg && !isPCM)
            {
                try
                {
                    MemoryStream ms = GetWaveStream(new MemoryStream(DataAsStored));
                    ms.Position = 0;
                    WaveFileReader wf = new WaveFileReader(ms);
                    return wf.TotalTime;
                } catch
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
