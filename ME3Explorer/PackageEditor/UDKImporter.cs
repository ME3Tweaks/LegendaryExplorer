using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using UDKExplorer.UDK;

namespace ME3Explorer
{
    public partial class UDKImporter : Form
    {
        List<int> Objects;
        UDKObject importudk;
        public static string[] ImportableClassTypes = { "StaticMesh", "RB_BodySetup" };
        public UDKImporter()
        {
            InitializeComponent();
        }

        private void loadUPKButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.upk|*.upk";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                upkImportableExportsListbox.Items.Clear();
                Objects = new List<int>();
                IMEPackage importudk = MEPackageHandler.OpenMEPackage(d.FileName);
                for (int i = 0; i < importudk.Exports.Count; i++)
                {
                    if (ImportableClassTypes.Contains(importudk.Exports[i].ClassName))
                    {
                        Objects.Add(i);
                        upkImportableExportsListbox.Items.Add("#" + i.ToString("d6") + " : " + importudk.Exports[i].ObjectName);
                    }
                }
                currentUPKFileLabel.Text = d.FileName;
            }
        }
    }
}
