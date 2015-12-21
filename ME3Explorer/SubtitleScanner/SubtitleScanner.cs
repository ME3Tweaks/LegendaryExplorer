using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using KFreonLib.Debugging;
using KFreonLib.MEDirectories;
using UsefulThings;

namespace ME3Explorer.SubtitleScanner
{
    public partial class SubtitleScanner : Form
    {
        public struct EntryStruct
        {
            public string text;
            public string convname;
            public string pathpcc;
            public string pathdlc;
            public string pathafc;
            public string speaker;
            public int indexpcc;
            public int ID;
            public bool inDLC;
        }

        public List<EntryStruct> Entries;
        public TalkFile talkFile;

        public void InitTalkFile(Object editorTalkFile = null)
        {
            if (editorTalkFile == null)
            {
                var tlkPath = ME3Directory.cookedPath + "BIOGame_INT.tlk";
                talkFile = new TalkFile();
                talkFile.LoadTlkData(tlkPath);
            }
            else
            {
                talkFile = (TalkFile)editorTalkFile;
            }
        }

        public SubtitleScanner()
        {
            if (String.IsNullOrEmpty(ME3Directory.cookedPath))
            {
                MessageBox.Show("This tool requires ME3 to be installed. Set its path at:\n Options > Set Custom Path > Mass Effect 3");
                this.Close();
                return;
            }
            InitializeComponent();
            BitConverter.IsLittleEndian = true;
        }

        private void startScanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult res = MessageBox.Show("Scan DLCs too?", "", MessageBoxButtons.YesNo);
            bool scanDLC = (res == System.Windows.Forms.DialogResult.Yes);            
            DebugOutput.StartDebugger("Subtitle Scanner");
            Entries = new List<EntryStruct>();
            InitTalkFile();
            ScanBasefolder();
            if (scanDLC)
            {
                ScanDLCfolder1();
                ScanDLCfolder2();
            }
            RefreshDisplay();
        }

        public void ScanBasefolder()
        {
            string dir = ME3Directory.cookedPath;
            string[] files = Directory.GetFiles(dir, "*.pcc");
            pbar1.Maximum = files.Length - 1;
            int count = 0;
            foreach (string file in files)
            {
                DebugOutput.PrintLn("Scan file #" + count + " : " + file, count % 10 == 0);
                try
                {
                    PCCObject pcc = new PCCObject(file);
                    for (int i = 0; i < pcc.Exports.Count; i++)
                        if (pcc.Exports[i].ClassName == "BioConversation")
                        {
                            DebugOutput.PrintLn("Found dialog \"" + pcc.Exports[i].ObjectName + "\"", false);
                            BioConversation Dialog = new BioConversation(pcc, i);
                            foreach (BioConversation.EntryListStuct e in Dialog.EntryList)
                            {
                                string text = talkFile.findDataById(e.refText);
                                if (text.Length != 7 && text != "No Data")
                                {
                                    EntryStruct t = new EntryStruct();
                                    t.inDLC = false;
                                    t.text = text;
                                    t.ID = e.refText;
                                    t.indexpcc = i;
                                    t.pathafc = "";//Todo
                                    t.pathdlc = "";
                                    t.pathpcc = file;
                                    t.convname = pcc.Exports[i].ObjectName;
                                    if (e.SpeakerIndex >= 0 && e.SpeakerIndex < Dialog.SpeakerList.Count)
                                        t.speaker = pcc.getNameEntry(Dialog.SpeakerList[e.SpeakerIndex]);
                                    else
                                        t.speaker = "unknown";
                                    if (t.speaker == null || t.speaker == "")
                                        t.speaker = "unknown";
                                    Entries.Add(t);
                                    DebugOutput.PrintLn("Requ.: ("+ t.speaker + ") " + t.text, false);
                                }
                            }
                            foreach (BioConversation.ReplyListStruct e in Dialog.ReplyList)
                            {
                                string text = talkFile.findDataById(e.refText);
                                if (text.Length != 7 && text != "No Data")
                                {
                                    EntryStruct t = new EntryStruct();
                                    t.inDLC = false;
                                    t.text = text;
                                    t.ID = e.refText;
                                    t.indexpcc = i;
                                    t.pathafc = "";//Todo
                                    t.pathdlc = "";
                                    t.pathpcc = file;
                                    t.convname = pcc.Exports[i].ObjectName;
                                    Entries.Add(t);
                                    DebugOutput.PrintLn("Reply: " + t.text, false);
                                }
                            }
                        }
                    if (count % 10 == 0)
                    {
                        Application.DoEvents();
                        pbar1.Value = count;
                    }
                    count++;
                }
                catch (Exception ex)
                {
                    DebugOutput.PrintLn("=====ERROR=====\n" + ex.ToString() + "\n=====ERROR=====");
                }
            }
        }

        public void ScanDLCfolder1()
        {
            DebugOutput.PrintLn("\n\nDLC Scan for unpacked files...\n", true);
            string dir = ME3Directory.DLCPath;
            string[] files = Directory.GetFiles(dir, "*.pcc", SearchOption.AllDirectories);
            if (files.Length == 0)
                return;
            pbar1.Maximum = files.Length - 1;
            int count = 0;
            foreach (string file in files)
                if (!file.ToLower().Contains("patch"))
                {
                    DebugOutput.PrintLn("Scan file #" + count + " : " + file, count % 10 == 0);
                    try
                    {
                        PCCObject pcc = new PCCObject(file);
                        for (int i = 0; i < pcc.Exports.Count; i++)
                            if (pcc.Exports[i].ClassName == "BioConversation")
                            {
                                DebugOutput.PrintLn("Found dialog \"" + pcc.Exports[i].ObjectName + "\"", false);
                                BioConversation Dialog = new BioConversation(pcc, i);
                                foreach (BioConversation.EntryListStuct e in Dialog.EntryList)
                                {
                                    string text = talkFile.findDataById(e.refText);
                                    if (text.Length != 7 && text != "No Data")
                                    {
                                        EntryStruct t = new EntryStruct();
                                        t.inDLC = false;
                                        t.text = text;
                                        t.ID = e.refText;
                                        t.indexpcc = i;
                                        t.pathafc = "";//Todo
                                        t.pathdlc = "";
                                        t.pathpcc = file;
                                        t.convname = pcc.Exports[i].ObjectName;
                                        if (e.SpeakerIndex >= 0 && e.SpeakerIndex < Dialog.SpeakerList.Count)
                                            t.speaker = pcc.getNameEntry(Dialog.SpeakerList[e.SpeakerIndex]);
                                        else
                                            t.speaker = "unknown";
                                        if (t.speaker == null || t.speaker == "")
                                            t.speaker = "unknown";
                                        Entries.Add(t);
                                        DebugOutput.PrintLn("Requ.: (" + t.speaker + ") " + t.text, false);
                                    }
                                }
                                foreach (BioConversation.ReplyListStruct e in Dialog.ReplyList)
                                {
                                    string text = talkFile.findDataById(e.refText);
                                    if (text.Length != 7 && text != "No Data")
                                    {
                                        EntryStruct t = new EntryStruct();
                                        t.inDLC = false;
                                        t.text = text;
                                        t.ID = e.refText;
                                        t.indexpcc = i;
                                        t.pathafc = "";//Todo
                                        t.pathdlc = "";
                                        t.pathpcc = file;
                                        t.convname = pcc.Exports[i].ObjectName;
                                        Entries.Add(t);
                                        DebugOutput.PrintLn("Reply: " + t.text, false);
                                    }
                                }
                            }
                        if (count % 10 == 0)
                        {
                            Application.DoEvents();
                            pbar1.Value = count;
                        }
                        count++;
                    }
                    catch (Exception ex)
                    {
                        DebugOutput.PrintLn("=====ERROR=====\n" + ex.ToString() + "\n=====ERROR=====");
                    }
                }
        }

        public void ScanDLCfolder2()
        {
            DebugOutput.PrintLn("\n\nDLC Scan for packed files...\n", true);
            string dir = ME3Directory.DLCPath;
            string[] files = Directory.GetFiles(dir, "*.sfar", SearchOption.AllDirectories);
            if (files.Length == 0)
                return;
            pbar1.Maximum = files.Length - 1;
            int count = 0;
            foreach (string file in files)
                if (!file.ToLower().Contains("patch"))
                {
                    DebugOutput.PrintLn("Scan file #" + count + " : " + file, count % 10 == 0);
                    try
                    {
                        DLCPackage dlc = new DLCPackage(file);
                        DebugOutput.PrintLn("found " + dlc.Files.Length + " files : " + file);
                        for (int j = 0; j < dlc.Files.Length; j++)
                        {
                            if (dlc.Files[j].FileName.ToLower().EndsWith(".pcc"))
                            {
                                string filename = dlc.Files[j].FileName;
                                DebugOutput.PrintLn(" " + j.ToString("d4") + " / " + dlc.Files.Length.ToString("d4") + " : opening " + Path.GetFileName(filename),true);
                                MemoryStream mem = dlc.DecompressEntry(j);
                                File.WriteAllBytes("temp.pcc", mem.ToArray());
                                PCCObject pcc = new PCCObject("temp.pcc");
                                for (int i = 0; i < pcc.Exports.Count; i++)
                                    if (pcc.Exports[i].ClassName == "BioConversation")
                                    {
                                        DebugOutput.PrintLn("Found dialog \"" + pcc.Exports[i].ObjectName + "\"", false);
                                        BioConversation Dialog = new BioConversation(pcc, i);
                                        foreach (BioConversation.EntryListStuct e in Dialog.EntryList)
                                        {
                                            string text = talkFile.findDataById(e.refText);
                                            if (text.Length != 7 && text != "No Data")
                                            {
                                                EntryStruct t = new EntryStruct();
                                                t.inDLC = true;
                                                t.text = text;
                                                t.ID = e.refText;
                                                t.indexpcc = i;
                                                t.pathafc = "";//Todo
                                                t.pathdlc = file;
                                                t.pathpcc = filename;
                                                t.convname = pcc.Exports[i].ObjectName;
                                                if (e.SpeakerIndex >= 0 && e.SpeakerIndex < Dialog.SpeakerList.Count)
                                                    t.speaker = pcc.getNameEntry(Dialog.SpeakerList[e.SpeakerIndex]);
                                                else
                                                    t.speaker = "unknown";
                                                if (t.speaker == null || t.speaker == "")
                                                    t.speaker = "unknown";
                                                Entries.Add(t);
                                                DebugOutput.PrintLn("Requ.: (" + t.speaker + ") " + t.text, false);
                                            }
                                        }
                                        foreach (BioConversation.ReplyListStruct e in Dialog.ReplyList)
                                        {
                                            string text = talkFile.findDataById(e.refText);
                                            if (text.Length != 7 && text != "No Data")
                                            {
                                                EntryStruct t = new EntryStruct();
                                                t.inDLC = true;
                                                t.text = text;
                                                t.ID = e.refText;
                                                t.indexpcc = i;
                                                t.pathafc = "";//Todo
                                                t.pathdlc = file;
                                                t.pathpcc = filename;
                                                t.convname = pcc.Exports[i].ObjectName;
                                                Entries.Add(t);
                                                DebugOutput.PrintLn("Reply: " + t.text, false);
                                            }
                                        }
                                    }
                            }
                        }
                        if (count % 10 == 0)
                        {
                            Application.DoEvents();
                            pbar1.Value = count;
                        }
                        count++;
                    }
                    catch (Exception ex)
                    {
                        DebugOutput.PrintLn("=====ERROR=====\n" + ex.ToString() + "\n=====ERROR=====");
                    }
                }
            if (File.Exists("temp.pcc"))
                File.Delete("temp.pcc");
        }


        public void RefreshDisplay()
        {
            listBox1.Items.Clear();
            int count = 0;
            listBox1.Visible = false;
            foreach (EntryStruct e in Entries)
                listBox1.Items.Add((count++).ToString("d8") + " : ID(" + e.ID + ") Speaker(" + e.speaker + ") " + e.text);
            listBox1.Visible = true;
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            EntryStruct t = Entries[n];
            string s = "Entry #" + n + "\n\n" +
                       "ID\t\t\t: " + t.ID + " = 0x" + t.ID.ToString("X8") + "\n" +
                       "Text\t\t\t: " + t.text.Trim() + "\n" +
                       "Speaker\t\t: " + t.speaker + "\n" +
                       "Dialog Name\t\t: " + t.convname + "\n" +
                       "Dialog Index\t: " + t.indexpcc + "\n" +
                       "Is in DLC\t\t: " + t.inDLC + "\n" +
                       "PCC path\t\t: " + t.pathpcc + "\n" +
                       "DLC path\t\t: " + t.pathdlc + "\n";// +
                       //"AFC path\t:\n"; *todo*
            rtb1.Text = s;
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            Search();
        }

        public void Search()
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                n = 0;
            else
                n++;
            for (int i = n; i < listBox1.Items.Count; i++)
                if (listBox1.Items[i].ToString().ToLower().Contains(toolStripTextBox1.Text.ToLower()))
                {
                    listBox1.SelectedIndex = i;
                    break;
                }
        }

        private void saveResultToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.subdb|*.subdb";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SaveToDB(d.FileName);
                MessageBox.Show("Done.");
            }
        }

        public void SaveToDB(string path)
        {
            int count = 0;
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            try
            {
                FH.WriteInt(fs, Entries.Count);
                foreach (EntryStruct e in Entries)
                {
                    FH.WriteString(fs, e.text);
                    if (e.speaker != null && e.speaker != "")
                        FH.WriteString(fs, e.speaker);
                    else
                        FH.WriteString(fs, "unknown");
                    FH.WriteString(fs, e.convname);
                    FH.WriteString(fs, e.pathpcc);
                    FH.WriteString(fs, e.pathdlc);
                    FH.WriteString(fs, e.pathafc);
                    FH.WriteInt(fs, e.ID);
                    FH.WriteInt(fs, e.indexpcc);
                    if (e.inDLC)
                        fs.WriteByte(1);
                    else
                        fs.WriteByte(0);
                    count++;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Error : \n" + e.Message);
            }
            fs.Close();
        }

        public void LoadFromDB(string path)
        {
            try
            {
                Entries = new List<EntryStruct>();
                FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                int count = FH.GetInt(fs);
                for (int i = 0; i < count; i++) 
                {
                    EntryStruct e = new EntryStruct();
                    e.text = FH.ReadString(fs);
                    e.speaker = FH.ReadString(fs);
                    e.convname = FH.ReadString(fs);
                    e.pathpcc = FH.ReadString(fs);
                    e.pathdlc = FH.ReadString(fs);
                    e.pathafc = FH.ReadString(fs);
                    e.ID = FH.GetInt(fs);
                    e.indexpcc = FH.GetInt(fs);
                    byte b = (byte)fs.ReadByte();
                    e.inDLC = b == 1;
                    Entries.Add(e);
                }
                fs.Close();
                RefreshDisplay();
            }
            catch (Exception e)
            {
                MessageBox.Show("Error : \n" + e.Message);
            }
        }

        private void loadResultToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.subdb|*.subdb";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                LoadFromDB(d.FileName);
                MessageBox.Show("Done.");
            }
        }

        private void toolStripTextBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
                Search();
        }
    }
}
