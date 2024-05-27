using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LegendaryExplorer.Misc;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Inherits from Export Loader Control. A control using this class has the ability to load its own files when popped out into its own window, for things like TLK file loading which are not package files.
    /// </summary>
    public abstract class FileExportLoaderControl : ExportLoaderControl
    {
        protected FileExportLoaderControl(string memoryTrackerName) : base(memoryTrackerName)
        {
        }

        /// <summary>
        /// Invoked when a file is loaded through LoadFile(). Subclasses of this class must ensure they call OnFileLoaded() at the end of LoadFile() so subscribers can be notified.
        /// </summary>
        public event EventHandler FileLoaded;
        protected virtual void OnFileLoaded(EventArgs e)
        {
            FileLoaded?.Invoke(this, e);
        }

        public abstract void LoadFile(string filepath);
        public string LoadedFile;
        public abstract bool CanLoadFile();
        public abstract void Save();
        public abstract void SaveAs();
        internal abstract void OpenFile();
        public abstract bool CanSave();
        //public List<string> RFiles;
        //private const string RECENTFILES_FILE = "RECENTFILES";
        //private string RecentsAppDataFile => Path.Combine(Directory.CreateDirectory(Path.Combine(AppDirectories.AppDataFolder, DataFolder)).FullName, "RECENTFILES");

        //internal abstract string DataFolder { get; }
        //internal MenuItem Recents_MenuItem;
        //public void SaveRecentList()
        //{
        //    File.WriteAllLines(RecentsAppDataFile, RFiles);
        //}

        //public void AddRecent(string s, bool loadingList)
        //{
        //    RFiles = RFiles.Where(x => !x.Equals(s, StringComparison.InvariantCultureIgnoreCase)).ToList();
        //    if (loadingList)
        //    {
        //        RFiles.Add(s); //in order
        //    }
        //    else
        //    {
        //        RFiles.Insert(0, s); //put at front
        //    }
        //    if (RFiles.Count > 10)
        //    {
        //        RFiles.RemoveRange(10, RFiles.Count - 10);
        //    }
        //    //ExportLoaderHostedWindow will handle the menu being enabled
        //}

        //public void RefreshRecent(bool propogate, List<string> recents = null)
        //{
        //    if (propogate && recents != null)
        //    {
        //        //we are posting an update to other instances

        //        var forms = System.Windows.Forms.Application.OpenForms;
        //        foreach (var form in Application.Current.Windows)
        //        {
        //            if (form is ExportLoaderHostedWindow wpf && wpf.HostedControl is FileExportLoaderControl felc && this != wpf.HostedControl && wpf.HostedControl.GetType() == GetType())
        //            {
        //                felc.RefreshRecent(false, RFiles);
        //            }
        //        }
        //    }
        //    else if (recents != null)
        //    {
        //        //we are receiving an update
        //        RFiles = new List<string>(recents);
        //    }
        //    Recents_MenuItem.Items.Clear();
        //    if (RFiles.Count <= 0)
        //    {
        //        Recents_MenuItem.IsEnabled = false;
        //        return;
        //    }
        //    Recents_MenuItem.IsEnabled = true;
        //    int i = 0;
        //    foreach (string filepath in RFiles)
        //    {
        //        MenuItem fr = new MenuItem()
        //        {
        //            Header = filepath.Replace("_", "__"),
        //            Tag = filepath
        //        };
        //        fr.Click += RecentFile_click;
        //        Recents_MenuItem.Items.Add(fr);
        //        i++;
        //    }
        //}

        public event EventHandler ModifiedStatusChanging;
        protected virtual void OnModifiedStatusChanging(EventArgs e)
        {
            ModifiedStatusChanging?.Invoke(this, e);
        }
        private bool _fileModified;
        public bool FileModified
        {
            get => _fileModified;
            set
            {
                SetProperty(ref _fileModified, value);
                OnModifiedStatusChanging(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Name of the tool. If using the recents system this MUST be set to non-null
        /// </summary>
        public abstract string Toolname { get; }

        /// <summary>
        /// Forcibly hides the recents control
        /// </summary>
        public bool ForceHideRecents { get; set; }

        //internal abstract void RecentFile_click(object sender, RoutedEventArgs e);

        internal abstract bool CanLoadFileExtension(string extension);
        //public void LoadRecentList()
        //{
        //    Recents_MenuItem.IsEnabled = false;
        //    RFiles = new List<string>();
        //    if (File.Exists(RecentsAppDataFile))
        //    {
        //        string[] recents = File.ReadAllLines(RecentsAppDataFile);
        //        foreach (string recent in recents)
        //        {
        //            if (File.Exists(recent))
        //            {
        //                AddRecent(recent, true);
        //            }
        //        }
        //    }
        //}
    }
}
