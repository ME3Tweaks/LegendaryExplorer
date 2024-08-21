using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Audio;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;

// Do not change namespace, its partial class
namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public partial class WwiseStream
    {
        // Non binary addition to class
        // Is this correct?
        public bool IsPCCStored => Filename == null;

        public string GetPathToAFC(string forcedFilename = null)
        {
            //Check if pcc-stored
            if (IsPCCStored)
            {
                return null; //it's pcc stored. we will return null for this case since we already coded for "".
            }

            var afcFilename = (forcedFilename ?? Filename) + ".afc";

            //Look in current directory first
            string path = Path.Combine(Path.GetDirectoryName(Export.FileRef.FilePath), afcFilename);
            if (File.Exists(path))
            {
                return path; //in current directory of this pcc file
            }

            var cooked = Export.FileRef.Game is Packages.MEGame.ME2 ? "CookedPC" : "CookedPCConsole";
            //Look in top level CookedPCConsole folder next
            if(path.Contains(cooked))
            {
                path = path.Split(cooked)[0];
                path = Path.Combine(path, cooked, afcFilename);
                if (File.Exists(path)) return path;
            }

            var gameFiles = MELoadedFiles.GetFilesLoadedInGame(Export.FileRef.Game, includeAFCs: true);
            gameFiles.TryGetValue(afcFilename, out string afcPath);
            return forcedFilename != null ? "" : afcPath ?? ""; // return "" if not found and the name is forced, we don't know where the afc path is right now.
        }

        public AudioInfo GetAudioInfo()
        {
            try
            {
                AudioInfo ai = new AudioInfo();
                Stream dataStream;
                if (IsPCCStored)
                {
                    dataStream = new MemoryStream(EmbeddedData);
                }
                else
                {
                    var afc = GetPathToAFC();
                    if (string.IsNullOrWhiteSpace(afc) || !File.Exists(afc))
                    {
                        return null; // We have access to where the audio is contained
                    }

                    dataStream = ExternalFileHelper.ReadExternalData(afc, DataOffset, DataSize);
                }

                EndianReader er = new EndianReader(dataStream);
                var header = er.ReadStringASCII(4);
                if (header == "RIFX") er.Endian = Endian.Big;
                if (header == "RIFF") er.Endian = Endian.Little;
                // Position 4

                er.Seek(0xC, SeekOrigin.Current); // Post 'fmt ', get fmt size
                var fmtSize = er.ReadInt32();
                var postFormatPosition = er.Position;
                ai.CodecID = er.ReadUInt16();

                switch (ai.CodecID)
                {
                    case 0xFFFF:
                        ai.CodecName = "Vorbis";
                        break;
                    case 0x0166:
                        ai.CodecName = "XMA2";
                        break;
                    default:
                        ai.CodecName = $"Unknown codec ID {ai.CodecID}";
                        break;
                }

                ai.Channels = er.ReadUInt16();
                ai.SampleRate = er.ReadUInt32();
                er.ReadInt32(); //Average bits per second
                er.ReadUInt16(); //Alignment. VGMStream shows this is 16bit but that doesn't seem right
                ai.BitsPerSample = er.ReadUInt16(); //Bytes per sample. For vorbis this is always 0!
                var extraSize = er.ReadUInt16();
                if (extraSize == 0x30)
                {
                    // Newer Wwise
                    er.Seek(postFormatPosition + 0x18, SeekOrigin.Begin);
                    ai.SampleCount = er.ReadUInt32();
                }
                else
                {
                    if (ai.CodecID == 0xFFFF)
                    {
                        // Vorbis
                        er.Seek(0x14 + fmtSize, SeekOrigin.Begin);
                        var chunkName = er.ReadStringASCII(4);
                        while (!chunkName.Equals("vorb", StringComparison.InvariantCultureIgnoreCase))
                        {
                            er.Seek(er.ReadInt32(), SeekOrigin.Current);
                            chunkName = er.ReadStringASCII(4);
                        }
                        er.SkipInt32(); //Skip vorb size
                        ai.SampleCount = er.ReadUInt32();
                    }
                    else if (ai.CodecID == 0x0166)
                    {
                        // XMA2 (360)

                        // This calculation is wrong.
                        // See https://github.com/losnoco/vgmstream/blob/master/src/meta/wwise.c#L484
                        // and 
                        // https://github.com/losnoco/vgmstream/blob/b61908f3af892714dda09c143a52fe0d65228985/src/coding/coding_utils.c#L767
                        // Seems correct, but will need to investigate why it's wrong. Example file is 543 in BioSnd_OmgPrA.xxx Xenon ME2
                        er.Seek(0x14 + 0x18, SeekOrigin.Begin); //Start of fmt + 0x18
                        ai.SampleCount = er.ReadUInt32();
                    }
                    else
                    {
                        // UNKNOWN!!
                        Debug.WriteLine("Unknown codec ID!");
                    }
                }

                // We don't care about the rest.
                return ai;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}