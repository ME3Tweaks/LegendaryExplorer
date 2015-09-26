using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Gibbed.IO;

namespace ME3Explorer
{
    public static class TOCbinUpdater
    {
        public struct Inventory
        {
            public uint offset;
            public string name;
            public uint size;
        }

        public static byte[] memory;
        public static int memsize = 0;
        public static List<Inventory> content;

        public static void UpdateTocBin(string TOC, string basedir, ListBox lb, ToolStripProgressBar pb)
        {
            BitConverter.IsLittleEndian = true;
            loadTOCfile(TOC);
            if (lb != null)
            {
                lb.Items.Clear();
                lb.Visible = false;
            }
            if (pb != null)
            {
                pb.Minimum = 0;
                pb.Maximum = content.Count() - 1;
            }
            List<int> updates = new List<int>();
            List<uint> sizes = new List<uint>();
            for (int i = 0; i < content.Count(); i++)
            {
                string pre = i.ToString("d4") + " ";
                Inventory inv = content[i];
                if (File.Exists(basedir + inv.name))
                {
                    FileStream fs = new FileStream(basedir + inv.name, FileMode.Open, FileAccess.Read);
                    uint size = (uint)fs.Length;
                    fs.Close();
                    if (size == inv.size)
                        pre += "OK  : ";
                    else
                    {
                        pre += "UPDT: ";
                        updates.Add(i);
                        byte[] buff = BitConverter.GetBytes(size);
                        for (int j = 0; j < 4; j++)
                            memory[inv.offset + j] = buff[j];
                        sizes.Add(size);
                    }
                }
                else
                    pre += "NFND: ";
                pre += inv.name;
                if(pb != null) pb.Value = i;
                if(lb !=null) lb.Items.Add(pre);
                Application.DoEvents();
            }
            if (lb != null) lb.Items.Add("");
            if (lb != null) lb.Items.Add("===========================");
            if (updates.Count() > 0)
            {
                if (lb != null)
                {
                    lb.Items.Add("Result: " + updates.Count() + " Updates");
                    for (int i = 0; i < updates.Count(); i++)
                        lb.Items.Add(updates[i].ToString("d4") + " : " + content[updates[i]].name + " old Size: " + content[i].size + " new Size: " + sizes[i]);
                    lb.SelectedIndex = lb.Items.Count - 1;
                    lb.Visible = true;
                }
                if ((pb == null && lb == null) || MessageBox.Show("Found " + updates.Count() + " updates, want to apply and save them?", "ME3Explorer", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    FileStream fs = new FileStream(TOC, FileMode.Create, FileAccess.Write);
                    fs.Write(memory, 0, memsize);
                    fs.Close();
                    if (lb != null && pb!=null) 
                        MessageBox.Show("Done.");
                }
            }
            else
            {
                if (lb != null)
                {
                    lb.Items.Add("No updates found");
                    lb.SelectedIndex = lb.Items.Count - 1;
                    lb.Visible = true;
                }
            }
        }

        public static void loadTOCfile(string path)
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
    }
}
