using System.Collections.Generic;
using System.Windows.Controls;
using LegendaryExplorer.UserControls.SharedToolControls;

namespace LegendaryExplorer.SharedUI.Interfaces
{
    /// <summary>
    /// Interface that says the implementer supports recent files subsystem
    /// </summary>
    interface IRecents
    {
        /// <summary>
        /// Propogation receiver when recents in another window of the same type has been changed. This method should only be invoked by RecentsControl
        /// </summary>
        /// <param name="propogationToolSource">The tool name of the source, which can be used to accept or reject the propogation</param>
        /// <param name="newRecents">The new recents to set</param>
        public void PropogateRecentsChange(string propogationToolSource, IEnumerable<RecentsControl.RecentItem> newRecents);
        /// <summary>
        /// The tool name, which controls where the recents file is stored. Must be passed to the init method of the controller
        /// </summary>
        public string Toolname { get; }
    }
}
