using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace ME3Explorer
{
    public class Tool
    {
        public string name { get; set; }
        public ImageSource icon { get; set; }
        public Action open { get; set; }
        public List<string> tags;
    }

    public static class Tools
    {
        public static List<Tool> items;

        public static void InitializeTools()
        {
            List<Tool> list = new List<Tool>();
            
            #region Utilities
            list.Add(new Tool
            {
                name = "Audio Extractor",
                icon = Application.Current.FindResource("iconAudioExtractor") as ImageSource,
                open = () =>
                {
                    (new AFCExtract()).Show();
                },
                tags = new List<string> { "utility", "afc", "audio" }
            });
            list.Add(new Tool
            {
                name = "Bik Extractor",
                icon = Application.Current.FindResource("iconBikExtractor") as ImageSource,
                open = () =>
                {
                    (new BIKExtract()).Show();
                },
                tags = new List<string> { "utility", "bik", "movie" }
            });
            list.Add(new Tool
            {
                name = "ME3 Backup Tool",
                icon = Application.Current.FindResource("iconME3BackupTool") as ImageSource,
                open = () =>
                {
                    string loc = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                    if (File.Exists(loc + "\\ME3VanillaMaker.exe"))
                        Process.Start(loc + "\\ME3VanillaMaker.exe");
                },
                tags = new List<string> { "utility", "vanilla" }
            });
            list.Add(new Tool
            {
                name = "Image Engine",
                icon = Application.Current.FindResource("iconImageEngine") as ImageSource,
                open = () =>
                {
                    (new CSharpImageLibrary.MainWindow()).Show();
                },
                tags = new List<string> { "utility", "dds", "texture", "convert" }
            });
            list.Add(new Tool
            {
                name = "Language Selector",
                icon = Application.Current.FindResource("iconLanguageSelector") as ImageSource,
                open = () =>
                {
                    (new Language_Selector()).Show();
                },
                tags = new List<string> { "utility" }
            });
            list.Add(new Tool
            {
                name = "Batch Renamer",
                icon = Application.Current.FindResource("iconBatchRenamer") as ImageSource,
                open = () =>
                {
                    (new batchrenamer.BatchRenamer()).Show();
                },
                tags = new List<string> { "utility" }
            });
            #endregion
            
            #region Install Mods
            list.Add(new Tool
            {
                name = "Asset Explorer",
                icon = Application.Current.FindResource("iconAssetExplorer") as ImageSource,
                open = () =>
                {
                    AssetExplorer assExp = new AssetExplorer();
                    assExp.Show();
                    assExp.LoadMe();
                },
                tags = new List<string> { "user" }
            });
            list.Add(new Tool
            {
                name = "Modmaker",
                icon = Application.Current.FindResource("iconModMaker") as ImageSource,
                open = () =>
                {
                    (new ModMaker()).Show();
                },
                tags = new List<string> { "user" }
            });
            list.Add(new Tool
            {
                name = "Texplorer",
                icon = Application.Current.FindResource("iconTexplorer") as ImageSource,
                open = () =>
                {
                    (new Texplorer2()).Show();
                },
                tags = new List<string> { "user" }
            });
            list.Add(new Tool
            {
                name = "TPF Tools",
                icon = Application.Current.FindResource("iconTPFTools") as ImageSource,
                open = () =>
                {
                    (new KFreonTPFTools3()).Show();
                },
                tags = new List<string> { "user" }
            });
            list.Add(new Tool
            {
                name = "Plot Database",
                icon = Application.Current.FindResource("iconPlotDatabase") as ImageSource,
                open = () =>
                {
                    (new PlotVarDB.PlotVarDB()).Show();
                },
                tags = new List<string> { "user" }
            });
            list.Add(new Tool
            {
                name = "AutoTOC",
                icon = Application.Current.FindResource("iconAutoTOC") as ImageSource,
                open = () =>
                {
                    (new AutoTOC.AutoTOC()).Show();
                },
                tags = new List<string> { "user" }
            });
            list.Add(new Tool
            {
                name = "SFAR TOC Updater",
                icon = Application.Current.FindResource("iconSFARTOCUpdater") as ImageSource,
                open = () =>
                {
                    (new SFARTOCbinUpdater()).Show();
                },
                tags = new List<string> { "user", "dlc" }
            });
            #endregion

            #region Create Mods
            list.Add(new Tool
            {
                name = "Package Editor",
                icon = Application.Current.FindResource("iconPackageEditor") as ImageSource,
                open = () =>
                {
                    PackageEditor pck = new PackageEditor();
                    pck.Show();
                    pck.LoadMostRecent();
                },
                tags = new List<string> { "developer", "pcc" }
            });
            list.Add(new Tool
            {
                name = "PCC Repacker",
                icon = Application.Current.FindResource("iconPCCRepacker") as ImageSource,
                open = () =>
                {
                    (new PCCRepack()).Show();
                },
                tags = new List<string> { "developer" }
            });
            list.Add(new Tool
            {
                name = "ME3 Creator",
                icon = Application.Current.FindResource("iconME3Creator") as ImageSource,
                open = () =>
                {
                    string loc = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                    if (File.Exists(loc + "\\ME3Creator.exe"))
                    {
                        Process.Start(loc + "\\ME3Creator.exe");
                    }
                },
                tags = new List<string> { "developer" }
            });
            list.Add(new Tool
            {
                name = "Sequence Editor",
                icon = Application.Current.FindResource("iconSequenceEditor") as ImageSource,
                open = () =>
                { 
                    (new SequenceEditor()).Show();
                },
                tags = new List<string> { "developer", "kismet" }
            });
            #endregion

            items = list;
        }
    }
}