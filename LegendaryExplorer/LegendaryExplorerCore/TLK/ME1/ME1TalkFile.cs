using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK.ME2ME3;

namespace LegendaryExplorerCore.TLK.ME1
{
    /// <summary>
    /// Represents the tlk embedded in a BioTalkFile export, which is used in ME1/LE1. For ME2/ME3/LE2/LE3TLK, use <see cref="ME2ME3TalkFile"/>.
    /// </summary>
    [DebuggerDisplay("ME1TalkFile - StringRefs: {StringRefs.Count} - Modified: {IsModified} - Package: {FilePath}")]
    public class ME1TalkFile : IEquatable<ME1TalkFile>, ITalkFile
    {
        /// <summary>
        /// The localization of the TLK
        /// </summary>
        public MELocalization Localization { get; set; }

        /// <summary>
        /// If TLK is modified. This should not be trusted as you can directly edit StringRefs. Only use if your own code sets it.
        /// </summary>
        public bool IsModified { get; set; }

        #region structs

        private readonly struct HuffmanNode
        {
            public readonly int LeftNodeID;
            public readonly int RightNodeID;
            public readonly char Data;

            public HuffmanNode(int r, int l)
            {
                RightNodeID = r;
                LeftNodeID = l;
                Data = default;
            }

            public HuffmanNode(char c)
            {
                Data = c;
                LeftNodeID = -1;
                RightNodeID = -1;
            }
        }

        #endregion

        private HuffmanNode[] nodes;

        /// <summary>
        /// All the <see cref="TLKStringRef"/>s in the TLK
        /// </summary>
        public List<TLKStringRef> StringRefs { get; set; }

        /// <summary>
        /// The UIndex of the BioTLKFile export
        /// </summary>
        public readonly int UIndex;

        /// <summary>
        /// The path of the file this was in
        /// </summary>
        public readonly string FilePath;

        /// <summary>
        /// The name of the BioTalkFile export
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// The name of the BioTlkSet this BioTalkFile is in. (Probably. Not gauranteed to be accurate)
        /// </summary>
        public readonly string BioTlkSetName;


        #region Constructors
        /// <summary>
        /// Creates a new <see cref="ME1TalkFile"/> from the export at <paramref name="uIndex"/> in <paramref name="pcc"/>
        /// </summary>
        /// <param name="pcc">The ME1/LE1 package file the BioTalkFile export is in</param>
        /// <param name="uIndex">The uIndex in <paramref name="pcc"/> of the BioTalkFile export</param>
        public ME1TalkFile(IMEPackage pcc, int uIndex) : this(pcc, pcc.GetUExport(uIndex))
        {
        }

        /// <summary>
        /// Creates a new <see cref="ME1TalkFile"/> from the <paramref name="export"/>
        /// </summary>
        /// <param name="export">A BioTalkFile export</param>
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

            // ME1 localizations for TLK are... fun
            Localization = getTlkLocalization(export);

        }

        private MELocalization getTlkLocalization(ExportEntry exportEntry)
        {
            var testLoc = Name.GetUnrealLocalization();
            if (testLoc != MELocalization.None) return testLoc;

            if (Name is "tlk" or "tlk_M") return MELocalization.INT;
            if (Name is "GlobalTlk_tlk" or "GlobalTlk_tlk_M")
            {
                // If the package doesn't have a localization, we just default to INT.
                // An example of this is GlobalTlk.upk in ME1 which has no LOC designations 
                // but is INT.
                return exportEntry.FileRef.Localization != MELocalization.None
                    ? exportEntry.FileRef.Localization
                    : MELocalization.INT;
            }
            return MELocalization.None; // We have no idea
        }

        #endregion


        /// <summary>
        /// Replaces a string in the list of StringRefs.
        /// </summary>
        /// <param name="stringID">The ID of the string to replace.</param>
        /// <param name="newText">The new text of the string.</param>
        /// <param name="addIfNotFound">If the string should be added as new stringref if it is not found. Default is false.</param>
        /// <returns>True if the string was found, false otherwise.</returns>
        public bool ReplaceString(int stringID, string newText, bool addIfNotFound = false)
        {
            var strRef = StringRefs.Find(x => x.StringID == stringID);
            if (strRef != null)
            {
                strRef.Data = newText;
                return true; // Was found and updated.
            }

            if (addIfNotFound)
            {
                IsModified = true;
                AddString(new TLKStringRef(stringID, newText));
                return false; // Was not found, but was added.
            }
            else
            {
                // Not found, not added
                return false;
            }
        }

        /// <summary>
        /// Adds a new string reference to the TLK. Marks the TLK as modified.
        /// </summary>
        /// <param name="sref"></param>
        public void AddString(TLKStringRef sref)
        {
            StringRefs.Add(sref);
            IsModified = true;
        }

        /// <summary>
        /// Gets the string corresponding to the <paramref name="strRefID"/> (wrapped in quotes), if it exists in this tlk. If it does not, returns <c>"No Data"</c>
        /// </summary>
        /// <param name="strRefID">The ID to lookup</param>
        /// <param name="withFileName">Optional: If true, the filename will be appended to the returned string</param>
        /// <param name="returnNullIfNotFound">Optional: If TLK string is not found, setting this to true will return null rather than "No Data"</param>
        /// <param name="noQuotes">Optional: If the returned string data should be in quotes or not. Setting this to false makes the string return the exact TLK string</param>
        /// <param name="male">Optional: Unused in Game 1 as TLKs are fully split.</param>
        /// 
        public string FindDataById(int strRefID, bool withFileName = false, bool returnNullIfNotFound = false, bool noQuotes = false, bool male = true)
        {
            // Todo: Find way to do this faster if possible, maybe like binary search (if TLKs are in order?)
            foreach (TLKStringRef tlkStringRef in StringRefs)
            {
                if (tlkStringRef.StringID == strRefID)
                {
                    string data = "";
                    if (noQuotes)
                    {
                        data = tlkStringRef.Data ?? "";
                    }
                    else
                    {
                        data = $"\"{(tlkStringRef.Data ?? "")}\"";
                    }
                    if (withFileName)
                    {
                        data += $" ({Path.GetFileName(FilePath)} -> {BioTlkSetName}.{Name})";
                    }

                    return data;
                }
            }
            return returnNullIfNotFound ? null : "No Data";
        }


        /// <summary>
        /// Find the matching string id for the specified string. Returns -1 if not found. The male parameter is not used.
        /// </summary>
        /// <param name="value">The text value to find, without quotes.</param>
        /// <param name="male">Optional: Not used in Game 1</param>
        /// <returns></returns>
        public int FindIdByData(string value, bool male = true)
        {
            // Male is not used
            var matching = StringRefs.FirstOrDefault(x => x.Data == value);
            if (matching != null) return matching.StringID;
            return -1;
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
            var hashCode = new HashCode();
            hashCode.Add(UIndex);
            hashCode.Add(FilePath, StringComparer.OrdinalIgnoreCase);
            return hashCode.ToHashCode();
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
            StringRefs = new List<TLKStringRef>(entryCount);
            for (int i = 0; i < entryCount; i++)
            {
                StringRefs.Add(new TLKStringRef(r, true));
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
                    char c = curNode.Data;
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
                char c = curNode.Data;
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
        /// Saves this TLK object to an XML file
        /// </summary>
        /// <param name="filePath">path to write an XML file to</param>
        public void SaveToXML(string filePath)
        {
            using var xr = new XmlTextWriter(filePath, Encoding.UTF8);
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

        /// <summary>
        /// Writes the provided <see cref="TLKStringRef"/>s to XML, returned as a string.
        /// </summary>
        /// <param name="name">Tlk name</param>
        /// <param name="tlkStringRefs"><see cref="TLKStringRef"/>s to write to xml</param>
        /// <returns></returns>
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