using System.Collections.Generic;
using System.Windows.Forms;
using MassEffect2.SaveEdit.Properties;

namespace MassEffect2.SaveEdit.BasicTable
{
	internal static class Reputation
	{
		public static List<TableItem> Build(Editor editor)
		{
			return new List<TableItem>
			{
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Reputation_ParagonPoints,
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
							"Plot.Helpers.ParagonPoints",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Reputation_RenegadePoints,
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
							"Plot.Helpers.RenegadePoints",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				}/*,
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Reputation_Reputation,
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
							"Plot.Helpers.Reputation",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},*/
				/*new TableItem
				{
					Name = Localization.Editor_BasicTable_Reputation_ReputationPoints,
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
							"Plot.Helpers.ReputationPoints",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				}*/
			};
		}
	}
}