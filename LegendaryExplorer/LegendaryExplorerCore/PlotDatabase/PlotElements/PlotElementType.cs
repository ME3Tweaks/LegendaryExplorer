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
}