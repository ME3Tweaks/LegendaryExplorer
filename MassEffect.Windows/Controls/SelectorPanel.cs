using System.Windows;
using System.Windows.Controls.Primitives;

namespace MassEffect.Windows.Controls
{
	public class SelectorPanel : Selector
	{
		static SelectorPanel()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof (SelectorPanel), new FrameworkPropertyMetadata(typeof (SelectorPanel)));
		}
	}
}
