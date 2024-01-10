using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI.Controls;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.Tools.Sequence_Editor;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorer.Tools.Soundplorer;
using LegendaryExplorer.Tools.FaceFXEditor;
using LegendaryExplorer.Tools.InterpEditor;
using LegendaryExplorer.Tools.AFCCompactorWindow;
using LegendaryExplorer.Tools.AnimationImporterExporter;
using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Tools.ConditionalsEditor;
using LegendaryExplorer.Tools.Meshplorer;
using LegendaryExplorer.Tools.PathfindingEditor;
using LegendaryExplorer.Tools.WwiseEditor;
using LegendaryExplorer.Tools.TFCCompactor;
using LegendaryExplorer.Tools.MountEditor;
using LegendaryExplorer.ToolsetDev;
using LegendaryExplorer.ToolsetDev.MemoryAnalyzer;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using Newtonsoft.Json;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using System.Text;
using LegendaryExplorer.Tools.ClassViewer;
using LegendaryExplorer.Tools.PlotDatabase;
using LegendaryExplorer.Tools.ScriptDebugger;

namespace LegendaryExplorer
{
    public class Tool : DependencyObject
    {
        public string name { get; set; }
        public ImageSource icon { get; set; }
        public Action open { get; set; }
        public List<string> tags;
        public string category { get; set; }
        public string category2 { get; set; }
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
        private const string ICON_COMING_SOON_RES_NAME = "iconPlaceholder";

        //has an invisible no width space at the beginning so it will sort last
        private const string other = "⁣Other";
        private static HashSet<Tool> items;

        private static readonly string FavoritesPath = Path.Combine(AppDirectories.AppDataFolder, "Favorites.JSON");

        public static event EventHandler FavoritesChanged;

        public static IReadOnlyCollection<Tool> Items => items;

        public static void Initialize()
        {
            HashSet<Tool> set = new();

            #region Toolset Devs

#if DEBUG
            set.Add(new Tool
            {
                name = "AutoTOC",
                type = typeof(Tools.AutoTOC.AutoTOCWindow),
                icon = Application.Current.FindResource("iconAutoTOC") as ImageSource,
                open = () =>
                {
                    (new Tools.AutoTOC.AutoTOCWindow()).Show();
                },
                tags = new List<string> { "user", "toc", "tocing", "crash", "infinite", "loop", "loading" },
                category = "Toolset Devs",
                description = "AutoTOC is a tool for ME3 and LE that updates and/or creates the PCConsoleTOC.bin files associated with the base game and each DLC."
            });

            set.Add(new Tool
            {
                name = "Memory Analyzer",
                type = typeof(MemoryAnalyzerUI),
                icon = Application.Current.FindResource("iconMemoryAnalyzer") as ImageSource,
                open = () =>
                {
                    (new MemoryAnalyzerUI()).Show();
                },
                tags = new List<string> { "utility", "toolsetdev" },
                category = "Toolset Devs",
                description = "Memory Analyzer allows you to track references to objects to help trace memory leaks."
            });

            set.Add(new Tool
            {
                name = "File Hex Analyzer",
                type = typeof(FileHexViewer),
                icon = Application.Current.FindResource("iconFileHexAnalyzer") as ImageSource,
                open = () =>
                {
                    (new FileHexViewer()).Show();
                },
                tags = new List<string> { "utility", "toolsetdev", "hex" },
                category = "Toolset Devs",
                description = "File Hex Analyzer is a package hex viewer that shows references in the package hex. It also works with non-package files, but won't show any references, obviously."
            });

            set.Add(new Tool
            {
                name = "PSA Viewer",
                type = typeof(PSAViewerWindow),
                icon = Application.Current.FindResource("iconPlaceholder") as ImageSource,
                open = () =>
                {
                    (new PSAViewerWindow()).Show();
                },
                tags = new List<string> { "utility", "toolsetdev", "animation" },
                category = "Toolset Devs",
                description = "PSA Viewer is a tool for viewing the contents of a PSA file."
            });

            set.Add(new Tool
            {
                name = "SFAR Explorer",
                type = typeof(Tools.SFARExplorer.SFARExplorerWindow),
                icon = Application.Current.FindResource("iconSFARExplorer") as ImageSource,
                open = () =>
                {
                    (new Tools.SFARExplorer.SFARExplorerWindow()).Show();
                },
                tags = new List<string> { "developer", "dlc" },
                category = "Toolset Devs",
                description = "SFAR Explorer allows you to explore and extract ME3 DLC archive files (SFAR).",
            });
#endif
            #endregion

            #region Utilities
            set.Add(new Tool
            {
                name = "Animation Viewer",
                type = typeof(Tools.AnimationViewer.AnimationViewerWindow),
                icon = Application.Current.FindResource("iconAnimViewer") as ImageSource,
                open = () =>
                {
                    if (Tools.AnimationViewer.AnimationViewerWindow.Instance == null)
                    {
                        (new Tools.AnimationViewer.AnimationViewerWindow()).Show();
                    }
                    else
                    {
                        Tools.AnimationViewer.AnimationViewerWindow.Instance.RestoreAndBringToFront();
                    }
                },
                tags = new List<string> { "utility", "animation", "gesture" },
                category = "Cinematic Tools",
                category2 = "Utilities",
                description = "Animation Viewer allows you to preview any animation in Mass Effect 3 (OT only)."
            });

#if DEBUG
            set.Add(new Tool
            {
                name = "Animation Viewer 2",
                type = typeof(Tools.AnimationViewer.AnimationViewerWindow2),
                icon = Application.Current.FindResource("iconAnimViewer") as ImageSource,
                open = () =>
                {
                    var gameStr = InputComboBoxWPF.GetValue(null, "Choose game you want to use Animation Viewer 2 with.", "Live Level Editor 2 game selector",
                        new[] { "LE1", "LE2", /*"LE3"*/ }, "LE2");

                    if (Enum.TryParse(gameStr, out MEGame game))
                    {
                        if (Tools.AnimationViewer.AnimationViewerWindow2.Instance(game) is { } instance)
                        {
                            instance.RestoreAndBringToFront();
                        }
                        else
                        {
                            (new Tools.AnimationViewer.AnimationViewerWindow2(game)).Show();
                        }
                    }
                },
                tags = new List<string> { "utility", "animation", "gesture" },
                category = "Cinematic Tools",
                category2 = "Utilities",
                description = "IN DEVELOPMENT: (LE ONLY) Animation Viewer 2 allows you to preview any animation in the Legendary Edition versions of the games."
            });
#endif
            set.Add(new Tool
            {
                name = "Class Hierarchy Viewer",
                type = typeof(Tools.ClassViewer.ClassViewerWindow),
                icon = Application.Current.FindResource("iconClassViewer") as ImageSource,
                open = () =>
                {
                    var gameStr = InputComboBoxWPF.GetValue(null, "Choose game you want to use Class Hierarchy Viewer with.", "Class Hierarchy Viewer game selector",
                        new[] { "LE1", "LE2", "LE3", "ME1", "ME2", "ME3" }, "LE3");

                    if (Enum.TryParse(gameStr, out MEGame game))
                    {
                        new ClassViewerWindow(game).Show();
                    }
                },
                tags = new List<string> { "utility", "class", "property" },
                category = "Utilities",
                description = "Class Hierarchy Viewer shows you how classes and properties inherit from each other, and where some override."
            });
            set.Add(new Tool
            {
                name = "Live Level Editor",
                type = typeof(Tools.LiveLevelEditor.LiveLevelEditorWindow),
                icon = Application.Current.FindResource("iconLiveLevelEditor") as ImageSource,
                open = () =>
                {
                    var gameStr = InputComboBoxWPF.GetValue(null, "Choose game you want to use Live Level Editor with.", "Live Level Editor game selector",
                                              new[] { "LE3", "LE2", "LE1", "ME3", "ME2" }, "LE3");

                    if (Enum.TryParse(gameStr, out MEGame game))
                    {
                        if (game.IsLEGame())
                        {
                            if (Tools.LiveLevelEditor.LELiveLevelEditorWindow.Instance(game) is { } instance)
                            {
                                instance.RestoreAndBringToFront();
                            }
                            else
                            {
                                (new Tools.LiveLevelEditor.LELiveLevelEditorWindow(game)).Show();
                            }
                        }
                        else
                        {
                            if (Tools.LiveLevelEditor.LiveLevelEditorWindow.Instance(game) is { } instance)
                            {
                                instance.RestoreAndBringToFront();
                            }
                            else
                            {
                                (new Tools.LiveLevelEditor.LiveLevelEditorWindow(game)).Show();
                            }
                        }
                    }
                },
                tags = new List<string> { "utility" },
                category = "Utilities",
                description = "Live Level Editor allows you to preview the effect of property changes to Actors in game, to reduce iteration times."
            });

            set.Add(new Tool
            {
                name = "Script Debugger",
                type = typeof(ScriptDebuggerWindow),
                icon = Application.Current.FindResource("iconScriptDebugger") as ImageSource,
                open = () =>
                {
                    var gameStr = InputComboBoxWPF.GetValue(null, "Choose game you want to use Script Debugger with.", "Script Debugger game selector",
                        new[] { "LE1", "LE2", "LE3" }, "LE3");

                    if (Enum.TryParse(gameStr, out MEGame game))
                    {
                        if (ScriptDebuggerWindow.Instance(game) is { } instance)
                        {
                            instance.RestoreAndBringToFront();
                        }
                        else
                        {
                            (new ScriptDebuggerWindow(game)).Show();
                        }
                    }
                },
                tags = new List<string> { "utility", "unrealscript" },
                category = "Utilities",
                description = "Script Debugger lets you debug your UnrealScript for Legendary Edition games. Set breakpoints, step through code, and inspect and change the values of local and instance variables"
            });
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
                category = "Audio Tools",
                category2 = "Utilities",
                description = "AFC Compactor can compact your ME2 or ME3 Audio File Cache (AFC) files by removing unreferenced chunks. It also can be used to reduce or remove AFC dependencies so users do not have to have DLC installed for certain audio to work.",
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
            //                category = "Debugging",
            //                description = "ASI Manager allows you to install and uninstall ASI mods for all three Mass Effect Trilogy games. ASI mods allow you to run native mods that allow you to do things such as kismet logging or function call monitoring."
            //            });
            set.Add(new Tool
            {
                name = "Audio Localizer",
                type = typeof(Tools.AudioLocalizer.AudioLocalizerWindow),
                icon = Application.Current.FindResource("iconAudioLocalizer") as ImageSource,
                open = () =>
                {
                    (new Tools.AudioLocalizer.AudioLocalizerWindow()).Show();
                },
                tags = new List<string> { "utility", "localization", "LOC_INT", "translation" },
                category = "Audio Tools",
                category2 = "Utilities",
                description = "Audio Localizer allows you to copy the afc offsets and filenames from localized files to your mods LOC_INT files."
            });
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
            //                category = "Extractors + Repackers",
            //                description = "BIK Movie Extractor is a utility for extracting BIK videos from the ME3 Movies.tfc. This file contains small resolution videos played during missions, such as footage of Miranda in Sanctuary.",
            //            });
            set.Add(new Tool
            {
                name = "Coalesced Compiler",
                type = typeof(Tools.CoalescedCompiler.CoalescedCompilerWindow),
                icon = Application.Current.FindResource("iconCoalescedCompiler") as ImageSource,
                open = () =>
                {
                    (new Tools.CoalescedCompiler.CoalescedCompilerWindow()).Show();
                },
                tags = new List<string> { "utility", "coal", "ini", "bin" },
                category = "Extractors + Repackers",
                category2 = "Utilities",
                description = "Coalesced Compiler converts between XML and BIN formats for coalesced files. These are key game files that help control a large amount of content.",
            });
            set.Add(new Tool
            {
                name = "DLC Unpacker",
                type = typeof(Tools.DLCUnpacker.DLCUnpackerWindow),
                icon = Application.Current.FindResource("iconDLCUnpacker") as ImageSource,
                open = () =>
                {
                    if (ME3Directory.DefaultGamePath != null)
                    {
                        new Tools.DLCUnpacker.DLCUnpackerWindow().Show();
                    }
                    else
                    {
                        MessageBox.Show("DLC Unpacker only works with Mass Effect 3.");
                    }
                },
                tags = new List<string> { "utility", "dlc", "sfar", "unpack", "extract" },
                category = "Extractors + Repackers",
                description = "DLC Unpacker allows you to extract Mass Effect 3 OT DLC SFAR files, allowing you to access their contents for modding. This unpacker is based on MEM code, which is very fast and is compatible with the ALOT texture mod.",
            });
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
                category = "Utilities",
                description = "Hex Converter is a utility that converts among floats, signed/unsigned integers, and hex code in big/little endian.",
            });
            set.Add(new Tool
            {
                name = "Interp Editor",
                type = typeof(InterpEditorWindow),
                icon = Application.Current.FindResource("iconInterpEditor") as ImageSource,
                open = () =>
                {
                    (new InterpEditorWindow()).Show();
                },
                tags = new List<string> { "utility", "dialogue", "matinee", "cutscene", "animcutscene", "interpdata" },
                category = "Cinematic Tools",
                description = "Interp Editor is a simplified version of UDK’s Matinee Editor. It loads interpdata objects and displays their children as tracks on a timeline, allowing the user to visualize the game content associated with a specific scene."
            });
#if DEBUG
            set.Add(new Tool
            {
                name = "LEX Custom Files Manager",
                type = typeof(Tools.CustomFilesManager.CustomFilesManagerWindow),
                icon = Application.Current.FindResource("iconLexCustomFileManager") as ImageSource,
                open = () =>
                {
                    (new Tools.CustomFilesManager.CustomFilesManagerWindow()).Show();
                },
                tags = new List<string> { "utility", "startup", "import", "custom", "file", "class", "kismet" },
                category = "Utilities",
                description = "The LEX Custom Files Manager allows you to define custom files for use with the toolset, including specifying mod startup files that can be imported from and files to inventory the classes of on toolset boot."
            });
#endif
            set.Add(new Tool
            {
                name = "Mesh Explorer",
                type = typeof(MeshplorerWindow),
                icon = Application.Current.FindResource("iconMeshplorer") as ImageSource,
                open = () =>
                {
                    (new MeshplorerWindow()).Show();
                },
                tags = new List<string> { "developer", "mesh", "meshplorer" },
                category = "Meshes + Textures",
                description = "Mesh Explorer loads and displays all meshes within a file. The tool skins most meshes with its associated texture. This tool works with all three games."
            });
            set.Add(new Tool
            {
                name = "Animation Importer/Exporter",
                type = typeof(AnimationImporterExporterWindow),
                icon = Application.Current.FindResource("iconAnimationImporter") as ImageSource,
                open = () =>
                {
                    (new AnimationImporterExporterWindow()).Show();
                },
                tags = new List<string> { "developer", "animation", "psa", "animset", "animsequence" },
                category = "Extractors + Repackers",
                description = "Import and Export AnimSequences from/to PSA and UDK"
            });
            set.Add(new Tool
            {
                name = "TLK Editor",
                type = typeof(TLKEditorExportLoader),
                icon = Application.Current.FindResource("iconTLKEditor") as ImageSource,
                open = () =>
                {
                    var elhw = new ExportLoaderHostedWindow(new TLKEditorExportLoader())
                    {
                        Title = $"TLK Editor"
                    };
                    elhw.Show();
                },
                tags = new List<string> { "utility", "dialogue", "subtitle", "text", "string" },
                category = "Core Editors",
                category2 = "Utilities",
                description = "TLK Editor is an editor for localized text, located in TLK files. These files are embedded in package files in Mass Effect 1 and stored externally in Mass Effect 2 and 3.",
            });
            set.Add(new Tool
            {
                name = "Package Dumper",
                type = typeof(Tools.PackageDumper.PackageDumperWindow),
                icon = Application.Current.FindResource("iconPackageDumper") as ImageSource,
                open = () =>
                {
                    (new Tools.PackageDumper.PackageDumperWindow()).Show();
                },
                tags = new List<string> { "utility", "package", "pcc", "text", "dump" },
                category = "Utilities",
                category2 = "Extractors + Repackers",
                description = "Package Dumper is a utility for dumping package information to files that can be searched with tools like GrepWin. Names, Imports, Exports, Properties and more are dumped."
            });
            set.Add(new Tool
            {
                name = "Dialogue Dumper",
                type = typeof(Tools.DialogueDumper.DialogueDumperWindow),
                icon = Application.Current.FindResource("iconDialogueDumper") as ImageSource,
                open = () =>
                {
                    (new Tools.DialogueDumper.DialogueDumperWindow()).Show();
                },
                tags = new List<string> { "utility", "convo", "dialogue", "text", "dump" },
                category = "Utilities",
                category2 = "Extractors + Repackers",
                description = "Dialogue Dumper is a utility for dumping conversation strings from games into an excel file. It shows the actor that spoke the line and which file the line is taken from. It also produces a table of who owns which conversation, for those that the owner is anonymous."
            });
            set.Add(new Tool
            {
                name = "Asset Database",
                type = typeof(AssetDatabaseWindow),
                icon = Application.Current.FindResource("iconAssetDatabase") as ImageSource,
                open = () =>
                {
                    (new AssetDatabaseWindow()).Show();
                },
                tags = new List<string> { "utility", "mesh", "material", "class", "animation" },
                category = "Utilities",
                description = "Scans games and creates a database of classes, animations, materials, textures, particles and meshes. Individual assets can be opened directly from the interface with tools for editing."
            });
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
                category = "Meshes + Textures",
                category2 = "Utilities",
                description = "TFC Compactor can compact your DLC mod TFC file by effectively removing unreferenced chunks and compressing the referenced textures. It can also reduce or remove TFC dependencies so users do not have to have DLC installed for certain textures to work.",
            });
            #endregion

            #region Core Tools
            set.Add(new Tool
            {
                name = "Conditionals Editor",
                type = typeof(ConditionalsEditorWindow),
                icon = Application.Current.FindResource("iconConditionalsEditor") as ImageSource,
                open = () =>
                {
                    (new ConditionalsEditorWindow()).Show();
                },
                tags = new List<string> { "developer", "conditional", "plot", "boolean", "flag", "int", "integer", "cnd" },
                category = "Core Editors",
                description = "Conditionals Editor is used to create and edit ME3/LE3 files with the .cnd extension. CND files control game story by checking for specific combinations of plot events.",
            });
            set.Add(new Tool
            {
                name = "Dialogue Editor",
                type = typeof(DialogueEditor.DialogueEditorWindow),
                icon = Application.Current.FindResource("iconDialogueEditor") as ImageSource,
                open = () =>
                {
                    (new DialogueEditor.DialogueEditorWindow()).Show();
                },
                tags = new List<string> { "developer", "me1", "me2", "me3", "cutscene" },
                category = "Core Editors",
                category2 = "Cinematic Tools",
                description = "Dialogue Editor is a visual tool used to edit in-game conversations for all games.",
            });
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
                category = "Cinematic Tools",
                category2 = "Core Editors",
                description = "FaceFX Editor is the toolset’s highly-simplified version of FaceFX Studio. With this tool modders can edit FaceFX AnimSets (FXEs) for all three games.",
            });
            set.Add(new Tool
            {
                name = "Mount Editor",
                type = typeof(MountEditorWindow),
                icon = Application.Current.FindResource("iconMountEditor") as ImageSource,
                open = () =>
                {
                    new MountEditorWindow().Show();
                },
                tags = new List<string> { "developer", "mount", "dlc", "me2", "me3" },
                category = "Utilities",
                category2 = "Core Editors",
                description = "Mount Editor allows you to create or modify mount.dlc files, which are used in DLC for Mass Effect 2 and Mass Effect 3."
            });
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
                category = "Core Editors",
                category2 = "Utilities",
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
                category = "Core Editors",
                description = "Package Editor is Legendary Explorer's general purpose editing tool for Unreal package files in all games. " +
                              "Edit files in a single window with easy access to external tools such as Curve Editor and Sound Explorer."
            });
            set.Add(new Tool
            {
                name = "Pathfinding Editor",
                type = typeof(PathfindingEditorWindow),
                icon = Application.Current.FindResource("iconPathfindingEditor") as ImageSource,
                open = () =>
                {
                    (new PathfindingEditorWindow()).Show();
                },
                tags = new List<string> { "user", "developer", "path", "ai", "combat", "spline", "spawn", "map", "path", "node", "cover", "level" },
                category = "Core Editors",
                description = "Pathfinding Editor allows you to modify pathing nodes so squadmates and enemies can move around a map. You can also edit placement of several different types of level objects such as StaticMeshes, Splines, CoverSlots, and more.",
            });
            set.Add(new Tool
            {
                name = "Plot Editor",
                type = typeof(Tools.PlotEditor.PlotEditorWindow),
                icon = Application.Current.FindResource("iconPlotEditor") as ImageSource,
                open = () =>
                {
                    var plotEd = new Tools.PlotEditor.PlotEditorWindow();
                    plotEd.Show();
                },
                tags = new List<string> { "developer", "codex", "state transition", "quest", "natives" },
                category = "Core Editors",
                description = "Plot Editor is used to examine, edit, and search plot maps in all 3 games for quests, state events, and codex entries."
            });
            set.Add(new Tool
            {
                name = "Plot Database",
                type = typeof(PlotManagerWindow),
                icon = Application.Current.FindResource("iconPlotDatabase") as ImageSource,
                open = () =>
                {
                    var plotMan = new PlotManagerWindow();
                    plotMan.Show();
                },
                tags = new List<string> { "developer", "codex", "state transition", "quest", "plots", "database", "conditional" },
                category = "Core Editors",
                description = "Plot Database is used to view and create databases of plot elements from all three games. This tool is for reference only, and affects nothing in game."
            });
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
                category = "Core Editors",
                description = "Sequence Editor is the toolset’s version of UDK’s UnrealKismet. With this cross-game tool, users can edit and create new sequences that control gameflow within and across levels.",
            });
            set.Add(new Tool
            {
                name = "Texture Studio",
                type = typeof(Tools.TextureStudio.TextureStudioWindow),
                icon = Application.Current.FindResource("iconTextureStudio") as ImageSource,
                open = () =>
                {
                    (new Tools.TextureStudio.TextureStudioWindow()).Show();
                },
                tags = new List<string> { "texture", "developer", "studio", "graphics" },
                category = "Meshes + Textures",
                description = "THIS TOOL IS NOT COMPLETE AND MAY BREAK MODS. Texture Studio is a tool designed for texture editing files in a directory of files, such as a DLC mod. It is not the same as other tools such as Mass Effect Modder, which is a game wide replacement tool.",
            });
            set.Add(new Tool
            {
                name = "Sound Explorer",
                type = typeof(SoundplorerWPF),
                icon = Application.Current.FindResource("iconSoundplorer") as ImageSource,
                open = () =>
                {
                    (new SoundplorerWPF()).Show();
                },
                tags = new List<string> { "user", "developer", "audio", "dialogue", "music", "wav", "ogg", "sound", "afc", "wwise", "bank", "soundplorer" },
                category = "Audio Tools",
                description = "Extract and play audio from all 3 games, and replace audio directly in Mass Effect 3 and Mass Effect 2 LE.",
            });
            set.Add(new Tool
            {
                name = "Wwise Graph Editor",
                type = typeof(WwiseEditorWindow),
                icon = Application.Current.FindResource("iconWwiseGraphEditor") as ImageSource,
                open = () =>
                {
                    (new WwiseEditorWindow()).Show();
                },
                tags = new List<string> { "developer", "audio", "music", "sound", "wwise", "bank" },
                category = "Audio Tools",
                description = "Wwise Graph Editor currently has no editing functionality. " +
                "It can be used to help visualize the relationships between HIRC objects as well as their connection to WwiseEvent and WwiseStream Exports."
            });
            #endregion

            items = set;

            loadFavorites();
        }

        private static void loadFavorites()
        {
            try
            {
                var favorites = new HashSet<string>(Misc.AppSettings.Settings.MainWindow_Favorites.Split(';'));
                foreach (var tool in items)
                {
                    if (favorites.Contains(tool.name))
                    {
                        tool.IsFavorited = true;
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
                    var favorites = new StringBuilder();
                    foreach (var tool in items)
                    {
                        if (tool.IsFavorited)
                        {
                            favorites.Append(tool.name + ";");
                        }
                    }
                    if (favorites.Length > 0) favorites.Remove(favorites.Length - 1, 1);
                    Misc.AppSettings.Settings.MainWindow_Favorites = favorites.ToString();
                }
                catch
                {
                    return;
                }
            }
        }
    }
}