using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.TLK.ME1;

namespace LegendaryExplorerCore.TLK.ME2ME3
{
    /// <summary>
    /// Like <see cref="ME2ME3TalkFile"/>, but does not decompress the strings when loaded. Only decompresses a string when it's requested.
    /// </summary>
    public sealed class ME2ME3LazyTLK : ME2ME3TLKBase
    {
        private TLKBitArray Bits;
        private HuffmanNode[] Nodes;
        private StringBuilder _builder;
        private Dictionary<int, int> LazyStringRefs; //StringID -> BitOffset

        /// <inheritdoc/>
        public override void LoadTlkDataFromStream(Stream fs)
        {
            //Magic: "Tlk " on Little Endian
            var r = EndianReader.SetupForReading(fs, 0x006B6C54, out int _);
            r.Position = 0;
            Header = new TLKHeader(r);

            //for the lazyTLK, we're ignoring female entries, as our system currently has no way to request them
            int strRefCount = Header.MaleEntryCount;// + Header.FemaleEntryCount;
            LazyStringRefs = new Dictionary<int, int>(strRefCount);
            for (int i = 0; i < strRefCount; i++)
            {
                //
                LazyStringRefs.Add(r.ReadInt32(), r.ReadInt32());
            }
            r.Skip(Header.FemaleEntryCount * 8);

            Nodes = new HuffmanNode[Header.treeNodeCount];
            for (int i = 0; i < Header.treeNodeCount; i++)
                Nodes[i] = new HuffmanNode(r);

            Bits = new TLKBitArray(r.BaseStream, Header.dataLen);
            r.Close();
        }

        /// <summary>
        /// Gets the string corresponding to the <paramref name="strRefID"/> (wrapped in quotes if <paramref name="noQuotes"/> is not set), if it exists in this file. If it does not, returns <c>"No Data"</c>, or null if <paramref name="returnNullIfNotFound"/> is true.
        /// </summary>
        /// <param name="strRefID"></param>
        /// <param name="withFileName">Optional: Should the filename be appended to the returned string</param>
        /// <returns></returns>
        public string FindDataById(int strRefID, bool withFileName = false, bool returnNullIfNotFound = false, bool noQuotes = false)
        {
            if (LazyStringRefs.TryGetValue(strRefID, out int bitOffset) && bitOffset >= 0)
            {
                string retdata = null;
                if (noQuotes)
                {
                    retdata = GetString(ref bitOffset, _builder ??= new StringBuilder(), Bits, Nodes);
                }
                else
                {
                    retdata = "\"" + GetString(ref bitOffset, _builder ??= new StringBuilder(), Bits, Nodes) + "\"";
                }
                if (withFileName)
                {
                    retdata += " (" + FileName + ")";
                }
                return retdata;
            }

            return returnNullIfNotFound ? null : "No Data";
        }
    }
}
