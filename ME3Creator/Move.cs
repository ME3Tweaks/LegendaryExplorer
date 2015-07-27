using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3LibWV;
using ME3LibWV.UnrealClasses;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3Creator
{
    public partial class MoveStuff : Form
    {
        public bool Aborted = false;
        public bool PressedOK = false;
        public PCCPackage pcc;
        public StaticMeshActor stma;
        public string myType = "stma";

        public MoveStuff()
        {
            InitializeComponent();
        }

        private void button8_Click(object sender, EventArgs e)
        {
            Aborted = true;
        }

        private void Move_FormClosing(object sender, FormClosingEventArgs e)
        {
            Aborted = true;
        }

        private void button7_Click(object sender, EventArgs e)
        {
            PressedOK = true;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            float dist = Convert.ToSingle(textBox1.Text);
            switch (myType)
            {
                case "stma":
                    stma.STMC.STM.TempMatrix = Matrix.Translation(new Vector3(0, 0, dist)) * stma.STMC.STM.TempMatrix;
                    textBox4.Text = (Convert.ToSingle(textBox4.Text) + dist).ToString();
                    break;
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            float dist = Convert.ToSingle(textBox1.Text);
            switch (myType)
            {
                case "stma":
                    stma.STMC.STM.TempMatrix = Matrix.Translation(new Vector3(0, 0, -dist)) * stma.STMC.STM.TempMatrix;
                    textBox4.Text = (Convert.ToSingle(textBox4.Text) - dist).ToString();
                    break;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            float dist = Convert.ToSingle(textBox1.Text);
            switch (myType)
            {
                case "stma":
                    stma.STMC.STM.TempMatrix = Matrix.Translation(new Vector3(0, dist, 0)) * stma.STMC.STM.TempMatrix;
                    textBox3.Text = (Convert.ToSingle(textBox3.Text) + dist).ToString();
                    break;
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            float dist = Convert.ToSingle(textBox1.Text);
            switch (myType)
            {
                case "stma":
                    stma.STMC.STM.TempMatrix = Matrix.Translation(new Vector3(0, -dist, 0)) * stma.STMC.STM.TempMatrix;
                    textBox3.Text = (Convert.ToSingle(textBox3.Text) - dist).ToString();
                    break;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            float dist = Convert.ToSingle(textBox1.Text);
            switch (myType)
            {
                case "stma":
                    stma.STMC.STM.TempMatrix = Matrix.Translation(new Vector3(dist, 0, 0)) * stma.STMC.STM.TempMatrix;
                    textBox2.Text = (Convert.ToSingle(textBox2.Text) + dist).ToString();
                    break;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            float dist = Convert.ToSingle(textBox1.Text);
            switch (myType)
            {
                case "stma":
                    stma.STMC.STM.TempMatrix = Matrix.Translation(new Vector3(-dist, 0, 0)) * stma.STMC.STM.TempMatrix;
                    textBox2.Text = (Convert.ToSingle(textBox2.Text) - dist).ToString();
                    break;
            }
        }
    }
}
