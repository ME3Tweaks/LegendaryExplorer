using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.PlotDatabase.Databases;
using LegendaryExplorerCore.PlotDatabase.PlotElements;

namespace LegendaryExplorerCore.PlotDatabase
{
    /// <summary>
    /// Manages loading, saving, and accessing of multiple mod plot databases for a single game
    /// </summary>
    public class ModPlotContainer
    {
        /// <summary>The starting ElementId for all mod-added plot elements</summary>
        /// <remarks>No mod should contain elements with a lower id than this</remarks>
        public static int StartingModId = 100000;

        /// <summary>The game that this collection of mods is associated with</summary>
        public MEGame Game { get; }

        /// <summary>The plot element representing the root of this collection</summary>
        /// <remarks>All mods will get added as a child of this element</remarks>
        public PlotElement GameHeader { get; }

        /// <summary>A list of all loaded mod databases</summary>
        public List<ModPlotDatabase> Mods { get; } = new List<ModPlotDatabase>();

        /// <summary>Gets the name of a subfolder in AppData where mod .jsons may be stored</summary>
        public string LocalModFolderName => $"ModPlots{Game}";

        private int _highestModId = StartingModId + 1;

        /// <summary>
        /// Initializes a ModPlotContainer, creating a new <see cref="GameHeader"/>
        /// </summary>
        /// <param name="game">Game to create container for</param>
        public ModPlotContainer(MEGame game)
        {
            Game = game;
            GameHeader = new PlotElement(0, StartingModId, $"{game.ToLEVersion()}/{game.ToOTVersion()} Mods", PlotElementType.Region, 0,
                new List<PlotElement>());
        }

        /// <summary>
        /// Adds a mod to the collection. Will reindex it's elements to avoid collision with any loaded mods.
        /// </summary>
        /// <param name="mod"></param>
        public void AddMod(ModPlotDatabase mod)
        {
            mod.ModRoot.AssignParent(GameHeader);
            _highestModId = mod.ReindexElements(_highestModId);
            Mods.Add(mod);
        }

        /// <summary>
        /// Removes a given mod from the collection
        /// </summary>
        /// <param name="mod">Mod to remove</param>
        /// <param name="deleteFile">If <c>true</c>, deletes this mod's .json file from disk. Requires <paramref name="appDataFolder"/> to be provided.</param>
        /// <param name="appDataFolder">Application AppData folder</param>
        public void RemoveMod(ModPlotDatabase mod, bool deleteFile = false, string appDataFolder = "")
        {
            mod.ModRoot.RemoveFromParent();
            Mods.Remove(mod);

            var saveFolder = Path.Combine(appDataFolder, LocalModFolderName);
            if (deleteFile && Directory.Exists(saveFolder))
            {
                var dbPath = Path.Combine(saveFolder, $"{mod.ModRoot.Label}.json");
                File.Delete(dbPath);
            }
        }

        /// <summary>
        /// Calculates the next available ElementId
        /// </summary>
        /// <returns>ElementId</returns>
        public int GetNextElementId()
        {
            return _highestModId++;
        }

        /// <summary>
        /// Loads all mods for this game from the input AppData folder
        /// </summary>
        /// <param name="appDataFolder">Application AppData folder</param>
        public void LoadModsFromDisk(string appDataFolder)
        {
            var saveFolder = Path.Combine(appDataFolder, LocalModFolderName);
            if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);
            var jsonFiles = new DirectoryInfo(saveFolder).EnumerateFiles().Where(f => f.Extension == ".json");
            foreach (var file in jsonFiles)
            {
                try
                {
                    LoadModFromDisk(file);
                }
                catch
                {
                    Debug.WriteLine($"Unable to load Mod Plot Database at {file.FullName}");
                }
            }
        }

        /// <summary>
        /// Load an individual mod from disk
        /// </summary>
        /// <param name="modJsonPath">Path to mod .json file</param>
        public void LoadModFromDisk(string modJsonPath)
        {
            LoadModFromDisk(new FileInfo(modJsonPath));
        }

        /// <summary>
        /// Load an individual mod from disk
        /// </summary>
        /// <param name="file">FileInfo of mod .json file</param>
        /// <exception cref="Exception">Input file does not exist or is not a .json file</exception>
        public void LoadModFromDisk(FileInfo file)
        {
            if (!file.Exists || file.Extension != ".json") throw new Exception("Input path is not a JSON file");
            var newMod = new ModPlotDatabase() {Game = Game};
            newMod.LoadPlotsFromFile(file.FullName);
            foreach (var oldMod in Mods.Where(m => m.ModRoot.Label == newMod.ModRoot.Label).ToList())
            {
                RemoveMod(oldMod);
            }
            AddMod(newMod);
        }

        /// <summary>
        /// Saves all loaded mods to the given appdata folder
        /// </summary>
        /// <param name="appDataFolder">Application AppData folder to save mods to</param>
        public void SaveModsToDisk(string appDataFolder)
        {
            var saveFolder = Path.Combine(appDataFolder, LocalModFolderName);
            foreach (var mod in Mods)
            {
                mod.SaveDatabaseToFile(saveFolder, true);
            }
        }
    }
}