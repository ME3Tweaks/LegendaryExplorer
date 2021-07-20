using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Helpers;
using Newtonsoft.Json;
using System.IO;

namespace LegendaryExplorerCore.PlotDatabase
{
    /// <summary>
    /// A class representing the JSON serialized plot database file
    /// </summary>
    public class PlotDatabaseFile
    {
        // TODO: Change the JSON serialization to dictionary
        [JsonProperty("bools")]
        public List<PlotBool> Bools;

        [JsonProperty("ints")]
        public List<PlotElement> Ints;

        [JsonProperty("floats")]
        public List<PlotElement> Floats;

        [JsonProperty("conditionals")]
        public List<PlotConditional> Conditionals;

        [JsonProperty("transitions")] 
        public List<PlotTransition> Transitions;

        [JsonProperty("organizational")] 
        public List<PlotElement> Organizational;

        /// <summary>
        /// Builds the Parent and Child List relationships between all plot elements.
        /// Needs to be run when database gets initialized.
        /// </summary>
        public void BuildTree()
        {
            Dictionary<int, PlotElement> table =
                Bools.Concat<PlotElement>(Ints)
                    .Concat(Floats).Concat(Conditionals)
                    .Concat(Transitions).Concat(Organizational)
                    .ToDictionary((e) => e.ElementId);

            foreach (var element in table)
            {
                var plot = element.Value;
                var parentId = plot.ParentElementId;
                if (parentId != 0)
                {
                    var parent = table[parentId];
                    plot.Parent = parent;
                    parent.Children.Add(plot);
                }
            }
        }
    }

    public class PlotDatabase
    {
        public Dictionary<int, PlotBool> Bools { get; set; } = new Dictionary<int, PlotBool>();
        public Dictionary<int, PlotElement> Ints { get; set; } = new Dictionary<int, PlotElement>();
        public Dictionary<int, PlotElement> Floats { get; set; } = new Dictionary<int, PlotElement>();
        public Dictionary<int, PlotConditional> Conditionals { get; set; } = new Dictionary<int, PlotConditional>();
        public Dictionary<int, PlotTransition> Transitions { get; set; } = new Dictionary<int, PlotTransition>();
        public Dictionary<int, PlotElement> Organizational { get; set; } = new Dictionary<int, PlotElement>();

        public MEGame refGame { get; set; }

        public bool IsBioware { get; set; }

        public PlotDatabase(MEGame refgame, bool isbioware)
        {
            this.refGame = refgame;
            this.IsBioware = isbioware;
        }

        public PlotDatabase()
        {

        }

        public void LoadPlotsFromJSON(MEGame game, bool loadBioware = true, string dpPath = null)
        {
            refGame = game;
            IsBioware = loadBioware;
            var pdb = new PlotDatabaseFile();
            string json;
            if (loadBioware)
            {
                json = LegendaryExplorerCoreUtilities.LoadStringFromCompressedResource("PlotDatabases.zip", LegendaryExplorerCoreLib.CustomPlotFileName(game));
            }
            else
            {
                if (dpPath == null)
                    return;
                StreamReader sr = new StreamReader(dpPath);
                json = sr.ReadToEnd();
            }
            pdb = JsonConvert.DeserializeObject<PlotDatabaseFile>(json, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore});
            pdb.BuildTree();

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
            var elements = Bools.Values.ToList()
                .Concat(Ints.Values.ToList())
                .Concat(Floats.Values.ToList())
                .Concat(Conditionals.Values.ToList())
                .Concat(Transitions.Values.ToList())
                .Concat(Organizational.Values.ToList())
                .ToDictionary(e => e.ElementId);

            return new SortedDictionary<int, PlotElement>(elements);
        }

        public bool CanSave() => !IsBioware;

        public void SaveDatabaseToFile(string folder)
        {
            if (!CanSave() || !Directory.Exists(folder))
                return;
            var dbPath = Path.Combine(folder, $"PlotDBMods{refGame}.json");

            var serializationObj = new PlotDatabaseFile();
            serializationObj.Bools = Bools.Values.ToList();
            serializationObj.Ints = Ints.Values.ToList();
            serializationObj.Floats = Floats.Values.ToList();
            serializationObj.Conditionals = Conditionals.Values.ToList();
            serializationObj.Transitions = Transitions.Values.ToList();
            serializationObj.Organizational = Organizational.Values.ToList();

            var json = JsonConvert.SerializeObject(serializationObj);

            File.WriteAllText(dbPath, json);
        }

        public int GetNextElementId()
        {
            var maxElement = 1;

            foreach(var kvp in Bools)
            {
                if(kvp.Value.ElementId > maxElement)
                {
                    maxElement = kvp.Value.ElementId;
                }
            }
            foreach (var kvp in Ints)
            {
                if (kvp.Value.ElementId > maxElement)
                {
                    maxElement = kvp.Value.ElementId;
                }
            }
            foreach (var kvp in Floats)
            {
                if (kvp.Value.ElementId > maxElement)
                {
                    maxElement = kvp.Value.ElementId;
                }
            }
            foreach (var kvp in Conditionals)
            {
                if (kvp.Value.ElementId > maxElement)
                {
                    maxElement = kvp.Value.ElementId;
                }
            }
            foreach (var kvp in Transitions)
            {
                if (kvp.Value.ElementId > maxElement)
                {
                    maxElement = kvp.Value.ElementId;
                }
            }
            foreach (var kvp in Organizational)
            {
                if (kvp.Value.ElementId > maxElement)
                {
                    maxElement = kvp.Value.ElementId;
                }
            }
            return maxElement + 1;
        }
    }
}
