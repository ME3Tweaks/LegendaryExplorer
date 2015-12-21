using KFreonLib.MEDirectories;
using ME3Explorer.SequenceObjects;
using ME3Explorer.Unreal;
using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Windows.Forms;
using ME3LibWV;

namespace ME3Explorer.InterpEditor
{
    public partial class InterpEditor : Form
    {
        public PCCObject pcc;
        public TalkFile talkfile;
        public string CurrentFile;
        public List<int> objects;

        public InterpEditor()
        {
            SText.fontcollection = LoadFont("KismetFont.ttf", 8);
            InitializeComponent();
            timeline.Scrollbar = vScrollBar1;
            timeline.GroupList.ScrollbarH = hScrollBar1;
            timeline.GroupList.tree1 = treeView1;
            timeline.GroupList.tree2 = treeView2;
            BitConverter.IsLittleEndian = true;
            objects = new List<int>();
        }

        public void InitTalkFile(Object editorTalkFile = null)
        {
            if (editorTalkFile == null)
            {
                var tlkPath = ME3Directory.cookedPath + "BIOGame_INT.tlk";
                talkfile = new TalkFile();
                talkfile.LoadTlkData(tlkPath);
            }
            else
            {
                talkfile = (TalkFile)editorTalkFile;
            }
        }

        private void openPCCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(ME3Directory.cookedPath) && talkfile == null)
            {
                MessageBox.Show("ME3 install directory not found. Set its path at:\n Options > Set Custom Path > Mass Effect 3\n\n Or, specify a .tlk file location with:\n File > Load Alternate TLK");
                return;
            }
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "PCC Files(*.pcc)|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                LoadPCC(d.FileName);
            }
        }

        public void LoadPCC(string fileName, Object editorTalkFile = null)
        {
            if (editorTalkFile != null)
            {
                InitTalkFile(editorTalkFile);
            }
            else
            {
                InitTalkFile(talkfile);
            }
            objects.Clear();
            pcc = new PCCObject(fileName);
            CurrentFile = fileName;
            for (int i = 0; i < pcc.Exports.Count; i++)
                if (pcc.Exports[i].ClassName == "InterpData")
                    objects.Add(i);
            RefreshCombo();
        }

        public void RefreshCombo()
        {
            if (objects == null)
                return;
            toolStripComboBox1.Items.Clear();
            foreach (int i in objects)
                toolStripComboBox1.Items.Add("#" + i + " : " + pcc.Exports[i].ObjectName);
            if (toolStripComboBox1.Items.Count != 0)
                toolStripComboBox1.SelectedIndex = 0;
        }

        public void loadInterpData(int index)
        {
            timeline.GroupList.LoadInterpData(index, pcc);
            timeline.GroupList.OnCameraChanged(timeline.Camera);
            timeline.GroupList.Talkfile = talkfile;
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            int n = toolStripComboBox1.SelectedIndex;
            if (n == -1)
                return;
            loadInterpData(objects[n]);
        }

        public static PrivateFontCollection LoadFont(string file, int fontSize)
        {
            PrivateFontCollection fontCollection = new PrivateFontCollection();
            fontCollection.AddFontFile(file);
            if (fontCollection.Families.Length < 0)
            {
                throw new InvalidOperationException("No font familiy found when loading font");
            }
            return fontCollection;
        }

        private void treeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {

        }

        private void loadAlternateTlkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.tlk|*.tlk";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (talkfile == null)
                {
                    talkfile = new TalkFile();
                }
                talkfile.LoadTlkData(d.FileName);
                timeline.GroupList.Talkfile = talkfile;
                MessageBox.Show("Done.");
            }
        }

        //for debugging purposes. not exposed to user
        private void InterpTrackScan_Click(object sender, EventArgs e)
        {
            KFreonLib.Debugging.DebugOutput.StartDebugger("Main ME3Explorer Form");
            string basepath = KFreonLib.MEDirectories.ME3Directory.cookedPath;
            string[] files = Directory.GetFiles(basepath, "*.pcc");
            List<string> conds = new List<string>();
            List<string> conds1 = new List<string>();

            //string property = Microsoft.VisualBasic.Interaction.InputBox("Please enter property name", "ME3 Explorer");
            string name;
            for (int f = 0; f < files.Length; f++)
            {
                string file = files[f];
                //KFreonLib.Debugging.DebugOutput.PrintLn((f + 1) + " / " + files.Length + " : " + file + " :", true);
                PCCPackage p = new PCCPackage(file, true, false, true);
                for (int i = 0; i < p.Exports.Count; i++)
                {
                    if (p.GetObjectClass(i).StartsWith("InterpTrackVisibility")) //GetObject(p.Exports[i].idxLink).StartsWith("InterpGroup"))
                    {
                        BitConverter.IsLittleEndian = true;
                        List<ME3LibWV.PropertyReader.Property> props = ME3LibWV.PropertyReader.getPropList(p, p.Exports[i].Data);
                        foreach (ME3LibWV.PropertyReader.Property prop in props)
                        {
                            //KFreonLib.Debugging.DebugOutput.PrintLn(p.GetName(prop.Name));
                            if (p.GetName(prop.Name) == "VisibilityTrack")
                            {
                                int pos = 28;
                                int count = BitConverter.ToInt32(prop.raw, 24);
                                for (int j = 0; j < count; j++)
                                {
                                    List<ME3LibWV.PropertyReader.Property> p2 = ME3LibWV.PropertyReader.ReadProp(p, prop.raw, pos);
                                    for (int k = 0; k < p2.Count; k++)
                                    {
                                        name = p.GetName(p2[k].Name);
                                        if (name == "Action")
                                        {
                                            if (!conds.Contains(p.GetName(BitConverter.ToInt32(p2[k].raw, 32))))
                                            {
                                                conds.Add(p.GetName(BitConverter.ToInt32(p2[k].raw, 32)));
                                                KFreonLib.Debugging.DebugOutput.PrintLn("Action " + p.GetName(BitConverter.ToInt32(p2[k].raw, 24)) + ", " + p.GetName(BitConverter.ToInt32(p2[k].raw, 32)) + "               at: #" + i + " " + p.GetObjectPath(i + 1) + p.GetObjectClass(i + 1) + "        in: " + file.Substring(file.LastIndexOf(@"\") + 1), false);
                                            }
                                        }
                                        else if (name == "ActiveCondition")
                                        {
                                            if (!conds1.Contains(p.GetName(BitConverter.ToInt32(p2[k].raw, 32))))
                                            {
                                                conds1.Add(p.GetName(BitConverter.ToInt32(p2[k].raw, 32)));
                                                KFreonLib.Debugging.DebugOutput.PrintLn("ActiveCondition " + p.GetName(BitConverter.ToInt32(p2[k].raw, 24)) + ", " + p.GetName(BitConverter.ToInt32(p2[k].raw, 32)) + "               at: #" + i + " " + p.GetObjectPath(i + 1) + p.GetObjectClass(i + 1) + "        in: " + file.Substring(file.LastIndexOf(@"\") + 1), false);
                                            }
                                        }
                                        pos += p2[k].raw.Length;
                                    }
                                }
                            }
                            //if (p.GetName(prop.Name) == property)
                            //{
                            //    if(!conds.Contains(p.GetName(BitConverter.ToInt32(prop.raw, 32))))
                            //    {
                            //        conds.Add(p.GetName(BitConverter.ToInt32(prop.raw, 32)));
                            //        KFreonLib.Debugging.DebugOutput.PrintLn(p.GetName(BitConverter.ToInt32(prop.raw, 24)) + ", " + p.GetName(BitConverter.ToInt32(prop.raw, 32)) + "               at: #" + i + " " + p.GetObjectPath(i + 1) + p.GetObjectClass(i + 1) + "        in: " + file.Substring(file.LastIndexOf(@"\") + 1), false);
                            //    }
                            //}
                        }
                        //KFreonLib.Debugging.DebugOutput.PrintLn(i + " : " + p.GetObjectClass(i + 1) + "               at: " + p.GetObjectPath(i + 1) + "        in: " + file.Substring(file.LastIndexOf(@"\") + 1), false);
                    }
                }
                Application.DoEvents();
            }
            KFreonLib.Debugging.DebugOutput.PrintLn();
            KFreonLib.Debugging.DebugOutput.PrintLn("*****************");
            KFreonLib.Debugging.DebugOutput.PrintLn("Done");
        }

        //private void openInPCCEditor2ToolStripMenuItem_Click(object sender, EventArgs e)
        //{
        //    int l = CurrentObjects[listBox1.SelectedIndex];
        //    if (l == -1)
        //        return;
        //    PCCEditor2 p = new PCCEditor2();
        //    p.MdiParent = this.MdiParent;
        //    p.WindowState = FormWindowState.Maximized;
        //    p.Show();
        //    p.pcc = new PCCObject(CurrentFile);
        //    p.SetView(2);
        //    p.RefreshView();
        //    p.InitStuff();
        //    p.listBox1.SelectedIndex = l;
        //}
    }
}