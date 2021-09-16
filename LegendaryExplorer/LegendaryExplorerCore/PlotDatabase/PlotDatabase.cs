using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase
{
    public abstract class PlotDatabase
    {
        public PlotElement Root { get; protected set; }
        public Dictionary<int, PlotBool> Bools { get; set; } = new Dictionary<int, PlotBool>();
        public Dictionary<int, PlotElement> Ints { get; set; } = new Dictionary<int, PlotElement>();
        public Dictionary<int, PlotElement> Floats { get; set; } = new Dictionary<int, PlotElement>();
        public Dictionary<int, PlotConditional> Conditionals { get; set; } = new Dictionary<int, PlotConditional>();
        public Dictionary<int, PlotTransition> Transitions { get; set; } = new Dictionary<int, PlotTransition>();
        public Dictionary<int, PlotElement> Organizational { get; set; } = new Dictionary<int, PlotElement>();

        public MEGame Game { get; set; }

        public abstract bool IsBioware { get; }

        public bool CanSave() => !IsBioware;

        internal static JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
            {NullValueHandling = NullValueHandling.Ignore};

        public PlotDatabase(MEGame refgame, bool isbioware)
        {
            this.Game = refgame;
        }

        public PlotDatabase()
        {
        }

        internal void ImportPlots(SerializedPlotDatabase pdb)
        {
            if (pdb == null) throw new Exception("Plot Database was null.");

            Bools = pdb.Bools.ToDictionary((b) => b.PlotId);
            Ints = pdb.Ints.ToDictionary((b) => b.PlotId);
            Floats = pdb.Floats.ToDictionary((b) => b.PlotId);
            Conditionals = pdb.Conditionals.ToDictionary((b) => b.PlotId);
            Transitions = pdb.Transitions.ToDictionary((b) => b.PlotId);
            Organizational = pdb.Organizational.ToDictionary((b) => b.ElementId);
        }

        /// <summary>
        /// Turns the plot database into a single dictionary, with the key being ElementID
        /// </summary>
        /// <returns></returns>
        public SortedDictionary<int, PlotElement> GetMasterDictionary()
        {
            try
            {
                var elements = Bools.Values.ToList()
                    .Concat(Ints.Values.ToList())
                    .Concat(Floats.Values.ToList())
                    .Concat(Conditionals.Values.ToList())
                    .Concat(Transitions.Values.ToList())
                    .Concat(Organizational.Values.ToList())
                    .ToDictionary(e => e.ElementId);

                return new SortedDictionary<int, PlotElement>(elements);
            }
            catch //fallback in case saved dictionary has duplicate element ids
            {
                return new SortedDictionary<int, PlotElement>();
            }
        }

        /// <summary>
        /// Get an element from by it's Element ID
        /// </summary>
        /// <param name="elementId"></param>
        /// <returns></returns>
        public PlotElement GetElementById(int elementId)
        {
            var sorted = GetMasterDictionary();
            if (sorted.ContainsKey(elementId)) return sorted[elementId];

            return null;
        }

        public int GetNextElementId()
        {
            var maxElement = GetMasterDictionary().Keys.Max();
            return maxElement + 1;
        }
    }

    public class BasegamePlotDatabase : PlotDatabase
    {
        public override bool IsBioware => true;

        public void LoadPlotsFromLEC(MEGame game)
        {
            Game = game;
            var pdb = new SerializedPlotDatabase();

            string json = LegendaryExplorerCoreUtilities.LoadStringFromCompressedResource("PlotDatabases.zip",
                    LegendaryExplorerCoreLib.CustomPlotFileName(game));

            pdb = JsonConvert.DeserializeObject<SerializedPlotDatabase>(json, _jsonSerializerSettings);
            pdb.BuildTree();
            ImportPlots(pdb);

            Root = Organizational[1];
        }

        public static BasegamePlotDatabase CreateBasegamePlotDatabase(MEGame game)
        {
            var db = new BasegamePlotDatabase();
            db.LoadPlotsFromLEC(game);
            return db;
        }
    }

    public class ModPlotDatabase : PlotDatabase
    {
        public static int StartingModId = 100000;

        public override bool IsBioware => false;

        public void LoadPlotsFromFile(MEGame game, string dbPath)
        {
            if (dbPath == null || !File.Exists(dbPath))
                throw new ArgumentException("Database file was null or doesn't exist");
            Game = game;
            StreamReader sr = new StreamReader(dbPath);
            string json = sr.ReadToEnd();
            ImportPlotsFromJSON(json);
        }

        public void ImportPlotsFromJSON(string json)
        {
            var pdb = JsonConvert.DeserializeObject<SerializedPlotDatabase>(json, _jsonSerializerSettings);
            pdb.BuildTree();
            ImportPlots(pdb);

            if (Organizational.TryGetValue(StartingModId, out var root))
            {
                Root = root;
            };
        }

        public void SaveDatabaseToFile(string folder)
        {
            if (!CanSave() || !Directory.Exists(folder))
                return;

            var serializationObj = new SerializedPlotDatabase(this);
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