using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.PlotDatabase.PlotElements;
using LegendaryExplorerCore.PlotDatabase.Serialization;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.Databases
{
    /// <summary>
    /// Represents a database of Plot Elements for a single mod
    /// </summary>
    public class ModPlotDatabase : PlotDatabaseBase
    {
        /// <summary>
        /// Represents the source of this <see cref="ModPlotDatabase"/>
        /// </summary>
        public enum ModPlotSource
        {
            /// <summary>Unknown database source</summary>
            Unknown,
            /// <summary>Database was loaded from a local file</summary>
            LocalFile,
            /// <summary>Database was downloaded from a server</summary>
            Server,
        }

        /// <summary>
        /// Gets or sets a value representing the source of this database
        /// </summary>
        public ModPlotSource Source { get; set; }

        /// <summary>
        /// Gets or sets the root mod element for this database
        /// </summary>
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

        /// <summary>
        /// Initializes a <see cref="ModPlotDatabase"/>
        /// </summary>
        public ModPlotDatabase() { }

        /// <summary>
        /// Initializes a <see cref="ModPlotDatabase"/>, creating a new PlotModElement root with the given name and id
        /// </summary>
        /// <param name="modName">Label for new root element</param>
        /// <param name="startingId">ElementID for new root element</param>
        public ModPlotDatabase(string modName, int startingId)
        {
            ModRoot = new PlotModElement(-1, startingId, modName, PlotElementType.Mod, null, new List<PlotElement>());
        }

        public override bool IsBioware => false;

        /// <summary>
        /// Loads and imports a mod plot database in .json format from disk
        /// </summary>
        /// <param name="dbPath">Path to .json file</param>
        /// <exception cref="ArgumentException">Database file is null or does not exist</exception>
        public void LoadPlotsFromFile(string dbPath)
        {
            if (dbPath == null || !File.Exists(dbPath))
                throw new ArgumentException("Database file was null or doesn't exist");
            StreamReader sr = new StreamReader(dbPath);
            string json = sr.ReadToEnd();
            ImportPlotsFromJSON(json);
            Source = ModPlotSource.LocalFile;
        }

        /// <summary>
        /// Imports a mod plot database from json format
        /// </summary>
        /// <param name="json">.json string of serialized database</param>
        /// <exception cref="Exception">Error during deserialization</exception>
        public void ImportPlotsFromJSON(string json)
        {
            var pdb = JsonConvert.DeserializeObject<SerializedModPlotDatabase>(json, _jsonSerializerSettings);
            if (pdb is null) throw new Exception("Unable to deserialize mod plot database.");
            pdb.BuildTree();
            ImportPlots(pdb);
            Root = pdb.ModRoot;
        }

        /// <summary>
        /// Attempt to save this database in a .json format to the given folder
        /// </summary>
        /// <param name="folder">Path to folder</param>
        /// <param name="forceSave"></param>
        public void SaveDatabaseToFile(string folder, bool forceSave = false)
        {
            if (!CanSave() || !Directory.Exists(folder) || (Source != ModPlotSource.LocalFile && !forceSave))
                return;

            var serializationObj = new SerializedModPlotDatabase(this);
            var json = JsonConvert.SerializeObject(serializationObj);

            var dbPath = Path.Combine(folder, $"{ModRoot.Label}.json");
            File.WriteAllText(dbPath, json);
        }

        /// <summary>
        /// Updates ElementIds for all elements in this database, starting from the input ID
        /// </summary>
        /// <param name="startingId">Initial element ID to be used</param>
        /// <returns>Next usable ID after all elements have been reindexed</returns>
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