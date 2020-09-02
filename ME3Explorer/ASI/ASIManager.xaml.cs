using ME3Explorer.SharedUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Input;
using System.Xml.Linq;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.MEDirectories;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using Microsoft.AppCenter.Analytics;

namespace ME3Explorer.ASI
{
    /// <summary>
    /// Interaction logic for ASIManager.xaml
    /// </summary>
    public partial class ASIManager : NotifyPropertyChangedWindowBase
    {

        #region Busy variables
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private bool _isBusyTaskbar;
        public bool IsBusyTaskbar
        {
            get => _isBusyTaskbar;
            set => SetProperty(ref _isBusyTaskbar, value);
        }

        private string _busyText;
        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }
        #endregion
        public static readonly string ASIManagerDataFolder = Path.Combine(App.AppDataFolder, @"ASIManager\");
        public static readonly string CachedASIsFolder = Path.Combine(ASIManagerDataFolder, "CachedASIs");

        public static readonly string ManifestLocation = Path.Combine(ASIManagerDataFolder, "manifest.xml");
        public static readonly string StagedManifestLocation = Path.Combine(ASIManagerDataFolder, "manifest_staged.xml");
        private List<ASIModUpdateGroup> ASIModUpdateGroups = new List<ASIModUpdateGroup>();
        private bool DeselectingDueToOtherList;
        private string _selectedASIDescription = "Select an ASI";
        private string _selectedASISubtext = "";
        private string _selectedASIAuthor = "";
        private string _installButtonText = "Install ASI";

        private readonly string ME1ASILoaderHash = "30660f25ab7f7435b9f3e1a08422411a";
        private readonly string ME2ASILoaderHash = "a5318e756893f6232284202c1196da13";
        private readonly string ME3ASILoaderHash = "1acccbdae34e29ca7a50951999ed80d5";
        private object selectedASIObject;
        public string SelectedASIDescription
        {
            get => _selectedASIDescription;
            set => SetProperty(ref _selectedASIDescription, value);
        }
        public string SelectedASISubtext
        {
            get => _selectedASISubtext;
            set => SetProperty(ref _selectedASISubtext, value);
        }
        public string SelectedASIName
        {
            get => _selectedASIAuthor;
            set => SetProperty(ref _selectedASIAuthor, value);
        }

        public string InstallButtonText
        {
            get => _installButtonText;
            set => SetProperty(ref _installButtonText, value);
        }
        public ObservableCollectionExtended<object> ME1DisplayedASIMods { get; } = new ObservableCollectionExtended<object>();
        public ObservableCollectionExtended<object> ME2DisplayedASIMods { get; } = new ObservableCollectionExtended<object>();
        public ObservableCollectionExtended<object> ME3DisplayedASIMods { get; } = new ObservableCollectionExtended<object>();

        private bool _me1LoaderInstalled;
        private bool _me2LoaderInstalled;
        private bool _me3LoaderInstalled;
        private List<InstalledASIMod> InstalledASIs;

        public bool ME1LoaderInstalled
        {
            get => _me1LoaderInstalled;
            set
            {
                SetProperty(ref _me1LoaderInstalled, value);
                OnPropertyChanged(nameof(ME1LoaderStatusText));
            }
        }
        public bool ME2LoaderInstalled
        {
            get => _me2LoaderInstalled;
            set
            {
                SetProperty(ref _me2LoaderInstalled, value);
                OnPropertyChanged(nameof(ME2LoaderStatusText));
            }
        }
        public bool ME3LoaderInstalled
        {
            get => _me3LoaderInstalled;
            set
            {
                SetProperty(ref _me3LoaderInstalled, value);
                OnPropertyChanged(nameof(ME3LoaderStatusText));
            }
        }

        public string ME1LoaderStatusText => ME1LoaderInstalled ? "ASI Loader Installed" : "ASI Loader Not Installed";
        public string ME2LoaderStatusText => ME2LoaderInstalled ? "ASI Loader Installed" : "ASI Loader Not Installed";
        public string ME3LoaderStatusText => ME3LoaderInstalled ? "ASI Loader Installed" : "ASI Loader Not Installed";

        /// <summary>
        /// This ASI Manager is a feature ported from ME3CMM and maintains synchronization with Mass Effect 3 Mod Manager's code for 
        /// managing and installing ASIs. ASIs are useful for debugging purposes, which is why this feature is now 
        /// part of ME3Explorer.
        /// 
        /// Please do not change the logic for this code (at least, for Mass Effect 3) as it may break compatibility with Mass
        /// Effect 3 Mod Manager (e.g. dual same ASIs are installed) and the ME3Tweaks serverside components.
        /// </summary>
        public ASIManager()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("ASI Manager", new WeakReference(this));
            Analytics.TrackEvent("Used tool", new Dictionary<string, string>()
            {
                { "Toolname", "ASI Manager" }
            });
            DataContext = this;

            if (!Directory.Exists(ASIManagerDataFolder))
            {
                Directory.CreateDirectory(ASIManagerDataFolder);
            }

            RefreshBinkStatuses();
            LoadCommands();
            InitializeComponent();
        }
        public ICommand InstallCommand { get; private set; }
        public ICommand SourceCodeCommand { get; private set; }

        private void LoadCommands()
        {
            InstallCommand = new GenericCommand(InstallUninstallASI, ASIIsSelected);
            SourceCodeCommand = new GenericCommand(ViewSourceCode, ManifestASIIsSelected);
        }

        private void ViewSourceCode()
        {
            if (selectedASIObject is ASIMod asi)
            {
                Process.Start(asi.SourceCodeLink);
            }
        }

        private void InstallUninstallASI()
        {
            if (selectedASIObject is InstalledASIMod instASI)
            {
                //Unknown ASI
                File.Delete(instASI.InstalledPath);
                RefreshASIStates();
            }
            else if (selectedASIObject is ASIMod asi)
            {
                //Check if this is actually installed or not (or outdated)
                var installedInfo = asi.InstalledInfo;
                if (installedInfo != null)
                {
                    var correspondingAsi = getManifestModByHash(installedInfo.Hash);
                    if (correspondingAsi != asi)
                    {
                        //Outdated - update mod
                        InstallASI(asi, installedInfo);
                    }
                    else
                    {
                        //Up to date - delete mod
                        File.Delete(installedInfo.InstalledPath);
                        RefreshASIStates();
                    }
                }
                else
                {
                    InstallASI(asi);
                }
            }
        }

        private void InstallASI(ASIMod asiToInstall, InstalledASIMod oldASIToRemoveOnSuccess = null)
        {
            IsBusy = true;
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (a, b) =>
            {
                ASIModUpdateGroup g = getUpdateGroupByMod(asiToInstall);
                string destinationFilename = $"{asiToInstall.InstalledPrefix}-v{asiToInstall.Version}.asi";
                string cachedPath = Path.Combine(CachedASIsFolder, destinationFilename);
                string destinationDirectory = getASIDirectoryForGame(g.Game);
                string finalPath = Path.Combine(destinationDirectory, destinationFilename);

                if (File.Exists(cachedPath))
                {
                    //Check hash first
                    var md5 = BitConverter.ToString(System.Security.Cryptography.MD5.Create().ComputeHash(File.ReadAllBytes(cachedPath))).Replace("-", "").ToLower();
                    if (md5 == asiToInstall.Hash)
                    {
                        File.Copy(cachedPath, finalPath);
                        return;
                    }
                }
                WebRequest request = WebRequest.Create(asiToInstall.DownloadLink);

                using (WebResponse response = request.GetResponse())
                {

                    MemoryStream memoryStream = new MemoryStream();
                    response.GetResponseStream().CopyTo(memoryStream);
                    //MD5 check on file for security
                    var md5 = BitConverter.ToString(System.Security.Cryptography.MD5.Create().ComputeHash(memoryStream.ToArray())).Replace("-", "").ToLower();
                    if (md5 != asiToInstall.Hash)
                    {
                        //ERROR!
                    }
                    else
                    {

                        File.WriteAllBytes(finalPath, memoryStream.ToArray());
                        if (!Directory.Exists(CachedASIsFolder))
                        {
                            Directory.CreateDirectory(CachedASIsFolder);
                        }
                        File.WriteAllBytes(cachedPath, memoryStream.ToArray()); //cache it
                        if (oldASIToRemoveOnSuccess != null)
                        {
                            File.Delete(oldASIToRemoveOnSuccess.InstalledPath);
                        }
                    }
                };
            };
            worker.RunWorkerCompleted += (a, b) =>
            {

                IsBusy = false;
                RefreshASIStates();
            };

            worker.RunWorkerAsync();
        }

        private string getASIDirectoryForGame(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1ASIDirectory;
                case MEGame.ME2:
                    return ME2ASIDirectory;
                case MEGame.ME3:
                    return ME3ASIDirectory;
                default:
                    return null;
            }
        }

        private bool ASIIsSelected() => selectedASIObject != null;

        private bool ManifestASIIsSelected() => selectedASIObject is ASIMod;

        private void RefreshBinkStatuses()
        {
            var md5 = System.Security.Cryptography.MD5.Create();
            if (ME1Directory.gamePath != null && Directory.Exists(ME1Directory.gamePath))
            {
                var binkw32 = Path.Combine(ME1Directory.gamePath, "Binaries", "binkw32.dll");
                if (File.Exists(binkw32))
                {
                    var hashBytes = md5.ComputeHash(File.ReadAllBytes(binkw32));
                    var hashStr = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                    ME1LoaderInstalled = hashStr == ME1ASILoaderHash;
                }
            }

            if (ME2Directory.gamePath != null && Directory.Exists(ME2Directory.gamePath))
            {
                var binkw32 = Path.Combine(ME2Directory.gamePath, "Binaries", "binkw32.dll");
                if (File.Exists(binkw32))
                {
                    var hashBytes = md5.ComputeHash(File.ReadAllBytes(binkw32));
                    var hashStr = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                    ME2LoaderInstalled = hashStr == ME2ASILoaderHash;
                }
            }

            if (ME3Directory.gamePath != null && Directory.Exists(ME3Directory.gamePath))
            {
                var binkw32 = Path.Combine(ME3Directory.gamePath, "Binaries", "win32", "binkw32.dll");
                if (File.Exists(binkw32))
                {
                    var hashBytes = md5.ComputeHash(File.ReadAllBytes(binkw32));
                    var hashStr = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                    ME3LoaderInstalled = hashStr == ME3ASILoaderHash;
                }
            }
        }

        private void RefreshASIStates()
        {
            //Remove installed ASIs
            ME1DisplayedASIMods.ReplaceAll(ME1DisplayedASIMods.Where(x => x is ASIMod).ToList());
            ME2DisplayedASIMods.ReplaceAll(ME2DisplayedASIMods.Where(x => x is ASIMod).ToList());
            ME3DisplayedASIMods.ReplaceAll(ME3DisplayedASIMods.Where(x => x is ASIMod).ToList());

            //Clear installation states
            foreach (var asi in ME1DisplayedASIMods)
            {
                if (asi is ASIMod a)
                {
                    a.UIOnly_Installed = false;
                    a.UIOnly_Outdated = false;
                    a.InstalledInfo = null;
                }
            }
            foreach (var asi in ME2DisplayedASIMods)
            {
                if (asi is ASIMod a)
                {
                    a.UIOnly_Installed = false;
                    a.UIOnly_Outdated = false;
                    a.InstalledInfo = null;
                }
            }
            foreach (var asi in ME3DisplayedASIMods)
            {
                if (asi is ASIMod a)
                {
                    a.UIOnly_Installed = false;
                    a.UIOnly_Outdated = false;
                    a.InstalledInfo = null;
                }
            }

            InstalledASIs = getInstalledASIMods();
            MapInstalledASIs();
            UpdateSelectionTexts(selectedASIObject);
        }

        private void MapInstalledASIs()
        {
            //Find what group contains our installed ASI.
            foreach (InstalledASIMod asi in InstalledASIs)
            {
                bool mapped = false;
                foreach (ASIModUpdateGroup amug in ASIModUpdateGroups)
                {
                    var matchingAsi = amug.ASIModVersions.FirstOrDefault(x => x.Hash == asi.Hash);
                    if (matchingAsi != null)
                    {
                        //We have an installed ASI in the manifest
                        var displayedItem = amug.ASIModVersions.MaxBy(y => y.Version);
                        displayedItem.UIOnly_Installed = true;
                        displayedItem.UIOnly_Outdated = displayedItem != matchingAsi; //is the displayed item (the latest) the same as the item we found?
                        displayedItem.InstalledInfo = asi;
                        mapped = true;
                        break;
                    }

                }
                if (!mapped)
                {
                    switch (asi.Game)
                    {
                        case MEGame.ME1:
                            ME1DisplayedASIMods.Add(asi);
                            break;
                        case MEGame.ME2:
                            ME2DisplayedASIMods.Add(asi);
                            break;
                        case MEGame.ME3:
                            ME3DisplayedASIMods.Add(asi);
                            break;
                    }
                }
            }
        }

        private void ASIManager_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            using (WebClient wc = new WebClient())
            {
                IsBusy = true;
                wc.DownloadFileCompleted += (a, b) => LoadManifest(StagedManifestLocation, true);
                wc.DownloadFileAsync(
                    // Param1 = Link of file
                    new System.Uri("https://raw.githubusercontent.com/ME3Tweaks/ME3Explorer/Beta/ME3Explorer/ME3Tweaks/asimanifest.xml"),
                    // Param2 = Path to save
                    StagedManifestLocation);
            }
        }

        private void LoadManifest(string manifestToLoad, bool isStaged = false)
        {
            IsBusy = false;
            try
            {
                XElement rootElement = XElement.Load(manifestToLoad);

                //I Love Linq
                ASIModUpdateGroups = (from e in rootElement.Elements("updategroup")
                                      select new ASIModUpdateGroup
                                      {
                                          UpdateGroupId = (int)e.Attribute("groupid"),
                                          Game = intToGame((int)e.Attribute("game")),
                                          ASIModVersions = e.Elements("asimod").Select(z => new ASIMod
                                          {
                                              Name = (string)z.Element("name"),
                                              InstalledPrefix = (string)z.Element("installedname"),
                                              Author = (string)z.Element("author"),
                                              Version = (string)z.Element("version"),
                                              Description = (string)z.Element("description"),
                                              Hash = (string)z.Element("hash"),
                                              SourceCodeLink = (string)z.Element("sourcecode"),
                                              DownloadLink = (string)z.Element("downloadlink"),
                                          }).ToList()
                                      }).ToList();

                ME1DisplayedASIMods.ReplaceAll(ASIModUpdateGroups.Where(x => x.Game == MEGame.ME1).Select(x => x.ASIModVersions.MaxBy(y => y.Version)).OrderBy(x => x.Name)); //latest
                ME2DisplayedASIMods.ReplaceAll(ASIModUpdateGroups.Where(x => x.Game == MEGame.ME2).Select(x => x.ASIModVersions.MaxBy(y => y.Version)).OrderBy(x => x.Name)); //latest
                ME3DisplayedASIMods.ReplaceAll(ASIModUpdateGroups.Where(x => x.Game == MEGame.ME3).Select(x => x.ASIModVersions.MaxBy(y => y.Version)).OrderBy(x => x.Name)); //latest

                RefreshASIStates();
                if (isStaged)
                {
                    File.Copy(StagedManifestLocation, ManifestLocation, true); //this will make sure cached manifest is parsable.
                }
            }
            catch (Exception e)
            {
                if (isStaged && File.Exists(ManifestLocation))
                {
                    //try cached instead
                    LoadManifest(ManifestLocation, false);
                    return;
                }
                RefreshASIStates();
                throw new Exception("Error parsing the ASI Manifest: " + e.Message);
            }

        }

        /// <summary>
        /// Object containing information about an ASI mod in the ASI mod manifest
        /// </summary>
        public class ASIMod : NotifyPropertyChangedBase
        {
            public string DownloadLink { get; internal set; }
            public string SourceCodeLink { get; internal set; }
            public string Hash { get; internal set; }
            public string Version { get; internal set; }
            public string Author { get; internal set; }
            public string InstalledPrefix { get; internal set; }
            public string Name { get; internal set; }
            public string Description { get; internal set; }

            private bool _uionly_installed;
            private bool _uionly_outdated;
            private InstalledASIMod _installedInfo;
            public bool UIOnly_Installed { get => _uionly_installed; set => SetProperty(ref _uionly_installed, value); }
            public bool UIOnly_Outdated { get => _uionly_outdated; set => SetProperty(ref _uionly_outdated, value); }
            public InstalledASIMod InstalledInfo
            {
                get => _installedInfo;
                set => SetProperty(ref _installedInfo, value);
            }
        }

        /// <summary>
        /// Object describing an installed ASI file. It is not a general ASI mod object but it can be mapped to one
        /// </summary>
        public class InstalledASIMod
        {
            public InstalledASIMod(string asiFile, MEGame game)
            {
                Game = game;
                InstalledPath = asiFile;
                Filename = Path.GetFileNameWithoutExtension(asiFile);
                Hash = BitConverter.ToString(System.Security.Cryptography.MD5.Create()
                    .ComputeHash(File.ReadAllBytes(asiFile))).Replace("-", "").ToLower();
            }

            public MEGame Game { get; }
            public string InstalledPath { get; set; }
            public string Hash { get; set; }
            public string Filename { get; set; }
        }

        public class ASIModUpdateGroup
        {
            public List<ASIMod> ASIModVersions { get; internal set; }
            public int UpdateGroupId { get; internal set; }
            public MEGame Game { get; internal set; }
        }

        private MEGame intToGame(int i)
        {
            switch (i)
            {
                case 1:
                    return MEGame.ME1;
                case 2:
                    return MEGame.ME2;
                case 3:
                    return MEGame.ME3;
                default:
                    return MEGame.Unknown;
            }
        }

        private void ASIManagerLists_SelectedChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DeselectingDueToOtherList) return;
            DeselectingDueToOtherList = true;
            if (sender != ME1_InstalledASIs_List)
            {
                ME1_InstalledASIs_List.SelectedIndex = -1;
            }
            if (sender != ME2_InstalledASIs_List)
            {
                ME2_InstalledASIs_List.SelectedIndex = -1;
            }
            if (sender != ME3_InstalledASIs_List)
            {
                ME3_InstalledASIs_List.SelectedIndex = -1;
            }
            if (e.AddedItems.Count > 0)
            {
                UpdateSelectionTexts(e.AddedItems[0]);
                selectedASIObject = e.AddedItems[0];
            }
            else
            {
                UpdateSelectionTexts(null);
                selectedASIObject = null;
            }

            DeselectingDueToOtherList = false;
        }

        private void UpdateSelectionTexts(object v)
        {
            if (v is ASIMod asiMod)
            {
                SelectedASIDescription = asiMod.Description;
                SelectedASIName = asiMod.Name;
                string subtext = $"By {asiMod.Author} | Version {asiMod.Version}\n";
                if (asiMod.UIOnly_Outdated)
                {
                    subtext += "Installed, outdated";
                    InstallButtonText = "Update ASI";
                }
                else if (asiMod.UIOnly_Installed)
                {
                    subtext += "Installed, up to date";
                    InstallButtonText = "Uninstall ASI";

                }
                else
                {
                    subtext += "Not installed";
                    InstallButtonText = "Install ASI";

                }
                SelectedASISubtext = subtext;
            }
            else if (v is InstalledASIMod nonManifestAsiMod)
            {
                SelectedASIDescription = "Unknown ASI mod. You should be careful with this ASI as it may contain malicious code.";
                SelectedASIName = nonManifestAsiMod.Filename;
                SelectedASISubtext = $"ASI not present in manifest";
                InstallButtonText = "Uninstall ASI";
            }
            else
            {
                SelectedASIDescription = "Select an ASI to view options";
                SelectedASIName = "";
                SelectedASISubtext = "";
                selectedASIObject = null;
                InstallButtonText = "No ASI selected";

            }
        }

        public string ME1ASIDirectory => ME1Directory.gamePath != null ? Path.Combine(ME1Directory.gamePath, "Binaries", "asi") : null;
        public string ME2ASIDirectory => ME2Directory.gamePath != null ? Path.Combine(ME2Directory.gamePath, "Binaries", "asi") : null;
        public string ME3ASIDirectory => ME3Directory.gamePath != null ? Path.Combine(ME3Directory.gamePath, "Binaries", "win32", "asi") : null;


        /// <summary>
        /// Gets a list of installed ASI mods.
        /// </summary>
        /// <param name="game">Game to filter results by. Enter 1 2 or 3 for that game only, or anything else to get everything.</param>
        /// <returns></returns>
        private List<InstalledASIMod> getInstalledASIMods(int game = 0)
        {
            List<InstalledASIMod> results = new List<InstalledASIMod>();
            string asiDirectory = null;
            string gameDirectory = null;
            MEGame gameEnum = MEGame.Unknown;
            switch (game)
            {
                case 1:
                    asiDirectory = ME1ASIDirectory;
                    gameDirectory = ME1Directory.gamePath;
                    gameEnum = MEGame.ME1;
                    break;
                case 2:
                    asiDirectory = ME2ASIDirectory;
                    gameDirectory = ME2Directory.gamePath;
                    gameEnum = MEGame.ME2;
                    break;
                case 3:
                    asiDirectory = ME3ASIDirectory;
                    gameDirectory = ME3Directory.gamePath;
                    gameEnum = MEGame.ME3;
                    break;
                default:
                    results.AddRange(getInstalledASIMods(1));
                    results.AddRange(getInstalledASIMods(2));
                    results.AddRange(getInstalledASIMods(3));
                    return results;
            }
            if (asiDirectory != null && Directory.Exists(gameDirectory))
            {
                if (!Directory.Exists(asiDirectory))
                {
                    Directory.CreateDirectory(asiDirectory);
                    return results; //It won't have anything in it if we are creating it
                }
                var asiFiles = Directory.GetFiles(asiDirectory, "*.asi");
                foreach (var asiFile in asiFiles)
                {
                    results.Add(new InstalledASIMod(asiFile, gameEnum));
                }
            }

            return results;
        }

        private ASIMod getManifestModByHash(string hash)
        {
            foreach (var updateGroup in ASIModUpdateGroups)
            {
                var asi = updateGroup.ASIModVersions.FirstOrDefault(x => x.Hash == hash);
                if (asi != null) return asi;
            }
            return null;
        }

        private ASIModUpdateGroup getUpdateGroupByMod(ASIMod mod)
        {
            foreach (var updateGroup in ASIModUpdateGroups)
            {
                var asi = updateGroup.ASIModVersions.FirstOrDefault(x => x == mod);
                if (asi != null) return updateGroup;
            }
            return null;
        }
    }
}
