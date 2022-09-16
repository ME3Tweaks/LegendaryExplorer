using LegendaryExplorerCore.Packages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Audio;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Gammtek.Extensions.IO;
using LegendaryExplorerCore.Gammtek.IO;

namespace LegendaryExplorerCore.Sound.ISACT
{
    /// <summary>
    /// Contains methods related to working with ISACT content
    /// </summary>
    public static class ISACTHelper
    {
        /// <summary>
        /// Generates the SoundNodeWaveStreamingData binary using C# implementation
        /// </summary>
        /// <param name="wsdExport"></param>
        /// <param name="icbPath"></param>
        /// <param name="isbPath"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string GenerateSoundNodeWaveStreamingDataCS(ExportEntry wsdExport, string icbPath, string isbPath)
        {
            if (icbPath is null || isbPath is null || wsdExport is null)
                throw new Exception("No arguments can be null");

            if (!File.Exists(icbPath))
                throw new Exception($"ICB path not available: {icbPath}");

            if (!File.Exists(isbPath))
                throw new Exception($"ISB path not available: {isbPath}");

            var streamingData = GetStreamingData(icbPath, isbPath);

            MemoryStream ms = new MemoryStream();
            ms.WriteInt32(0);
            streamingData.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);
            ms.WriteInt32((int)ms.Length - 4);
            wsdExport.WriteBinary(ms.ToArray());

            return null;
        }

        private static MemoryStream GetStreamingData(string icbPath, string isbPath)
        {
            ISACTBankPair ibp = new ISACTBankPair();
            using var icbFs = File.OpenRead(icbPath);
            using var isbFs = File.OpenRead(isbPath);
            ibp.ICBBank = new ISACTBank(icbFs);
            ibp.ISBBank = new ISACTBank(isbFs);

            ibp.ISBBank.StripSamples();
            MemoryStream ms = new MemoryStream(SerializePairedBanks(ibp));
            ms.Position = 0; // Reset to zero
            return ms;
        }

        // Todo: Eventually split to own classes and replace existing ISBank class

        /// <summary>
        /// Input data must start with the integer that matches the data to follow size
        /// </summary>
        /// <param name="binRawData"></param>
        /// <returns></returns>
        public static ISACTBankPair GetPairedBanks(byte[] binRawData)
        {
            ISACTBankPair pair = new ISACTBankPair();
            MemoryStream ms = new MemoryStream(binRawData);
            var isbOffset = ms.ReadInt32(); // We don't read this technically
            while (ms.Position < ms.Length && ms.ReadStringASCII(4) == "RIFF")
            {
                ms.Position -= 4;
                var bank = new ISACTBank(ms);
                switch (bank.BankType)
                {
                    case ISACTBankType.ICB:
                        pair.ICBBank = bank;
                        break;
                    case ISACTBankType.ISB:
                        pair.ISBBank = bank;
                        break;
                    default:
                        throw new Exception($"Unsupported bank type: {bank.BankType}");

                }
            }

            return pair;
        }

        public static byte[] SerializePairedBanks(ISACTBankPair banks)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteInt32(0); // ISB offset
            banks.ICBBank.Write(ms);
            ms.Seek(0, SeekOrigin.Begin);
            ms.WriteInt32((int)ms.Length);
            ms.Seek(0, SeekOrigin.End);
            banks.ISBBank.Write(ms);

            // Write size
            return ms.ToArray();
        }
    }

    /// <summary>
    /// Contains a pair of ISACT banks (ICB + ISB)
    /// </summary>
    public class ISACTBankPair
    {
        public ISACTBankPair() { }

        public ISACTBank ICBBank { get; set; }
        public ISACTBank ISBBank { get; set; }
    }

    public enum ISACTBankType
    {
        ICB, // Content Bank
        ISB, // Sample Bank
        SAC // Not used by game
    }

    public class ISACTBank
    {
        public ISACTBankType BankType;
        public List<BankChunk> BankChunks { get; set; }

        public ISACTBank(Stream inStream)
        {
            // Stream should start on RIFF
            var riff = inStream.ReadStringASCII(4);
            if (riff != "RIFF")
                throw new Exception($"Input for bank does not start with RIFF! It starts with {riff}");

            var bankFileLen = inStream.ReadInt32();
            var bankStartPos = inStream.Position;

            var bankType = inStream.ReadStringASCII(4);
            switch (bankType)
            {
                case "icbf":
                    BankType = ISACTBankType.ICB;
                    break;
                case "isbf":
                    BankType = ISACTBankType.ISB;
                    break;
                case "isac":
                    BankType = ISACTBankType.SAC;
                    break;
                default:
                    throw new Exception($"Unsupported file type: {bankType}");
            }

            BankChunks = new List<BankChunk>();
            var endPos = bankFileLen + bankStartPos;
            while (inStream.Position < endPos)
            {
                Debug.WriteLine($"Reading chunk at 0x{inStream.Position:X8}, endpos: 0x{endPos:X8}");
                ReadChunk(inStream, BankChunks);
            }
        }

        public static void ReadChunk(Stream inStream, List<BankChunk> chunks)
        {
            var chunkName = inStream.ReadStringASCII(4);

            int chunkLen = 0;
            // Some are subchunks that have a known fixed length. In this instance we don't read the len
            switch (chunkName)
            {

                //case "samp":
                //case "fldr":
                case "snde":
                    break; // Seems to be a marker of some kind for parser
                default:
                    chunkLen = inStream.ReadInt32();
                    break;
            }

            // Parse special types
            switch (chunkName)
            {
                case "snde":
                    chunks.Add(new NameOnlyChunk(chunkName)); // These are just markers in files it seems and don't have any actual standalone chunk data
                    break;
                case "cmpi":
                    chunks.Add(new CompressionInfoBankChunk(inStream)); // We need to know data for this to replace audio
                    break;
                case "sinf":
                    chunks.Add(new SampleInfoBankChunk(inStream)); // We need to know data for this to replace audio
                    break;
                case "soff":
                    chunks.Add(new SampleOffsetBankChunk(inStream));
                    break;
                case "chnk":
                    chunks.Add(new ChannelBankChunk(inStream)); // We need to know data for this to replace audio
                    break;
                case "LIST":
                    // These are special listing things.
                    chunks.Add(new ListBankChunk(chunkLen, inStream)); // We need to know data for this to replace audio
                    break;
                case "titl":
                    chunks.Add(new TitleBankChunk(chunkName, chunkLen, inStream)); // Easier to use for debugging
                    break;
                default:
                    chunks.Add(new BankChunk(chunkName, chunkLen, inStream));
                    break;
            }
        }

        /// <summary>
        /// Writes this bank's info out to the memory stream
        /// </summary>
        /// <param name="ms"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void Write(MemoryStream ms)
        {
            ms.WriteStringASCII("RIFF");
            var riffLenPos = ms.Position;
            ms.WriteInt32(0); // length placeholder

            ms.WriteStringASCII(BankType.ToString().ToLower() + "f"); //icbf isbf

            foreach (var bank in BankChunks)
            {
                bank.Write(ms);
            }

            // Write size
            ms.Seek(riffLenPos, SeekOrigin.Begin);
            ms.WriteInt32((int)(ms.Length - riffLenPos));
            ms.Seek(0, SeekOrigin.End);
        }

        /// <summary>
        /// Do not use this for serializing as it will contain enclosing chunks of chunks
        /// </summary>
        /// <returns></returns>
        public List<BankChunk> GetAllBankChunks()
        {
            List<BankChunk> chunks = new List<BankChunk>();
            foreach (var chunk in BankChunks)
            {
                chunks.Add(chunk); // Technically if this has subchunks it will also include it
                chunks.AddRange(chunk.GetAllSubChunks());
            }

            foreach (var c in chunks)
            {
                Debug.WriteLine(c.ChunkName);
            }

            return chunks;
        }

        public void StripSamples()
        {
            // Replace data segment with soff
            stripSubchunks(BankChunks);
        }

        private void stripSubchunks(List<BankChunk> chunks)
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                var bc = chunks[i];
                if (bc.ChunkName == "data")
                {
                    chunks[i] = new SampleOffsetBankChunk() { SampleOffset = (uint)bc.ChunkDataStartOffset};  // Points directly at OggS
                }
                if (bc.SubChunks.Any())
                    stripSubchunks(bc.SubChunks);
            }
        }
    }

    public class ChannelBankChunk : BankChunk
    {
        public int ChannelCount;
        public ChannelBankChunk(Stream inStream)
        {
            ChunkName = "chnk";
            ChannelCount = inStream.ReadInt32();
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(4); // size
            outStream.WriteInt32(ChannelCount);
        }
    }

    [DebuggerDisplay("ListBankChunk of type {ObjectType} with {SubChunks.Count} sub chunks")]
    internal class ListBankChunk : BankChunk
    {
        public string ObjectType; // What this LIST object is
        public ListBankChunk(int chunkLen, Stream inStream)
        {
            ChunkName = "LIST";
            var startPos = inStream.Position;
            var endPos = chunkLen + startPos;

            ObjectType = inStream.ReadStringASCII(4);

            while (inStream.Position < endPos)
            {
                Debug.WriteLine($"Reading chunk at 0x{inStream.Position:X8}, endpos: 0x{endPos:X8}");
                ISACTBank.ReadChunk(inStream, SubChunks);

                if (inStream.Position + 1 == endPos)
                    inStream.ReadByte(); // Even boundary
            }
        }


        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            var lenPos = outStream.Position;
            outStream.WriteInt32(0);
            outStream.WriteStringASCII(ObjectType);
            foreach (var chunk in SubChunks)
            {
                chunk.Write(outStream);
            }

            outStream.Seek(lenPos, SeekOrigin.Begin);
            var len = outStream.Length - lenPos;
            outStream.WriteInt32((int)len);
            outStream.Seek(0, SeekOrigin.End);

            if ((len & 1L) != 0L)
                outStream.WriteByte(0); // Even boundary

        }
    }

    internal class NameOnlyChunk : BankChunk
    {
        public NameOnlyChunk(string chunkName)
        {
            ChunkName = chunkName;
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
        }
    }

    /// <summary>
    /// A generic 4 byte name 4 byte length block that is not parsed.
    /// </summary>
    [DebuggerDisplay("BankChunk {ChunkName}")]

    public class BankChunk
    {
        public string ChunkName;
        public byte[] RawData;

        /// <summary>
        /// Some chunks have subchunks
        /// </summary>
        public List<BankChunk> SubChunks = new List<BankChunk>(0);


        /// <summary>
        /// The offset in which this bank's data starts - + 8 after header & size. For name only this should not be used.
        /// </summary>
        public long ChunkDataStartOffset { get; init; }

        public BankChunk() { }
        public BankChunk(string chunkName, int chunkLen, Stream inStream)
        {
#if DEBUG
            if (chunkName == "data")
                Debug.WriteLine("FOUND 'data'!");
#endif
            ChunkDataStartOffset = inStream.Position;
            ChunkName = chunkName;
            RawData = inStream.ReadToBuffer(chunkLen);
        }


        public virtual void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(RawData.Length);
            outStream.Write(RawData);
        }

        public IEnumerable<BankChunk> GetAllSubChunks()
        {
            if (SubChunks.Count == 0) return Array.Empty<BankChunk>();
            List<BankChunk> returnList = new List<BankChunk>();
            returnList.AddRange(SubChunks);
            returnList.AddRange(SubChunks.SelectMany(x => x.GetAllSubChunks()));
            return returnList;
        }
    }

    [DebuggerDisplay("TitleBankChunk: {Value}")]
    public class TitleBankChunk : BankChunk
    {
        public string Value;
        public TitleBankChunk(string chunkName, int chunkLen, Stream inStream) : base(chunkName, chunkLen, inStream)
        {
            Value = Encoding.Unicode.GetString(RawData);
        }
    }

    /// <summary>
    /// ISACT Compression Info bank chunk
    /// </summary>
    public class CompressionInfoBankChunk : BankChunk
    {
        /// <summary>
        /// Should be same as Target in processed ISB
        /// </summary>
        public int CurrentFormat;
        /// <summary>
        /// Should be same as current in processed ISB
        /// </summary>
        public int TargetFormat;

        /// <summary>
        /// Size of the compressed data block
        /// </summary>
        public int TotalSize;
        /// <summary>
        /// Not sure
        /// </summary>
        public int PacketSize;
        /// <summary>
        /// Ratio chosen to compress data with
        /// </summary>
        public float CompressionRatio;
        /// <summary>
        /// Not sure
        /// </summary>
        public float CompressionQuality;
        public CompressionInfoBankChunk(Stream inStream)
        {
            ChunkDataStartOffset = inStream.Position;


            ChunkName = @"cmpi"; // We know the chunk name and len already so we don't need this.
            CurrentFormat = inStream.ReadInt32();
            TargetFormat = inStream.ReadInt32();
            TotalSize = inStream.ReadInt32();
            PacketSize = inStream.ReadInt32();
            CompressionRatio = inStream.ReadFloat();
            CompressionQuality = inStream.ReadFloat(); // Seems to not always be present
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(24); // Fixed size
            outStream.WriteInt32(CurrentFormat);
            outStream.WriteInt32(TargetFormat);
            outStream.WriteInt32(TotalSize);
            outStream.WriteInt32(PacketSize);
            outStream.WriteFloat(CompressionRatio);
            outStream.WriteFloat(CompressionQuality);
        }
    }

    /// <summary>
    /// ISACT info about the sample data
    /// </summary>
    public class SampleInfoBankChunk : BankChunk
    {
        public int BufferOffset;
        public int TimeLength;
        public int SamplesPerSecond;
        public int ByteLength;
        public ushort BitsPerSample;
        public SampleInfoBankChunk(Stream inStream)
        {
            ChunkDataStartOffset = inStream.Position;

            ChunkName = @"sinf"; // We know the chunk name and len already so we don't need this.
            BufferOffset = inStream.ReadInt32();
            TimeLength = inStream.ReadInt32();
            SamplesPerSecond = inStream.ReadInt32();
            ByteLength = inStream.ReadInt32();
            BitsPerSample = inStream.ReadUInt16();
            inStream.ReadUInt16(); // Align 2 since struct size is 20 (align 4)
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(20); // Fixed size
            outStream.WriteInt32(BufferOffset);
            outStream.WriteInt32(TimeLength);
            outStream.WriteInt32(SamplesPerSecond);
            outStream.WriteInt32(ByteLength);
            outStream.WriteUInt16(BitsPerSample);
            outStream.WriteUInt16(0); // Align to 4 byte boundary
        }
    }

    /// <summary>
    /// BioWare-specific: Sample offset in external ISB
    /// </summary>
    public class SampleOffsetBankChunk : BankChunk
    {
        public uint SampleOffset { get; set; }

        public SampleOffsetBankChunk(Stream inStream) : this()
        {
            ChunkDataStartOffset = inStream.Position;
            SampleOffset = inStream.ReadUInt32(); // Align 2 since struct size is 20 (align 4)
        }

        public SampleOffsetBankChunk()
        {
            ChunkName = @"soff"; // We know the chunk name and len already so we don't need this.
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(4); // Fixed size
            outStream.WriteUInt32(SampleOffset);
        }
    }
}
