using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ME3Explorer.Packages;

namespace ME3Explorer
{
    public abstract class WPFBase : NotifyPropertyChangedWindowBase
    {
        private IMEPackage pcc;
        /// <summary>
        /// Currently loaded Package file, if any.
        /// </summary>
        public IMEPackage Pcc
        {
            get => pcc;
            private set => SetProperty(ref pcc, value);
        }

        protected WPFBase()
        {
            this.Closing += WPFBase_Closing;
        }

        private void WPFBase_Closing(object sender, CancelEventArgs e)
        {
            if (pcc != null && pcc.IsModified && pcc.Tools.Count == 1 &&
                MessageBoxResult.No == MessageBox.Show($"{Path.GetFileName(pcc.FileName)} has unsaved changes. Do you really want to close {Title}?", "Unsaved changes", MessageBoxButton.YesNo))
            {
                e.Cancel = true;
            }
            else
            {
                DataContext = null; //Remove all binding sources
            }
        }

        public void LoadMEPackage(string s)
        {
            UnLoadMEPackage();
            Pcc = MEPackageHandler.OpenMEPackage(s, wpfWindow: this);
        }

        public void LoadME3Package(string s)
        {
            UnLoadMEPackage();
            Pcc = MEPackageHandler.OpenME3Package(s, wpfWindow: this);
        }

        protected void UnLoadMEPackage()
        {
            pcc?.Release(wpfWindow: this);
            Pcc = null;
        }

        public abstract void handleUpdate(List<PackageUpdate> updates);
    }
}
