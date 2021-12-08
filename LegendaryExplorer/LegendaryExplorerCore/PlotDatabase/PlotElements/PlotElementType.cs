using System;

namespace LegendaryExplorerCore.PlotDatabase.PlotElements
{
    /// <summary>
    /// Represents the type of a plot element, as found in the BioWare Plot Databases
    /// </summary>
    public enum PlotElementType : int
    {
        /// <summary>No plot element type set</summary>
        None = -1,
        /// <summary>A preset region of the game</summary>
        Region = 1,
        /// <summary>A plot or sub-plot</summary>
        Plot = 2,
        /// <summary>A group of flags</summary>
        FlagGroup = 3,
        /// <summary>A flag, represented in-game as a boolean plot variable</summary>
        Flag = 4,
        /// <summary>A linear plot state (true or false), represented in-game as a boolean plot variable</summary>
        State = 5,
        /// <summary>A sub-flag or sub-state (either first or many trigger a state change), represented in-game as a boolean plot variable</summary>
        SubState = 6,
        /// <summary>A boolean expression used to test a game state or set of states, represented in-game as a conditional function</summary>
        Conditional = 7,
        /// <summary>A set of actions to take upon state change, represented in-game as a plot transition</summary>
        Consequence = 8,
        /// <summary>A method to call to set the value of various states</summary>
        Transition = 9,
        /// <summary>The primary goal of an entire quest</summary>
        JournalGoal = 10,
        /// <summary>A single task in a quest</summary>
        JournalTask = 11,
        /// <summary>A plot item</summary>
        JournalItem = 12,
        /// <summary>An integer, represented in-game as an int plot variable</summary>
        Integer = 13,
        /// <summary>A floating point number, represented in-game as a float plot variable</summary>
        Float = 14,
        /// <summary>A category for elements for a single mod or modder</summary>
        Mod = 15,
        /// <summary>A category of various elements in a mod</summary>
        Category = 16
    }

    public static class PlotElementTypeExtensions
    {
        /// <summary>
        /// Gets a textual description of a PlotElementType, as found in the BioWare plot databases
        /// </summary>
        /// <param name="plotType">Type to get description of</param>
        /// <returns>Description</returns>
        /// <exception cref="ArgumentException">Plot type has no descriptions</exception>
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
            _ => throw new ArgumentException($"Unexpected plot type: ${plotType}")
        };
    }
}