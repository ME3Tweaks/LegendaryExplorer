using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace ME3Explorer
{
    class TalkFile
    {
        internal enum Fileformat
        {
            Txt,
            Csv,
            Xml
        }
        
        public struct TLKHeader
        {
            public int magic;
            public int ver;
            public int min_ver;
            public int entry1Count;
            public int entry2Count;
            public int treeNodeCount;
            public int dataLen;

            public TLKHeader(BinaryReader r)
                : this()
            {
                magic = r.ReadInt32();
                ver = r.ReadInt32();
                min_ver = r.ReadInt32();
                entry1Count = r.ReadInt32();
                entry2Count = r.ReadInt32();
                treeNodeCount = r.ReadInt32();
                dataLen = r.ReadInt32();
            }
        };

        public struct TLKStringRef
        {
            public int StringID;
            public int BitOffset;

            public string Data;
            public int StartOfString;
            public int position;

            public TLKStringRef(BinaryReader r)
                : this()
            {
                StringID = r.ReadInt32();
                BitOffset = r.ReadInt32();
            }
        }

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

        TLKHeader Header;
        public List<TLKStringRef> StringRefs;
        List<HuffmanNode> CharacterTree;
        BitArray Bits;

        public delegate void ProgressChangedEventHandler(int percentProgress);
        public event ProgressChangedEventHandler ProgressChanged;
        private void OnProgressChanged(int percentProgress)
        {
            ProgressChangedEventHandler handler = ProgressChanged;
            if (handler != null)
                handler(percentProgress);
        }

        /// <summary>
        /// Loads a TLK file into memory.
        /// </summary>
        /// <param name="fileName"></param>
        public void LoadTlkData(string fileName)
        {
            /* **************** STEP ONE ****************
             *          -- load TLK file header --
             * 
             * reading first 28 (4 * 7) bytes 
             */
            Stream fs = File.OpenRead(fileName);
            BinaryReader r = new BinaryReader(fs);
            Header = new TLKHeader(r);

            //DebugTools.PrintHeader(Header);

            /* **************** STEP TWO ****************
             *  -- read and store Huffman Tree nodes -- 
             */
            /* jumping to the beginning of Huffmann Tree stored in TLK file */
            long pos = r.BaseStream.Position;
            r.BaseStream.Seek(pos + (Header.entry1Count + Header.entry2Count) * 8, SeekOrigin.Begin);

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
            while (offset < Bits.Length)
            {
                int key = offset;
                // if (key > maxOffset)
                    // maxOffset = key;
                /* read the string and update 'offset' variable to store NEXT string offset */
                string s = GetString(ref offset);
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
            StringRefs = new List<TLKStringRef>();
            for (int i = 0; i < Header.entry1Count + Header.entry2Count; i++)
            {
                TLKStringRef sref = new TLKStringRef(r);
                sref.position = i;
                if (sref.BitOffset >= 0)
                {
                    if (!rawStrings.ContainsKey(sref.BitOffset))
                    {
                        int tmpOffset = sref.BitOffset;
                        string partString = GetString(ref tmpOffset);

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
            }
            r.Close();
        }

        /// <summary>
        /// Writes data stored in memory to an appriopriate text format.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="ff"></param>
        public void DumpToFile(string fileName, Fileformat ff)
        {
            File.Delete(fileName);
            /* for now, it's better not to sort, to preserve original order */
            // StringRefs.Sort(CompareTlkStringRef);

            if (ff == Fileformat.Xml)
                SaveToXmlFile(fileName);
            else
                SaveToTextFile(fileName);
        }

        /// <summary>
        /// Starts reading 'Bits' array at position 'bitOffset'. Read data is
        /// used on a Huffman Tree to decode read bits into real strings.
        /// 'bitOffset' variable is updated with last read bit PLUS ONE (first unread bit).
        /// </summary>
        /// <param name="bitOffset"></param>
        /// <returns>
        /// decoded string or null if there's an error (last string's bit code is incomplete)
        /// </returns>
        /// <remarks>
        /// Global variables used:
        /// List(of HuffmanNodes) CharacterTree
        /// BitArray Bits
        /// </remarks>
        private string GetString(ref int bitOffset)
        {
            HuffmanNode root = CharacterTree[0];
            HuffmanNode curNode = root;

            string curString = "";
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
                    char c = BitConverter.ToChar(BitConverter.GetBytes(0xffff - nextNodeID), 0);
                    if (c != '\0')
                    {
                        /* it's not NULL */
                        curString += c;
                        curNode = root;
                    }
                    else
                    {
                        /* it's a NULL terminating processed string, we're done */
                        bitOffset = i + 1;
                        return curString;
                    }
                }
            }
            bitOffset = i + 1;
            return null;
        }

        /// <summary>
        /// Writing data in an XML format.
        /// </summary>
        /// <param name="fileName"></param>
        private void SaveToXmlFile(string fileName)
        {
            int totalCount = StringRefs.Count();
            int count = 0;
            int lastProgress = -1;
            XmlTextWriter xr = new XmlTextWriter(fileName, Encoding.UTF8);
            xr.Formatting = Formatting.Indented; 
            xr.Indentation = 4;

            xr.WriteStartDocument();
            xr.WriteStartElement("tlkFile");
            xr.WriteAttributeString("TLKToolVersion", App.GetVersion());

            xr.WriteComment("Male entries section begin (ends at position " + (Header.entry1Count - 1) + ")");

            foreach (var s in StringRefs)
            {
                if (s.position == Header.entry1Count)
                {
                    xr.WriteComment("Male entries section end");
                    xr.WriteComment("Female entries section begin (ends at position " + (Header.entry1Count + Header.entry2Count - 1) + ")");
                }
                
                xr.WriteStartElement("string");

                xr.WriteStartElement("id");
                xr.WriteValue(s.StringID);
                xr.WriteEndElement(); // </id>

                xr.WriteStartElement("position");
                xr.WriteValue(s.position);
                xr.WriteEndElement(); // </position>

                if (s.BitOffset < 0)
                    xr.WriteElementString("data", "-1");
                else
                    xr.WriteElementString("data", s.Data);

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
            xr.Flush();
            xr.Close();
        }

        /// <summary>
        /// Writing data in a normal text format.
        /// </summary>
        /// <remarks>
        /// Currently not used by main application, but it works ok.
        /// </remarks>
        /// <param name="fileName"></param>
        private void SaveToTextFile(string fileName)
        {
            int totalCount = StringRefs.Count();
            int count = 0;
            int lastProgress = -1;

            foreach (var s in StringRefs)
            {
                string line = s.StringID + ": " + s.Data + "\r\n";
                File.AppendAllText(fileName, line);

                int progress = (++count * 100) / totalCount;
                if (progress > lastProgress)
                {
                    lastProgress = progress;
                    OnProgressChanged(lastProgress);
                }
            }
        }

        /* for sorting */
        private static int CompareTlkStringRef(TLKStringRef strRef1, TLKStringRef strRef2)
        {
            int result = strRef1.StringID.CompareTo(strRef2.StringID);
            return result;
        }
    }
}
