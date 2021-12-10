using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using LegendaryExplorerCore.Gammtek.IO;
using static LegendaryExplorerCore.TLK.ME1.ME1TalkFile;

namespace LegendaryExplorerCore.TLK.ME2ME3
{
    public sealed class TalkFile : ME2ME3TLKBase
    {
        private Dictionary<int, string> MaleStringRefsTable;
        private Dictionary<int, string> FemaleStringRefsTable;
        public List<TLKStringRef> StringRefs;

        public delegate void ProgressChangedEventHandler(int percentProgress);
        public event ProgressChangedEventHandler ProgressChanged;
        private void OnProgressChanged(int percentProgress)
        {
            ProgressChanged?.Invoke(percentProgress);
        }

        /// <summary>
        /// Loads TLK data from a stream. The position must be properly set.
        /// </summary>
        /// <param name="fs"></param>
        public override void LoadTlkDataFromStream(Stream fs)
        {
            //Magic: "Tlk " on Little Endian
            EndianReader r = EndianReader.SetupForReading(fs, 0x006B6C54, out int _);
            r.Position = 0;
            Header = new TLKHeader(r);

            int strRefCount = Header.MaleEntryCount + Header.FemaleEntryCount;
            //DebugTools.PrintHeader(Header);

            /* **************** STEP TWO ****************
             *  -- read and store Huffman Tree nodes -- 
             */
            /* jumping to the beginning of Huffmann Tree stored in TLK file */
            long pos = r.BaseStream.Position;
            r.BaseStream.Seek(pos + strRefCount * 8, SeekOrigin.Begin);


            var characterTree = new HuffmanNode[Header.treeNodeCount];
            for (int i = 0; i < Header.treeNodeCount; i++)
                characterTree[i] = new HuffmanNode(r);

            /* **************** STEP THREE ****************
             *  -- read all of coded data into memory -- 
             * and store it as raw bits for further processing */
            var bits = new TLKBitArray(r.BaseStream, Header.dataLen);

            /* rewind BinaryReader just after the Header
             * at the beginning of TLK Entries data */
            r.BaseStream.Seek(pos, SeekOrigin.Begin);

            /* **************** STEP FOUR ****************
             * -- decode (basing on Huffman Tree) raw bits data into actual strings --
             * and store them in a Dictionary<int, string> where:
             *   int: bit offset of the beginning of data (offset starting at 0 and counted for Bits array)
             *        so offset == 0 means the first bit in Bits array
             *   string: actual decoded string */
            var rawStrings = new Dictionary<int, string>(strRefCount); //strRefCount will be either the correct capacity or a slight overestimate, due to the possibility of duplicate strings
            int offset = 0;
            // int maxOffset = 0;
            var builder = new StringBuilder(); //reuse the same stringbuilder to avoid allocations
            while (offset < bits.Length)
            {
                int key = offset;
                // if (key > maxOffset)
                // maxOffset = key;
                /* read the string and update 'offset' variable to store NEXT string offset */
                string s = GetString(ref offset, builder, bits, characterTree);
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
            StringRefs = new List<TLKStringRef>(strRefCount);
            MaleStringRefsTable = new Dictionary<int, string>(Header.MaleEntryCount);
            FemaleStringRefsTable = new Dictionary<int, string>(Header.FemaleEntryCount);
            for (int i = 0; i < strRefCount; i++)
            {
                var sref = new TLKStringRef(r, false)
                {
                    Index = i
                };
                if (sref.BitOffset >= 0)
                {
                    if (!rawStrings.ContainsKey(sref.BitOffset))
                    {
                        int tmpOffset = sref.BitOffset;
                        string partString = GetString(ref tmpOffset, builder, bits, characterTree);

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
                if (i < Header.MaleEntryCount)
                {
                    MaleStringRefsTable.Add(sref.StringID, sref.Data);
                }
                else
                {
                    FemaleStringRefsTable[sref.StringID] = sref.Data;
                }
            }
            r.Close();
        }

        public string findDataById(int strRefID, bool withFileName = false, bool returnNullIfNotFound = false, bool noQuotes = false, bool male = true)
        {
            string data;
            if (male && MaleStringRefsTable.TryGetValue(strRefID, out data) || !male && FemaleStringRefsTable.TryGetValue(strRefID, out data))
            {
                if (noQuotes)
                    return data;

                var retdata = "\"" + data + "\"";
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

            using var xr = new XmlTextWriter(fileName, Encoding.UTF8);
            WriteXML(xr);
        }

        public string WriteXMLString()
        {
            var InputTLK = new StringBuilder();
            using var stringWriter = new StringWriter(InputTLK);
            using var writer = new XmlTextWriter(stringWriter);
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
            if (MaleStringRefsTable.ContainsKey(stringID))
            {
                IsModified = true;
                MaleStringRefsTable[stringID] = newText;
                FemaleStringRefsTable.Remove(stringID);
                foreach (TLKStringRef stringRef in StringRefs.Where(strRef => strRef.StringID == stringID))
                {
                    stringRef.Data = newText;
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
        private static int CompareTlkStringRef(TLKStringRef strRef1, TLKStringRef strRef2)
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
            if (MaleStringRefsTable.ContainsKey(sref.StringID))
            {
                FemaleStringRefsTable[sref.StringID] = sref.Data;
            }
            else
            {
                MaleStringRefsTable.Add(sref.StringID, sref.Data);
            }

            IsModified = true;
        }
    }
}
