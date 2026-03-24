using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.Misc;

namespace LegendaryExplorer.Dialogs
{
    /// <summary>
    /// Dialog that has copy button, designed for showing lists of short lines of text
    /// </summary>
    public partial class ListDialog : TrackingNotifyPropertyChangedWindowBase
    {
        public ObservableCollectionExtended<object> Items { get; } = new();
        // Backwards-compatible handler for EntryStringPair items
        public Action<EntryStringPair> DoubleClickEntryHandler { get; set; }
        // General-purpose handler for arbitrary list items (strings, etc.)
        public Action<object> DoubleClickItemHandler { get; set; }
        private string topText;

        public string TopText
        {
            get => topText;
            set => SetProperty(ref topText, value);
        }

        private ListDialog(string title, string message, Window owner, int width = 0, int height = 0) : base("List Dialog", false)
        {
            DataContext = this;
            InitializeComponent();
            Title = title;
            if (width != 0)
            {
                Width = width;
            }
            if (height != 0)
            {
                Height = height;
            }
            Owner = owner;
            TopText = message;
        }

        public ListDialog(IEnumerable<EntryStringPair> listItems, string title, string message, Window owner, int width = 0, int height = 0) : this(title, message, owner, width, height)
        {
            Items.ReplaceAll(listItems);
        }

        public ListDialog(IEnumerable<string> listItems, string title, string message, Window owner, int width = 0, int height = 0) : this(title, message, owner, width, height)
        {
            Items.ReplaceAll(listItems);
        }

        private void CopyItemsToClipBoard_Click(object sender, RoutedEventArgs e)
        {
            string toClipboard = string.Join("\n", Items);
            try
            {
                Clipboard.SetText(toClipboard);
                ListDialog_Status.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                //yes, this actually happens sometimes...
                MessageBox.Show("Could not set data to clipboard:\n" + ex.Message);
            }
        }

        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var ctx = ((FrameworkElement)e.OriginalSource).DataContext;
            if (ctx is EntryStringPair esp && (esp.Entry is not null || esp.Openable is not null))
            {
                if (DoubleClickEntryHandler == null)
                {
                    MessageBox.Show("This dialog doesn't support double click to goto yet, please report this");
                }
                else
                {
                    DoubleClickEntryHandler.Invoke(esp);
                }
            }
            else if (ctx != null)
            {
                if (DoubleClickItemHandler == null)
                {
                    // No-op: dialog may be used just for display
                }
                else
                {
                    DoubleClickItemHandler.Invoke(ctx);
                }
            }
        }
    }
}
