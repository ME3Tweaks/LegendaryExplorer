using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace ME3Explorer
{
    public partial class PackageEditor
    {
        private void findExportsWithSerialSizeMismatchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<string> serialexportsbad = new List<string>();
            foreach (IExportEntry entry in pcc.Exports)
            {
                Console.WriteLine(entry.Index + " " + entry.Data.Length + " " + entry.DataSize);
                if (entry.Data.Length != entry.DataSize)
                {
                    serialexportsbad.Add(entry.GetFullPath + " Header lists: " + entry.DataSize + ", Actual data size: " + entry.Data.Length);
                }
            }

            if (serialexportsbad.Count > 0)
            {
                ListDialog lw = new ListDialog(serialexportsbad, "Serial Size Mismatches", "The following exports had serial size mismatches.");
                lw.Show();
            }
            else
            {
                System.Windows.MessageBox.Show("No exports have serial size mismatches.");
            }
        }

        private void dEBUGEnumerateAllClassesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc != null)
            {
                foreach (IExportEntry exp in pcc.Exports)
                {
                    if (exp.ClassName == "Class")
                    {
                        Debug.WriteLine("Testing " + exp.Index + " " + exp.GetFullPath);
                        binaryInterpreterControl.export = exp;
                        binaryInterpreterControl.InitInterpreter();
                    }
                }
            }
        }



        private void dEBUGCallReadPropertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (!GetSelected(out n))
            {
                return;
            }
            if (n >= 0)
            {
                try
                {
                    IExportEntry exp = pcc.Exports[n];
                    exp.GetProperties(true); //force properties to reload
                }
                catch (Exception ex)
                {

                }
            }
        }

        private void dEBUGOpenPackageEditorWPFToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new PackageEditorWPF().Show();
        }

        /// <summary>
        /// Do not remove this method - it is used by ME3Tweaks Rebuilding TFC Guide
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dEBUGEnsureFolderOfPackageFilesHasANameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog { Filter = "ME2/ME3 Package files|*.pcc" };
            if (d.ShowDialog() == DialogResult.OK)
            {
                var pccs = Directory.GetFiles(Path.GetDirectoryName(d.FileName), "*pcc");
                if (pccs.Count() > 0)
                {
                    string input = "Enter a name. This name will be added to every PCC in this folder if it does not already exist.";
                    string name = PromptDialog.Prompt(null,input, "Enter name");
                    if (name != null)
                    {
                        foreach (string pccPath in pccs)
                        {
                            LoadMEPackage(pccPath);
                            int numNames = pcc.Names.Count;
                            pcc.FindNameOrAdd(name);
                            int afternumNames = pcc.Names.Count;
                            if (numNames != afternumNames)
                            {
                                pcc.save();
                                Debug.WriteLine("Added " + name + " to " + pccPath);
                            }
                        }
                    }
                }
            }
        }

        private void dEBUGAddAPropertyToExportsMatchingCriteriaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc != null)
            {
                int count = 0;
                foreach (IExportEntry exp in pcc.Exports)
                {
                    if (exp.ObjectName == "StaticMeshCollectionActor")
                    {
                        var smas = exp.GetProperty<ArrayProperty<ObjectProperty>>("StaticMeshComponents");
                        foreach (ObjectProperty item in smas)
                        {
                            var export = pcc.getExport(item.Value - 1);
                            var props = export.GetProperties();
                            props.AddOrReplaceProp(new BoolProperty(false, "bUsePrecomputedShadows"));
                            export.WriteProperties(props);
                            count++;
                        }
                    }
                }
                System.Windows.MessageBox.Show($"Done. Updated {count} exports.");
            }
        }
    }
}
