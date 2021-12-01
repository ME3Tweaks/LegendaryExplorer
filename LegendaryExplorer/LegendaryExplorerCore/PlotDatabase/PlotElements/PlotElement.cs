using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.PlotElements
{
    /// <summary>
    /// Base class for all Plot Elements, implements notify property changed and can be serialized to JSON
    /// </summary>
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
        public int RelevantId
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
                    case PlotElementType.State:
                    case PlotElementType.SubState:
                    case PlotElementType.Transition:
                        return PlotId;
                    default:
                        return ElementId;
                }
            }
        }

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

        public void SetElementId(int id)
        {
            this.ElementId = id;
            foreach (var c in Children)
            {
                c.ParentElementId = id;
            }
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

    }
}
