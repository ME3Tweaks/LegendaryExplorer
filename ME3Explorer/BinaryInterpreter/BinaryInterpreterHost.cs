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
using ME3Explorer.Unreal;
using ME3Explorer.Packages;

namespace ME3Explorer
{
    public partial class BinaryInterpreterHost : WinFormsBase
    {
        public BinaryInterpreterHost(string fileName, int index)
        {
            InitializeComponent();
            LoadMEPackage(fileName);
            interpreter1.Pcc = pcc;
            interpreter1.export = pcc.getExport(index);
            interpreter1.InitInterpreter();
            toolStripStatusLabel1.Text = "Class: " + interpreter1.export.ClassName + ", Export Index: " + index;
            toolStripStatusLabel2.Text = "@" + Path.GetFileName(pcc.FileName);
            interpreter1.hb1.ReadOnly = true;
            interpreter1.saveHexButton.Visible = false;
            interpreter1.exportButton.Visible = true;
        }

        public new void Show()
        {
            base.Show();
            interpreter1.treeView1.Nodes[0].Expand();
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            if (updates.Contains(new PackageUpdate { change = PackageChange.ExportData, index = interpreter1.export.Index }))
            {
                interpreter1.memory = interpreter1.export.Data;
                interpreter1.RefreshMem();
            }
            if (updates.Contains(new PackageUpdate { change = PackageChange.ExportHeader, index = interpreter1.export.Index }))
            {
                toolStripStatusLabel1.Text = "Class: " + interpreter1.export.ClassName + ", Export Index: " + interpreter1.export.Index;
                interpreter1.RefreshMem();
            }
        }
    }
}
