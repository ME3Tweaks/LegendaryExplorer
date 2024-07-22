using FontAwesome5;
using System.Windows.Media;
using LegendaryExplorer.Misc;

namespace LegendaryExplorer.SharedUI.Controls
{
    /// <summary>
    /// An item that can be placed into a ListBox collection to be used as an indicator of tasks progressing (WPF only)
    /// Ensure the collection that these will be added to is collection synchronized if updating from a backgound thread.
    /// </summary>
    public class ListBoxTask : NotifyPropertyChangedBase
    {
        private string _header;
        public string Header { get => _header; set => SetProperty(ref _header, value); }

        private EFontAwesomeIcon _icon = EFontAwesomeIcon.Solid_Spinner;
        public EFontAwesomeIcon Icon { get => _icon; set => SetProperty(ref _icon, value); }

        private Brush _foreground = Brushes.Gray;
        public Brush Foreground { get => _foreground; set => SetProperty(ref _foreground, value); }

        public bool _spinning = true;
        public bool Spinning { get => _spinning; set => SetProperty(ref _spinning, value); }

        public ListBoxTask()
        {
        }

        public ListBoxTask(string header)
        {
            Header = header;
        }

        /// <summary>
        /// Completes this task
        /// </summary>
        /// <param name="newHeader">Header text to set</param>
        internal void Complete(string newHeader)
        {
            Header = newHeader;
            Icon = EFontAwesomeIcon.Solid_CheckSquare;
            Spinning = false;
            Foreground = Brushes.Green;
        }

        internal void Failed(string newHeader)
        {
            Header = newHeader;
            Icon = EFontAwesomeIcon.Solid_TimesCircle;
            Spinning = false;
            Foreground = Brushes.Red;
        }
    }
}
