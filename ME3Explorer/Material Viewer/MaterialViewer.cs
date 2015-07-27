using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;

namespace ME3Explorer.Material_Viewer
{
    public partial class MaterialViewer : Form
    {
        public PCCObject pcc;
        public string CurrentFile;
        public List<int> CurrentObjects;

        public struct SceneType
        {
            public MaterialObject mat;
        }

        public struct MaterialObject
        {

        }

        public MaterialViewer()
        {
            InitializeComponent();
        }

        private void MaterialViewer_Load(object sender, EventArgs e)
        {
            MaterialPreview.InitializeGraphics(pb1);
            MaterialPreview.GenerateTriangles();
            MaterialPreview.CreateEffect();
            timer1.Enabled = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (MaterialPreview.init)
                MaterialPreview.Render();
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "PCC Files(*.pcc)|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                status.Text = "Loaded : " + d.FileName;
                pcc = new PCCObject(d.FileName);
                CurrentFile = d.FileName;
                LoadMaterials();
            }
        }

        public void LoadMaterials()
        {
            CurrentObjects = new List<int>();
            listBox1.Items.Clear();
            for (int i = 0; i < pcc.Exports.Count; i++)
                if (pcc.Exports[i].ClassName == "Material")
                {
                    listBox1.Items.Add("#" + i + " : " + pcc.Exports[i].ObjectName);
                    CurrentObjects.Add(i);
                }
        }

        private void shaderCodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Shader_Code sc = new Shader_Code();
            sc.rtb1.Text = MaterialPreview.script;
            sc.ShowDialog();
        }
    }
}
