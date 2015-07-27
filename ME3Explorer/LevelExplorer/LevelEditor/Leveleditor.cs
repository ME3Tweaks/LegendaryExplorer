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
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using KFreonLib.Debugging;

namespace ME3Explorer.LevelExplorer.LevelEditor
{
    public partial class Leveleditor : Form
    {
        public SceneManager SceneMan = new SceneManager();
        public bool MoveWASD = true;
        public bool FocusOnClick = true;
        public PCCObject pcc;

        public Leveleditor()
        {
            InitializeComponent();
        }

        private void openPccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                LoadPCC(d.FileName);
        }

        public void LoadPCC(string path)
        {
            timer1.Enabled = false;
            DebugOutput.StartDebugger("LevelEditor");
            SceneMan.AddLevel(path);
            timer1.Enabled = true;
        }

        private void Leveleditor_Activated(object sender, EventArgs e)
        {
            toolStripComboBox1.Items.Clear();
            toolStripComboBox1.Items.Add("100");
            toolStripComboBox1.Items.Add("200");
            toolStripComboBox1.Items.Add("500");
            toolStripComboBox1.Items.Add("1000");
            toolStripComboBox1.Items.Add("2000");
            toolStripComboBox1.Items.Add("5000");
            toolStripComboBox1.Items.Add("10000");
            toolStripComboBox1.SelectedIndex = 6;
            SceneMan = new SceneManager();
            SceneMan.Init(SceneMan.CreateView(p1), tv1);
            timer1.Enabled = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            SceneMan.Render();
            SceneMan.Update();
        }

        private void tv1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            string path = SceneMan.GetNodeIndexPath(e.Node);
            status.Text = path;
            SceneMan.ProcessTreeClick(path, FocusOnClick);
        }

        private void p1_MouseMove(object sender, MouseEventArgs e)
        {
            if (MoveWASD)
            {
                float w = e.X / (float)p1.Width;
                DirectXGlobal.Cam.dir = Microsoft.DirectX.Vector3.Normalize(
                    new Microsoft.DirectX.Vector3(
                        (float)(Math.Cos(w * 3.1415f * 2f)),
                        (float)(Math.Sin(w * 3.1415f * 2f)),
                        (e.Y / (float)p1.Height) * -2 + 1
                        )
                    );
            }
        }

        private void Leveleditor_KeyDown(object sender, KeyEventArgs e)
        {
            SceneMan.ProcessKey(e.KeyValue, true);
            status.Text = DirectXGlobal.Cam.pos.ToString();
        }

        private void Leveleditor_KeyUp(object sender, KeyEventArgs e)
        {
            SceneMan.ProcessKey(e.KeyValue, false);
        }

        private void p1_Resize(object sender, EventArgs e)
        {
            if (SceneMan != null && SceneMan.device != null)
            {
                PresentParameters p = SceneMan.device.PresentationParameters;
                if (p1.Width > 0 && p1.Height > 0)
                {
                    p.BackBufferWidth = p1.Width;
                    p.BackBufferHeight = p1.Height;
                    SceneMan.device.Reset(p);
                    SceneMan.ResetDX();
                }
            }
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            toolStripButton1.Checked = true;
            toolStripButton2.Checked = false;
            DirectXGlobal.DrawWireFrame = true;
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            toolStripButton1.Checked = false;
            toolStripButton2.Checked = true;
            DirectXGlobal.DrawWireFrame = false;
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            MoveWASD = toolStripButton3.Checked;
            toolStripButton3.Checked = MoveWASD;
            SceneMan.MoveWASD = MoveWASD;
        }

        private void moveWASDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MoveWASD = moveWASDToolStripMenuItem.Checked;
            toolStripButton3.Checked = MoveWASD;
            SceneMan.MoveWASD = MoveWASD;
        }

        private void toolStripComboBox1_Click(object sender, EventArgs e)
        {
            float[] speeds = { 100f, 200f, 500f, 1000f, 2000f, 5000f, 10000f };
            if (toolStripComboBox1.SelectedIndex != -1)
                SceneMan.MoveSpeed = speeds[toolStripComboBox1.SelectedIndex];
        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            FocusOnClick = toolStripButton4.Checked;
        }

        private void p1_MouseClick(object sender, MouseEventArgs e)
        {
            Device device = SceneMan.device;
            if (device == null || timer1.Enabled == false)
                return;
            Vector3 CamEye = DirectXGlobal.Cam.pos;
            Vector3 CamDir = DirectXGlobal.Cam.dir;
            Vector3 near = Vector3.Unproject(new Vector3(e.X, e.Y, 0), device.Viewport, device.Transform.Projection, device.Transform.View, Matrix.Identity);
            Vector3 far = Vector3.Unproject(new Vector3(e.X, e.Y, 1), device.Viewport, device.Transform.Projection, device.Transform.View, Matrix.Identity);
            Vector3 dir = far - near;
            dir.Normalize();
            SceneMan.DeSelectAll();
            SceneMan.Process3DClick(CamEye, dir);
        }

        private void unloadAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            SceneMan = new SceneManager();
            SceneMan.Init(SceneMan.CreateView(p1), tv1);
            timer1.Enabled = true;
        }

        private void transformToolStripMenuItem_Click(object sender, EventArgs e)
        {
            panel_transform.Visible = transformToolStripMenuItem.Checked;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string s = textBox1.Text;
            float dx = GetFloat(ref s);
            textBox1.Text = s;
            Matrix m = Matrix.Translation(dx, 0, 0);
            SceneMan.ApplyTransform(m);
        }

        public float GetFloat(ref string s)
        {
            float f = 0;
            try
            {
                f = Convert.ToSingle(s);
            }
            catch (Exception e)
            {
                s = "0";
            }
            return f;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string s = textBox1.Text;
            float dx = GetFloat(ref s);
            textBox1.Text = s;
            Matrix m = Matrix.Translation(-dx, 0, 0);
            SceneMan.ApplyTransform(m);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string s = textBox2.Text;
            float dy = GetFloat(ref s);
            textBox2.Text = s;
            Matrix m = Matrix.Translation(0, dy, 0);
            SceneMan.ApplyTransform(m);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            string s = textBox2.Text;
            float dy = GetFloat(ref s);
            textBox2.Text = s;
            Matrix m = Matrix.Translation(0, -dy, 0);
            SceneMan.ApplyTransform(m);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            string s = textBox3.Text;
            float dz = GetFloat(ref s);
            textBox3.Text = s;
            Matrix m = Matrix.Translation(0, 0, dz);
            SceneMan.ApplyTransform(m);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            string s = textBox3.Text;
            float dz = GetFloat(ref s);
            textBox3.Text = s;
            Matrix m = Matrix.Translation(0, 0, -dz);
            SceneMan.ApplyTransform(m);
        }

        private void saveAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure?", "Saving Changes", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
            {
                SceneMan.SaveAllChanges();
                MessageBox.Show("Done.");
            }
        }

        private void createModJobsFromCurrentChangesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SceneMan.CreateModJobs();
        }

        private void button12_Click(object sender, EventArgs e)
        {
            string s = textBox4.Text;
            float rx = GetFloat(ref s);
            textBox4.Text = s;
            SceneMan.ApplyRotation(new Vector3(rx, 0, 0));
        }

        private void button11_Click(object sender, EventArgs e)
        {
            string s = textBox4.Text;
            float rx = GetFloat(ref s);
            textBox4.Text = s;
            SceneMan.ApplyRotation(new Vector3(-rx, 0, 0));
        }

        private void button10_Click(object sender, EventArgs e)
        {
            string s = textBox5.Text;
            float ry = GetFloat(ref s);
            textBox5.Text = s;
            SceneMan.ApplyRotation(new Vector3(0, ry, 0));
        }

        private void button9_Click(object sender, EventArgs e)
        {
            string s = textBox5.Text;
            float ry = GetFloat(ref s);
            textBox5.Text = s;
            SceneMan.ApplyRotation(new Vector3(0, -ry, 0));
        }

        private void button8_Click(object sender, EventArgs e)
        {
            string s = textBox6.Text;
            float rz = GetFloat(ref s);
            textBox6.Text = s;
            SceneMan.ApplyRotation(new Vector3(0, 0, rz));
        }

        private void button7_Click(object sender, EventArgs e)
        {
            string s = textBox6.Text;
            float rz = GetFloat(ref s);
            textBox6.Text = s;
            SceneMan.ApplyRotation(new Vector3(0, 0, -rz));
        }

        private void exportSceneTo3DSToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.3ds|*.3ds";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                timer1.Enabled = false;
                SceneMan.ExportScene3DS(d.FileName);
                timer1.Enabled = true;
                MessageBox.Show("Done.");
            }
        }
    }
}
