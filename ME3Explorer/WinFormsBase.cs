using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ME3Explorer.Packages;

namespace ME3Explorer
{
    public abstract class WinFormsBase : Form
    {
        protected IMEPackage pcc;

        public void LoadMEPackage(string s)
        {
            pcc?.Release(winForm: this);
            pcc = MEPackageHandler.OpenMEPackage(s, winForm: this);
        }

        public void LoadME1Package(string s)
        {
            pcc?.Release(winForm: this);
            pcc = MEPackageHandler.OpenME1Package(s, winForm: this);
        }

        public void LoadME2Package(string s)
        {
            pcc?.Release(winForm: this);
            pcc = MEPackageHandler.OpenME2Package(s, winForm: this);
        }

        public void LoadME3Package(string s)
        {
            pcc?.Release(winForm: this);
            pcc = MEPackageHandler.OpenME3Package(s, winForm: this);
        }
    }
}
