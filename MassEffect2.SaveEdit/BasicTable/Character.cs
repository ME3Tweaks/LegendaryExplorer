using System.Collections.Generic;
using System.Windows.Forms;
using MassEffect2.SaveEdit.Properties;

namespace MassEffect2.SaveEdit.BasicTable
{
	internal static class Character
	{
		public static List<TableItem> Build(Editor editor)
		{
			return new List<TableItem>
			{
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Character_Name,
					Control = new TextBox
					{
						Dock = DockStyle.Fill,
					},
					Binding =
						new Binding("Text",
							editor._RootSaveFileBindingSource,
							"Player.FirstName",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Character_Level,
					Control = new NumericUpDown
					{
						Minimum = 1,
						Maximum = 60,
						Increment = 1,
						Dock = DockStyle.Fill,
					},
					Binding =
						new Binding("Value",
							editor._RootSaveFileBindingSource,
							"Player.Level",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Character_Class,
					Control = new ComboBox
					{
						DropDownStyle = ComboBoxStyle.DropDownList,
						DisplayMember = "Name",
						ValueMember = "Type",
						DataSource = PlayerClass.Classes,
						Dock = DockStyle.Fill,
					},
					Binding =
						new Binding("SelectedValue",
							editor._RootSaveFileBindingSource,
							"Player.ClassName",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Character_Gender,
					Control = new ComboBox
					{
						DropDownStyle = ComboBoxStyle.DropDownList,
						DisplayMember = "Name",
						ValueMember = "Type",
						DataSource = PlayerGender.GetGenders(),
						Dock = DockStyle.Fill,
					},
					Binding =
						new Binding("SelectedValue",
							editor._RootSaveFileBindingSource,
							"Player.IsFemale",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Character_Origin,
					Control = new ComboBox
					{
						DropDownStyle = ComboBoxStyle.DropDownList,
						DisplayMember = "Name",
						ValueMember = "Type",
						DataSource = PlayerOrigin.GetOrigins(),
						Dock = DockStyle.Fill,
					},
					Binding =
						new Binding("SelectedValue",
							editor._RootSaveFileBindingSource,
							"Player.Origin",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Character_Notoriety,
					Control = new ComboBox
					{
						DropDownStyle = ComboBoxStyle.DropDownList,
						DisplayMember = "Name",
						ValueMember = "Type",
						DataSource = PlayerNotoriety.GetNotorieties(),
						Dock = DockStyle.Fill,
					},
					Binding =
						new Binding("SelectedValue",
							editor._RootSaveFileBindingSource,
							"Player.Notoriety",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Character_TalentPoints,
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
							"Player.TalentPoints",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new TableItem
				{
					Name = Localization.Editor_BasicTable_Character_XP,
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
							"Player.CurrentXP",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
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
				}
			};
		}
	}
}