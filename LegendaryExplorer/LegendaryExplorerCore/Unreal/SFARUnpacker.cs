/*
 * Copyright (C) 2015-2018 Pawel Kolodziejski
 * Copyright (C) 2018 Mgamerz
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorerCore.Unreal
{
    public class SFARUnpacker : INotifyCollectionChanged, INotifyPropertyChanged
    {
        const uint SfarTag = 0x53464152; // 'SFAR'
        const uint SfarVersion = 0x00010000;
        const uint LZMATag = 0x6c7a6d61; // 'lzma'
        const uint HeaderSize = 0x20;
        const uint EntryHeaderSize = 0x1e;
        readonly byte[] FileListHash = { 0xb5, 0x50, 0x19, 0xcb, 0xf9, 0xd3, 0xda, 0x65, 0xd5, 0x5b, 0x32, 0x1c, 0x00, 0x19, 0x69, 0x7c };
        const long MaxBlockSize = 0x00010000;
        int filenamesIndex;
        public List<DLCEntry> filesList { get; private set; }
        uint maxBlockSize;
        List<ushort> blockSizes;
        public string filePath;

        public long UncompressedSize { get; private set; }
        public int GetNumberOfFiles => (int)TotalFilesInDLC;

        /// <summary>
        /// Allow cancel unpack files from DLC and revert to state before unpack
        /// </summary>
        public volatile bool UnpackCanceled;

        /// <summary>
        /// Current text describing what the overall status is for this DLC
        /// </summary>
        public string CurrentOverallStatus { get; set; }

        /// <summary>
        /// Current text describing what is currently going on with this DLC.
        /// </summary>
        public string CurrentStatus { get; set; }

        /// <summary>
        /// If the current status is indeterminate or not (e.g. archive is loading into memory)
        /// </summary>
        public bool IndeterminateState { get; set; }

        /// <summary>
        /// The total number of files in this DLC
        /// </summary>
        public uint TotalFilesInDLC { get; private set; }

        /// <summary>
        /// Current number of files that have been extracted from this DLC
        /// </summary>
        public int CurrentFilesProcessed { get; set; }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// Current over progress percent for this DLC's extraction
        /// </summary>
        public int CurrentProgress { get; set; }

        public struct DLCEntry
        {
            public byte[] filenameHash;
            public string filenamePath;
            public long uncomprSize;
            public int compressedBlockSizesIndex;
            public uint numBlocks;
            public long dataOffset;
        }

        public SFARUnpacker(string path)
        {
            if (!File.Exists(path))
                throw new Exception($"File to unpack doesn't exist: {path}");
            filePath = path;
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                LoadHeader(stream);
            }
        }

        private void LoadHeader(Stream stream)
        {
            uint tag = stream.ReadUInt32();
            if (tag != SfarTag)
                throw new Exception("Wrong SFAR tag");
            uint sfarVersion = stream.ReadUInt32();
            if (sfarVersion != SfarVersion)
                throw new Exception("Wrong SFAR version");

            uint dataOffset = stream.ReadUInt32();
            uint entriesOffset = stream.ReadUInt32();
            TotalFilesInDLC = stream.ReadUInt32();
            uint sizesArrayOffset = stream.ReadUInt32();
            maxBlockSize = stream.ReadUInt32();
            uint compressionTag = stream.ReadUInt32();
            if (compressionTag != LZMATag)
                throw new Exception("Not LZMA compression for SFAR file");

            uint numBlockSizes = 0;
            stream.JumpTo(entriesOffset);
            filesList = new List<DLCEntry>();
            for (int i = 0; i < TotalFilesInDLC; i++)
            {
                DLCEntry file = new DLCEntry
                {
                    filenameHash = stream.ReadToBuffer(16),
                    compressedBlockSizesIndex = stream.ReadInt32(),
                    uncomprSize = stream.ReadUInt32()
                };
                file.uncomprSize |= (long)stream.ReadByte() << 32;
                file.dataOffset = stream.ReadUInt32();
                file.dataOffset |= (long)stream.ReadByte() << 32;
                file.numBlocks = (uint)((file.uncomprSize + maxBlockSize - 1) / maxBlockSize);
                filesList.Add(file);
                numBlockSizes += file.numBlocks;
                UncompressedSize += file.uncomprSize;
            }

            stream.JumpTo(sizesArrayOffset);
            blockSizes = new List<ushort>();
            for (int i = 0; i < numBlockSizes; i++)
            {
                blockSizes.Add(stream.ReadUInt16());
            }

            filenamesIndex = -1;
            for (int i = 0; i < TotalFilesInDLC; i++)
            {
                if (StructuralComparisons.StructuralEqualityComparer.Equals(filesList[i].filenameHash, FileListHash))
                {
                    stream.JumpTo(filesList[i].dataOffset);
                    int compressedBlockSize = blockSizes[filesList[i].compressedBlockSizesIndex];
                    byte[] inBuf = stream.ReadToBuffer(compressedBlockSize);
                    byte[] outBuf = LZMA.Decompress(inBuf, (uint)filesList[i].uncomprSize);
                    if (outBuf.Length == 0)
                        throw new Exception();
                    StreamReader filenamesStream = new StreamReader(new MemoryStream(outBuf));
                    while (filenamesStream.EndOfStream == false)
                    {
                        string name = filenamesStream.ReadLine();
                        byte[] hash = MD5.Create().ComputeHash(Encoding.ASCII.GetBytes(name.ToLowerInvariant()));
                        for (int l = 0; l < TotalFilesInDLC; l++)
                        {
                            if (StructuralComparisons.StructuralEqualityComparer.Equals(filesList[l].filenameHash, hash))
                            {
                                DLCEntry f = filesList[l];
                                f.filenamePath = name.Replace('/', '\\');
                                filesList[l] = f;
                            }
                        }
                    }
                    filenamesIndex = i;
                    break;
                }
            }
        }

        public void ExtractEntry(DLCEntry entry, Stream input, Stream output)
        {
            input.JumpTo(entry.dataOffset);
            if (entry.compressedBlockSizesIndex == -1)
            {
                output.WriteFromStream(input, entry.uncomprSize);
            }
            else
            {
                var uncompressedBlockBuffers = new List<byte[]>();
                var compressedBlockBuffers = new List<byte[]>();
                var blockBytesLeft = new List<long>();
                long bytesLeft = entry.uncomprSize;
                for (int j = 0; j < entry.numBlocks; j++)
                {
                    blockBytesLeft.Add(bytesLeft);
                    int compressedBlockSize = blockSizes[entry.compressedBlockSizesIndex + j];
                    int uncompressedBlockSize = (int)Math.Min(bytesLeft, maxBlockSize);
                    if (compressedBlockSize == 0)
                    {
                        compressedBlockSize = (int)maxBlockSize;
                    }
                    compressedBlockBuffers.Add(input.ReadToBuffer(compressedBlockSize));
                    uncompressedBlockBuffers.Add(null);
                    bytesLeft -= uncompressedBlockSize;
                }

                Parallel.For(0, entry.numBlocks, j =>
                {
                    int compressedBlockSize = blockSizes[entry.compressedBlockSizesIndex + (int)j];
                    int uncompressedBlockSize = (int)Math.Min(blockBytesLeft[(int)j], maxBlockSize);
                    if (compressedBlockSize == 0 || compressedBlockSize == blockBytesLeft[(int)j])
                    {
                        uncompressedBlockBuffers[(int)j] = compressedBlockBuffers[(int)j];
                    }
                    else
                    {
                        uncompressedBlockBuffers[(int)j] = LZMA.Decompress(compressedBlockBuffers[(int)j], (uint)uncompressedBlockSize);
                        if (uncompressedBlockBuffers[(int)j].Length == 0)
                            throw new Exception();
                    }
                });

                for (int j = 0; j < entry.numBlocks; j++)
                {
                    output.WriteFromBuffer(uncompressedBlockBuffers[j]);
                }
            }
        }

        private static string GetPrettyDLCNameFromPath(string sfarPath)
        {
            var dlcFolderName = Path.GetFileName(Directory.GetParent(Directory.GetParent(sfarPath).FullName).FullName);
            ME3Directory.OfficialDLCNames.TryGetValue(dlcFolderName, out var prettyname);
            return prettyname ?? dlcFolderName;
        }

        public void Extract(string outPath)
        {
            if (!File.Exists(filePath))
                throw new Exception("filename missing");

            UnpackCanceled = false;
            IndeterminateState = true;
            CurrentOverallStatus = $"Extracting {GetPrettyDLCNameFromPath(filePath)}";
            CurrentStatus = $"Loading {GetPrettyDLCNameFromPath(filePath)} into memory ({FileSize.FormatSize(new FileInfo(filePath).Length)})";
            byte[] buffer = File.ReadAllBytes(filePath);
            CurrentFilesProcessed = 0;
            IndeterminateState = false;

            using (MemoryStream stream = new MemoryStream(buffer))
            {
                for (int i = 0; i < TotalFilesInDLC; i++, CurrentFilesProcessed++)
                {
                    if (UnpackCanceled)
                        break;

                    if (filenamesIndex == i)
                        continue;
                    if (filesList[i].filenamePath == null)
                        throw new Exception("filename missing");

                    CurrentStatus = "File " + (i + 1) + " of " + filesList.Count + " - " + Path.GetFileName(filesList[i].filenamePath);
                    CurrentProgress = (int)(100.0 * CurrentFilesProcessed) / (int)TotalFilesInDLC;

                    int pos = filesList[i].filenamePath.IndexOf("\\BIOGame\\DLC\\", StringComparison.OrdinalIgnoreCase);
                    string filename = filesList[i].filenamePath.Substring(pos + ("\\BIOGame\\DLC\\").Length);
                    string dir = Path.GetDirectoryName(outPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(dir, filename)));
                    using (FileStream outputFile = new FileStream(Path.Combine(dir, filename), FileMode.Create, FileAccess.Write))
                    {
                        ExtractEntry(filesList[i], stream, outputFile);
                    }
                }
            }

            if (UnpackCanceled)
            {
                CurrentStatus = "Canceling, cleaning up...";
                IndeterminateState = true;
                string dir = Path.GetDirectoryName(outPath);
                for (int i = 0; i < TotalFilesInDLC; i++)
                {
                    if (filenamesIndex == i)
                        continue;
                    int pos = filesList[i].filenamePath.IndexOf("\\BIOGame\\DLC\\", StringComparison.OrdinalIgnoreCase);
                    string filename = filesList[i].filenamePath.Substring(pos + ("\\BIOGame\\DLC\\").Length);
                    if (File.Exists(Path.Combine(dir, filename)))
                        File.Delete(Path.Combine(dir, filename));
                }
                IndeterminateState = false;
                return;
            }

            File.Delete(filePath);
            using (FileStream outputFile = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                outputFile.WriteUInt32(SfarTag);
                outputFile.WriteUInt32(SfarVersion);
                outputFile.WriteUInt32(HeaderSize);
                outputFile.WriteUInt32(HeaderSize);
                outputFile.WriteUInt32(0);
                outputFile.WriteUInt32(HeaderSize);
                outputFile.WriteUInt32((uint)MaxBlockSize);
                outputFile.WriteUInt32(LZMATag);
            }
        }
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
