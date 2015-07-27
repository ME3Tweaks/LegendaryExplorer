/*  Copyright (C) 2013 AmaroK86 (marcidm 'at' hotmail 'dot' com)
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.

 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.

 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using Gibbed.IO;
using System.Drawing;
using System.Windows.Forms;

namespace AmaroK86.MassEffect3
{
    public class TOCHandler
    {
        public List<chunk> chunkList;
        public bool bChanged = false;

        public class chunk
        {
            public int globalSize { get { return (fileList == null) ? 0 : fileList.Sum(x => x.fileSize); } }

            public long offset;
            public int relPosition;
            public int countNextFiles { get { return (fileList == null) ? 0 : fileList.Count; } }
            public List<fileStruct> fileList;
        }

        public class fileStruct
        {
            // fileblock size must be a multiple of 4 (4,8,...,64,72,...,88,92,96,...)
            public short blockSize { get { return (short)(4 * (1 + (28 + filePath.Length) / 4)); } }
            public short flag;
            public int fileSize;
            public byte[] sha1;
            public string filePath = "";
            public bool exist = true;
        }

        public string tocFilePath;
        public string gamePath;

        public TOCHandler(string tocFileName, string gamePath)
        {
            using (FileStream tocStream = File.OpenRead(tocFileName))
            {
                if (tocStream.ReadValueU32() != 0x3AB70C13)
                    throw new Exception("not a toc.bin file");

                tocFilePath = Path.GetFullPath(tocFileName);
                this.gamePath = gamePath;

                chunkList = new List<chunk>();

                tocStream.Seek(8, SeekOrigin.Begin);

                int numChunks = tocStream.ReadValueS32();
                for (int i = 0; i < numChunks; i++)
                {
                    chunk newChunk = new chunk();
                    newChunk.offset = tocStream.Position;
                    newChunk.relPosition = tocStream.ReadValueS32();
                    int countBlockFiles = tocStream.ReadValueS32();

                    if (countBlockFiles == 0)
                    {
                        chunkList.Add(newChunk);
                        continue;
                    }

                    newChunk.fileList = new List<fileStruct>();
                    tocStream.Seek(newChunk.relPosition - 8, SeekOrigin.Current);

                    for (int j = 0; j < countBlockFiles; j++)
                    {
                        fileStruct newFileStruct = new fileStruct(); 
                        
                        long fileOffset = tocStream.Position;
                        tocStream.Seek(2, SeekOrigin.Current);
                        newFileStruct.flag = tocStream.ReadValueS16();
                        newFileStruct.fileSize = tocStream.ReadValueS32();
                        newFileStruct.sha1 = tocStream.ReadBytes(20);
                        newFileStruct.filePath = tocStream.ReadStringZ();
                        newFileStruct.exist = true;

                        tocStream.Seek(fileOffset + newFileStruct.blockSize, SeekOrigin.Begin);

                        newChunk.fileList.Add(newFileStruct);
                    }

                    tocStream.Seek(newChunk.offset + 8, SeekOrigin.Begin);

                    chunkList.Add(newChunk);
                }
            }
        }

        public string saveToFile(bool fileOverwrite = true)
        {
            bChanged = false;

            string finalTocFile = fileOverwrite ? tocFilePath : tocFilePath + ".tmp";
            using (FileStream newFileStream = File.Create(finalTocFile))
            {
                newFileStream.WriteValueU32(0x3AB70C13);
                newFileStream.WriteValueS32(0x0);
                newFileStream.WriteValueS32(chunkList.Count);

                int chunkOffset = 12;
                int fileOffset = 12 + (chunkList.Count * 8);

                string lastFile = chunkList.Last(x => (x.fileList != null) && x.fileList.Count(/*y => y.exist*/) != 0).fileList.Last(/*z => z.exist*/).filePath;

                //foreach (chunk element in chunkList)
                for(int i = 0; i < chunkList.Count; i++)
                {
                    chunk element = chunkList[i];
                    newFileStream.Seek(chunkOffset, SeekOrigin.Begin);

                    if (element.countNextFiles == 0)// || element.fileList.Count(x => x.exist) == 0)
                    {
                        newFileStream.WriteValueS64(0x0);
                        chunkOffset = (int)newFileStream.Position;
                    }
                    else
                    {
                        newFileStream.WriteValueS32(fileOffset - chunkOffset);
                        newFileStream.WriteValueS32(element.fileList.Count/*(x => x.exist)*/);
                        chunkOffset = (int)newFileStream.Position;

                        newFileStream.Seek(fileOffset, SeekOrigin.Begin);
                        //foreach (fileStruct fileElement in element.fileList.Where(x => x.exist))
                        for(int j = 0; j < element.fileList.Count; j++)
                        {
                            fileStruct fileElement = element.fileList[j];

                            //if (!fileElement.exist)
                            //    continue;
                            MemoryStream buffer = new MemoryStream(fileElement.blockSize);
                            {
                                if (fileElement.filePath == lastFile)
                                    buffer.WriteValueS16(0x0);
                                else
                                    buffer.WriteValueS16(fileElement.blockSize);
                                buffer.WriteValueS16(fileElement.flag);
                                buffer.WriteValueS32(fileElement.fileSize);
                                buffer.WriteBytes(fileElement.sha1);
                                buffer.WriteStringZ(fileElement.filePath);
                                byte[] byteBuff = new byte[fileElement.blockSize];
                                buffer.ToArray().CopyTo(byteBuff, 0);
                                newFileStream.WriteBytes(byteBuff);
                            }
                            //newFileStream.Seek(fileOffset, SeekOrigin.Begin);
                        }
                        fileOffset = (int)newFileStream.Position;
                    }
                }
            }

            return finalTocFile;
        }

        public bool existsFile(string fileName)
        {
            string fullFileName = Path.GetFullPath(fileName);

            if (fullFileName.Length < gamePath.Length)
                return false;

            if (fullFileName.Substring(0, gamePath.Length).ToLowerInvariant() != gamePath.ToLowerInvariant())
                return false;

            string tocFileName = fullFileName.Substring(gamePath.Length);

            return chunkList.Exists(x => x.fileList != null && x.fileList.Exists(file => file.filePath == tocFileName));
        }

        public void removeNotExistingFiles(WindowProgressForm dbprog, object args)
        {
            bChanged = true;
            int count = 0;
            int totalBlocks = chunkList.Count;

            dbprog.Invoke((Action)delegate
            {
                dbprog.Text = "Cleaning toc.bin";
                dbprog.lblCommand.Text = "Removing not existing files from PCConsoleTOC.bin";
                dbprog.progressBar.Maximum = totalBlocks;
                dbprog.progressBar.Value = 0;
            });

            foreach (chunk element in chunkList)
            {
                dbprog.Invoke((Action)delegate { dbprog.progressBar.Value = count++; dbprog.richTextBox.Text = "Cleaning block " + count + " of " + totalBlocks; });

                if (element.fileList != null)
                    element.fileList = element.fileList.Where(x => x.exist).ToList();

                //element.countNextFiles = (element.fileList == null) ? 0 : element.fileList.Count/*(x => x.exist)*/;
            }
        }
        
        public void clearFiles()
        {
            bChanged = true;

            using (FileStream newFileStream = File.Create(tocFilePath + ".tmp"))
            {
                newFileStream.WriteValueU32(0x3AB70C13);
                newFileStream.WriteValueS32(0x0);
                newFileStream.WriteValueS32(chunkList.Count);

                int chunkOffset = 12;
                int fileOffset = 12 + (chunkList.Count * 8);

                foreach (chunk element in chunkList)
                {
                    newFileStream.Seek(chunkOffset, SeekOrigin.Begin);

                    if (element.countNextFiles == 0 || element.fileList.Count(x => x.exist) == 0)
                    {
                        newFileStream.WriteValueS64(0x0);
                        chunkOffset = (int)newFileStream.Position;
                    }
                    else
                    {
                        newFileStream.WriteValueS32(fileOffset - chunkOffset);
                        newFileStream.WriteValueS32(element.fileList.Count(x => x.exist));
                        chunkOffset = (int)newFileStream.Position;

                        newFileStream.Seek(fileOffset, SeekOrigin.Begin);

                        foreach (fileStruct fileElement in element.fileList)
                        {
                            if (!fileElement.exist)
                                continue;

                            newFileStream.WriteValueS16(fileElement.blockSize);
                            newFileStream.WriteValueS16(fileElement.flag);
                            newFileStream.WriteValueS32(fileElement.fileSize);
                            newFileStream.WriteBytes(fileElement.sha1);
                            newFileStream.WriteStringZ(fileElement.filePath);

                            fileOffset += fileElement.blockSize;
                            newFileStream.Seek(fileOffset, SeekOrigin.Begin);
                        }
                    }
                }
            }
        }

        public void fixAll(WindowProgressForm dbprog, object args)
        {
            bChanged = true;

            bool forceAll = false;

            int count = 0;
            int total = chunkList.Count;

            dbprog.Invoke((Action)delegate
            {
                dbprog.Text = "Fixing toc.bin";
                dbprog.lblCommand.Text = "Updating PCConsoleTOC.bin";
                dbprog.progressBar.Maximum = total;
                dbprog.progressBar.Value = 0;
            });

            using (FileStream tocStream = File.OpenWrite(tocFilePath))
            {
                foreach (chunk entry in chunkList)
                {
                    dbprog.Invoke((Action)delegate { dbprog.progressBar.Value = count++; dbprog.richTextBox.Text = "Fixing block " + count + " of " + total;});

                    if (entry.fileList == null)
                        continue;

                    foreach (TOCHandler.fileStruct fileStruct in entry.fileList)
                    {
                        string fullFileName = gamePath + "\\" + fileStruct.filePath;
                        if (File.Exists(fullFileName) && fileStruct.filePath != "BioGame\\PCConsoleTOC.bin")
                        {
                            using (FileStream fileStream = File.OpenRead(fullFileName))
                            {
                                if (forceAll || fileStruct.fileSize != fileStream.Length)
                                {
                                    fileStruct.fileSize = (int)fileStream.Length;
                                    using (SHA1 sha = SHA1.Create())
                                    {
                                        sha.Initialize();
                                        byte[] buffer = new byte[fileStream.Length];
                                        int inputCount = fileStream.Read(buffer, 0, buffer.Length);
                                        sha.TransformBlock(buffer, 0, inputCount, null, 0);
                                        sha.TransformFinalBlock(buffer, 0, 0);
                                        fileStruct.sha1 = sha.Hash;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void removeFile(string fileName)
        {
            chunk selBlock = chunkList.Single(x => x.fileList != null && x.fileList.Exists(file => file.filePath == fileName));
            selBlock.fileList.Remove(selBlock.fileList.Single(x => x.filePath == fileName));
            bChanged = true;
        }

        public void addFile(string newFileName, int blockIndex)
        {
            if (newFileName.Substring(0, gamePath.Length) != gamePath)
            {
                throw new Exception("Can't add \"" + Path.GetFileName(newFileName) + "\", it must reside inside \n" + gamePath);
            }

            string tocBinFilePath = newFileName.Substring(gamePath.Length);
            if (existsFile(newFileName))
                throw new Exception("Can't add \"" + tocBinFilePath + "\",\nit already exist inside PCConsoleTOC.bin.");

            /*foreach (chunk chunkElem in chunkList)
            {
                if (chunkElem.fileList == null)
                    continue;
                foreach (fileStruct fileElem in chunkElem.fileList)
                {
                    if (tocBinFilePath.ToLower() == fileElem.filePath.ToLower())
                    {
                        throw new Exception("Can't add \"" + tocBinFilePath + "\",\nit already exist inside PCConsoleTOC.bin.");
                    }
                }
            }*/

            fileStruct addFileStruct = new fileStruct();

            switch (Path.GetExtension(newFileName))
            {
                case ".tlk":
                case ".tfc": addFileStruct.flag = 0x09; break;
                default: addFileStruct.flag = 0x01; break;
            }

            addFileStruct.filePath = tocBinFilePath;
            addFileStruct.exist = true;

            using (FileStream fileStream = File.OpenRead(newFileName))
            {
                addFileStruct.fileSize = (int)fileStream.Length;
                using (SHA1 sha = SHA1.Create())
                {
                    sha.Initialize();
                    byte[] buffer = new byte[fileStream.Length];
                    int inputCount = fileStream.Read(buffer, 0, buffer.Length);
                    sha.TransformBlock(buffer, 0, inputCount, null, 0);
                    sha.TransformFinalBlock(buffer, 0, 0);
                    addFileStruct.sha1 = sha.Hash;
                }
            }

            if (chunkList[blockIndex].fileList == null)
                chunkList[blockIndex].fileList = new List<fileStruct>();
            chunkList[blockIndex].fileList.Add(addFileStruct);

            bChanged = true;
        }
    }
}
