using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Creator
{
    public partial class Hexconverter : Form
    {
        public Hexconverter()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string input = textBox1.Text.ToLower().Replace(" ", "");
            if (!isHexString(input) || input.Length != 8)
            {
                MessageBox.Show("Invalid input!");
                return;
            }
            byte[] buff = new byte[4];
            byte[] buff2 = new byte[4];
            string s = "";
            for (int i = 0; i < 4; i++)
            {
                buff[i] = Convert.ToByte(input.Substring(i * 2, 2), 16);
                s = buff[i].ToString("x2") + s;
                buff2[3 - i] = buff[i];
            }
            textBox2.Text = s;
            textBox3.Text = BitConverter.ToInt32(buff, 0).ToString();
            textBox4.Text = BitConverter.ToUInt32(buff, 0).ToString();
            textBox5.Text = BitConverter.ToSingle(buff, 0).ToString();
        }

        public string hexchars = "0123456789abcdef";

        public bool isHexString(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                int f = -1;
                for (int j = 0; j < hexchars.Length; j++)
                    if (s[i] == hexchars[j])
                    {
                        f = j;
                        break;
                    }
                if (f == -1)
                    return false;
            }
            return true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string input = textBox2.Text.ToLower().Replace(" ", "");
            if (!isHexString(input) || input.Length != 8)
            {
                MessageBox.Show("Invalid input!");
                return;
            }
            byte[] buff = new byte[4];
            byte[] buff2 = new byte[4];
            string s = "";
            for (int i = 0; i < 4; i++)
            {
                buff[i] = Convert.ToByte(input.Substring(i * 2, 2), 16);
                s = buff[i].ToString("x2") + s;
                buff2[3 - i] = buff[i];
            }
            textBox1.Text = s;
            textBox3.Text = BitConverter.ToInt32(buff2, 0).ToString();
            textBox4.Text = BitConverter.ToUInt32(buff2, 0).ToString();
            textBox5.Text = BitConverter.ToSingle(buff2, 0).ToString();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            int n = 0;
            if (!Int32.TryParse(textBox3.Text, out n))
            {
                MessageBox.Show("Invalid input!");
                return;
            }
            byte[] buff = BitConverter.GetBytes(n);
            string s = "", s2 = "";
            for (int i = 0; i < 4; i++)
            {
                s += buff[i].ToString("x2");
                s2 = buff[i].ToString("x2") + s2;
            }
            textBox1.Text = s;
            textBox2.Text = s2;
            textBox4.Text = BitConverter.ToUInt32(buff, 0).ToString();
            textBox5.Text = BitConverter.ToSingle(buff, 0).ToString();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            uint n = 0;
            if (!UInt32.TryParse(textBox4.Text, out n))
            {
                MessageBox.Show("Invalid input!");
                return;
            }
            byte[] buff = BitConverter.GetBytes(n);
            string s = "", s2 = "";
            for (int i = 0; i < 4; i++)
            {
                s += buff[i].ToString("x2");
                s2 = buff[i].ToString("x2") + s2;
            }
            textBox1.Text = s;
            textBox2.Text = s2;
            textBox3.Text = BitConverter.ToInt32(buff, 0).ToString();
            textBox5.Text = BitConverter.ToSingle(buff, 0).ToString();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            float n = 0;
            string seperator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string wrongsep;
            if (seperator == ".")
                wrongsep = ",";
            else
                wrongsep = ".";
            textBox5.Text = textBox5.Text.Replace(wrongsep, seperator);
            if (!float.TryParse(textBox5.Text, out n))
            {
                MessageBox.Show("Invalid input!");
                return;
            }
            byte[] buff = BitConverter.GetBytes(n);
            string s = "", s2 = "";
            for (int i = 0; i < 4; i++)
            {
                s += buff[i].ToString("x2");
                s2 = buff[i].ToString("x2") + s2;
            }
            textBox1.Text = s;
            textBox2.Text = s2;
            textBox3.Text = BitConverter.ToInt32(buff, 0).ToString();
            textBox4.Text = BitConverter.ToUInt32(buff, 0).ToString();
        }
    }
}
