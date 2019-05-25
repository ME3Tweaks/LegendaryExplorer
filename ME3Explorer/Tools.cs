using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using KFreonLib.MEDirectories;
using ME3Explorer.Sequence_Editor;
using ME3Explorer.SharedUI;
using ME3Explorer.Pathfinding_Editor;
using Newtonsoft.Json;
using ME3Explorer.AutoTOC;

namespace ME3Explorer
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
            Tools.saveFavorites();
        }
    }

    public static class Tools
    {
        //has an invisible no width space at the beginning so it will sort last
        private const string other = "⁣Other";
        private static HashSet<Tool> items;

        private static readonly string FavoritesPath = Path.Combine(App.AppDataFolder, "Favorites.JSON");

        public static event EventHandler FavoritesChanged;

        public static IReadOnlyCollection<Tool> Items => items;

        public static void Initialize()
        {
            HashSet<Tool> set = new HashSet<Tool>();

            #region Install Mods
            set.Add(new Tool
            {
                name = "AutoTOC",
                type = typeof(AutoTOCWPF),
                icon = Application.Current.FindResource("iconAutoTOC") as ImageSource,
                open = () =>
                {
                    (new AutoTOCWPF()).Show();
                },
                tags = new List<string> { "user", "toc", "tocing", "crash", "infinite", "loop", "loading" },
                description = "AutoTOC WPF is a tool for ME3 that updates and/or creates the PCConsoleTOC.bin files associated with the base game and each DLC.\n\nRunning this tool upon mod installation is imperative to ensuring proper functionality of the game."
            });
            set.Add(new Tool
            {
                name = "Mod Maker",
                type = typeof(ModMaker),
                icon = Application.Current.FindResource("iconModMaker") as ImageSource,
                open = () =>
                {
                    (new ModMaker()).Show();
                },
                tags = new List<string> { "utility", ".mod", "mod", "mesh" },
                subCategory = "Mod Packagers",
                description = "MOD MAKER IS UNSUPPORTED IN ME3EXPLORER ME3TWEAKS FORK\n\nMod Maker is used to create and install files with the \".mod\" extension. MOD files are compatible with ME3 and may be packaged with meshes and other game resources."
            });
#if DEBUG
            set.Add(new Tool
            {
                name = "Memory Analyzer",
                type = typeof(ME3ExpMemoryAnalyzer.MemoryAnalyzer),
                icon = Application.Current.FindResource("iconMemoryAnalyzer") as ImageSource,
                open = () =>
                {
                    (new ME3ExpMemoryAnalyzer.MemoryAnalyzer()).Show();
                },
                tags = new List<string> { "utility", "toolsetdev" },
                subCategory = "For Toolset Devs Only",
                description = "Memory Analyzer allows you to track references to objects to help trace memory leaks."
            });

            set.Add(new Tool
            {
                name = "File Hex Analyzer",
                type = typeof(ME3ExpMemoryAnalyzer.MemoryAnalyzer),
                icon = Application.Current.FindResource("iconFileHexAnalyzer") as ImageSource,
                open = () =>
                {
                    (new FileHexViewer.FileHexViewerWPF()).Show();
                },
                tags = new List<string> { "utility", "toolsetdev", "hex" },
                subCategory = "For Toolset Devs Only",
                description = "File Hex Analyzer is a package hex viewer that shows references in the package hex. It also works with non-package files, but won't show any references, obviously."
            });
#endif
            set.Add(new Tool
            {
                name = "TPF Tools",
                type = typeof(KFreonTPFTools3),
                icon = Application.Current.FindResource("iconTPFTools") as ImageSource,
                open = () =>
                {
                    (new KFreonTPFTools3()).Show();
                },
                tags = new List<string> { "utility", "texture", "tpf", "dds", "bmp", "jpg", "png" },
                subCategory = "Mod Packagers",
                description = "TPF TOOLS IS UNSUPPORTED IN ME3EXPLORER ME3TWEAKS FORK\n\nTPF Tools allows for permanent insertion of textures into game files. TPF tools can also be used by modders to package textures into TPFs for distribution.\n\nThis tool has been mostly superceded by Mass Effect Modder (MEM)."
            });
            #endregion

            #region Utilities
            set.Add(new Tool
            {
                name = "Animation Explorer",
                type = typeof(AnimationExplorer.AnimationExplorer),
                icon = Application.Current.FindResource("iconAnimationExplorer") as ImageSource,
                open = () =>
                {
                    (new AnimationExplorer.AnimationExplorer()).Show();
                },
                tags = new List<string> { "utility", "animation", "gesture", "bone" },
                subCategory = "Explorers",
                description = "Animation Explorer can build a database of all the files containing animtrees and complete animsets in Mass Effect 3. You can import and export Animsets to PSA files."
            });
            set.Add(new Tool
            {
                name = "Bik Movie Extractor",
                type = typeof(BIKExtract),
                icon = Application.Current.FindResource("iconBikExtractor") as ImageSource,
                open = () =>
                {
                    (new BIKExtract()).Show();
                },
                tags = new List<string> { "utility", "bik", "movie" },
                subCategory = "Extractors + Repackers",
                description = "BIK Movie Extractor is a utility for extracting BIK videos from the ME3 Movies.tfc. This file contains small resolution videos played during missions, such as footage of Miranda in Sanctuary.",
            });
            set.Add(new Tool
            {
                name = "Coalesced Editor",
                type = typeof(MassEffect3.CoalesceTool.CoalescedEditor),
                icon = Application.Current.FindResource("iconCoalescedEditor") as ImageSource,
                open = () =>
                {
                    (new MassEffect3.CoalesceTool.CoalescedEditor()).Show();
                },
                tags = new List<string> { "utility", "coal", "ini", "bin" },
                subCategory = "Extractors + Repackers",
                description = "Coalesced Editor converts between XML and BIN formats for ME3's coalesced files. These are key game files that help control a large amount of content.",
            });
            set.Add(new Tool
            {
                name = "DLC Unpacker",
                type = typeof(DLCUnpacker.DLCUnpacker),
                icon = Application.Current.FindResource("iconDLCUnpacker") as ImageSource,
                open = () =>
                {
                    if (ME3Directory.gamePath != null)
                    {
                        new DLCUnpacker.DLCUnpacker().Show();
                    }
                    else
                    {
                        MessageBox.Show("DLC Unpacker only works with Mass Effect 3.");
                    }
                },
                tags = new List<string> { "utility", "dlc", "sfar", "unpack", "extract" },
                subCategory = "Extractors + Repackers",
                description = "DLC Unpacker allows you to extract Mass Effect 3 DLC SFAR files, allowing you to access their contents for modding.\n\nThis unpacker is based on MEM code, which is very fast and is compatible with the ALOT texture mod.",
            });
            set.Add(new Tool
            {
                name = "Hex Converter",
                //type = typeof(HexConverter.Hexconverter),
                icon = Application.Current.FindResource("iconHexConverter") as ImageSource,
                open = () =>
                {
                    if (File.Exists(App.HexConverterPath))
                        Process.Start(App.HexConverterPath);
                },
                tags = new List<string> { "utility", "code", "endian", "convert", "integer", "float" },
                subCategory = "Converters",
                description = "Hex Converter is a utility that converts among floats, signed/unsigned integers, and hex code in big/little endian.",
            });
            set.Add(new Tool
            {
                name = "Image Engine",
                type = typeof(CSharpImageLibrary.MainWindow),
                icon = Application.Current.FindResource("iconImageEngine") as ImageSource,
                open = () =>
                {
                    (new CSharpImageLibrary.MainWindow()).Show();
                },
                tags = new List<string> { "utility", "texture", "convert", "dds", "bmp", "jpg", "png" },
                subCategory = "Converters",
                description = "Image Engine is a texture conversion utility. It supports BMP, JPG, PNG, TGA files, as well as a variety of DDS formats and compressions. Modification to mipmaps are also supported.",
            });
            set.Add(new Tool
            {
                name = "Interp Viewer",
                type = typeof(Matinee.InterpEditor),
                icon = Application.Current.FindResource("iconInterpViewer") as ImageSource,
                open = () =>
                {
                    (new Matinee.InterpEditor()).Show();
                },
                tags = new List<string> { "utility", "dialogue", "matinee", "cutscene", "animcutscene", "interpdata" },
                subCategory = "Explorers",
                description = "Interp Viewer is a simplified version of UDK’s Matinee Editor. It loads interpdata objects and displays their children as tracks on a timeline, allowing the user to visualize the game content associated with a specific scene.\n\nAttention: This tool is a utility; editing is not yet supported."
            });
            set.Add(new Tool
            {
                name = "Meshplorer",
                type = typeof(Meshplorer.Meshplorer),
                icon = Application.Current.FindResource("iconMeshplorer") as ImageSource,
                open = () =>
                {
                    (new Meshplorer.Meshplorer()).Show();
                },
                tags = new List<string> { "developer", "mesh" },
                subCategory = "Meshes + Textures",
                description = "Meshplorer loads and displays all meshes within a file. The tool skins most meshes with its associated texture.\n\nThis tool only works with Mass Effect 3.",
            });
            set.Add(new Tool
            {
                name = "ME3 + ME2 TLK Editor",
                type = typeof(TLKEditor),
                icon = Application.Current.FindResource("iconTLKEditorME23") as ImageSource,
                open = () =>
                {
                    (new TLKEditor()).Show();
                },
                tags = new List<string> { "utility", "dialogue", "subtitle", "text" },
                subCategory = "Extractors + Repackers",
                description = "TLK Editor converts between XML and TLK formats, allowing users to edit the display of all game text in ME2 and ME3. Edits to XML files must be done in an external editor, such as Notepad++.",
            });
            set.Add(new Tool
            {
                name = "Package Dumper",
                type = typeof(PackageDumper.PackageDumper),
                icon = Application.Current.FindResource("iconPackageDumper") as ImageSource,
                open = () =>
                {
                    (new PackageDumper.PackageDumper()).Show();
                },
                tags = new List<string> { "utility", "package", "pcc", "text", "dump" },
                subCategory = "Utilities",
                description = "Package Dumper is a utility for dumping package information to files that can be searched with tools like GrepWin. Names, Imports, Exports, Properties and more are dumped."
            });
            set.Add(new Tool
            {
                name = "Dialogue Dumper",
                type = typeof(DialogueDumper.DialogueDumper),
                icon = Application.Current.FindResource("iconDialogueDumper") as ImageSource,
                open = () =>
                {
                    (new DialogueDumper.DialogueDumper()).Show();
                },
                tags = new List<string> { "utility", "convo", "dialogue", "text", "dump" },
                subCategory = "Utilities",
                description = "Dialogue Dumper is a utility for dumping conversation strings from games into an excel file. It shows the actor that spoke the line and which file the line is taken from. It also produces a table of who owns which conversation, for those that the owner is anonymous."
            });
            set.Add(new Tool
            {
                name = "Plot Database",
                type = typeof(PlotVarDB.PlotVarDB),
                icon = Application.Current.FindResource("iconPlotDatabase") as ImageSource,
                open = () =>
                {
                    (new PlotVarDB.PlotVarDB()).Show();
                },
                tags = new List<string> { "utility", "bool", "boolean", "flag", "int", "integer", "id" },
                subCategory = "Databases",
                description = "Plot Database is a cross-game utility used to store plot IDs for reference. The tool comes pre-loaded with a default .db file that can be customized by the user. Never look up a plot bool or integer again!",
            });
            set.Add(new Tool
            {
                name = "Property Database",
                type = typeof(Propertydb.PropertyDB),
                icon = Application.Current.FindResource("iconPropertyDatabase") as ImageSource,
                open = () =>
                {
                    (new Propertydb.PropertyDB()).Show();
                },
                tags = new List<string> { "utility" },
                subCategory = "Databases",
                description = "Scans ME3 and creates a database of all the classes and properties for those classes that Bioware uses.\n\nThis is different than Package Dumper, as it looks across all instances of the class and what is actually used."
            });
            set.Add(new Tool
            {
                name = "PSA Viewer",
                type = typeof(PSAViewer),
                icon = Application.Current.FindResource("iconPSAViewer") as ImageSource,
                open = () =>
                {
                    (new PSAViewer()).Show();
                },
                tags = new List<string> { "utility", "mesh", "animation" },
                subCategory = "Explorers",
                description = "View the data contained in a PSA animation file extracted using Gildor's umodel toolkit."
            });
            set.Add(new Tool
            {
                name = "PSK Viewer",
                type = typeof(PSKViewer.PSKViewer),
                icon = Application.Current.FindResource("iconPSKViewer") as ImageSource,
                open = () =>
                {
                    (new PSKViewer.PSKViewer()).Show();
                },
                tags = new List<string> { "utility", "mesh" },
                subCategory = "Explorers",
                description = "View the data contained in a PSK skeletal mesh file extracted using Gildor's umodel toolkit."
            });
            set.Add(new Tool
            {
                name = "Script Database",
                type = typeof(ScriptDB.ScriptDB),
                icon = Application.Current.FindResource("iconScriptDatabase") as ImageSource,
                open = () =>
                {
                    (new ScriptDB.ScriptDB()).Show();
                },
                tags = new List<string> { "utility", "unreal" },
                subCategory = "Databases",
                description = "Script Database is used to locate UnrealScript exports across multiple files for ME3. This tool is deprecated and is no longer supported.",
            });
            set.Add(new Tool
            {
                name = "Subtitle Scanner",
                type = typeof(SubtitleScanner.SubtitleScanner),
                icon = Application.Current.FindResource("iconSubtitleScanner") as ImageSource,
                open = () =>
                {
                    (new SubtitleScanner.SubtitleScanner()).Show();
                },
                tags = new List<string> { "utility", "dialogue", "text", "line" },
                subCategory = "Explorers",
                description = "Subtitle Scanner is a utility for ME3 that scans game files for all subtitles and displays the results in a searchable dialog.",
            });
            #endregion

            #region Create Mods
            //set.Add(new Tool
            //{
            //    name = "Audio Editor",
            //    type = typeof(Audio_Editor.AudioEditor),
            //    icon = Application.Current.FindResource("iconAudioEditor") as ImageSource,
            //    open = () =>
            //    {
            //        (new Audio_Editor.AudioEditor()).Show();
            //    },
            //    tags = new List<string> { "developer", "afc", "sound", "wwise" },
            //    subCategory = "Core",
            //});
            set.Add(new Tool
            {
                name = "Binary Interpreter",
                type = typeof(BinaryInterpreterHost),
                icon = Application.Current.FindResource("iconInterpreter") as ImageSource,
                tags = new List<string>(),
                subCategory = other,
            });
            set.Add(new Tool
            {
                name = "Conditionals Editor",
                type = typeof(Conditionals),
                icon = Application.Current.FindResource("iconConditionalsEditor") as ImageSource,
                open = () =>
                {
                    (new Conditionals()).Show();
                },
                tags = new List<string> { "developer", "conditional", "plot", "boolean", "flag", "int", "integer", "cnd" },
                subCategory = "Core",
                description = "Conditionals Editor is used to create and edit ME3 files with the .cnd extension. CND files control game story by checking for specific combinations of plot events.",
            });
            set.Add(new Tool
            {
                name = "Curve Editor",
                type = typeof(CurveEd.CurveEditor),
                icon = Application.Current.FindResource("iconPlaceholder") as ImageSource,
                tags = new List<string>(),
                subCategory = other,
            });
            set.Add(new Tool
            {
                name = "Dialogue Editor",
                type = typeof(DialogEditor.DialogEditor),
                icon = Application.Current.FindResource("iconDialogueEditor") as ImageSource,
                open = () =>
                {
                    string result = InputComboBox.GetValue("Which game's files do you want to edit?", new[] { "ME3", "ME2", "ME1" }, "ME3", true);
                    switch (result)
                    {
                        case "ME3":
                            (new DialogEditor.DialogEditor()).Show();
                            break;
                        case "ME2":
                            (new ME2Explorer.DialogEditor()).Show();
                            break;
                        case "ME1":
                            (new ME1Explorer.DialogEditor()).Show();
                            break;
                    }

                },
                tags = new List<string> { "developer", "me1", "me2", "me3", "cutscene" },
                subCategory = "Scene Shop",
                description = "Dialogue Editor is a cross-game tool used to edit Bioconversation objects, which control the flow of dialogue during a conversation.",
            });
            set.Add(new Tool
            {
                name = "ME2 Dialogue Editor",
                type = typeof(ME2Explorer.DialogEditor),
                icon = Application.Current.FindResource("iconDialogueEditor") as ImageSource,
                tags = new List<string>(),
            });
            set.Add(new Tool
            {
                name = "ME3 Dialogue Editor",
                type = typeof(ME1Explorer.DialogEditor),
                icon = Application.Current.FindResource("iconDialogueEditor") as ImageSource,
                tags = new List<string>(),
            });
            set.Add(new Tool
            {
                name = "FaceFX Editor",
                type = typeof(FaceFX.FaceFXEditor),
                icon = Application.Current.FindResource("iconFaceFXEditor") as ImageSource,
                open = () =>
                {
                    (new FaceFX.FaceFXEditor()).Show();
                },
                tags = new List<string> { "developer", "fxa", "facefx", "lipsync", "fxe", "bones", "animation", "me3", "me3" },
                subCategory = "Scene Shop",
                description = "FaceFX Editor is the toolset’s highly-simplified version of FaceFX Studio. With this tool modders can edit ME3 and ME2 FaceFX AnimSets (FXEs).",
            });
            set.Add(new Tool
            {
                name = "FaceFXAnimSet Editor",
                type = typeof(FaceFX.FaceFXAnimSetEditor),
                icon = Application.Current.FindResource("iconFaceFXAnimSetEditor") as ImageSource,
                open = () =>
                {
                    (new FaceFX.FaceFXAnimSetEditor()).Show();
                    //string result = InputComboBox.GetValue("Which game's files do you want to edit?", new string[] { "ME3", "ME2" }, "ME3", true);
                    //switch (result)
                    //{
                    //    case "ME3":
                    //        (new FaceFX.FaceFXAnimSetEditor()).Show();
                    //        break;
                    //    case "ME2":
                    //        (new ME2Explorer.FaceFXAnimSetEditor()).Show();
                    //        break;
                    //}
                },
                tags = new List<string> { "developer", "fxa", "facefx", "lipsync", "fxe", "bones", "animation" },
                subCategory = "Scene Shop",
                description = "FaceFXAnimSetEditor is the original tool for manipulating FaceFXAnimsets. It will soon be completely replaced by the more complete FaceFX Editor.",
            });
            set.Add(new Tool
            {
                name = "Interpreter",
                type = typeof(InterpreterHost),
                icon = Application.Current.FindResource("iconInterpreter") as ImageSource,
                tags = new List<string>(),
                subCategory = other,
            });
            //Benji's tool. Uncomment when we have more progress.
            /*set.Add(new Tool
            {
                name = "Level Explorer",
                type = typeof(LevelExplorer.LevelExplorer),
                icon = Application.Current.FindResource("iconLevelEditor") as ImageSource,
                open = () =>
                {
                    (new LevelExplorer.LevelExplorer()).Show();
                },
                tags = new List<string> { "developer" },
                subCategory = other,
                description = "Level Explorer allows you to view the meshes of a level.",

            });*/
            set.Add(new Tool
            {
                name = "Mesh Database",
                type = typeof(Meshplorer2.MeshDatabase),
                icon = Application.Current.FindResource("iconMeshDatabase") as ImageSource,
                open = () =>
                {
                    (new Meshplorer2.MeshDatabase()).Show();
                },
                tags = new List<string> { "utility", "mesh" },
                subCategory = "Databases",
                description = "Scans ME3 (no DLC) for meshes and makes a database. This tool is deprecated and no longer supported."
            });
            set.Add(new Tool
            {
                name = "Mount Editor",
                type = typeof(MountEditor.MountEditorWPF),
                icon = Application.Current.FindResource("iconMountEditor") as ImageSource,
                open = () =>
                {
                    new MountEditor.MountEditorWPF().Show();
                },
                tags = new List<string> { "developer", "mount", "dlc", "me2", "me3" },
                subCategory = "Core",
                description = "Mount Editor allows you to create or modify mount.dlc files, which are used in DLC for Mass Effect 2 and Mass Effect 3."
            });
            set.Add(new Tool
            {
                name = "TLK Manager WPF",
                type = typeof(TlkManagerNS.TLKManagerWPF),
                icon = Application.Current.FindResource("iconTLKManager") as ImageSource,
                open = () =>
                {
                    new TlkManagerNS.TLKManagerWPF().Show();
                },
                tags = new List<string> { "developer", "dialogue", "subtitle", "text", "string", "localize", "language" },
                subCategory = "Core",
                description = "TLK Manager WPF manages loaded TLK files that are used to display string data in editor tools. You can also use it to extract and recompile TLK files."
            });
            set.Add(new Tool
            {
                name = "Package Editor (Old)",
                type = typeof(PackageEditor),
                icon = Application.Current.FindResource("iconPackageEditorClassic") as ImageSource,
                open = () =>
                {
                    PackageEditor pck = new PackageEditor();
                    pck.Show();
                },
                tags = new List<string> { "developer", "pcc", "cloning", "import", "export", "sfm", "upk", ".u", "me2", "me1", "me3", "name" },
                subCategory = "Core",
                description = "Package Editor Classic is a tool for editing trilogy package files in various formats (PCC, SFM, UPK). Properties, arrays, names, curve data, and more can all be easily added and edited.\n\nPackage Editor Classic has been deprecated and is scheduled for removal in the next release."
            });
            set.Add(new Tool
            {
                name = "Package Editor",
                type = typeof(PackageEditorWPF),
                icon = Application.Current.FindResource("iconPackageEditor") as ImageSource,
                open = () =>
                {
                    new PackageEditorWPF().Show();
                },
                tags = new List<string> { "user", "developer", "pcc", "cloning", "import", "export", "sfm", "upk", ".u", "me2", "me1", "me3", "name" },
                subCategory = "Core",
                description = "Package Editor WPF is a complete rewrite of Package Editor using the WPF design language. Edit trilogy game files in a single window with access to external tools such as Curve Editor and Soundplorer, right in the same window."
            });
            set.Add(new Tool
            {
                name = "Pathfinding Editor",
                type = typeof(PathfindingEditorWPF),
                icon = Application.Current.FindResource("iconPathfindingEditor") as ImageSource,
                open = () =>
                {
                    (new PathfindingEditorWPF()).Show();
                },
                tags = new List<string> { "user", "developer", "path", "ai", "combat", "spline", "spawn", "map", "path", "node", "cover", "level" },
                subCategory = "Core",
                description = "Pathfinding Editor WPF allows you to modify pathing nodes so squadmates and enemies can move around a map. You can also edit placement of several different types of level objects such as StaticMeshes, Splines, CoverSlots, and more.",
            });
            set.Add(new Tool
            {
                name = "Plot Editor",
                type = typeof(MassEffect.NativesEditor.Views.PlotEditor),
                icon = Application.Current.FindResource("iconPlotEditor") as ImageSource,
                open = () =>
                {
                    var plotEd = new MassEffect.NativesEditor.Views.PlotEditor();
                    plotEd.Show();
                },
                tags = new List<string> { "developer", "codex", "state transition", "quest", "natives" },
                subCategory = "Core",
                description = "Plot Editor is used to examine, edit, and search ME3's plot maps for quests, state events, and codices."
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
                subCategory = "Core",
                description = "Sequence Editor WPF is the toolset’s version of UDK’s UnrealKismet. With this cross-game tool, users can edit and create new sequences that control gameflow within and across levels.",
            });
            set.Add(new Tool
            {
                name = "SFAR Editor",
                type = typeof(SFAREditor2),
                icon = Application.Current.FindResource("iconSFAREditor") as ImageSource,
                open = () =>
                {
                    (new SFAREditor2()).Show();
                },
                tags = new List<string> { "developer", "dlc" },
                subCategory = other,
                description = "SFAR Editor allows you to explore SFAR files in Mass Effect 3. This tool has been deprecated as DLC unpacking and AutoTOC has replaced the need to inspect SFAR files.",
            });
            set.Add(new Tool
            {
                name = "Soundplorer",
                type = typeof(Soundplorer.SoundplorerWPF),
                icon = Application.Current.FindResource("iconSoundplorer") as ImageSource,
                open = () =>
                {
                    (new Soundplorer.SoundplorerWPF()).Show();
                },
                tags = new List<string> { "user", "developer", "audio", "dialogue", "music", "wav", "ogg", "sound" },
                subCategory = "Scene Shop",
                description = "Soundplorer WPF is a complete rewrite of the original  Soundplorer. Extract and play audio from all 3 games, and replace audio directly in Mass Effect 3.",
            });
            set.Add(new Tool
            {
                name = "Texplorer",
                type = typeof(Texplorer2),
                icon = Application.Current.FindResource("iconTexplorer") as ImageSource,
                open = () =>
                {
                    (new Texplorer2()).Show();
                },
                tags = new List<string> { "developer", "texture", "tfc", "scan", "tree" },
                subCategory = "Meshes + Textures",
                description = "TEXPLORER IS UNSUPPORTED IN ME3EXPLORER ME3TWEAKS FORK\n\nTexplorer is a texturing utility that allows users to browse and install textures for all 3 Mass Effect trilogy games. It has been superceded by Mass Effect Modder (MEM) in most regards."
            });

            set.Add(new Tool
            {
                name = "WwiseBank Editor",
                type = typeof(WwiseBankEditor.WwiseEditor),
                icon = Application.Current.FindResource("iconWwiseBankEditor") as ImageSource,
                open = () =>
                {
                    (new WwiseBankEditor.WwiseEditor()).Show();
                },
                tags = new List<string> { "developer", "dialogue", "text", "line" },
                subCategory = "Scene Shop",
                description = "Wwisebank Editor edits ME3 Wwisebank objects, which contain data references to specific sets of Wwiseevents and Wwisestreams in the PCC. \n\nEditing “the bank” is often necessary when changing game music or when adding new dialogue.",
            });
            set.Add(new Tool
            {
                name = "UDK Explorer",
                type = typeof(UDKExplorer.MainWindow),
                icon = Application.Current.FindResource("iconUDKExplorer") as ImageSource,
                open = () =>
                {
                    string loc = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                    if (File.Exists(loc + "\\UDKExplorer.exe"))
                    {
                        Process.Start(loc + "\\UDKExplorer.exe");
                    }
                },
                tags = new List<string> { "developer" },
                subCategory = other,
                description = "Edits .udk and .upk files created by the UDK. This tool is deprecated and no longer supported."
            });
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