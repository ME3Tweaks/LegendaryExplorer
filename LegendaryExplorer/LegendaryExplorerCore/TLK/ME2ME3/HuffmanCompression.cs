using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.TLK.ME1;
using static LegendaryExplorerCore.TLK.ME1.ME1TalkFile;

namespace LegendaryExplorerCore.TLK.ME2ME3
{
    /// <summary>
    /// ME2/ME3 huffman compressor for TLK
    /// </summary>
    public class HuffmanCompression
    {
        private Version _inputFileVersion = null;
        private List<TLKStringRef> _inputData = new();
        private readonly Dictionary<char, int> frequencyCount = new();
        private readonly List<HuffmanNode> _huffmanTree = new();
        private readonly Dictionary<char, BitArray> _huffmanCodes = new();

        private class HuffmanNode
        {
            public char Data;
            public readonly int FrequencyCount;
            public HuffmanNode Left;
            public HuffmanNode Right;

            public int ID;

            public HuffmanNode(char d, int freq)
            {
                Data = d;
                FrequencyCount = freq;
            }

            public HuffmanNode(HuffmanNode left, HuffmanNode right)
            {
                FrequencyCount = left.FrequencyCount + right.FrequencyCount;
                Left = left;
                Right = right;
            }
        }

        /// <summary>
        /// Loads a file into memory and prepares for compressing it to TLK
        /// </summary>
        /// <param name="filePath">Path of the file to open</param>
        /// <param name="debugVersion">Should the StringID be appended to every string</param>
        /// 
        public void LoadInputData(string filePath, bool debugVersion = false)
        {
            _inputData.Clear();
            LoadXmlInputData(filePath, debugVersion);
            _inputData.Sort();
            PrepareHuffmanCoding();
        }

        /// <summary>
        /// Loads multiple files into memory, dedups, and prepares for compressing it to a single TLK
        /// </summary>
        /// <param name="filePaths">Paths of files to load</param>
        /// <param name="debugVersion">Should the StringID be appended to every string</param>
        /// 
        public void LoadInputData(string[] filePaths, bool debugVersion)
        {
            var maleDict = new Dictionary<int, TLKStringRef>();
            var femaleDict = new Dictionary<int, TLKStringRef>();
            foreach (string fileName in filePaths)
            {
                _inputData.Clear();
                var tempMaleDict = new Dictionary<int, TLKStringRef>();
                var tempFemaleDict = new Dictionary<int, TLKStringRef>();
                LoadXmlInputData(fileName, debugVersion);
                foreach (TLKStringRef strRef in _inputData)
                {
                    if (tempMaleDict.ContainsKey(strRef.CalculatedID))
                    {
                        tempFemaleDict[strRef.CalculatedID] = strRef;
                    }
                    else
                    {
                        tempMaleDict[strRef.CalculatedID] = strRef;
                    }
                }

                foreach ((int key, TLKStringRef value) in tempMaleDict)
                {
                    maleDict[key] = value;
                }
                foreach ((int key, TLKStringRef value) in tempFemaleDict)
                {
                    femaleDict[key] = value;
                }
            }

            _inputData = maleDict.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();
            _inputData.AddRange(femaleDict.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value));

            PrepareHuffmanCoding();
        }

        /// <summary>
        /// Loads <see cref="TLKStringRef"/>s and prepares for compressing them to a TLK
        /// </summary>
        /// <param name="tlkEntries">All the <see cref="TLKStringRef"/>s to add to the TLK</param>
        public void LoadInputData(List<TLKStringRef> tlkEntries)
        {
            _inputData = tlkEntries;
            PrepareHuffmanCoding();
        }

        /// <summary>
        /// Saves <paramref name="stringRefs"/> to TLK compressed file format.
        /// </summary>
        /// <param name="filePath">Path to save to</param>
        /// <param name="stringRefs">The <see cref="TLKStringRef"/>s that should be saved to the file</param>
        public static void SaveToTlkFile(string filePath, List<TLKStringRef> stringRefs)
        {
            SaveToTlkStream(stringRefs).WriteToFile(filePath);
        }

        /// <summary>
        /// Saves to TLK compressed file format.
        /// </summary>
        /// <remarks>
        /// Compressed data should be read into memory first, by LoadInputData method.
        /// </remarks>
        /// <param name="filePath">Path to save to</param>
        public void SaveToFile(string filePath)
        {
            SaveToTlkFile(filePath, _inputData);
        }

        /// <summary>
        /// Writes compressed TLK file to a <see cref="MemoryStream"/>
        /// </summary>
        /// <remarks>
        /// Compressed data should be read into memory first, by LoadInputData method.
        /// </remarks>
        /// <returns>A <see cref="MemoryStream"/> containing the compressed TLK file</returns>
        public MemoryStream SaveToStream()
        {
            return SaveToTlkStream(_inputData);
        }

        /// <summary>
        /// Loads data from XML file into memory
        /// </summary>
        /// <param name="filePath">Path of XML file</param>
        /// <param name="debugVersion">Should the StringID be appended to every string</param>
        private void LoadXmlInputData(string filePath, bool debugVersion)
        {
            var xmlReader = new XmlTextReader(filePath);

            /* read and store TLK Tool version, which was used to create the XML file */
            while (xmlReader.Read())
            {
                if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name.Equals("tlkFile", StringComparison.OrdinalIgnoreCase))
                {
                    if (xmlReader.GetAttribute("TLKToolVersion") is string toolVersion)
                    {
                        _inputFileVersion = new Version(toolVersion.Trim('v'));
                    }
                    break;
                }
            }
            if (_inputFileVersion == null || _inputFileVersion >= new Version("2.0.12"))
            {
                int position = 0;
                while (xmlReader.Read())
                {
                    if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name.Equals("String", StringComparison.OrdinalIgnoreCase))
                    {
                        int id = 0;
                        if (!int.TryParse(xmlReader.GetAttribute("id"), out id))
                        {
                            throw new XmlException("id not an integer.", null, xmlReader.LineNumber, xmlReader.LinePosition);
                        }
                        string data = xmlReader.ReadElementContentAsString();

                        data = data.Replace("\r\n", "\n");
                        /* every string should be NULL-terminated */
                        if (id >= 0)
                            data += '\0';
                        /* only add debug info if we are in debug mode and StringID is positive AND it's localizable */
                        if (id >= 0 && debugVersion && (id & 0x8000000) != 0x8000000)
                            _inputData.Add(new TLKStringRef(id, "(#" + id + ") " + data, position, position));
                        else
                            _inputData.Add(new TLKStringRef(id, data, position, position));
                        position++;
                    }
                }
            }
            else //legacy support
            {
                while (xmlReader.Read())
                {
                    if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name.Equals("string", StringComparison.OrdinalIgnoreCase))
                    {
                        int id = 0, position = 0;
                        string data = "";
                        while (xmlReader.Name != "string" || xmlReader.NodeType != XmlNodeType.EndElement)
                        {
                            if (!xmlReader.Read() || xmlReader.NodeType != XmlNodeType.Element)
                                continue;
                            if (xmlReader.Name == "id")
                                id = xmlReader.ReadElementContentAsInt();
                            else if (xmlReader.Name == "position")
                                position = xmlReader.ReadElementContentAsInt();
                            else if (xmlReader.Name == "data")
                                data = xmlReader.ReadString();
                        }
                        data = data.Replace("\r\n", "\n");
                        /* every string should be NULL-terminated */
                        if (id >= 0)
                            data += '\0';
                        /* only add debug info if we are in debug mode and StringID is positive AND it's localizable */
                        if (id >= 0 && debugVersion && (id & 0x8000000) != 0x8000000)
                            _inputData.Add(new TLKStringRef(id, "(#" + id + ") " + data, 0, position));
                        else
                            _inputData.Add(new TLKStringRef(id, data, 0, position));
                    }
                }
            }
            xmlReader.Close();
        }

        /// <summary>
        /// Creates Huffman Tree based on data from memory.
        /// For every character in text data, a corresponding Huffman Code is prepared.
        /// Source: <see href="http://en.wikipedia.org/wiki/Huffman_coding"/>
        /// </summary>
        private void PrepareHuffmanCoding()
        {
            frequencyCount.Clear();
            foreach (var entry in _inputData)
            {
                if (entry.StringID < 0)
                    continue;
                foreach (char c in entry.ASCIIData)
                {
                    if (!frequencyCount.ContainsKey(c))
                        frequencyCount.Add(c, 0);
                    ++frequencyCount[c];
                }
            }

            foreach (var element in frequencyCount)
                _huffmanTree.Add(new HuffmanNode(element.Key, element.Value));

            BuildHuffmanTree();
            BuildCodingArray();
        }

        /// <summary>
        /// Standard implementation of builidng a Huffman Tree
        /// </summary>
        private void BuildHuffmanTree()
        {
            while (_huffmanTree.Count() > 1)
            {
                /* sort Huffman Nodes by frequency */
                _huffmanTree.Sort(CompareNodes);

                HuffmanNode parent = new HuffmanNode(_huffmanTree[0], _huffmanTree[1]);
                _huffmanTree.RemoveAt(0);
                _huffmanTree.RemoveAt(0);
                _huffmanTree.Add(parent);
            }
        }

        /// <summary>
        /// Using Huffman Tree (created with BuildHuffmanTree method), generates a binary code for every character.
        /// </summary>
        private void BuildCodingArray()
        {
            /* stores a binary code */
            List<bool> currentCode = new List<bool>();
            HuffmanNode currenNode = _huffmanTree[0];

            TraverseHuffmanTree(currenNode, currentCode);
        }

        /// <summary>
        /// Recursively traverses Huffman Tree and generates codes
        /// </summary>
        /// <param name="node"></param>
        /// <param name="code"></param>
        private void TraverseHuffmanTree(HuffmanNode node, List<bool> code)
        {
            /* check if both sons are null */
            if (node.Left == node.Right)
            {
                var ba = new BitArray(code.ToArray());
                _huffmanCodes.Add(node.Data, ba);
            }
            else
            {
                /* adds 0 to the code - process left son*/
                code.Add(false);
                TraverseHuffmanTree(node.Left, code);
                code.RemoveAt(code.Count() - 1);

                /* adds 1 to the code - process right son*/
                code.Add(true);
                TraverseHuffmanTree(node.Right, code);
                code.RemoveAt(code.Count() - 1);
            }
        }

        /// <summary>
        /// Converts a Huffman Tree to it's binary representation used by TLK format of Mass Effect 2.
        /// </summary>
        /// <returns></returns>
        private List<int> ConvertHuffmanTreeToBuffer()
        {
            Queue<HuffmanNode> q = new Queue<HuffmanNode>();
            Dictionary<int, HuffmanNode> indices = new Dictionary<int, HuffmanNode>();

            int index = 0;
            q.Enqueue(_huffmanTree[0]);

            while (q.Count > 0)
            {
                HuffmanNode node = q.Dequeue();
                /* if it's a leaf - set it's ID to reflect char data the node contains */
                if (node.Left == node.Right)
                {
                    /* store the char data */
                    node.ID = -1 - node.Data;

                    /* that's how it's going to be decoded when parsing TLK file:
                     * char c = BitConverter.ToChar(BitConverter.GetBytes(0xffff - node.ID), 0); */
                }
                else
                {
                    node.ID = index++;
                    indices.Add(node.ID, node);
                }
                if (node.Right != null)
                    q.Enqueue(node.Right);
                if (node.Left != null)
                    q.Enqueue(node.Left);
            }

            List<int> output = new List<int>();

            foreach (HuffmanNode node in indices.Values)
            {
                output.Add(node.Left.ID);
                output.Add(node.Right.ID);
            }

            return output;
        }

        /// <summary>
        /// Converts bits in a BitArray to an array with bytes.
        /// Such array is ready to be written to a file.
        /// </summary>
        /// <param name="bitsList"></param>
        /// <param name="bitsCount"></param>
        /// <returns></returns>
        private static byte[] BitArrayListToByteArray(List<BitArray> bitsList, int bitsCount)
        {
            const int BITSPERBYTE = 8;

            int bytesize = bitsCount / BITSPERBYTE;
            if (bitsCount % BITSPERBYTE > 0)
                bytesize++;

            byte[] bytes = new byte[bytesize];
            int bytepos = 0;
            int bitsRead = 0;
            byte value = 0;
            byte significance = 1;

            foreach (BitArray bits in bitsList)
            {
                int bitpos = 0;

                while (bitpos < bits.Length)
                {
                    if (bits.Get(bitpos))
                    {
                        value += significance;
                    }
                    ++bitpos;
                    ++bitsRead;
                    if (bitsRead % BITSPERBYTE == 0)
                    {
                        bytes[bytepos] = value;
                        ++bytepos;
                        value = 0;
                        significance = 1;
                        bitsRead = 0;
                    }
                    else
                    {
                        significance <<= 1;
                    }
                }
            }
            if (bitsRead % BITSPERBYTE != 0)
                bytes[bytepos] = value;
            return bytes;
        }

        /// <summary>
        /// For sorting Huffman Nodes
        /// </summary>
        /// <param name="L1"></param>
        /// <param name="L2"></param>
        /// <returns></returns>
        private static int CompareNodes(HuffmanNode L1, HuffmanNode L2)
        {
            return L1.FrequencyCount.CompareTo(L2.FrequencyCount);
        }
        
        /// <summary>
        /// Compresses <paramref name="stringRefs"/> to a TLK file, returned as a <see cref="MemoryStream"/>
        /// </summary>
        /// <param name="stringRefs">The <see cref="TLKStringRef"/>s that should be compressed</param>
        /// <returns>A <see cref="MemoryStream"/> containing the compressed TLK file</returns>
        public static MemoryStream SaveToTlkStream(List<TLKStringRef> stringRefs)
        {
            MemoryStream memStream = MemoryManager.GetMemoryStream();
            var hc = new HuffmanCompression();
            if (stringRefs != null)
            {
                hc._inputData = stringRefs.OrderBy(x => x.CalculatedID).ToList();
                hc.PrepareHuffmanCoding();
            }
            /* converts Huffmann Tree to binary form */
            List<int> treeBuffer = hc.ConvertHuffmanTreeToBuffer();

            /* preparing data and entries for writing to file
             * entries list consists of pairs <String ID, Offset> */
            var binaryData = new List<BitArray>();
            var maleStrings = new Dictionary<int, int>();
            var femaleStrings = new Dictionary<int, int>();
            int offset = 0;

            foreach (var entry in hc._inputData)
            {
                if (entry.StringID < 0)
                {
                    if (!maleStrings.ContainsKey(entry.StringID))
                        maleStrings.Add(entry.StringID, Convert.ToInt32(entry.ASCIIData));
                    else
                        femaleStrings.Add(entry.StringID, Convert.ToInt32(entry.ASCIIData));
                    continue;
                }

                if (!maleStrings.ContainsKey(entry.StringID))
                    maleStrings.Add(entry.StringID, offset);
                else
                    femaleStrings.Add(entry.StringID, offset);

                /* for every character in a string, put it's binary code into data array */
                foreach (char c in entry.ASCIIData)
                {
                    binaryData.Add(hc._huffmanCodes[c]);
                    offset += hc._huffmanCodes[c].Count;
                }
            }

            /* preparing TLK Header */
            const int magic = 7040084; //Tlk\0
            const int ver = 3;
            const int min_ver = 2;
            int entry1Count = maleStrings.Count;
            int entry2Count = femaleStrings.Count;
            int treeNodeCount = treeBuffer.Count / 2;
            int dataLength = offset / 8;
            if (offset % 8 > 0)
                ++dataLength;

            /* writing TLK Header */
            memStream.WriteInt32(magic);
            memStream.WriteInt32(ver);
            memStream.WriteInt32(min_ver);
            memStream.WriteInt32(entry1Count);
            memStream.WriteInt32(entry2Count);
            memStream.WriteInt32(treeNodeCount);
            memStream.WriteInt32(dataLength);

            /* writing entries */
            foreach (var entry in maleStrings)
            {
                memStream.WriteInt32(entry.Key);
                memStream.WriteInt32(entry.Value);
            }
            foreach (var entry in femaleStrings)
            {
                memStream.WriteInt32(entry.Key);
                memStream.WriteInt32(entry.Value);
            }

            /* writing HuffmanTree */
            foreach (int element in treeBuffer)
            {
                memStream.WriteInt32(element);
            }

            /* writing data */
            byte[] data = BitArrayListToByteArray(binaryData, offset);
            memStream.WriteFromBuffer(data);
            return memStream;
        }
    }
}
