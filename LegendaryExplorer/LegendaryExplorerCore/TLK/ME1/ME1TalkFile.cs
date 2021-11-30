using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.TLK.ME1
{
    public class ME1TalkFile : IEquatable<ME1TalkFile>
    {
        #region structs
        public struct HuffmanNode
        {
            public int LeftNodeID;
            public int RightNodeID;
            public char data;

            public HuffmanNode(int r, int l)
                : this()
            {
                RightNodeID = r;
                LeftNodeID = l;
            }

            public HuffmanNode(char c)
                : this()
            {
                data = c;
                LeftNodeID = -1;
                RightNodeID = -1;
            }
        }

        [DebuggerDisplay("TLKStringRef {StringID} {Data}")]
        public class TLKStringRef : INotifyPropertyChanged, IEquatable<TLKStringRef>, IComparable
        {
            public int StringID { get; set; }
            public string Data { get; set; }
            public int Flags { get; set; }
            public int Index { get; set; }

            public int BitOffset
            {
                get { return Flags; }
                //use same variable to save memory as flags is not used in me2/3, but bitoffset is.
                set { Flags = value; }
            }

            public int CalculatedID
            {
                get { return StringID >= 0 ? StringID : -(int.MinValue - StringID); }
            }

            /// <summary>
            /// This is used by huffman compression
            /// </summary>
            public string ASCIIData
            {
                get
                {
                    if (Data == null)
                    {
                        return "-1\0";
                    }
                    if (Data.EndsWith("\0", StringComparison.Ordinal))
                    {
                        return Data;
                    }
                    return Data + '\0';
                }
            }

            public TLKStringRef(BinaryReader r, bool me1)
            {
                StringID = r.ReadInt32();
                if (me1)
                {
                    Flags = r.ReadInt32();
                    Index = r.ReadInt32();
                }
                else
                {
                    BitOffset = r.ReadInt32();
                }
            }

            public TLKStringRef(int id, int flags, string data, int index = -1)
            {
                StringID = id;
                Flags = flags;
                Data = data;
                Index = index;
            }

            public bool Equals(TLKStringRef other)
            {
                return StringID == other.StringID && ASCIIData == other.ASCIIData && Flags == other.Flags /*&& Index == other.Index*/;
            }
            public int CompareTo(object obj)
            {
                TLKStringRef entry = (TLKStringRef)obj;
                return Index.CompareTo(entry.Index);
            }
#pragma warning disable
            public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
        }
        #endregion

        private HuffmanNode[] nodes;

        public TLKStringRef[] StringRefs;
        public int UIndex;

        public string language;
        public bool male;
        public readonly string FilePath;

        public string Name;
        public string BioTlkSetName;


        #region Constructors
        public ME1TalkFile(IMEPackage pcc, int uIndex) : this(pcc, pcc.GetUExport(uIndex))
        {
        }

        public ME1TalkFile(ExportEntry export) : this(export.FileRef, export)
        {
        }

        private ME1TalkFile(IMEPackage pcc, ExportEntry export)
        {
            if (!pcc.Game.IsGame1())
            {
                throw new Exception("ME1 Unreal TalkFile cannot be initialized with a non-ME1 file");
            }
            UIndex = export.UIndex;
            LoadTlkData(pcc);
            FilePath = pcc.FilePath;
            Name = export.ObjectName.Instanced;
            BioTlkSetName = export.ParentName; //Not technically the tlkset name, but should be about the same
        }
        #endregion

        //ITalkFile
        public string findDataById(int strRefID, bool withFileName = false)
        {
            string data = "No Data";
            foreach (TLKStringRef tlkStringRef in StringRefs)
            {
                if (tlkStringRef.StringID == strRefID)
                {
                    data = $"\"{tlkStringRef.Data}\"";
                    if (withFileName)
                    {
                        data += $" ({Path.GetFileName(FilePath)} -> {BioTlkSetName}.{Name})";
                    }
                    break;
                }
            }
            return data;
        }

        #region IEquatable
        public bool Equals(ME1TalkFile other)
        {
            return (other?.UIndex == UIndex && other.FilePath == FilePath);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            return Equals(obj as ME1TalkFile);
        }

        public override int GetHashCode()
        {
            return 1;
        }
        #endregion

        #region Load Data
        private void LoadTlkData(IMEPackage pcc)
        {
            var r = new EndianReader(pcc.GetUExport(UIndex).GetReadOnlyBinaryStream(), Encoding.Unicode)
            {
                Endian = pcc.Endian
            };
            //hashtable
            int entryCount = r.ReadInt32();
            StringRefs = new TLKStringRef[entryCount];
            for (int i = 0; i < entryCount; i++)
            {
                StringRefs[i] = new TLKStringRef(r, true);
            }

            //Huffman tree
            int nodeCount = r.ReadInt32();
            nodes = new HuffmanNode[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                bool leaf = r.ReadBoolean();
                if (leaf)
                {
                    nodes[i] = new HuffmanNode(r.ReadChar());
                }
                else
                {
                    nodes[i] = new HuffmanNode(r.ReadInt16(), r.ReadInt16());
                }
            }
            //TraverseHuffmanTree(nodes[0], new List<bool>());

            //encoded data
            int stringCount = r.ReadInt32();
            byte[] data = new byte[r.BaseStream.Length - r.BaseStream.Position];
            r.Read(data, 0, data.Length);
            var bits = new TLKBitArray(data);

            //decompress encoded data with huffman tree
            int offset = 4;
            var rawStrings = new List<string>(stringCount);
            while (offset * 8 < bits.Length)
            {
                int size = BitConverter.ToInt32(data, offset);
                offset += 4;
                string s = GetString(offset * 8, bits);
                offset += size + 4;
                rawStrings.Add(s);
            }

            //associate StringIDs with strings
            foreach (TLKStringRef strRef in StringRefs)
            {
                if (strRef.Flags == 1)
                {
                    strRef.Data = rawStrings[strRef.Index];
                }
            }
        }

        private string GetString(int bitOffset, TLKBitArray bits)
        {
            HuffmanNode root = nodes[0];
            HuffmanNode curNode = root;

            var builder = new StringBuilder();
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
                    curNode = nodes[nextNodeID];
                else
                /* it's a leaf! */
                {
                    char c = curNode.data;
                    if (c != '\0')
                    {
                        /* it's not NULL */
                        builder.Append(c);
                        curNode = root;
                        i--;
                    }
                    else
                    {
                        /* it's a NULL terminating processed string, we're done */
                        //skip ahead approximately 9 bytes to the next string
                        return builder.ToString();
                    }
                }
            }

            if (curNode.LeftNodeID == curNode.RightNodeID)
            {
                char c = curNode.data;
                //We hit edge case where final bit is on a byte boundary and there is nothing left to read. This is a leaf node.
                if (c != '\0')
                {
                    /* it's not NULL */
                    builder.Append(c);
                    curNode = root;
                }
                else
                {
                    /* it's a NULL terminating processed string, we're done */
                    //skip ahead approximately 9 bytes to the next string
                    return builder.ToString();
                }
            }

            Debug.WriteLine("RETURNING NULL STRING (NOT NULL TERMINATED)!");
            return null;
        }

        private void TraverseHuffmanTree(HuffmanNode node, List<bool> code)
        {
            /* check if both sons are null */
            if (node.LeftNodeID == node.RightNodeID)
            {
                var ba = new BitArray(code.ToArray());
                string c = "";
                foreach (bool b in ba)
                {
                    c += b ? '1' : '0';
                }
            }
            else
            {
                /* adds 0 to the code - process left son*/
                code.Add(false);
                TraverseHuffmanTree(nodes[node.LeftNodeID], code);
                code.RemoveAt(code.Count - 1);

                /* adds 1 to the code - process right son*/
                code.Add(true);
                TraverseHuffmanTree(nodes[node.RightNodeID], code);
                code.RemoveAt(code.Count - 1);
            }
        }
        #endregion

        /// <summary>
        /// Saves this TLK object to XML
        /// </summary>
        /// <param name="fileName"></param>
        public void saveToFile(string fileName)
        {
            using var xr = new XmlTextWriter(fileName, Encoding.UTF8);
            WriteXML(StringRefs, Name, xr);
        }

        private static void WriteXML(IEnumerable<TLKStringRef> tlkStringRefs, string name, XmlTextWriter writer)
        {
            writer.Formatting = Formatting.Indented;
            writer.Indentation = 4;

            writer.WriteStartDocument();
            writer.WriteStartElement("tlkFile");
            writer.WriteAttributeString("Name", name);

            foreach (TLKStringRef tlkStringRef in tlkStringRefs)
            {
                writer.WriteStartElement("string");
                writer.WriteStartElement("id");
                writer.WriteValue(tlkStringRef.StringID);
                writer.WriteEndElement(); // </id>
                writer.WriteStartElement("flags");
                writer.WriteValue(tlkStringRef.Flags);
                writer.WriteEndElement(); // </flags>
                if (tlkStringRef.Flags != 1)
                    writer.WriteElementString("data", "-1");
                else
                    writer.WriteElementString("data", tlkStringRef.Data);
                writer.WriteEndElement(); // </string>
            }
            writer.WriteEndElement(); // </tlkFile>
        }

        public static string TLKtoXmlstring(string name, IEnumerable<TLKStringRef> tlkStringRefs)
        {
            var InputTLK = new StringBuilder();
            using var stringWriter = new StringWriter(InputTLK);
            using var writer = new XmlTextWriter(stringWriter);
            WriteXML(tlkStringRefs, name, writer);
            return InputTLK.ToString();
        }


    }
}