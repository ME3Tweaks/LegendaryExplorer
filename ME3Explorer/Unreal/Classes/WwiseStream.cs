using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Media;
using ME3Explorer.Unreal;
using ME3Explorer.Packages;
using NAudio.Wave;
using System.Diagnostics;
using System.Threading.Tasks;
using Gammtek.Conduit.IO;
using ME3Explorer.Soundplorer;
using StreamHelpers;

namespace ME3Explorer.Unreal.Classes
{
    public class WwiseStream
    {
        public byte[] memory;

        public int DataSize;
        public int DataOffset;
        public int ValueOffset;
        public int Id;
        public string FileName;
        ExportEntry export;

        public bool IsPCCStored => FileName == null;

        public WwiseStream()
        {
        }

        public WwiseStream(ExportEntry export)
        {
            this.export = export;
            memory = export.Data;
            Deserialize(export.FileRef);
        }

        public void Deserialize(IMEPackage pcc)
        {
            PropertyCollection properties = export.GetProperties();
            int off;
            switch (pcc.Game)
            {
                case MEGame.ME3:
                    off = export.propsEnd() + 8;
                    break;
                case MEGame.ME2:
                    off = export.propsEnd() + 0x28;
                    break;
                default:
                    throw new Exception("Can oly read WwiseStreams for ME3 and ME2!");
            }
            ValueOffset = off;
            DataSize = EndianReader.ToInt32(memory, off, pcc.Endian);
            DataOffset = EndianReader.ToInt32(memory, off + 4, pcc.Endian);
            NameProperty nameProp = properties.GetProp<NameProperty>("Filename");
            FileName = nameProp?.Value;
            Id = properties.GetProp<IntProperty>("Id");
        }

        /// <summary>
        /// This method is deprecated and will be removed eventually
        /// </summary>
        /// <param name="path"></param>
        /// <param name="pathtoafc"></param>
        public void ImportFromFile(string path, string pathtoafc = "")
        {
            if (FileName == "")
                return;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                if (pathtoafc != "")
                {
                    if (File.Exists(pathtoafc))
                        ImportWwiseOgg(pathtoafc, stream);
                    else if (File.Exists(pathtoafc + FileName + ".afc")) //legacy code for old soundplorer
                        ImportWwiseOgg(pathtoafc + FileName + ".afc", stream);
                    else
                    {
                        OpenFileDialog d = new OpenFileDialog();
                        d.Filter = FileName + ".afc|" + FileName + ".afc";
                        if (d.ShowDialog() == DialogResult.OK)
                            ImportWwiseOgg(d.FileName, stream);
                    }
                }
                else
                {
                    OpenFileDialog d = new OpenFileDialog();
                    d.Filter = FileName + ".afc|" + FileName + ".afc";
                    if (d.ShowDialog() == DialogResult.OK)
                        ImportWwiseOgg(d.FileName, stream);
                }
            }
        }

        public TimeSpan? GetSoundLength()
        {
            string path;
            if (IsPCCStored)
            {
                path = export.FileRef.FilePath; //we must load it decompressed.
            }
            else
            {
                path = GetPathToAFC();
            }

            var rawRiff = ExtractRawFromSource(path, DataSize, DataOffset);
            //Parse RIFF header a bit
            rawRiff.ReadInt32(); //RIFF
            rawRiff.ReadInt32(); //size
            rawRiff.ReadInt32(); //WAVE
            rawRiff.ReadInt32(); //'fmt '
            var fmtsize = rawRiff.ReadInt32();
            var fmtPos = rawRiff.Position;
            var riffFormat = rawRiff.ReadUInt16();
            var unk = rawRiff.ReadInt16(); //lookup in vgmstream
            var sampleRate = rawRiff.ReadInt32();
            var averageBPS = rawRiff.ReadInt32();
            var blockAlign = rawRiff.ReadInt16();
            var bitsPerSample = rawRiff.ReadInt16();
            var extraSize = rawRiff.ReadInt16();
            long seconds = 0;
            if (riffFormat == 0xFFFF)
            {
                //Vorbis

                if (extraSize == 0x30)
                {
                    //find 'vorb' chunk (ME2)
                    rawRiff.Seek(extraSize, SeekOrigin.Current);
                    var chunkName = rawRiff.ReadStringASCII(4);
                    uint numSamples = 1; //to prevent division by 0
                    if (chunkName == "vorb")
                    {
                        //ME2 Vorbis
                        var vorbsize = rawRiff.ReadInt32();
                        numSamples = rawRiff.ReadUInt32();
                    }
                    else if (chunkName == "data")
                    {
                        //ME3 Vorbis
                        var numSamplesOffset = rawRiff.Position = fmtPos + 0x18;
                        numSamples = rawRiff.ReadUInt32();
                    }

                    seconds = (long) ((double) numSamples / sampleRate);
                }
            }
            return new TimeSpan(0,0,0,(int) seconds);

            Stream waveStream = CreateWaveStream(path);
            if (waveStream != null)
            {
                //Check it is RIFF
                byte[] riffHeaderBytes = new byte[4];
                waveStream.Read(riffHeaderBytes, 0, 4);
                string wemHeader = "" + (char)riffHeaderBytes[0] + (char)riffHeaderBytes[1] + (char)riffHeaderBytes[2] + (char)riffHeaderBytes[3];
                if (wemHeader == "RIFF")
                {
                    waveStream.Position = 0;
                    WaveFileReader wf = new WaveFileReader(waveStream);
                    return wf.TotalTime;
                }
            }
            return null;
        }

        public string GetPathToAFC()
        {
            //Check if pcc-stored
            if (FileName == null)
            {
                return null; //it's pcc stored. we will return null for this case since we already coded for "".
            }

            //Look in currect directory first


            string path = Path.Combine(Path.GetDirectoryName(export.FileRef.FilePath), FileName + ".afc");
            if (File.Exists(path))
            {
                return path; //in current directory of this pcc file
            }

            var gameFiles = MELoadedFiles.GetFilesLoadedInGame(export.FileRef.Game, includeAFCs: true);
            gameFiles.TryGetValue(FileName + ".afc", out string afcPath);
            return afcPath ?? "";
        }

        /// <summary>
        /// Creates wav file in temp directory
        /// </summary>
        /// <param name="afcPath"></param>
        /// <returns></returns>
        public string CreateWave(string afcPath)
        {
            string basePath = GetTempSoundPath();
            if (ExtractRawFromSourceToFile(basePath + ".wem", GetPathToAFC(), DataSize, DataOffset))
            {
                var dataStream = ISBankEntry.ConvertAudioToWave(basePath + ".wem");
                //MemoryStream dataStream = ConvertRiffToWav(basePath + ".dat", export.FileRef.Game == MEGame.ME2);
                File.WriteAllBytes(basePath + ".wav", dataStream.ToArray());
            }
            return basePath + ".wav";
        }

        public static bool ExtractRawFromSourceToFile(string outfile, string afcPath, int dataSize, int dataOffset)
        {
            var ms = ExtractRawFromSource(afcPath, dataSize, dataOffset);
            if (File.Exists(outfile)) File.Delete(outfile);
            ms.WriteToFile(outfile);
            return true;
        }

        /// <summary>
        /// Creates wav stream from this WwiseStream
        /// </summary>
        /// <param name="afcPath"></param>
        /// <returns></returns>
        public Stream CreateWaveStream(string afcPath)
        {
            string basePath = GetTempSoundPath();
            if (ExtractRawFromSourceToFile(basePath + ".wem", afcPath, DataSize, DataOffset))
            {
                return ISBankEntry.ConvertAudioToWave(basePath + ".wem");
                //return ConvertRiffToWav(basePath + ".wem", export.FileRef.Game == MEGame.ME2);
            }
            return null;
        }

        /// <summary>
        /// Creates wav stream from this WwiseStream
        /// </summary>
        /// <param name="afcPath"></param>
        /// <returns></returns>
        public static Stream CreateWaveStreamFromRaw(string afcPath, int offset, int datasize, bool ME2)
        {
            string basePath = GetTempSoundPath();
            if (ExtractRawFromSourceToFile(basePath + ".wem", afcPath, datasize, offset))
            {
                return ISBankEntry.ConvertAudioToWave(basePath + ".wem");
            }
            return null;
        }

        private static string GetTempSoundPath() => $"{Path.GetTempPath()}ME3EXP_SOUND_{Guid.NewGuid()}";

        /// <summary>
        /// Converts a RAW RIFF from game data to a playable WAV stream. This can be written to disk as a playable WAV file.
        /// </summary>
        /// <param name="riffPath">Path to RIFF RAW data</param>
        /// <param name="fullSetup">Full setup flag - use for ME2</param>
        public static MemoryStream ConvertRiffToWav(string riffPath, bool fullSetup)
        {
            Stream oggStream = ConvertRIFFToWWwiseOGG(riffPath, fullSetup);
            if (oggStream != null)// && File.Exists(outputOggPath))
            {
                oggStream.Seek(0, SeekOrigin.Begin);
                string oggPath = Path.Combine(Directory.GetParent(riffPath).FullName, Path.GetFileNameWithoutExtension(riffPath)) + ".ogg";

                using (FileStream fs = new FileStream(oggPath, FileMode.OpenOrCreate))
                {
                    oggStream.CopyTo(fs);
                    fs.Flush();
                }
                File.Delete(riffPath); //raw
                return ConvertOggToWave(oggPath);
            }
            return null;
        }

        /// <summary>
        /// Converts an ogg file to a wav file using oggdec
        /// </summary>
        /// <param name="oggPath">Path to ogg file</param>
        /// <returns></returns>
        public static MemoryStream ConvertOggToWave(string oggPath)
        {
            //convert OGG to WAV
            MemoryStream outputData = new MemoryStream();

            ProcessStartInfo procStartInfo = new ProcessStartInfo(Path.Combine(App.ExecFolder, "oggdec.exe"), $"--stdout \"{oggPath}\"")
            {
                WorkingDirectory = App.ExecFolder,
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

        /*using (var output = new FileStream(outputFile, FileMode.Create))
        {
            process.StandardOutput.BaseStream.CopyTo(output);
        }*/
            });
            Task.WaitAll(outputTask);

            proc.WaitForExit();
            File.Delete(oggPath); //intermediate

            //Fix headers as they are not correct when output from oggdec over stdout - no idea what it is outputting.
            outputData.Position = 0x4;
            outputData.Write(BitConverter.GetBytes(((int)outputData.Length) - 0x8), 0, 4); //filesize
            outputData.Position = 0x28;
            outputData.Write(BitConverter.GetBytes(((int)outputData.Length) - 0x24), 0, 4); //datasize
            outputData.Position = 0;
            return outputData;
        }

        /// <summary>
        /// Converts a RAW RIFF from game data to a Wwise-based Ogg Vorbis stream
        /// </summary>
        /// <param name="riffPath">Path to RIFF RAW data</param>
        /// <param name="fullSetup">Full setup flag - use for ME2</param>
        public static MemoryStream ConvertRIFFToWWwiseOGG(string riffPath, bool fullSetup)
        {
            //convert RIFF to WwiseOGG
            //System.Diagnostics.Debug.WriteLine("ww2ogg: " + riffPath);
            if (!File.Exists(riffPath))
            {
                Debug.WriteLine("Error: input file does not exist");
            }

            ProcessStartInfo procStartInfo = null;
            if (!fullSetup)
            {
                procStartInfo = new ProcessStartInfo(Path.Combine(App.ExecFolder, "ww2ogg.exe"), "--stdout \"" + riffPath + "\"");
            }
            else
            {
                procStartInfo = new ProcessStartInfo(Path.Combine(App.ExecFolder, "ww2ogg.exe"), "--stdout --full-setup \"" + riffPath + "\"");
            }
            procStartInfo.WorkingDirectory = App.ExecFolder;
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.RedirectStandardError = true;

            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            Process proc = new Process { StartInfo = procStartInfo };
            proc.Start();

            MemoryStream outputData = new MemoryStream();
            MemoryStream outputErrorData = new MemoryStream();
            var outputTask = Task.Run(() =>
            {
                proc.StandardOutput.BaseStream.CopyTo(outputData);
                proc.StandardError.BaseStream.CopyTo(outputErrorData);

        /*using (var output = new FileStream(outputFile, FileMode.Create))
        {
            process.StandardOutput.BaseStream.CopyTo(output);
        }*/
            });
            Task.WaitAll(outputTask);

            proc.WaitForExit();
            proc.Close();
            //File.WriteAllBytes(System.IO.Path.Combine(loc, "testingoggerr.txt"), outputErrorData.ToArray());

            //Debug.WriteLine("Done");
            return outputData;
            //            return Path.Combine(Directory.GetParent(riffPath).FullName, Path.GetFileNameWithoutExtension(riffPath)) + ".ogg";
        }

        public bool ExtractRawFromSourceToFile(string outputFile, string afcPath)
        {
            return ExtractRawFromSourceToFile(outputFile, afcPath, DataSize, DataOffset);
        }

        public static MemoryStream ExtractRawFromSource(string afcPath, int DataSize, int DataOffset)
        {
            if (!File.Exists(afcPath))
                return null;

            Stream embeddedStream = null;
            if (afcPath.EndsWith(".pcc"))
            {
                using (IMEPackage package = MEPackageHandler.OpenMEPackage(afcPath))
                {
                    if (package.IsCompressed)
                    {
                        embeddedStream = CompressionHelper.Decompress(afcPath);
                    }
                }
            }

            using (Stream fs = embeddedStream ?? new FileStream(afcPath, FileMode.Open, FileAccess.Read))
            {
                if (DataOffset + DataSize > fs.Length)
                    return null; //invalid pointer, outside bounds
                MemoryStream ms = new MemoryStream();
                fs.Seek(DataOffset, SeekOrigin.Begin);
                //for (int i = 0; i < DataSize; i++)
                //    fs2.WriteByte((byte)fs.ReadByte());
                fs.CopyToEx(ms, DataSize);
                ms.Position = 0;
                return ms;
                /*
                if (File.Exists(outputFile))
                    File.Delete(outputFile);
                using (FileStream fs2 = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                {
                    fs.Seek(DataOffset, SeekOrigin.Begin);
                    //for (int i = 0; i < DataSize; i++)
                    //    fs2.WriteByte((byte)fs.ReadByte());
                    var dataToCopy = new byte[DataSize];
                    fs.Read(dataToCopy, 0, DataSize);
                    fs2.Write(dataToCopy, 0, DataSize);
                }*/
            }
            //            return true;
        }

        /// <summary>
        /// Converts a Wwise-genreated ogg to the format usable by ME3.
        /// This effectively replaces the need for afc_creator.exe
        /// </summary>
        /// <param name="stream">Stream containing wwiseogg</param>
        /// <returns>ME3 AFC ready stream, at position 0</returns>
        public static MemoryStream ConvertWwiseOggToME3Ogg(Stream stream)
        {
            stream.Position = 0;
            MemoryStream convertedStream = new MemoryStream();
            stream.CopyToEx(convertedStream, 4);
            convertedStream.Write(BitConverter.GetBytes((int)stream.Length - 16), 0, 4);
            stream.Position += 4; //skip over size
            stream.CopyToEx(convertedStream, 0x24); //up to VORB
            stream.Position += 8; //skip vorb
            stream.CopyTo(convertedStream); //copy remaining data

            //update format bytes
            convertedStream.Seek(0x10, SeekOrigin.Begin);
            byte[] firstFmtBytes = { 0x42, 0x00, 0x00, 0x00, 0xFF, 0xFF };
            convertedStream.Write(firstFmtBytes, 0x0, firstFmtBytes.Length);

            //Update second format bytes
            convertedStream.Seek(0x20, SeekOrigin.Begin);
            byte[] secondFmtBytes = { 0x00, 0x00, 0x00, 0x00, 0x30, 0x00, 0x18, 0x00 };
            convertedStream.Write(secondFmtBytes, 0x0, secondFmtBytes.Length);

            convertedStream.Position = 0;
            return convertedStream;
        }

        private void ImportWwiseOgg(string pathafc, Stream wwiseOggStream)
        {
            if (!File.Exists(pathafc) || wwiseOggStream == null)
                return;
            //Convert wwiseoggstream
            MemoryStream convertedStream = ConvertWwiseOggToME3Ogg(wwiseOggStream);
            byte[] newWavfile = convertedStream.ToArray();
            //Open AFC
            FileStream fs = new FileStream(pathafc, FileMode.Open, FileAccess.Read);
            var Header = new byte[94];

            //Seek to data we are replacing and read header
            fs.Seek(DataOffset, SeekOrigin.Begin);
            fs.Read(Header, 0, 94);
            fs.Close();


            //append new wav
            fs = new FileStream(pathafc, FileMode.Append, FileAccess.Write, FileShare.Write);
            int newWavDataOffset = (int)fs.Length;
            int newWavSize = newWavfile.Length;
            fs.Write(newWavfile, 0, newWavSize);
            fs.Close();

            //update memory in this export (clone of memory)
            byte[] buff = BitConverter.GetBytes(newWavSize);
            for (int i = 0; i < 4; i++)
                memory[ValueOffset + i - 4] = buff[i];
            for (int i = 0; i < 4; i++)
                memory[ValueOffset + i] = buff[i];
            buff = BitConverter.GetBytes(newWavDataOffset);
            for (int i = 0; i < 4; i++)
                memory[ValueOffset + i + 4] = buff[i];
            DataSize = newWavSize;
            DataOffset = newWavDataOffset;
        }
    }
}
