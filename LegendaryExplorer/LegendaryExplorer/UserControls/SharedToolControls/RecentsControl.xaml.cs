using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BCnEncoder.Shared.ImageFiles;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Converters;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.UserControls.SharedToolControls
{
    /// <summary>
    /// Control that handles the 'Recents' system, including the main no-open-file panel and the menu system for windows. All calls must be done from a UI thread.
    /// </summary>
    public partial class RecentsControl : NotifyPropertyChangedControlBase, IDisposable
    {
        public class RecentItem
        {
            public RecentItem(string path, MEGame? game)
            {
                Path = path;
                Game = game;
            }

            public RecentItem() { }

            public string ConvertToRecentEntry()
            {
                // Null coalescing doesn't work here apparently
                return $"{(Game == null ? "NUL" : Game)} {Path}";
            }

            public static RecentItem FromRecentEntryString(string entry)
            {
#if DEBUG
                // TRANSITION TO NEW RECENT SYSTEM ONLY CODE!!
                // Remove later. This is debug only cause it was made when LEX was in dev
                if (File.Exists(entry))
                {
                    return new RecentItem(entry, null);
                }

#endif
                var gameId = entry.Substring(0, 3);
                MEGame? game = null;
                if (Enum.TryParse<MEGame>(gameId, false, out var _game))
                {
                    game = _game;
                }
                return new RecentItem(entry.Substring(4), game);
            }

            public MEGame? Game { get; }
            public string Path { get; }
        }
        private Action<string> RecentItemClicked;

        public ObservableCollectionExtended<RecentItem> RecentItems { get; } = new();

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
            RecentFileOpenCommand = new RelayCommand(filePath => RecentItemClicked?.Invoke((string)filePath));
        }

        private string RecentsAppDataFile => Path.Combine(Directory.CreateDirectory(Path.Combine(AppDirectories.AppDataFolder, RecentsFoldername)).FullName, "RECENTFILES");

        /// <summary>
        /// Must be called before the control will properly work
        /// </summary>
        /// <param name="filename">Recents filename. Stored in the appdata. Do not pass an extension, just the name.</param>
        /// <param name="openFileCallback">The callback to invoke when a recents item is clicked.</param>
        public void InitRecentControl(string toolname, MenuItem recentsMenu, Action<string> openFileCallback)
        {
            RecentsMenu = recentsMenu;
            RecentsMenu.IsEnabled = false; //Default to false as there may be no recents
            RecentItemClicked = null;
            if (toolname == null)
            {
                // Recents is disabled
                RecentItems.ClearEx();
                return;
            }

            // Init the control
            RecentsFoldername = toolname;
            RecentItemClicked = openFileCallback;
            
            // Load recents list
            if (File.Exists(RecentsAppDataFile))
            {
                string[] recents = File.ReadAllLines(RecentsAppDataFile);
                SetRecents(recents.Select(RecentItem.FromRecentEntryString));
            }
        }

        /// <summary>
        /// Sets the whole recents list. Does not propogate.
        /// </summary>
        /// <param name="recents"></param>
        private void SetRecents(IEnumerable<RecentItem> recents)
        {
            RecentItems.ClearEx();
            foreach (var referencedFile in recents)
            {
                if (IsFolderRecents)
                {
                    if (Directory.Exists(referencedFile.Path))
                    {
                        AddRecent(referencedFile.Path, true, referencedFile.Game);
                    }
                }
                else if (File.Exists(referencedFile.Path))
                {
                    AddRecent(referencedFile.Path, true, referencedFile.Game);
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
            foreach (var recentItem in RecentItems)
            {
                var iconBitmap = GameToImageIconConverter.StaticConvert(recentItem.Game);
                var fr = new MenuItem
                {
                    Icon = iconBitmap == null ? null : new Image { Source = iconBitmap },
                    Header = recentItem.Path.Replace("_", "__"),
                    Tag = recentItem.Path
                };
                fr.Click += (x, y) => RecentItemClicked?.Invoke((string)fr.Tag);
                RecentsMenu.Items.Add(fr);
            }
        }

        /// <summary>
        /// Adds a new item to the recents list in the appropriate position.
        /// </summary>
        /// <param name="path">The file path of the file that is being added</param>
        /// <param name="isLoading">If the control is loading, and the list shouldn't be cleared, rather just appended to. </param>
        public void AddRecent(string path, bool isLoading, MEGame? game)
        {
            if (isLoading)
            {
                RecentItems.Add(new RecentItem(path, game)); //in order
            }
            else
            {
                // Remove the new recent from the list if it exists - as we will re-insert it (at the front)
                RecentItems.ReplaceAll(RecentItems.Where(x =>
                    !x.Path.Equals(path, StringComparison.InvariantCultureIgnoreCase)).ToList());
                RecentItems.Insert(0, new RecentItem(path, game)); //put at front
            }
            while (RecentItems.Count > 10)
            {
                RecentItems.RemoveAt(10); //Just remove trailing items
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
            if (RecentsFoldername != null)
            {
                File.WriteAllLines(RecentsAppDataFile, RecentItems.Select(x => x.ConvertToRecentEntry()));
                if (propogate)
                {
                    PropogateRecentsChange(true, RecentItems);
                }
            }
        }

        public void PropogateRecentsChange(bool outboundPropogation, IEnumerable<RecentItem> newRecents)
        {
            if (outboundPropogation)
            {
                var propogationSource = Window.GetWindow(this);
                //we are posting an update to other instances
                foreach (var form in Application.Current.Windows)
                {
                    if (form.GetType() == propogationSource.GetType() && !ReferenceEquals(form, propogationSource) && form is IRecents recentsSupportedWindow && ((Window)form).IsLoaded)
                    {
                        recentsSupportedWindow.PropogateRecentsChange(RecentsFoldername, newRecents);
                    }
                }
            }
            else
            {
                // Inbound, we are receiving an update
                SetRecents(newRecents);
            }
        }

        public void Dispose()
        {
            RecentItemClicked = null;
            RecentsMenu = null;
        }
    }
}
