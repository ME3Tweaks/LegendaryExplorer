using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using ME3Explorer.Unreal;
using KFreonLib.Debugging;
using KFreonLib.MEDirectories;

namespace ME3Explorer.Property_Dumper
{
    public partial class PropDumper : Form
    {

        PCCObject pcc;

        public PropDumper()
        {
            InitializeComponent();
        }

        private void makePropDumpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pcc = new PCCObject(d.FileName);
                LetsDump();
            }
        }

        public void LetsDump()
        {
            pb1.Minimum = 0;
            pb1.Maximum = pcc.Exports.Count;
            rtb1.Text = "";
            int count = 0;
            string t = "";
            for (int i = 0; i < pcc.Exports.Count; i++)
            {
                PCCObject.ExportEntry e = pcc.Exports[i];
                string s = "Properties for Object #" + i + " \"" + e.ObjectName + "\" :\n\n";
                List<PropertyReader.Property> p = PropertyReader.getPropList(pcc, e.Data);
                foreach (PropertyReader.Property prop in p)
                    s += PropertyReader.PropertyToText(prop, pcc) + "\n";
                s += "\n";
                t += s;
                if (count++ > 100)
                {                    
                    count = 0;
                    pb1.Value = i;
                    Status.Text = "State : " + i + " / " + pcc.Exports.Count;
                    Application.DoEvents();
                }
            }
            Status.Text = "State : Done";
            rtb1.Text = t;
            rtb1.SelectionStart = rtb1.TextLength;
            rtb1.SelectionLength = 0;
            rtb1.ScrollToCaret();
            pb1.Value = 0;
        }

        public string DumpArray(PCCObject pcc,byte [] raw, int pos, string s, int depth)
        {
            string res = "";
            List<PropertyReader.Property> p = PropertyReader.ReadProp(pcc, raw, pos);
            for (int i = 0; i < p.Count; i++)
            {
                if (p[i].TypeVal == PropertyReader.Type.StringRefProperty)
                {
                    for (int j = 0; j < depth; j++)
                        res += "\t";
                    res += i + " : " + PropertyReader.PropertyToText(p[i], pcc) + "\n";
                }
                if (p[i].TypeVal == PropertyReader.Type.ArrayProperty)
                {
                    //for (int j = 0; j < depth; j++)
                    //    res += "\t";
                    //res += "in Property #" + i + " : " + PropertyReader.PropertyToText(p[i], pcc) + "\n";
                    res += DumpArray(pcc, raw, p[i].offsetval + 4, res, depth + 1);
                }
                if (p[i].TypeVal == PropertyReader.Type.StructProperty)
                {
                    //for (int j = 0; j < depth; j++)
                    //    res += "\t";
                    //res += "in Property #" + i + " : " + PropertyReader.PropertyToText(p[i], pcc) + "\n";
                    res += DumpArray(pcc, raw, p[i].offsetval + 8, res, depth + 1);
                }
            }
            return res;
        }

        private void makeDialogDumpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = ME3Directory.cookedPath;
            string[] files = Directory.GetFiles(path);
            pb1.Minimum = 0;            
            rtb1.Text = "";
            int count = 0;
            int count2 = 0;
            string t = "";
            pauseToolStripMenuItem.Visible = true;
            pb2.Value = 0;
            pb2.Maximum = files.Length;
            for (int i = 0; i < files.Length; i++)
                if (files[i].ToLower().EndsWith(".pcc"))
                {
                    
                    while(pause)
                        Application.DoEvents();
                    pcc = new PCCObject(files[i]);
                    pb1.Maximum = pcc.Exports.Count;
                    pb2.Value = i;
                    string s = "String references for file " + files[i] + "\n";
                    for (int j = 0; j < pcc.Exports.Count; j++)
                    {
                        PCCObject.ExportEntry ent = pcc.Exports[j];
                        List<PropertyReader.Property> p = PropertyReader.getPropList(pcc, ent.Data);
                        
                        for (int k = 0; k < p.Count; k++)
                        {
                            PropertyReader.Property prop = p[k];
                            if (prop.TypeVal == PropertyReader.Type.StringRefProperty)
                                s += "Object #" + j + " : " + PropertyReader.PropertyToText(prop, pcc) + "\n";
                            if (prop.TypeVal == PropertyReader.Type.ArrayProperty)
                            {
                                string tt = DumpArray(pcc, ent.Data, prop.offsetval + 4, s, 1);
                                if (tt.Length != 0)
                                {
                                    s += "Object #" + j + " in : " + PropertyReader.PropertyToText(prop, pcc) + "\n";
                                    s += tt;
                                }   
                            }
                            if (prop.TypeVal == PropertyReader.Type.StructProperty)
                            {
                                string tt = DumpArray(pcc, ent.Data, prop.offsetval + 8, s, 1);
                                if (tt.Length != 0)
                                {
                                    s += "Object #" + j + " in : " + PropertyReader.PropertyToText(prop, pcc) + "\n";
                                    s += tt;
                                }
                            }
                        }
                        if (count++ > 500)
                        {
                            count = 0;
                            pb1.Value = j;
                            Status.Text = "State : " + j + " / " + pcc.Exports.Count;
                            if (count2++ > 10)
                            {
                                count2 = 0;
                                rtb1.Text = t;
                                rtb1.SelectionStart = rtb1.TextLength;
                                rtb1.ScrollToCaret();
                                rtb1.Visible = true;
                            }
                            Application.DoEvents();
                        }

                    }
                    t += s + "\n";
                }
            Status.Text = "State : Done";
            rtb1.Text = t;
            rtb1.SelectionStart = rtb1.TextLength;
            rtb1.ScrollToCaret();
            pb1.Value = 0;
            pauseToolStripMenuItem.Visible = false;
        }

        private void saveDumpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.txt|*txt";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                rtb1.SaveFile(d.FileName, RichTextBoxStreamType.PlainText);
        }

        public bool pause = false;

        private void pauseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            pause = (!pause);
        }

        private void PropDumper_FormClosing(object sender, FormClosingEventArgs e)
        {
            pause = false;
        }

        private void makePropDumpForClassToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LetsDump2(Microsoft.VisualBasic.Interaction.InputBox("Please enter class name", "ME3 Explorer", "", 0, 0));
        }

        public void LetsDump2(string classname)
        {
            string path = ME3Directory.cookedPath;
            string[] files = Directory.GetFiles(path,"*.pcc");
            pb1.Minimum = 0;
            rtb1.Text = "";
            pauseToolStripMenuItem.Visible = true;
            pb2.Value = 0;
            pb2.Maximum = files.Length;
            List<string> Names = new List<string>();
            List<string> Types = new List<string>();
            List<string> First = new List<string>();
            DebugOutput.Clear();
            for (int i = 0; i < files.Length; i++)
                {

                    while (pause)
                        Application.DoEvents();
                    pcc = new PCCObject(files[i]);
                    DebugOutput.PrintLn(i + "/" + files.Length + " Scanning file : " + Path.GetFileName(files[i]));
                    pb1.Maximum = pcc.Exports.Count;
                    pb2.Value = i;
                    for (int j = 0; j < pcc.Exports.Count; j++)
                    {
                        PCCObject.ExportEntry ent = pcc.Exports[j];
                        if (ent.ClassName == classname)
                        {
                            List<PropertyReader.Property> p = PropertyReader.getPropList(pcc, ent.Data);
                            for (int k = 0; k < p.Count; k++)
                            {
                                PropertyReader.Property prop = p[k];
                                int found = -1;
                                for (int l = 0; l < Names.Count(); l++)
                                    if (pcc.getNameEntry(prop.Name) == Names[l])
                                        found = l;
                                if (found == -1)
                                {
                                    Names.Add(pcc.getNameEntry(prop.Name));
                                    Types.Add(PropertyReader.TypeToString((int)prop.TypeVal));
                                    First.Add(Path.GetFileName(files[i]) + " #" + j);
                                }
                            }
                        }                        
                        if (j%500 == 0)
                        {
                            pb1.Value = j;
                            Status.Text = "State : " + j + " / " + pcc.Exports.Count;
                            string s = "Possible properties found so far for class \"" + classname + "\":\n";
                            for (int k = 0; k < Names.Count(); k++)
                                s += Types[k] + " : \"" + Names[k] + "\" first found: " + First[k] +"\n";
                            Action action = () => rtb1.Text = s;
                            rtb1.Invoke(action);
                            action = () => rtb1.SelectionStart = s.Length;
                            rtb1.Invoke(action);
                            action = () => rtb1.ScrollToCaret();
                            rtb1.Invoke(action);
                            Application.DoEvents();
                        }
                    }
                   
                }
            Status.Text = "State : Done";
            string t = "Possible properties found for class \"" + classname + "\":\n";
            for (int k = 0; k < Names.Count(); k++)
                t += Types[k] + " : \"" + Names[k] + "\" first found: " + First[k] + "\n";
            rtb1.Text = t;
            rtb1.SelectionStart = rtb1.TextLength;
            rtb1.ScrollToCaret();
            pb1.Value = 0;
            pauseToolStripMenuItem.Visible = false;
        }
    }
}
