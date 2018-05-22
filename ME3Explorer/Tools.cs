using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json;

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
            get { return (bool)GetValue(IsFavoritedProperty); }
            set { SetValue(IsFavoritedProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsFavorited.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsFavoritedProperty =
            DependencyProperty.Register("IsFavorited", typeof(bool), typeof(Tool), new PropertyMetadata(false, OnIsFavoritedChanged));

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

        public static IReadOnlyCollection<Tool> Items
        {
            get
            {
                return items;
            }
        }

        public static void Initialize()
        {
            HashSet<Tool> set = new HashSet<Tool>();

            #region Install Mods
            set.Add(new Tool
            {
                name = "AutoTOC",
                type = typeof(AutoTOC),
                icon = Application.Current.FindResource("iconAutoTOC") as ImageSource,
                open = () =>
                {
                    (new AutoTOC()).Show();
                },
                tags = new List<string> { "user", "toc", "tocing", "crash", "infinite", "loop", "loading" },
                description = "AutoTOC is a tool for ME3 that updates and/or creates the PCConsoleTOC.bin files associated with the base game and each DLC.\n\nRunning this tool upon mod installation is imperative to ensuring proper functionality of the game."
            });
            set.Add(new Tool
            {
                name = "ModMaker",
                type = typeof(ModMaker),
                icon = Application.Current.FindResource("iconModMaker") as ImageSource,
                open = () =>
                {
                    (new ModMaker()).Show();
                },
                tags = new List<string> { "user", "utility", ".mod", "mod", "mesh" },
                subCategory = "Mod Packagers",
                description = "ModMaker is used to create and install files with the \".mod\" extension. MOD files are compatible with ME3 and may be packaged with meshes and other game resources.\n\nAttention: Installation of textures via MOD files is deprecated. Use MM to extract any textures, then install them with TPF Tools, instead."
            });
            set.Add(new Tool
            {
                name = "TPF Tools",
                type = typeof(KFreonTPFTools3),
                icon = Application.Current.FindResource("iconTPFTools") as ImageSource,
                open = () =>
                {
                    (new KFreonTPFTools3()).Show();
                },
                tags = new List<string> { "user", "utility", "texture", "tpf", "dds", "bmp", "jpg", "png" },
                subCategory = "Mod Packagers",
                description = "TPF Tools is the toolset’s primary texture installation utility for users. An alternative to Texmod, TPF Tools allows for permanent insertion of textures into game files. It’s compatible with a variety of texture formats, will help “repair” improperly-formatted textures, and has an assortment of other features.\n\nTPF Tools can also be used by modders to package textures into TPFs for distribution."
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
            });
            set.Add(new Tool
            {
                name = "Asset Explorer",
                type = typeof(AssetExplorer),
                icon = Application.Current.FindResource("iconAssetExplorer") as ImageSource,
                open = () =>
                {
                    AssetExplorer assExp = new AssetExplorer();
                    assExp.Show();
                    assExp.LoadMe();
                },
                tags = new List<string> { "utility", "novice", "friendly", "user-friendly", "new", "help" },
                subCategory = "Explorers",
                description = "Asset Explorer is a useful utility for newcomers to modding Mass Effect. It allows for the browsing of ME3 PCC files via a somewhat user-friendly GUI.\n\nAttention: this tool is in archival state and may contain features that no longer function.",
            });
            set.Add(new Tool
            {
                name = "Audio Extractor",
                type = typeof(AFCExtract),
                icon = Application.Current.FindResource("iconAudioExtractor") as ImageSource,
                open = () =>
                {
                    (new AFCExtract()).Show();
                },
                tags = new List<string> { "utility", "afc", "music", "ogg", "wav", "sound", "dialogue" },
                subCategory = "Extractors + Repackers",
                description = "Audio Extractor extracts sound data from ME3 AFC files."
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
                name = "Hex Converter",
                type = typeof(HexConverter.Hexconverter),
                icon = Application.Current.FindResource("iconHexConverter") as ImageSource,
                open = () =>
                {
                    string loc = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                    if (File.Exists(loc + "\\HexConverter.exe"))
                        Process.Start(loc + "\\HexConverter.exe");
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
                description = "Meshplorer loads and displays all meshes within a database or level. The tool skins most meshes with its associated texture. A variety of view options including solid and wireframe are available.",
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
                name = "ME1 TLK Editor",
                type = typeof(TlkManager),
                icon = Application.Current.FindResource("iconTLKEditorME1") as ImageSource,
                open = () =>
                {
                    (new ME1Explorer.TlkManager(true)).Show();
                },
                tags = new List<string> { "utility", "dialogue", "subtitle", "text" },
                subCategory = "Extractors + Repackers",
                description = "ME1 TLK Editor extracts and repackages TLK data, allowing users to edit the display of all game text in ME1. Extracted data is stored in XML format and must be edited with an external program, such as Notepad++.",
            });
            set.Add(new Tool
            {
                name = "PCC Repacker",
                type = typeof(PCCRepack),
                icon = Application.Current.FindResource("iconPCCRepacker") as ImageSource,
                open = () =>
                {
                    (new PCCRepack()).Show();
                },
                tags = new List<string> { "utility", "compress", "decompress", "pack", "unpack" },
                subCategory = "Extractors + Repackers",
                description = "PCC Repacker allows you to compress and decompress PCC files.",

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
            });
            set.Add(new Tool
            {
                name = "Property Dumper",
                type = typeof(Property_Dumper.PropDumper),
                icon = Application.Current.FindResource("iconPropertyDumper") as ImageSource,
                open = () =>
                {
                    (new Property_Dumper.PropDumper()).Show();
                },
                tags = new List<string> { "utility" },
                subCategory = "Properties",
            });
            set.Add(new Tool
            {
                name = "Property Manager",
                type = typeof(PropertyManager),
                icon = Application.Current.FindResource("iconPropertyManager") as ImageSource,
                open = () =>
                {
                    (new PropertyManager()).Show();
                },
                tags = new List<string> { "utility" },
                subCategory = "Properties",
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
                    string result = InputComboBox.GetValue("Which game's files do you want to edit?", new string[] { "ME3", "ME2", "ME1" }, "ME3", true);
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
            set.Add(new Tool
            {
                name = "Level Explorer",
                type = typeof(LevelExplorer.LevelEditor.Leveleditor),
                icon = Application.Current.FindResource("iconLevelEditor") as ImageSource,
                open = () =>
                {
                    (new LevelExplorer.LevelEditor.Leveleditor()).Show();
                },
                tags = new List<string> { "developer" },
                subCategory = other,
                description = "Level Explorer allows you to view the meshes of a level. This tool is deprecated, no longer supported, and will be replaced in the future.\n\nFor those who have trouble with this tool, level objects can also be visualized with ME3Creator.",

            });
            set.Add(new Tool
            {
                name = "ME3 Creator",
                type = typeof(ME3Creator.Form1),
                icon = Application.Current.FindResource("iconME3Creator") as ImageSource,
                open = () =>
                {
                    string loc = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                    if (File.Exists(loc + "\\ME3Creator.exe"))
                    {
                        Process.Start(loc + "\\ME3Creator.exe");
                    }
                },
                tags = new List<string> { "developer", "level" },
                subCategory = "Core",
                description = "ME3Creator is deprecated. All functionalities save the level viewer and file header viewer are now incorporated into Package Editor. It will be removed from the toolset in the near future.",
            });
            set.Add(new Tool
            {
                name = "Mesh Database",
                type = typeof(Meshplorer2.MeshDatabase),
                icon = Application.Current.FindResource("iconMeshplorer2") as ImageSource,
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
                name = "Package Editor",
                type = typeof(PackageEditor),
                icon = Application.Current.FindResource("iconPackageEditor") as ImageSource,
                open = () =>
                {
                    PackageEditor pck = new PackageEditor();
                    pck.Show();
                },
                tags = new List<string> { "developer", "pcc", "cloning", "import", "export", "sfm", "upk", ".u", "me2", "me1", "me3", "name" },
                subCategory = "Core",
                description = "Package Editor is the toolset's main tool for editing trilogy package files in various formats (PCC, SFM, UPK). Properties, arrays, names, curve data, and more can all be easily added and edited."
            });
            set.Add(new Tool
            {
                name = "Pathfinding Editor",
                type = typeof(PathfindingEditor),
                icon = Application.Current.FindResource("iconPathfindingEditor") as ImageSource,
                open = () =>
                {
                    (new PathfindingEditor()).Show();
                },
                tags = new List<string> { "developer", "path", "ai", "combat", "spline", "spawn", "map", "path", "node", "cover", "level"},
                subCategory = "Core",
                description = "Pathfinding Editor allows you to modify pathing nodes so squadmates and enemies can move around a map. You can also edit placement of several different types of level objects such as StaticMeshes, Splines, CoverSlots, and more.",
            });
            set.Add(new Tool
            {
                name = "Plot Editor",
                type = typeof(MassEffect.NativesEditor.Views.ShellView),
                icon = Application.Current.FindResource("iconPlotEditor") as ImageSource,
                open = () =>
                {
                    var shellView = new MassEffect.NativesEditor.Views.ShellView();
                    shellView.Show();
                },
                tags = new List<string> { "developer", "codex", "state transition", "quest", "natives" },
                subCategory = "Core",
                description = "Plot Editor is used to examine, edit, and search ME3's plot maps for quests, state events, and codices.\n\nSupport for ME1 and ME2 is planned for the future."
            });
            set.Add(new Tool
            {
                name = "Sequence Editor",
                type = typeof(SequenceEditor),
                icon = Application.Current.FindResource("iconSequenceEditor") as ImageSource,
                open = () =>
                { 
                    (new SequenceEditor()).Show();
                },
                tags = new List<string> { "developer", "kismet", "me1", "me2", "me3" },
                subCategory = "Core",
                description = "Sequence Editor is the toolset’s version of UDK’s UnrealKismet. With this cross-game tool, users can edit and create new sequences that control gameflow within and across levels.",
            });
            set.Add(new Tool
            {
                name = "SFAR Editor 2",
                type = typeof(SFAREditor2),
                icon = Application.Current.FindResource("iconSFAREditor2") as ImageSource,
                open = () =>
                {
                    (new SFAREditor2()).Show();
                },
                tags = new List<string> { "developer", "dlc" },
                subCategory = other,
                description = "SFAR Editor 2 is an advanced SFAR exploration and editing tool for ME3. With DLC unpacking now the norm and the advent of DLC mods, this tool will be used by few modders.",
            });
            set.Add(new Tool
            {
                name = "Soundplorer",
                type = typeof(Soundplorer),
                icon = Application.Current.FindResource("iconSoundplorer") as ImageSource,
                open = () =>
                {
                    (new Soundplorer()).Show();
                },
                tags = new List<string> { "developer", "audio", "dialogue", "music", "wav", "ogg", "sound" },
                subCategory = "Scene Shop",
                description = "Soundplorer provides access to all Wwisestream and Wwisebank objects inside an ME3 PCC. Sounds can be played within the tool, exported, and changed via import.",
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
                tags = new List<string> { "user" ,"developer", "texture", "tfc", "scan", "tree" },
                subCategory = "Meshes + Textures",
                description = "For users and modders alike, Texplorer is the toolset's primary texture tool for the trilogy. Textures are organized into a package tree, and each is displayed with its associated data. Textures can be searched, extracted/replaced, and exported into TPF Tools."
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
                    HashSet<string> favorites = JsonConvert.DeserializeObject<HashSet<string>>(raw);
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
                    HashSet<string> favorites = new HashSet<string>();
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