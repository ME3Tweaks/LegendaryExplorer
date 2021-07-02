using NAudio.Wave;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ME3ExplorerCore.Gammtek.IO;
using WwiseStreamHelper = ME3Explorer.Unreal.WwiseStreamHelper;

namespace ME3Explorer.Soundplorer
{
    public class ISBankEntry : NotifyPropertyChangedBase
    {
        public string _tlkString;
        public string TLKString
        {
            get => _tlkString;
            set => SetProperty(ref _tlkString, value);
        }

        public string FileName { get; set; }
        public Endian FileEndianness { get; set; }
        public uint numberOfChannels = 0;
        public uint sampleRate = 0;
        public uint DataOffset;
        public int CodecID = -1;
        internal int CodecID2 = -1;
        public int bps;
        public int SmplTitlOffset;
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

            if (CodecID == 0x1 || CodecID == 0x4 || CodecID == 0x5)
            {
                //Xbox IMA, XMA, Sony MSF (PS3)
                //Use VGM Stream
                if (FullData == null)
                {
                    PopulateFakeFullData();
                }
                if (FullData != null)
                {
                    var tempPath = GetTempSoundPath() + ".isb";
                    File.WriteAllBytes(tempPath, FullData);
                    return ConvertAudioToWave(tempPath);
                }
            }
            if (CodecID == 0x2)
            {
                // Ogg Vorbis
                string basePath = System.IO.Path.GetTempPath() + "ME3EXP_SOUND_" + Guid.NewGuid().ToString() + ".ogg";
                File.WriteAllBytes(basePath, DataAsStored);
                MemoryStream waveStream = WwiseStreamHelper.ConvertOggToWave(basePath);
                return waveStream;
            }
            Debug.WriteLine("Unsupported codec for getting wave: " + CodecID);
            return null; //other codecs currently unsupported
        }

        /// <summary>
        /// Converts this entry to a standalone RIFF and stores it in the FullData variable. Used for preparing data to feed to VGM stream when this is a subsong in an ISB file
        /// The output of this is NOT a valid ISB file! Only enough to allow VGMStream to parse it.
        /// </summary>
        private void PopulateFakeFullData()
        {
            // This needs further testing. It doesn't seem to be correct for
            // Xenon platform (ME1)
            
            MemoryStream outStream = new MemoryStream();
            EndianWriter writer = new EndianWriter(outStream);
            writer.Endian = FileEndianness;
            writer.WriteStringASCII("RIFF");
            writer.Write(0); //Placeholder for length
            writer.WriteStringASCII("isbf"); //titl is actually a chunk
            writer.WriteStringASCII("LIST");
            var listsizepos = writer.BaseStream.Position;
            writer.Write(0); //list size placeholder
            writer.WriteStringASCII("samp"); //sample ahead

            writer.WriteStringASCII("chnk");
            writer.Write(4);
            writer.Write(numberOfChannels);

            writer.WriteStringASCII("chnk");
            writer.Write(10);
            writer.Write(sampleRate);
            writer.Write(pcmBytes);
            writer.Write(bps);

            writer.WriteStringASCII("cpmi");
            writer.Write(8);
            writer.Write(CodecID);
            writer.Write(CodecID2);

            writer.WriteStringASCII("data");
            writer.Write(DataAsStored.Length);
            writer.Write(DataAsStored);

            //Correct headers
            writer.BaseStream.Position = listsizepos;
            writer.Write((uint)writer.BaseStream.Length - (uint)listsizepos);

            writer.BaseStream.Position = 0x4;
            writer.Write((uint)writer.BaseStream.Length - 0x8);
            FullData = outStream.ToArray();
        }

        //TODO: Move this out of ISBankEntry as it's a generic raw RIFF -> WAV converter
        /// <summary>
        /// Converts a RAW RIFF/RIFX to WAVE using VGMStream and returns the data
        /// </summary>
        /// <param name="inputfilepath">Path to RIFF file</param>
        /// <returns></returns>
        public static MemoryStream ConvertAudioToWave(string inputfile)
        {
            //convert ISB Codec 1/4 to WAV
            MemoryStream outputData = new MemoryStream();

            // Todo: Link against VGMStream with a wrapper so we don't have to perform disk writes
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
            if (CodecID == 0x0)
            {
                //PCM
            }
            else if (CodecID == 0x1)
            {

            }
            else if (CodecID == 0x2)
            {
                //vorbis - based on VGMStream
                var samplecount = pcmBytes / numberOfChannels / (bps / 8);
                var seconds = (double)samplecount / sampleRate;
                return TimeSpan.FromSeconds(seconds);
            }
            else if (CodecID == 0x4)
            {
                //XMA
            }
            else if (CodecID == 0x5)
            {
                //Sony MSF (PS3 ME1)
                // Get actual samplerate (stored in audio container)
                //var datasize = EndianReader.ToUInt32(DataAsStored, 0x0C, FileEndianness);
                var actualSampleRate = EndianReader.ToUInt32(DataAsStored, 0x10, FileEndianness);


                var seconds = (double)pcmBytes / actualSampleRate / (bps / 8);
                //var seconds = (double)samplecount / actualSampleRate;
                return TimeSpan.FromSeconds(seconds);
            }
            return new TimeSpan(0);
        }
    }
}
