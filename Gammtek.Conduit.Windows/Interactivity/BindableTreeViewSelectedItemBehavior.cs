using System.Windows;
using System.Windows.Controls;
using System.Windows.Interactivity;
using System.Windows.Media;

namespace Gammtek.Conduit.Windows.Interactivity
{
	public class BindableTreeViewSelectedItemBehavior : Behavior<TreeView>
	{
		public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
			"SelectedItem", typeof (object), typeof (BindableTreeViewSelectedItemBehavior),
			new UIPropertyMetadata(null, OnSelectedItemChanged));

		public object SelectedItem
		{
			get { return GetValue(SelectedItemProperty); }
			set { SetValue(SelectedItemProperty, value); }
		}

		private static void OnSelectedItemChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			var tvi = e.NewValue as TreeViewItem;

			if (tvi == null)
			{
				var tree = ((BindableTreeViewSelectedItemBehavior) sender).AssociatedObject;
				tvi = GetTreeViewItem(tree, e.NewValue);
			}

			if (tvi == null)
			{
				return;
			}

			tvi.IsSelected = true;
			tvi.Focus();
		}

		private void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			SelectedItem = e.NewValue;
		}

		private static TreeViewItem GetTreeViewItem(ItemsControl container, object item)
		{
			if (container == null)
			{
				return null;
			}

			if (container.DataContext == item)
			{
				return container as TreeViewItem;
			}

			// Expand the current container
			if (container is TreeViewItem && !((TreeViewItem) container).IsExpanded)
			{
				container.SetValue(TreeViewItem.IsExpandedProperty, true);
			}

			// Try to generate the ItemsPresenter and the ItemsPanel.
			// by calling ApplyTemplate.  Note that in the 
			// virtualizing case even if the item is marked 
			// expanded we still need to do this step in order to 
			// regenerate the visuals because they may have been virtualized away.

			container.ApplyTemplate();

			var itemsPresenter = (ItemsPresenter) container.Template.FindName("ItemsHost", container);

			if (itemsPresenter != null)
			{
				itemsPresenter.ApplyTemplate();
			}
			else
			{
				// The Tree template has not named the ItemsPresenter, 
				// so walk the descendents and find the child.
				itemsPresenter = FindVisualChild<ItemsPresenter>(container);

				if (itemsPresenter == null)
				{
					container.UpdateLayout();
					itemsPresenter = FindVisualChild<ItemsPresenter>(container);
				}
			}

			var itemsHostPanel = (Panel) VisualTreeHelper.GetChild(itemsPresenter, 0);

			// Ensure that the generator for this panel has been created.
#pragma warning disable 168
			// ReSharper disable UnusedVariable
			var children = itemsHostPanel.Children;
			// ReSharper restore UnusedVariable
#pragma warning restore 168

			for (int i = 0, count = container.Items.Count; i < count; i++)
			{
				var subContainer = (TreeViewItem) container.ItemContainerGenerator.ContainerFromIndex(i);

				if (subContainer == null)
				{
					continue;
				}

				subContainer.BringIntoView();

				// Search the next level for the object.
				var resultContainer = GetTreeViewItem(subContainer, item);

				if (resultContainer != null)
				{
					return resultContainer;
				}
			}

			return null;
		}

		/// <summary>
		///     Search for an element of a certain type in the visual tree.
		/// </summary>
		/// <typeparam name="T">The type of element to find.</typeparam>
		/// <param name="visual">The parent element.</param>
		/// <returns></returns>
		private static T FindVisualChild<T>(DependencyObject visual) where T : Visual
		{
			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(visual); i++)
			{
				var child = (Visual) VisualTreeHelper.GetChild(visual, i);

				if (child == null)
				{
					continue;
				}

				var correctlyTyped = child as T;

				if (correctlyTyped != null)
				{
					return correctlyTyped;
				}

				var descendent = FindVisualChild<T>(child);

				if (descendent != null)
				{
					return descendent;
				}
			}

			return null;
		}

		protected override void OnAttached()
		{
			base.OnAttached();

			AssociatedObject.SelectedItemChanged += OnTreeViewSelectedItemChanged;
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			if (AssociatedObject != null)
			{
				AssociatedObject.SelectedItemChanged -= OnTreeViewSelectedItemChanged;
			}
		}
	}
}
