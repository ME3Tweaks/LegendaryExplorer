using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using ME3ExplorerCore.Packages;

namespace LegendaryExplorer.SharedUI.Bases
{
    public class WinFormsBase : Form, IPackageUser
    {
        public IMEPackage Pcc { get; private set; }

        //class really ought to be abstract, but it can't be for the designer to work
        protected WinFormsBase()
        {
            this.FormClosing += WinFormsBase_FormClosing;
        }

        private void WinFormsBase_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Use WPF messagebox. We want as little of Winforms as possible
            if (Pcc != null && Pcc.IsModified && Pcc.Users.Count == 1 && e.CloseReason == CloseReason.UserClosing &&
                MessageBoxResult.No == System.Windows.MessageBox.Show($"{Path.GetFileName(Pcc.FilePath)} has unsaved changes. Do you really want to close {Name}?", "Unsaved changes", MessageBoxButton.YesNo))
            {
                e.Cancel = true;
            }
        }

        public void LoadMEPackage(string s)
        {
            Pcc?.Release(this);
            Pcc = MEPackageHandler.OpenMEPackage(s, this);
        }

        public virtual void handleUpdate(List<PackageUpdate> updates) { }

        FormClosedEventHandler winformClosed;
        public void RegisterClosed(Action handler)
        {
            winformClosed = (obj, args) =>
            {
                handler();
            };
            FormClosed += winformClosed;
        }

        public void ReleaseUse()
        {
            FormClosed -= winformClosed;
            winformClosed = null;
        }
    }
}
