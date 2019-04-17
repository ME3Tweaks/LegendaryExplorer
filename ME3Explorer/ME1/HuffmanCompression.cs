using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using ME3Explorer;
using ME3Explorer.Packages;
using static ME1Explorer.Unreal.Classes.TalkFile;

namespace ME1Explorer
{
    class HuffmanCompression
    {
        private List<TLKStringRef> _inputData = new List<TLKStringRef>();
        private Dictionary<char, int> frequencyCount = new Dictionary<char, int>();
        private List<HuffmanNode> _huffmanTree = new List<HuffmanNode>();
        private Dictionary<char, BitArray> _huffmanCodes = new Dictionary<char, BitArray>();

        //private class TLKEntry
        //{
        //    public int StringID;
        //    public int Flags;
        //    public string data;
        //    public int index;

        //    public TLKEntry(int StringID, int flags, string data)
        //    {
        //        this.StringID = StringID;
        //        this.Flags = flags;
        //        this.data = data;
        //        index = -1;
        //    }
        //}

        private class HuffmanNode
        {
            public char Data;
            public readonly int FrequencyCount;
            public HuffmanNode Left;
            public HuffmanNode Right;

            public ushort ID;
            public bool leaf;

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

        private struct EncodedString
        {
            public int stringLength;
            public int encodedLength;
            public byte[] binaryData;


            public EncodedString(int _stringLength, int _encodedLength, byte[] _data)
            {
                stringLength = _stringLength;
                encodedLength = _encodedLength;
                binaryData = _data;
            }
        }

        private class PendingEncodedString
        {
            public string originalString;
            public List<BitArray> huffmanCodes;


            public PendingEncodedString(string originalString, List<BitArray> huffmanCodes)
            {
                this.originalString = originalString;
                this.huffmanCodes = huffmanCodes;
            }

            public BitArray GetFullStringAsBits()
            {
                int fullsize = 0;
                huffmanCodes.ForEach(x => fullsize += x.Count); //get size of resulting bitarray

                var result = new BitArray(fullsize);

                var first = huffmanCodes[0];
                for (int i = 1; i < huffmanCodes.Count; i++)
                {
                    first = first.Append(huffmanCodes[i]);
                }

                return first;
                //foreach (var value in first.Cast<bool>().Concat(second.Cast<bool>()))
                //{
                //    result[i++] = value;
                //}
            }
        }

        /// <summary>
        /// Loads a file into memory and prepares for compressing it to TLK
        /// </summary>
        /// <param name="fileName"></param>
        public void LoadInputData(string fileName)
        {
            _inputData.Clear();
            LoadXmlInputData(fileName);
            PrepareHuffmanCoding();
        }

        /// <summary>
        /// Loads a file into memory and prepares for compressing it to TLK
        /// </summary>
        /// <param name="fileName"></param>
        public void LoadInputData(List<TLKStringRef> tlkEntries)
        {
            _inputData = tlkEntries;
            PrepareHuffmanCoding();
        }

        public void serializeTLKStrListToExport(IExportEntry export, bool savePackage = false)
        {
            if (export.FileRef.Game != MEGame.ME1)
            {
                throw new Exception("Cannot save a ME1 TLK to a game that is not Mass Effect 1.");
            }
            serializeTalkfileToExport(export.FileRef as ME1Package, export.Index, savePackage);
        }

        public void serializeTalkfileToExport(ME1Package pcc, int Index, bool savePackage = false)
        {
            /* converts Huffmann Tree to binary form */
            byte[] treeBuffer = ConvertHuffmanTreeToBuffer();

            List<EncodedString> encodedStrings = new List<EncodedString>();
            List<PendingEncodedString> pendingEncodedStrings = new List<PendingEncodedString>();

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
                    List<KeyValuePair<char, BitArray>> huffmanCodesForthisStr = new List<KeyValuePair<char, BitArray>>();
                    int numberOfHuffmanBytes = 0;

                    bool finalx = false;
                    if (entry == _inputData[_inputData.Count - 1])
                    {
                        finalx = true;
                        //Debugger.Break();
                    }
                    /* for every character in a string, put it's binary code into data array */
                    foreach (char c in entry.ASCIIData)
                    {
                        var code = _huffmanCodes[c];

                        if (entry == _inputData[_inputData.Count - 1])
                        {
                            if (finalx && entry.ASCIIData.Last() == c)
                            {
                                //Debugger.Break();
                            }
                            var letterDisp = c == '\0' ? "[NullTerm \\0]" : c.ToString();
                            Debug.WriteLine("Adding: " + letterDisp + " " + code.DebugString());
                        }
                        
                        huffmanCodesForthisStr.Add(new KeyValuePair<char, BitArray>(c, code));
                        numberOfHuffmanBytes += code.Count;
                    }
                    byte[] buffer = BitArrayListToByteArray(huffmanCodesForthisStr, numberOfHuffmanBytes, finalx);
                    encodedStrings.Add(new EncodedString(entry.ASCIIData.Length, buffer.Length, buffer));
                    pendingEncodedStrings.Add(new PendingEncodedString(entry.ASCIIData, huffmanCodesForthisStr.Select(x => x.Value).ToList()));
                }
            }

            /* get properties from object we're replacing*/
            byte[] properties = pcc.Exports[Index].Data.Take(40).ToArray();

            MemoryStream m = new MemoryStream();

            /* writing properties */
            m.Write(properties, 0, 40);
            m.Seek(0x1C, SeekOrigin.Begin);
            m.Write(BitConverter.GetBytes(_inputData.Count), 0, 4);
            m.Seek(0, SeekOrigin.End);

            /* writing entries */
            m.Write(BitConverter.GetBytes(_inputData.Count), 0, 4);
            //foreach (TLKStringRef entry in _inputData)
            for (int z = 0; z < _inputData.Count; z++)
            {
                TLKStringRef entry = _inputData[z];
                if (z == _inputData.Count - 1)
                {
                    //Debugger.Break();
                }
                m.Write(BitConverter.GetBytes(entry.StringID), 0, 4);
                m.Write(BitConverter.GetBytes(entry.Flags), 0, 4);
                m.Write(BitConverter.GetBytes(entry.Index), 0, 4);
            }

            /* writing HuffmanTree */
            m.Write(treeBuffer, 0, treeBuffer.Length);

            /* writing data */
            m.Write(BitConverter.GetBytes(encodedStrings.Count), 0, 4);
            MemoryStream encodedStringsDEBUG = new MemoryStream();

            //OLD
            for (int z = 0; z < encodedStrings.Count; z++)
            {
                EncodedString enc = encodedStrings[z];
                if (z == encodedStrings.Count - 1)
                {
                    Debug.WriteLine("Writing final encoded string len uncomp at 0x" + encodedStringsDEBUG.Position.ToString("X8"));
                    //Debugger.Break();
                }
                m.Write(BitConverter.GetBytes(enc.stringLength), 0, 4);
                encodedStringsDEBUG.Write(BitConverter.GetBytes(enc.stringLength), 0, 4);
                if (z == encodedStrings.Count - 1)
                {
                    Debug.WriteLine("Writing final encoded string len comp at 0x" + encodedStringsDEBUG.Position.ToString("X8"));
                }
                m.Write(BitConverter.GetBytes(enc.encodedLength), 0, 4);
                encodedStringsDEBUG.Write(BitConverter.GetBytes(enc.encodedLength), 0, 4);
                if (z == encodedStrings.Count - 1)
                {
                    Debug.WriteLine("Writing final encoded string at 0x" + encodedStringsDEBUG.Position.ToString("X8"));
                }
                m.Write(enc.binaryData, 0, enc.encodedLength);
                encodedStringsDEBUG.Write(enc.binaryData, 0, enc.encodedLength);
            }

            //NEW
            //for (int z = 0; z < pendingEncodedStrings.Count; z++)
            //{
            //    PendingEncodedString enc = pendingEncodedStrings[z];
            //    if (z == pendingEncodedStrings.Count - 1)
            //    {
            //        Debug.WriteLine("Writing final encoded string len uncomp at 0x" + m.Position.ToString("X8"));
            //        //Debugger.Break();
            //    }
            //    m.Write(BitConverter.GetBytes(enc.originalString.Length), 0, 4);
            //    //encodedStringsDEBUG.Write(BitConverter.GetBytes(enc.stringLength), 0, 4);
            //    if (z == encodedStrings.Count - 1)
            //    {
            //        Debug.WriteLine("Writing final encoded string len comp at 0x" + m.Position.ToString("X8"));
            //    }

            //    var encodedBytes = enc.GetFullStringAsBits();

            //    m.Write(BitConverter.GetBytes(encodedBytes.Length), 0, 4);
            //    //encodedStringsDEBUG.Write(BitConverter.GetBytes(enc.encodedLength), 0, 4);
            //    if (z == encodedStrings.Count - 1)
            //    {
            //        Debug.WriteLine("Writing final encoded string at 0x" + m.Position.ToString("X8"));
            //        Debug.WriteLine(encodedBytes.DebugString()); 
            //    }
            //    var array = BitArrayToByteArray(encodedBytes);
            //    m.Write(array, 0, array.Count());
            //    //encodedStringsDEBUG.Write(enc.binaryData, 0, enc.encodedLength);
            //}

            byte[] buff = m.ToArray();
            File.WriteAllBytes(@"C:\users\public\serializing-encodedstrings.bin", m.ToArray());
            pcc.Exports[Index].Data = buff;
            if (savePackage)
            {
                pcc.save(pcc.FileName);
            }
        }

        public static byte[] BitArrayToByteArray(BitArray bits)
        {
            byte[] ret = new byte[(bits.Length - 1) / 8 + 1];
            bits.CopyTo(ret, 0);
            return ret;
        }

        /// <summary>
        /// Loads data from XML file into memory
        /// </summary>
        /// <param name="fileName"></param>
        private void LoadXmlInputData(string fileName)
        {
            var xmlReader = new XmlTextReader(fileName);

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
                    _inputData.Add(new TLKStringRef(id, flags, data));
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
                BitArray ba = new BitArray(code.ToArray());
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
            List<HuffmanNode> nodes = new List<HuffmanNode>();
            Queue<HuffmanNode> q = new Queue<HuffmanNode>();

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

            List<byte> output = new List<byte>();
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
        /// <param name="huffmanCodes"></param>
        /// <param name="totalNumBits"></param>
        /// <returns></returns>
        private static byte[] BitArrayListToByteArray(List<KeyValuePair<char, BitArray>> huffmanCodesMap, int totalNumBits, bool debug = false)
        {
            const int BITSPERBYTE = 8;

            int finalEncodedSize = totalNumBits / BITSPERBYTE;
            if (totalNumBits % BITSPERBYTE > 0)
                finalEncodedSize++;

            byte[] bytes = new byte[finalEncodedSize];
            int bytepos = 0;
            int bitsRead = 0;
            byte value = 0;
            byte significance = 1;

            foreach (KeyValuePair<char, BitArray> codePair in huffmanCodesMap)
            {
                int bitpos = 0;
                if (debug)
                {
                    Debug.WriteLine("Compacting " + codePair.Key);
                }
                while (bitpos < codePair.Value.Length)
                {
                    if (codePair.Value[bitpos])
                    {
                        //If the next significant bit is set, add this to the current byte value to set the bits
                        value += significance;
                    }
                    ++bitpos;
                    ++bitsRead;
                    //Write the byte out as this byte has filled
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
                        //Keep shifting into the current byte
                        significance <<= 1;
                    }
                }
            }
            //Write any remaining byte out
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
