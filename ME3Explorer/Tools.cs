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

            #region Install Mods
            list.Add(new Tool
            {
                name = "AutoTOC",
                icon = Application.Current.FindResource("iconAutoTOC") as ImageSource,
                open = () =>
                {
                    (new AutoTOC.AutoTOC()).Show();
                },
                tags = new List<string> { "user", "toc", "tocing", "crash", "infinite", "loop", "loading" }
            });
            list.Add(new Tool
            {
                name = "Modmaker",
                icon = Application.Current.FindResource("iconModMaker") as ImageSource,
                open = () =>
                {
                    (new ModMaker()).Show();
                },
                tags = new List<string> { "user", ".mod", "mod", "mesh" }
            });
            list.Add(new Tool
            {
                name = "SFAR TOC Updater",
                icon = Application.Current.FindResource("iconSFARTOCUpdater") as ImageSource,
                open = () =>
                {
                    (new SFARTOCbinUpdater()).Show();
                },
                tags = new List<string> { "user", "dlc", "toc", "tocing" }
            });
            list.Add(new Tool
            {
                name = "TPF Tools",
                icon = Application.Current.FindResource("iconTPFTools") as ImageSource,
                open = () =>
                {
                    (new KFreonTPFTools3()).Show();
                },
                tags = new List<string> { "user", "texture", "tpf", "dds", "bmp", "jpg", "png" }
            });
            #endregion

            #region Utilities
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
                tags = new List<string> { "utlity", "novice", "friendly", "user-friendly" }
            });
            list.Add(new Tool
            {
                name = "Audio Extractor",
                icon = Application.Current.FindResource("iconAudioExtractor") as ImageSource,
                open = () =>
                {
                    (new AFCExtract()).Show();
                },
                tags = new List<string> { "utility", "afc", "music", "ogg", "wav", "sound", "dialogue" }
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
            list.Add(new Tool
            {
                name = "Bik Movie Extractor",
                icon = Application.Current.FindResource("iconBikExtractor") as ImageSource,
                open = () =>
                {
                    (new BIKExtract()).Show();
                },
                tags = new List<string> { "utility", "bik", "movie" }
            });
            list.Add(new Tool
            {
                name = "Class Viewer",
                icon = Application.Current.FindResource("iconClassViewer") as ImageSource,
                open = () =>
                {
                    (new ClassViewer.ClassViewer()).Show();
                },
                tags = new List<string> { "utility", "import" }
            });
            list.Add(new Tool
            {
                name = "Hex Converter",
                icon = Application.Current.FindResource("iconHexConverter") as ImageSource,
                open = () =>
                {
                    string loc = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                    if (File.Exists(loc + "\\HexConverter.exe"))
                        Process.Start(loc + "\\HexConverter.exe");
                },
                tags = new List<string> { "utility", "code", "endian" }
            });
            list.Add(new Tool
            {
                name = "Image Engine",
                icon = Application.Current.FindResource("iconImageEngine") as ImageSource,
                open = () =>
                {
                    (new CSharpImageLibrary.MainWindow()).Show();
                },
                tags = new List<string> { "utility", "texture", "convert", "dds", "bmp", "jpg", "png" }
            });
            list.Add(new Tool
            {
                name = "Interp Viewer",
                icon = Application.Current.FindResource("iconInterpViewer") as ImageSource,
                open = () =>
                {
                    (new InterpEditor.InterpEditor()).Show();
                },
                tags = new List<string> { "utiity", "dialogue", "matinee", "cutscene", "animcutscene" }
            });
            list.Add(new Tool
            {
                name = "Level Database",
                icon = Application.Current.FindResource("iconLevelDatabase") as ImageSource,
                open = () =>
                {
                    (new LevelExplorer.Levelbase()).Show();
                },
                tags = new List<string> { "utility" }
            });
            list.Add(new Tool
            {
                name = "PCC Repacker",
                icon = Application.Current.FindResource("iconPCCRepacker") as ImageSource,
                open = () =>
                {
                    (new PCCRepack()).Show();
                },
                tags = new List<string> { "utility", "compress", "decompress" }
            });
            list.Add(new Tool
            {
                name = "Plot Database",
                icon = Application.Current.FindResource("iconPlotDatabase") as ImageSource,
                open = () =>
                {
                    (new PlotVarDB.PlotVarDB()).Show();
                },
                tags = new List<string> { "utility", "bool", "boolean", "flag", "int", "integer" }
            });
            list.Add(new Tool
            {
                name = "Property Database",
                icon = Application.Current.FindResource("iconPropertyDatabase") as ImageSource,
                open = () =>
                {
                    (new Propertydb.PropertyDB()).Show();
                },
                tags = new List<string> { "utility" }
            });
            list.Add(new Tool
            {
                name = "Property Dumper",
                icon = Application.Current.FindResource("iconPropertyDumper") as ImageSource,
                open = () =>
                {
                    (new Property_Dumper.PropDumper()).Show();
                },
                tags = new List<string> { "utility" }
            });
            list.Add(new Tool
            {
                name = "Property Manager",
                icon = Application.Current.FindResource("iconPropertyManager") as ImageSource,
                open = () =>
                {
                    (new PropertyManager()).Show();
                },
                tags = new List<string> { "utility" }
            });
            list.Add(new Tool
            {
                name = "PSA Viewer",
                icon = Application.Current.FindResource("iconPSAViewer") as ImageSource,
                open = () =>
                {
                    (new PSAViewer()).Show();
                },
                tags = new List<string> { "utility", "mesh", "animation" }
            });
            list.Add(new Tool
            {
                name = "PSK Viewer",
                icon = Application.Current.FindResource("iconPSKViewer") as ImageSource,
                open = () =>
                {
                    (new PSKViewer.PSKViewer()).Show();
                },
                tags = new List<string> { "utility", "mesh" }
            });
            list.Add(new Tool
            {
                name = "ME1 Save Game Editor",
                icon = Application.Current.FindResource("iconSaveGameEditor") as ImageSource,
                open = () =>
                {
                    (new ME1Explorer.SaveGameEditor.SaveEditor()).Show();
                },
                tags = new List<string> { "utility" }
            });
            list.Add(new Tool
            {
                name = "ME1 Save Game Operator",
                icon = Application.Current.FindResource("iconSaveGameOperator") as ImageSource,
                open = () =>
                {
                    (new ME1Explorer.SaveGameOperator.SaveGameOperator()).Show();
                },
                tags = new List<string> { "utility" }
            });
            list.Add(new Tool
            {
                name = "Script Database",
                icon = Application.Current.FindResource("iconScriptDatabase") as ImageSource,
                open = () =>
                {
                    (new ScriptDB.ScriptDB()).Show();
                },
                tags = new List<string> { "utility" }
            });
            list.Add(new Tool
            {
                name = "Subtitle Scanner",
                icon = Application.Current.FindResource("iconSubtitleScanner") as ImageSource,
                open = () =>
                {
                    (new SubtitleScanner.SubtitleScanner()).Show();
                },
                tags = new List<string> { "utility", "dialogue", "text", "line" }
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
                name = "Language Selector",
                icon = Application.Current.FindResource("iconLanguageSelector") as ImageSource,
                open = () =>
                {
                    (new Language_Selector()).Show();
                },
                tags = new List<string> { "utility" }
            });
            #endregion

            #region Create Mods
            list.Add(new Tool
            {
                name = "Animation Explorer",
                icon = Application.Current.FindResource("iconAnimationExplorer") as ImageSource,
                open = () =>
                {
                    (new AnimationExplorer.AnimationExplorer()).Show();
                },
                tags = new List<string> { "developer", "animation", "gesture", "bones" }
            });
            list.Add(new Tool
            {
                name = "Camera Tool",
                icon = Application.Current.FindResource("iconCameraTool") as ImageSource,
                open = () =>
                {
                    (new CameraTool.CamTool()).Show();
                },
                tags = new List<string> { "developer" }
            });
            list.Add(new Tool
            {
                name = "Coalesced Editor",
                icon = Application.Current.FindResource("iconCoalescedEditor") as ImageSource,
                open = () =>
                {
                    (new Coalesced_Editor.CoalEditor()).Show();
                },
                tags = new List<string> { "developer", "coalesced", "ini", "bin" }
            });
            list.Add(new Tool
            {
                name = "Conditionals Editor",
                icon = Application.Current.FindResource("iconConditionalsEditor") as ImageSource,
                open = () =>
                {
                    (new Conditionals()).Show();
                },
                tags = new List<string> { "developer", "conditional", "plot", "boolean", "flag", "int", "integer", "cnd" }
            });
            list.Add(new Tool
            {
                name = "Dialogue Editor",
                icon = Application.Current.FindResource("iconDialogueEditor") as ImageSource,
                open = () =>
                {
                    (new DialogEditor.DialogEditor()).Show();
                },
                tags = new List<string> { "developer", "me1", "me2", "me3", "cutscene" }
            });
            list.Add(new Tool
            {
                name = "FaceFXAnimSet Editor",
                icon = Application.Current.FindResource("iconFaceFXAnimSetEditor") as ImageSource,
                open = () =>
                {
                    (new FaceFXAnimSetEditor.FaceFXAnimSetEditor()).Show();
                },
                tags = new List<string> { "developer", "fxa", "facefx", "lipsync", "fxe", "bones", "animation" }
            });
            list.Add(new Tool
            {
                name = "GUID Cache Editor",
                icon = Application.Current.FindResource("iconGUIDCacheEditor") as ImageSource,
                open = () =>
                {
                    (new GUIDCacheEditor.GUIDCacheEditor()).Show();
                },
                tags = new List<string> { "developer" }
            });
            list.Add(new Tool
            {
                name = "Level Editor",
                icon = Application.Current.FindResource("iconLevelEditor") as ImageSource,
                open = () =>
                {
                    (new LevelExplorer.LevelEditor.Leveleditor()).Show();
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
                tags = new List<string> { "developer", "advanced", "cloning", "import", "export" }
            });
            list.Add(new Tool
            {
                name = "Meshplorer",
                icon = Application.Current.FindResource("iconMeshplorer") as ImageSource,
                open = () =>
                {
                    (new Meshplorer.Meshplorer()).Show();
                },
                tags = new List<string> { "developer", "mesh" }
            });
            list.Add(new Tool
            {
                name = "Meshplorer 2",
                icon = Application.Current.FindResource("iconMeshplorer2") as ImageSource,
                open = () =>
                {
                    (new Meshplorer2.Meshplorer2()).Show();
                },
                tags = new List<string> { "developer", "mesh" }
            });
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
                tags = new List<string> { "developer", "pcc", "cloning", "import", "export", "sfm", "upk", ".u"}
            });
            list.Add(new Tool
            {
                name = "Sequence Editor",
                icon = Application.Current.FindResource("iconSequenceEditor") as ImageSource,
                open = () =>
                { 
                    (new SequenceEditor()).Show();
                },
                tags = new List<string> { "developer", "kismet", "me1", "me2", "me3" }
            });
            list.Add(new Tool
            {
                name = "SFAR Basic Editor",
                icon = Application.Current.FindResource("iconSFARBasicEditor") as ImageSource,
                open = () =>
                {
                    (new SFARBasicEditor()).Show();
                },
                tags = new List<string> { "developer", "dlc" }
            });
            list.Add(new Tool
            {
                name = "SFAR Editor 2",
                icon = Application.Current.FindResource("iconSFAREditor2") as ImageSource,
                open = () =>
                {
                    (new SFAREditor2()).Show();
                },
                tags = new List<string> { "developer", "dlc" }
            });
            list.Add(new Tool
            {
                name = "Soundplorer",
                icon = Application.Current.FindResource("iconSoundplorer") as ImageSource,
                open = () =>
                {
                    (new Soundplorer()).Show();
                },
                tags = new List<string> { "developer", "audio", "dialogue", "music", "wav", "ogg", "sound" }
            });
            list.Add(new Tool
            {
                name = "Texplorer",
                icon = Application.Current.FindResource("iconTexplorer") as ImageSource,
                open = () =>
                {
                    (new Texplorer2()).Show();
                },
                tags = new List<string> { "developer", "texture", "tfc", "scan", "tree" }
            });
            list.Add(new Tool
            {
                name = "ME3 + ME2 TLK Editor",
                icon = Application.Current.FindResource("iconTLKEditor") as ImageSource,
                open = () =>
                {
                    (new TLKEditor()).Show();
                },
                tags = new List<string> { "developer", "dialogue", "subtitle", "text" }
            });
            list.Add(new Tool
            {
                name = "ME1 TLK Editor",
                icon = Application.Current.FindResource("iconTLKEditor") as ImageSource,
                open = () =>
                {
                    (new ME1Explorer.TlkManager()).Show();
                },
                tags = new List<string> { "developer", "dialogue", "subtitle", "text" }
            });
            #endregion

            items = list;
        }
    }
}