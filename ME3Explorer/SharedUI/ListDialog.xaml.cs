using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ME3Explorer.Packages;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Dialog that has copy button, designed for showing lists of short lines of text
    /// </summary>
    public partial class ListDialog : NotifyPropertyChangedWindowBase
    {
        public ObservableCollectionExtended<object> Items { get; } = new ObservableCollectionExtended<object>();
        public Action<EntryItem> DoubleClickEntryHandler { get; set; }
        private string topText;

        public string TopText
        {
            get => topText;
            set => SetProperty(ref topText, value);
        }

        private ListDialog(string title, string message, Window owner, int width = 0, int height = 0)
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
        }


        public ListDialog(IEnumerable<EntryItem> listItems, string title, string message, Window owner, int width = 0, int height = 0) : this(title, message, owner, width, height)
        {
            Items.ReplaceAll(listItems);
            TopText = message;
        }

        public ListDialog(IEnumerable<string> listItems, string title, string message, Window owner, int width = 0, int height = 0) : this(title, message, owner, width, height)
        {
            Items.ReplaceAll(listItems);
            TopText = message;
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

        /// <summary>
        /// Class used for associating an item in the dialog with an entry. This object will be passed through the
        /// double click handler, if one is assigned.
        /// </summary>
        public class EntryItem
        {
            public string Message { get; }
            public IEntry ReferencedEntry { get; }

            public EntryItem(IEntry entry, string message)
            {
                Message = message;
                ReferencedEntry = entry;
            }

            public override string ToString() => Message;

            public static implicit operator EntryItem(ExportEntry entry)
            {
                return new EntryItem(entry, $"{$"#{entry.UIndex}",-9} {entry.FileRef.FilePath}");
            }
        }

        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = ((FrameworkElement)e.OriginalSource).DataContext as EntryItem;
            if (item != null && item.ReferencedEntry != null)
            {
                if (DoubleClickEntryHandler == null)
                {
                    MessageBox.Show("This dialog doesn't support double click to goto yet, please report this");
                }
                else
                {
                    DoubleClickEntryHandler.Invoke(item);
                }
            }
        }
    }
}
