using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    }
}
