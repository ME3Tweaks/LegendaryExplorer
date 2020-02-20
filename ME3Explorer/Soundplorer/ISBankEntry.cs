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
        public uint numberOfChannels = 0;
        public uint sampleRate = 0;
        public uint DataOffset;
        public int CodecID = -1;
        internal int CodecID2 = -1;
        public int bps;
        public uint pcmBytes;
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

                retstr += $"\nSamplerate: {sampleRate} - Bits per sample: {bps}";
                return retstr;
            }
        }

        public byte[] FullData { get; set; }

        private static string GetTempSoundPath() => $"{Path.GetTempPath()}ME3EXP_SOUND_{Guid.NewGuid()}";

        internal MemoryStream GetWaveStream()
        {
            //string outPath = Path.Combine(path, currentFileName);
            if (CodecID == 0x0)
            {
                //PCM
                var ms = new MemoryStream(DataAsStored);
                var raw = new RawSourceWaveStream(ms, new WaveFormat((int)sampleRate, bps, (int)numberOfChannels));
                var waveStream = new MemoryStream();
                WaveFileWriter.WriteWavFileToStream(waveStream, raw);
                return waveStream;
            }

            if (CodecID == 0x1 || CodecID == 0x4)
            {
                //Xbox IMA, XMA
                //Use VGM Stream
                if (FullData != null)
                {
                    var tempPath = GetTempSoundPath() + ".isb";
                    File.WriteAllBytes(tempPath, FullData);
                    return ConvertXboxIMAXMAToWave(tempPath);
                }
            }
            if (CodecID == 0x2)
            {
                string basePath = System.IO.Path.GetTempPath() + "ME3EXP_SOUND_" + Guid.NewGuid().ToString() + ".ogg";
                File.WriteAllBytes(basePath, DataAsStored);
                MemoryStream waveStream = WwiseStream.ConvertOggToWave(basePath);
                return waveStream;
            }
            Debug.WriteLine("Unsupported codec for getting wave: " + CodecID);
            return null; //other codecs currently unsupported
        }

        /// <summary>
        /// Converts an ogg file to a wav file using oggdec
        /// </summary>
        /// <param name="oggPath">Path to ogg file</param>
        /// <returns></returns>
        private static MemoryStream ConvertXboxIMAXMAToWave(string inputfile)
        {
            //convert ISB Codec 1/4 to WAV
            MemoryStream outputData = new MemoryStream();

            ProcessStartInfo procStartInfo = new ProcessStartInfo(Path.Combine(App.ExecFolder, "vgmstream", "vgmstream.exe"), $"-P \"{inputfile}\"")
            {
                WorkingDirectory = Path.Combine(App.ExecFolder, "vgmstream"),
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            //procStartInfo.StandardOutputEncoding = Encoding.GetEncoding(850); //standard cmd-page
            Process proc = new Process
            {
                StartInfo = procStartInfo
            };

            // Set our event handler to asynchronously read the sort output.
            proc.Start();
            //proc.BeginOutputReadLine();
            var outputTask = Task.Run(() =>
            {
                proc.StandardOutput.BaseStream.CopyTo(outputData);
            });
            Task.WaitAll(outputTask);

            proc.WaitForExit();
            File.Delete(inputfile); //intermediate

            //Fix headers as they are not correct when output from oggdec over stdout - no idea what it is outputting.
            //outputData.Position = 0x4;
            //outputData.Write(BitConverter.GetBytes(((int)outputData.Length) - 0x8), 0, 4); //filesize
            //outputData.Position = 0x28;
            //outputData.Write(BitConverter.GetBytes(((int)outputData.Length) - 0x24), 0, 4); //datasize
            //outputData.Position = 0;
            return outputData;
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

        public string getCodecStr()
        {
            switch (CodecID)
            {
                case -1: return null;
                case 0: return $"{bps}-bit PCM";
                case 1: return "Xbox IMA";
                case 2: return "Vorbis";
                case 4: return "XMA";
                case 5: return "Sony MSF container"; //only for PS3 files, but we'll just document it here anyways
                default: return $"Unknown codec ID ({CodecID})";
            }
        }

        public TimeSpan? GetLength()
        {
            if (CodecID == 0x2)
            {
                //vorbis - based on VGMStream
                var samplecount = pcmBytes / numberOfChannels / (bps / 8);
                var seconds = (double)samplecount / sampleRate;
                return TimeSpan.FromSeconds(seconds);
            }
            //other codecs are not supported right now

            //todo: update this with algorithm that VGMStream uses so we don't have to even extract to determine it
            //if (!isOgg && !isPCM)
            //{
            //    try
            //    {
            //        MemoryStream ms = GetWaveStream();
            //        ms.Position = 0;
            //        WaveFileReader wf = new WaveFileReader(ms);
            //        return wf.TotalTime;
            //    }
            //    catch
            //    {
            //        return null;
            //    }
            //}
            //if (isOgg)
            //{

            //    WaveFileReader wf = new WaveFileReader(GetWaveStream());
            //    return wf.TotalTime;
            //}
            return new TimeSpan(0);
        }
    }
}
