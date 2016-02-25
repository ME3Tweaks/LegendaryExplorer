using System.Collections.Generic;
using System.Windows.Forms;
using MassEffect3.SaveEdit.Properties;

namespace MassEffect3.SaveEdit.BasicTable
{
	internal static class Resources
	{
		public static List<BasicTableItem> Build(Editor editor)
		{
			return new List<BasicTableItem>
			{
				new BasicTableItem
				{
					Name = Localization.Editor_BasicTable_Resources_Credits,
					Control = new NumericUpDown
					{
						Minimum = int.MinValue,
						Maximum = int.MaxValue,
						Increment = 1,
						Dock = DockStyle.Fill
					},
					Binding =
						new Binding("Value",
							editor._rootSaveFileBindingSource,
							"Player.Credits",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new BasicTableItem
				{
					Name = Localization.Editor_BasicTable_Resources_Medigel,
					Control = new NumericUpDown
					{
						Minimum = int.MinValue,
						Maximum = int.MaxValue,
						Increment = 1,
						Dock = DockStyle.Fill
					},
					Binding =
						new Binding("Value",
							editor._rootSaveFileBindingSource,
							"Player.Medigel",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new BasicTableItem
				{
					Name = Localization.Editor_BasicTable_Resources_ExtraMedigel,
					Control = new NumericUpDown
					{
						Minimum = int.MinValue,
						Maximum = int.MaxValue,
						Increment = 1,
						Dock = DockStyle.Fill
					},
					Binding =
						new Binding("Value",
							editor._rootSaveFileBindingSource,
							"Plot.Helpers.ExtraMedigel",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new BasicTableItem
				{
					Name = Localization.Editor_BasicTable_Resources_Grenades,
					Control = new NumericUpDown
					{
						Minimum = int.MinValue,
						Maximum = int.MaxValue,
						Increment = 1,
						Dock = DockStyle.Fill
					},
					Binding =
						new Binding("Value",
							editor._rootSaveFileBindingSource,
							"Player.Grenades",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				}
			};
		}
	}
}
