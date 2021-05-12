using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.TLK.ME1;
using static LegendaryExplorerCore.TLK.ME1.ME1TalkFile;

namespace LegendaryExplorerCore.TLK.ME2ME3
{
    public class TalkFile
    {
        public struct TLKHeader
        {
            public int magic;
            public int ver;
            public int min_ver;
            public int MaleEntryCount;
            public int FemaleEntryCount;
            public int treeNodeCount;
            public int dataLen;

            public TLKHeader(EndianReader r)
                : this()
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

        public struct HuffmanNode
        {
            public int LeftNodeID;
            public int RightNodeID;

            public HuffmanNode(BinaryReader r)
                : this()
            {
                LeftNodeID = r.ReadInt32();
                RightNodeID = r.ReadInt32();
            }
        }

        public TLKHeader Header;
        /// <summary>
        /// Lookup table for strings. The result is a list - the first string is the main one, the second is the female one (if any).
        /// </summary>
        public Dictionary<int, List<TLKStringRef>> StringRefsTable;
        public List<TLKStringRef> StringRefs;
        public string name;
        public string path;
        List<HuffmanNode> CharacterTree;
        BitArray Bits;

        public delegate void ProgressChangedEventHandler(int percentProgress);
        public event ProgressChangedEventHandler ProgressChanged;
        private void OnProgressChanged(int percentProgress)
        {
            ProgressChanged?.Invoke(percentProgress);
        }


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

        /// <summary>
        /// Loads TLK data from a stream. The position must be properly set.
        /// </summary>
        /// <param name="fs"></param>
        public void LoadTlkDataFromStream(Stream fs)
        {
            //Magic: "Tlk " on Little Endian
            EndianReader r = EndianReader.SetupForReading(fs, 0x006B6C54, out var magic);
            r.Position = 0;
            Header = new TLKHeader(r);

            //DebugTools.PrintHeader(Header);

            /* **************** STEP TWO ****************
             *  -- read and store Huffman Tree nodes -- 
             */
            /* jumping to the beginning of Huffmann Tree stored in TLK file */
            long pos = r.BaseStream.Position;
            r.BaseStream.Seek(pos + (Header.MaleEntryCount + Header.FemaleEntryCount) * 8, SeekOrigin.Begin);

            CharacterTree = new List<HuffmanNode>();
            for (int i = 0; i < Header.treeNodeCount; i++)
                CharacterTree.Add(new HuffmanNode(r));

            /* **************** STEP THREE ****************
             *  -- read all of coded data into memory -- 
             */
            byte[] data = new byte[Header.dataLen];
            r.BaseStream.Read(data, 0, data.Length);
            /* and store it as raw bits for further processing */
            Bits = new BitArray(data);

            /* rewind BinaryReader just after the Header
             * at the beginning of TLK Entries data */
            r.BaseStream.Seek(pos, SeekOrigin.Begin);

            /* **************** STEP FOUR ****************
             * -- decode (basing on Huffman Tree) raw bits data into actual strings --
             * and store them in a Dictionary<int, string> where:
             *   int: bit offset of the beginning of data (offset starting at 0 and counted for Bits array)
             *        so offset == 0 means the first bit in Bits array
             *   string: actual decoded string */
            Dictionary<int, string> rawStrings = new Dictionary<int, string>();
            int offset = 0;
            // int maxOffset = 0;
            var builder = new StringBuilder(); //reuse the same stringbuilder to avoid allocations
            while (offset < Bits.Length)
            {
                int key = offset;
                // if (key > maxOffset)
                // maxOffset = key;
                /* read the string and update 'offset' variable to store NEXT string offset */
                string s = GetString(ref offset, builder);
                rawStrings.Add(key, s);
            }

            // Console.WriteLine("Max offset = " + maxOffset);

            /* **************** STEP FIVE ****************
             *         -- bind data to String IDs --
             * go through Entries in TLK file and read it's String ID and offset
             * then check if offset is a key in rawStrings and if it is, then bind data.
             * Sometimes there's no such key, in that case, our String ID is probably a substring
             * of another String present in rawStrings. 
             */
            StringRefs = new List<ME1TalkFile.TLKStringRef>();
            StringRefsTable = new Dictionary<int, List<TLKStringRef>>();
            for (int i = 0; i < Header.MaleEntryCount + Header.FemaleEntryCount; i++)
            {
                ME1TalkFile.TLKStringRef sref = new ME1TalkFile.TLKStringRef(r, false);
                sref.Index = i;
                if (sref.BitOffset >= 0)
                {
                    if (!rawStrings.ContainsKey(sref.BitOffset))
                    {
                        int tmpOffset = sref.BitOffset;
                        string partString = GetString(ref tmpOffset, builder);

                        /* actually, it should store the fullString and subStringOffset,
                         * but as we don't have to use this compression feature,
                         * we will store only the part of string we need */

                        /* int key = rawStrings.Keys.Last(c => c < sref.BitOffset);
                         * string fullString = rawStrings[key];
                         * int subStringOffset = fullString.LastIndexOf(partString);
                         * sref.StartOfString = subStringOffset;
                         * sref.Data = fullString;
                         */
                        sref.Data = partString;
                    }
                    else
                    {
                        sref.Data = rawStrings[sref.BitOffset];
                    }
                }
                StringRefs.Add(sref);
                if (!StringRefsTable.TryGetValue(sref.StringID, out var srefList))
                {
                    srefList = new List<TLKStringRef>();
                    StringRefsTable[sref.StringID] = srefList;
                }
                srefList.Add(sref);
            }
            r.Close();
        }

        public string findDataById(int strRefID, bool withFileName = false, bool returnNullIfNotFound = false, bool noQuotes = false)
        {
            if (StringRefsTable.TryGetValue(strRefID, out var data) && data.Any())
            {
                var strref = data[0];
                if (noQuotes)
                    return strref.Data;

                var retdata = "\"" + strref.Data + "\"";
                if (withFileName)
                {
                    retdata += " (" + name + ")";
                }
                return retdata;
            }

            return returnNullIfNotFound ? null : "No Data";
        }

        /// <summary>
        /// Writes data stored in memory to an appriopriate text format.
        /// </summary>
        /// <param name="fileName"></param>
        public void DumpToFile(string fileName)
        {
            File.Delete(fileName);
            /* for now, it's better not to sort, to preserve original order */
            // StringRefs.Sort(CompareTlkStringRef);

            using XmlTextWriter xr = new XmlTextWriter(fileName, Encoding.UTF8);
            WriteXML(xr);
        }

        public string WriteXMLString()
        {
            StringBuilder InputTLK = new StringBuilder();
            using StringWriter stringWriter = new StringWriter(InputTLK);
            using XmlTextWriter writer = new XmlTextWriter(stringWriter);
            WriteXML(writer);
            return InputTLK.ToString();
        }

        private void WriteXML(XmlTextWriter xr)
        {
            int totalCount = StringRefs.Count;
            int count = 0;
            int lastProgress = -1;
            xr.Formatting = Formatting.Indented;
            xr.Indentation = 4;

            xr.WriteStartDocument();
            xr.WriteStartElement("tlkFile");
            xr.WriteAttributeString("TLKToolVersion", LegendaryExplorerCoreLib.GetVersion());

            xr.WriteComment("Male entries section begin");

            foreach (var s in StringRefs)
            {
                if (s.Index == Header.MaleEntryCount)
                {
                    xr.WriteComment("Male entries section end");
                    xr.WriteComment("Female entries section begin");
                }

                xr.WriteStartElement("String");

                xr.WriteStartAttribute("id");
                xr.WriteValue(s.StringID);
                xr.WriteEndAttribute();

                if (s.BitOffset < 0)
                {
                    xr.WriteStartAttribute("calculatedID");
                    xr.WriteValue(-(int.MinValue - s.StringID));
                    xr.WriteEndAttribute();

                    xr.WriteString("-1");
                }
                else
                    xr.WriteString(s.Data);

                xr.WriteEndElement(); // </string> 

                int progress = (++count * 100) / totalCount;
                if (progress > lastProgress)
                {
                    lastProgress = progress;
                    OnProgressChanged(lastProgress);
                }
            }

            xr.WriteComment("Female entries section end");
            xr.WriteEndElement(); // </tlkFile>
        }

        /// <summary>
        /// Starts reading 'Bits' array at position 'bitOffset'. Read data is
        /// used on a Huffman Tree to decode read bits into real strings.
        /// 'bitOffset' variable is updated with last read bit PLUS ONE (first unread bit).
        /// </summary>
        /// <param name="bitOffset"></param>
        /// <param name="builder"></param>
        /// <returns>
        /// decoded string or null if there's an error (last string's bit code is incomplete)
        /// </returns>
        /// <remarks>
        /// Global variables used:
        /// List(of HuffmanNodes) CharacterTree
        /// BitArray Bits
        /// </remarks>
        private string GetString(ref int bitOffset, StringBuilder builder)
        {
            HuffmanNode root = CharacterTree[0];
            HuffmanNode curNode = root;
            builder.Clear();
            int i;
            for (i = bitOffset; i < Bits.Length; i++)
            {
                /* reading bits' sequence and decoding it to Strings while traversing Huffman Tree */
                int nextNodeID;
                if (Bits[i])
                    nextNodeID = curNode.RightNodeID;
                else
                    nextNodeID = curNode.LeftNodeID;

                /* it's an internal node - keep looking for a leaf */
                if (nextNodeID >= 0)
                    curNode = CharacterTree[nextNodeID];
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

        /// <summary>
        /// If the TLK instance has been modified by the ReplaceString method.
        /// </summary>
        public bool IsModified { get; private set; }

        /// <summary>
        /// Replaces a string in the list of StringRefs. Does not work for Female strings as they share the same string ID (all instances will be replaced)
        /// </summary>
        /// <param name="stringID">The ID of the string to replace.</param>
        /// <param name="newText">The new text of the string.</param>
        /// <param name="addIfNotFound">If the string should be added as new stringref if it is not found. Default is false.</param>
        /// <returns>True if the string was found, false otherwise.</returns>
        public bool ReplaceString(int stringID, string newText, bool addIfNotFound = false)
        {
            if (StringRefsTable.TryGetValue(stringID, out var strRef))
            {
                IsModified = true;
                foreach (var v in strRef)
                {
                    v.Data = newText;
                }
            }
            else if (addIfNotFound)
            {
                IsModified = true;
                AddString(new TLKStringRef(stringID, 0, newText));
                return false; // Was not found, but was added.
            }
            else
            {
                // Not found, not added
                return false;
            }

            return true;
        }

        /* for sorting */
        private static int CompareTlkStringRef(ME1TalkFile.TLKStringRef strRef1, ME1TalkFile.TLKStringRef strRef2)
        {
            int result = strRef1.StringID.CompareTo(strRef2.StringID);
            return result;
        }

        /// <summary>
        /// Adds a new string reference to the TLK. Marks the TLK as modified.
        /// </summary>
        /// <param name="sref"></param>
        public void AddString(TLKStringRef sref)
        {
            StringRefs.Add(sref);
            if (!StringRefsTable.TryGetValue(sref.StringID, out var srefList))
            {
                srefList = new List<TLKStringRef>();
                StringRefsTable[sref.StringID] = srefList;
            }
            srefList.Add(sref);

            IsModified = true;
        }
    }
}
