using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ME3Explorer.Packages;

namespace ME3Explorer
{
    public abstract class WPFBase : Window
    {
        protected IMEPackage pcc;

        protected WPFBase()
        {
            this.Closing += WPFBase_Closing;
        }

        private void WPFBase_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (pcc != null && pcc.IsModified && pcc.Tools.Count == 1 &&
                MessageBoxResult.No == MessageBox.Show($"{Path.GetFileName(pcc.FileName)} has unsaved changes. Do you really want to close {Title}?", "", MessageBoxButton.YesNo))
            {
                e.Cancel = true;
            }
        }

        public void LoadMEPackage(string s)
        {
            pcc?.Release(wpfWindow: this);
            pcc = MEPackageHandler.OpenMEPackage(s, wpfWindow: this);
        }

        public void LoadME3Package(string s)
        {
            pcc?.Release(wpfWindow: this);
            pcc = MEPackageHandler.OpenME3Package(s, wpfWindow: this);
        }

        public abstract void handleUpdate(List<PackageUpdate> updates);
    }
}
