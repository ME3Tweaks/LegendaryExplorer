using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using ME3Explorer.SequenceObjects;
using ME3Explorer.Packages;
using System.Linq;

namespace ME3Explorer.InterpViewer
{
    public partial class InterpEditor : WinFormsBase
    {
        public List<int> objects;

        public InterpEditor()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("InterpViewer", new WeakReference(this));

            InitializeComponent();
            timeline.Scrollbar = vScrollBar1;
            timeline.GroupList.ScrollbarH = hScrollBar1;
            timeline.GroupList.tree1 = treeView1;
            timeline.GroupList.tree2 = treeView2;
            
            objects = new List<int>();
        }

        private void openPCCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "PCC Files(*.pcc)|*.pcc";
            if (d.ShowDialog() == DialogResult.OK)
            {
                LoadPCC(d.FileName);
            }
        }

        public void LoadPCC(string fileName)
        {
            try
            {
                LoadME3Package(fileName);
                RefreshCombo();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message);
            }
        }

        public void RefreshCombo()
        {
            objects.Clear();
            for (int i = 0; i < Pcc.Exports.Count; i++)
                if (Pcc.Exports[i].ClassName == "InterpData")
                    objects.Add(i);
            toolStripComboBox1.Items.Clear();
            foreach (int i in objects)
                toolStripComboBox1.Items.Add("#" + i + " : " + Pcc.Exports[i].ObjectName);
            if (toolStripComboBox1.Items.Count != 0)
                toolStripComboBox1.SelectedIndex = 0;
        }

        public void loadInterpData(int index)
        {
            timeline.GroupList.LoadInterpData(index, Pcc as ME3Package);
            timeline.GroupList.OnCameraChanged(timeline.Camera);
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            int n = toolStripComboBox1.SelectedIndex;
            if (n == -1)
                return;
            loadInterpData(objects[n]);
        }

        private void treeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
        }

        private void loadAlternateTlkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new TlkManagerNS.TLKManagerWPF().Show();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Pcc == null)
                return;
            Pcc.save();
            MessageBox.Show("Done");
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.change != PackageChange.Import &&
                                                                            x.change != PackageChange.ImportAdd &&
                                                                            x.change != PackageChange.Names);
            List<int> updatedExports = relevantUpdates.Select(x => x.index).ToList();
            if (updatedExports.Contains(timeline.GroupList.index))
            {
                //loaded InterpData is no longer an InterpData
                if (Pcc.getExport(timeline.GroupList.index).ClassName != "InterpData")
                {
                    //?
                }
                else
                {
                    timeline.GroupList.LoadInterpData(timeline.GroupList.index, Pcc as ME3Package);
                }
                updatedExports.Remove(timeline.GroupList.index);
            }
            if (updatedExports.Intersect(objects).Count() > 0)
            {
                RefreshCombo();
            }
            else
            {
                foreach (var i in updatedExports)
                {
                    if (Pcc.getExport(i).ClassName == "InterpData")
                    {
                        RefreshCombo();
                        break;
                    }
                }
            }
        }
    }
}