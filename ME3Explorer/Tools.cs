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
        public string subCategory { get; set; }
        public string description { get; set; }
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
                tags = new List<string> { "user", "toc", "tocing", "crash", "infinite", "loop", "loading" },
                description = "AutoTOC is a tool for ME3 that updates and/or creates the PCConsoleTOC.bin files associated with the base game and each DLC.\n\nRunning this tool upon mod installation is imperative to ensuring proper functionality of the game."
            });
            list.Add(new Tool
            {
                name = "Modmaker",
                icon = Application.Current.FindResource("iconModMaker") as ImageSource,
                open = () =>
                {
                    (new ModMaker()).Show();
                },
                tags = new List<string> { "user", ".mod", "mod", "mesh" },
                description = "ModMaker is a tool used to create and install files with the \".mod\" extension. MOD files are compatible with ME3 and may be packaged with meshes and other game resources.\n\nAttention: Installation of textures via MOD files is deprecated. Use MM to extract any textures, then install them with TPF Tools, instead."
            });
            list.Add(new Tool
            {
                name = "TPF Tools",
                icon = Application.Current.FindResource("iconTPFTools") as ImageSource,
                open = () =>
                {
                    (new KFreonTPFTools3()).Show();
                },
                tags = new List<string> { "user", "texture", "tpf", "dds", "bmp", "jpg", "png" },
                description = "TPF Tools is the toolset’s primary texture installation utility for users. An alternative to Texmod, TPF Tools allows for permanent insertion of textures into game files. It’s compatible with a variety of texture formats, will help “repair” improperly-formatted textures, and has an assortment of other features.\n\nTPF Tools can also be used by modders to package textures into TPFs for distribution."
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
                tags = new List<string> { "utility", "novice", "friendly", "user-friendly" },
                subCategory = "Explorers",
                description = "Asset Explorer is a useful utility for newcomers to modding Mass Effect. It allows for the browsing of ME3 PCC files via a somewhat user-friendly GUI.\n\nAttention: this tool is in archival state and may contain features that no longer function.",
            });
            list.Add(new Tool
            {
                name = "Audio Extractor",
                icon = Application.Current.FindResource("iconAudioExtractor") as ImageSource,
                open = () =>
                {
                    (new AFCExtract()).Show();
                },
                tags = new List<string> { "utility", "afc", "music", "ogg", "wav", "sound", "dialogue" },
                subCategory = "Extractors + Repackers",
                description = "Audio Extractor** is a utility that extracts sound data from ME3 AFC files."
            });
            list.Add(new Tool
            {
                name = "Bik Movie Extractor",
                icon = Application.Current.FindResource("iconBikExtractor") as ImageSource,
                open = () =>
                {
                    (new BIKExtract()).Show();
                },
                tags = new List<string> { "utility", "bik", "movie" },
                subCategory = "Extractors + Repackers",
                description = "BIK Movie Extractor is a utility for extracting BIK videos from the ME3 Movies.tfc. This file contains small resolution videos played during missions, such as footage of Miranda in Sanctuary.",
            });
            list.Add(new Tool
            {
                name = "Class Viewer",
                icon = Application.Current.FindResource("iconClassViewer") as ImageSource,
                open = () =>
                {
                    (new ClassViewer.ClassViewer()).Show();
                },
                tags = new List<string> { "utility", "import" },
                subCategory = "Explorers",
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
                tags = new List<string> { "utility", "code", "endian" },
                subCategory = "Converters",
                description = "Hex Converter is a utility that converts among floats, signed/unsigned integers, and hex code in big/little endian.",
            });
            list.Add(new Tool
            {
                name = "Image Engine",
                icon = Application.Current.FindResource("iconImageEngine") as ImageSource,
                open = () =>
                {
                    (new CSharpImageLibrary.MainWindow()).Show();
                },
                tags = new List<string> { "utility", "texture", "convert", "dds", "bmp", "jpg", "png" },
                subCategory = "Converters",
                description = "Image Engine is a texture conversion utility. It supports BMP, JPG, PNG, TGA files, as well as a variety of DDS formats and compressions. Modification to mipmaps are also supported.",
            });
            list.Add(new Tool
            {
                name = "Interp Viewer",
                icon = Application.Current.FindResource("iconInterpViewer") as ImageSource,
                open = () =>
                {
                    (new InterpEditor.InterpEditor()).Show();
                },
                tags = new List<string> { "utiity", "dialogue", "matinee", "cutscene", "animcutscene" },
                subCategory = "Explorers",
                description = "Interp Viewer is a simplified version of UDK’s Matinee Editor. It loads interpdata objects and displays their children as tracks on a timeline, allowing the user to visualize the game content associated with a specific scene.\n\nAttention: This tool is a utility; editing is not yet supported."
            });
            list.Add(new Tool
            {
                name = "Level Database",
                icon = Application.Current.FindResource("iconLevelDatabase") as ImageSource,
                open = () =>
                {
                    (new LevelExplorer.Levelbase()).Show();
                },
                tags = new List<string> { "utility" },
                subCategory = "Databases",
            });
            list.Add(new Tool
            {
                name = "PCC Repacker",
                icon = Application.Current.FindResource("iconPCCRepacker") as ImageSource,
                open = () =>
                {
                    (new PCCRepack()).Show();
                },
                tags = new List<string> { "utility", "compress", "decompress" },
                subCategory = "Extractors + Repackers",
            });
            list.Add(new Tool
            {
                name = "Plot Database",
                icon = Application.Current.FindResource("iconPlotDatabase") as ImageSource,
                open = () =>
                {
                    (new PlotVarDB.PlotVarDB()).Show();
                },
                tags = new List<string> { "utility", "bool", "boolean", "flag", "int", "integer" },
                subCategory = "Databases",
                description = "Plot Database is a cross-game utility used to store story data associated with plot IDs. The tool comes pre-loaded with a default .db that can be customized by the user. Never look up a plot bool or integer again!",
            });
            list.Add(new Tool
            {
                name = "Property Database",
                icon = Application.Current.FindResource("iconPropertyDatabase") as ImageSource,
                open = () =>
                {
                    (new Propertydb.PropertyDB()).Show();
                },
                tags = new List<string> { "utility" },
                subCategory = "Databases",
            });
            list.Add(new Tool
            {
                name = "Property Dumper",
                icon = Application.Current.FindResource("iconPropertyDumper") as ImageSource,
                open = () =>
                {
                    (new Property_Dumper.PropDumper()).Show();
                },
                tags = new List<string> { "utility" },
                subCategory = "Properties",
            });
            list.Add(new Tool
            {
                name = "Property Manager",
                icon = Application.Current.FindResource("iconPropertyManager") as ImageSource,
                open = () =>
                {
                    (new PropertyManager()).Show();
                },
                tags = new List<string> { "utility" },
                subCategory = "Properties",
            });
            list.Add(new Tool
            {
                name = "PSA Viewer",
                icon = Application.Current.FindResource("iconPSAViewer") as ImageSource,
                open = () =>
                {
                    (new PSAViewer()).Show();
                },
                tags = new List<string> { "utility", "mesh", "animation" },
                subCategory = "Explorers",
            });
            list.Add(new Tool
            {
                name = "PSK Viewer",
                icon = Application.Current.FindResource("iconPSKViewer") as ImageSource,
                open = () =>
                {
                    (new PSKViewer.PSKViewer()).Show();
                },
                tags = new List<string> { "utility", "mesh" },
                subCategory = "Explorers",
            });
            list.Add(new Tool
            {
                name = "ME1 Save Game Editor",
                icon = Application.Current.FindResource("iconSaveGameEditor") as ImageSource,
                open = () =>
                {
                    (new ME1Explorer.SaveGameEditor.SaveEditor()).Show();
                },
                tags = new List<string> { "utility" },
                subCategory = "Saved Games",
            });
            list.Add(new Tool
            {
                name = "ME1 Save Game Operator",
                icon = Application.Current.FindResource("iconSaveGameOperator") as ImageSource,
                open = () =>
                {
                    (new ME1Explorer.SaveGameOperator.SaveGameOperator()).Show();
                },
                tags = new List<string> { "utility" },
                subCategory = "Saved Games",
            });
            list.Add(new Tool
            {
                name = "Script Database",
                icon = Application.Current.FindResource("iconScriptDatabase") as ImageSource,
                open = () =>
                {
                    (new ScriptDB.ScriptDB()).Show();
                },
                tags = new List<string> { "utility" },
                subCategory = "Databases",
            });
            list.Add(new Tool
            {
                name = "Subtitle Scanner",
                icon = Application.Current.FindResource("iconSubtitleScanner") as ImageSource,
                open = () =>
                {
                    (new SubtitleScanner.SubtitleScanner()).Show();
                },
                tags = new List<string> { "utility", "dialogue", "text", "line" },
                subCategory = "Explorers",
                description = "Subtitle Scanner is a utility for ME3 that scans game files for all subtitles and displays the results in a searchable dialog.",
            });
            list.Add(new Tool
            {
                name = "WwiseBank Editor",
                icon = Application.Current.FindResource("iconWwiseBankEditor") as ImageSource,
                open = () =>
                {
                    (new WwiseBankEditor.WwiseEditor()).Show();
                },
                tags = new List<string> { "utility", "dialogue", "text", "line" },
                subCategory = "Scene Shop",
                description = "Wwisebank Editor edits ME3 Wwisebank objects, which contain data references to specific sets of Wwiseevents and Wwisestreams in the PCC. \n\nEditing “the bank” is often necessary when changing game music or when adding new dialogue.",
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
                tags = new List<string> { "developer", "animation", "gesture", "bones" },
                subCategory = "Scene Shop",
            });
            list.Add(new Tool
            {
                name = "Camera Tool",
                icon = Application.Current.FindResource("iconCameraTool") as ImageSource,
                open = () =>
                {
                    (new CameraTool.CamTool()).Show();
                },
                tags = new List<string> { "developer" },
                subCategory = "Scene Shop",
            });
            list.Add(new Tool
            {
                name = "Coalesced Editor",
                icon = Application.Current.FindResource("iconCoalescedEditor") as ImageSource,
                open = () =>
                {
                    (new Coalesced_Editor.CoalEditor()).Show();
                },
                tags = new List<string> { "developer", "coalesced", "ini", "bin" },
                subCategory = "Core",
                description = "Coalesced Editor is used to create and edit ME3 Coalesced.bin files for the base game and DLC. These are key game files that help control a large amount of content.\n\nAttention: This tool is deprecated and does not work correctly for DLC.It will be updated soon.Use with caution.",
            });
            list.Add(new Tool
            {
                name = "Conditionals Editor",
                icon = Application.Current.FindResource("iconConditionalsEditor") as ImageSource,
                open = () =>
                {
                    (new Conditionals()).Show();
                },
                tags = new List<string> { "developer", "conditional", "plot", "boolean", "flag", "int", "integer", "cnd" },
                subCategory = "Core",
                description = "Conditionals Editor is used to create and edit ME3 files with the .cnd extension. CND files control game story by checking for specific combinations of plot events.",
            });
            list.Add(new Tool
            {
                name = "Dialogue Editor",
                icon = Application.Current.FindResource("iconDialogueEditor") as ImageSource,
                open = () =>
                {
                    string result = InputComboBox.GetValue("Which game's files do you want to edit?", new string[] { "ME3", "ME2", "ME1" }, "ME3");
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
                description = "Dialogue Editor is  cross-game tool used to edit Bioconversation objects, which control the flow of dialogue during a conversation.",
            });
            list.Add(new Tool
            {
                name = "FaceFX Editor",
                icon = Application.Current.FindResource("iconFaceFXAnimSetEditor") as ImageSource,
                open = () =>
                {
                    (new FaceFXAnimSetEditor.FaceFXEditor()).Show();
                },
                tags = new List<string> { "developer", "fxa", "facefx", "lipsync", "fxe", "bones", "animation" },
                subCategory = "Scene Shop",
            });
            list.Add(new Tool
            {
                name = "FaceFXAnimSet Editor",
                icon = Application.Current.FindResource("iconFaceFXAnimSetEditor") as ImageSource,
                open = () =>
                {
                    string result = InputComboBox.GetValue("Which game's files do you want to edit?", new string[] { "ME3", "ME2" }, "ME3");
                    switch (result)
                    {
                        case "ME3":
                            (new FaceFXAnimSetEditor.FaceFXAnimSetEditor()).Show();
                            break;
                        case "ME2":
                            (new ME2Explorer.FaceFXAnimSetEditor()).Show();
                            break;
                    }
                },
                tags = new List<string> { "developer", "fxa", "facefx", "lipsync", "fxe", "bones", "animation" },
                subCategory = "Scene Shop",
            });
            list.Add(new Tool
            {
                name = "GUID Cache Editor",
                icon = Application.Current.FindResource("iconGUIDCacheEditor") as ImageSource,
                open = () =>
                {
                    (new GUIDCacheEditor.GUIDCacheEditor()).Show();
                },
                tags = new List<string> { "developer" },
                subCategory = "Other",
            });
            list.Add(new Tool
            {
                name = "Level Editor",
                icon = Application.Current.FindResource("iconLevelEditor") as ImageSource,
                open = () =>
                {
                    (new LevelExplorer.LevelEditor.Leveleditor()).Show();
                },
                tags = new List<string> { "developer" },
                subCategory = "Other",
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
                tags = new List<string> { "developer", "advanced", "cloning", "import", "export" },
                subCategory = "Core",
                description = "ME3Creator is the toolset’s most advanced modding tool for ME3. It allows for level viewing, intrafile and interfile import and export cloning, re-linking of game objects, and much more.",
            });
            list.Add(new Tool
            {
                name = "Meshplorer",
                icon = Application.Current.FindResource("iconMeshplorer") as ImageSource,
                open = () =>
                {
                    (new Meshplorer.Meshplorer()).Show();
                },
                tags = new List<string> { "developer", "mesh" },
                subCategory = "Meshes + Textures",
            });
            list.Add(new Tool
            {
                name = "Meshplorer 2",
                icon = Application.Current.FindResource("iconMeshplorer2") as ImageSource,
                open = () =>
                {
                    (new Meshplorer2.Meshplorer2()).Show();
                },
                tags = new List<string> { "developer", "mesh" },
                subCategory = "Meshes + Textures",
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
                tags = new List<string> { "developer", "pcc", "cloning", "import", "export", "sfm", "upk", ".u"},
                subCategory = "Core",
                description = "Package Editor is the toolset’s main tool for editing trilogy package files in various formats (PCC, UPK, SFM). Properties, arrays, names, curve data, and more can all be easily added and edited."
            });
            list.Add(new Tool
            {
                name = "Sequence Editor",
                icon = Application.Current.FindResource("iconSequenceEditor") as ImageSource,
                open = () =>
                { 
                    (new SequenceEditor()).Show();
                },
                tags = new List<string> { "developer", "kismet", "me1", "me2", "me3" },
                subCategory = "Core",
                description = "Sequence Editor is the toolset’s version of UDK’s UnrealKismet. With this cross-game tool, users can edit and create new sequences that control gameflow within and across levels.",
            });
            list.Add(new Tool
            {
                name = "SFAR Basic Editor",
                icon = Application.Current.FindResource("iconSFARBasicEditor") as ImageSource,
                open = () =>
                {
                    (new SFARBasicEditor()).Show();
                },
                tags = new List<string> { "developer", "dlc" },
                subCategory = "SFARS",
                description = "SFAR Basic Editor loads ME3 DLC SFARs, allowing for exploration and basic edits within a relatively user-friendly GUI.",
            });
            list.Add(new Tool
            {
                name = "SFAR Editor 2",
                icon = Application.Current.FindResource("iconSFAREditor2") as ImageSource,
                open = () =>
                {
                    (new SFAREditor2()).Show();
                },
                tags = new List<string> { "developer", "dlc" },
                subCategory = "SFARS",
                description = "SFAR Editor 2 is an advanced SFAR exploration and editing tool for ME3. It displays technical data absent from the Basic Editor and contains searching and unpack features.",
            });
            list.Add(new Tool
            {
                name = "SFAR TOC Updater",
                icon = Application.Current.FindResource("iconSFARTOCUpdater") as ImageSource,
                open = () =>
                {
                    (new SFARTOCbinUpdater()).Show();
                },
                tags = new List<string> { "developer", "dlc", "toc", "tocing" },
                subCategory = "SFARS",
                description = "SFAR TOC Updater updates PCConsoleTOC.bin files that are inside unpacked ME3 SFARs. Due to DLC unpacking requirement for texture viewing and modification, most users will never use this tool."
            });
            list.Add(new Tool
            {
                name = "Soundplorer",
                icon = Application.Current.FindResource("iconSoundplorer") as ImageSource,
                open = () =>
                {
                    (new Soundplorer()).Show();
                },
                tags = new List<string> { "developer", "audio", "dialogue", "music", "wav", "ogg", "sound" },
                subCategory = "Scene Shop",
                description = "Soundplorer provides access to all Wwisestream and Wwisebank objects inside an ME3 PCC. Sounds can be played within the tool, exported, and changed via import.",
            });
            list.Add(new Tool
            {
                name = "Texplorer",
                icon = Application.Current.FindResource("iconTexplorer") as ImageSource,
                open = () =>
                {
                    (new Texplorer2()).Show();
                },
                tags = new List<string> { "developer", "texture", "tfc", "scan", "tree" },
                subCategory = "Meshes + Textures",
                description = "Texplorer is the toolset’s primary texture tool for the trilogy. Textures are organized into a package tree, and each is displayed with its associated data. Textures can be searched, extracted/replaced, and exported into TPF Tools."
            });
            list.Add(new Tool
            {
                name = "ME3 + ME2 TLK Editor",
                icon = Application.Current.FindResource("iconTLKEditor") as ImageSource,
                open = () =>
                {
                    (new TLKEditor()).Show();
                },
                tags = new List<string> { "developer", "dialogue", "subtitle", "text" },
                subCategory = "Scene Shop",
                description = "TLK Editor converts between XML and TLK formats, allowing users to edit the display of all game text in ME2 and ME3. Edits to the XML files themselves must be done in an external editor, such as Notepad++.",
            });
            list.Add(new Tool
            {
                name = "ME1 TLK Editor",
                icon = Application.Current.FindResource("iconTLKEditor") as ImageSource,
                open = () =>
                {
                    (new ME1Explorer.TlkManager()).Show();
                },
                tags = new List<string> { "developer", "dialogue", "subtitle", "text" },
                subCategory = "Scene Shop",
                description = "ME1 TLK Editor extracts tlk files from ME1 packages and converts them into xml, allowing users to edit the display of all game text. Edits to the XML files themselves must be done in an external editor, such as Notepad++.",
            });
            #endregion

            items = list;
        }
    }
}