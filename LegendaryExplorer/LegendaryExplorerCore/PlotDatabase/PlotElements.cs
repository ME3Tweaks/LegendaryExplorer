using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
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
        Float = 14,
        Mod = 15,
        Category = 16
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
            PlotElementType.State => "A linear plot state (true or false)",
            PlotElementType.SubState => "A sub-flag or sub-state (either first or many trigger a state change)",
            PlotElementType.Conditional => "A boolean expression used to test a game state or set of states",
            PlotElementType.Consequence => "A set of actions to take upon state change",
            PlotElementType.Transition => "A method to call to set the value of various states",
            PlotElementType.JournalGoal => "The primary goal of an entire quest",
            PlotElementType.JournalTask => "A single task in a quest",
            PlotElementType.JournalItem => "A plot item",
            PlotElementType.Integer => "An integer",
            PlotElementType.Float => "A floating point number",
            PlotElementType.Mod => "A category for items for a single mod or modder",
            PlotElementType.Category => "A category of various states",
            _ => throw new ArgumentOutOfRangeException($"Unexpected plot type: ${plotType}")
        };
    }

    [DebuggerDisplay("{Type} {PlotId}: {Path}")]
    public class PlotElement : INotifyPropertyChanged
    {
        [JsonProperty("plotid")]
        public int PlotId { get; set; }

        [JsonProperty("elementid")]
        public int ElementId { get; set; }

        [JsonProperty("parentelementid")]
        public int ParentElementId { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("sequence")]
        public float Sequence { get; set; }

        [JsonProperty("type")]
        public PlotElementType Type { get; set; }

        [JsonIgnore]
        public PlotElement Parent { get; set; }

        [JsonIgnore] public ObservableCollectionExtended<PlotElement> Children { get; } = new ();

        [JsonIgnore]
        public string Path
        {
            get
            {
                var path = new StringBuilder();
                PlotElement el = this;
                path.Insert(0, el.Label);
                while (el.ParentElementId > 0 && el.Parent != null)
                {
                    el = el.Parent;
                    path.Insert(0, ".");
                    path.Insert(0, el.Label);
                }
                return path.ToString();
            }
        }

        [JsonIgnore]
        public int RelevantId => PlotId <= 0 ? ElementId : PlotId;

        [JsonIgnore]
        public bool IsAGameState
        {
            get
            {
                switch (Type)
                {
                    case PlotElementType.Conditional:
                    case PlotElementType.Consequence:
                    case PlotElementType.Flag:
                    case PlotElementType.Float:
                    case PlotElementType.Integer:
                    case PlotElementType.JournalGoal:
                    case PlotElementType.JournalItem:
                    case PlotElementType.JournalTask:
                    case PlotElementType.State:
                    case PlotElementType.SubState:
                    case PlotElementType.Transition:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public PlotElement()
        { }

        public PlotElement(int plotid, int elementid, string label, PlotElementType type, int parentelementId, List<PlotElement> children = null)
        {
            PlotId = plotid;
            ElementId = elementid;
            Label = label;
            Type = type;
            ParentElementId = parentelementId;
            Children.AddRange(children ?? new List<PlotElement>());
        }

        public PlotElement(int plotid, int elementid, string label, PlotElementType type, PlotElement parent, List<PlotElement> children = null)
            : this(plotid, elementid, label, type, -1, children)
        {
            AssignParent(parent);
        }

        public void AssignParent(PlotElement parent)
        {
            if (Parent != parent && Parent != null)
            {
                RemoveFromParent();
            }
            Parent = parent;
            ParentElementId = parent?.ElementId ?? -1;
            if (Parent != null && !Parent.Children.Contains(this))
            {
                parent?.Children.Add(this);
            }
        }

        public bool RemoveFromParent()
        {
            if (Parent != null)
            {
                Parent.Children.RemoveAll((i) => i == this);
                Parent = null;
                ParentElementId = -1;
                return true;
            }

            return false;
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

    }

    public class PlotBool : PlotElement
    {
        [JsonProperty("subtype")]
        public PlotElementType? SubType { get; set; }

        [JsonProperty("gamervariable")]
        public int? GamerVariable { get; set; }

        [JsonProperty("achievementid")]
        public int? AchievementID { get; set; }

        [JsonProperty("galaxyatwar")]
        public int? GalaxyAtWar { get; set; }

        PlotBool()
        {

        }

        public PlotBool(int plotid, int elementid, string label, PlotElementType type, PlotElement parent, List<PlotElement> children = null)
            : base(plotid, elementid, label, type, parent, children) { }
    }

    public class PlotConditional : PlotElement
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        PlotConditional()
        {

        }

        public PlotConditional(int plotid, int elementid, string label, PlotElementType type, PlotElement parent, List<PlotElement> children = null)
            : base(plotid, elementid, label, type, parent, children) { }
    }

    public class PlotTransition : PlotElement
    {
        [JsonProperty("argument")]
        public string Argument { get; set; }

        PlotTransition()
        {

        }

        public PlotTransition(int plotid, int elementid, string label, PlotElementType type, PlotElement parent, List<PlotElement> children = null)
            : base(plotid, elementid, label, type, parent, children) { }
    }
}
