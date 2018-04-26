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
                MessageBox.Show("No exports have serial size mismatches.");
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

        private void dEBUGCopyConfigurablePropsToClipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string fpath = @"X:\Mass Effect Games HDD\Mass Effect";
            var ext = new List<string> { "u", "upk", "sfm" };
            var files = Directory.GetFiles(fpath, "*.*", SearchOption.AllDirectories)
              .Where(file => new string[] { ".sfm", ".upk", ".u" }
              .Contains(Path.GetExtension(file).ToLower()))
              .ToList();
            StringBuilder sb = new StringBuilder();

            int threads = Environment.ProcessorCount;
            string[] results = files.AsParallel().WithDegreeOfParallelism(threads).WithExecutionMode(ParallelExecutionMode.ForceParallelism).Select(ScanForConfigValues).ToArray();


            foreach (string res in results)
            {
                sb.Append(res);
            }
            try
            {
                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Finished");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
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

        
    }
}
