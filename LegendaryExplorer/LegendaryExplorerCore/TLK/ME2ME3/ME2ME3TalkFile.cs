using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK.ME1;

namespace LegendaryExplorerCore.TLK.ME2ME3
{
    /// <summary>
    /// Represents a .tlk file, as used by ME2/ME3/LE2/LE3. For ME1/LE1 TLK, use <see cref="ME1TalkFile"/>. Used for reading from .tlk files, and writing an xml representation. 
    /// </summary>
    /// <remarks>Writing a .tlk file is done with the <see cref="HuffmanCompression"/> class</remarks>
    public sealed class ME2ME3TalkFile : ME2ME3TLKBase, ITalkFile
    {
        private Dictionary<int, string> MaleStringRefsTable;
        private Dictionary<int, string> FemaleStringRefsTable;

        /// <summary>
        /// Empty constructor (usable by external libraries)
        /// </summary>
        public ME2ME3TalkFile()
        {
        }

        /// <summary>
        /// Loads a ME2ME3TalkFile from the specified file path.
        /// </summary>
        /// <param name="filepath">File on disk to read from.</param>
        public ME2ME3TalkFile(string filepath)
        {
            LoadTlkData(filepath);
        }

        /// <summary>
        /// Loads a ME2ME3TalkFile from the specified <see cref="Stream"/>. The position must be properly set.
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        public ME2ME3TalkFile(Stream stream)
        {
            LoadTlkDataFromStream(stream);
        }

        /// <inheritdoc/>
        public List<TLKStringRef> StringRefs { get; set; }

        /// <summary>
        /// A delegate used for reporting progress
        /// </summary>
        /// <param name="percentProgress">What percent is finished</param>
        public delegate void ProgressChangedEventHandler(int percentProgress);

        /// <summary>
        /// Reports progress of writing to XML
        /// </summary>
        public event ProgressChangedEventHandler ProgressChanged;
        private void OnProgressChanged(int percentProgress)
        {
            ProgressChanged?.Invoke(percentProgress);
        }

        /// <inheritdoc/>
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
                    if (rawStrings.TryGetValue(sref.BitOffset, out string value))
                    {
                        sref.Data = value;
                    }
                    else
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

        /// <summary>
        /// Gets the string corresponding to the <paramref name="strRefID"/> (wrapped in quotes), if it exists in this file. If it does not, returns <c>"No Data"</c>
        /// </summary>
        /// <param name="strRefID"></param>
        /// <param name="withFileName">Optional: Should the filename be appended to the returned string</param>
        /// <param name="returnNullIfNotFound">Optional: return <c>null</c> instead of <c>"No Data"</c></param>
        /// <param name="noQuotes">Optional: do not wrap the returned string in quotation marks</param>
        /// <param name="male">Optional: if false, gets the female version of the string</param>
        /// <returns></returns>
        public string FindDataById(int strRefID, bool withFileName = false, bool returnNullIfNotFound = false, bool noQuotes = false, bool male = true)
        {
            string data;
            if (male && MaleStringRefsTable.TryGetValue(strRefID, out data) || !male && FemaleStringRefsTable.TryGetValue(strRefID, out data))
            {
                // Todo: Find way to do this faster if possible, maybe like binary search (if TLKs are in order?)
                foreach (TLKStringRef tlkStringRef in StringRefs)
                {
                    if (tlkStringRef.StringID == strRefID)
                    {
                        if (noQuotes)
                        {
                            if(withFileName)
                            {
                                return $"{(data ?? "")} ({Path.GetFileName(FilePath)})";
                            }
                            return data ?? "";
                        }
                        else
                        {
                            data = $"\"{(tlkStringRef.Data ?? "")}\"";
                        }
                        if (withFileName)
                        {
                            data += $" ({Path.GetFileName(FilePath)})";
                        }

                        return data;
                    }
                }
            }

            return returnNullIfNotFound ? null : "No Data";
        }

        /// <summary>
        /// Find the matching string id for the specified string. Returns -1 if not found.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="male">If the search should looking in the male or female table. The male table is the main one.</param>
        /// <returns></returns>
        public int FindIdByData(string value, bool male = true)
        {
            var refs = male ? MaleStringRefsTable : FemaleStringRefsTable;
            var matching = refs.FirstOrDefault(x => x.Value == value);
            if (matching.Value != null) return matching.Key;
            return -1;
        }

        /// <summary>
        /// Saves this TLK object to an XML file
        /// </summary>
        /// <param name="filePath">path to write an XML file to</param>
        public void SaveToXML(string filePath)
        {
            File.Delete(filePath);
            /* for now, it's better not to sort, to preserve original order */
            // StringRefs.Sort(CompareTlkStringRef);

            using var xr = new XmlTextWriter(filePath, Encoding.UTF8);
            WriteXML(xr);
        }

        /// <summary>
        /// Writes TLK data to XML, and returns it as a string
        /// </summary>
        public string WriteXMLString()
        {
            var inputTLK = new StringBuilder();
            using var stringWriter = new StringWriter(inputTLK);
            using var writer = new XmlTextWriter(stringWriter);
            WriteXML(writer);
            return inputTLK.ToString();
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
            xr.WriteAttributeString("TLKToolVersion", LegendaryExplorerCoreLib.GetTLKToolVersion());

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
        /// If the TLK instance has been modified by <see cref="ReplaceString"/> or <see cref="AddString"/>
        /// </summary>
        public bool IsModified { get; set; }

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
                AddString(new TLKStringRef(stringID, newText, 0));
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
