using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Gammtek.Conduit.Extensions.IO;
using Gammtek.Conduit.IO;
using MassEffect2.SaveEdit.BasicTable;
using MassEffect2.SaveEdit.Properties;
using MassEffect3.ColorPicker;
using MassEffect3.FileFormats.Unreal;
using MassEffect2.SaveFormats;
using Newtonsoft.Json;
using ColorDialog = MassEffect3.ColorPicker.ColorDialog;
using Resources = MassEffect2.SaveEdit.BasicTable.Resources;

namespace MassEffect2.SaveEdit
{
	public partial class Editor : Form
	{
		private const string PlayerPlayerRootMaleImageKey = "Tab_Player_Root_Female";
		private const string PlayerPlayerRootFemaleImageKey = "Tab_Player_Root_Female";
		private const string HeadMorphMagic = "GIBBEDMASSEFFECT2HEADMORPH";

		private readonly List<CheckedListBox> _plotBools = new List<CheckedListBox>();
		private readonly List<NumericUpDown> _plotInts = new List<NumericUpDown>();
		private readonly string _savePath;
		private SFXSaveGameFile _saveFile;

		private int _updatingPlotEditors;

		public Editor()
		{
			InitializeComponent();

			_savePath = null;
			var savePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			if (string.IsNullOrEmpty(savePath) == false)
			{
				savePath = Path.Combine(savePath, "BioWare");
				savePath = Path.Combine(savePath, "Mass Effect 2");
				savePath = Path.Combine(savePath, "Save");

				if (Directory.Exists(savePath))
				{
					_savePath = savePath;
				}
			}

			// ReSharper disable DoNotCallOverridableMethodsInConstructor
			DoubleBuffered = true;
			if (Version.Revision > 0)
			{
				Text += String.Format(
					Localization.Editor_BuildRevision,
					Version.Revision,
					Version.Date);
			}
			// ReSharper restore DoNotCallOverridableMethodsInConstructor

			LoadDefaultMaleSave();

			var presetPath = Path.Combine(GetExecutablePath(), "presets");
			if (Directory.Exists(presetPath))
			{
				_RootAppearancePresetOpenFileDialog.InitialDirectory = presetPath;
				_RootAppearancePresetSaveFileDialog.InitialDirectory = presetPath;
			}

			InitializePostComponent();
		}

		private SFXSaveGameFile SaveFile
		{
			get { return _saveFile; }
			set
			{
				if (_saveFile != value)
				{
					if (_saveFile != null)
					{
						_saveFile.Player.PropertyChanged -= OnPlayerPropertyChanged;
						_saveFile.Player.Appearance.PropertyChanged -= OnPlayerAppearancePropertyChanged;
					}

					var oldValue = _saveFile;
					_saveFile = value;

					if (_saveFile != null)
					{
						_saveFile.Player.PropertyChanged += OnPlayerPropertyChanged;
						_saveFile.Player.Appearance.PropertyChanged += OnPlayerAppearancePropertyChanged;

						_RawParentPropertyGrid.SelectedObject = value;
						_RootSaveFileBindingSource.DataSource = value;
						_RootVectorParametersBindingSource.DataSource =
							value.Player.Appearance.MorphHead.VectorParameters;

						_PlayerRootTabPage.ImageKey =
							_saveFile.Player.IsFemale == false
								? PlayerPlayerRootMaleImageKey
								: PlayerPlayerRootFemaleImageKey;
					}
				}
			}
		}

		private bool IsUpdatingPlotEditors
		{
			get { return _updatingPlotEditors != 0; }
		}

		private void InitializePostComponent()
		{
			SuspendLayout();

			Icon = new Icon(new MemoryStream(Properties.Resources.Guardian));

			if (_savePath != null)
			{
				_RootSaveGameOpenFileDialog.InitialDirectory = _savePath;
				_RootSaveGameSaveFileDialog.InitialDirectory = _savePath;
			}
			else
			{
				_RootDontUseCareerPickerToolStripMenuItem.Checked = true;
				_RootDontUseCareerPickerToolStripMenuItem.Enabled = false;
				_RootOpenFromCareerMenuItem.Enabled = false;
				_RootSaveToCareerMenuItem.Enabled = false;
			}

			// ReSharper disable LocalizableElement
			const string playerBasicTabPageImageKey = "Tab_Player_Basic";
			const string playerSquadTabPageImageKey = "Tab_Player_Squad";
			const string playerAppearanceColorTabPageImageKey = "Tab_Player_Appearance_Color";
			const string playerAppearanceRootTabPageImageKey = "Tab_Player_Appearance_Root";
			const string rawTabPageImageKey = "Tab_Raw";
			const string plotRootTabPageImageKey = "Tab_Plot_Root";
			const string plotManualTabPageImageKey = "Tab_Plot_Manual";
			// ReSharper restore LocalizableElement

			_RootIconImageList.Images.Clear();
			_RootIconImageList.Images.Add("Unknown", new Bitmap(16, 16));
			_RootIconImageList.Images.Add(PlayerPlayerRootMaleImageKey,
				Properties.Resources.Editor_Tab_Player_Root_Male);
			_RootIconImageList.Images.Add(PlayerPlayerRootFemaleImageKey,
				Properties.Resources.Editor_Tab_Player_Root_Female);
			_RootIconImageList.Images.Add(playerBasicTabPageImageKey, Properties.Resources.Editor_Tab_Player_Basic);
			_RootIconImageList.Images.Add(playerAppearanceRootTabPageImageKey,
				Properties.Resources.Editor_Tab_Player_Appearance_Root);
			_RootIconImageList.Images.Add(playerAppearanceColorTabPageImageKey,
				Properties.Resources.Editor_Tab_Player_Appearance_Color);
			_RootIconImageList.Images.Add(rawTabPageImageKey, Properties.Resources.Editor_Tab_Raw);
			_RootIconImageList.Images.Add(plotRootTabPageImageKey, Properties.Resources.Editor_Tab_Plot_Root);
			_RootIconImageList.Images.Add(plotManualTabPageImageKey, Properties.Resources.Editor_Tab_Plot_Manual);

			_PlayerRootTabPage.ImageKey = PlayerPlayerRootMaleImageKey;
			_PlayerBasicTabPage.ImageKey = playerBasicTabPageImageKey;
			_PlayerAppearanceColorTabPage.ImageKey = playerAppearanceColorTabPageImageKey;
			_PlayerAppearanceRootTabPage.ImageKey = playerAppearanceRootTabPageImageKey;
			_RawTabPage.ImageKey = rawTabPageImageKey;
			_PlotRootTabPage.ImageKey = plotRootTabPageImageKey;
			_PlotManualTabPage.ImageKey = plotManualTabPageImageKey;

			_RawSplitContainer.Panel2Collapsed = true;

			AddTable(Localization.Editor_BasicTable_Character_Label, Character.Build(this));
			//AddTable(Localization.Editor_BasicTable_Reputation_Label, Reputation.Build(this));
			AddTable(Localization.Editor_BasicTable_Resources_Label, Resources.Build(this));

			AddPlotEditors();

			if (_saveFile != null)
			{
				var saveFile = _saveFile;
				SaveFile = null;
				SaveFile = saveFile;
			}

			ResumeLayout();
		}
		
		private void AddTable(string name, List<TableItem> items)
		{
			var row = 0;

			var panel = new TableLayoutPanel
			{
				ColumnCount = 2,
				RowCount = items.Count
			};

			panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
			panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));

			// ReSharper disable ForCanBeConvertedToForeach
			for (var i = 0; i < items.Count; i++) // ReSharper restore ForCanBeConvertedToForeach
			{
				panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			}

			foreach (var item in items)
			{
				panel.Controls.Add(item.Control);
				panel.SetRow(item.Control, row);
				panel.SetColumn(item.Control, 1);

				if (string.IsNullOrEmpty(item.Name) == false)
				{
					var label = new Label
					{
						Text = string.Format(Localization.Editor_BasicTable_ItemLabelFormat, item.Name),
						Dock = DockStyle.Fill,
						AutoSize = true,
						TextAlign = ContentAlignment.MiddleRight,
					};
					panel.Controls.Add(label);
					panel.SetRow(label, row);
					panel.SetColumn(label, 0);
				}
				else
				{
					panel.SetColumnSpan(item.Control, 2);
				}

				if (item.Binding != null)
				{
					item.Control.DataBindings.Add(item.Binding);
				}

				row++;
			}

			panel.AutoSize = true;
			panel.Dock = DockStyle.Fill;

			var group = new GroupBox();
			group.Text = name;
			group.MinimumSize = new Size(320, 0);
			group.AutoSize = true;
			group.Controls.Add(panel);

			_PlayerBasicPanel.Controls.Add(group);
		}

		private void AddTable(string name, List<Button> items)
		{
			var row = 0;

			var panel = new TableLayoutPanel
			{
				ColumnCount = 1,
				RowCount = items.Count
			};

			panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

			// ReSharper disable ForCanBeConvertedToForeach
			for (var i = 0; i < items.Count; i++) // ReSharper restore ForCanBeConvertedToForeach
			{
				panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			}

			foreach (var item in items)
			{
				item.Dock = DockStyle.Fill;
				panel.Controls.Add(item);
				panel.SetRow(item, row);
				panel.SetColumn(item, 1);

				row++;
			}

			panel.AutoSize = true;
			panel.Dock = DockStyle.Fill;

			var group = new GroupBox();
			group.Text = name;
			group.MinimumSize = new Size(120, 0);
			group.AutoSize = true;
			group.Controls.Add(panel);

			_PlayerBasicPanel.Controls.Add(group);
		}

		private void AddPlotEditors()
		{
			var plotPath = Path.Combine(GetExecutablePath(), "plots");
			if (Directory.Exists(plotPath) == false)
			{
				return;
			}

			var containers = new List<PlotCategoryContainer>();
			foreach (var inputPath in Directory.GetFiles(plotPath, "*.me2_plot.json", SearchOption.AllDirectories))
			{
				try
				{
					string text;
					using (var input = File.OpenRead(inputPath))
					{
						var reader = new StreamReader(input);
						text = reader.ReadToEnd();
					}

					var settings = new JsonSerializerSettings
					{
						MissingMemberHandling = MissingMemberHandling.Error,
					};

					var cat = JsonConvert.DeserializeObject<PlotCategoryContainer>(text, settings);
					containers.Add(cat);
				}
				catch (Exception e)
				{
					MessageBox.Show(
						string.Format(Localization.Editor_PlotCategoryLoadError, inputPath, e),
						Localization.Error,
						MessageBoxButtons.OK,
						MessageBoxIcon.Error);
				}
			}

			var consumedBools = new List<int>();
			var consumedInts = new List<int>();
			var consumedFloats = new List<int>();

			foreach (var container in containers.OrderBy(c => c.Order))
			{
				var tabs = new List<TabPage>();

				foreach (var category in container.Categories.OrderBy(c => c.Order))
				{
					var count = 0;
					count += string.IsNullOrEmpty(category.Note) == false ? 1 : 0;
					count += category.Bools.Any() ? 1 : 0;
					count += category.Ints.Any() ? 1 : 0;
					count += category.Floats.Any() ? 1 : 0;

					if (count == 0)
					{
						continue;
					}

					var categoryTabPage = new TabPage
					{
						Text = category.Name,
						UseVisualStyleBackColor = true,
					};
					tabs.Add(categoryTabPage);

					Control boolControl = null;
					Control intControl = null;
					Control floatControl = null;

					if (count > 1)
					{
						var categoryTabControl = new TabControl
						{
							Dock = DockStyle.Fill,
						};
						categoryTabPage.Controls.Add(categoryTabControl);

						if (string.IsNullOrEmpty(category.Note) == false)
						{
							var tabPage = new TabPage
							{
								Text = Localization.Editor_PlotEditor_NoteLabel,
								UseVisualStyleBackColor = true,
							};
							categoryTabControl.Controls.Add(tabPage);

							var textBox = new TextBox
							{
								Dock = DockStyle.Fill,
								Multiline = true,
								Text = category.Note.Trim(),
								ReadOnly = true,
								BackColor = SystemColors.Window,
							};
							tabPage.Controls.Add(textBox);
						}

						if (category.Bools.Count > 0)
						{
							var tabPage = new TabPage
							{
								Text = Localization.Editor_PlotEditor_BoolsLabel,
								UseVisualStyleBackColor = true,
							};
							categoryTabControl.Controls.Add(tabPage);

							boolControl = tabPage;
						}

						if (category.Ints.Count > 0)
						{
							var tabPage = new TabPage
							{
								Text = Localization.Editor_PlotEditor_IntsLabel,
								UseVisualStyleBackColor = true,
							};
							categoryTabControl.Controls.Add(tabPage);

							intControl = tabPage;
						}

						if (category.Floats.Count > 0)
						{
							var tabPage = new TabPage
							{
								Text = Localization.Editor_PlotEditor_FloatsLabel,
								UseVisualStyleBackColor = true,
							};
							categoryTabControl.Controls.Add(tabPage);

							floatControl = tabPage;
						}
					}
					else
					{
						boolControl = categoryTabPage;
						intControl = categoryTabPage;
						floatControl = categoryTabPage;
					}

					if (category.Bools.Count > 0)
					{
						var listBox = new CheckedListBox
						{
							Dock = DockStyle.Fill,
							MultiColumn = category.MultilineBools,
							ColumnWidth = 225,
							Sorted = true,
							IntegralHeight = false,
						};
						listBox.ItemCheck += OnPlotBoolChecked;
						// ReSharper disable PossibleNullReferenceException
						boolControl.Controls.Add(listBox);
						// ReSharper restore PossibleNullReferenceException

						foreach (var plot in category.Bools)
						{
							if (consumedBools.Contains(plot.Id))
							{
								//throw new FormatException(string.Format("bool ID {0} already added", plot.Id));
								MessageBox.Show(string.Format(Localization.Editor_DuplicatePlotBool, plot.Id),
									Localization.Warning,
									MessageBoxButtons.OK,
									MessageBoxIcon.Warning);
								continue;
							}
							consumedBools.Add(plot.Id);

							listBox.Items.Add(plot);
						}

						_plotBools.Add(listBox);
					}

					if (category.Ints.Count > 0)
					{
						var panel = new TableLayoutPanel
						{
							Dock = DockStyle.Fill,
							ColumnCount = 2,
							RowCount = category.Ints.Count + 1,
							AutoScroll = true,
							Padding = new Padding(0, 0, 20, 0)
						};

						panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

						foreach (var plot in category.Ints)
						{
							if (consumedInts.Contains(plot.Id))
							{
								//throw new FormatException(string.Format("int ID {0} already added", plot.Id));
								MessageBox.Show(string.Format(Localization.Editor_DuplicatePlotInt, plot.Id),
									Localization.Warning,
									MessageBoxButtons.OK,
									MessageBoxIcon.Warning);
								continue;
							}
							consumedInts.Add(plot.Id);

							var label = new Label
							{
								Text =
									string.Format(Localization.Editor_PlotEditor_ValueLabelFormat, plot.Name),
								Dock = DockStyle.Fill,
								AutoSize = true,
								TextAlign = ContentAlignment.MiddleRight,
							};
							panel.Controls.Add(label);

							var numericUpDown = new NumericUpDown
							{
								Minimum = int.MinValue,
								Maximum = int.MaxValue,
								Increment = 1,
								Tag = plot,
							};
							numericUpDown.ValueChanged += OnPlotIntValueChanged;
							panel.Controls.Add(numericUpDown);

							_plotInts.Add(numericUpDown);
						}

						// ReSharper disable PossibleNullReferenceException
						intControl.Controls.Add(panel);
						// ReSharper restore PossibleNullReferenceException
					}
				}

				if (tabs.Any() == false)
				{
					continue;
				}

				//if (tabs.Count > 1)
				{
					var containerTabPage = new TabPage
					{
						Text = container.Name,
						UseVisualStyleBackColor = true,
					};
					_PlotRootTabControl.TabPages.Add(containerTabPage);

					var containerTabControl = new TabControl
					{
						Dock = DockStyle.Fill,
					};
					containerTabPage.Controls.Add(containerTabControl);

					containerTabControl.TabPages.AddRange(tabs.ToArray());
				}
				/*else
                {
                    this.plotTabControl.TabPages.Add(tabs.First());
                }*/
			}
		}

		private void UpdatePlotEditors()
		{
			_updatingPlotEditors++;

			foreach (var list in _plotBools)
			{
				for (var i = 0; i < list.Items.Count; i++)
				{
					var plot = list.Items[i] as PlotBool;
					if (plot == null)
					{
						continue;
					}

					var value = SaveFile.Plot.GetBoolVariable(plot.Id);
					list.SetItemChecked(i, value);
				}
			}

			foreach (var numericUpDown in _plotInts)
			{
				var plot = numericUpDown.Tag as PlotInt;
				if (plot == null)
				{
					continue;
				}

				numericUpDown.Value = SaveFile.Plot.GetIntVariable(plot.Id);
			}

			_updatingPlotEditors--;
		}

		private void OnPlotBoolChecked(object sender, ItemCheckEventArgs e)
		{
			if (IsUpdatingPlotEditors)
			{
				return;
			}

			var list = sender as CheckedListBox;

			if (list == null)
			{
				e.NewValue = e.CurrentValue;
				return;
			}

			var plot = list.Items[e.Index] as PlotBool;

			if (plot == null)
			{
				e.NewValue = e.CurrentValue;
				return;
			}

			SaveFile.Plot.SetBoolVariable(plot.Id, e.NewValue == CheckState.Checked);
		}

		private void OnPlotIntValueChanged(object sender, EventArgs e)
		{
			if (IsUpdatingPlotEditors)
			{
				return;
			}

			var numericUpDown = sender as NumericUpDown;

			if (numericUpDown == null)
			{
				return;
			}

			var plot = numericUpDown.Tag as PlotInt;
			if (plot == null)
			{
				return;
			}

			SaveFile.Plot.SetIntVariable(plot.Id, (int) numericUpDown.Value);
		}

		private static string GetExecutablePath()
		{
			return Path.GetDirectoryName(Application.ExecutablePath);
		}

		private void LoadDefaultMaleSave()
		{
			using (var memory = new MemoryStream(Properties.Resources.DefaultMaleSave))
			{
				LoadSaveFromStream(memory);
			}
		}

		private void LoadDefaultFemaleSave()
		{
			using (var memory = new MemoryStream(Properties.Resources.DefaultFemaleSave))
			{
				LoadSaveFromStream(memory);
			}
		}

		private void OnPlayerPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			// goofy?

			if (e.PropertyName == "IsFemale")
			{
				_PlayerRootTabPage.ImageKey =
					(_saveFile == null || _saveFile.Player.IsFemale == false)
						? "Tab_Player_Root_Male"
						: "Tab_Player_Root_Female";
			}
		}

		private void OnPlayerAppearancePropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "MorphHead")
			{
				_RootVectorParametersBindingSource.DataSource =
					_saveFile.Player.Appearance.MorphHead.VectorParameters;
			}
		}

		private void LoadSaveFromStream(Stream stream)
		{
			if (stream.ReadUInt32(ByteOrder.BigEndian) == 0x434F4E20) // 'CON '
			{
				MessageBox.Show(Localization.Editor_CannotLoadXbox360CONFile,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}
			stream.Seek(-4, SeekOrigin.Current);

			SFXSaveGameFile saveFile;
			try
			{
				saveFile = SFXSaveGameFile.Read(stream);
			}
			catch (Exception e)
			{
				MessageBox.Show(
					string.Format(CultureInfo.InvariantCulture, Localization.Editor_SaveReadException, e),
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			SaveFile = saveFile;

			UpdatePlotEditors();
		}

		private void OnSaveNewMale(object sender, EventArgs e)
		{
			LoadDefaultMaleSave();
		}

		private void OnSaveNewFemale(object sender, EventArgs e)
		{
			LoadDefaultFemaleSave();
		}

		private void OnSaveOpenFromGeneric(object sender, EventArgs e)
		{
			if (_RootDontUseCareerPickerToolStripMenuItem.Checked == false)
			{
				OnSaveOpenFromCareer(sender, e);
			}
			else
			{
				OnSaveOpenFromFile(sender, e);
			}
		}

		private void OnSaveOpenFromCareer(object sender, EventArgs e)
		{
			using (var picker = new SavePicker())
			{
				picker.Owner = this;
				picker.FileMode = SavePicker.PickerMode.Load;
				picker.FilePath = _savePath;

				var result = picker.ShowDialog();
				if (result != DialogResult.OK)
				{
					return;
				}

				if (string.IsNullOrEmpty(picker.SelectedPath))
				{
					MessageBox.Show(
						Localization.Editor_ThisShouldNeverHappen,
						Localization.Error,
						MessageBoxButtons.OK,
						MessageBoxIcon.Error);
					return;
				}

				using (var input = File.OpenRead(picker.SelectedPath))
				{
					LoadSaveFromStream(input);
				}
			}
		}

		private void OnSaveOpenFromFile(object sender, EventArgs e)
		{
			if (_RootSaveGameOpenFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			var dir = Path.GetDirectoryName(_RootSaveGameOpenFileDialog.FileName);
			_RootSaveGameOpenFileDialog.InitialDirectory = dir;

			using (var input = _RootSaveGameOpenFileDialog.OpenFile())
			{
				LoadSaveFromStream(input);
			}
		}

		private void OnSaveSaveToGeneric(object sender, EventArgs e)
		{
			if (_RootDontUseCareerPickerToolStripMenuItem.Checked == false)
			{
				OnSaveSaveToCareer(sender, e);
			}
			else
			{
				OnSaveSaveToFile(sender, e);
			}
		}

		private void OnSaveSaveToFile(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(
					Localization.Editor_NoActiveSave,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			_RootSaveGameSaveFileDialog.FilterIndex =
				SaveFile.Endian == ByteOrder.LittleEndian ? 1 : 2;
			if (_RootSaveGameSaveFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			var dir = Path.GetDirectoryName(_RootSaveGameSaveFileDialog.FileName);
			_RootSaveGameSaveFileDialog.InitialDirectory = dir;

			SaveFile.Endian = _RootSaveGameSaveFileDialog.FilterIndex != 2
				? ByteOrder.LittleEndian
				: ByteOrder.BigEndian;

			using (var output = _RootSaveGameSaveFileDialog.OpenFile())
			{
				SFXSaveGameFile.Write(SaveFile, output);
			}
		}

		private void OnSaveSaveToCareer(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(
					Localization.Editor_NoActiveSave,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			using (var picker = new SavePicker())
			{
				picker.Owner = this;
				picker.FileMode = SavePicker.PickerMode.Save;
				picker.FilePath = _savePath;
				picker.SaveFile = SaveFile;

				var result = picker.ShowDialog();
				if (result != DialogResult.OK)
				{
					return;
				}

				if (string.IsNullOrEmpty(picker.SelectedPath) == false)
				{
					var selectedDirectory = Path.GetDirectoryName(picker.SelectedPath);
					if (selectedDirectory != null)
					{
						Directory.CreateDirectory(selectedDirectory);
					}

					using (var output = File.Create(picker.SelectedPath))
					{
						SFXSaveGameFile.Write(SaveFile, output);
					}
				}
				else
				{
					MessageBox.Show(
						Localization.Editor_ThisShouldNeverHappen,
						Localization.Error,
						MessageBoxButtons.OK,
						MessageBoxIcon.Error);
				}
			}
		}

		private void OnSelectedGridItemChanged(object sender, SelectedGridItemChangedEventArgs e)
		{
			if (e.OldSelection != null)
			{
				var oldPc = e.OldSelection.Value as INotifyPropertyChanged;
				if (oldPc != null)
				{
					oldPc.PropertyChanged -= OnPropertyChanged;
				}
			}

			if (e.NewSelection != null)
			{
				if ((e.NewSelection.Value is ISerializable))
				{
					_RawChildPropertyGrid.SelectedObject = e.NewSelection.Value;
					_RawSplitContainer.Panel2Collapsed = false;

					var newPc = e.NewSelection.Value as INotifyPropertyChanged;
					if (newPc != null)
					{
						newPc.PropertyChanged += OnPropertyChanged;
					}

					return;
				}
			}

			_RawChildPropertyGrid.SelectedObject = null;
			_RawSplitContainer.Panel2Collapsed = true;
		}

		private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			_RawParentPropertyGrid.Refresh();
		}

		private void OnImportHeadMorph(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(
					Localization.Editor_NoActiveSave,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			if (_RootMorphHeadOpenFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			var dir = Path.GetDirectoryName(_RootMorphHeadOpenFileDialog.FileName);
			_RootMorphHeadOpenFileDialog.InitialDirectory = dir;

			using (var input = _RootMorphHeadOpenFileDialog.OpenFile())
			{
				if (input.ReadString(HeadMorphMagic.Length, Encoding.ASCII) != HeadMorphMagic)
				{
					MessageBox.Show(
						Localization.Editor_HeadMorphInvalid,
						Localization.Error,
						MessageBoxButtons.OK,
						MessageBoxIcon.Error);
					input.Close();
					return;
				}

				if (input.ReadUInt8() != 0)
				{
					MessageBox.Show(
						Localization.Editor_HeadMorphVersionUnsupported,
						Localization.Error,
						MessageBoxButtons.OK,
						MessageBoxIcon.Error);
					input.Close();
					return;
				}

				var version = input.ReadUInt32();

				if (version != SaveFile.Version)
				{
					if (MessageBox.Show(
						string.Format(
							Localization.Editor_HeadMorphVersionMaybeIncompatible,
							version,
							SaveFile.Version),
						Localization.Question,
						MessageBoxButtons.YesNo,
						MessageBoxIcon.Question) == DialogResult.No)
					{
						input.Close();
						return;
					}
				}

				var reader = new FileReader(
					input, version, ByteOrder.LittleEndian);
				var morphHead = new MorphHead();
				morphHead.Serialize(reader);
				SaveFile.Player.Appearance.MorphHead = morphHead;
				SaveFile.Player.Appearance.HasMorphHead = true;
			}
		}

		private void OnExportHeadMorph(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(
					Localization.Editor_NoActiveSave,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			if (SaveFile.Player.Appearance.HasMorphHead == false)
			{
				MessageBox.Show(
					Localization.Editor_NoHeadMorph,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			if (_RootMorphHeadSaveFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			var dir = Path.GetDirectoryName(_RootMorphHeadSaveFileDialog.FileName);
			_RootMorphHeadSaveFileDialog.InitialDirectory = dir;

			using (var output = _RootMorphHeadSaveFileDialog.OpenFile())
			{
				output.WriteString(HeadMorphMagic, Encoding.ASCII);
				output.WriteByte(0); // "version" in case I break something in the future
				output.WriteUInt32(SaveFile.Version);
				var writer = new FileWriter(
					output, SaveFile.Version, ByteOrder.LittleEndian);
				SaveFile.Player.Appearance.MorphHead.Serialize(writer);
			}
		}

		private void AppendToPlotManualLog(string format, params object[] args)
		{
			if (string.IsNullOrEmpty(_PlotManualLogTextBox.Text) == false)
			{
				_PlotManualLogTextBox.AppendText(Environment.NewLine);
			}
			_PlotManualLogTextBox.AppendText(
				string.Format(
					Thread.CurrentThread.CurrentCulture,
					format,
					args));
		}

		private void OnPlotManualGetBool(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(
					Localization.Editor_NoActiveSave,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			int id;
			if (int.TryParse(
				_PlotManualBoolIdTextBox.Text,
				NumberStyles.None,
				Thread.CurrentThread.CurrentCulture,
				out id) == false)
			{
				MessageBox.Show(Localization.Editor_FailedToParseId,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			var value = SaveFile.Plot.GetBoolVariable(id);
			AppendToPlotManualLog(Localization.Editor_PlotManualLogBoolGet,
				id,
				value);
			_PlotManualBoolValueCheckBox.Checked = value;
		}

		private void OnPlotManualSetBool(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(
					Localization.Editor_NoActiveSave,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			int id;
			if (int.TryParse(
				_PlotManualBoolIdTextBox.Text,
				NumberStyles.None,
				Thread.CurrentThread.CurrentCulture,
				out id) == false)
			{
				MessageBox.Show(Localization.Editor_FailedToParseId,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			var newValue = _PlotManualBoolValueCheckBox.Checked;
			var oldValue = SaveFile.Plot.GetBoolVariable(id);
			SaveFile.Plot.SetBoolVariable(id, newValue);
			AppendToPlotManualLog(Localization.Editor_PlotManualLogBoolSet,
				id,
				newValue,
				oldValue);
		}

		private void OnPlotManualGetInt(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(
					Localization.Editor_NoActiveSave,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			int id;
			if (int.TryParse(
				_PlotManualIntIdTextBox.Text,
				NumberStyles.None,
				Thread.CurrentThread.CurrentCulture,
				out id) == false)
			{
				MessageBox.Show(Localization.Editor_FailedToParseId,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			var value = SaveFile.Plot.GetIntVariable(id);
			AppendToPlotManualLog(Localization.Editor_PlotManualLogIntGet,
				id,
				value);
			_PlotManualIntValueTextBox.Text =
				value.ToString(Thread.CurrentThread.CurrentCulture);
		}

		private void OnPlotManualSetInt(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(
					Localization.Editor_NoActiveSave,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			int id;
			if (int.TryParse(
				_PlotManualIntIdTextBox.Text,
				NumberStyles.None,
				Thread.CurrentThread.CurrentCulture,
				out id) == false)
			{
				MessageBox.Show(Localization.Editor_FailedToParseId,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			int newValue;
			if (int.TryParse(
				_PlotManualIntValueTextBox.Text,
				NumberStyles.None,
				Thread.CurrentThread.CurrentCulture,
				out newValue) == false)
			{
				MessageBox.Show(Localization.Editor_FailedToParseValue,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			var oldValue = SaveFile.Plot.GetIntVariable(id);
			SaveFile.Plot.SetIntVariable(id, newValue);
			AppendToPlotManualLog(Localization.Editor_PlotManualLogIntSet,
				id,
				newValue,
				oldValue);
		}

		private void OnPlotManualGetFloat(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(
					Localization.Editor_NoActiveSave,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			int id;
			if (int.TryParse(
				_PlotManualFloatIdTextBox.Text,
				NumberStyles.None,
				Thread.CurrentThread.CurrentCulture,
				out id) == false)
			{
				MessageBox.Show(Localization.Editor_FailedToParseId,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			var value = SaveFile.Plot.GetFloatVariable(id);
			AppendToPlotManualLog(Localization.Editor_PlotManualLogFloatGet,
				id,
				value);
			_PlotManualFloatValueTextBox.Text =
				value.ToString(Thread.CurrentThread.CurrentCulture);
		}

		private void OnPlotManualSetFloat(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(
					Localization.Editor_NoActiveSave,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			int id;
			if (int.TryParse(
				_PlotManualFloatIdTextBox.Text,
				NumberStyles.None,
				Thread.CurrentThread.CurrentCulture,
				out id) == false)
			{
				MessageBox.Show(Localization.Editor_FailedToParseId,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			float newValue;
			if (float.TryParse(
				_PlotManualIntValueTextBox.Text,
				NumberStyles.None,
				Thread.CurrentThread.CurrentCulture,
				out newValue) == false)
			{
				MessageBox.Show(Localization.Editor_FailedToParseValue,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			var oldValue = SaveFile.Plot.GetFloatVariable(id);
			SaveFile.Plot.SetFloatVariable(id, newValue);
			AppendToPlotManualLog(Localization.Editor_PlotManualLogFloatSet,
				id,
				newValue,
				oldValue);
		}

		private void OnPlotManualClearLog(object sender, EventArgs e)
		{
			_PlotManualLogTextBox.Clear();
		}

		private static void ApplyAppearancePreset(MorphHead morphHead,
			AppearancePreset preset)
		{
			if (morphHead == null)
			{
				throw new ArgumentNullException("morphHead");
			}

			if (preset == null)
			{
				throw new ArgumentNullException("preset");
			}

			if (string.IsNullOrEmpty(preset.HairMesh) == false)
			{
				morphHead.HairMesh = preset.HairMesh;
			}

			if (preset.Scalars != null)
			{
				if (preset.Scalars.Clear)
				{
					morphHead.ScalarParameters.Clear();
				}

				if (preset.Scalars.Remove != null)
				{
					foreach (var scalar in preset.Scalars.Remove)
					{
						morphHead.ScalarParameters.RemoveAll(
							p => string.Compare(p.Name, scalar, StringComparison.InvariantCultureIgnoreCase) == 0);
					}
				}

				if (preset.Scalars.Add != null)
				{
					foreach (var scalar in preset.Scalars.Add)
					{
						morphHead.ScalarParameters.Add(
							new MorphHead.ScalarParameter
							{
								Name = scalar.Key,
								Value = scalar.Value,
							});
					}
				}

				if (preset.Scalars.Set != null)
				{
					foreach (var scalar in preset.Scalars.Set)
					{
						morphHead.ScalarParameters.RemoveAll(
							p => string.Compare(p.Name, scalar.Key, StringComparison.InvariantCultureIgnoreCase) == 0);
						morphHead.ScalarParameters.Add(
							new MorphHead.ScalarParameter
							{
								Name = scalar.Key,
								Value = scalar.Value,
							});
					}
				}
			}

			if (preset.Textures != null)
			{
				if (preset.Textures.Clear)
				{
					morphHead.TextureParameters.Clear();
				}

				if (preset.Textures.Remove != null)
				{
					foreach (var texture in preset.Textures.Remove)
					{
						morphHead.TextureParameters.RemoveAll(
							p => string.Compare(p.Name, texture, StringComparison.InvariantCultureIgnoreCase) == 0);
					}
				}

				if (preset.Textures.Add != null)
				{
					foreach (var texture in preset.Textures.Add)
					{
						morphHead.TextureParameters.Add(
							new MorphHead.TextureParameter
							{
								Name = texture.Key,
								Value = texture.Value,
							});
					}
				}

				if (preset.Textures.Set != null)
				{
					foreach (var texture in preset.Textures.Set)
					{
						morphHead.TextureParameters.RemoveAll(
							p => string.Compare(p.Name, texture.Key, StringComparison.InvariantCultureIgnoreCase) == 0);
						morphHead.TextureParameters.Add(
							new MorphHead.TextureParameter
							{
								Name = texture.Key,
								Value = texture.Value,
							});
					}
				}
			}

			if (preset.Vectors != null)
			{
				if (preset.Vectors.Clear)
				{
					morphHead.VectorParameters.Clear();
				}

				if (preset.Vectors.Remove != null)
				{
					foreach (var vector in preset.Vectors.Remove)
					{
						var temp = vector;
						morphHead.VectorParameters.RemoveAll(
							p => string.Compare(p.Name, temp, StringComparison.InvariantCultureIgnoreCase) == 0);
					}
				}

				if (preset.Vectors.Add != null)
				{
					foreach (var vector in preset.Vectors.Add)
					{
						morphHead.VectorParameters.Add(
							new MorphHead.VectorParameter
							{
								Name = vector.Key,
								Value = new LinearColor
								{
									R = vector.Value.R,
									G = vector.Value.G,
									B = vector.Value.B,
									A = vector.Value.A,
								},
							});
					}
				}

				if (preset.Vectors.Set != null)
				{
					foreach (var vector in preset.Vectors.Set)
					{
						var temp = vector;
						morphHead.VectorParameters.RemoveAll(
							p => string.Compare(p.Name, temp.Key, StringComparison.InvariantCultureIgnoreCase) == 0);
						morphHead.VectorParameters.Add(
							new MorphHead.VectorParameter
							{
								Name = vector.Key,
								Value = new LinearColor
								{
									R = vector.Value.R,
									G = vector.Value.G,
									B = vector.Value.B,
									A = vector.Value.A,
								},
							});
					}
				}
			}
		}

		private void OnLoadAppearancePresetFromFile(object sender, EventArgs e)
		{
			if (_saveFile == null)
			{
				MessageBox.Show(
					Localization.Editor_NoActiveSave,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			if (_saveFile.Player.Appearance.HasMorphHead == false)
			{
				MessageBox.Show(
					Localization.Editor_NoHeadMorph,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			if (_RootAppearancePresetOpenFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			var dir = Path.GetDirectoryName(_RootAppearancePresetOpenFileDialog.FileName);
			_RootAppearancePresetOpenFileDialog.InitialDirectory = dir;

			string text;
			using (var input = _RootAppearancePresetOpenFileDialog.OpenFile())
			{
				var reader = new StreamReader(input);
				text = reader.ReadToEnd();
			}

			var preset = JsonConvert.DeserializeObject<AppearancePreset>(text);
			ApplyAppearancePreset(_saveFile.Player.Appearance.MorphHead, preset);
		}

		private void OnSaveAppearancePresetToFile(object sender, EventArgs e)
		{
			if (_saveFile == null)
			{
				MessageBox.Show(
					Localization.Editor_NoActiveSave,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			if (_saveFile.Player.Appearance.HasMorphHead == false)
			{
				MessageBox.Show(
					Localization.Editor_NoHeadMorph,
					Localization.Error,
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}

			if (_RootAppearancePresetSaveFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			var dir = Path.GetDirectoryName(_RootAppearancePresetSaveFileDialog.FileName);
			_RootAppearancePresetSaveFileDialog.InitialDirectory = dir;

			var headMorph = SaveFile.Player.Appearance.MorphHead;

			// ReSharper disable UseObjectOrCollectionInitializer
			var preset = new AppearancePreset();
			// ReSharper restore UseObjectOrCollectionInitializer

			preset.HairMesh = headMorph.HairMesh;

			foreach (var scalar in headMorph.ScalarParameters)
			{
				preset.Scalars.Set.Add(new KeyValuePair<string, float>(scalar.Name, scalar.Value));
			}

			foreach (var texture in headMorph.TextureParameters)
			{
				preset.Textures.Set.Add(new KeyValuePair<string, string>(texture.Name, texture.Value));
			}

			foreach (var vector in headMorph.VectorParameters)
			{
				preset.Vectors.Set.Add(new KeyValuePair<string, AppearancePreset.LinearColor>(vector.Name,
					new AppearancePreset.LinearColor
					{
						R = vector.Value.R,
						G = vector.Value.G,
						B = vector.Value.B,
						A = vector.Value.A,
					}));
			}

			using (var output = File.Create(_RootAppearancePresetSaveFileDialog.FileName))
			{
				var writer = new StreamWriter(output);
				writer.Write(JsonConvert.SerializeObject(
					preset, Formatting.Indented));
				writer.Flush();
			}
		}

		private static ColorBgra LinearColorToBgra(LinearColor linearColor)
		{
			return LinearColorToBgra(
				linearColor.R,
				linearColor.G,
				linearColor.B,
				linearColor.A);
		}

		private static ColorBgra LinearColorToBgra(float r, float g, float b, float a)
		{
			var rb = (byte) Math.Round(r * 255);
			var gb = (byte) Math.Round(g * 255);
			var bb = (byte) Math.Round(b * 255);
			var ab = (byte) Math.Round(a * 255);
			return ColorBgra.FromBgra(bb, gb, rb, ab);
		}

		private static LinearColor BgraToLinearColor(ColorBgra bgra)
		{
			return new LinearColor(
				(float) bgra.R / 255,
				(float) bgra.G / 255,
				(float) bgra.B / 255,
				(float) bgra.A / 255);
		}

		private void OnDrawColorListBoxItem(object sender, DrawItemEventArgs e)
		{
			if (e.Index < 0)
			{
				return;
			}

			var g = e.Graphics;
			var listbox = (ListBox) sender;

			var backColor = (e.State & DrawItemState.Selected) != 0
				? SystemColors.Highlight
				: listbox.BackColor;
			var foreColor = (e.State & DrawItemState.Selected) != 0
				? SystemColors.HighlightText
				: listbox.ForeColor;

			g.FillRectangle(new SolidBrush(backColor), e.Bounds);

			var colorBounds = e.Bounds;

			colorBounds.Width = 30;
			colorBounds.Height -= 4;
			colorBounds.X += 2;
			colorBounds.Y += 2;

			var textBounds = e.Bounds;
			textBounds.Offset(30, 0);
			textBounds.Inflate(-2, -2);
			var textBoundsF = new RectangleF(textBounds.X, textBounds.Y, textBounds.Width, textBounds.Height);

			g.FillRectangle(
				new HatchBrush(
					HatchStyle.LargeCheckerBoard,
					Color.White,
					Color.Gray),
				colorBounds);

			var item = listbox.Items[e.Index];

			var vector = item as MorphHead.VectorParameter;
			if (vector != null)
			{
				var valueColor = LinearColorToBgra(vector.Value).ToColor();

				g.FillRectangle(new SolidBrush(valueColor), colorBounds);
				g.DrawRectangle(Pens.Black, colorBounds);

				var format = StringFormat.GenericDefault;
				format.LineAlignment = StringAlignment.Center;

				e.Graphics.DrawString(vector.Name,
					listbox.Font,
					new SolidBrush(foreColor),
					textBoundsF,
					format);
			}
		}

		private void OnPlayerAppearanceColorRemove(object sender, EventArgs e)
		{
			var item = _PlayerAppearanceColorListBox.SelectedItem as MorphHead.VectorParameter;
			if (item != null)
			{
				_saveFile.Player.Appearance.MorphHead.VectorParameters.Remove(item);
			}
		}

		private void OnPlayerAppearanceColorAdd(object sender, EventArgs e)
		{
			var input = new InputBox
			{
				Owner = this,
				Text = Localization.Editor_ColorName,
				InputText = "",
			};

			if (input.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			_saveFile.Player.Appearance.MorphHead.VectorParameters.Add(
				new MorphHead.VectorParameter
				{
					Name = input.InputText,
					Value = new LinearColor(1, 1, 1, 1),
				});
		}

		private void OnPlayerAppearanceColorChange(object sender, EventArgs e)
		{
			var item = _PlayerAppearanceColorListBox.SelectedItem as MorphHead.VectorParameter;
			if (item != null)
			{
				var bgra = LinearColorToBgra(item.Value);

				// ReSharper disable UseObjectOrCollectionInitializer
				var picker = new ColorDialog();
				// ReSharper restore UseObjectOrCollectionInitializer
				picker.WheelColor = bgra;

				if (picker.ShowDialog() != DialogResult.OK)
				{
					return;
				}

				item.Value = BgraToLinearColor(picker.WheelColor);
			}
		}

		private void OnRootTabIndexChanged(object sender, EventArgs e)
		{
			if (_RootTabControl.SelectedTab == _RawTabPage)
			{
				// HACK: refresh property grids, just in case
				_RawParentPropertyGrid.Refresh();
				_RawChildPropertyGrid.Refresh();
			}
		}
	}
}