using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;

namespace LegendaryExplorerCore.Unreal
{
    // Todo: Convert to EndianReader
    public class TOCBinFile
    {
        public const int TOCMagicNumber = 0x3AB70C13;

        /// <summary>
        /// Single entry in TOC file, contained in a hash bucket
        /// </summary>
        [DebuggerDisplay("TOCEntry 0x{offset.ToString(\"8\")}, {name}, file size {size}")]
        public class Entry
        {
            /// <summary>
            /// Offset of the entry. Not used for writing to disk, only for reading in
            /// </summary>
            public int offset;

            /// <summary>
            /// Size of this entry on disk. Not used for writing to disk, only for reading in
            /// </summary>
            public int entrydisksize;

            /// <summary>
            /// Flags for the file. It's a bitmask
            /// </summary>
            public short flags;

            /// <summary>
            /// Size of the file in bytes
            /// </summary>
            public int size;

            /// <summary>
            /// The SHA1 of the file. Not used if the flag for SHA1 is not set
            /// </summary>
            public byte[] sha1;

            /// <summary>
            /// The relative path of the file
            /// </summary>
            public string name;

            /// <summary>
            /// Size this entry consumes on disk with byte alignment
            /// </summary>
            public short ToCSize
            {
                get
                {
                    short memSize = MemorySize;
                    return (short)
                        (memSize // Base size of the object
                         + ((memSize % 4 == 0) ? 0 : (4 - (memSize % 4))));    // Padding to 4 byte alignment.
                }
            }

            /// <summary>
            /// Size this entry alone consumes in memory.
            /// </summary>
            public short MemorySize
            {
                get
                {
                    return (short)
                        (2 * sizeof(short) // ToCSize, Flags
                         + 1 * sizeof(int)    // Filesize
                         + 20 // SHA1
                              // DVD information (not used on PC)
                              //+ Sectors.Length * sizeof(int) // integer for each sector
                         + (name.Length + 1));    // String + Null Terminator
                }
            }

            /// <summary>
            /// Writes this entry out to the stream
            /// </summary>
            /// <param name="fs">Stream to write to</param>
            /// <param name="dontWriteEntrySize">If ToCSize should be written out. The final entry in the TOC file will not write this out</param>
            /// <param name="byteAligned">If entries should byte align the next entry. ME3 does not use byte alignment, LE games do</param>
            public void WriteOut(MemoryStream fs, bool dontWriteEntrySize)
            {
                var expectedEndPos = (int)fs.Position + ToCSize;
                fs.WriteInt16(dontWriteEntrySize ? (short)0 : ToCSize);
                fs.WriteInt16(flags);
                fs.WriteInt32(size);
                if (sha1 != null)
                    fs.Write(sha1);
                else
                    fs.WriteZeros(20); // must not have flag for CRC

                fs.WriteStringLatin1Null(name);
                var padding = expectedEndPos - fs.Position;
                fs.WriteZeros((int)padding); // Byte align
            }
        }

        public List<TOCHashTableEntry> HashBuckets = new();

        public TOCBinFile()
        {
        }

        public TOCBinFile(MemoryStream m)
        {
            ReadFile(m);
        }

        public TOCBinFile(string path)
        {
            ReadFile(new MemoryStream(File.ReadAllBytes(path).ToArray()));
        }

        public void DumpTOCToTxtFile(string dumpToFile = "")
        {
            StreamWriter file = new StreamWriter(dumpToFile);

            file.WriteLine($"TOC Hash Buckets: {HashBuckets.Count}");
            for (int i = 0; i < HashBuckets.Count; i++)
            {
                var hb = HashBuckets[i];
                file.WriteLine($"TOC Hash Bucket {i}, {hb.TOCEntries.Count} Entries");
                for (int j = 0; j < hb.TOCEntries.Count; j++)
                {
                    var hte = hb.TOCEntries[j];
                    file.WriteLine($"\tHash File Entry {j} @ {hte.offset:X6}");
                    file.WriteLine($"\t\tNext Offset\t\t\t{hte.entrydisksize}");
                    file.WriteLine($"\t\tFlags\t\t\t0x{hte.flags:X4}");
                    file.WriteLine($"\t\tFilesize\t\t\t{hte.size} ({FileSize.FormatSize(hte.size)})");
                    file.WriteLine($"\t\tSHA1\t\t\t{BitConverter.ToString(hte.sha1)}");
                    file.WriteLine($"\t\tFilename\t\t\t{hte.name}");
                }
            }
        }

#if DEBUG
        public void DumpTOC()
        {
            for (int i = 0; i < HashBuckets.Count; i++)
            {
                var hb = HashBuckets[i];
                if (hb.entrycount != hb.TOCEntries.Count) Debugger.Break();
                Debug.WriteLine($"TOC Hash Bucket {i}, {hb.TOCEntries.Count} Entries");
                for (int j = 0; j < hb.TOCEntries.Count; j++)
                {
                    var hte = hb.TOCEntries[j];
                    Debug.WriteLine($"\tHash File Entry {j} @ {hte.offset:X6}");
                    Debug.WriteLine($"\t\tNext Offset\t\t\t{hte.entrydisksize}");
                    Debug.WriteLine($"\t\tFlags\t\t\t0x{hte.flags:X4}");
                    Debug.WriteLine($"\t\tFilesize\t\t\t{hte.size} ({FileSize.FormatSize(hte.size)})");
                    Debug.WriteLine($"\t\tSHA1\t\t\t{BitConverter.ToString(hte.sha1)}");
                    Debug.WriteLine($"\t\tFilename\t\t\t{hte.name}");
                }
            }
        }
#endif

        public class TOCHashTableEntry
        {
            internal int offset { get; set; }
            internal int entrycount { get; set; }
            public List<Entry> TOCEntries { get; } = new();
        }

public void ReadFile(MemoryStream ms)
{
    var reader = new EndianReader(ms);
    uint magic = (uint)reader.ReadInt32();
    if (magic != TOCMagicNumber)
    {
        throw new Exception("Not a TOC file, bad magic header");
    }

    var mediaTableCount = reader.ReadInt32(); // Should be 0
    var hashTableCount = reader.ReadInt32();

    long maxReadValue = 0;
    for (int i = 0; i < hashTableCount; i++)
    {
        var pos = reader.Position;
        var newEntry = new TOCHashTableEntry()
        {
            offset = reader.ReadInt32(),
            entrycount = reader.ReadInt32(),
        };
        HashBuckets.Add(newEntry);

        var resumePosition = reader.Position;
        // Read Entries

        reader.Position = newEntry.offset + pos;
        for (int j = 0; j < newEntry.entrycount; j++)
        {
            Entry e = new()
            {
                offset = (int)reader.Position,
                entrydisksize = reader.ReadInt16(),
                flags = reader.ReadInt16(),
                size = reader.ReadInt32(),
                sha1 = reader.ReadToBuffer(0x14), // 20
                name = reader.ReadStringASCIINull()
            };

            reader.Seek(e.offset + e.entrydisksize, SeekOrigin.Begin);

            maxReadValue = Math.Max(maxReadValue, reader.Position);

            newEntry.TOCEntries.Add(e);
        }

        reader.Position = resumePosition;
    }
}

        public void UpdateEntry(int Index, int size)
        {
            // Uhhhhhhhhhhhh
            // Not sure how useful this is
            throw new NotImplementedException("Not implemented in LEC right now");
            //if (Entries == null || Index < 0 || Index >= Entries.Count)
            //    return;
            //Entry e = Entries[Index];
            //e.size = size;
            //Entries[Index] = e;
        }

        public MemoryStream Save()
        {
            var lastBucketWithEntries = HashBuckets.LastOrDefault(x => x.TOCEntries.Any());

            MemoryStream fs = MemoryManager.GetMemoryStream();

            fs.WriteInt32(TOCBinFile.TOCMagicNumber); // Endian check
            fs.WriteInt32(0x0); // Media Data Count
            fs.WriteInt32(HashBuckets.Count); // Hash Table Count

            var hashTableStartPos = fs.Position;

            // Skip the hash table for now, we will come back and rewrite it
            fs.Seek((int)HashBuckets.Count * 8, SeekOrigin.Current);

            for (int i = 0; i < HashBuckets.Count; i++)
            {
                var hb = HashBuckets[i];

                var hbEntryStartPos = fs.Position;
                for (int j = 0; j < hb.TOCEntries.Count; j++)
                {
                    var entry = hb.TOCEntries[j];
                    var dontWriteEntrySize = hb == lastBucketWithEntries && j == (hb.TOCEntries.Count - 1);
                    entry.WriteOut(fs, dontWriteEntrySize); // byte aligns automatically
                }

                var nextEntryPos = fs.Position;

                // Update hash table info
                fs.Seek(hashTableStartPos + (i * 8), SeekOrigin.Begin);
                if (hb.TOCEntries.Any())
                {
                    var firstEntryOffset = (int)(hbEntryStartPos - fs.Position);
                    fs.WriteInt32(firstEntryOffset); // Offset from hash entry to first TOC file entry
                    fs.WriteInt32(hb.TOCEntries.Count); // How many files have this hash
                }
                else
                {
                    fs.WriteInt32(0); // No Offset
                    fs.WriteInt32(0); // 0 Entries
                }

                fs.Seek(nextEntryPos, SeekOrigin.Begin);
            }
            return fs;
        }

        /// <summary>
        /// Gets a list of all entries, without hash table information
        /// </summary>
        /// <returns></returns>
        public List<Entry> GetAllEntries() => HashBuckets.Where(x => x.TOCEntries.Any()).SelectMany(x => x.TOCEntries).ToList();
    }
}
