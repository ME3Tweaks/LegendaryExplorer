using NAudio.Wave;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Audio;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Sound.ISACT;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.UnrealExtensions
{
    /// <summary>
    /// Helper class for audio stuff
    /// </summary>
    public class AudioStreamHelper
    {
        public static bool ExtractRawFromSourceToFile(string outfile, string afcPath, int dataSize, int dataOffset)
        {
            var ms = ExternalFileHelper.ReadExternalData(afcPath, dataOffset, dataSize);
            if (ms is null)
            {
                return false;
            }
            if (File.Exists(outfile)) File.Delete(outfile);
            ms.WriteToFile(outfile);
            return true;
        }

        /// <summary>
        /// Creates wav stream from this WwiseStream
        /// </summary>
        /// <param name="afcPath"></param>
        /// <returns></returns>
        public static Stream CreateWaveStreamFromRaw(string afcPath, int offset, int datasize, bool ME2)
        {
            string basePath = GetATempSoundPath();
            if (ExtractRawFromSourceToFile(basePath + ".wem", afcPath, datasize, offset))
            {
                return ConvertRIFFToWaveVGMStream(basePath + ".wem");
            }
            return null;
        }

        public static string GetATempSoundPath() => $"{Path.GetTempPath()}ME3EXP_SOUND_{Guid.NewGuid()}";

        /// <summary>
        /// Converts a RAW RIFF from game data to a playable WAV stream. This can be written to disk as a playable WAV file.
        /// </summary>
        /// <param name="riffPath">Path to RIFF RAW data</param>
        /// <param name="fullSetup">Full setup flag - use for ME2</param>
        public static MemoryStream ConvertRiffToWav(string riffPath, bool fullSetup)
        {
            Stream oggStream = ConvertRIFFToWwiseOGG(riffPath, fullSetup, false);
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

            ProcessStartInfo procStartInfo = new ProcessStartInfo(Path.Combine(AppDirectories.ExecFolder, "oggdec.exe"), $"--stdout \"{oggPath}\"")
            {
                WorkingDirectory = AppDirectories.ExecFolder,
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
        /// <param name="useAlternateCodebook">Alternate PCB - use for LE</param>
        ///
        public static MemoryStream ConvertRIFFToWwiseOGG(string riffPath, bool fullSetup, bool useAlternateCodebook)
        {
            //convert RIFF to WwiseOGG
            // Is this useful?
            //System.Diagnostics.Debug.WriteLine("ww2ogg: " + riffPath);
            if (!File.Exists(riffPath))
            {
                Debug.WriteLine("Error: input file does not exist");
            }

            var alternatePcbFile = Path.Combine(AppDirectories.ExecFolder, "packed_codebooks_aoTuV_603.bin");

            var ww2oggArguments = $@"{(useAlternateCodebook ? @$"--pcb {alternatePcbFile}" : "")} {(fullSetup ? "--full-setup" : "")} --stdout {riffPath}";
            var procStartInfo = new ProcessStartInfo(Path.Combine(AppDirectories.ExecFolder, "ww2ogg.exe"), ww2oggArguments)
            {
                WorkingDirectory = AppDirectories.ExecFolder,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

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
            Debug.WriteLine(System.Text.Encoding.UTF8.GetString(outputErrorData.ToArray()));

            //Debug.WriteLine("Done");
            return outputData;
            //            return Path.Combine(Directory.GetParent(riffPath).FullName, Path.GetFileNameWithoutExtension(riffPath)) + ".ogg";
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

        /// <summary>
        /// Converts a RAW RIFF/RIFX to WAVE using VGMStream and returns the data
        /// </summary>
        /// <param name="inputfilepath">Path to RIFF file</param>
        /// <returns></returns>
        public static MemoryStream ConvertRIFFToWaveVGMStream(string inputfile)
        {
            //convert ISB Codec 1/4 to WAV
            MemoryStream outputData = new MemoryStream();

            // Todo: Link against VGMStream with a wrapper so we don't have to perform disk writes
            ProcessStartInfo procStartInfo = new ProcessStartInfo(Path.Combine(AppDirectories.ExecFolder, "vgmstream", "vgmstream.exe"), $"-P \"{inputfile}\"")
            {
                WorkingDirectory = Path.Combine(AppDirectories.ExecFolder, "vgmstream"),
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            //procStartInfo.StandardOutputEncoding = Encoding.GetEncoding(850); //standard cmd-page
            Process proc = new Process
            {
                StartInfo = procStartInfo
            };

            Debug.WriteLine($"\"{procStartInfo.FileName}\" {procStartInfo.Arguments}");

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

        /// <summary>
        /// Gets a playable stream of data from the ISB Entry. The result may be a <see cref="OggWaveStream"/>, which is the Ogg file - this may need converted for use!
        /// </summary>
        /// <param name="bankEntry">ISBankEntry to get stream from</param>
        /// <param name="game">What game this entry is for - is used for external ISB lookup</param>
        /// <returns></returns>
        public static MemoryStream GetWaveStreamFromISBEntry(ISACTListBankChunk bankEntry, bool forceReturnWaveData = false, string isbName = null, MEGame game = MEGame.Unknown)
        {
            //string outPath = Path.Combine(path, currentFileName);
            MemoryStream waveStream;
            byte[] sampleData = bankEntry.SampleData;
            if (sampleData == null && bankEntry.SampleOffset != null && isbName != null && game != MEGame.Unknown && MEDirectories.GetDefaultGamePath(game) != null)
            {
                // We have to fetch from external ISB
                var fullIsbName = isbName + ".isb";
                var isbs = Directory.EnumerateFiles(MEDirectories.GetDefaultGamePath(game), "*.isb", SearchOption.AllDirectories);
                var fullIsbPath = isbs.FirstOrDefault(x => Path.GetFileName(x).CaseInsensitiveEquals(fullIsbName));
                if (fullIsbPath != null)
                {
                    using var extIsb = File.OpenRead(fullIsbPath);
                    extIsb.Seek((long)bankEntry.SampleOffset, SeekOrigin.Begin);
                    sampleData = extIsb.ReadToBuffer(bankEntry.CompressionInfo.TotalSize);
                }
            }

            if (sampleData == null)
            {
                Debug.WriteLine("Sample data could not be loaded");
                return null;
            }

            switch (bankEntry.CompressionInfo.CurrentFormat)
            {
                case CompressionInfoBankChunk.ISACTCompressionFormat.PCM:
                    {
                        var ms = new MemoryStream(sampleData);
                        var raw = new RawSourceWaveStream(ms,
                            new WaveFormat((int)bankEntry.SampleInfo.SamplesPerSecond, bankEntry.SampleInfo.BitsPerSample,
                                bankEntry.ChannelCount));
                        waveStream = new MemoryStream();
                        WaveFileWriter.WriteWavFileToStream(waveStream, raw);
                        return waveStream;
                    }

                // These require external conversion
                case CompressionInfoBankChunk.ISACTCompressionFormat.IMA4ADPCM:
                case CompressionInfoBankChunk.ISACTCompressionFormat.XMA:
                case CompressionInfoBankChunk.ISACTCompressionFormat.MSMP3:
                case CompressionInfoBankChunk.ISACTCompressionFormat.MSADPCM:
                case CompressionInfoBankChunk.ISACTCompressionFormat.MSPCMBIG:
                case CompressionInfoBankChunk.ISACTCompressionFormat.OGGVORBIS when forceReturnWaveData:
                    //Xbox IMA, XMA, Sony MSF (PS3)
                    //Use VGM Stream
                    byte[] fakeData = bankEntry.GenerateFakeSampleISB();
                    var tempPath = GetATempSoundPath() + ".isb";
                    File.WriteAllBytes(tempPath, fakeData);
                    return ConvertRIFFToWaveVGMStream(tempPath);

                // This code is played directly

                case CompressionInfoBankChunk.ISACTCompressionFormat.OGGVORBIS:
                    {
                        // Return custom stream - player will see custom class
                        var ms = new OggWaveStream(sampleData);
                        return ms;
                    }
                default:
                    //Debug.WriteLine("Unsupported codec for getting wave: " + bankEntry.CodecID);
                    return null; //other codecs currently unsupported
            }
        }

        public static string GetHircObjTypeString(byte b) => GetHircObjTypeString((HIRCType)b);

        public static string GetHircObjTypeString(HIRCType ht) =>
            ht switch
            {
                HIRCType.SoundSXFSoundVoice => "Sound SFX/Sound Voice",
                HIRCType.EventAction => "Event Action",
                HIRCType.Event => "Event",
                HIRCType.RandomOrSequenceContainer => "Random Container or Sequence Container",
                HIRCType.ActorMixer => "Actor-Mixer",
                HIRCType.MusicSegment => "Music Segment",
                HIRCType.MusicTrack => "Music Track",
                HIRCType.MusicSwitchContainer => "Music Switch Container",
                HIRCType.MusicPlaylistContainer => "Music Playlist Container",
                HIRCType.Attenuation => "Attenuation",
                HIRCType.Effect => "Effect",
                HIRCType.AuxiliaryBus => "Auxiliary Bus",
                HIRCType.Settings => "Settings",
                HIRCType.SwitchContainer => "Switch Container",
                HIRCType.AudioBus => "Audio Bus",
                HIRCType.BlendContainer => "Blend Container",
                HIRCType.DialogueEvent => "Dialogue Event",
                HIRCType.MotionBus => "Motion Bus",
                HIRCType.MotionFX => "Motion FX",
                _ => "UNKNOWN HIRCOBJECT TYPE!"
            };

        public static string GetEventActionTypeString(WwiseBank.EventActionType actionType) =>
            actionType switch
            {
                WwiseBank.EventActionType.Play => "Play",
                WwiseBank.EventActionType.Stop => "Stop",
                _ => "Unknown Action"
            };
    }
}