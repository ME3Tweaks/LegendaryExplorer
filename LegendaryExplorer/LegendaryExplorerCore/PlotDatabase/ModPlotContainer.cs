using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.PlotDatabase.PlotElements;

namespace LegendaryExplorerCore.PlotDatabase
{
    public class ModPlotContainer
    {
        public static int StartingModId = 100000;
        public MEGame Game { get; }
        public PlotElement GameHeader { get; }

        public List<ModPlotDatabase> Mods { get; } = new List<ModPlotDatabase>();

        private string LocalModFolderName => $"ModPlots{Game}";

        public ModPlotContainer(MEGame game)
        {
            Game = game;
            GameHeader = new PlotElement(0, StartingModId, $"{game.ToLEVersion()}/{game.ToOTVersion()} Mods", PlotElementType.Region, 0,
                new List<PlotElement>());
        }

        public void AddMod(ModPlotDatabase mod)
        {
            mod.ModRoot.AssignParent(GameHeader);
            Mods.Add(mod);
        }

        public void RemoveMod(ModPlotDatabase mod)
        {
            mod.ModRoot.RemoveFromParent();
            Mods.Remove(mod);
        }

        public void LoadModsFromDisk(string appDataFolder)
        {
            var saveFolder = Path.Combine(appDataFolder, LocalModFolderName);
            var jsonFiles = new DirectoryInfo(saveFolder).EnumerateFiles().Where(f => f.Extension == "json");
            foreach (var file in jsonFiles)
            {
                try
                {
                    var newMod = new ModPlotDatabase();
                    newMod.LoadPlotsFromFile(file.FullName);
                    foreach (var oldMod in Mods.Where(m => m.ModRoot.Label == newMod.ModRoot.Label))
                    {
                        RemoveMod(oldMod);
                    }
                    AddMod(newMod);
                }
                catch
                {
                    Debug.WriteLine($"Unable to load Mod Plot Database at {file.FullName}");
                }
            }
        }

        public void SaveModsToDisk(string appDataFolder)
        {
            var saveFolder = Path.Combine(appDataFolder, LocalModFolderName);
            foreach (var mod in Mods)
            {
                mod.SaveDatabaseToFile(saveFolder);
            }
        }
    }
}