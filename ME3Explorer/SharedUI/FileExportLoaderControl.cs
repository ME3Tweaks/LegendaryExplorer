using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Inherits from Export Loader Control. A control using this class has the ability to load its own files when popped out into its own window, for things like TLK file loading which are not package files.
    /// </summary>
    public abstract class FileExportLoaderControl : ExportLoaderControl
    {
        public abstract void LoadFile(string filepath);
        public string LoadedFile;
        public abstract bool CanLoadFile();
        public abstract void Save();
        public abstract void SaveAs();
        internal abstract void OpenFile();
        public abstract bool CanSave();
        public List<string> RFiles;
        private const string RECENTFILES_FILE = "RECENTFILES";
        internal abstract string DataFolder { get; }
        internal MenuItem Recents_MenuItem;
        public void SaveRecentList()
        {
            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
            }
            string path = DataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllLines(path, RFiles);
        }

        public void AddRecent(string s, bool loadingList)
        {
            RFiles = RFiles.Where(x => !x.Equals(s, StringComparison.InvariantCultureIgnoreCase)).ToList();
            if (loadingList)
            {
                RFiles.Add(s); //in order
            }
            else
            {
                RFiles.Insert(0, s); //put at front
            }
            if (RFiles.Count > 10)
            {
                RFiles.RemoveRange(10, RFiles.Count - 10);
            }
            //ExportLoaderHostedWindow will handle the menu being enabled
        }

        public void RefreshRecent(bool propogate, List<string> recents = null)
        {
            if (propogate && recents != null)
            {
                //we are posting an update to other instances

                var forms = System.Windows.Forms.Application.OpenForms;
                foreach (var form in Application.Current.Windows)
                {
                    if (form is ExportLoaderHostedWindow wpf && wpf.HostedControl is FileExportLoaderControl felc && this != wpf.HostedControl && wpf.HostedControl.GetType() == GetType())
                    {
                        felc.RefreshRecent(false, RFiles);
                    }
                }
            }
            else if (recents != null)
            {
                //we are receiving an update
                RFiles = new List<string>(recents);
            }
            Recents_MenuItem.Items.Clear();
            if (RFiles.Count <= 0)
            {
                Recents_MenuItem.IsEnabled = false;
                return;
            }
            Recents_MenuItem.IsEnabled = true;
            int i = 0;
            foreach (string filepath in RFiles)
            {
                MenuItem fr = new MenuItem()
                {
                    Header = filepath.Replace("_", "__"),
                    Tag = filepath
                };
                fr.Click += RecentFile_click;
                Recents_MenuItem.Items.Add(fr);
                i++;
            }
        }

        internal abstract void RecentFile_click(object sender, RoutedEventArgs e);

        public void LoadRecentList()
        {
            Recents_MenuItem.IsEnabled = false;
            RFiles = new List<string>();
            string path = DataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
            {
                string[] recents = File.ReadAllLines(path);
                foreach (string recent in recents)
                {
                    if (File.Exists(recent))
                    {
                        AddRecent(recent, true);
                    }
                }
            }
        }
    }
}
