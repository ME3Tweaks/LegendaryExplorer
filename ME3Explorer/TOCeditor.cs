using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Gibbed.IO;

namespace ME3Explorer
{
    public class TOCeditor
    {
        public struct Inventory
        {
            public uint offset;
            public string name;
            public uint size;
        }

        public byte[] memory;
        public List<Inventory> content;

        public int updateTOCFromCommandLine(IList<string> arguments)
        {
            if (arguments.Count % 2 != 1 || arguments.Count < 3)
            {
                MessageBox.Show("Wrong number of arguments for automated TOC update.\nSyntax is: <exe> -toceditorupdate <TOCFILE> <UPDATESIZEFILE> <SIZE> [<UPDATESIZEFILE> <SIZE>]...", "ME3Explorer TOCEditor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            string tocfile = arguments[0];
            int numfiles = (arguments.Count - 1) / 2;

            string[] searchTerms = new string[numfiles];
            string[] filesizes = new string[numfiles];

            int argnum = 1;
            for (int i = 0; i < searchTerms.Length; i++)
            {
                searchTerms[i] = arguments[argnum];
                argnum++;
                filesizes[i] = arguments[argnum];
                argnum++;
            }

            loadTOCfile(tocfile);
            for (int i = 0; i < numfiles; i++)
            {
                int n = searchFirstResult(searchTerms[i]);
                if (n == -1)
                {
                    MessageBox.Show("The filepath in this PCConsoleTOC.bin file was not found. Unable to proceed.\nFile path in this TOC not found:\n" + tocfile + "\n" + searchTerms[i], "ME3Explorer TOCEditor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
                editFilesize(n, filesizes[i]);
            }
            saveTOC(tocfile);
            return 0;
        }

        private void loadTOCfile(string path)
        {
            
            FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            int memsize = (int)fileStream.Length;
            memory = new byte[memsize];
            int count;
            int sum = 0;
            while ((count = fileStream.Read(memory, sum, memsize - sum)) > 0) sum += count;
            fileStream.Close();
            if (memsize > 0)
            {
                content = new List<Inventory>();
                Inventory temp = new Inventory();
                MemoryStream myStream = new MemoryStream(memory);
                if (myStream.ReadValueU32() == 0x3AB70C13)
                {
                    myStream.Seek(8, SeekOrigin.Begin);
                    uint jumpSize = myStream.ReadValueU32();
                    myStream.Seek(jumpSize * 8, SeekOrigin.Current);
                    uint blockSize;
                    do
                    {
                        temp = new Inventory();
                        long position = myStream.Position;
                        uint value = myStream.ReadValueU32();
                        blockSize = value & 0xFFFF;
                        uint offset = (uint)myStream.Position;
                        uint fileSize = myStream.ReadValueU32();
                        myStream.Seek(20, SeekOrigin.Current);


                        string filePath = myStream.ReadStringZ();

                        myStream.Seek(position + blockSize, SeekOrigin.Begin);

                        temp.name = filePath;
                        temp.offset = offset;
                        temp.size = fileSize;
                        content.Add(temp);

                    } while (blockSize != 0);
                }
                myStream.Close();
            }
        }

        private int searchFirstResult(string search)
        {
            for (int i = 0; i < content.Count(); i++)
            {
                if (content[i].name.Contains(search))
                {
                    return i;
                }
            }
            return -1;
        }

        private void editFilesize(int n, string newsize)
        {
            Inventory temp = content[n];
            try
            {
                temp.size = Convert.ToUInt32(newsize);
            }
            catch (FormatException)
            {
                //exit this method
                return;
            }
            content[n] = temp;
            uint pos = temp.offset;
            byte[] buff = BitConverter.GetBytes(temp.size);
            for (int i = 0; i < 4; i++)
            {
                memory[pos + i] = buff[i];
            }
        }
        
        private void saveTOC(string path)
        {
            FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
            for (int i = 0; i < memory.Length; i++)
            {
                fileStream.WriteByte(memory[i]);
            }
            fileStream.Close();
        }

    }
}
