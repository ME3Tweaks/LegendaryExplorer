using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.Tools.Sequence_Editor;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorer.Tools.Soundplorer;
using LegendaryExplorer.Tools.FaceFXEditor;
using LegendaryExplorer.Tools.InterpEditor;
using LegendaryExplorer.Tools.AFCCompactorWindow;
using LegendaryExplorer.Tools.TFCCompactor;
using Newtonsoft.Json;
using ME3ExplorerCore.GameFilesystem;
using ME3ExplorerCore.Packages;

namespace LegendaryExplorer
{
    public class Tool : DependencyObject
    {
        public string name { get; set; }
        public ImageSource icon { get; set; }
        public Action open { get; set; }
        public List<string> tags;
        public string subCategory { get; set; }
        public string description { get; set; }
        public Type type { get; set; }
        public bool IsFavorited
        {
            get => (bool)GetValue(IsFavoritedProperty);
            set => SetValue(IsFavoritedProperty, value);
        }

        // Using a DependencyProperty as the backing store for IsFavorited.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsFavoritedProperty =
            DependencyProperty.Register(nameof(IsFavorited), typeof(bool), typeof(Tool), new PropertyMetadata(false, OnIsFavoritedChanged));

        private static void OnIsFavoritedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ToolSet.saveFavorites();
        }
    }

    public static class ToolSet
    {
        //has an invisible no width space at the beginning so it will sort last
        private const string other = "⁣Other";
        private static HashSet<Tool> items;

        private static readonly string FavoritesPath = Path.Combine(AppDirectories.AppDataFolder, "Favorites.JSON");

        public static event EventHandler FavoritesChanged;

        public static IReadOnlyCollection<Tool> Items => items;

        public static void Initialize()
        {
            HashSet<Tool> set = new();

            #region Install Mods
//            set.Add(new Tool
//            {
//                name = "AutoTOC",
//                type = typeof(AutoTOCWPF),
//                icon = Application.Current.FindResource("iconAutoTOC") as ImageSource,
//                open = () =>
//                {
//                    (new AutoTOCWPF()).Show();
//                },
//                tags = new List<string> { "user", "toc", "tocing", "crash", "infinite", "loop", "loading" },
//                description = "AutoTOC is a tool for ME3 that updates and/or creates the PCConsoleTOC.bin files associated with the base game and each DLC.\n\nRunning this tool upon mod installation is imperative to ensuring proper functionality of the game."
//            });
#if DEBUG
//            set.Add(new Tool
//            {
//                name = "Memory Analyzer",
//                type = typeof(ME3ExpMemoryAnalyzer.MemoryAnalyzerUI),
//                icon = Application.Current.FindResource("iconMemoryAnalyzer") as ImageSource,
//                open = () =>
//                {
//                    (new ME3ExpMemoryAnalyzer.MemoryAnalyzerUI()).Show();
//                },
//                tags = new List<string> { "utility", "toolsetdev" },
//                subCategory = "For Toolset Devs Only",
//                description = "Memory Analyzer allows you to track references to objects to help trace memory leaks."
//            });

//            set.Add(new Tool
//            {
//                name = "File Hex Analyzer",
//                type = typeof(FileHexViewerWPF),
//                icon = Application.Current.FindResource("iconFileHexAnalyzer") as ImageSource,
//                open = () =>
//                {
//                    (new FileHexViewer.FileHexViewerWPF()).Show();
//                },
//                tags = new List<string> { "utility", "toolsetdev", "hex" },
//                subCategory = "For Toolset Devs Only",
//                description = "File Hex Analyzer is a package hex viewer that shows references in the package hex. It also works with non-package files, but won't show any references, obviously."
//            });
#endif
            #endregion

            #region Utilities
//            //set.Add(new Tool
//            //{
//            //    name = "Animation Explorer",
//            //    type = typeof(AnimationExplorer.AnimationExplorer),
//            //    icon = Application.Current.FindResource("iconAnimationExplorer") as ImageSource,
//            //    open = () =>
//            //    {
//            //        (new AnimationExplorer.AnimationExplorer()).Show();
//            //    },
//            //    tags = new List<string> { "utility", "animation", "gesture", "bone", "PSA" },
//            //    subCategory = "Explorers",
//            //    description = "Animation Explorer can build a database of all the files containing animtrees and complete animsets in Mass Effect 3. You can import and export Animsets to PSA files."
//            //});
//            set.Add(new Tool
//            {
//                name = "Animation Viewer",
//                type = typeof(AnimationExplorer.AnimationViewer),
//                icon = Application.Current.FindResource("iconAnimViewer") as ImageSource,
//                open = () =>
//                {
//                    if (AnimationExplorer.AnimationViewer.Instance == null)
//                    {
//                        (new AnimationExplorer.AnimationViewer()).Show();
//                    }
//                    else
//                    {
//                        AnimationExplorer.AnimationViewer.Instance.RestoreAndBringToFront();
//                    }
//                },
//                tags = new List<string> { "utility", "animation", "gesture" },
//                subCategory = "Explorers",
//                description = "Animation Viewer allows you to preview any animation in Mass Effect 3"
//            });
//            set.Add(new Tool
//            {
//                name = "Live Level Editor",
//                type = typeof(GameInterop.LiveLevelEditor),
//                icon = Application.Current.FindResource("iconLiveLevelEditor") as ImageSource,
//                open = () =>
//                {
//                    var gameStr = InputComboBoxWPF.GetValue(null, "Choose game you want to use Live Level Editor with.", "Live Level Editor game selector",
//                                              new[] {"ME3", "ME2"}, "ME3");

//                    if (Enum.TryParse(gameStr, out MEGame game))
//                    {
//                        if (GameInterop.LiveLevelEditor.Instance(game) is {} instance)
//                        {
//                            instance.RestoreAndBringToFront();
//                        }
//                        else
//                        {
//                            (new GameInterop.LiveLevelEditor(game)).Show();
//                        }
//                    }
//                },
//                tags = new List<string> { "utility" },
//                subCategory = "Utilities",
//                description = "Live Level Editor allows you to preview the effect of property changes to Actors in game, to reduce iteration times. It also has a Camera Path Editor, which lets you make camera pans quickly."
//            });
            set.Add(new Tool
            {
                name = "AFC Compactor",
                type = typeof(AFCCompactorWindow),
                icon = Application.Current.FindResource("iconAFCCompactor") as ImageSource,
                open = () =>
                {
                    (new AFCCompactorWindow()).Show();
                },
                tags = new List<string> { "utility", "deployment", "audio", },
                subCategory = "Deployment",
                description = "AFC Compactor can compact your ME2 or ME3 Audio File Cache (AFC) files by effectively removing unreferenced chunks in it. It also can be used to reduce or remove AFC dependencies so users do not have to have DLC installed for certain audio to work.",
            });
//            set.Add(new Tool
//            {
//                name = "ASI Manager",
//                type = typeof(ASI.ASIManager),
//                icon = Application.Current.FindResource("iconASIManager") as ImageSource,
//                open = () =>
//                {
//                    (new ASI.ASIManager()).Show();
//                },
//                tags = new List<string> { "utility", "asi", "debug", "log" },
//                subCategory = "Debugging",
//                description = "ASI Manager allows you to install and uninstall ASI mods for all three Mass Effect Trilogy games. ASI mods allow you to run native mods that allow you to do things such as kismet logging or function call monitoring."
//            });
//            set.Add(new Tool
//            {
//                name = "Audio Localizer",
//                type = typeof(AudioLocalizer),
//                icon = Application.Current.FindResource("iconAudioLocalizer") as ImageSource,
//                open = () =>
//                {
//                    (new AudioLocalizer()).Show();
//                },
//                tags = new List<string> { "utility", "localization", "LOC_INT", "translation" },
//                subCategory = "Utilities",
//                description = "Audio Localizer allows you to copy the afc offsets and filenames from localized files to your mods LOC_INT files."
//            });
//            set.Add(new Tool
//            {
//                name = "Bik Movie Extractor",
//                type = typeof(BIKExtract),
//                icon = Application.Current.FindResource("iconBikExtractor") as ImageSource,
//                open = () =>
//                {
//                    (new BIKExtract()).Show();
//                },
//                tags = new List<string> { "utility", "bik", "movie", "bink", "video", "tfc" },
//                subCategory = "Extractors + Repackers",
//                description = "BIK Movie Extractor is a utility for extracting BIK videos from the ME3 Movies.tfc. This file contains small resolution videos played during missions, such as footage of Miranda in Sanctuary.",
//            });
//            set.Add(new Tool
//            {
//                name = "Coalesced Editor",
//                type = typeof(MassEffect3.CoalesceTool.CoalescedEditor),
//                icon = Application.Current.FindResource("iconCoalescedEditor") as ImageSource,
//                open = () =>
//                {
//                    (new MassEffect3.CoalesceTool.CoalescedEditor()).Show();
//                },
//                tags = new List<string> { "utility", "coal", "ini", "bin" },
//                subCategory = "Extractors + Repackers",
//                description = "Coalesced Editor converts between XML and BIN formats for ME3's coalesced files. These are key game files that help control a large amount of content.",
//            });
//            set.Add(new Tool
//            {
//                name = "DLC Unpacker",
//                type = typeof(DLCUnpacker.DLCUnpackerUI),
//                icon = Application.Current.FindResource("iconDLCUnpacker") as ImageSource,
//                open = () =>
//                {
//                    if (ME3Directory.DefaultGamePath != null)
//                    {
//                        new DLCUnpacker.DLCUnpackerUI().Show();
//                    }
//                    else
//                    {
//                        MessageBox.Show("DLC Unpacker only works with Mass Effect 3.");
//                    }
//                },
//                tags = new List<string> { "utility", "dlc", "sfar", "unpack", "extract" },
//                subCategory = "Extractors + Repackers",
//                description = "DLC Unpacker allows you to extract Mass Effect 3 DLC SFAR files, allowing you to access their contents for modding.\n\nThis unpacker is based on MEM code, which is very fast and is compatible with the ALOT texture mod.",
//            });
            set.Add(new Tool
            {
                name = "Hex Converter",
                icon = Application.Current.FindResource("iconHexConverter") as ImageSource,
                open = () =>
                {
                    if (File.Exists(AppDirectories.HexConverterPath))
                    {
                        Process.Start(AppDirectories.HexConverterPath);
                    }
                    else
                    {
                        new HexConverter.MainWindow().Show();
                    }
                },
                tags = new List<string> { "utility", "code", "endian", "convert", "integer", "float" },
                subCategory = "Converters",
                description = "Hex Converter is a utility that converts among floats, signed/unsigned integers, and hex code in big/little endian.",
            });
            set.Add(new Tool
            {
                name = "Interp Editor",
                type = typeof(InterpEditorWindow),
                icon = Application.Current.FindResource("iconInterpViewer") as ImageSource,
                open = () =>
                {
                    (new InterpEditorWindow()).Show();
                },
                tags = new List<string> { "utility", "dialogue", "matinee", "cutscene", "animcutscene", "interpdata" },
                subCategory = "Explorers",
                description = "Interp Editor is a simplified version of UDK’s Matinee Editor. It loads interpdata objects and displays their children as tracks on a timeline, allowing the user to visualize the game content associated with a specific scene."
            });
            //            set.Add(new Tool
            //            {
            //                name = "Meshplorer",
            //                type = typeof(MeshplorerWPF),
            //                icon = Application.Current.FindResource("iconMeshplorer") as ImageSource,
            //                open = () =>
            //                {
            //                    (new MeshplorerWPF()).Show();
            //                },
            //                tags = new List<string> { "developer", "mesh" },
            //                subCategory = "Meshes + Textures",
            //                description = "Meshplorer loads and displays all meshes within a file. The tool skins most meshes with its associated texture.\n\nThis tool works with all three games."
            //            });
            //            set.Add(new Tool
            //            {
            //                name = "Animation Importer/Exporter",
            //                type = typeof(AnimationImporter),
            //                icon = Application.Current.FindResource("iconAnimationImporter") as ImageSource,
            //                open = () =>
            //                {
            //                    (new AnimationImporter()).Show();
            //                },
            //                tags = new List<string> { "developer", "animation", "psa", "animset", "animsequence" },
            //                subCategory = "Scene Shop",
            //                description = "Import and Export AnimSequences from/to PSA and UDK"
            //            });
            //            set.Add(new Tool
            //            {
            //                name = "TLK Editor",
            //                type = typeof(ExportLoaderHostedWindow),
            //                icon = Application.Current.FindResource("iconTLKEditor") as ImageSource,
            //                open = () =>
            //                {
            //                    ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new ME1TlkEditor.ME1TlkEditorWPF());
            //                    elhw.Title = $"TLK Editor";
            //                    elhw.Show();
            //                },
            //                tags = new List<string> { "utility", "dialogue", "subtitle", "text", "strin'" },
            //                subCategory = "Extractors + Repackers",
            //                description = "TLK Editor is an editor for localized text, located in TLK files. These files are embedded in package files in Mass Effect 1 and stored externally in Mass Effect 2 and 3.",
            //            });
            //            set.Add(new Tool
            //            {
            //                name = "ME3 + ME2 TLK Editor",
            //                type = typeof(TLKEditor),
            //                icon = Application.Current.FindResource("iconTLKEditorME23") as ImageSource,
            //                open = () =>
            //                {
            //                    (new TLKEditor()).Show();
            //                },
            //                tags = new List<string> { "utility", "dialogue", "subtitle", "text" },
            //                subCategory = "Extractors + Repackers",
            //                description = "TLK Editor converts between XML and TLK formats, allowing users to edit the display of all game text in ME2 and ME3. Edits to XML files must be done in an external editor, like Notepad++.",
            //            });
            //            set.Add(new Tool
            //            {
            //                name = "Package Dumper",
            //                type = typeof(PackageDumper.PackageDumper),
            //                icon = Application.Current.FindResource("iconPackageDumper") as ImageSource,
            //                open = () =>
            //                {
            //                    (new PackageDumper.PackageDumper()).Show();
            //                },
            //                tags = new List<string> { "utility", "package", "pcc", "text", "dump" },
            //                subCategory = "Utilities",
            //                description = "Package Dumper is a utility for dumping package information to files that can be searched with tools like GrepWin. Names, Imports, Exports, Properties and more are dumped."
            //            });
            //            set.Add(new Tool
            //            {
            //                name = "Dialogue Dumper",
            //                type = typeof(DialogueDumper.DialogueDumper),
            //                icon = Application.Current.FindResource("iconDialogueDumper") as ImageSource,
            //                open = () =>
            //                {
            //                    (new DialogueDumper.DialogueDumper()).Show();
            //                },
            //                tags = new List<string> { "utility", "convo", "dialogue", "text", "dump" },
            //                subCategory = "Utilities",
            //                description = "Dialogue Dumper is a utility for dumping conversation strings from games into an excel file. It shows the actor that spoke the line and which file the line is taken from. It also produces a table of who owns which conversation, for those that the owner is anonymous."
            //            });
            //            set.Add(new Tool
            //            {
            //                name = "Plot Database",
            //                type = typeof(PlotVarDB.PlotVarDB),
            //                icon = Application.Current.FindResource("iconPlotDatabase") as ImageSource,
            //                open = () =>
            //                {
            //                    (new PlotVarDB.PlotVarDB()).Show();
            //                },
            //                tags = new List<string> { "utility", "bool", "boolean", "flag", "int", "integer", "id" },
            //                subCategory = "Databases",
            //                description = "Plot Database is a cross-game utility used to store plot IDs for reference. The tool comes pre-loaded with a default .db file that can be customized by the user. Never look up a plot bool or integer again!",
            //            });
            //            set.Add(new Tool
            //            {
            //                name = "Asset Database",
            //                type = typeof(AssetDatabase.AssetDB),
            //                icon = Application.Current.FindResource("iconAssetDatabase") as ImageSource,
            //                open = () =>
            //                {
            //                    (new AssetDatabase.AssetDB()).Show();
            //                },
            //                tags = new List<string> { "utility", "mesh", "material", "class", "animation" },
            //                subCategory = "Databases",
            //                description = "Scans games and creates a database of classes, animations, materials, textures, particles and meshes.\n\nIndividual assets can be opened directly from the interface with tools for editing."
            //            });
            //            set.Add(new Tool
            //            {
            //                name = "PSA Viewer",
            //                type = typeof(PSAViewer),
            //                icon = Application.Current.FindResource("iconPSAViewer") as ImageSource,
            //                open = () =>
            //                {
            //                    (new PSAViewer()).Show();
            //                },
            //                tags = new List<string> { "utility", "mesh", "animation" },
            //                subCategory = "Explorers",
            //                description = "View the data contained in a PSA animation file extracted using Gildor's umodel toolkit."
            //            });
            //            set.Add(new Tool
            //            {
            //                name = "Script Database",
            //                type = typeof(ScriptDB.ScriptDB),
            //                icon = Application.Current.FindResource("iconScriptDatabase") as ImageSource,
            //                open = () =>
            //                {
            //                    (new ScriptDB.ScriptDB()).Show();
            //                },
            //                tags = new List<string> { "utility", "unreal" },
            //                subCategory = "Databases",
            //                description = "Script Database is used to locate UnrealScript exports across multiple files for ME3. This tool is deprecated and is no longer supported.",
            //            });
            set.Add(new Tool
            {
                name = "TFC Compactor",
                type = typeof(TFCCompactorWindow),
                icon = Application.Current.FindResource("iconTFCCompactor") as ImageSource,
                open = () =>
                {
                    (new TFCCompactorWindow()).Show();
                },
                tags = new List<string> { "utility", "deployment", "textures", "compression" },
                subCategory = "Deployment",
                description = "TFC Compactor can compact your ME2 or ME3 DLC mod TFC file by effectively removing unreferenced chunks in it and compressing the referenced textures. It also can be used to reduce or remove TFC dependencies so users do not have to have DLC installed for certain textures to work.",
            });
            #endregion

            #region Create Mods
            //            set.Add(new Tool
            //            {
            //                name = "Conditionals Editor",
            //                type = typeof(Conditionals),
            //                icon = Application.Current.FindResource("iconConditionalsEditor") as ImageSource,
            //                open = () =>
            //                {
            //                    (new Conditionals()).Show();
            //                },
            //                tags = new List<string> { "developer", "conditional", "plot", "boolean", "flag", "int", "integer", "cnd" },
            //                subCategory = "Core",
            //                description = "Conditionals Editor is used to create and edit ME3 files with the .cnd extension. CND files control game story by checking for specific combinations of plot events.",
            //            });
            //            set.Add(new Tool
            //            {
            //                name = "Curve Editor",
            //                type = typeof(CurveEd.CurveEditor),
            //                icon = Application.Current.FindResource("iconPlaceholder") as ImageSource,
            //                tags = new List<string>(),
            //                subCategory = other,
            //            });
            //            set.Add(new Tool
            //            {
            //                name = "Dialogue Editor",
            //                type = typeof(Dialogue_Editor.DialogueEditorWPF),
            //                icon = Application.Current.FindResource("iconDialogueEditor") as ImageSource,
            //                open = () =>
            //                {
            //                    (new Dialogue_Editor.DialogueEditorWPF()).Show();
            //                },
            //                tags = new List<string> { "developer", "me1", "me2", "me3", "cutscene" },
            //                subCategory = "Scene Shop",
            //                description = "Dialogue Editor is a visual tool used to edit in-game conversations. It works with all the games.",
            //            });
            set.Add(new Tool
            {
                name = "FaceFX Editor",
                type = typeof(FaceFXEditorWindow),
                icon = Application.Current.FindResource("iconFaceFXEditor") as ImageSource,
                open = () =>
                {
                    (new FaceFXEditorWindow()).Show();
                },
                tags = new List<string> { "developer", "fxa", "facefx", "lipsync", "fxe", "bones", "animation", "me3", "me3" },
                subCategory = "Scene Shop",
                description = "FaceFX Editor is the toolset’s highly-simplified version of FaceFX Studio. With this tool modders can edit FaceFX AnimSets (FXEs) for all three games.",
            });
            //            //Benji's tool. Uncomment when we have more progress.
            //            /*set.Add(new Tool
            //            {
            //                name = "Level Explorer",
            //                type = typeof(LevelExplorer.LevelExplorer),
            //                icon = Application.Current.FindResource("iconLevelEditor") as ImageSource,
            //                open = () =>
            //                {
            //                    (new LevelExplorer.LevelExplorer()).Show();
            //                },
            //                tags = new List<string> { "developer" },
            //                subCategory = other,
            //                description = "Level Explorer allows you to view the meshes of a level.",

            //            });*/
            //            set.Add(new Tool
            //            {
            //                name = "Mount Editor",
            //                type = typeof(MountEditor.MountEditorWPF),
            //                icon = Application.Current.FindResource("iconMountEditor") as ImageSource,
            //                open = () =>
            //                {
            //                    new MountEditor.MountEditorWPF().Show();
            //                },
            //                tags = new List<string> { "developer", "mount", "dlc", "me2", "me3" },
            //                subCategory = "Core",
            //                description = "Mount Editor allows you to create or modify mount.dlc files, which are used in DLC for Mass Effect 2 and Mass Effect 3."
            //            });
            set.Add(new Tool
            {
                name = "TLK Manager",
                type = typeof(TLKManagerWPF),
                icon = Application.Current.FindResource("iconTLKManager") as ImageSource,
                open = () =>
                {
                    new TLKManagerWPF().Show();
                },
                tags = new List<string> { "developer", "dialogue", "subtitle", "text", "string", "localize", "language" },
                subCategory = "Core",
                description = "TLK Manager manages loaded TLK files that are used to display string data in editor tools. You can also use it to extract and recompile TLK files."
            });
            set.Add(new Tool
            {
                name = "Package Editor",
                type = typeof(PackageEditorWindow),
                icon = Application.Current.FindResource("iconPackageEditor") as ImageSource,
                open = () =>
                {
                    new PackageEditorWindow().Show();
                },
                tags = new List<string> { "user", "developer", "pcc", "cloning", "import", "export", "sfm", "upk", ".u", "me2", "me1", "me3", "name" },
                subCategory = "Core",
                description = "Package Editor is ME3Explorer's general purpose editing tool for unreal package files. It can edit ME3 and ME2 .pcc files, as well as ME1 .sfm, .upk, and .u files." +
                              "\n\nEdit trilogy game files in a single window with access to external tools such as Curve Editor and Soundplorer, right in the same window."
            });
//            set.Add(new Tool
//            {
//                name = "Pathfinding Editor",
//                type = typeof(PathfindingEditorWPF),
//                icon = Application.Current.FindResource("iconPathfindingEditor") as ImageSource,
//                open = () =>
//                {
//                    (new PathfindingEditorWPF()).Show();
//                },
//                tags = new List<string> { "user", "developer", "path", "ai", "combat", "spline", "spawn", "map", "path", "node", "cover", "level" },
//                subCategory = "Core",
//                description = "Pathfinding Editor allows you to modify pathing nodes so squadmates and enemies can move around a map. You can also edit placement of several different types of level objects such as StaticMeshes, Splines, CoverSlots, and more.",
//            });
//            set.Add(new Tool
//            {
//                name = "Plot Editor",
//                type = typeof(MassEffect.NativesEditor.Views.PlotEditor),
//                icon = Application.Current.FindResource("iconPlotEditor") as ImageSource,
//                open = () =>
//                {
//                    var plotEd = new MassEffect.NativesEditor.Views.PlotEditor();
//                    plotEd.Show();
//                },
//                tags = new List<string> { "developer", "codex", "state transition", "quest", "natives" },
//                subCategory = "Core",
//                description = "Plot Editor is used to examine, edit, and search ME3's plot maps for quests, state events, and codices."
//            });
            set.Add(new Tool
            {
                name = "Sequence Editor",
                type = typeof(SequenceEditorWPF),
                icon = Application.Current.FindResource("iconSequenceEditor") as ImageSource,
                open = () =>
                {
                    (new SequenceEditorWPF()).Show();
                },
                tags = new List<string> { "user", "developer", "kismet", "me1", "me2", "me3" },
                subCategory = "Core",
                description = "Sequence Editor is the toolset’s version of UDK’s UnrealKismet. With this cross-game tool, users can edit and create new sequences that control gameflow within and across levels.",
            });
            //            set.Add(new Tool
            //            {
            //                name = "Texture Studio",
            //                type = typeof(TextureStudioUI),
            //                icon = Application.Current.FindResource("iconTextureStudio") as ImageSource,
            //                open = () =>
            //                {
            //                    (new TextureStudioUI()).Show();
            //                },
            //                tags = new List<string> { "texture", "developer", "studio", "graphics" },
            //                subCategory = "Meshes + Textures",
            //                description = "Texture Studio is a tool designed for texture editing files in a directory of files, such as a DLC mod. It is not the same as other tools such as Mass Effect Modder, which is a game wide replacement tool.",
            //            });
            //            set.Add(new Tool
            //            {
            //                name = "SFAR Explorer",
            //                type = typeof(SFARExplorer),
            //                icon = Application.Current.FindResource("iconSFARExplorer") as ImageSource,
            //                open = () =>
            //                {
            //                    (new SFARExplorer()).Show();
            //                },
            //                tags = new List<string> { "developer", "dlc" },
            //                subCategory = other,
            //                description = "SFAR Explorer allows you to explore and extract ME3 DLC archive files (SFAR).",
            //            });
            set.Add(new Tool
            {
                name = "Soundplorer",
                type = typeof(SoundplorerWPF),
                icon = Application.Current.FindResource("iconSoundplorer") as ImageSource,
                open = () =>
                {
                    (new SoundplorerWPF()).Show();
                },
                tags = new List<string> { "user", "developer", "audio", "dialogue", "music", "wav", "ogg", "sound", "afc", "wwise", "bank" },
                subCategory = "Scene Shop",
                description = "Extract and play audio from all 3 games, and replace audio directly in Mass Effect 3.",
            });
            //            set.Add(new Tool
            //            {
            //                name = "Wwise Graph Editor",
            //                type = typeof(WwiseEditor.WwiseEditorWPF),
            //                icon = Application.Current.FindResource("iconWwiseEditor") as ImageSource,
            //                open = () =>
            //                {
            //                    (new WwiseEditor.WwiseEditorWPF()).Show();
            //                },
            //                tags = new List<string> { "developer", "audio", "music", "sound", "wwise", "bank" },
            //                subCategory = "Scene Shop",
            //                description = "Wwise Editor currently has no editing functionality. " +
            //                "It can be used to help visualize the relationships between HIRC objects as well as their connection to WwiseEvent and WwiseStream Exports. " +
            //                "There are many relationships not shown, due to most HIRC objects not being parsed yet.",
            //            });
            #endregion

            items = set;

            loadFavorites();
        }

        private static void loadFavorites()
        {
            try
            {
                if (File.Exists(FavoritesPath))
                {
                    string raw = File.ReadAllText(FavoritesPath);
                    var favorites = JsonConvert.DeserializeObject<HashSet<string>>(raw);
                    foreach (var tool in items)
                    {
                        if (favorites.Contains(tool.name))
                        {
                            tool.IsFavorited = true;
                        }
                    }
                }
            }
            catch
            {
                return;
            }
        }

        public static void saveFavorites()
        {

            if (FavoritesChanged != null)
            {
                FavoritesChanged.Invoke(null, EventArgs.Empty);
                try
                {
                    var favorites = new HashSet<string>();
                    foreach (var tool in items)
                    {
                        if (tool.IsFavorited)
                        {
                            favorites.Add(tool.name);
                        }
                    }
                    string file = JsonConvert.SerializeObject(favorites);
                    File.WriteAllText(FavoritesPath, file);
                }
                catch
                {
                    return;
                }
            }
        }
    }
}