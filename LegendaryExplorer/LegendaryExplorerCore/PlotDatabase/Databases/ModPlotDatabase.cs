using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.PlotDatabase.PlotElements;
using LegendaryExplorerCore.PlotDatabase.Serialization;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.Databases
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

        public ModPlotDatabase() { }

        public ModPlotDatabase(string modName, int startingId)
        {
            ModRoot = new PlotModElement(-1, startingId, modName, PlotElementType.Mod, null, new List<PlotElement>());
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

        /// <summary>
        /// Updates ElementIds for all elements in this database, starting from the input ID
        /// </summary>
        /// <param name="startingId"></param>
        /// <returns>Next usable ID</returns>
        public int ReindexElements(int startingId)
        {
            int idx = startingId;
            var elements = new List<PlotElement> {ModRoot};
            elements.AddRange(Bools.Values);
            elements.AddRange(Ints.Values);
            elements.AddRange(Floats.Values);
            elements.AddRange(Transitions.Values);
            elements.AddRange(Conditionals.Values);
            foreach (var el in elements)
            {
                el.SetElementId(idx);
                idx++;
            }

            var organizational = Organizational.Values.ToList();
            Organizational.Clear();
            foreach (var el in organizational)
            {
                el.SetElementId(idx);
                idx++;
                Organizational.Add(el.ElementId, el);
            }

            return idx;
        }
    }
}