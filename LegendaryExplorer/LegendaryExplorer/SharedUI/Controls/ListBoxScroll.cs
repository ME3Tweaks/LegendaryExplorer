using System.Windows.Controls;

namespace LegendaryExplorer.SharedUI.Controls
{
    public class ListBoxScroll : ListBox
    {
        public ListBoxScroll()
        {
            SelectionChanged += ListBoxScroll_SelectionChanged;
        }

        void ListBoxScroll_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ScrollIntoView(SelectedItem);
        }
    }
}
