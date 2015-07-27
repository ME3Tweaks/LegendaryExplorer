using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.PlotVarDB
{
    public partial class PlotVarEditor : Form
    {
        public PlotVarEditor()
        {
            InitializeComponent();
        }

        public PlotVarDB parent;
        public int version;
        public int index;

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            string text = rtb1.Text;
            string[] lines = text.Split(';');
            PlotVarDB.PlotVarEntry t = new PlotVarDB.PlotVarEntry();
            List<PlotVarDB.PlotVarEntry>res = new List<PlotVarDB.PlotVarEntry>();
            foreach (string line in lines)
                if (line.Trim().Length != 0)
                    if (ReadEntry(line, out t))
                        res.Add(t);
                    else
                    {
                        MessageBox.Show("Error in line : \n" + line);
                        return;
                    }
            if (version == 0)
                parent.database.ME1.AddRange(res);
            if (version == 1)
                parent.database.ME2.AddRange(res);
            if (version == 2)
                parent.database.ME3.AddRange(res);
            parent.RefreshLists();
            this.Close();
        }

        public bool ReadEntry(string s, out PlotVarDB.PlotVarEntry res)
        {
            res = new PlotVarDB.PlotVarEntry();
            string t = s.Trim();
            if (!t.StartsWith("{") || !t.EndsWith("}"))
                return false;
            string[] t2 = t.Substring(1, t.Length - 2).Split(',');
            if (t2.Length != 3)
                return false;
            t2[0] = t2[0].Trim();
            t2[1] = t2[1].Trim();
            t2[2] = t2[2].Trim();
            int x;
            if (!int.TryParse(t2[0], out x))
                return false;
            res.ID = x;
            if (!t2[1].StartsWith("\"") || !t2[1].EndsWith("\""))
                return false;
            if (!t2[2].StartsWith("\"") || !t2[2].EndsWith("\""))
                return false;
            res.Desc = t2[2].Substring(1, t2[2].Length - 2);
            string t3 = t2[1].Substring(1, t2[1].Length - 2).ToLower();
            switch (t3)
            {
                case "bool":
                    res.type = 0;
                    break;
                case "int":
                    res.type = 1;
                    break;
                case "float":
                    res.type = 2;
                    break;
                default: return false;
            }
            return true;
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            string text = rtb1.Text;
            string[] lines = text.Split(';');
            PlotVarDB.PlotVarEntry t = new PlotVarDB.PlotVarEntry();
            List<PlotVarDB.PlotVarEntry> res = new List<PlotVarDB.PlotVarEntry>();
            foreach (string line in lines)
                if (line.Trim().Length != 0)
                    if (ReadEntry(line, out t))
                        res.Add(t);
                    else
                    {
                        MessageBox.Show("Error in line : \n" + line);
                        return;
                    }
            if (res.Count != 0)
            {
                if (version == 0)
                    parent.database.ME1[index] = res[0];
                if (version == 1)
                    parent.database.ME2[index] = res[0];
                if (version == 2)
                    parent.database.ME3[index] = res[0];
                parent.RefreshLists();
            }
            this.Close();
        }
    }
}
