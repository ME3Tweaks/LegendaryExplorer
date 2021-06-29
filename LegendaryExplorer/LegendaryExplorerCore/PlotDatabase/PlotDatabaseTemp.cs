using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Helpers;
using Newtonsoft.Json;

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

        public void LoadPlotsFromJSON(MEGame game, bool loadBioware = true)
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
                json = "{}"; //TO DO add load non-bioware modders asset
            }

            pdb = JsonConvert.DeserializeObject<PlotDatabaseFile>(json);
            pdb.BuildTree();

            Bools = pdb.Bools.ToDictionary((b) => b.PlotID);
            Ints = pdb.Ints.ToDictionary((b) => b.PlotID);
            Floats = pdb.Floats.ToDictionary((b) => b.PlotID);
            Conditionals = pdb.Conditionals.ToDictionary((b) => b.PlotID);
            Transitions = pdb.Transitions.ToDictionary((b) => b.PlotID);
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

    }
}
