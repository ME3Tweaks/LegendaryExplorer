using System;
using System.Collections.Generic;
using System.IO;
using LegendaryExplorerCore.Packages;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase
{
    public class ModPlotDatabase : PlotDatabaseBase
    {
        public PlotModElement ModRoot { get; set; }
        public override PlotElement Root
        {
            get => ModRoot;
            protected set
            {
                if (value is PlotModElement modRoot)
                {
                    ModRoot = modRoot;
                }
            }
        }

        public override bool IsBioware => false;

        public void LoadPlotsFromFile(string dbPath)
        {
            if (dbPath == null || !File.Exists(dbPath))
                throw new ArgumentException("Database file was null or doesn't exist");
            StreamReader sr = new StreamReader(dbPath);
            string json = sr.ReadToEnd();
            ImportPlotsFromJSON(json);
        }

        public void ImportPlotsFromJSON(string json)
        {
            var pdb = JsonConvert.DeserializeObject<SerializedModPlotDatabase>(json, _jsonSerializerSettings);
            pdb.BuildTree();
            ImportPlots(pdb);
            Root = pdb.ModRoot;
        }

        public void SaveDatabaseToFile(string folder)
        {
            if (!CanSave() || !Directory.Exists(folder))
                return;

            var serializationObj = new SerializedModPlotDatabase(this);
            var json = JsonConvert.SerializeObject(serializationObj);

            var dbPath = Path.Combine(folder, $"PlotDBMods{Game}.json");
            File.WriteAllText(dbPath, json);
        }

        public static ModPlotDatabase CreateModPlotDatabase(MEGame game)
        {
            if (!game.IsLEGame()) throw new ArgumentException("Cannot create mod database for non-LE game");
            var modDb = new ModPlotDatabase()
            {
                Game = game
            };
            var modsRoot = new PlotElement(0, StartingModId, $"{game.ToLEVersion()}/{game.ToOTVersion()} Mods", PlotElementType.Region, 0,
                new List<PlotElement>());
            modDb.Organizational.Add(StartingModId, modsRoot);
            modDb.Root = modsRoot;

            return modDb;
        }
    }
}