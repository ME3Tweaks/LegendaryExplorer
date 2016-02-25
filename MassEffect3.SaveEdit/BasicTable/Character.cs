using System.Collections.Generic;
using System.Windows.Forms;
using MassEffect3.SaveEdit.Properties;

namespace MassEffect3.SaveEdit.BasicTable
{
	internal static class Character
	{
		public static List<BasicTableItem> Build(Editor editor)
		{
			return new List<BasicTableItem>
			{
				new BasicTableItem
				{
					Name = Localization.Editor_BasicTable_Character_Name,
					Control = new TextBox
					{
						Dock = DockStyle.Fill
					},
					Binding =
						new Binding("Text",
							editor._rootSaveFileBindingSource,
							"Player.FirstName",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new BasicTableItem
				{
					Name = Localization.Editor_BasicTable_Character_Level,
					Control = new NumericUpDown
					{
						Minimum = 1,
						Maximum = 60,
						Increment = 1,
						Dock = DockStyle.Fill
					},
					Binding =
						new Binding("Value",
							editor._rootSaveFileBindingSource,
							"Player.Level",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new BasicTableItem
				{
					Name = Localization.Editor_BasicTable_Character_Class,
					Control = new ComboBox
					{
						DropDownStyle = ComboBoxStyle.DropDownList,
						DisplayMember = "Name",
						ValueMember = "Type",
						DataSource = PlayerClass.Classes,
						Dock = DockStyle.Fill
					},
					Binding =
						new Binding("SelectedValue",
							editor._rootSaveFileBindingSource,
							"Player.ClassName",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new BasicTableItem
				{
					Name = Localization.Editor_BasicTable_Character_Gender,
					Control = new ComboBox
					{
						DropDownStyle = ComboBoxStyle.DropDownList,
						DisplayMember = "Name",
						ValueMember = "Type",
						DataSource = PlayerGender.GetGenders(),
						Dock = DockStyle.Fill
					},
					Binding =
						new Binding("SelectedValue",
							editor._rootSaveFileBindingSource,
							"Player.IsFemale",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new BasicTableItem
				{
					Name = Localization.Editor_BasicTable_Character_Origin,
					Control = new ComboBox
					{
						DropDownStyle = ComboBoxStyle.DropDownList,
						DisplayMember = "Name",
						ValueMember = "Type",
						DataSource = PlayerOrigin.GetOrigins(),
						Dock = DockStyle.Fill
					},
					Binding =
						new Binding("SelectedValue",
							editor._rootSaveFileBindingSource,
							"Player.Origin",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new BasicTableItem
				{
					Name = Localization.Editor_BasicTable_Character_Notoriety,
					Control = new ComboBox
					{
						DropDownStyle = ComboBoxStyle.DropDownList,
						DisplayMember = "Name",
						ValueMember = "Type",
						DataSource = PlayerNotoriety.GetNotorieties(),
						Dock = DockStyle.Fill
					},
					Binding =
						new Binding("SelectedValue",
							editor._rootSaveFileBindingSource,
							"Player.Notoriety",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new BasicTableItem
				{
					Name = Localization.Editor_BasicTable_Character_TalentPoints,
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
							"Player.TalentPoints",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				},
				new BasicTableItem
				{
					Name = Localization.Editor_BasicTable_Character_XP,
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
							"Player.CurrentXP",
							true,
							DataSourceUpdateMode.OnPropertyChanged)
				}
			};
		}
	}
}
