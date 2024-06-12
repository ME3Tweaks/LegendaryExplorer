using System.IO;
using System.Text;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.TLK.ME2ME3
{
    /// <summary>
    /// Contains functionality shared between <see cref="ME2ME3TalkFile"/> and <see cref="ME2ME3LazyTLK"/> 
    /// </summary>
    public abstract class ME2ME3TLKBase
    {
        // This is part of ITalkFile in subclasses. This class does not implement it 
        // as only subclasses have the methods
        /// <summary>
        /// The localization of this TLK
        /// </summary>
        public MELocalization Localization { get; set; }

        protected readonly struct TLKHeader
        {
            public readonly int magic;
            public readonly int ver;
            public readonly int min_ver;
            public readonly int MaleEntryCount;
            public readonly int FemaleEntryCount;
            public readonly int treeNodeCount;
            public readonly int dataLen;

            public TLKHeader(EndianReader r)
            {
                magic = r.ReadInt32();
                ver = r.ReadInt32();
                min_ver = r.ReadInt32();
                MaleEntryCount = r.ReadInt32();
                FemaleEntryCount = r.ReadInt32();
                treeNodeCount = r.ReadInt32();
                dataLen = r.ReadInt32();
            }
        };

        protected readonly struct HuffmanNode
        {
            public readonly int LeftNodeID;
            public readonly int RightNodeID;

            public HuffmanNode(EndianReader r)
            {
                LeftNodeID = r.ReadInt32();
                RightNodeID = r.ReadInt32();
            }
        }

        /// <summary>
        /// The number of male TLK strings in this TLK
        /// </summary>
        public int MaleEntryCount => Header.MaleEntryCount;

        /// <summary>
        /// The number of female TLK strings in this TLK
        /// </summary>
        public int FemaleEntryCount => Header.FemaleEntryCount;

        protected TLKHeader Header;
        /// <summary>
        /// Filename without extension
        /// </summary>
        public string FileName;
        /// <summary>
        /// File path
        /// </summary>
        public string FilePath;

        /// <summary>
        /// Loads TLK data from a .tlk file
        /// </summary>
        /// <param name="filePath">Path of the file to load</param>
        public void LoadTlkData(string filePath)
        {
            FilePath = filePath;
            FileName = Path.GetFileNameWithoutExtension(filePath);
            using Stream fs = File.OpenRead(filePath);
            LoadTlkDataFromStream(fs);
            Localization = filePath.GetUnrealLocalization();
        }

        /// <summary>
        /// Loads TLK data from a stream. The position must be properly set.
        /// </summary>
        /// <param name="fs">Stream to read from</param>
        public abstract void LoadTlkDataFromStream(Stream fs);

        /// <summary>
        /// Starts reading <paramref name="bits"/> array at position <paramref name="bitOffset"/>. Read data is
        /// used on a Huffman Tree to decode read bits into real strings.
        /// <paramref name="bitOffset"/> variable is updated with last read bit PLUS ONE (first unread bit).
        /// </summary>
        /// <param name="bitOffset"></param>
        /// <param name="builder">Provide an existing <see cref="StringBuilder"/> to re-use (will be cleared before use)</param>
        /// <param name="bits"></param>
        /// <param name="characterTree">The huffman tree used to decode bits into a string</param>
        /// <returns>
        /// decoded string or null if there's an error (last string's bit code is incomplete)
        /// </returns>
        protected static string GetString(ref int bitOffset, StringBuilder builder, TLKBitArray bits, HuffmanNode[] characterTree)
        {
            HuffmanNode root = characterTree[0];
            HuffmanNode curNode = root;
            builder.Clear();
            int i;
            int bitsLength = bits.Length;
            for (i = bitOffset; i < bitsLength; i++)
            {
                /* reading bits' sequence and decoding it to Strings while traversing Huffman Tree */
                int nextNodeID;
                if (bits.Get(i))
                    nextNodeID = curNode.RightNodeID;
                else
                    nextNodeID = curNode.LeftNodeID;

                /* it's an internal node - keep looking for a leaf */
                if (nextNodeID >= 0)
                    curNode = characterTree[nextNodeID];
                else
                /* it's a leaf! */
                {
                    char c = (char)(0xffff - nextNodeID);
                    if (c != '\0')
                    {
                        /* it's not NULL */
                        builder.Append(c);
                        curNode = root;
                    }
                    else
                    {
                        /* it's a NULL terminating processed string, we're done */
                        bitOffset = i + 1;
                        return builder.ToString();
                    }
                }
            }

            bitOffset = i + 1;

            return null;
        }
    }
}
