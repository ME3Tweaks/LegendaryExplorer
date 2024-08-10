using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using Microsoft.Xaml.Behaviors;

// From https://stackoverflow.com/questions/183636/selecting-a-node-in-virtualized-treeview-with-wpf?answertab=votes#tab-top

namespace LegendaryExplorer.SharedUI.PeregrineTreeView
{
    public class TreeSelectionBehavior : Behavior<TreeView>
    {
        public ITreeItem SelectedItem
        {
            get => (ITreeItem)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register("SelectedItem", typeof(ITreeItem), typeof(TreeSelectionBehavior),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ITreeItem oldNode)
            {
                oldNode.IsSelected = false;
            }

            if (e.NewValue is ITreeItem newNode)
            {
                var behavior = (TreeSelectionBehavior)d;
                var tree = behavior.AssociatedObject;

                var nodeDynasty = new List<ITreeItem> { newNode };
                var parent = newNode.Parent;
                while (parent != null)
                {
                    nodeDynasty.Insert(0, parent);
                    parent = parent.Parent;
                }

                var currentParent = tree as ItemsControl;
                foreach (var node in nodeDynasty)
                {
                    // first try the easy way
                    var newParent = currentParent.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
                    var index = 0;
                    VirtualizingPanel virtualizingPanel = null;
                    if (newParent == null)
                    {
                        // if this failed, it's probably because of virtualization, and we will have to do it the hard way.
                        // this code is influenced by TreeViewItem.ExpandRecursive decompiled code, and the MSDN sample at http://code.msdn.microsoft.com/Changing-selection-in-a-6a6242c8/sourcecode?fileId=18862&pathId=753647475
                        // see also the question at http://stackoverflow.com/q/183636/46635
                        currentParent.ApplyTemplate();
                        var itemsPresenter = (ItemsPresenter)currentParent.Template.FindName("ItemsHost", currentParent);
                        if (itemsPresenter != null)
                        {
                            itemsPresenter.ApplyTemplate();
                        }
                        else
                        {
                            currentParent.UpdateLayout();
                        }

                        virtualizingPanel = GetItemsHost(currentParent) as VirtualizingPanel;
                        CallEnsureGenerator(virtualizingPanel);
                        index = currentParent.Items.IndexOf(node);
                        if (index < 0)
                        {
                            throw new InvalidOperationException("Node '" + node + "' cannot be found in container");
                        }
                        if (virtualizingPanel != null)
                        {
                            //This can cause an exception still (InvalidOperationException) if content generation is in progress. 
                            //Will have to figure out how to deal with it.
                            try
                            {
                                virtualizingPanel.BringIndexIntoViewPublic(index);
                            }
                            catch
                            {
                                //This seems to be an internal exception
                            }
                        }
                        newParent = currentParent.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItem;
                        if (newParent == null)
                        {
                            currentParent.UpdateLayout();
                            try
                            {
                                virtualizingPanel.BringIndexIntoViewPublic(index);
                            }
                            catch
                            {
                                //This seems to be an internal exception
                                return; //?
                            }
                            newParent = currentParent.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItem;
                        }
                    }

                    if (newParent == null)
                    {
                        return;
                        //throw new InvalidOperationException("Tree view item cannot be found or created for node '" + node + "'");
                    }

                    if (node != null && node.Equals(newNode))
                    {
                        newParent.IsSelected = true;
                        newParent.BringIntoView();
                        break;
                    }

                    newParent.IsExpanded = true;
                    currentParent = newParent;
                }
            }
        }
        private bool _isCleanedUp;

        private void Cleanup()
        {
            if (!_isCleanedUp)
            {
                _isCleanedUp = true;
                AssociatedObject.SelectedItemChanged -= OnTreeViewSelectedItemChanged;
                AssociatedObject.Unloaded -= AssociatedObjectOnUnloaded;
            }
        }
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Unloaded += AssociatedObjectOnUnloaded;
            AssociatedObject.SelectedItemChanged += OnTreeViewSelectedItemChanged;
        }

        private void AssociatedObjectOnUnloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
        }

        protected override void OnDetaching()
        {
            Cleanup();
            base.OnDetaching();
        }

        private void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            SelectedItem = (ITreeItem)e.NewValue;
        }

        #region Functions to get internal members using reflection

        // Some functionality we need is hidden in internal members, so we use reflection to get them

        #region ItemsControl.ItemsHost

        static readonly PropertyInfo ItemsHostPropertyInfo = typeof(ItemsControl).GetProperty("ItemsHost", BindingFlags.Instance | BindingFlags.NonPublic);

        private static Panel GetItemsHost(ItemsControl itemsControl)
        {
            Debug.Assert(itemsControl != null);
            return ItemsHostPropertyInfo.GetValue(itemsControl, null) as Panel;
        }

        #endregion ItemsControl.ItemsHost

        #region Panel.EnsureGenerator

        private static readonly MethodInfo EnsureGeneratorMethodInfo = typeof(Panel).GetMethod("EnsureGenerator", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void CallEnsureGenerator(Panel panel)
        {
            Debug.Assert(panel != null);
            EnsureGeneratorMethodInfo.Invoke(panel, null);
        }

        #endregion Panel.EnsureGenerator

        #region VirtualizingPanel.BringIndexIntoView

        private static readonly MethodInfo BringIndexIntoViewMethodInfo = typeof(VirtualizingPanel).GetMethod("BringIndexIntoView", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void CallBringIndexIntoView(VirtualizingPanel virtualizingPanel, int index)
        {
            Debug.Assert(virtualizingPanel != null);
            BringIndexIntoViewMethodInfo.Invoke(virtualizingPanel, new object[] { index });
        }

        #endregion VirtualizingPanel.BringIndexIntoView

        #endregion Functions to get internal members using reflection
    }
}