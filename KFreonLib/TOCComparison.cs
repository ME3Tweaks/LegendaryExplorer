using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BitConverter = KFreonLib.Misc.BitConverter;
using Gibbed.IO;

namespace KFreonLib
{
    public partial class TOCComparison : Form
    {
        class TOCEntryNode : TreeNode
        {
            public uint offset;
            public string name;
            public uint size;
            public TOCEntryNode Companion = null;
            public long ActualSize;

            public bool GoodCompanion
            {
                get
                {
                    return Companion != null;
                }
            }

            public string DisplayName
            {
                get
                {
                    return offset.ToString("X") + " : " + name + "   " + size.ToString() + " bytes";
                }
            }

            public bool GoodSize
            {
                get
                {
                    return GoodCompanion && Companion.size == size;
                }
            }

            public bool GoodProperSize
            {
                get
                {
                    return ActualSize == size;
                }
            }

            public bool GoodName
            {
                get
                {
                    return GoodCompanion && Companion.name == name;
                }
            }

            public bool GoodOffset
            {
                get
                {
                    return GoodCompanion && Companion.offset == offset;
                }
            }

            public bool GoodAll()
            {
                return GoodSize && GoodName && GoodOffset;
            }

            public TOCEntryNode(uint off, string nam, uint siz) : base()
            {
                offset = off;
                name = nam;
                size = siz;
                Text = DisplayName;
                ForeColor = Color.Red;
            }

            public TOCEntryNode() : base()
            {

            }

            internal void DoColour()
            {
                Font font = TreeView.DefaultFont;
                if (!GoodCompanion)
                {
                    ForeColor = Color.Gold;
                    Console.WriteLine("Bad companion");
                }

                if (!GoodOffset)
                {
                    font = new Font(font, FontStyle.Italic);
                    Console.WriteLine("Bad offset");
                }

                if (!GoodName)
                {
                    BackColor = Color.Blue;
                    Console.WriteLine("Bad name");
                }

                if (!GoodSize)
                {
                    ForeColor = Color.Red;
                    Console.WriteLine("Bad size");
                }

                if (!GoodProperSize)
                {
                    ForeColor = Color.Green;
                    Console.WriteLine("bad actual size. Actual: " + ActualSize + "  Read: " + size);
                }

                if (GoodAll())
                {
                    ForeColor = new TOCEntryNode().ForeColor;
                    Console.WriteLine("Bad all");
                }
            }
        }

        List<string> FirstStrings = new List<string>();
        List<string> SecondStrings = new List<string>();


        string f = null;
        string first 
        {
            get
            {
                return f;
            }
            set
            {
                f = value;
                FirstPathLabel.Text = f;
            }
        }
        string s = null;
        string second
        {
            get
            {
                return s;
            }
            set
            {
                s = value;
                SecondPathLabel.Text = s;
            }
        }

        public TOCComparison(List<string> paths = null)
        {
            InitializeComponent();

            if (paths == null)
            {
                FirstPathLabel.Text = "";
                SecondPathLabel.Text = "";
            }
            else
            {
                if (paths.Count != 2)
                    MessageBox.Show("Only two files can be compared.");
                else
                {
                    first = paths[0];
                    second = paths[1];
                }
            }
        }

        private void Checker(bool which)
        {
            string path;
            TreeNodeCollection nodes = null;
            if (which)
            {
                path = Path.GetDirectoryName(Path.GetDirectoryName(first));
                nodes = FirstTreeView.Nodes;
            }
            else
            {
                path = Path.GetDirectoryName(Path.GetDirectoryName(second));
                nodes = SecondTreeView.Nodes;
            }

            
            foreach (TOCEntryNode node in nodes)
            {
                FileInfo fi = new FileInfo(Path.Combine(path, node.name));
                node.ActualSize = fi.Length;
                //node.DoColour();
            }
        }

        private void FirstBrowseButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select TOC to compare to.";
                ofd.Filter = "PCCConsoleTOC.bin|PCConsoleTOC.bin";

                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.Cancel)
                {
                    first = ofd.FileName;
                    FirstTreeView.Nodes.AddRange(LoadTOCAsEntries(first).ToArray());
                    //Task.Run(() => Checker(true));
                }
            }
        }

        private void SecondBrowseButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select TOC to compare with.";
                ofd.Filter = "PCCConsoleTOC.bin|PCConsoleTOC.bin";

                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.Cancel)
                {
                    second = ofd.FileName;
                    SecondTreeView.Nodes.AddRange(LoadTOCAsEntries(second).ToArray());
                    //Task.Run(() => Checker(false));
                }
            }
        }

        private async void CompareButton_Click(object sender, EventArgs e)
        {
            if (first == null || second == null)
                MessageBox.Show("Need two files to do a comparison");
            else
            {
                await Task.Run(() => EntryCompare());
                FirstTreeView.Refresh();
                SecondTreeView.Refresh();
            }
        }

        private void EntryCompare()
        {
            int count = 0;
            while(true)
            {
                if (count < FirstTreeView.Nodes.Count && count < SecondTreeView.Nodes.Count)
                {
                    TOCEntryNode firstentry = (TOCEntryNode)FirstTreeView.Nodes[count];
                    TOCEntryNode secondentry = (TOCEntryNode)SecondTreeView.Nodes[count];
                    firstentry.Companion = secondentry;
                    secondentry.Companion = firstentry;

                    Checker(true);

                    firstentry.DoColour();
                    secondentry.DoColour();
                }
                else if (count >= FirstTreeView.Nodes.Count && count >= SecondTreeView.Nodes.Count)
                    break;
                count++;
            }
        }

        private List<string> StringCompare()
        {
            List<string> diffs = new List<string>();
            if (FirstStrings.Count != SecondStrings.Count)
                MessageBox.Show("Different counts! First: " + FirstStrings.Count + "  Second: " + SecondStrings.Count);
            else
                for (int i = 0; i < FirstStrings.Count; i++)
                    if (FirstStrings[i] != SecondStrings[i])
                        diffs.Add(FirstStrings[i] + "  " + SecondStrings[i]);

            return diffs;
        }


        #region TOC Stuff
        public struct Inventory
        {
            public uint offset;
            public string name;
            public uint size;
        }

        private List<TOCEntryNode> LoadTOCAsEntries(string path)
        {
            BitConverter.IsLittleEndian = true;
            FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            int memsize = (int)fileStream.Length;
            byte[] memory = new byte[memsize];
            int count;
            int sum = 0;
            while ((count = fileStream.Read(memory, sum, memsize - sum)) > 0) sum += count;
            fileStream.Close();
            List<TOCEntryNode> retval = new List<TOCEntryNode>();


            if (memsize > 0)
            {
                List<Inventory> content = new List<Inventory>();
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
                for (int i = 0; i < content.Count(); i++)
                {
                    TOCEntryNode entry = new TOCEntryNode(content[i].offset, content[i].name, content[i].size);
                    retval.Add(entry);
                    //retval.Add(content[i].offset.ToString("X") + " : " + content[i].name + " (bytes)" + content[i].size.ToString());
                }
            }
            return retval;
        }

        private List<string> LoadTOCASStrings(string path)
        {
            BitConverter.IsLittleEndian = true;
            FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            int memsize = (int)fileStream.Length;
            byte[] memory = new byte[memsize];
            int count;
            int sum = 0;
            while ((count = fileStream.Read(memory, sum, memsize - sum)) > 0) sum += count;
            fileStream.Close();
            List<string> retval = new List<string>();


            if (memsize > 0)
            {
                List<Inventory> content = new List<Inventory>();
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
                for (int i = 0; i < content.Count(); i++)
                {
                    retval.Add(content[i].offset.ToString("X") + " : " + content[i].name + " (bytes)" + content[i].size.ToString());
                }
            }
            return retval;
        }
        #endregion
    }
}
