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
        public int memsize = 0;
        public List<Inventory> content;
        public string lastsearch = ".pcc";

        public TOCeditor()
        {
            //FemShep's Mod Manager 3 automator for TOCEditor.
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Length > 2)
            {
                //try
                //{
                string cmdCommand = arguments[1];
                if (cmdCommand.Equals("-toceditorupdate", StringComparison.Ordinal))
                {
                    if (arguments.Length % 2 != 1 || arguments.Length < 5)
                    {
                        MessageBox.Show("Wrong number of arguments for automated TOC update.\nSyntax is: <exe> -toceditorupdate <TOCFILE> <UPDATESIZEFILE> <SIZE> [<UPDATESIZEFILE> <SIZE>]...", "ME3Explorer TOCEditor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    string tocfile = arguments[2];
                    int numfiles = (arguments.Length - 3) / 2;

                    string[] searchTerms = new string[numfiles];
                    string[] filesizes = new string[numfiles];

                    int argnum = 3; //starts at 3
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
                            Environment.Exit(1);
                            Application.Exit();
                        }
                        editFilesize(n, filesizes[i]);
                    }
                    saveTOC(tocfile);
                    Environment.Exit(0);
                    Application.Exit();
                }
                //}
            } //end automation
        }

        public bool UpdateFile(string name, uint size, string path)
        {
            loadTOCfile(path);
            int n = -1;
            for (int i = 0; i < content.Count(); i++)
                if (content[i].name.EndsWith(name))
                    n = i;
            if (n == -1) return false;
            //edit entry
            Inventory temp = content[n];
            temp.size = size;
            content[n] = temp;
            uint pos = temp.offset;
            BitConverter.IsLittleEndian = true;
            byte[] buff = BitConverter.GetBytes(size);
            for (int i = 0; i < 4; i++)
                memory[pos + i] = buff[i];
            //write file
            FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
            for (int i = 0; i < memsize; i++)
            {
                fileStream.WriteByte(memory[i]);
            }
            fileStream.Close();
            return true;
        }

        private void loadTOCfile(string path)
        {
            BitConverter.IsLittleEndian = true;
            FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            memsize = (int)fileStream.Length;
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
            lastsearch = search;
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
            for (int i = 0; i < memsize; i++)
            {
                fileStream.WriteByte(memory[i]);
            }
            fileStream.Close();
        }

    }
}
