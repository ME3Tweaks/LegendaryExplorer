using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Tools.ObjectReferenceViewer
{
    /// <summary>
    /// Interaction logic for ObjectReferenceViewer.xaml
    /// </summary>
    public partial class ObjectReferenceViewerWindow : NotifyPropertyChangedWindowBase
    {
        private Action<EntryStringPair> NavigateToEntry { get; set; }

        /// <summary>
        /// Object we are viewing references of
        /// </summary>
        private readonly IEntry Entry;

        private bool _isBusy = true;

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public ObservableCollectionExtended<ReferenceTreeWPF> TreeRoot { get; } = [];

        private ReferenceTreeWPF _selectedItem;

        public ReferenceTreeWPF SelectedItem
        {
            get => _selectedItem;
            set
            {
                // Some weird oddity exists in TreeView WPF where it selects the node twice when expanding stuff
                // and it makes first selection sometimes reset to nothing.
                // This is hack to make it not do that.

                // only allow selecting a null tree entry if there is no package loaded
                bool allowSelection = !TreeRoot.IsEmpty() && value != null;
                if (!allowSelection && TreeRoot.IsEmpty()) allowSelection = true;

                if (allowSelection && SetProperty(ref _selectedItem, value) && value != null)
                {
                    NavigateToEntry?.Invoke(new EntryStringPair(value.Entry));
                }
            }
        }

        public ObjectReferenceViewerWindow(IEntry obj, Action<EntryStringPair> navigateToDelegate)
        {
            Entry = obj;
            NavigateToEntry = navigateToDelegate;

            InitializeComponent();

            Title = $"Reference graph for {obj.InstancedFullPath}";
        }

        private void ObjectReferenceViewerWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            Task.Run(() => ReferenceTreeWPF.CalculateReferenceTree(Entry))
                .ContinueWithOnUIThread(tree =>
                {
                    IsBusy = false;
                    var root = tree.Result;
                    TreeRoot.Add(root);
                    root.IsExpanded = true;
                });
        }

        private void NavigateToHigherBranch_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: ReferenceTreeWPF { HigherLevelRef: ReferenceTreeWPF higherLevelRef } })
            {
                e.Handled = true;
                SelectedItem = higherLevelRef;
            }
        }
    }
}