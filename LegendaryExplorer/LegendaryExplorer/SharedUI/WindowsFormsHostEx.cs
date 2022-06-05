using System.Windows.Controls;
using System.Windows.Forms.Integration;

namespace LegendaryExplorer.SharedUI
{
    public class WindowsFormsHostEx : WindowsFormsHost
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            //WindowsFormsHost will sometimes stubbornly stick around in memory
            //this seems to be cured by severing it from its family
            (Parent as Panel)?.Children.Remove(this);
            Child = null;
        }
    }
}
