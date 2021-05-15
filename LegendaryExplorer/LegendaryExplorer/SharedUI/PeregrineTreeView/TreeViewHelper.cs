using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace LegendaryExplorer.SharedUI.PeregrineTreeView
{
    public class TreeViewHelper : Behavior<TreeView>
    {
        public object BoundSelectedItem
        {
            get => GetValue(BoundSelectedItemProperty);
            set => SetValue(BoundSelectedItemProperty, value);
        }

        public static readonly DependencyProperty BoundSelectedItemProperty =
            DependencyProperty.Register("BoundSelectedItem",
                typeof(object),
                typeof(TreeViewHelper),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnBoundSelectedItemChanged));

        private static void OnBoundSelectedItemChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            //if (args.NewValue is TreeViewEntry item)
            //    item.IsSelected = true;
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.SelectedItemChanged += OnTreeViewSelectedItemChanged;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.SelectedItemChanged -= OnTreeViewSelectedItemChanged;
            base.OnDetaching();
        }

        private void OnTreeViewSelectedItemChanged(object obj, RoutedPropertyChangedEventArgs<object> args)
        {
            BoundSelectedItem = args.NewValue;
        }
    }
}