using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Gammtek.IO;

namespace LegendaryExplorerCore.TLK.ME2ME3
{
    public abstract class ME2ME3TLKBase
    {
        public readonly struct TLKHeader
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

        protected TLKHeader Header;
        public string name;
        public string path;



        /// <summary>
        /// Loads a TLK file into memory.
        /// </summary>
        /// <param name="fileName"></param>
        public void LoadTlkData(string fileName)
        {
            path = fileName;
            name = Path.GetFileNameWithoutExtension(fileName);
            /* **************** STEP ONE ****************
             *          -- load TLK file header --
             * 
             * reading first 28 (4 * 7) bytes 
             */

            using Stream fs = File.OpenRead(fileName);
            LoadTlkDataFromStream(fs);
        }

        public abstract void LoadTlkDataFromStream(Stream fs);


        /// <summary>
        /// Starts reading 'Bits' array at position 'bitOffset'. Read data is
        /// used on a Huffman Tree to decode read bits into real strings.
        /// 'bitOffset' variable is updated with last read bit PLUS ONE (first unread bit).
        /// </summary>
        /// <param name="bitOffset"></param>
        /// <param name="builder"></param>
        /// <param name="bits"></param>
        /// <param name="characterTree"></param>
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
