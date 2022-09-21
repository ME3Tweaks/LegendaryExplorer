using System;
using System.ComponentModel;
using System.IO;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Sound.ISACT;

namespace LegendaryExplorerCore.Audio
{
    /// <summary>
    /// Contains all the information required to display and play an audio sample from an ISACT Sample Bank
    /// </summary>
    public class ISBankEntry_DEPRECATED : INotifyPropertyChanged
    {
        /// <summary>
        /// Filename of the sample
        /// </summary>
        public string SampleTitle { get; set; }
        /// <summary>
        /// The TLK string for the sample (if any)
        /// </summary>
        public string TLKString { get; set; }

        /// <summary>
        /// The endianness of the data
        /// </summary>
        public Endian FileEndianness { get; set; }

        // Information about the sample

        /// <summary>
        /// Number of audio channels
        /// </summary>
        public uint NumChannels { get; set; }
        /// <summary>
        /// The samplerate of the audio
        /// </summary>
        public uint SampleRate { get; set; }

        /// <summary>
        /// The current compression format of the audio
        /// </summary>
        public CompressionInfoBankChunk.ISACTCompressionFormat CompressionFormat { get; set; }

        /// <summary>
        /// The Bits Per Second of the sample
        /// </summary>
        public int BitsPerSecond { get; set; }

        /// <summary>
        /// The raw audio sample data ('data' segment)
        /// </summary>
        public byte[] AudioSampleData { get; set; }

        //public string DisplayString
        //{
        //    get
        //    {
        //        string retstr = FileName + " - Data offset: 0x" + DataOffset.ToString("X8");
        //        var codec = getCodecStr();
        //        if (codec != null)
        //        {
        //            retstr += " - Codec: " + codec;
        //        }

        //        retstr += $"\nSamplerate: {sampleRate} - Bits per sample: {bps}";
        //        return retstr;
        //    }
        //}
        

        /// <summary>
        /// Converts this entry to a standalone RIFF and stores it in the FullData variable. Used for preparing data to feed to VGM stream when this is a subsong in an ISB file
        /// The output of this is NOT a valid ISB file! Only enough to allow VGMStream to parse it.
        /// This is used for non Vorbis streams
        /// </summary>
        //public void PopulateFakeFullData()
        //{
        //    // This needs further testing. It doesn't seem to be correct for
        //    // Xenon platform (ME1)
            
        //    MemoryStream outStream = new MemoryStream();
        //    EndianWriter writer = new EndianWriter(outStream);
        //    writer.Endian = FileEndianness;
        //    writer.WriteStringLatin1("RIFF");
        //    writer.Write(0); //Placeholder for length
        //    writer.WriteStringLatin1("isbf"); //titl is actually a chunk
        //    writer.WriteStringLatin1("LIST");
        //    var listsizepos = writer.BaseStream.Position;
        //    writer.Write(0); //list size placeholder
        //    writer.WriteStringLatin1("samp"); //sample ahead

        //    writer.WriteStringLatin1("chnk");
        //    writer.Write(4);
        //    writer.Write(numberOfChannels);

        //    writer.WriteStringLatin1("chnk");
        //    writer.Write(10);
        //    writer.Write(sampleRate);
        //    writer.Write(pcmBytes);
        //    writer.Write(bps);

        //    writer.WriteStringLatin1("cpmi");
        //    writer.Write(8);
        //    writer.Write(CodecID);
        //    writer.Write(CodecID2);

        //    writer.WriteStringLatin1("data");
        //    writer.Write(DataAsStored.Length);
        //    writer.Write(DataAsStored);

        //    //Correct headers
        //    writer.BaseStream.Position = listsizepos;
        //    writer.Write((uint)writer.BaseStream.Length - (uint)listsizepos);

        //    writer.BaseStream.Position = 0x4;
        //    writer.Write((uint)writer.BaseStream.Length - 0x8);
        //    FullData = outStream.ToArray();
        //}

        //public string getCodecStr()
        //{
        //    switch (CodecID)
        //    {
        //        case -1: return null;
        //        case 0: return $"{bps}-bit PCM";
        //        case 1: return "Xbox IMA";
        //        case 2: return "Vorbis";
        //        case 4: return "XMA";
        //        case 5: return "Sony MSF container"; //only for PS3 files, but we'll just document it here anyways
        //        default: return $"Unknown codec ID ({CodecID})";
        //    }
        //}

        //public TimeSpan? GetLength()
        //{
        //    if (CodecID == 0x0)
        //    {
        //        //PCM
        //    }
        //    else if (CodecID == 0x1)
        //    {

        //    }
        //    else if (CodecID == 0x2)
        //    {
        //        //vorbis - based on VGMStream
        //        var samplecount = pcmBytes / numberOfChannels / (bps / 8);
        //        var seconds = (double)samplecount / sampleRate;
        //        return TimeSpan.FromSeconds(seconds);
        //    }
        //    else if (CodecID == 0x4)
        //    {
        //        //XMA
        //    }
        //    else if (CodecID == 0x5)
        //    {
        //        //Sony MSF (PS3 ME1)
        //        // Get actual samplerate (stored in audio container)
        //        //var datasize = EndianReader.ToUInt32(DataAsStored, 0x0C, FileEndianness);
        //        var actualSampleRate = EndianReader.ToUInt32(DataAsStored, 0x10, FileEndianness);


        //        var seconds = (double)pcmBytes / actualSampleRate / (bps / 8);
        //        //var seconds = (double)samplecount / actualSampleRate;
        //        return TimeSpan.FromSeconds(seconds);
        //    }
        //    return new TimeSpan(0);
        //}

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
