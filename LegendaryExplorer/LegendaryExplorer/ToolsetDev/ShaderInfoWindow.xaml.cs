using System.Collections.Generic;
using System.Windows;
using LegendaryExplorer.ToolsetDev.ShaderComparer;

namespace LegendaryExplorer.ToolsetDev;

public partial class ShaderInfoWindow : Window
{
    public string LeftTitle { get; }
    public string LeftInstructionCount { get; }
    public List<ShaderInfoReader.ResourceBindingEntry> LeftEntries { get; }

    public string RightTitle { get; }
    public string RightInstructionCount { get; }
    public List<ShaderInfoReader.ResourceBindingEntry> RightEntries { get; }

    public ShaderInfoWindow(
        string leftTitle, List<ShaderInfoReader.ResourceBindingEntry> leftEntries, int leftInstructions,
        string rightTitle, List<ShaderInfoReader.ResourceBindingEntry> rightEntries, int rightInstructions)
    {
        LeftTitle = leftTitle;
        LeftEntries = leftEntries;
        LeftInstructionCount = $"Instructions: {leftInstructions}";

        RightTitle = rightTitle;
        RightEntries = rightEntries;
        RightInstructionCount = $"Instructions: {rightInstructions}";

        DataContext = this;
        InitializeComponent();
    }
}
