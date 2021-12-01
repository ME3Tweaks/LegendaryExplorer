using System;

namespace LegendaryExplorerCore.PlotDatabase.PlotElements
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
}