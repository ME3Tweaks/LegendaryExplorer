using System.Collections.Generic;
using System.Windows.Forms;
using MassEffect3.SaveEdit.Properties;

namespace MassEffect3.SaveEdit.BasicTable
{
	internal static class Reputation
	{
		public static List<BasicTableItem> Build(Editor editor)
		{
			return new List<BasicTableItem>
			{
				new BasicTableItem
				{
					Name = Localization.Editor_BasicTable_Reputation_ParagonPoints,
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
							"Plot.Helpers.ParagonPoints",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new BasicTableItem
				{
					Name = Localization.Editor_BasicTable_Reputation_RenegadePoints,
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
							"Plot.Helpers.RenegadePoints",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new BasicTableItem
				{
					Name = Localization.Editor_BasicTable_Reputation_Reputation,
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
							"Plot.Helpers.Reputation",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new BasicTableItem
				{
					Name = Localization.Editor_BasicTable_Reputation_ReputationPoints,
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
							"Plot.Helpers.ReputationPoints",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				}
			};
		}
	}
}
