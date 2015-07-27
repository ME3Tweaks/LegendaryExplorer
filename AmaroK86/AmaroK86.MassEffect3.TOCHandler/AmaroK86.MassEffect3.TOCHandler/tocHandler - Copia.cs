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

        public class chunk
        {
            int relPosition;
            int countNextFiles;
            byte[] emptyBytes;
            List<fileStruct> fileList;

            public class fileStruct
            {
                short size;
                short flag;
                int fileSize;
                byte[] sha1;
                string filePath;
            }
        }

        public string tocFilePath;
        public string gamePath;
        public struct Element
        {
            public uint offset;
            public string name;
            public uint size;
            public byte[] sha1;
        }

        public List<Element> content;

        public TOCHandler(string tocFileName)
        {
            using (FileStream tocStream = File.OpenRead(tocFileName))
            {
                if (tocStream.ReadValueU32() != 0x3AB70C13)
                    throw new NotSupportedException("not a toc.bin file");

                tocFilePath = Path.GetFullPath(tocFileName);
                gamePath = Directory.GetParent(Path.GetDirectoryName(tocFileName)).FullName;
                content = new List<Element>();

                Element temp;
                tocStream.Seek(8, SeekOrigin.Begin);
                uint jumpSize = tocStream.ReadValueU32();
                tocStream.Seek(jumpSize * 8, SeekOrigin.Current);
                uint blockSize;
                do
                {
                    temp = new Element();
                    long position = tocStream.Position;
                    uint value = tocStream.ReadValueU32();
                    blockSize = value & 0xFFFF;
                    uint offset = (uint)tocStream.Position;
                    uint fileSize = tocStream.ReadValueU32();
                    //temp.sha1 = tocStream.ReadBytes(20);
                    tocStream.Seek(20, SeekOrigin.Current);

                    string filePath = tocStream.ReadStringZ();

                    tocStream.Seek(position + blockSize, SeekOrigin.Begin);

                    temp.name = filePath;
                    temp.offset = offset;
                    temp.size = fileSize;
                    content.Add(temp);

                } while (blockSize != 0);
            }
        }

        public void fixSizes(WindowProgressForm dbprog, object args)
        {
            int count = 0;

            dbprog.Invoke((Action)delegate
            {
                dbprog.Text = "Fixing toc.bin";
                dbprog.lblCommand.Text = "Updating PCConsoleTOC.bin";
                dbprog.progressBar.Maximum = content.Count;
                dbprog.progressBar.Value = 0;
            });

            using (FileStream tocStream = File.OpenWrite(tocFilePath))
            {
                foreach (Element entry in content)
                {
                    count++;
                    string fullFileName = gamePath + "\\" + entry.name;
                    if (File.Exists(fullFileName))
                    {
                        FileInfo fileInfo = new FileInfo(fullFileName);
                        if (fileInfo.Length != entry.size)
                        {
                            dbprog.Invoke((Action)delegate { dbprog.progressBar.Value = count; dbprog.richTextBox.Text = count + "\\" + content.Count + " - Fixing " + Path.GetFileName(entry.name); });
                            tocStream.Seek(entry.offset, SeekOrigin.Begin);
                            tocStream.WriteValueS32((int)fileInfo.Length);
                            using (FileStream fileStream = File.OpenRead(fullFileName))
                            {
                                using (SHA1 sha = SHA1.Create())
                                {
                                    sha.Initialize();
                                    byte[] buffer = new byte[fileStream.Length];
                                    int inputCount = fileStream.Read(buffer, 0, buffer.Length);
                                    sha.TransformBlock(buffer, 0, inputCount, null, 0);
                                    sha.TransformFinalBlock(buffer, 0, 0);
                                    tocStream.WriteBytes(sha.Hash);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void fixAll()
        {
            int count = 0;
            int total = content.Count;
            using (FileStream tocStream = File.OpenWrite(tocFilePath))
            {
                foreach (Element entry in content)
                {
                    Console.WriteLine(++count + "\\" + total + "Fixing {0}", entry.name);
                    string fullFileName = gamePath + "\\" + entry.name;
                    if (File.Exists(fullFileName) && entry.name != "BioGame\\PCConsoleTOC.bin")
                    {
                        using (FileStream fileStream = File.OpenRead(fullFileName))
                        {
                            tocStream.Seek(entry.offset, SeekOrigin.Begin);
                            tocStream.WriteValueS32((int)fileStream.Length);
                            using (SHA1 sha = SHA1.Create())
                            {
                                sha.Initialize();
                                byte[] buffer = new byte[fileStream.Length];
                                int inputCount = fileStream.Read(buffer, 0, buffer.Length);
                                sha.TransformBlock(buffer, 0, inputCount, null, 0);
                                sha.TransformFinalBlock(buffer, 0, 0);
                                tocStream.WriteBytes(sha.Hash);
                            }

                        }
                    }
                }
            }
        }

        public void clean()
        {
            using (FileStream tocStream = new FileStream(tocFilePath, FileMode.Open, FileAccess.ReadWrite), newTocStream = File.Create(tocFilePath + ".tmp"))
            {
                tocStream.Seek(0, SeekOrigin.Begin);
                byte[] buffer = tocStream.ReadBytes(8);
                newTocStream.WriteBytes(buffer);
                int count = tocStream.ReadValueS32();

                newTocStream.Seek(12, SeekOrigin.Begin);
                int newCount = 0;
                for (int i = 0; i < count; i++)
                {
                    int offset = tocStream.ReadValueS32();
                    int nextFiles = tocStream.ReadValueS32();
                    //if (offset != 0)
                    {
                        newCount++;
                        newTocStream.WriteValueS32(offset);
                        newTocStream.WriteValueS32(nextFiles);
                    }
                }
                //MessageBox.Show("pos: " + newTocStream.Position);
                newTocStream.Seek(8, SeekOrigin.Begin);
                newTocStream.WriteValueS32(newCount);

                int newDataOffset = 12 + (newCount * 8);

                //newTocStream.Seek(12 + (newCount * 8), SeekOrigin.Begin);
                //newTocStream.Seek(12, SeekOrigin.Begin);

                int oldDataOffset = 12 + (count * 8);
                int oldChunkOffset = 16;
                int newChunkOffset = 12;
                for (int i = 0; i < count; i++)
                {
                    //MessageBox.Show("pos: " + newTocStream.Length);
                    tocStream.Seek(oldChunkOffset, SeekOrigin.Begin);
                    oldChunkOffset += 8;
                    int numOfNextFiles = tocStream.ReadValueS32();
                    //oldChunkOffset += 4;
                    //MessageBox.Show("numofnextfiles: " + numOfNextFiles + " at pos: 0x" + oldChunkOffset.ToString("X4"));

                    /*if (numOfNextFiles == 0)
                    {
                        //newChunkOffset -= 4;
                        newTocStream.Seek(newChunkOffset, SeekOrigin.Begin);
                        continue;
                    }*/

                    //newChunkOffset += 4;
                    newTocStream.Seek(newChunkOffset, SeekOrigin.Begin);
                    if(numOfNextFiles == 0)
                        newTocStream.WriteValueS32(0);
                    else
                        newTocStream.WriteValueS32((int)newTocStream.Length - newChunkOffset);
                    newTocStream.WriteValueS32(numOfNextFiles);
                    newChunkOffset = (int)newTocStream.Position;
                    newTocStream.Seek(0, SeekOrigin.End);

                    for (int j = 0; j < numOfNextFiles; j++)
                    {
                        short size;
                        tocStream.Seek(oldDataOffset, SeekOrigin.Begin);
                        size = tocStream.ReadValueS16();
                        tocStream.Seek(oldDataOffset, SeekOrigin.Begin);
                        oldDataOffset += size;
                        buffer = tocStream.ReadBytes(size);
                        newTocStream.Seek(newDataOffset, SeekOrigin.Begin);
                        newTocStream.WriteBytes(buffer);
                        newDataOffset = (int)newTocStream.Position;
                    }
                }
            }
        }
    }
}
