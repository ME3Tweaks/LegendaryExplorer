using System.Collections.Generic;
using System.Windows.Forms;
using MassEffect2.SaveEdit.Properties;

namespace MassEffect2.SaveEdit.BasicTable
{
	internal static class Resources
	{
		public static List<TableItem> Build(Editor editor)
		{
			return new List<TableItem>
			{
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Resources_Credits,
					Control = new NumericUpDown
					{
						Minimum = int.MinValue,
						Maximum = int.MaxValue,
						Increment = 1,
						Dock = DockStyle.Fill,
					},
					Binding =
						new Binding("Value",
							editor._RootSaveFileBindingSource,
							"Player.Credits",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Resources_Medigel,
					Control = new NumericUpDown
					{
						Minimum = int.MinValue,
						Maximum = int.MaxValue,
						Increment = 1,
						Dock = DockStyle.Fill,
					},
					Binding =
						new Binding("Value",
							editor._RootSaveFileBindingSource,
							"Player.Medigel",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Resources_ExtraMedigel,
					Control = new NumericUpDown
					{
						Minimum = int.MinValue,
						Maximum = int.MaxValue,
						Increment = 1,
						Dock = DockStyle.Fill,
					},
					Binding =
						new Binding("Value",
							editor._RootSaveFileBindingSource,
							"Plot.Helpers.ExtraMedigel",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Resources_ElementZero,
					Control = new NumericUpDown
					{
						Minimum = int.MinValue,
						Maximum = int.MaxValue,
						Increment = 1,
						Dock = DockStyle.Fill,
					},
					Binding =
						new Binding("Value",
							editor._RootSaveFileBindingSource,
							"Player.ElementZero",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Resources_Iridium,
					Control = new NumericUpDown
					{
						Minimum = int.MinValue,
						Maximum = int.MaxValue,
						Increment = 1,
						Dock = DockStyle.Fill,
					},
					Binding =
						new Binding("Value",
							editor._RootSaveFileBindingSource,
							"Player.Iridium",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Resources_Palladium,
					Control = new NumericUpDown
					{
						Minimum = int.MinValue,
						Maximum = int.MaxValue,
						Increment = 1,
						Dock = DockStyle.Fill,
					},
					Binding =
						new Binding("Value",
							editor._RootSaveFileBindingSource,
							"Player.Palladium",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Resources_Platinum,
					Control = new NumericUpDown
					{
						Minimum = int.MinValue,
						Maximum = int.MaxValue,
						Increment = 1,
						Dock = DockStyle.Fill,
					},
					Binding =
						new Binding("Value",
							editor._RootSaveFileBindingSource,
							"Player.Platinum",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Resources_Fuel,
					Control = new NumericUpDown
					{
						Minimum = int.MinValue,
						Maximum = int.MaxValue,
						Increment = 1,
						Dock = DockStyle.Fill,
					},
					Binding =
						new Binding("Value",
							editor._RootSaveFileBindingSource,
							"Player.CurrentFuel",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Resources_Probes,
					Control = new NumericUpDown
					{
						Minimum = int.MinValue,
						Maximum = int.MaxValue,
						Increment = 1,
						Dock = DockStyle.Fill,
					},
					Binding =
						new Binding("Value",
							editor._RootSaveFileBindingSource,
							"Player.Probes",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				}
			};
		}
	}
}