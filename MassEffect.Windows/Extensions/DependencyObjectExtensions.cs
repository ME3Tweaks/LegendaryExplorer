using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace MassEffect.Windows.Extensions
{
	public static class DependencyObjectExtensions
	{
		public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject depObj) where T : DependencyObject
		{
			if (depObj == null)
			{
				yield break;
			}

			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
			{
				var child = VisualTreeHelper.GetChild(depObj, i);

				if (child is T)
				{
					yield return (T) child;
				}

				foreach (var childOfChild in FindVisualChildren<T>(child))
				{
					yield return childOfChild;
				}
			}
		}

		public static IEnumerable<T> FindLogicalChildren<T>(this DependencyObject depObj) where T : DependencyObject
		{
			if (depObj == null)
			{
				yield break;
			}

			foreach (var child in LogicalTreeHelper.GetChildren(depObj).OfType<DependencyObject>())
			{
				if (child is T)
				{
					yield return (T) child;
				}

				foreach (var childOfChild in FindLogicalChildren<T>(child))
				{
					yield return childOfChild;
				}
			}
		}

		public static DependencyObject FindVisualTreeRoot(this DependencyObject initial)
		{
			var current = initial;
			var result = initial;

			while (current != null)
			{
				result = current;

				if (current is Visual || current is Visual3D)
				{
					current = VisualTreeHelper.GetParent(current);
				}
				else
				{
					// If we're in Logical Land then we must walk 
					// up the logical tree until we find a 
					// Visual/Visual3D to get us back to Visual Land.
					current = LogicalTreeHelper.GetParent(current);
				}
			}

			return result;
		}

		public static T FindVisualAncestor<T>(this DependencyObject dependencyObject) where T : class
		{
			var target = dependencyObject;

			do
			{
				target = VisualTreeHelper.GetParent(target);
			} while (target != null && !(target is T));

			return target as T;
		}

		public static T FindLogicalAncestor<T>(this DependencyObject dependencyObject) where T : class
		{
			var target = dependencyObject;

			do
			{
				var current = target;
				target = LogicalTreeHelper.GetParent(target) ?? VisualTreeHelper.GetParent(current);
			} while (target != null && !(target is T));

			return target as T;
		}
	}
}
