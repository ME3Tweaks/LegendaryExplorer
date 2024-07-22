using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Audio;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Audio
{
    public class EmbeddedWEMFile
    {
        public uint Id;
        public bool HasBeenFixed;
        public MEGame Game;

        public EmbeddedWEMFile(byte[] WemData, string DisplayString, ExportEntry export, uint Id = 0)
        {
            this.export = export;
            this.Id = Id;
            this.Game = export.Game;
            this.WemData = WemData;
            this.DisplayString = DisplayString;

            int size = EndianReader.ToInt32(WemData, 4, export.FileRef.Endian);
            int subchunk2size = EndianReader.ToInt32(WemData, 0x5A, export.FileRef.Endian);

            if (size != WemData.Length - 8)
            {
                OriginalWemData = WemData.ArrayClone(); //store copy of the original data in the event the user rewrites a WEM

                //Some clips in ME3 are just the intro to the audio. The raw data is literally cutoff and the first ~.5 seconds are inserted into the soundbank.
                //In order to attempt to even listen to these we have to fix the headers for size and subchunk2size.
                size = WemData.Length - 8;
                HasBeenFixed = true;
                this.DisplayString += " - Preloading";
                int offset = 4;

                if (export.FileRef.Endian == Endian.Little)
                {
                    WemData[offset] = (byte)size; // fourth byte
                    WemData[offset + 1] = (byte)(size >> 8); // third byte
                    WemData[offset + 2] = (byte)(size >> 16); // second byte
                    WemData[offset + 3] = (byte)(size >> 24); // last byte

                    offset = 0x5A; //Subchunk2 size offset
                    size = WemData.Length - 94; //size of data to follow
                    WemData[offset] = (byte)size; // fourth byte
                    WemData[offset + 1] = (byte)(size >> 8); // third byte
                    WemData[offset + 2] = (byte)(size >> 16); // second byte
                    WemData[offset + 3] = (byte)(size >> 24); // last byte
                }
                else
                {
                    WemData[offset + 3] = (byte)size; // fourth byte
                    WemData[offset + 2] = (byte)(size >> 8); // third byte
                    WemData[offset + 1] = (byte)(size >> 16); // second byte
                    WemData[offset] = (byte)(size >> 24); // last byte

                    offset = 0x5A; //Subchunk2 size offset
                    size = WemData.Length - 94; //size of data to follow
                    WemData[offset + 3] = (byte)size; // fourth byte
                    WemData[offset + 2] = (byte)(size >> 8); // third byte
                    WemData[offset + 1] = (byte)(size >> 16); // second byte
                    WemData[offset] = (byte)(size >> 24); // last byte
                }

                var audioLen = GetAudioInfo(WemData)?.GetLength();
                if (audioLen != null && audioLen.Value != TimeSpan.Zero)
                {
                    this.DisplayString += $" ({audioLen.Value.ToString(@"mm\:ss\:fff")})";
                }

                if (App.IsDebug)
                {
                    var audioData = GetAudioInfo(WemData);
                    this.DisplayString += $" (Size {audioData.AudioDataSize.ToString()})";
                    this.DisplayString += $" (BitsPerSample {audioData.BitsPerSample.ToString()})";
                    this.DisplayString += $" (Channels {audioData.Channels.ToString()})";
                    this.DisplayString += $" (CodecID {audioData.CodecID.ToString()})";
                    this.DisplayString += $" (Codec {audioData.CodecName.ToString()})";
                    this.DisplayString += $" (SampleCount {audioData.SampleCount.ToString()})";
                    this.DisplayString += $" (SampleRate {audioData.SampleRate.ToString()})";
                }
            }
        }

        private ExportEntry export;
        public byte[] WemData { get; set; }
        public byte[] OriginalWemData { get; set; }
        public string DisplayString { get; set; }

        public AudioInfo GetAudioInfo(byte[] dataOverride = null)
        {
            // Similar to WwiseStream
            try
            {
                AudioInfo ai = new AudioInfo();
                Stream dataStream = new MemoryStream(dataOverride ?? WemData);

                EndianReader er = new EndianReader(dataStream);
                var header = er.ReadStringASCII(4);
                if (header == "RIFX") er.Endian = Endian.Big;
                if (header == "RIFF") er.Endian = Endian.Little;
                // Position 4

                // This info seems wrong. Needs to be updated. Probably for 5.1.

                er.Seek(0xC, SeekOrigin.Current); // Post 'fmt ', get fmt size
                var fmtSize = er.ReadInt32();
                var postFormatPosition = er.Position;
                ai.CodecID = er.ReadUInt16();

                switch (ai.CodecID)
                {
                    case 0xFFFF:
                        ai.CodecName = "Vorbis";
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
                    er.Seek(postFormatPosition + 0x18, SeekOrigin.Begin);
                    ai.SampleCount = er.ReadUInt32();
                }
                else
                {
                    // Find audio sample data chunk size
                    er.Seek(0x10 + fmtSize, SeekOrigin.Begin);
                    var chunkName = er.ReadStringASCII(4);
                    while (!chunkName.Equals("data", StringComparison.InvariantCultureIgnoreCase))
                    {
                        er.Seek(er.ReadInt32(), SeekOrigin.Current);
                        chunkName = er.ReadStringASCII(4);
                    }
                    ai.AudioDataSize = er.ReadUInt32();
                }

                // We don't care about the rest.
                return ai;
            }
            catch
            {
                return null;
            }
        }
    }
}
