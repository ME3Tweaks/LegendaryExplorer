using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ME3Explorer.Packages;

namespace ME3Explorer
{
    public class WinFormsBase : Form
    {
        public IMEPackage Pcc { get; private set; }

        //class really ought to be abstract, but it can't be for the designer to work
        protected WinFormsBase()
        {
            this.FormClosing += WinFormsBase_FormClosing;
        }

        private void WinFormsBase_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Pcc != null && Pcc.IsModified && Pcc.Tools.Count == 1 && e.CloseReason == CloseReason.UserClosing &&
                DialogResult.No == MessageBox.Show($"{Path.GetFileName(Pcc.FileName)} has unsaved changes. Do you really want to close {Name}?", "Unsaved changes", MessageBoxButtons.YesNo))
            {
                e.Cancel = true;
            }
        }

        public void LoadMEPackage(string s)
        {
            Pcc?.Release(winForm: this);
            Pcc = MEPackageHandler.OpenMEPackage(s, winForm: this);
        }

        public void LoadME1Package(string s)
        {
            Pcc?.Release(winForm: this);
            Pcc = MEPackageHandler.OpenME1Package(s, winForm: this);
        }

        public void LoadME2Package(string s)
        {
            Pcc?.Release(winForm: this);
            Pcc = MEPackageHandler.OpenME2Package(s, winForm: this);
        }

        public void LoadME3Package(string s)
        {
            Pcc?.Release(winForm: this);
            Pcc = MEPackageHandler.OpenME3Package(s, winForm: this);
        }

        public virtual void handleUpdate(List<PackageUpdate> updates) { }
    }
}
