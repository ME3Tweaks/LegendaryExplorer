using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using LegendaryExplorer.Tools.ObjectReferenceViewer;
using Microsoft.Xaml.Behaviors;

// From https://stackoverflow.com/questions/183636/selecting-a-node-in-virtualized-treeview-with-wpf?answertab=votes#tab-top

namespace LegendaryExplorer.SharedUI.PeregrineTreeView
{
    public class NodeReferenceTreeWPFSelectionBehavior : Behavior<TreeView>
    {
        public ReferenceTreeWPF SelectedItem
        {
            get => (ReferenceTreeWPF)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(ReferenceTreeWPF), typeof(NodeReferenceTreeWPFSelectionBehavior),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ReferenceTreeWPF oldNode)
            {
                oldNode.IsSelected = false;
            }

            if (e.NewValue is not ReferenceTreeWPF newNode) return;


            var behavior = (NodeReferenceTreeWPFSelectionBehavior)d;
            var tree = behavior.AssociatedObject;

            var nodeDynasty = new List<ReferenceTreeWPF> { newNode };
            var parent = newNode.Parent;
            while (parent != null)
            {
                nodeDynasty.Insert(0, parent);
                parent = parent.Parent;
            }

            var currentParent = (ItemsControl)tree;
            foreach (var node in nodeDynasty)
            {
                // first try the easy way
                var newParent = currentParent.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
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

                    var virtualizingPanel = GetItemsHost(currentParent) as VirtualizingPanel;
                    CallEnsureGenerator(virtualizingPanel);
                    int index = currentParent.Items.IndexOf(node);
                    if (index < 0)
                    {
                        throw new InvalidOperationException("Node '" + node + "' cannot be fount in container");
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

                if (node == newNode)
                {
                    newParent.IsSelected = true;
                    newParent.BringIntoView();
                    break;
                }

                newParent.IsExpanded = true;
                currentParent = newParent;
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
            SelectedItem = e.NewValue as ReferenceTreeWPF;
        }

        #region Functions to get internal members using reflection

        // Some functionality we need is hidden in internal members, so we use reflection to get them

        #region ItemsControl.ItemsHost

        private static Panel GetItemsHost(ItemsControl itemsControl)
        {
            Debug.Assert(itemsControl != null);
            return ItemsHost(itemsControl);

            [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_ItemsHost")]
            static extern Panel ItemsHost(ItemsControl itemsControlp);
        }

        #endregion ItemsControl.ItemsHost

        #region Panel.EnsureGenerator

        private static void CallEnsureGenerator(Panel panel)
        {
            Debug.Assert(panel != null);
            EnsureGenerator(panel);
            return;

            [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "EnsureGenerator")]
            static extern void EnsureGenerator(Panel panel);
        }

        #endregion Panel.EnsureGenerator

        #endregion Functions to get internal members using reflection
    }
}