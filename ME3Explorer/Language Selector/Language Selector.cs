using System;
using Microsoft.Win32;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer
{
    public partial class Language_Selector : Form
    {
        public Language_Selector()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            int sel = -1;
            if (radioButton1.Checked) sel = 0;
            if (radioButton2.Checked) sel = 1;
            if (radioButton3.Checked) sel = 2;
            if (radioButton4.Checked) sel = 3;
            if (radioButton5.Checked) sel = 4;
            if (radioButton6.Checked) sel = 5;
            string loc = "", gdf = "";
            if (sel == -1) return;
            RegistryKey rk = Registry.LocalMachine;
            RegistryKey srk = rk.OpenSubKey("SOFTWARE\\BioWare\\Mass Effect 3", true);
            Object path = srk.GetValue("Path");
            if (path == null) return;
            gdf = path.ToString();
            switch (sel)
            {
                case 0:
                    gdf += "\\GDFBinary_en_US.dll";
                    loc = "en_US";
                    break;
                case 1:
                    gdf += "\\GDFBinary_de_DE.dll";
                    loc = "de_DE";
                    break;
                case 2:
                    gdf += "\\GDFBinary_ru_RU.dll";
                    loc = "ru_RU";
                    break;
                case 3:
                    gdf += "\\GDFBinary_fr_FR.dll";
                    loc = "fr_FR";
                    break;
                case 4:
                    gdf += "\\GDFBinary_es_ES.dll";
                    loc = "es_ES";
                    break;
                case 5:
                    gdf += "\\GDFBinary_pl_PL.dll";
                    loc = "pl_PL";
                    break;
            }
            Object val = srk.GetValue("Locale");
            Object val2 = srk.GetValue("GDFBinary");
            if (val != null)
            {
                srk.SetValue("Locale", loc);
                srk.SetValue("GDFBinary", gdf);
            }
            else
            {
                srk.SetValue("Locale", loc);
                srk.SetValue("GDFBinary", gdf);
            }
            RegistryKey srk2 = rk.OpenSubKey("SOFTWARE\\Origin Games\\71402");
            if (srk2 != null)
            {
                Object val3 = srk2.GetValue("Locale");
                if (val3 != null)
                    srk2.SetValue("Locale", loc);
            }
            MessageBox.Show("Done.");
        }

        private void Language_Selector_Load(object sender, EventArgs e)
        {
            radioButton1.Select();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            int sel = -1;
            if (radioButton1.Checked) sel = 0;
            if (radioButton2.Checked) sel = 1;
            if (radioButton3.Checked) sel = 2;
            if (radioButton4.Checked) sel = 3;
            if (radioButton5.Checked) sel = 4;
            if (radioButton6.Checked) sel = 5;
            string loc = "", gdf = "";
            if (sel == -1) return;
            RegistryKey rk = Registry.LocalMachine;
            RegistryKey srk = rk.OpenSubKey("SOFTWARE\\Wow6432Node\\BioWare\\Mass Effect 3", true);
            if (srk == null)
                srk = rk.OpenSubKey("SOFTWARE\\Wow6432Node\\BioWare\\Mass Effect 3", true);
            Object path = srk.GetValue("Path");
            if (path == null) return;
            gdf = path.ToString();
            switch (sel)
            {
                case 0:
                    gdf += "\\GDFBinary_en_US.dll";
                    loc = "en_US";
                    break;
                case 1:
                    gdf += "\\GDFBinary_de_DE.dll";
                    loc = "de_DE";
                    break;
                case 2:
                    gdf += "\\GDFBinary_ru_RU.dll";
                    loc = "ru_RU";
                    break;
                case 3:
                    gdf += "\\GDFBinary_fr_FR.dll";
                    loc = "fr_FR";
                    break;
                case 4:
                    gdf += "\\GDFBinary_es_ES.dll";
                    loc = "es_ES";
                    break;
                case 5:
                    gdf += "\\GDFBinary_pl_PL.dll";
                    loc = "pl_PL";
                    break;
            }
            Object val = srk.GetValue("Locale");
            Object val2 = srk.GetValue("GDFBinary");
            if (val != null)
            {
                srk.SetValue("Locale", loc);
                srk.SetValue("GDFBinary", gdf);
            }
            else
            {
                srk.SetValue("Locale", loc);
                srk.SetValue("GDFBinary", gdf);
            }
            RegistryKey srk2 = rk.OpenSubKey("SOFTWARE\\Wow6432Node\\Origin Games\\71402");
            if (srk2 != null)
            {
                Object val3 = srk2.GetValue("Locale");
                if (val3 != null)
                    srk2.SetValue("Locale", loc);
            }
            MessageBox.Show("Done.");

        }
    }
}
