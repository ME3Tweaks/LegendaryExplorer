using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Interfaces;
using ME3ExplorerCore.Misc;

namespace LegendaryExplorer.UserControls.SharedToolControls
{
    /// <summary>
    /// Control that handles the 'Recents' system, including the main no-open-file panel and the menu system for windows. All calls must be done from a UI thread.
    /// </summary>
    public partial class RecentsControl : NotifyPropertyChangedControlBase
    {
        private Action<string> RecentItemClicked;

        public ObservableCollectionExtended<string> RecentPaths { get; } = new ObservableCollectionExtended<string>();

        public bool IsFolderRecents
        {
            get => (bool)GetValue(IsFolderRecentsProperty);
            set => SetValue(IsFolderRecentsProperty, value);
        }

        public static readonly DependencyProperty IsFolderRecentsProperty = DependencyProperty.Register(
            nameof(IsFolderRecents), typeof(bool), typeof(RecentsControl), new PropertyMetadata(false));



        public RecentsControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        public RelayCommand RecentFileOpenCommand { get; private set; }

        private void LoadCommands()
        {
            RecentFileOpenCommand = new RelayCommand(filePath => RecentItemClicked((string)filePath));
        }

        private string RecentsAppDataFile => Path.Combine(Directory.CreateDirectory(Path.Combine(App.AppDataFolder, RecentsFoldername)).FullName, "RECENTFILES");

        /// <summary>
        /// Must be called before the control will properly work
        /// </summary>
        /// <param name="filename">Recents filename. Stored in the appdata. Do not pass an extension, just the name.</param>
        /// <param name="openFileCallback">The callback to invoke when a recents item is clicked.</param>
        public void InitRecentControl(string toolname, MenuItem recentsMenu, Action<string> openFileCallback)
        {
            RecentsFoldername = toolname;
            RecentsMenu = recentsMenu;
            RecentItemClicked = openFileCallback;

            RecentsMenu.IsEnabled = false; //Default to false as there may be no recents

            // Load recents list
            if (File.Exists(RecentsAppDataFile))
            {
                string[] recents = File.ReadAllLines(RecentsAppDataFile);
                SetRecents(recents);
            }
        }

        /// <summary>
        /// Sets the whole recents list. Does not propogate.
        /// </summary>
        /// <param name="recents"></param>
        private void SetRecents(IEnumerable<string> recents)
        {
            RecentPaths.ClearEx();
            foreach (string referencedFile in recents)
            {
                if (IsFolderRecents)
                {
                    if (Directory.Exists(referencedFile))
                    {
                        AddRecent(referencedFile, true);
                    }
                }
                else if (File.Exists(referencedFile))
                {
                    AddRecent(referencedFile, true);
                }
            }
            RefreshRecentsMenu();
        }

        /// <summary>
        /// Appdata subfolder that will hold the RECENTS file
        /// </summary>
        public string RecentsFoldername { get; set; }

        /// <summary>
        /// Menu that is associated with the recents and is updated when the recents change
        /// </summary>
        public MenuItem RecentsMenu { get; set; }

        /// <summary>
        /// Refreshes the recents menu items
        /// </summary>
        /// <param name="recentsContainer"></param>
        private void RefreshRecentsMenu()
        {
            RecentsMenu.Items.Clear();
            foreach (string filepath in RecentPaths)
            {
                MenuItem fr = new MenuItem()
                {
                    Header = filepath.Replace("_", "__"),
                    Tag = filepath
                };
                fr.Click += (x, y) => RecentItemClicked?.Invoke((string)fr.Tag);
                RecentsMenu.Items.Add(fr);
            }
        }

        /// <summary>
        /// Adds a new item to the recents list in the appropriate position.
        /// </summary>
        /// <param name="newRecent"></param>
        /// <param name="isLoading"></param>
        public void AddRecent(string newRecent, bool isLoading)
        {
            if (isLoading)
            {
                RecentPaths.Add(newRecent); //in order
            }
            else
            {
                // Remove the new recent from the list if it exists - as we will re-insert it (at the front)
                RecentPaths.ReplaceAll(RecentPaths.Where(x =>
                    !x.Equals(newRecent, StringComparison.InvariantCultureIgnoreCase)).ToList());
                RecentPaths.Insert(0, newRecent); //put at front
            }
            while (RecentPaths.Count > 10)
            {
                RecentPaths.RemoveAt(10); //Just remove trailing items
            }

            RecentsMenu.IsEnabled = true; //An item exists in the menu
            if (!isLoading)
            {
                RefreshRecentsMenu();
                SaveRecentList(true);
            }
        }

        /// <summary>
        /// Commits the list of recent files to disk.
        /// </summary>
        /// <param name="propogate">If the list of recents from this instance should be shared to other instances that are hosted by the same type of window</param>
        public void SaveRecentList(bool propogate)
        {
            File.WriteAllLines(RecentsAppDataFile, RecentPaths);
            if (propogate)
            {
                PropogateRecentsChange(true, RecentPaths);
            }
        }

        public void PropogateRecentsChange(bool outboundPropogation, IEnumerable<string> newRecents)
        {
            if (outboundPropogation)
            {
                var propogationSource = Window.GetWindow(this);
                //we are posting an update to other instances
                foreach (var form in Application.Current.Windows)
                {
                    if (form.GetType() == propogationSource.GetType() && !ReferenceEquals(form, propogationSource) && form is IRecents recentsSupportedWindow && ((Window)form).IsLoaded)
                    {
                        recentsSupportedWindow.PropogateRecentsChange(newRecents);
                    }
                }
            }
            else
            {
                // Inbound, we are receiving an update
                SetRecents(newRecents);
            }
        }
    }
}
