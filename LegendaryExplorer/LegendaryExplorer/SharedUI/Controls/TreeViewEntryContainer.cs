using System.IO;
using System.Windows.Controls;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.UnrealScript.Documentation;

namespace LegendaryExplorer.SharedUI.Controls
{
    public class TreeViewEntryContainer : Border
    {
        public TreeViewEntryContainer() : base()
        {
#if DEBUG
            // debug only builds for now
            ToolTipOpening += OnToolTipOpening;
#endif
        }

        private void OnToolTipOpening(object sender, ToolTipEventArgs e)
        {
            if (DataContext is TreeViewEntry { Entry: not null } tve)
            {
                var db = LEXDocuDB.LoadDocuDB(tve.Entry.Game);
                if (db != null)
                {
                    var documentation = db.GetDocumentation(tve.Entry);
                    if (documentation != null)
                    {
                        ToolTip = documentation;
                        e.Handled = false; // open.
                        return;
                    }
                }
            }


            e.Handled = true; // Don't show
            return;
        }

    }
}
