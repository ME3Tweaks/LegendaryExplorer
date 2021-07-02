using System.Windows.Controls;

namespace ME3Explorer.SharedUI
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
