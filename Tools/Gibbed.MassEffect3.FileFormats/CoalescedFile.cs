/* Copyright (c) 2012 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gibbed.IO;

namespace Gibbed.MassEffect3.FileFormats
{
    public class CoalescedFile
    {
        public Endian Endian;
        public uint Version;
        public List<Coalesced.File> Files = new List<Coalesced.File>();

        public void Serialize(Stream output)
        {
            var endian = this.Endian;

            const uint headerSize = 32;
            output.WriteValueU32(0x42424947, endian);
            output.WriteValueU32(this.Version, endian);

            var keys = new List<string>() {""};

            int maxValueLength = 0;
            var blob = new StringBuilder();
            foreach (var file in this.Files)
            {
                keys.Add(file.Name);
                foreach (var section in file.Sections)
                {
                    keys.Add(section.Key);
                    foreach (var value in section.Value)
                    {
                        keys.Add(value.Key);
                        foreach (var item in value.Value)
                        {
                            if (item.Value != null)
                            {
                                blob.Append(item.Value + '\0');
                                maxValueLength = Math.Max(maxValueLength, item.Value.Length);
                            }
                        }
                    }
                }
            }

            var huffmanEncoder = new Huffman.Encoder();
            huffmanEncoder.Build(blob.ToString());

            keys = keys.Distinct().OrderBy(k => k.HashCrc32()).ToList();
            int maxKeyLength = keys.Max(k => k.Length);

            uint stringTableSize;
            using (var data = new MemoryStream())
            {
                data.Position = 4;
                data.WriteValueS32(keys.Count, endian);

                data.Position = 4 + 4 + (8 * keys.Count);
                var offsets = new List<KeyValuePair<uint, uint>>();
                foreach (var key in keys)
                {
                    var offset = (uint)data.Position;
                    data.WriteValueU16((ushort)key.Length, endian);
                    data.WriteString(key, Encoding.UTF8);
                    offsets.Add(new KeyValuePair<uint, uint>(key.HashCrc32(), offset));
                }

                data.Position = 8;
                foreach (var kv in offsets)
                {
                    data.WriteValueU32(kv.Key, endian);
                    data.WriteValueU32(kv.Value - 8, endian);
                }

                data.Position = 0;
                data.WriteValueU32((uint)data.Length, endian);

                data.Position = 0;
                stringTableSize = (uint)data.Length;

                output.Seek(headerSize, SeekOrigin.Begin);
                output.WriteFromStream(data, data.Length);
            }

            uint huffmanSize;
            using (var data = new MemoryStream())
            {
                var pairs = huffmanEncoder.GetPairs();
                data.WriteValueU16((ushort)pairs.Length, endian);
                foreach (var pair in pairs)
                {
                    data.WriteValueS32(pair.Left, endian);
                    data.WriteValueS32(pair.Right, endian);
                }

                data.Position = 0;
                huffmanSize = (uint)data.Length;

                output.Seek(headerSize + stringTableSize, SeekOrigin.Begin);
                output.WriteFromStream(data, data.Length);
            }

            var bits = new BitArray(huffmanEncoder.TotalBits);
            var bitOffset = 0;

            uint indexSize;
            using (var index = new MemoryStream())
            {
                var fileDataOffset = 2 + (this.Files.Count * 6);

                var files = new List<KeyValuePair<ushort, int>>();
                foreach (var file in this.Files.OrderBy(f => keys.IndexOf(f.Name)))
                {
                    files.Add(new KeyValuePair<ushort, int>(
                        (ushort)keys.IndexOf(file.Name), fileDataOffset));

                    var sectionDataOffset = 2 + (file.Sections.Count * 6);

                    var sections = new List<KeyValuePair<ushort, int>>();
                    foreach (var section in file.Sections.OrderBy(s => keys.IndexOf(s.Key)))
                    {
                        sections.Add(new KeyValuePair<ushort, int>(
                            (ushort)keys.IndexOf(section.Key), sectionDataOffset));

                        var valueDataOffset = 2 + (section.Value.Count * 6);

                        var values = new List<KeyValuePair<ushort, int>>();
                        foreach (var value in section.Value.OrderBy(v => keys.IndexOf(v.Key)))
                        {
                            index.Position = fileDataOffset + sectionDataOffset + valueDataOffset;

                            values.Add(new KeyValuePair<ushort, int>(
                                (ushort)keys.IndexOf(value.Key), valueDataOffset));

                            index.WriteValueU16((ushort)value.Value.Count, endian);
                            valueDataOffset += 2;

                            foreach (var item in value.Value)
                            {
                                if (item.Type == 1)
                                {
                                    index.WriteValueS32((1 << 29) | bitOffset, endian);
                                }
                                else if (item.Type == 0 || item.Type == 2 || item.Type == 3 || item.Type == 4)
                                {
                                    index.WriteValueS32((item.Type << 29) | bitOffset, endian);
                                    bitOffset += huffmanEncoder.Encode((item.Value ?? "") + '\0', bits, bitOffset);
                                }
                                valueDataOffset += 4;
                            }
                        }

                        index.Position = fileDataOffset + sectionDataOffset;
                        
                        index.WriteValueU16((ushort)values.Count, endian);
                        sectionDataOffset += 2;

                        foreach (var value in values)
                        {
                            index.WriteValueU16(value.Key, endian);
                            index.WriteValueS32(value.Value, endian);
                            sectionDataOffset += 6;
                        }

                        sectionDataOffset += valueDataOffset;
                    }

                    index.Position = fileDataOffset;

                    index.WriteValueU16((ushort)sections.Count, endian);
                    fileDataOffset += 2;

                    foreach (var section in sections)
                    {
                        index.WriteValueU16(section.Key, endian);
                        index.WriteValueS32(section.Value, endian);
                        fileDataOffset += 6;
                    }

                    fileDataOffset += sectionDataOffset;
                }

                index.Position = 0;

                index.WriteValueU16((ushort)files.Count, endian);
                foreach (var file in files)
                {
                    index.WriteValueU16(file.Key, endian);
                    index.WriteValueS32(file.Value, endian);
                }

                index.Position = 0;
                indexSize = (uint)index.Length;

                output.Seek(headerSize + stringTableSize + huffmanSize, SeekOrigin.Begin);
                output.WriteFromStream(index, index.Length);
            }

            output.Seek(headerSize + stringTableSize + huffmanSize + indexSize, SeekOrigin.Begin);
            output.WriteValueS32(bits.Length, endian);
            var bytes = new byte[(bits.Length - 1) / 8 + 1];
            bits.CopyTo(bytes, 0);
            output.WriteBytes(bytes);

            output.Seek(8, SeekOrigin.Begin);
            output.WriteValueS32(maxKeyLength, endian);
            output.WriteValueS32(maxValueLength, endian);
            output.WriteValueU32(stringTableSize, endian);
            output.WriteValueU32(huffmanSize, endian);
            output.WriteValueU32(indexSize, endian);
            output.WriteValueS32(bytes.Length, endian);

            output.Seek(0, SeekOrigin.Begin);
            output.WriteValueU32(0x666D726D, endian);
        }

        public void Deserialize(Stream input)
        {
            var magic = input.ReadValueU32(Endian.Little);
            if (magic != 0x666D726D && // fmrm
                magic.Swap() != 0x666D726D)
            {
                throw new FormatException();
            }
            var endian = magic == 0x666D726D ? Endian.Little : Endian.Big;

            var version = input.ReadValueU32(endian);
            if (version != 1)
            {
                throw new FormatException();
            }
            this.Version = version;

            /*var maxKeyLength =*/ input.ReadValueS32(endian);
            var maxValueLength = input.ReadValueS32(endian);

            var stringTableSize = input.ReadValueU32(endian);
            var huffmanSize = input.ReadValueU32(endian);
            var indexSize = input.ReadValueU32(endian);
            var dataSize = input.ReadValueU32(endian);

            var strings = new List<KeyValuePair<uint, string>>();
            using (var data = input.ReadToMemoryStream(stringTableSize))
            {
                var localStringTableSize = data.ReadValueU32(endian);
                if (localStringTableSize != stringTableSize)
                {
                    throw new FormatException();
                }

                var count = data.ReadValueU32(endian);

                var offsets = new List<KeyValuePair<uint, uint>>();
                for (uint i = 0; i < count; i++)
                {
                    var hash = data.ReadValueU32(endian);
                    var offset = data.ReadValueU32(endian);
                    offsets.Add(new KeyValuePair<uint, uint>(hash, offset));
                }

                foreach (var kv in offsets)
                {
                    var hash = kv.Key;
                    var offset = kv.Value;

                    data.Seek(8 + offset, SeekOrigin.Begin);
                    var length = data.ReadValueU16(endian);
                    var text = data.ReadString(length, Encoding.UTF8);

                    if (text.HashCrc32() != hash)
                    {
                        throw new InvalidOperationException();
                    }

                    strings.Add(new KeyValuePair<uint, string>(hash, text));
                }
            }

            Huffman.Pair[] huffmanTree;
            using (var data = input.ReadToMemoryStream(huffmanSize))
            {
                var count = data.ReadValueU16(endian);
                huffmanTree = new Huffman.Pair[count];
                for (ushort i = 0; i < count; i++)
                {
                    var left = data.ReadValueS32(endian);
                    var right = data.ReadValueS32(endian);
                    huffmanTree[i] = new Huffman.Pair(left, right);
                }
            }

            using (var index = input.ReadToMemoryStream(indexSize))
            {
                var totalBits = input.ReadValueS32(endian);
                var data = input.ReadBytes(dataSize);
                var bitArray = new BitArray(data) { Length = totalBits };

                var files = new List<KeyValuePair<string, uint>>();
                var fileCount = index.ReadValueU16(endian);
                for (ushort i = 0; i < fileCount; i++)
                {
                    var nameIndex = index.ReadValueU16(endian);
                    var name = strings[nameIndex].Value;
                    var offset = index.ReadValueU32(endian);
                    files.Add(new KeyValuePair<string, uint>(name, offset));
                }

                foreach (var fileInfo in files.OrderBy(f => f.Key))
                {
                    var file = new Coalesced.File() { Name = fileInfo.Key };

                    index.Seek(fileInfo.Value, SeekOrigin.Begin);
                    var sectionCount = index.ReadValueU16(endian);
                    var sections = new List<KeyValuePair<string, uint>>();
                    for (ushort i = 0; i < sectionCount; i++)
                    {
                        var nameIndex = index.ReadValueU16(endian);
                        var name = strings[nameIndex].Value;
                        var offset = index.ReadValueU32(endian);
                        sections.Add(new KeyValuePair<string, uint>(name, offset));
                    }

                    foreach (var sectionInfo in sections.OrderBy(s => s.Key))
                    {
                        var section = new Dictionary<string, List<Coalesced.Entry>>();

                        index.Seek(fileInfo.Value + sectionInfo.Value, SeekOrigin.Begin);
                        var valueCount = index.ReadValueU16(endian);
                        var values = new List<KeyValuePair<string, uint>>();
                        for (ushort i = 0; i < valueCount; i++)
                        {
                            var nameIndex = index.ReadValueU16(endian);
                            var name = strings[nameIndex].Value;
                            var offset = index.ReadValueU32(endian);
                            values.Add(new KeyValuePair<string, uint>(name, offset));
                        }

                        foreach (var valueInfo in values.OrderBy(v => v.Key))
                        {
                            var value = new List<Coalesced.Entry>();

                            index.Seek(fileInfo.Value + sectionInfo.Value + valueInfo.Value, SeekOrigin.Begin);
                            var itemCount = index.ReadValueU16(endian);

                            for (ushort i = 0; i < itemCount; i++)
                            {
                                var offset = index.ReadValueS32(endian);

                                var type = (offset & 0xE0000000) >> 29;
                                if (type == 1)
                                {
                                    value.Add(new Coalesced.Entry(1, null));
                                }
                                else if (type == 0 || type == 2 || type == 3 || type == 4)
                                {
                                    offset &= 0x1FFFFFFF;
                                    var text = Huffman.Decoder.Decode(
                                        huffmanTree, bitArray, offset, maxValueLength);
                                    value.Add(new Coalesced.Entry(2, text));
                                }
                                else
                                {
                                    throw new NotImplementedException();
                                }
                            }

                            section.Add(valueInfo.Key, value);
                        }

                        file.Sections.Add(sectionInfo.Key, section);
                    }

                    this.Files.Add(file);
                }
            }

            this.Endian = endian;
        }
    }
}
