using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorerCore.Unreal
{
    /// <summary>
    /// Represents a Game 3 .cnd file
    /// </summary>
    public class CNDFile
    {
        private const int Magic = 0x434F4E44;
        private const int Version = 1;

        /// <summary>
        /// The entries contained in this .cnd file
        /// </summary>
        public List<ConditionalEntry> ConditionalEntries;

        /// <summary>
        /// The path to the file this was originally loaded from
        /// </summary>
        public string FilePath;

        private void Read(Stream stream)
        {
            if (stream.ReadInt32() != Magic)
            {
                throw new Exception("This is not a conditional file!");
            }

            int version = stream.ReadInt32();
            if (version != Version)
            {
                throw new Exception($"Wrong file version! Expected '{Version}', got '{version}'");
            }

            //count of serialized conditional bodies. Conditionals with identical bytecode can share the same serialized bytecode,
            //so this can be less than the entryCount. We don't need to know it for deserializing 
            stream.SkipInt16();

            int entryCount = stream.ReadInt16();

            ConditionalEntries = new List<ConditionalEntry>(entryCount);
            for (int i = 0; i < entryCount; i++)
            {
                ConditionalEntries.Add(new ConditionalEntry
                {
                    ID = stream.ReadInt32(),
                    Offset = stream.ReadInt32()
                });
            }

            int streamLength = (int)stream.Length;
            List<ConditionalEntry> sortedEntries = ConditionalEntries.OrderBy(entry => entry.Offset).ToList();
            for (int i = 0; i < entryCount; i++)
            {
                ConditionalEntry entry = sortedEntries[i];
                int nextOffset = streamLength;
                //we have to scan ahead because multiple entries can share the same offset
                for (int j = i; j < entryCount; j++)
                {
                    if (sortedEntries[j].Offset > entry.Offset)
                    {
                        nextOffset = sortedEntries[j].Offset;
                        break;
                    }
                }
                int size = nextOffset - entry.Offset;
                stream.JumpTo(entry.Offset);
                entry.Data = stream.ReadToBuffer(size);
            }
        }

        private void Write(Stream stream)
        {
            ConditionalEntries = ConditionalEntries.OrderBy(x=>x.ID).ToList();

            stream.WriteInt32(Magic);
            stream.WriteInt32(Version);
            stream.WriteUInt16((ushort)ConditionalEntries.Count); // Serialized count. We don't save like BW, so this will always be the same as the amount of conditionals.
            stream.WriteUInt16((ushort)ConditionalEntries.Count);

            //This works, but is not the saving method bioware used.
            //to replicate that, we would need to sort by Data size and combine conditions with the same Data 

            int sumOffset = (int)(stream.Position) + ConditionalEntries.Count * 8;
            //DO NOT CONVERT TO FOREACH! breaks it for some unknown reason
            for (int i = 0; i < ConditionalEntries.Count; i++)
            {
                ConditionalEntry entry = ConditionalEntries[i];
                stream.WriteInt32(entry.ID);
                stream.WriteInt32(sumOffset);
                sumOffset += entry.Data.Length;
            }

            foreach (ConditionalEntry entry in ConditionalEntries)
            {
                stream.WriteFromBuffer(entry.Data);
            }
        }

        /// <summary>
        /// Writes this .cnd file to disk.
        /// </summary>
        /// <param name="filePath">File to write to. If null, will use the <see cref="FilePath"/> property</param>
        public void ToFile(string filePath = null)
        {
            filePath ??= FilePath;
            using var fs = new FileStream(filePath, FileMode.Create);
            Write(fs);
        }

        /// <summary>
        /// Factory method to load a <see cref="CNDFile"/> from a file on disk
        /// </summary>
        /// <param name="filePath">Path to a .cnd file on disk</param>
        /// <returns>Created CNDFile</returns>
        public static CNDFile FromFile(string filePath)
        {
            var cnd = new CNDFile
            {
                FilePath = filePath
            };
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            cnd.Read(fs);
            return cnd;
        }

        /// <summary>
        /// Represents a conditional in a Game 3 .cnd file
        /// </summary>
        [DebuggerDisplay("ID: {" + nameof(ID) + ("}, Offset: {" + nameof(Offset) + "}"))]
        public class ConditionalEntry
        {
            /// <summary>The conditional ID</summary>
            public int ID;
            /// <summary>The data offset of this conditional</summary>
            public int Offset;
            /// <summary>Actual conditional data</summary>
            public byte[] Data;
        }
    }
}
