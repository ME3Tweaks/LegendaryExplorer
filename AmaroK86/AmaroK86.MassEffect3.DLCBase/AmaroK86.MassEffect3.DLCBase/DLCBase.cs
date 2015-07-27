#define WITH_GUI
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Gibbed.IO;
using Gibbed.MassEffect3.FileFormats;
using Gibbed.MassEffect3.FileFormats.SFXArchive;
using SevenZip.Compression.LZMA;
using System.Threading;

#if (WITH_GUI)
using System.ComponentModel;
using System.Windows.Forms;
using System.Threading.Tasks;
#endif

namespace AmaroK86.MassEffect3
{
    public class sfarCollection : SortedKeyedCollection<FileNameHash, sfarFile>
    {
        public sfarCollection() : base() { }
        protected override FileNameHash GetKeyForItem(sfarFile item)
        {
            // In this example, the key is the part number.
            return item.nameHash;
        }

    }

    public class sfarFile
    {
        public long entryOffset;
        public FileNameHash nameHash;
        public int blockSizeIndex;
        public ushort[] blockSizeArray;
        public long uncompressedSize;
        public long[] dataOffset;
        public string fileName;
    }

    #region DLCBase

    public class DLCBase
    {
        public string fileName = null;

        public sfarCollection fileList = new sfarCollection();
        public List<string> fileNameList
        {
            get
            {
                return fileList.Where(y => y.fileName != null).Select(x => x.fileName).ToList();
            }
        }
        public static readonly FileNameHash fileListHash = new FileNameHash(new byte[] { 0xB5, 0x50, 0x19, 0xCB, 0xF9, 0xD3, 0xDA, 0x65, 0xD5, 0x5B, 0x32, 0x1C, 0x00, 0x19, 0x69, 0x7C, });
        //public SortedList<FileNameHash, sfarFile> fileList = new SortedList<FileNameHash, sfarFile>();

        public static uint sfarHeader = 0x53464152;
        public static uint MaximumBlockSize = 0x010000;
        public uint numOfFiles = 0;
        public long totalUncSize = 0;
        public long totalComprSize = 0;
        public uint entryOffset;
        public uint blockTableOffset;
        public uint dataOffset;
        public CompressionScheme CompressionScheme;

        private void getStructure(Stream input)
        {
            var magic = input.ReadValueU32(Endian.Little);
            if (magic != sfarHeader && // SFAR
                magic.Swap() != sfarHeader)
            {
                throw new FormatException("Not a valid sfar file.");
            }
            var endian = magic == sfarHeader ? Endian.Little : Endian.Big;

            var version = input.ReadValueU32(endian);
            if (version != 0x00010000)
            {
                throw new FormatException("Not supported version.");
            }

            dataOffset = input.ReadValueU32(endian);
            uint minDataOffset = dataOffset;
            entryOffset = input.ReadValueU32(endian);
            var fileTableCount = numOfFiles = input.ReadValueU32(endian);
            blockTableOffset = input.ReadValueU32(endian);
            MaximumBlockSize = input.ReadValueU32(endian);
            this.CompressionScheme = input.ReadValueEnum<CompressionScheme>(endian);

            if (entryOffset != 0x20)
            {
                throw new FormatException();
            }

            if (MaximumBlockSize != 0x010000)
            {
                throw new FormatException();
            }

            if (this.CompressionScheme != CompressionScheme.None &&
                this.CompressionScheme != CompressionScheme.Lzma &&
                this.CompressionScheme != CompressionScheme.Lzx)
            {
                throw new FormatException();
            }

            input.Seek(entryOffset, SeekOrigin.Begin);
            for (uint i = 0; i < fileTableCount; i++)
            {
                sfarFile entry = new sfarFile();
                entry.entryOffset = input.Position;
                entry.nameHash = input.ReadFileNameHash();
                entry.blockSizeIndex = input.ReadValueS32(endian);
                entry.uncompressedSize = input.ReadValueU32(endian);
                entry.uncompressedSize |= ((long)input.ReadValueU8()) << 32;
                totalUncSize += entry.uncompressedSize;

                if (entry.blockSizeIndex == -1)
                {
                    entry.dataOffset = new long[1];
                    entry.dataOffset[0] = input.ReadValueU32(endian);
                    entry.dataOffset[0] |= ((long)input.ReadValueU8()) << 32;
                    totalComprSize += entry.uncompressedSize;
                }
                else
                {
                    int numBlocks = (int)Math.Ceiling((double)entry.uncompressedSize / (double)MaximumBlockSize);
                    entry.dataOffset = new long[numBlocks];
                    entry.blockSizeArray = new ushort[numBlocks];
                    entry.dataOffset[0] = input.ReadValueU32(endian);
                    entry.dataOffset[0] |= ((long)input.ReadValueU8()) << 32;

                    long lastPosition = input.Position;
                    input.Seek(getBlockOffset(entry.blockSizeIndex, entryOffset, fileTableCount), 0);
                    entry.blockSizeArray[0] = input.ReadValueU16();

                    for (int j = 1; j < numBlocks; j++)
                    {
                        entry.blockSizeArray[j] = input.ReadValueU16();
                        entry.dataOffset[j] = entry.dataOffset[j - 1] + entry.blockSizeArray[j];
                        totalComprSize += entry.blockSizeArray[j];
                    }
                    input.Seek(lastPosition, 0);
                }
                fileList.Add(entry);
            }// end of foreach
        }

        public DLCBase(string fileName)
        {
            this.fileName = Path.GetFullPath(fileName);
            var fileStream = File.OpenRead(fileName);
            getStructure(fileStream);
            long minDataOffset;

            var fileListEntry = fileList[fileListHash];

            if (fileListEntry == null)
                throw new FormatException("File list not found.");

            minDataOffset = fileListEntry.dataOffset[0];

            using (var outStream = new MemoryStream())
            {
#if (WITH_GUI)
                DecompressEntry(fileListEntry, fileStream, outStream, null);
#else
                DecompressEntry(fileListEntry, fileStream, outStream);
#endif
                outStream.Position = 0;

                var reader = new StreamReader(outStream);
                while (reader.EndOfStream == false)
                {
                    string line = reader.ReadLine();
                    FileNameHash hashFile = FileNameHash.Compute(line);
                    fileList[hashFile].fileName = line;

                    if (fileList[hashFile].dataOffset[0] < minDataOffset)
                        minDataOffset = fileList[hashFile].dataOffset[0];
                }
            }
            if (minDataOffset > dataOffset)
                dataOffset = (uint)minDataOffset;
            fileStream.Close();
        }

        private long getBlockOffset(int blockIndex, uint entryOffset, uint numEntries)
        {
            return (long)(entryOffset + (numEntries * 0x1E) + (blockIndex * 2));
        }

        public void extractFile(string selectedFileName, string outputName)
        {
            sfarFile selectedFile = fileList.First(entry => entry.fileName == selectedFileName);
            using (FileStream input = File.OpenRead(fileName), output = File.Create(outputName))
            {
                DecompressEntry(selectedFile, input, output);
            }
        }

        public String getFullNameOfEntry(string shrtName)
        {
            shrtName = Path.GetFileName(shrtName);
            for (int i = 0; i < fileList.Count; i++)
            {
                if (String.Compare(Path.GetFileName(fileList[i].fileName), shrtName, true) == 0)
                    return fileList[i].fileName;
            }

            return null;
        }

#if (WITH_GUI)
        public void DecompressEntry(sfarFile entry, Stream input, Stream output, BackgroundWorker BWork = null)
        {
            int maxPerc = 0;
#else
        public void DecompressEntry(sfarFile entry, Stream input, Stream output)
        {
#endif
            int count = 0;
            byte[] inputBlock;
            byte[] outputBlock = new byte[MaximumBlockSize];

            var left = entry.uncompressedSize;
            input.Seek(entry.dataOffset[0], SeekOrigin.Begin);

            if (entry.blockSizeIndex == -1)
            {
                output.WriteFromStream(input, entry.uncompressedSize);
            }
            else
            {
                while (left > 0)
                {
                    uint compressedBlockSize = (uint)entry.blockSizeArray[count];
                    if (compressedBlockSize == 0)
                    {
                        compressedBlockSize = MaximumBlockSize;
                    }

                    if (CompressionScheme == CompressionScheme.None)
                    {
                        output.WriteFromStream(input, compressedBlockSize);
                        left -= compressedBlockSize;
                    }
                    else if (CompressionScheme == CompressionScheme.Lzma)
                    {
                        if (compressedBlockSize == MaximumBlockSize ||
                            compressedBlockSize == left)
                        {
                            output.WriteFromStream(input, compressedBlockSize);
                            left -= compressedBlockSize;
                        }
                        else
                        {
                            var uncompressedBlockSize = (uint)Math.Min(left, MaximumBlockSize);

                            if (compressedBlockSize < 5)
                            {
                                throw new InvalidOperationException();
                            }

                            inputBlock = new byte[compressedBlockSize];

                            //var properties = input.ReadBytes(5);
                            //compressedBlockSize -= 5;

                            if (input.Read(inputBlock, 0, (int)compressedBlockSize)
                                != compressedBlockSize)
                            {
                                throw new EndOfStreamException();
                            }

                            uint actualUncompressedBlockSize = uncompressedBlockSize;
                            uint actualCompressedBlockSize = compressedBlockSize;

                            /*var error = LZMA.Decompress(
                                outputBlock,
                                ref actualUncompressedBlockSize,
                                inputBlock,
                                ref actualCompressedBlockSize,
                                properties,
                                (uint)properties.Length);

                            if (error != LZMA.ErrorCode.Ok ||
                                uncompressedBlockSize != actualUncompressedBlockSize ||
                                compressedBlockSize != actualCompressedBlockSize)
                            {
                                throw new InvalidOperationException();
                            }*/

                            outputBlock = SevenZipHelper.Decompress(inputBlock, (int)actualUncompressedBlockSize);
                            if (outputBlock.Length != actualUncompressedBlockSize)
                                throw new NotImplementedException();

                            output.Write(outputBlock, 0, (int)actualUncompressedBlockSize);
                            left -= uncompressedBlockSize;

#if (WITH_GUI)
                            if (BWork != null)
                            {
                                int perc = (int)(((float)count / (float)(entry.blockSizeArray.Length)) * 100);
                                if (perc > maxPerc)
                                {
                                    maxPerc = perc;
                                    BWork.ReportProgress(perc);
                                }
                            }
#endif
                        }
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                    count++;
                }
            }
        }// end of DecompressEntry
    }

    #endregion

    #region DLCPack

    public static class DLCPack
    {
        public static bool verbose = false;
        public static bool forceCompress = false;

        private static uint sfarHeader = 0x53464152;
        private static uint sfarVersion = 0x00010000;
        private static uint sfarDataOffset = 0x0;
        private static uint sfarFileTableOffset = 0x00000020;
        private static uint sfarFileTableCount = 0x0;
        private static uint sfarBlockSizeTableOffset = 0x0;
        public static uint sfarMaxBlockSize = 0x00010000;

        private static byte[] properties = new byte[5];
        private static uint prLength = (uint)properties.Length;

        public static void Compress(string dir, string outputPath)
        {
            if (!Directory.Exists(dir))
                throw new ArgumentException("Invalid argument: not a directory.");

            if (Directory.Exists(outputPath))
            {
                if (outputPath[outputPath.Length - 1].CompareTo('\\') != 0)
                    outputPath += "\\";
                outputPath += "Default.sfar";
            }

            if (dir[dir.Length - 1].CompareTo('\\') != 0)
                dir += "\\";

            string bio = dir + "BIOGame";
            string unk = dir + "__UNKNOWN";

            if (Directory.Exists(bio))
            {
                if (Directory.Exists(unk))
                {
                    Directory.Delete(unk, true);
                }
            }
            else
            {
                throw new NotImplementedException("Incorrect folder location, BIOGame folder not founded");
            }

            uint pointerEntry = sfarFileTableOffset;
            uint pointerBlockSize = 0x0; //not defined yet
            uint pointerData = 0x0; //not defined yet

            uint entryBlockSize = 0x1E; //30 bytes

            uint totBlockSize = 0;
            int posBlockSize = 0;

            var inputBlock = new byte[sfarMaxBlockSize];
            var outputBlock = new byte[sfarMaxBlockSize];
            FileNameHash fileListHash = new FileNameHash(new byte[] { 0xB5, 0x50, 0x19, 0xCB, 0xF9, 0xD3, 0xDA, 0x65, 0xD5, 0x5B, 0x32, 0x1C, 0x00, 0x19, 0x69, 0x7C, });

            SortedDictionary<FileNameHash, string> fileTable = new SortedDictionary<FileNameHash, string>();

            string allFileList = "";

            //get the filelist to put inside the sfar archive
            string[] fileList = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);

            sfarFileTableCount = (uint)fileList.Length + 1;

            //updating the pointer of block size index
            sfarBlockSizeTableOffset = pointerBlockSize = sfarFileTableOffset + (sfarFileTableCount * entryBlockSize);

            uint counter = 1;

            //creating the file
            using (FileStream stream = new FileStream(outputPath, FileMode.Create))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    //writing sfar header
                    writer.Write(sfarHeader);
                    writer.Write(sfarVersion);
                    writer.Write(sfarDataOffset);
                    writer.Write(sfarFileTableOffset);
                    writer.Write(sfarFileTableCount);
                    writer.Write(sfarBlockSizeTableOffset);
                    writer.Write(sfarMaxBlockSize);
                    writer.Write((uint)CompressionScheme.Lzma);

                    uint actualUncompressedBlockSize = sfarMaxBlockSize;
                    uint actualCompressedBlockSize = sfarMaxBlockSize;

                    uint aux = 0;

                    //---------------------------------------------------------------------------------------------

                    //calculating num of blocksize blocks
                    foreach (string show in fileList)
                    {
                        string extension = Path.GetExtension(show);
                        //if the file is a .bik or .afc it's not counted
                        if ((extension.CompareTo(".bik") != 0 && extension.CompareTo(".afc") != 0) || forceCompress)
                        {
                            aux = (uint)Math.Ceiling((double)Getsize(show) / (double)sfarMaxBlockSize);
                            totBlockSize += aux;
                        }
                    }

                    //adding the filelist string blocksize blocks to the total num of blocksizes
                    foreach (string show in fileList)
                    {
                        var temp = show.Remove(0, dir.Length - 1) + Environment.NewLine;
                        temp = temp.Replace("\\", "/");
                        allFileList += temp;
                    }
                    aux = (uint)Math.Ceiling((double)allFileList.Length / (double)sfarMaxBlockSize);
                    totBlockSize += aux;
                    //---------------------------------------------------------------------------------------------

                    sfarDataOffset = pointerData = (totBlockSize * 2) + sfarBlockSizeTableOffset;

                    stream.Seek(8, SeekOrigin.Begin);
                    writer.Write(sfarDataOffset);

                    //creating the SORTED hash table array
                    string strFileNameHash;
                    foreach (string fileName in fileList)
                    {
                        strFileNameHash = fileName.Remove(0, dir.Length - 1);
                        strFileNameHash = strFileNameHash.Replace("\\", "/");
                        fileTable.Add(FileNameHash.Compute(strFileNameHash), fileName);
                    }
                    fileTable.Add(fileListHash, "");

                    Stream streamRead;
                    uint fileListPointerEntry = 0;
                    uint fileLength;
                    FileNameHash fileNameHash;
                    MemoryStream encStream;
                    int initialPosBlock = 0;
                    ushort[] blockSizeArray;
                    foreach (KeyValuePair<FileNameHash, string> kvp in fileTable)
                    {
                        if (kvp.Value == "") //this is the file list entry, it doesn't do anything for now
                        {
                            fileListPointerEntry = pointerEntry;
                            pointerEntry += entryBlockSize;
                        }
                        else
                        {
                            if (verbose)
                            {
                                Console.WriteLine("File: {0}\n Entry Offset: {1:X8}", kvp.Value, pointerEntry);
                                Console.WriteLine("  Block Index Offset: {0:X8}", pointerBlockSize);
                                Console.WriteLine("   Data Offset: {0:X8}", pointerData);
                                Console.WriteLine("    Initial blocknum: {0}", posBlockSize);
                            }

                            encStream = new MemoryStream();
                            string fileName = kvp.Value;
                            fileLength = (uint)Getsize(fileName);
                            fileNameHash = kvp.Key;
                            streamRead = new FileStream(fileName, FileMode.Open);

                            string extension = Path.GetExtension(fileName);
                            if ((extension.CompareTo(".bik") == 0 || extension.CompareTo(".afc") == 0) && !forceCompress)
                            {
                                outputBlock = new byte[fileLength];

                                streamRead.Read(outputBlock, 0, (int)fileLength);
                                encStream.Write(outputBlock, 0, (int)fileLength);
                                initialPosBlock = -1;
                            }
                            else
                            {
                                CompressFile(streamRead, out blockSizeArray, encStream);

                                //seeking the sfar to the last Block Size offset
                                stream.Seek((long)pointerBlockSize, SeekOrigin.Begin);
                                for (int i = 0; i < blockSizeArray.Length; i++)
                                {
                                    writer.Write(blockSizeArray[i]);
                                }
                                pointerBlockSize = (uint)stream.Position;
                                initialPosBlock = posBlockSize;
                                posBlockSize += blockSizeArray.Length;
                            }

                            //seeking the sfar to the last entry offset
                            stream.Seek((long)pointerEntry, SeekOrigin.Begin);
                            writer.Write(fileNameHash.A.Swap());
                            writer.Write(fileNameHash.B.Swap());
                            writer.Write(fileNameHash.C.Swap());
                            writer.Write(fileNameHash.D.Swap());
                            writer.Write(initialPosBlock);
                            writer.Write((uint)fileLength);
                            writer.Write((byte)0);
                            writer.Write((uint)pointerData);
                            writer.Write((byte)0);
                            pointerEntry = (uint)stream.Position;

                            //seeking the sfar to the last data offset
                            stream.Seek((long)pointerData, SeekOrigin.Begin);
                            encStream.WriteTo(stream);
                            pointerData += (uint)encStream.Length;

                            if (verbose)
                            {
                                Console.WriteLine("     Total Uncompressed: {0} B, Total Compressed: {1} B\n      Files Packed: {2}/{3}\n", fileLength, encStream.Length, counter++, sfarFileTableCount - 1);
                            }

                        }//end big else
                    }// end of foreach

                    // writing the fileList
                    streamRead = new MemoryStream(ASCIIEncoding.Default.GetBytes(allFileList));
                    fileLength = (uint)allFileList.Length;
                    fileNameHash = fileListHash;

                    encStream = new MemoryStream();

                    if (verbose)
                    {
                        Console.WriteLine("FileList entry Offset: {0:X8}", fileListPointerEntry);
                        Console.WriteLine(" Block Index Offset: {0:X8}", pointerBlockSize);
                        Console.WriteLine("  Data Offset: {0:X8}", pointerData);
                        Console.WriteLine("   Initial blocknum: {0}", posBlockSize);
                    }

                    CompressFile(streamRead, out blockSizeArray, encStream);

                    //seeking the sfar to the last Block Size offset
                    stream.Seek((long)pointerBlockSize, SeekOrigin.Begin);
                    for (int i = 0; i < blockSizeArray.Length; i++)
                    {
                        writer.Write(blockSizeArray[i]);
                    }
                    pointerBlockSize = (uint)stream.Position;
                    initialPosBlock = posBlockSize;
                    posBlockSize += blockSizeArray.Length;

                    //write the file list entry block
                    stream.Seek((long)fileListPointerEntry, SeekOrigin.Begin);
                    writer.Write(fileNameHash.A.Swap());
                    writer.Write(fileNameHash.B.Swap());
                    writer.Write(fileNameHash.C.Swap());
                    writer.Write(fileNameHash.D.Swap());
                    writer.Write(initialPosBlock);
                    writer.Write((uint)fileLength);
                    writer.Write((byte)0);
                    writer.Write((uint)pointerData);
                    writer.Write((byte)0);

                    //seeking the sfar to the last data offset
                    stream.Seek((long)pointerData, SeekOrigin.Begin);
                    encStream.WriteTo(stream);
                    pointerData += (uint)encStream.Length;

                    if (verbose)
                    {
                        Console.WriteLine("     Total Uncompressed: {0} B, Total Compressed: {1} B\n", fileLength, encStream.Length);
                    }
                    encStream.Close();

                    //closing the main binary file writer
                    writer.Close();
                }

                if (verbose)
                {
                    Console.WriteLine("------ END ------");
                    Console.WriteLine("File Table Count: {0}", sfarFileTableCount);
                    Console.WriteLine("File Table (Entry) Offset: {0:X8}", sfarFileTableOffset);
                    Console.WriteLine("Block Size Offset: {0:X8}", sfarBlockSizeTableOffset);
                    Console.WriteLine("Total blocks: {0}", totBlockSize);
                    Console.WriteLine("Data Offset: {0:X8}", sfarDataOffset);
                }
            }
        }

        public static long Getsize(string filename)
        {
            FileInfo fInfo = new FileInfo(filename);
            return fInfo.Length;
        }

        private static byte[] StrToByteArray(string str, uint dim)
        {
            System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
            string aux = str.Substring(0, (int)dim);
            str = str.Remove(0, (int)dim);
            return encoding.GetBytes(aux);
        }

        /*
         * Function: CompressFile
         * purpose: create a compressed stream of bytes dividing the input stream
         *          into blocks of 65536 bytes
         * 
         * Input: Stream, ref ushort[]
         * Output: MemoryStream
         * 
         * usage:
         *  - Create a readable Stream
         *  - set a new ushort[0] variable
         *  - Create a writable Stream
         *  - use it: CompressFile(readable Stream, reference to ushort array, writable Stream)
         *
         */
#if (WITH_GUI)
        public static void CompressFile(Stream inStream, out ushort[] blockSizes, MemoryStream outStream, BackgroundWorker worker = null)
        {
            int highPerc = 0;
            int count = 0;
#else
        public static void CompressFile(Stream inStream, out ushort[] blockSizes, MemoryStream outStream)
        {
#endif
            if (inStream == null)
                throw new NullReferenceException("Error: null input stream");
            if (!inStream.CanRead)
                throw new IOException("Error: not an input readable stream");
            if (outStream == null)
                throw new NullReferenceException("Error: null output stream");
            if (!outStream.CanWrite)
                throw new IOException("Error: not an output writable stream");

            int fileLength = (int)inStream.Length;
            blockSizes = new ushort[(int)Math.Ceiling((double)fileLength / (double)sfarMaxBlockSize)];

            byte[] inputBlock;
            byte[] outputBlock = new byte[sfarMaxBlockSize + 100];

            uint templength = (uint)fileLength;
            int blockCounter = 0;
            uint actualUncompressedBlockSize;
            uint actualCompressedBlockSize;

            while (templength > 0)
            {
                if (templength > sfarMaxBlockSize)
                {
                    actualUncompressedBlockSize = sfarMaxBlockSize;
                    actualCompressedBlockSize = sfarMaxBlockSize;
                }
                else
                {
                    actualUncompressedBlockSize = templength;
                    actualCompressedBlockSize = templength;
                }
                inputBlock = new byte[actualUncompressedBlockSize];
                inStream.Read(inputBlock, 0, (int)actualUncompressedBlockSize);

                outputBlock = SevenZipHelper.Compress(inputBlock);
                actualCompressedBlockSize = (uint)outputBlock.Length;

#if (WITH_GUI)
                if (worker != null)
                {
                    int perc = (int)Math.Ceiling((float)count++ / (float)blockSizes.Length * 100);
                    if (perc > highPerc)
                    {
                        highPerc = perc;
                        if (perc > 100)
                            perc = 100;
                        worker.ReportProgress(perc);
                    }
                }
#endif
                /*var error = LZMA.Compress(
                                inputBlock,
                                ref actualCompressedBlockSize,
                                outputBlock,
                                actualUncompressedBlockSize,
                                properties,
                                ref prLength);

                //Console.WriteLine("  properties: {0}",String.Concat(Array.ConvertAll(properties, x => x.ToString("X2"))) );
                if (error != LZMA.ErrorCode.Ok)
                {
                    Console.WriteLine("error: {0}",error);
                    throw new InvalidOperationException();
                }*/

                uint maxCompressedBlockSize = (uint)outputBlock.Length;
                //maxCompressedBlockSize += prLength;

                if (maxCompressedBlockSize >= actualUncompressedBlockSize ||
                    maxCompressedBlockSize >= sfarMaxBlockSize)
                {
                    outStream.Write(inputBlock, 0, (int)actualUncompressedBlockSize);
                    if (templength < sfarMaxBlockSize)
                    {
                        ushort mini = (ushort)actualUncompressedBlockSize;
                        maxCompressedBlockSize = mini;
                    }
                    else
                    {
                        maxCompressedBlockSize = 0;
                    }
                }
                else
                {
                    //outStream.Write(properties, 0, properties.Length);
                    // Temporary hack to get around outofmemoryexception - we'll see how it goes
                    while (true)
                    {
                        try
                        {
                            outStream.Write(outputBlock, 0, (int)actualCompressedBlockSize);
                            break;
                        }
                        catch (OutOfMemoryException)
                        { 
                            MessageBox.Show("Memory overuse detected!\nTotal in use: " + GC.GetTotalMemory(false)); 
                            GC.Collect(); 
                            MessageBox.Show("New memory use: " + GC.GetTotalMemory(true)); 
                        }
                    }
                }
                ushort finalCompressedBlockSize = (ushort)maxCompressedBlockSize;
                blockSizes[blockCounter++] = finalCompressedBlockSize;

                //updating the pointer to the last position of Block Size
                if (templength > sfarMaxBlockSize)
                {
                    templength -= sfarMaxBlockSize;
                }
                else
                {
                    templength = 0;
                }
            }// end of while
        }//end of function

        public static void CompressFile(Stream inStream, out ushort[] blockSizes, out byte[][] compressedArray, int ThreadNo)
        {
            if (inStream == null)
                throw new NullReferenceException("Error: null input stream");
            if (!inStream.CanRead)
                throw new IOException("Error: not an input readable stream");

            DLCSubCLass compressor = new DLCSubCLass(ThreadNo);
            compressor.RunCompression(inStream);
            blockSizes = compressor.blockSizes;
            compressedArray = compressor.compressedArray;
            #region Old code
            /*
            int fileLength = (int)inStream.Length;
            blockSizes = new ushort[(int)Math.Ceiling((double)fileLength / (double)sfarMaxBlockSize)];
            compressedArray = new byte[blockSizes.Length][];

            byte[] inputBlock;
            byte[] outputBlock = new byte[sfarMaxBlockSize + 100];

            uint templength = (uint)fileLength;
            int blockCounter = 0;
            uint actualUncompressedBlockSize;
            uint actualCompressedBlockSize;

            while (templength > 0)
            {
                if (templength > sfarMaxBlockSize)
                {
                    actualUncompressedBlockSize = sfarMaxBlockSize;
                    actualCompressedBlockSize = sfarMaxBlockSize;
                }
                else
                {
                    actualUncompressedBlockSize = templength;
                    actualCompressedBlockSize = templength;
                }
                inputBlock = new byte[actualUncompressedBlockSize];
                inStream.Read(inputBlock, 0, (int)actualUncompressedBlockSize);

                outputBlock = SevenZipHelper.Compress(inputBlock);
                actualCompressedBlockSize = (uint)outputBlock.Length;

                uint maxCompressedBlockSize = (uint)outputBlock.Length;

                // Instead of a memory stream use a byte array array. 
                compressedArray[blockCounter] = outputBlock;

                if (maxCompressedBlockSize >= actualUncompressedBlockSize ||
                    maxCompressedBlockSize >= sfarMaxBlockSize)
                {
                    if (templength < sfarMaxBlockSize)
                    {
                        ushort mini = (ushort)actualUncompressedBlockSize;
                        maxCompressedBlockSize = mini;
                    }
                    else
                    {
                        maxCompressedBlockSize = 0;
                    }
                }
                ushort finalCompressedBlockSize = (ushort)maxCompressedBlockSize;
                blockSizes[blockCounter++] = finalCompressedBlockSize;

                //updating the pointer to the last position of Block Size
                if (templength > sfarMaxBlockSize)
                {
                    templength -= sfarMaxBlockSize;
                }
                else
                {
                    templength = 0;
                }
            }// end of while

            for (int i = 0; i < compressedArray.Length; i++)
            {
                if (compressedArray[i] == null)
                {
                    throw new NullReferenceException("An index of the array is null. Not all indices used!");
                }
            }
            */
            #endregion
        }//end of function

        private class DLCSubCLass
        {
            public ushort[] blockSizes;
            public byte[][] compressedArray;
            private object Locker = new object();
            private object BlockLocker = new object();
            private int numThreads;
            Stream inStream;

            int fileLength;
            uint tempLength;
            int blockCounter;

            public DLCSubCLass(int threadno)
            {
                if (threadno <= 0)
                    throw new FormatException("Number of threads cannot be 0 or negative");
                numThreads = threadno;
            }

            public void RunCompression(Stream infile)
            {
                inStream = infile;
                fileLength = (int)infile.Length;
                blockSizes = new ushort[(int)Math.Ceiling((double)fileLength / (double)sfarMaxBlockSize)];
                compressedArray = new byte[blockSizes.Length][];

                tempLength = (uint)fileLength;
                blockCounter = 0;

                #region Old threading method
                /*Thread[] threads = new Thread[numThreads];
                for (int i = 0; i < threads.Length; i++)
                {
                    Thread t = new Thread(new ThreadStart(ThreadCompression));
                    t.Start();
                    threads[i] = t;
                }

                for (int i = 0; i < threads.Length; i++)
                    threads[i].Join();
                */
                #endregion

                ParallelOptions po = new ParallelOptions();
                po.MaxDegreeOfParallelism = numThreads;
                Parallel.For(0, numThreads, po, i => ThreadCompression());

                CheckArray();
            }

            private void CheckArray()
            {
                if (compressedArray == null)
                    throw new NullReferenceException("Array is null");
                for (int i = 0; i < compressedArray.Length; i++)
                {
                    if (compressedArray[i] == null)
                        throw new NullReferenceException("An index of the array is null. Not all indices used!");
                }
            }

            private void ThreadCompression()
            {
                byte[] inputBlock, outputBlock;
                uint length;
                uint actualUncompressedBlockSize;
                uint actualCompressedBlockSize;
                int localblockcount;

                while (true)
                {
                    lock (Locker)
                    {
                        if (tempLength <= 0)
                            break;
                        length = tempLength;
                        if (tempLength > sfarMaxBlockSize)
                        {
                            actualCompressedBlockSize = sfarMaxBlockSize;
                            actualUncompressedBlockSize = sfarMaxBlockSize;
                            tempLength -= sfarMaxBlockSize;
                        }
                        else
                        {
                            actualCompressedBlockSize = length;
                            actualUncompressedBlockSize = length;
                            tempLength = 0;
                        }

                        inputBlock = new byte[actualUncompressedBlockSize];
                        inStream.Read(inputBlock, 0, (int)actualUncompressedBlockSize);
                        localblockcount = blockCounter++;
                    }

                    outputBlock = SevenZipHelper.Compress(inputBlock);
                    actualCompressedBlockSize = (uint)outputBlock.Length;

                    uint maxCompressedBlockSize = actualCompressedBlockSize;
                    if (maxCompressedBlockSize >= actualUncompressedBlockSize ||
                    maxCompressedBlockSize >= sfarMaxBlockSize)
                    {
                        byte[] buff = new byte[actualUncompressedBlockSize];
                        Buffer.BlockCopy(outputBlock, 0, buff, 0, (int)actualUncompressedBlockSize);
                        outputBlock = buff;
                        if (length < sfarMaxBlockSize)
                        {
                            maxCompressedBlockSize = (ushort)actualUncompressedBlockSize;
                        }
                        else
                        {
                            maxCompressedBlockSize = 0;
                        }
                    }

                    if (outputBlock.Length == 0)
                        throw new Exception();

                    //if (localblockcount == 243)
                    //    throw new Exception();

                    compressedArray[localblockcount] = outputBlock;
                    blockSizes[localblockcount] = (ushort)maxCompressedBlockSize;

                    //lock (BlockLocker)
                    //{
                    //    compressedArray[blockCounter] = outputBlock;
                    //    blockSizes[blockCounter++] = (ushort)maxCompressedBlockSize;
                    //}
                }
            }
        }

    } // end of DLCPack class

    #endregion

    #region DLCUnpack

    public static class DLCUnpack
    {
        public static bool extractUnknowns = true;
        public static bool overwriteFiles = false;
        public static bool verbose = false;

        public static void Decompress(DLCBase dlcBase, string inputPath, string outputPath)
        {
            using (var input = File.OpenRead(inputPath))
            {
                int count = 1;
                foreach (sfarFile entry in dlcBase.fileList)
                {
                    string entryName = entry.fileName;

                    if (entryName == null)
                    {
                        if (extractUnknowns == false)
                        {
                            continue;
                        }

                        entryName = entry.nameHash.ToString();
                        entryName = Path.Combine("__UNKNOWN", entryName);
                    }
                    else
                    {
                        entryName = entryName.Replace("/", "\\");
                        if (entryName.StartsWith("\\") == true)
                        {
                            entryName = entryName.Substring(1);
                        }
                    }

                    var entryPath = Path.Combine(outputPath, entryName);
                    if (overwriteFiles == false &&
                        File.Exists(entryPath) == true)
                    {
                        continue;
                    }

                    if (verbose == true)
                    {
                        Console.WriteLine("[{0}/{1}] {2}", count++, dlcBase.numOfFiles, entry.nameHash);
                    }

                    input.Seek(entry.dataOffset[0], SeekOrigin.Begin);

                    Directory.CreateDirectory(Path.GetDirectoryName(entryPath));
                    using (var output = File.Create(entryPath))
                    {
                        DecompressEntry(entry, input, output, dlcBase.CompressionScheme);
                    }
                }
            }
        }

#if (WITH_GUI)
        public static void DecompressEntry(sfarFile entry, Stream input, Stream output, CompressionScheme cScheme, BackgroundWorker worker = null)
        {
            int highPerc = 0;
            int count = 0;
#else
        public static void DecompressEntry(sfarFile entry, Stream input, Stream output, CompressionScheme cScheme)
        {
#endif
            byte[] inputBlock;
            byte[] outputBlock = new byte[DLCBase.MaximumBlockSize];

            var left = entry.uncompressedSize;
            input.Seek(entry.dataOffset[0], SeekOrigin.Begin);

            int numBlocks = (int)(Math.Ceiling((float)entry.uncompressedSize / (float)DLCBase.MaximumBlockSize));

            if (entry.blockSizeIndex == -1)
            {
                output.WriteFromStream(input, entry.uncompressedSize);
            }
            else
            {
                while (left > 0)
                {
                    uint compressedBlockSize = (uint)entry.blockSizeArray[count];
                    if (compressedBlockSize == 0)
                    {
                        compressedBlockSize = DLCBase.MaximumBlockSize;
                    }

                    if (cScheme == CompressionScheme.None)
                    {
                        output.WriteFromStream(input, compressedBlockSize);
                        left -= compressedBlockSize;
                    }
                    else if (cScheme == CompressionScheme.Lzma)
                    {
                        if (compressedBlockSize == DLCBase.MaximumBlockSize ||
                            compressedBlockSize == left)
                        {
                            output.WriteFromStream(input, compressedBlockSize);
                            left -= compressedBlockSize;
                        }
                        else
                        {
                            var uncompressedBlockSize = (uint)Math.Min(left, DLCBase.MaximumBlockSize);

                            if (compressedBlockSize < 5)
                            {
                                throw new InvalidOperationException();
                            }

                            inputBlock = new byte[compressedBlockSize];

                            //var properties = input.ReadBytes(5);
                            //compressedBlockSize -= 5;

                            if (input.Read(inputBlock, 0, (int)compressedBlockSize)
                                != compressedBlockSize)
                            {
                                throw new EndOfStreamException();
                            }

                            uint actualUncompressedBlockSize = uncompressedBlockSize;
                            uint actualCompressedBlockSize = compressedBlockSize;

                            /*var error = LZMA.Decompress(
                                outputBlock,
                                ref actualUncompressedBlockSize,
                                inputBlock,
                                ref actualCompressedBlockSize,
                                properties,
                                (uint)properties.Length);

                            if (error != LZMA.ErrorCode.Ok ||
                                uncompressedBlockSize != actualUncompressedBlockSize ||
                                compressedBlockSize != actualCompressedBlockSize)
                            {
                                throw new InvalidOperationException();
                            }*/

                            outputBlock = SevenZipHelper.Decompress(inputBlock, (int)actualUncompressedBlockSize);
                            if (outputBlock.Length != actualUncompressedBlockSize)
                                throw new NotImplementedException();

                            output.Write(outputBlock, 0, (int)actualUncompressedBlockSize);
                            left -= uncompressedBlockSize;
                        }
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }

#if (WITH_GUI)
                    if (worker != null)
                    {
                        int perc = (int)Math.Ceiling((float)count++ / (float)numBlocks * 100);
                        if (perc > highPerc)
                        {
                            highPerc = perc;
                            if (perc > 100)
                                perc = 100;
                            worker.ReportProgress(perc);
                        }
                    }
                    else
                        count++;
#endif
                }
            }
        }// end of DecompressEntry
    } // end of DLCUnpack class

    #endregion
}
