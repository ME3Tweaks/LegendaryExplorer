using System;
using System.Collections.Generic;
using System.IO;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.PlotDatabase.PlotElements;
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
            if (pdb is null) throw new Exception("Cannot deserialize mod plot database.");
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

            var dbPath = Path.Combine(folder, $"{ModRoot.Label}.json");
            File.WriteAllText(dbPath, json);
        }
    }
}