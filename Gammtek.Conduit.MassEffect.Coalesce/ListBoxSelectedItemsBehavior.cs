using System.Windows.Controls;
using System.Windows.Interactivity;

namespace Gammtek.Conduit.MassEffect.Coalesce
{
	public class ListBoxSelectedItemsBehavior : Behavior<ListBox>
	{
		protected override void OnAttached()
		{
			AssociatedObject.SelectionChanged += AssociatedObjectSelectionChanged;
		}

		protected override void OnDetaching()
		{
			AssociatedObject.SelectionChanged -= AssociatedObjectSelectionChanged;
		}

		private void AssociatedObjectSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// Assuming your selection mode is single.
			if (e.AddedItems.Count > 0)
			{
				AssociatedObject.ScrollIntoView(e.AddedItems[0]);
			}
		}
	}
}
