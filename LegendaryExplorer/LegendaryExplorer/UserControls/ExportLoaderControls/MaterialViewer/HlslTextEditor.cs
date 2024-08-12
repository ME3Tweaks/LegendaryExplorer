using LegendaryExplorer.Resources;
using LegendaryExplorer.UserControls.SharedToolControls;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.MaterialViewer;

public class HlslTextEditor : CodeEditorBase
{
    public HlslTextEditor()
    {
        SyntaxHighlighting = EmbeddedResources.HlslSyntaxDefinition;
        FontSize = 13;
    }
}