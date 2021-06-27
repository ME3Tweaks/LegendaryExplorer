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
    /// A temporary class to test serialization of the plot database json
    /// </summary>
    public class PlotDatabaseTemp
    {
        [JsonProperty("bools")]
        public List<PlotBool> Bools;

        [JsonProperty("ints")]
        public List<PlotInt> Ints;

        [JsonProperty("floats")]
        public List<PlotFloat> Floats;

        [JsonProperty("conditionals")]
        public List<PlotConditional> Conditionals;

        [JsonProperty("transitions")] 
        public List<PlotTransition> Transitions;

        [JsonProperty("organizational")] 
        public List<PlotElement> OrganizationalElements;

        public SortedDictionary<int, PlotElement> BuildTree()
        {
            Dictionary<int, PlotElement> table =
                Bools.Concat<PlotElement>(Ints)
                    .Concat(Floats).Concat(Conditionals)
                    .Concat(Transitions).Concat(OrganizationalElements)
                    .ToDictionary((e) => e.PlotElementId);

            foreach (var element in table)
            {
                var plot = element.Value;
                var parentId = plot.ParentPlotId;
                if (parentId != 0)
                {
                    var parent = table[parentId];
                    plot.Parent = parent;
                    parent.Children.Add(plot);
                    if (!String.IsNullOrEmpty(plot.Action))
                    {
                        var actionElements = plot.Action.Split(".");
                        plot.TreeText = actionElements.Last();
                    }

                }
            }

            return new SortedDictionary<int, PlotElement>(table);
        }

    }

    public class PlotDatabase
    {
        public SortedDictionary<int, PlotElement> Elements { get; set; } = new SortedDictionary<int, PlotElement>();

        public MEGame refGame { get; set; }

        public bool IsBioware { get; set; }

        public PlotDatabase(SortedDictionary<int, PlotElement> elements, MEGame refgame, bool isbioware)
        {
            this.Elements = elements;
            this.refGame = refgame;
            this.IsBioware = isbioware;
        }

        public PlotDatabase()
        {

        }

        public void LoadPlotsFromJSON( MEGame game, bool loadBioware = true)
        {
            refGame = game;
            IsBioware = loadBioware;
            var pdb = new PlotDatabaseTemp();
            string json;
            if (loadBioware)
            {
                json = LegendaryExplorerCoreUtilities.LoadStringFromCompressedResource("PlotDatabases.zip", LegendaryExplorerCoreLib.CustomPlotFileName(game));
            }
            else
            {
                json = "{}"; //TO DO add load non-bioware modders asset
            }
            pdb = JsonConvert.DeserializeObject<PlotDatabaseTemp>(json);
            Elements = pdb.BuildTree();
        }
    }
}
