using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Dialog that has copy button, designed for showing lists of short lines of text
    /// </summary>
    public partial class ListDialog : TrackingNotifyPropertyChangedWindowBase
    {
        public ObservableCollectionExtended<object> Items { get; } = new ObservableCollectionExtended<object>();
        public Action<EntryStringPair> DoubleClickEntryHandler { get; set; }
        public Action<EntryRefAndMessage> DoubleClickEntryHandler2 { get; set; }
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
        }


        public ListDialog(IEnumerable<EntryRefAndMessage> listItems, string title, string message, Window owner, int width = 0, int height = 0) : this(title, message, owner, width, height)
        {
            Items.ReplaceAll(listItems);
            TopText = message;
        }


        public ListDialog(IEnumerable<EntryStringPair> listItems, string title, string message, Window owner, int width = 0, int height = 0) : this(title, message, owner, width, height)
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

        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            switch (((FrameworkElement)e.OriginalSource).DataContext)
            {
                case EntryStringPair {Entry: not null} item:
                {
                    if (DoubleClickEntryHandler == null)
                    {
                        MessageBox.Show("This dialog doesn't support double click to goto yet, please report this");
                    }
                    else
                    {
                        DoubleClickEntryHandler.Invoke(item);
                    }

                    break;
                }
                case EntryRefAndMessage item2:
                    if (DoubleClickEntryHandler2 == null)
                    {
                        MessageBox.Show("This dialog doesn't support double click to goto yet, please report this");
                    }
                    else
                    {
                        DoubleClickEntryHandler2.Invoke(item2);
                    }

                    break;
            }
        }
    }

    public record EntryRefAndMessage(int UIndex, string FilePath, string Message)
    {
        public EntryRefAndMessage(IEntry entry, string message = null) : this(entry?.UIndex ?? 0, entry?.FileRef.FilePath ?? null, message ?? $"{$"#{entry.UIndex}",-9} {entry.FileRef.FilePath}")
        {
        }

        public override string ToString() => Message;
    };
}