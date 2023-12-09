using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using static LegendaryExplorerCore.TLK.ME1.ME1TalkFile;

namespace LegendaryExplorerCore.TLK.ME1
{
    /// <summary>
    /// ME1/LE1 TLK compressor
    /// </summary>
    public class HuffmanCompression
    {
        private List<TLKStringRef> _inputData = new();
        private readonly Dictionary<char, int> frequencyCount = new();
        private readonly List<HuffmanNode> _huffmanTree = new();
        private readonly Dictionary<char, BitArray> _huffmanCodes = new();

        private class HuffmanNode
        {
            public readonly char Data;
            public readonly int FrequencyCount;
            public readonly HuffmanNode Left;
            public readonly HuffmanNode Right;

            public ushort ID;
            public readonly bool leaf;

            public HuffmanNode(char d, int freq)
            {
                leaf = true;
                Data = d;
                FrequencyCount = freq;
            }

            public HuffmanNode(HuffmanNode left, HuffmanNode right)
            {
                leaf = false;
                FrequencyCount = left.FrequencyCount + right.FrequencyCount;
                Left = left;
                Right = right;
            }
        }

        private readonly struct EncodedString
        {
            public readonly int stringLength;
            public readonly int encodedLength;
            public readonly byte[] binaryData;

            public EncodedString(int _stringLength, int _encodedLength, byte[] _data)
            {
                stringLength = _stringLength;
                encodedLength = _encodedLength;
                binaryData = _data;
            }
        }

        /// <summary>
        /// Loads a file into memory and prepares for compressing it to TLK
        /// </summary>
        /// <param name="filePath">Path of the file to open</param>
        public void LoadInputData(string filePath)
        {
            _inputData.Clear();
            LoadXmlInputData(filePath);
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
        /// Compresses loaded tlk data to a BioTlkFile export
        /// </summary>
        /// <param name="export">The BioTlkFile export to write the data to</param>
        /// <param name="savePackage">Optional: Should the file the export is is be saved to disk</param>
        public void SerializeTalkfileToExport(ExportEntry export, bool savePackage = false)
        {
            // Sort data so it's in proper order
            _inputData = _inputData.OrderBy(x => x.CalculatedID).ToList();

            /* converts Huffmann Tree to binary form */
            byte[] treeBuffer = ConvertHuffmanTreeToBuffer();

            var encodedStrings = new List<EncodedString>();
            int i = 0;
            
            foreach (var entry in _inputData)
            {
                if (entry.Flags == 0)
                {
                    if (entry.StringID > 0)
                        entry.Index = -1;
                    else
                        entry.Index = 0;
                }
                else
                {
                    entry.Index = i;
                    i++;
                    var binaryData = new List<BitArray>();
                    int binaryLength = 0;

                    /* for every character in a string, put it's binary code into data array */
                    foreach (char c in entry.ASCIIData)
                    {
                        binaryData.Add(_huffmanCodes[c]);
                        binaryLength += _huffmanCodes[c].Count;
                    }
                    byte[] buffer = BitArrayListToByteArray(binaryData, binaryLength);
                    encodedStrings.Add(new EncodedString(entry.ASCIIData.Length, buffer.Length, buffer));
                }
            }


            /* writing properties */
            export.WriteProperty(new IntProperty(_inputData.Count, "m_nHashTableSize"));

            using MemoryStream m = MemoryManager.GetMemoryStream();
            /* writing entries */
            m.Write(BitConverter.GetBytes(_inputData.Count), 0, 4);
            foreach (TLKStringRef entry in _inputData)
            {
                m.WriteInt32(entry.StringID);
                m.WriteInt32(entry.Flags);
                m.WriteInt32(entry.Index);
            }

            /* writing HuffmanTree */
            m.Write(treeBuffer, 0, treeBuffer.Length);

            /* writing data */
            m.Write(BitConverter.GetBytes(encodedStrings.Count), 0, 4);
            foreach (EncodedString enc in encodedStrings)
            {
                m.WriteInt32(enc.stringLength);
                m.WriteInt32(enc.encodedLength);
                m.Write(enc.binaryData, 0, enc.encodedLength);
            }

            byte[] buff = m.ToArray();
            export.WriteBinary(buff);
            if (savePackage)
            {
                export.FileRef.Save(export.FileRef.FilePath);
            }
        }

        /// <summary>
        /// Loads tlk data from an XML file
        /// </summary>
        /// <param name="filePath">Path of the xml file</param>
        private void LoadXmlInputData(string filePath)
        {
            var xmlReader = new XmlTextReader(filePath);

            while (xmlReader.Read())
            {
                if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "string")
                {
                    int id = 0, flags = 0;
                    string data = "";
                    while (xmlReader.Name != "string" || xmlReader.NodeType != XmlNodeType.EndElement)
                    {
                        if (!xmlReader.Read() || xmlReader.NodeType != XmlNodeType.Element)
                            continue;
                        if (xmlReader.Name == "id")
                            id = xmlReader.ReadElementContentAsInt();
                        else if (xmlReader.Name == "flags")
                            flags = xmlReader.ReadElementContentAsInt();
                        else if (xmlReader.Name == "data")
                            data = xmlReader.ReadString();
                    }
                    data = data.Replace("\r\n", "\n");
                    /* every string should be NULL-terminated */
                    if (id > 0)
                        data += '\0';
                    _inputData.Add(new TLKStringRef(id, data, flags));
                }
            }
            xmlReader.Close();
        }

        /// <summary>
        /// Creates Huffman Tree based on data from memory.
        /// For every character in text data, a corresponding Huffman Code is prepared.
        /// Source: http://en.wikipedia.org/wiki/Huffman_coding
        /// </summary>
        private void PrepareHuffmanCoding()
        {
            frequencyCount.Clear();
            foreach (var entry in _inputData)
            {
                if (entry.StringID <= 0)
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
        /// Standard implementation of building a Huffman Tree
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
        /// Converts a Huffman Tree to it's binary representation used by TLK format of Mass Effect 1.
        /// </summary>
        /// <returns></returns>
        private byte[] ConvertHuffmanTreeToBuffer()
        {
            var nodes = new List<HuffmanNode>();
            var q = new Queue<HuffmanNode>();

            ushort index = 0;
            q.Enqueue(_huffmanTree[0]);

            while (q.Count > 0)
            {
                HuffmanNode node = q.Dequeue();
                nodes.Add(node);
                node.ID = index;
                index++;
                if (node.Right != null)
                {
                    q.Enqueue(node.Right);
                }
                if (node.Left != null)
                {
                    q.Enqueue(node.Left);
                }
            }

            var output = new List<byte>();
            output.AddRange(BitConverter.GetBytes((int)index));
            foreach (HuffmanNode node in nodes)
            {
                if (node.leaf)
                {
                    output.Add(1);
                    output.AddRange(BitConverter.GetBytes(node.Data));
                }
                else
                {
                    output.Add(0);
                    output.AddRange(BitConverter.GetBytes(node.Right.ID));
                    output.AddRange(BitConverter.GetBytes(node.Left.ID));
                }
            }

            return output.ToArray();
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
    }
}
