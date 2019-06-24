using ME3Explorer.SharedUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;

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
        public static readonly string ManifestLocation = Path.Combine(ASIManagerDataFolder, "manifest.xml");
        private List<ASIModUpdateGroup> ASIModUpdateGroups = new List<ASIModUpdateGroup>();
        private bool DeselectingDueToOtherList;
        private string _selectedASIDescription = "Select an ASI";


        private readonly string ME1ASILoaderHash = "30660f25ab7f7435b9f3e1a08422411a";
        private readonly string ME2ASILoaderHash = "a5318e756893f6232284202c1196da13";
        private readonly string ME3ASILoaderHash = "1acccbdae34e29ca7a50951999ed80d5";
        public string SelectedASIDescription
        {
            get => _selectedASIDescription;
            set => SetProperty(ref _selectedASIDescription, value);
        }
        public ObservableCollectionExtended<ASIMod> ME1DisplayedASIMods { get; } = new ObservableCollectionExtended<ASIMod>();
        public ObservableCollectionExtended<ASIMod> ME2DisplayedASIMods { get; } = new ObservableCollectionExtended<ASIMod>();
        public ObservableCollectionExtended<ASIMod> ME3DisplayedASIMods { get; } = new ObservableCollectionExtended<ASIMod>();

        private bool _me1LoaderInstalled;
        private bool _me2LoaderInstalled;
        private bool _me3LoaderInstalled;

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
            DataContext = this;

            if (!Directory.Exists(ASIManagerDataFolder))
            {
                Directory.CreateDirectory(ASIManagerDataFolder);
            }

            RefreshBinkStatuses();

            InitializeComponent();
        }

        private void RefreshBinkStatuses()
        {
            var md5 = System.Security.Cryptography.MD5.Create();
            if (ME1Directory.gamePath != null)
            {
                var binkw32 = Path.Combine(ME1Directory.gamePath, "Binaries", "binkw32.dll");
                if (File.Exists(binkw32))
                {
                    var hashBytes = md5.ComputeHash(File.ReadAllBytes(binkw32));
                    var hashStr = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                    ME1LoaderInstalled = hashStr == ME1ASILoaderHash;
                }
            }

            if (ME2Directory.gamePath != null)
            {
                var binkw32 = Path.Combine(ME2Directory.gamePath, "Binaries", "binkw32.dll");
                if (File.Exists(binkw32))
                {
                    var hashBytes = md5.ComputeHash(File.ReadAllBytes(binkw32));
                    var hashStr = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                    ME2LoaderInstalled = hashStr == ME2ASILoaderHash;
                }
            }

            if (ME3Directory.gamePath != null)
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

        private void wc_DownloadCompleted(object sender, AsyncCompletedEventArgs eventargs)
        {
            IsBusy = false;
            XElement rootElement = XElement.Load(ManifestLocation);
            /*
             *       <name>Mouse Disabler</name>
      <installedname>MouseDisabler</installedname>
      <author>Erik JS</author>
      <description>Makes Mass Effect 3 not respond to mouse input. It's very useful with the controller mods because the interfaces for some reason respond to mouse input on the first frame and can interfere with scrolling.</description>
      <version>2</version>
      <hash>fe33ab85c79875e2deb0df6ca3e7c232</hash>
      <sourcecode>https://github.com/Erik-JS/ME3-ASI/tree/master/ME3MouseDisabler</sourcecode>
      <downloadlink>https://me3tweaks.com/mods/asi/MouseDisabler-v2.asi</downloadlink>*/

            //I Love Linq
            ASIModUpdateGroups = (from e in rootElement.Elements("updategroup")
                                  select new ASIModUpdateGroup
                                  {
                                      UpdateGroupId = (int)e.Attribute("groupid"),
                                      Game = (int)e.Attribute("game"),
                                      ASIModVersions = e.Elements("asimod").Select(z => new ASIMod
                                      {
                                          Name = (string)z.Element("name"),
                                          InstalledPrefix = (string)z.Element("installedname"),
                                          Author = (string)z.Element("author"),
                                          Version = (string)z.Element("version"),
                                          Description = (string)z.Element("description"),
                                          Hash = (string)z.Element("hash"),
                                          SourceCodeLink = (string)z.Element("sourcecode"),
                                          DownloadLink = (string)z.Element("downloadlink")
                                      }).ToList()
                                  }).ToList();

            var installedASIs = getInstalledASIMods();


            ME1DisplayedASIMods.ReplaceAll(ASIModUpdateGroups.Where(x => x.Game == 1).Select(x => x.ASIModVersions.MaxBy(y => y.Version)));
            ME2DisplayedASIMods.ReplaceAll(ASIModUpdateGroups.Where(x => x.Game == 2).Select(x => x.ASIModVersions.MaxBy(y => y.Version)));
            ME3DisplayedASIMods.ReplaceAll(ASIModUpdateGroups.Where(x => x.Game == 3).Select(x => x.ASIModVersions.MaxBy(y => y.Version)));
        }

        // Event to track the progress
        void wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
        }

        private void ASIManager_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            using (WebClient wc = new WebClient())
            {
                IsBusy = true;
                wc.DownloadProgressChanged += wc_DownloadProgressChanged;
                wc.DownloadFileCompleted += wc_DownloadCompleted;
                wc.DownloadFileAsync(
                    // Param1 = Link of file
                    new System.Uri("https://me3tweaks.com/mods/asi/getmanifest?AllGames=1"),
                    // Param2 = Path to save
                    ManifestLocation);
            }
        }

        /// <summary>
        /// Object containing information about an ASI mod in the ASI mod manifest
        /// </summary>
        public class ASIMod
        {
            public string DownloadLink { get; internal set; }
            public string SourceCodeLink { get; internal set; }
            public string Hash { get; internal set; }
            public string Version { get; internal set; }
            public string Author { get; internal set; }
            public string InstalledPrefix { get; internal set; }
            public string Name { get; internal set; }
            public string Description { get; internal set; }
        }

        /// <summary>
        /// Object describing an installed ASI file. It is not a general ASI mod object but it can be mapped to one
        /// </summary>
        public class InstalledASIMod
        {
            public InstalledASIMod(string asiFile)
            {
                InstalledPath = asiFile;
                Filename = Path.GetFileNameWithoutExtension(asiFile);
                Hash = BitConverter.ToString(System.Security.Cryptography.MD5.Create()
                    .ComputeHash(File.ReadAllBytes(asiFile))).Replace("-", "").ToLower();
            }

            public string InstalledPath { get; set; }
            public string Hash { get; set; }
            public string Filename { get; set; }
        }

        public class ASIModUpdateGroup
        {
            public List<ASIMod> ASIModVersions { get; internal set; }
            public int UpdateGroupId { get; internal set; }
            public int Game { get; internal set; }
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
                var newSelectedASI = (ASIMod)e.AddedItems[0];
                SelectedASIDescription = newSelectedASI.Description;

            }
            DeselectingDueToOtherList = false;

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

            switch (game)
            {
                case 1:
                    asiDirectory = ME1ASIDirectory;
                    break;
                case 2:
                    asiDirectory = ME2ASIDirectory;
                    break;
                case 3:
                    asiDirectory = ME3ASIDirectory;
                    break;
                default:
                    results.AddRange(getInstalledASIMods(1));
                    results.AddRange(getInstalledASIMods(2));
                    results.AddRange(getInstalledASIMods(3));
                    return results;
            }
            if (asiDirectory != null)
            {
                if (!Directory.Exists(asiDirectory))
                {
                    Directory.CreateDirectory(asiDirectory);
                    return results; //It won't have anything in it if we are creating it
                }
                var asiFiles = Directory.GetFiles(asiDirectory, "*.asi");
                foreach (var asiFile in asiFiles)
                {
                    results.Add(new InstalledASIMod(asiFile));
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
    }
}
