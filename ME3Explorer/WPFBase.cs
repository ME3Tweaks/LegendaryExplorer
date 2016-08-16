using System;
using System.Collections.Generic;
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
    }
}
