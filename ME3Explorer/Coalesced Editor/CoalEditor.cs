using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Gibbed.MassEffect3.FileFormats;
using NDesk.Options;
using Newtonsoft.Json;
using Gibbed.MassEffect3.FileFormats.Coalesced;

namespace ME3Explorer.Coalesced_Editor
{
    public partial class CoalEditor : Form
    {
        public CoalEditor()
        {
            InitializeComponent();
        }

        enum Mode
        {
            Unknown,
            ToJson,
            ToBin,
        }

        public string tempPath="";
        public List<string> files;

        public void LoadBIN(string path,string temp)
        {
            var mode = Mode.ToJson;
            if (mode == Mode.ToJson)
            {
                var inputPath = path;
                var outputPath = temp;

                using (var input = System.IO.File.OpenRead(inputPath))
                {
                    var coal = new CoalescedFile();
                    coal.Deserialize(input);

                    var padding = coal.Files.Count.ToString().Length;

                    var setup = new Setup
                    {
                        Endian = coal.Endian,
                        Version = coal.Version,
                    };

                    var counter = 0;
                    foreach (var file in coal.Files)
                    {
                        var iniPath = string.Format("{1}_{0}",
                            Path.GetFileNameWithoutExtension(file.Name),
                            counter.ToString().PadLeft(padding, '0'));
                        iniPath = Path.Combine(outputPath, Path.ChangeExtension(iniPath, ".json"));
                        counter++;

                        setup.Files.Add(Path.GetFileName(iniPath));

                        Directory.CreateDirectory(Path.GetDirectoryName(iniPath));
                        using (var output = System.IO.File.Create(iniPath))
                        {
                            var writer = new StreamWriter(output);
                            writer.Write(JsonConvert.SerializeObject(
                                new FileWrapper()
                                {
                                    Name = file.Name,
                                    Sections = file.Sections,
                                }, Formatting.Indented));
                            writer.Flush();
                        }
                    }

                    Directory.CreateDirectory(outputPath);
                    using (var output = System.IO.File.Create(Path.Combine(outputPath, "@coalesced.json")))
                    {
                        var writer = new StreamWriter(output);
                        writer.Write(JsonConvert.SerializeObject(
                            setup, Formatting.Indented));
                        writer.Flush();
                    }
                }
            }
        }

        public void SaveBIN(string path, string temp)
        {
            var mode = Mode.ToBin;
            var inputPath = temp;
            var outputPath = path;
            Setup setup;
            var setupPath = Path.Combine(inputPath, "@coalesced.json");
            using (var input = System.IO.File.OpenRead(setupPath))
            {
                var reader = new StreamReader(input);
                var text = reader.ReadToEnd();
                try
                {
                    setup = JsonConvert.DeserializeObject<Setup>(text);
                }
                catch (JsonReaderException e)
                {
                    return;
                }
            }

            var coal = new CoalescedFile
            {
                Endian = setup.Endian,
                Version = setup.Version,
            };

            foreach (var iniName in setup.Files)
            {
                string iniPath = Path.IsPathRooted(iniName) == false ?
                    Path.Combine(inputPath, iniName) : iniName;

                using (var input = System.IO.File.OpenRead(iniPath))
                {
                    var reader = new StreamReader(input);
                    var text = reader.ReadToEnd();

                    FileWrapper file;
                    try
                    {
                        file = JsonConvert.DeserializeObject<FileWrapper>(text);
                    }
                    catch (JsonReaderException e)
                    {
                        return;
                    }

                    coal.Files.Add(new Gibbed.MassEffect3.FileFormats.Coalesced.File()
                    {
                        Name = file.Name,
                        Sections = file.Sections,
                    }
                    );
                }
            }

            using (var output = System.IO.File.Create(outputPath))
            {
                coal.Serialize(output);
            }
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.bin|*.bin";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                tempPath = System.IO.Path.GetTempPath() + "CoalTemp\\";
                System.IO.DirectoryInfo folder = new DirectoryInfo(tempPath);
                if(folder.Exists)
                    foreach (FileInfo file in folder.GetFiles())
                        file.Delete();
                LoadBIN(d.FileName, tempPath);
                string[] filePaths = Directory.GetFiles(tempPath);
                files = new List<string>();
                for (int i = 0; i < filePaths.Length; i++)
                    if (filePaths[i][0] != (byte)'@')
                        files.Add(filePaths[i]);
                RefreshList();
            }
        }
        public void RefreshList()
        {
            listBox1.Items.Clear();
            for (int i = 0; i < files.Count(); i++)
            {
                string name = Path.GetFileNameWithoutExtension(files[i]);
                name = name.Substring(3) + ".ini";
                listBox1.Items.Add(name);
            }
        }        

        private void listBox1_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            System.IO.StreamReader myFile = new System.IO.StreamReader(files[listBox1.SelectedIndex]);
            rtb1.Text = myFile.ReadToEnd();
            myFile.Close();
            toolStripButton1.Enabled = false;
        }

        private void rtb1_TextChanged_1(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex != -1 && rtb1.Text != "")
            {
                toolStripButton1.Enabled = true;
            }
        }

        private void toolStripButton1_Click_1(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex != -1 && rtb1.Text != "")
            {
                FileStream fs = new FileStream(files[listBox1.SelectedIndex],FileMode.Create,FileAccess.Write);
                string s = rtb1.Text.ToString();
                MemoryStream m = new MemoryStream();
                for (int i = 0; i < s.Length; i++)
                    if (s[i] != 0xA)
                        m.WriteByte((byte)s[i]);
                    else
                    {
                        m.WriteByte(0xD);
                        m.WriteByte(0xA);
                    }
                fs.Write(m.ToArray(), 0, (int)m.Length);
                fs.Close();
                MessageBox.Show("Done.");
            }
            toolStripButton1.Enabled = false;
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.bin|*.bin";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                tempPath = System.IO.Path.GetTempPath() + "CoalTemp\\";
                SaveBIN(d.FileName, tempPath);
                MessageBox.Show("Done.");
            }
        }

        public void Search()
        {
            string s = toolStripTextBox1.Text;
            string t = rtb1.Text;
            int start = rtb1.SelectionStart + 1;
            int f = t.IndexOf(s, start);
            if(f!=-1)
            {
                rtb1.SelectionStart = f;
                rtb1.Focus();
            }
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            Search();
        }

        private void searchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Search();
        }

        private void toolStripTextBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 0xD)
                Search();
        }

        private void CoalEditor_Load(object sender, EventArgs e)
        {
            
        }

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string s = rtb1.Text;
            Clipboard.SetText(rtb1.SelectedText);
            if (rtb1.SelectedText.Length > 0)
            {
                string s2 = s.Substring(0, rtb1.SelectionStart);
                s2 += s.Substring(rtb1.SelectionStart + rtb1.SelectionLength, s.Length - (rtb1.SelectionStart + rtb1.SelectionLength));
                rtb1.Text = s2;
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(rtb1.SelectedText);
        }

        private void pasteToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            string s = (string)Clipboard.GetData("Text");
            if (rtb1.SelectionLength != 0)
            {
                rtb1.SelectedText = s;
            }
            else
            {
                string s2 = rtb1.Text.Substring(0, rtb1.SelectionStart);
                s2 += s;
                s2 += rtb1.Text.Substring(rtb1.SelectionStart, rtb1.Text.Length - rtb1.SelectionStart);
                rtb1.Text = s2;
            }
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string s = rtb1.Text;
            if (rtb1.SelectedText.Length > 0)
            {
                string s2 = s.Substring(0, rtb1.SelectionStart);
                s2 += s.Substring(rtb1.SelectionStart + rtb1.SelectionLength, s.Length - (rtb1.SelectionStart + rtb1.SelectionLength));
                rtb1.Text = s2;
            }
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtb1.Undo();
        }
    }
}
