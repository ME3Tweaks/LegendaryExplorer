using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase
{
    /// <summary>
    /// The type of a plot element, as found in the BioWare Plot Databases
    /// </summary>
    public enum PlotElementType : int
    {
        None = -1,
        Region = 1,
        Plot = 2,
        FlagGroup = 3,
        Flag = 4,
        State = 5,
        SubState = 6,
        Conditional = 7,
        Consequence = 8,
        Transition = 9,
        JournalGoal = 10,
        JournalTask = 11,
        JournalItem = 12,
        Integer = 13,
        Float = 14

    }

    public static class PlotElementTypeExtensions
    {
        public static string GetDescription(this PlotElementType plotType) => plotType switch
        {
            PlotElementType.None => "No plot element type set",
            PlotElementType.Region => "A preset region of the game",
            PlotElementType.Plot => "A plot or sub-plot",
            PlotElementType.FlagGroup => "A group of flags",
            PlotElementType.Flag => "A flag",
            PlotElementType.State => "A linear plot state",
            PlotElementType.SubState => "A sub-flag or sub-state",
            PlotElementType.Conditional => "A boolean expression used to test a game state or set of states",
            PlotElementType.Consequence => "A set of actions to take upon state change",
            PlotElementType.Transition => "A method to call to set the value of various states",
            PlotElementType.JournalGoal => "The primary goal of an entire quest",
            PlotElementType.JournalTask => "A single task in a quest",
            PlotElementType.JournalItem => "A plot item",
            PlotElementType.Integer => "An integer",
            PlotElementType.Float => "A floating point number",
            _ => throw new ArgumentOutOfRangeException($"Unexpected plot type: ${plotType}")
        };
    }

    [DebuggerDisplay("{Type} {PlotID}: {Path}")]
    public class PlotElement
    {
        [JsonProperty("plotid")]
        public int PlotID;

        [JsonProperty("elementid")]
        public int ElementId;

        [JsonProperty("parentelementid")]
        public int ParentElementId;

        [JsonProperty("label")]
        public string Label;

        [JsonProperty("sequence")] 
        public float Sequence;

        [JsonProperty("type")]
        public PlotElementType Type;

        [JsonIgnore]
        public PlotElement Parent;

        [JsonIgnore]
        public List<PlotElement> Children = new List<PlotElement>();

        public string Path
        {
            get
            {
                var path = new StringBuilder();
                PlotElement el = this;
                path.Insert(0, el.Label);
                while (el.Parent != null)
                {
                    el = el.Parent;
                    path.Insert(0, ".");
                    path.Insert(0, el.Label);
                }
                return path.ToString();
            }
        }

    }

    public class PlotBool : PlotElement
    {
        [JsonProperty("subtype")] 
        public PlotElementType? SubType;

        [JsonProperty("gamervariable")]
        public int? GamerVariable;

        [JsonProperty("achievementid")]
        public int? AchievementID;

        [JsonProperty("galaxyatwar")]
        public int? GalaxyAtWar;
    }

    public class PlotConditional : PlotElement
    {
        [JsonProperty("code")] 
        public string Code;
    }

    public class PlotTransition : PlotElement
    {
        [JsonProperty("argument")] 
        public string Argument;
    }
}
