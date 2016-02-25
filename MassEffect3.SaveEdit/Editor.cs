using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using Gammtek.Conduit.Extensions;
using Gammtek.Conduit.Extensions.IO;
using Gammtek.Conduit.IO;
using MassEffect3.ColorPicker;
using MassEffect3.FileFormats.Unreal;
using MassEffect3.SaveEdit.BasicTable;
using MassEffect3.SaveEdit.Properties;
using MassEffect3.SaveEdit.Squad;
using MassEffect3.SaveEdit.SquadBasicTable;
using MassEffect3.SaveFormats;
using Newtonsoft.Json;
using ColorDialog = MassEffect3.ColorPicker.ColorDialog;
using Resources = MassEffect3.SaveEdit.Properties.Resources;

namespace MassEffect3.SaveEdit
{
	public partial class Editor : Form
	{
		private const string HeadMorphMagic = "GIBBEDMASSEFFECT3HEADMORPH";
		private const string PlayerPlayerRootFemaleImageKey = "Tab_Player_Root_Female";
		private const string PlayerPlayerRootMaleImageKey = "Tab_Player_Root_Female";
		private static bool _backupAutoSaves;
		private static bool _useCareerPicker = true;
		private readonly TimeSpan _fileTimeSpan = TimeSpan.FromMilliseconds(200);
		private readonly Dictionary<string, DateTime> _fileTimes = new Dictionary<string, DateTime>();
		private readonly Dictionary<string, string> _manualSaveHashes = new Dictionary<string, string>();
		private readonly MD5 _md5Hash;
		private readonly AutoCompleteStringCollection _playerVarAutoCompleteCollection = new AutoCompleteStringCollection();

		//

		private readonly List<CheckedListBox> _plotBools = new List<CheckedListBox>();
		private readonly List<NumericUpDown> _plotFloats = new List<NumericUpDown>();
		private readonly List<NumericUpDown> _plotInts = new List<NumericUpDown>();
		private readonly List<NumericUpDown> _plotPlayerVariables = new List<NumericUpDown>();
		private readonly FileSystemWatcher _saveFileWatcher;
		private readonly string _savePath;
		private SFXSaveGameFile _saveFile;
		private int _updatingPlotEditors;

		// Autosave/ChapterSave watcher

		public Editor()
		{
			InitializeComponent();

			//Settings.Default.PropertyChanged += Default_PropertyChanged;

			_savePath = null;
			var savePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

			if (string.IsNullOrEmpty(savePath) == false)
			{
				savePath = Path.Combine(savePath, "BioWare");
				savePath = Path.Combine(savePath, "Mass Effect 3");
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
				_rootAppearancePresetOpenFileDialog.InitialDirectory = presetPath;
				_rootAppearancePresetSaveFileDialog.InitialDirectory = presetPath;
			}

			LoadSettings();

			InitializePostComponent();

			// SaveFile Watcher
			_saveFileWatcher = new FileSystemWatcher(_savePath ?? "", "*.pcsav")
							   {
								   EnableRaisingEvents = _backupAutoSaves,
								   IncludeSubdirectories = true,
								   InternalBufferSize = 1024 * 16,
								   NotifyFilter = /*NotifyFilters.Attributes |*/
									   NotifyFilters.CreationTime |
									   //NotifyFilters.DirectoryName |
									   //NotifyFilters.FileName |
									   /*NotifyFilters.LastAccess |*/
									   NotifyFilters.LastWrite |
									   /*NotifyFilters.Security |*/
									   NotifyFilters.Size
							   };

			_saveFileWatcher.Changed += SaveFileWatcherOnChanged;
			_saveFileWatcher.Created += SaveFileWatcherOnChanged;
			_saveFileWatcher.Deleted += SaveFileWatcherOnChanged;
			_saveFileWatcher.Error += SaveFileWatcherOnError;
			_saveFileWatcher.Renamed += SaveFileWatcherOnChanged;
		}

		private bool IsUpdatingPlotEditors
		{
			get { return _updatingPlotEditors != 0; }
		}

		public SFXSaveGameFile SaveFile
		{
			get { return _saveFile; }
			private set
			{
				if (_saveFile == value)
				{
					return;
				}

				if (_saveFile != null)
				{
					_saveFile.Player.PropertyChanged -= OnPlayerPropertyChanged;
					_saveFile.Player.Appearance.PropertyChanged -= OnPlayerAppearancePropertyChanged;
				}

				_saveFile = value;

				if (_saveFile == null)
				{
					return;
				}

				_saveFile.Player.PropertyChanged += OnPlayerPropertyChanged;
				_saveFile.Player.Appearance.PropertyChanged += OnPlayerAppearancePropertyChanged;

				_rawParentPropertyGrid.SelectedObject = value;
				_rootSaveFileBindingSource.DataSource = value;
				_rootVectorParametersBindingSource.DataSource =
					value.Player.Appearance.MorphHead.VectorParameters;

				_playerRootTabPage.ImageKey =
					_saveFile.Player.IsFemale == false
						? PlayerPlayerRootMaleImageKey
						: PlayerPlayerRootFemaleImageKey;

				UpdatePlayerVarAutoComplete();

				_plotManualPlayerVarIdTextBox.AutoCompleteCustomSource = _playerVarAutoCompleteCollection;
				_plotManualPlayerVarIdTextBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
				_plotManualPlayerVarIdTextBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
			}
		}

		private static void ApplyAppearancePreset(MorphHead morphHead, AppearancePreset preset)
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
						morphHead.ScalarParameters.RemoveAll(p => string.Compare(p.Name, scalar, StringComparison.InvariantCultureIgnoreCase) == 0);
					}
				}

				if (preset.Scalars.Add != null)
				{
					foreach (var scalar in preset.Scalars.Add)
					{
						morphHead.ScalarParameters.Add(new MorphHead.ScalarParameter
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
						morphHead.ScalarParameters.RemoveAll(p => string.Compare(p.Name, scalar.Key, StringComparison.InvariantCultureIgnoreCase) == 0);
						morphHead.ScalarParameters.Add(new MorphHead.ScalarParameter
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
						morphHead.TextureParameters.RemoveAll(p => string.Compare(p.Name, texture, StringComparison.InvariantCultureIgnoreCase) == 0);
					}
				}

				if (preset.Textures.Add != null)
				{
					foreach (var texture in preset.Textures.Add)
					{
						morphHead.TextureParameters.Add(new MorphHead.TextureParameter
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
						morphHead.TextureParameters.RemoveAll(p => string.Compare(p.Name, texture.Key, StringComparison.InvariantCultureIgnoreCase) == 0);
						morphHead.TextureParameters.Add(new MorphHead.TextureParameter
														{
															Name = texture.Key,
															Value = texture.Value,
														});
					}
				}
			}

			if (preset.Vectors == null)
			{
				return;
			}

			if (preset.Vectors.Clear)
			{
				morphHead.VectorParameters.Clear();
			}

			if (preset.Vectors.Remove != null)
			{
				foreach (var vector in preset.Vectors.Remove)
				{
					var temp = vector;
					morphHead.VectorParameters.RemoveAll(p => string.Compare(p.Name, temp, StringComparison.InvariantCultureIgnoreCase) == 0);
				}
			}

			if (preset.Vectors.Add != null)
			{
				foreach (var vector in preset.Vectors.Add)
				{
					morphHead.VectorParameters.Add(new MorphHead.VectorParameter
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

			if (preset.Vectors.Set == null)
			{
				return;
			}

			foreach (var vector in preset.Vectors.Set)
			{
				var temp = vector;
				morphHead.VectorParameters.RemoveAll(p => string.Compare(p.Name, temp.Key, StringComparison.InvariantCultureIgnoreCase) == 0);
				morphHead.VectorParameters.Add(new MorphHead.VectorParameter
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

		private static LinearColor BgraToLinearColor(ColorBgra bgra)
		{
			return new LinearColor(
				(float) bgra.R / 255,
				(float) bgra.G / 255,
				(float) bgra.B / 255,
				(float) bgra.A / 255);
		}

		private static string ComputeMD5Hash(string path)
		{
			using (var stream = File.Open(path, FileMode.Open))
			{
				var md5 = MD5.Create();

				return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "");
			}
		}

		private static string GetExecutablePath()
		{
			return Path.GetDirectoryName(Application.ExecutablePath);
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

		private void AddPlotEditors()
		{
			var plotPath = Path.Combine(GetExecutablePath(), "plots");

			if (Directory.Exists(plotPath) == false)
			{
				return;
			}

			var containers = new List<PlotCategoryContainer>();

			foreach (var inputPath in Directory.GetFiles(plotPath, "*.me3_plot.json", SearchOption.AllDirectories))
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
					MessageBox.Show(string.Format(Localization.Editor_PlotCategoryLoadError, inputPath, e), Localization.Error, MessageBoxButtons.OK,
						MessageBoxIcon.Error);
				}
			}

			var consumedBools = new List<int>();
			var consumedInts = new List<int>();
			var consumedFloats = new List<int>();
			var consumedPlayerVariables = new List<string>();

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
					count += category.PlayerVariables.Any() ? 1 : 0;

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
					Control playerVariablesControl = null;

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

						if (category.PlayerVariables.Count > 0)
						{
							var tabPage = new TabPage
										  {
											  Text = @"Player Variables",
											  UseVisualStyleBackColor = true,
										  };

							categoryTabControl.Controls.Add(tabPage);

							playerVariablesControl = tabPage;
						}
					}
					else
					{
						boolControl = categoryTabPage;
						intControl = categoryTabPage;
						floatControl = categoryTabPage;
						playerVariablesControl = categoryTabPage;
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

						boolControl.Controls.Add(listBox);

						foreach (var plot in category.Bools)
						{
							if (consumedBools.Contains(plot.Id))
							{
								//throw new FormatException(string.Format("bool ID {0} already added", plot.Id));
								MessageBox.Show(string.Format(Localization.Editor_DuplicatePlotBool, plot.Id), Localization.Warning, MessageBoxButtons.OK,
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
								MessageBox.Show(string.Format(Localization.Editor_DuplicatePlotInt, plot.Id), Localization.Warning, MessageBoxButtons.OK,
									MessageBoxIcon.Warning);
								continue;
							}

							consumedInts.Add(plot.Id);

							var label = new Label
										{
											Text = string.Format(Localization.Editor_PlotEditor_ValueLabelFormat, plot.Name),
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

						intControl.Controls.Add(panel);
					}

					if (category.Floats.Count > 0)
					{
						var panel = new TableLayoutPanel
									{
										Dock = DockStyle.Fill,
										ColumnCount = 2,
										RowCount = category.Floats.Count + 1,
										AutoScroll = true,
										Padding = new Padding(0, 0, 20, 0)
									};

						panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

						foreach (var plot in category.Floats)
						{
							if (consumedFloats.Contains(plot.Id))
							{
								//throw new FormatException(string.Format("float ID {0} already added", plot.Id));
								MessageBox.Show(string.Format(Localization.Editor_DuplicatePlotInt, plot.Id), Localization.Warning, MessageBoxButtons.OK,
									MessageBoxIcon.Warning);

								continue;
							}

							consumedFloats.Add(plot.Id);

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
													DecimalPlaces = 4,
													Minimum = new decimal(float.MinValue),
													Maximum = new decimal(float.MaxValue),
													Increment = 1,
													Tag = plot,
												};

							numericUpDown.ValueChanged += OnPlotFloatValueChanged;
							panel.Controls.Add(numericUpDown);

							_plotInts.Add(numericUpDown);
						}

						floatControl.Controls.Add(panel);
					}

					if (category.PlayerVariables.Count > 0)
					{
						var panel = new TableLayoutPanel
									{
										Dock = DockStyle.Fill,
										ColumnCount = 2,
										RowCount = category.PlayerVariables.Count + 1,
										AutoScroll = true,
										Padding = new Padding(0, 0, 20, 0)
									};

						panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

						foreach (var plot in category.PlayerVariables)
						{
							//if (consumedPlayerVariables.Contains(plot.Id))
							if (consumedPlayerVariables.Exists(s => s.Equals(plot.Id, StringComparison.InvariantCultureIgnoreCase)))
							{
								//throw new FormatException(string.Format("int ID {0} already added", plot.Id));
								MessageBox.Show(string.Format(Localization.Editor_DuplicatePlotInt, plot.Id), Localization.Warning, MessageBoxButtons.OK,
									MessageBoxIcon.Warning);

								continue;
							}

							consumedPlayerVariables.Add(plot.Id);

							var label = new Label
										{
											Text = string.Format(Localization.Editor_PlotEditor_ValueLabelFormat, plot.Name),
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

							numericUpDown.ValueChanged += OnPlotPlayerVariableValueChanged;
							panel.Controls.Add(numericUpDown);

							_plotPlayerVariables.Add(numericUpDown);
						}

						playerVariablesControl.Controls.Add(panel);
					}
				}

				if (!tabs.Any())
				{
					continue;
				}

				var containerTabPage = new TabPage
									   {
										   Text = container.Name,
										   UseVisualStyleBackColor = true,
									   };
				_plotRootTabControl.TabPages.Add(containerTabPage);

				var containerTabControl = new TabControl
										  {
											  Dock = DockStyle.Fill,
										  };
				containerTabPage.Controls.Add(containerTabControl);

				containerTabControl.TabPages.AddRange(tabs.ToArray());
			}
		}

		private void AddTable(string name, List<BasicTableItem> items)
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
			for (var i = 0; i < items.Count; i++)
			// ReSharper restore ForCanBeConvertedToForeach
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

			_playerBasicPanel.Controls.Add(group);
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

			_playerBasicPanel.Controls.Add(group);
		}

		private void AddSquadTable(string name, List<BasicTableItem> items)
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
			for (var i = 0; i < items.Count; i++)
			{
				panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			}
			// ReSharper restore ForCanBeConvertedToForeach

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

			_squadBasicPanel.Controls.Add(group);
		}

		private void AddSquadTable(string name, List<Button> items)
		{
			var row = 0;

			var panel = new TableLayoutPanel
			{
				ColumnCount = 1,
				RowCount = items.Count
			};

			panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

			// ReSharper disable ForCanBeConvertedToForeach
			for (var i = 0; i < items.Count; i++)
			{
				panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			}
			// ReSharper restore ForCanBeConvertedToForeach

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

			_squadBasicPanel.Controls.Add(group);
		}

		private void AppendToPlotManualLog(string format, params object[] args)
		{
			if (string.IsNullOrEmpty(_plotManualLogTextBox.Text) == false)
			{
				_plotManualLogTextBox.AppendText(Environment.NewLine);
			}

			_plotManualLogTextBox.AppendText(string.Format(Thread.CurrentThread.CurrentCulture, format, args));
		}

		private void AppendToPlotManualPlayerVarLog(string format, params object[] args)
		{
			if (string.IsNullOrEmpty(_plotManualPlayerVarLogTextBox.Text) == false)
			{
				_plotManualPlayerVarLogTextBox.AppendText(Environment.NewLine);
			}

			_plotManualPlayerVarLogTextBox.AppendText(string.Format(Thread.CurrentThread.CurrentCulture, format, args));
		}

		private void BackupAutoSavesMenuItem_OnCheckedChanged(object sender, EventArgs e)
		{
			_backupAutoSaves = _backupAutoSavesMenuItem.Checked;

			if (_saveFileWatcher != null)
			{
				_saveFileWatcher.EnableRaisingEvents = _backupAutoSaves;
			}
		}

		private int GetHighestSaveNumber(string savePath)
		{
			var highestSaveNumber = 0;

			if (savePath == null || !Directory.Exists(savePath))
			{
				return highestSaveNumber;
			}

			foreach (var baseName in 
				Directory.EnumerateFiles(savePath, "*.pcsav")
					.OrderByDescending(Directory.GetLastWriteTime)
					.Select(Path.GetFileNameWithoutExtension)
					.Where(baseName => baseName != null && baseName.StartsWith("Save_") && baseName.Length == 9))
			{
				int saveNumber;

				if (int.TryParse(baseName.Substring(5).TrimStart('0'), out saveNumber))
				{
					highestSaveNumber = Math.Max(saveNumber, highestSaveNumber);
				}
			}

			return highestSaveNumber;
		}

		private void InitializePostComponent()
		{
			SuspendLayout();

			Icon = new Icon(new MemoryStream(Resources.Guardian));

			_useCareerPickerMenuItem.Checked = _useCareerPicker;
			_backupAutoSavesMenuItem.Checked = _backupAutoSaves;

			if (_savePath != null)
			{
				_rootSaveGameOpenFileDialog.InitialDirectory = _savePath;
				_rootSaveGameSaveFileDialog.InitialDirectory = _savePath;
			}
			else
			{
				_useCareerPickerMenuItem.Checked = false;
				_useCareerPickerMenuItem.Enabled = false;
				_rootOpenFromCareerMenuItem.Enabled = false;
				_rootSaveToCareerMenuItem.Enabled = false;
			}

			// ReSharper disable LocalizableElement
			const string playerBasicTabPageImageKey = "Tab_Player_Basic";
			const string playerAppearanceColorTabPageImageKey = "Tab_Player_Appearance_Color";
			const string playerAppearanceRootTabPageImageKey = "Tab_Player_Appearance_Root";
			const string rawTabPageImageKey = "Tab_Raw";
			const string plotRootTabPageImageKey = "Tab_Plot_Root";
			const string plotManualTabPageImageKey = "Tab_Plot_Manual";
			// ReSharper restore LocalizableElement

			_rootIconImageList.Images.Clear();
			_rootIconImageList.Images.Add("Unknown", new Bitmap(16, 16));
			_rootIconImageList.Images.Add(PlayerPlayerRootMaleImageKey,
				Resources.Editor_Tab_Player_Root_Male);
			_rootIconImageList.Images.Add(PlayerPlayerRootFemaleImageKey,
				Resources.Editor_Tab_Player_Root_Female);
			_rootIconImageList.Images.Add(playerBasicTabPageImageKey, Resources.Editor_Tab_Player_Basic);
			_rootIconImageList.Images.Add(playerAppearanceRootTabPageImageKey,
				Resources.Editor_Tab_Player_Appearance_Root);
			_rootIconImageList.Images.Add(playerAppearanceColorTabPageImageKey,
				Resources.Editor_Tab_Player_Appearance_Color);
			_rootIconImageList.Images.Add(rawTabPageImageKey, Resources.Editor_Tab_Raw);
			_rootIconImageList.Images.Add(plotRootTabPageImageKey, Resources.Editor_Tab_Plot_Root);
			_rootIconImageList.Images.Add(plotManualTabPageImageKey, Resources.Editor_Tab_Plot_Manual);

			_playerRootTabPage.ImageKey = PlayerPlayerRootMaleImageKey;
			_playerBasicTabPage.ImageKey = playerBasicTabPageImageKey;
			_playerAppearanceColorTabPage.ImageKey = playerAppearanceColorTabPageImageKey;
			_playerAppearanceRootTabPage.ImageKey = playerAppearanceRootTabPageImageKey;
			_rawTabPage.ImageKey = rawTabPageImageKey;
			_plotRootTabPage.ImageKey = plotRootTabPageImageKey;
			_plotManualTabPage.ImageKey = plotManualTabPageImageKey;

			//this.rootTabControl.SelectedTab = rawRootTabPage;
			_rawSplitContainer.Panel2Collapsed = true;

			// Player
			AddTable(Localization.Editor_BasicTable_Character_Label, Character.Build(this));
			AddTable(Localization.Editor_BasicTable_Reputation_Label, Reputation.Build(this));
			AddTable(Localization.Editor_BasicTable_Resources_Label, BasicTable.Resources.Build(this));

			// Squad
			AddSquadTable(Localization.Editor_BasicTable_Reset_Label, ResetCharacters.Build(this));

			// Plots
			AddPlotEditors();

			if (_saveFile != null)
			{
				var saveFile = _saveFile;
				SaveFile = null;
				SaveFile = saveFile;
			}

			ResumeLayout();
		}

		private void LoadDefaultFemaleSave()
		{
			using (var memory = new MemoryStream(Resources.DefaultFemaleSave))
			{
				LoadSaveFromStream(memory);
				SaveFile.Player.Guid = Guid.NewGuid();
			}
		}

		private void LoadDefaultMaleSave()
		{
			using (var memory = new MemoryStream(Resources.DefaultMaleSave))
			{
				LoadSaveFromStream(memory);
				SaveFile.Player.Guid = Guid.NewGuid();
			}
		}

		private static void LoadXmlConfig(string path = null)
		{
			if (path == null)
			{
				path = Path.Combine(GetExecutablePath(), "Config", "SaveEditorData.xml");
			}

			if (!File.Exists(path))
			{
				return;
			}

			var root = XElement.Load(path);

			/*if (root.Name != "SaveEditor")
			{
				return;
			}*/

			// LoadoutData
			var loadoutDataElement = root.Element("LoadoutData");

			if (loadoutDataElement != null)
			{
				// PowerClasses
				var currentElement = loadoutDataElement.Element("PowerClasses");
					
				if (currentElement != null)
				{
					var powerClasses = from el in currentElement.Elements("Power") select el;
					var elements = powerClasses as IList<XElement> ?? powerClasses.ToList();

					if (elements.Any())
					{
						LoadoutData.PowerClasses = new List<PowerClass>();

						foreach (var element in elements)
						{
							var className = (string)element.Attribute("ClassName");
							var customName = (string)element.Attribute("CustomName");
							var name = (string)element.Attribute("Name");
							var powerType = (string)element.Attribute("PowerType");

							if (className.IsNullOrWhiteSpace() || name.IsNullOrWhiteSpace() || powerType.IsNullOrWhiteSpace())
							{
								continue;
							}

							PowerClassType powerClassType;

							if (!Enum.TryParse(powerType, out powerClassType))
							{
								continue;
							}

							LoadoutData.PowerClasses.Add(new PowerClass(className, name, powerClassType, customName));
						}
					}
				}

				// WeaponClasses
				currentElement = loadoutDataElement.Element("WeaponClasses");
					
				if (currentElement != null)
				{
					var weaponClasses = from el in currentElement.Elements("Weapon") select el;
					var elements = weaponClasses as IList<XElement> ?? weaponClasses.ToList();

					if (elements.Any())
					{
						LoadoutData.WeaponClasses = new List<WeaponClass>();

						foreach (var element in elements)
						{
							var className = (string)element.Attribute("ClassName");
							var name = (string)element.Attribute("Name");
							var weaponType = (string)element.Attribute("WeaponType");

							if (className.IsNullOrWhiteSpace() || name.IsNullOrWhiteSpace() || weaponType.IsNullOrWhiteSpace())
							{
								continue;
							}

							WeaponClassType weaponClassType;

							if (!Enum.TryParse(weaponType, out weaponClassType))
							{
								continue;
							}

							LoadoutData.WeaponClasses.Add(new WeaponClass(className, name, weaponClassType));
						}
					}
				}

				// WeaponModClasses
				currentElement = loadoutDataElement.Element("WeaponModClasses");
					
				if (currentElement != null)
				{
					var weaponModClasses = from el in currentElement.Elements("WeaponMod") select el;
					var elements = weaponModClasses as IList<XElement> ?? weaponModClasses.ToList();

					if (elements.Any())
					{
						LoadoutData.WeaponModClasses = new List<WeaponModClass>();

						foreach (var element in elements)
						{
							var className = (string)element.Attribute("ClassName");
							var name = (string)element.Attribute("Name");
							var weaponType = (string)element.Attribute("WeaponType");

							if (className.IsNullOrWhiteSpace() || name.IsNullOrWhiteSpace() || weaponType.IsNullOrWhiteSpace())
							{
								continue;
							}

							WeaponClassType weaponClassType;

							if (!Enum.TryParse(weaponType, out weaponClassType))
							{
								continue;
							}

							LoadoutData.WeaponModClasses.Add(new WeaponModClass(className, name, weaponClassType));
						}
					}
				}

				// DefaultWeaponMods
				currentElement = loadoutDataElement.Element("DefaultWeaponMods");
					
				if (currentElement != null)
				{
					var defaultWeaponMods = from el in currentElement.Elements("DefaultWeaponMod") select el;
					var elements = defaultWeaponMods as IList<XElement> ?? defaultWeaponMods.ToList();

					if (elements.Any())
					{
						LoadoutData.DefaultWeaponMods = new List<LoadoutDataWeaponMod>();

						foreach (var element in elements)
						{
							var weaponType = (string)element.Attribute("WeaponType");
							var player = (string)element.Attribute("Player") ?? "";
							var henchman = (string)element.Attribute("Henchman") ?? "";

							if (weaponType.IsNullOrWhiteSpace())
							{
								continue;
							}

							WeaponClassType weaponClassType;

							if (!Enum.TryParse(weaponType, out weaponClassType))
							{
								continue;
							}

							var playerList = player.Replace(" ", "").Split(',');
							var henchmanList = henchman.Replace(" ", "").Split(',');

							LoadoutData.DefaultWeaponMods.Add(new LoadoutDataWeaponMod(weaponClassType, playerList, henchmanList));
						}
					}
				}

				// HenchmanClasses
				currentElement = loadoutDataElement.Element("HenchmanClasses");

				if (currentElement != null)
				{
					var henchmanClasses = from el in currentElement.Elements("Henchman") select el;
					var elements = henchmanClasses as IList<XElement> ?? henchmanClasses.ToList();

					if (elements.Any())
					{
						LoadoutData.HenchmenClasses = new List<HenchmanClass>();

						foreach (var element in elements)
						{
							var className = (string) element.Attribute("ClassName");
							var tag = (string) element.Attribute("Tag");
							var powers = (string) element.Attribute("Powers") ?? "";
							var weapons = (string) element.Attribute("Weapons") ?? "";
							var defaultPowers = (string) element.Attribute("DefaultPowers") ?? "";
							var defaultWeapons = (string) element.Attribute("DefaultWeapons") ?? "";

							if (className.IsNullOrWhiteSpace() || tag.IsNullOrWhiteSpace())
							{
								continue;
							}

							var powersSplit = powers.Replace(" ", "").Split(',');
							var weaponsSplit = weapons.Replace(" ", "").Split(',');
							var defaultPowersSplit = defaultPowers.Replace(" ", "").Split(',');
							var defaultWeaponsSplit = defaultWeapons.Replace(" ", "").Split(',');

							var weaponClassTypeParse = WeaponClassType.None;

							var weaponsList = (from s in weaponsSplit where Enum.TryParse(s, out weaponClassTypeParse) select weaponClassTypeParse).ToList();
							var defaultWeaponsList = (from s in defaultWeaponsSplit where Enum.TryParse(s, out weaponClassTypeParse) select weaponClassTypeParse).ToList();

							LoadoutData.HenchmenClasses.Add(new HenchmanClass(className, tag, powersSplit, weaponsList, defaultPowersSplit, defaultWeaponsList));
						}
					}
				}

				// PlayerClasses
				currentElement = loadoutDataElement.Element("PlayerClasses");
					
				if (currentElement != null)
				{
					var playerClasses = from el in currentElement.Elements("Player") select el;
					var elements = playerClasses as IList<XElement> ?? playerClasses.ToList();
			
					if (elements.Any())
					{
						LoadoutData.PlayerClasses = new List<Squad.PlayerClass>();

						foreach (var element in elements)
						{
							var characterClass = (string)element.Attribute("CharacterClass");
							var combatName = (string)element.Attribute("CombatName");
							var nonCombatName = (string)element.Attribute("NonCombatName");
							var displayName = (int?)element.Attribute("DisplayName") ?? -1;
							var powers = (string)element.Attribute("Powers") ?? "";
							var weapons = (string)element.Attribute("Weapons") ?? "";
							var defaultPowers = (string)element.Attribute("DefaultPowers") ?? "";
							var defaultWeapons = (string)element.Attribute("DefaultWeapons") ?? "";
				
							if (characterClass.IsNullOrWhiteSpace() || combatName.IsNullOrWhiteSpace() || nonCombatName.IsNullOrWhiteSpace() || displayName < 0)
							{
								continue;
							}

							PlayerCharacterClass playerCharacterClass;

							if (!Enum.TryParse(characterClass, out playerCharacterClass))
							{
								continue;
							}

							var powersSplit = powers.Replace(" ", "").Split(',');
							var weaponsSplit = weapons.Replace(" ", "").Split(',');
							var defaultPowersSplit = defaultPowers.Replace(" ", "").Split(',');
							var defaultWeaponsSplit = defaultWeapons.Replace(" ", "").Split(',');

							var weaponClassTypeParse = WeaponClassType.None;

							var weaponsList = (from s in weaponsSplit where Enum.TryParse(s, out weaponClassTypeParse) select weaponClassTypeParse).ToList();
							var defaultWeaponsList = (from s in defaultWeaponsSplit where Enum.TryParse(s, out weaponClassTypeParse) select weaponClassTypeParse).ToList();

							LoadoutData.PlayerClasses.Add(new Squad.PlayerClass(playerCharacterClass, combatName, nonCombatName, displayName, powersSplit, weaponsList, defaultPowersSplit, defaultWeaponsList));
						}
					}
				}
			}


			// LevelRewards
			var levelRewardsElement = root.Element("LevelRewards");

			if (levelRewardsElement == null)
			{
				return;
			}

			var levels = new SortedList<int, Tuple<int, int, int>>();
			var levelRewards = from el in levelRewardsElement.Elements("LevelReward") select el;

			foreach (var levelReward in levelRewards)
			{
				var level = (int?)levelReward.Attribute("Level") ?? -1;
				var experienceRequired = (int?)levelReward.Attribute("ExperienceRequired") ?? 0;
				var talentReward = (int?)levelReward.Attribute("TalentReward") ?? 0;
				var henchmanTalentReward = (int?)levelReward.Attribute("HenchmanTalentReward") ?? 0;

				if (level < 0 || level > SquadVariables.MaxPlayerLevel)
				{
					continue;
				}

				levels.Add(level, Tuple.Create(experienceRequired, talentReward, henchmanTalentReward));
			}

			SquadVariables.ExperienceRequired = levels.Select(pair => pair.Value.Item1).ToList();
			SquadVariables.PlayerTalentPoints = levels.Select(pair => pair.Value.Item2).ToList();
			SquadVariables.HenchTalentPoints = levels.Select(pair => pair.Value.Item3).ToList();
		}

		private SFXSaveGameFile LoadSave(Stream stream)
		{
			if (stream.ReadUInt32(ByteOrder.BigEndian) == 0x434F4E20) // 'CON '
			{
				MessageBox.Show(Localization.Editor_CannotLoadXbox360CONFile, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return null;
			}
			stream.Seek(-4, SeekOrigin.Current);

			SFXSaveGameFile saveFile;
			try
			{
				saveFile = SFXSaveGameFile.Read(stream);
			}
			catch (Exception e)
			{
				MessageBox.Show(string.Format(CultureInfo.InvariantCulture, Localization.Editor_SaveReadException, e), Localization.Error,
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return null;
			}

			if (saveFile.Version < 59)
			{
				MessageBox.Show(Localization.Editor_SaveFileTooOld, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return null;
			}

			return saveFile;
			//SaveFile = saveFile;
			//UpdatePlotEditors();
		}

		private void LoadSaveFromStream(Stream stream)
		{
			if (stream.ReadUInt32(ByteOrder.BigEndian) == 0x434F4E20) // 'CON '
			{
				MessageBox.Show(Localization.Editor_CannotLoadXbox360CONFile, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

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
				MessageBox.Show(string.Format(CultureInfo.InvariantCulture, Localization.Editor_SaveReadException, e), Localization.Error,
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			if (saveFile.Version < 59)
			{
				MessageBox.Show(Localization.Editor_SaveFileTooOld, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			SaveFile = saveFile;
			UpdatePlotEditors();
		}

		private void LoadSettings(string path = null)
		{
			//LoadLevelRewards(path);
			LoadXmlConfig(path);
		}

		private void OnDrawColorListBoxItem(object sender, DrawItemEventArgs e)
		{
			if (e.Index < 0)
			{
				return;
			}

			var g = e.Graphics;
			var listbox = (ListBox) sender;

			var backColor = (e.State & DrawItemState.Selected) != 0 ? SystemColors.Highlight : listbox.BackColor;
			var foreColor = (e.State & DrawItemState.Selected) != 0 ? SystemColors.HighlightText : listbox.ForeColor;

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

			g.FillRectangle(new HatchBrush(HatchStyle.LargeCheckerBoard, Color.White, Color.Gray), colorBounds);

			var item = listbox.Items[e.Index];

			var vector = item as MorphHead.VectorParameter;
			if (vector != null)
			{
				var valueColor = LinearColorToBgra(vector.Value).ToColor();

				g.FillRectangle(new SolidBrush(valueColor), colorBounds);
				g.DrawRectangle(Pens.Black, colorBounds);

				var format = StringFormat.GenericDefault;
				format.LineAlignment = StringAlignment.Center;

				e.Graphics.DrawString(vector.Name, listbox.Font, new SolidBrush(foreColor), textBoundsF, format);
			}
		}

		private void OnExportHeadMorph(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(Localization.Editor_NoActiveSave, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return;
			}

			if (SaveFile.Player.Appearance.HasMorphHead == false)
			{
				MessageBox.Show(Localization.Editor_NoHeadMorph, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return;
			}

			if (_rootMorphHeadSaveFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			var dir = Path.GetDirectoryName(_rootMorphHeadSaveFileDialog.FileName);
			_rootMorphHeadSaveFileDialog.InitialDirectory = dir;

			using (var output = _rootMorphHeadSaveFileDialog.OpenFile())
			{
				output.WriteString(HeadMorphMagic, Encoding.ASCII);
				output.WriteByte(0); // "version" in case I break something in the future
				output.WriteUInt32(SaveFile.Version);

				var writer = new FileWriter(output, SaveFile.Version, ByteOrder.LittleEndian);

				SaveFile.Player.Appearance.MorphHead.Serialize(writer);
			}
		}

		private void OnImportHeadMorph(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(Localization.Editor_NoActiveSave, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return;
			}

			if (_rootMorphHeadOpenFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			var dir = Path.GetDirectoryName(_rootMorphHeadOpenFileDialog.FileName);
			_rootMorphHeadOpenFileDialog.InitialDirectory = dir;

			using (var input = _rootMorphHeadOpenFileDialog.OpenFile())
			{
				if (input.ReadString(HeadMorphMagic.Length, Encoding.ASCII) != HeadMorphMagic)
				{
					MessageBox.Show(Localization.Editor_HeadMorphInvalid, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

					input.Close();

					return;
				}

				if (input.ReadUInt8() != 0)
				{
					MessageBox.Show(Localization.Editor_HeadMorphVersionUnsupported, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

					input.Close();

					return;
				}

				var version = input.ReadUInt32();

				if (version != SaveFile.Version)
				{
					if (MessageBox.Show(string.Format(Localization.Editor_HeadMorphVersionMaybeIncompatible, version, SaveFile.Version),
						Localization.Question, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
					{
						input.Close();
						return;
					}
				}

				var reader = new FileReader(input, version, ByteOrder.LittleEndian);
				var morphHead = new MorphHead();
				morphHead.Serialize(reader);
				SaveFile.Player.Appearance.MorphHead = morphHead;
				SaveFile.Player.Appearance.HasMorphHead = true;
			}
		}

		private void OnLoadAppearancePresetFromFile(object sender, EventArgs e)
		{
			if (_saveFile == null)
			{
				MessageBox.Show(Localization.Editor_NoActiveSave, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			if (_saveFile.Player.Appearance.HasMorphHead == false)
			{
				MessageBox.Show(Localization.Editor_NoHeadMorph, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			if (_rootAppearancePresetOpenFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			var dir = Path.GetDirectoryName(_rootAppearancePresetOpenFileDialog.FileName);
			_rootAppearancePresetOpenFileDialog.InitialDirectory = dir;

			string text;
			using (var input = _rootAppearancePresetOpenFileDialog.OpenFile())
			{
				var reader = new StreamReader(input);
				text = reader.ReadToEnd();
			}

			var preset = JsonConvert.DeserializeObject<AppearancePreset>(text);
			ApplyAppearancePreset(_saveFile.Player.Appearance.MorphHead, preset);
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

			_saveFile.Player.Appearance.MorphHead.VectorParameters.Add(new MorphHead.VectorParameter
																	   {
																		   Name = input.InputText,
																		   Value = new LinearColor(1, 1, 1, 1),
																	   });
		}

		private void OnPlayerAppearanceColorChange(object sender, EventArgs e)
		{
			var item = _playerAppearanceColorListBox.SelectedItem as MorphHead.VectorParameter;
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

		private void OnPlayerAppearanceColorRemove(object sender, EventArgs e)
		{
			var item = _playerAppearanceColorListBox.SelectedItem as MorphHead.VectorParameter;
			if (item != null)
			{
				_saveFile.Player.Appearance.MorphHead.VectorParameters.Remove(item);
			}
		}

		private void OnPlayerAppearancePropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "MorphHead")
			{
				_rootVectorParametersBindingSource.DataSource =
					_saveFile.Player.Appearance.MorphHead.VectorParameters;
			}
		}

		private void OnPlayerPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			// goofy?

			if (e.PropertyName == "IsFemale")
			{
				_playerRootTabPage.ImageKey =
					(_saveFile == null || _saveFile.Player.IsFemale == false)
						? "Tab_Player_Root_Male"
						: "Tab_Player_Root_Female";
			}
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

		private void OnPlotFloatValueChanged(object sender, EventArgs e)
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

			var plot = numericUpDown.Tag as PlotFloat;

			if (plot == null)
			{
				return;
			}

			SaveFile.Plot.SetFloatVariable(plot.Id, (float) numericUpDown.Value);
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

		private void OnPlotManualClearLog(object sender, EventArgs e)
		{
			_plotManualLogTextBox.Clear();
		}

		private void OnPlotManualGetBool(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(Localization.Editor_NoActiveSave, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return;
			}

			int id;
			if (int.TryParse(_plotManualBoolIdTextBox.Text, NumberStyles.None, Thread.CurrentThread.CurrentCulture, out id) == false)
			{
				MessageBox.Show(Localization.Editor_FailedToParseId, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			var value = SaveFile.Plot.GetBoolVariable(id);
			AppendToPlotManualLog(Localization.Editor_PlotManualLogBoolGet, id, value);
			_plotManualBoolValueCheckBox.Checked = value;
		}

		private void OnPlotManualGetFloat(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(Localization.Editor_NoActiveSave, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			int id;

			if (int.TryParse(_plotManualFloatIdTextBox.Text, NumberStyles.None, Thread.CurrentThread.CurrentCulture, out id) == false)
			{
				MessageBox.Show(Localization.Editor_FailedToParseId, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return;
			}

			var value = SaveFile.Plot.GetFloatVariable(id);
			AppendToPlotManualLog(Localization.Editor_PlotManualLogFloatGet, id, value);
			_plotManualFloatValueTextBox.Text = value.ToString(Thread.CurrentThread.CurrentCulture);
		}

		private void OnPlotManualGetInt(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(Localization.Editor_NoActiveSave, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return;
			}

			int id;

			if (int.TryParse(_plotManualIntIdTextBox.Text, NumberStyles.None, Thread.CurrentThread.CurrentCulture, out id) == false)
			{
				MessageBox.Show(Localization.Editor_FailedToParseId, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return;
			}

			var value = SaveFile.Plot.GetIntVariable(id);
			AppendToPlotManualLog(Localization.Editor_PlotManualLogIntGet, id, value);
			_plotManualIntValueTextBox.Text = value.ToString(Thread.CurrentThread.CurrentCulture);
		}

		private void OnPlotManualGetPlayerVar(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(Localization.Editor_NoActiveSave, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			var id = _plotManualPlayerVarIdTextBox.Text;
			var playerVarible = SaveFile.PlayerVariables.Find(v => v.Name.Equals(id, StringComparison.CurrentCultureIgnoreCase));

			if (playerVarible == null)
			{
				MessageBox.Show(Localization.Editor_FailedToParseId, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			var value = playerVarible.Value;

			AppendToPlotManualPlayerVarLog(Localization.Editor_PlotManualPlayerVarLogGet, id, value);
			_plotManualPlayerVarValueTextBox.Text = value.ToString(Thread.CurrentThread.CurrentCulture);
		}

		private void OnPlotManualPlayerVarClearLog(object sender, EventArgs e)
		{
			_plotManualPlayerVarLogTextBox.Clear();
		}

		private void OnPlotManualSetBool(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(Localization.Editor_NoActiveSave, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return;
			}

			int id;
			if (int.TryParse(_plotManualBoolIdTextBox.Text, NumberStyles.None, Thread.CurrentThread.CurrentCulture, out id) == false)
			{
				MessageBox.Show(Localization.Editor_FailedToParseId, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			var newValue = _plotManualBoolValueCheckBox.Checked;
			var oldValue = SaveFile.Plot.GetBoolVariable(id);
			SaveFile.Plot.SetBoolVariable(id, newValue);
			AppendToPlotManualLog(Localization.Editor_PlotManualLogBoolSet, id, newValue, oldValue);
		}

		private void OnPlotManualSetFloat(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(Localization.Editor_NoActiveSave, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return;
			}

			int id;

			if (int.TryParse(_plotManualFloatIdTextBox.Text, NumberStyles.None, Thread.CurrentThread.CurrentCulture, out id) == false)
			{
				MessageBox.Show(Localization.Editor_FailedToParseId, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return;
			}

			float newValue;

			if (float.TryParse(_plotManualIntValueTextBox.Text, NumberStyles.None, Thread.CurrentThread.CurrentCulture, out newValue) == false)
			{
				MessageBox.Show(Localization.Editor_FailedToParseValue, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return;
			}

			var oldValue = SaveFile.Plot.GetFloatVariable(id);
			SaveFile.Plot.SetFloatVariable(id, newValue);
			AppendToPlotManualLog(Localization.Editor_PlotManualLogFloatSet, id, newValue, oldValue);
		}

		private void OnPlotManualSetInt(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(Localization.Editor_NoActiveSave, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return;
			}

			int id;

			if (int.TryParse(_plotManualIntIdTextBox.Text, NumberStyles.None, Thread.CurrentThread.CurrentCulture, out id) == false)
			{
				MessageBox.Show(Localization.Editor_FailedToParseId, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return;
			}

			int newValue;

			if (int.TryParse(_plotManualIntValueTextBox.Text, NumberStyles.None, Thread.CurrentThread.CurrentCulture, out newValue) == false)
			{
				MessageBox.Show(Localization.Editor_FailedToParseValue, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return;
			}

			var oldValue = SaveFile.Plot.GetIntVariable(id);
			SaveFile.Plot.SetIntVariable(id, newValue);
			AppendToPlotManualLog(Localization.Editor_PlotManualLogIntSet, id, newValue, oldValue);
		}

		private void OnPlotManualSetPlayerVar(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(Localization.Editor_NoActiveSave, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			var id = _plotManualPlayerVarIdTextBox.Text;
			var playerVariable = SaveFile.PlayerVariables.Find(v => v.Name.Equals(id, StringComparison.CurrentCultureIgnoreCase));

			if (playerVariable == null)
			{
				playerVariable = new PlayerVariable
								 {
									 Name = id,
									 Value = 0
								 };

				SaveFile.PlayerVariables.Add(playerVariable);
			}

			var oldValue = playerVariable.Value;
			var newValue = 0;

			if (int.TryParse(_plotManualPlayerVarValueTextBox.Text, NumberStyles.None, Thread.CurrentThread.CurrentCulture, out newValue) == false)
			{
				MessageBox.Show(Localization.Editor_FailedToParseValue, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			playerVariable.Value = newValue;

			AppendToPlotManualPlayerVarLog(Localization.Editor_PlotManualPlayerVarLogSet, id, newValue, oldValue);
		}

		private void OnPlotPlayerVariableValueChanged(object sender, EventArgs e)
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

			var plot = numericUpDown.Tag as PlotPlayerVariable;

			if (plot == null)
			{
				return;
			}

			var id = plot.Id;
			var value = (int) numericUpDown.Value;

			var playerVariable = SaveFile.PlayerVariables.Find(v => v.Name.Equals(id, StringComparison.CurrentCultureIgnoreCase));

			if (playerVariable == null)
			{
				playerVariable = new PlayerVariable
								 {
									 Name = id,
									 Value = 0
								 };

				SaveFile.PlayerVariables.Add(playerVariable);
			}

			playerVariable.Value = value;
		}

		private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			_rawParentPropertyGrid.Refresh();
		}

		private void OnRootTabIndexChanged(object sender, EventArgs e)
		{
			if (_rootTabControl.SelectedTab == _rawTabPage)
			{
				// HACK: refresh property grids, just in case
				_rawParentPropertyGrid.Refresh();
				_rawChildPropertyGrid.Refresh();
			}
		}

		private void OnSaveAppearancePresetToFile(object sender, EventArgs e)
		{
			if (_saveFile == null)
			{
				MessageBox.Show(Localization.Editor_NoActiveSave, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			if (_saveFile.Player.Appearance.HasMorphHead == false)
			{
				MessageBox.Show(Localization.Editor_NoHeadMorph, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			if (_rootAppearancePresetSaveFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			var dir = Path.GetDirectoryName(_rootAppearancePresetSaveFileDialog.FileName);
			_rootAppearancePresetSaveFileDialog.InitialDirectory = dir;

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

			using (var output = File.Create(_rootAppearancePresetSaveFileDialog.FileName))
			{
				var writer = new StreamWriter(output);
				writer.Write(JsonConvert.SerializeObject(preset, Formatting.Indented));
				writer.Flush();
			}
		}

		private void OnSaveNewFemale(object sender, EventArgs e)
		{
			LoadDefaultFemaleSave();
		}

		private void OnSaveNewMale(object sender, EventArgs e)
		{
			LoadDefaultMaleSave();
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
					MessageBox.Show(Localization.Editor_ThisShouldNeverHappen, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

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
			if (_rootSaveGameOpenFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			var dir = Path.GetDirectoryName(_rootSaveGameOpenFileDialog.FileName);
			_rootSaveGameOpenFileDialog.InitialDirectory = dir;

			using (var input = _rootSaveGameOpenFileDialog.OpenFile())
			{
				LoadSaveFromStream(input);
			}
		}

		private void OnSaveOpenFromGeneric(object sender, EventArgs e)
		{
			if (_useCareerPickerMenuItem.Checked)
			{
				OnSaveOpenFromCareer(sender, e);
			}
			else
			{
				OnSaveOpenFromFile(sender, e);
			}
		}

		private void OnSaveSaveToCareer(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(Localization.Editor_NoActiveSave, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

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

					//SaveFile.PlayerVariables.Sort((variable, playerVariable) => string.Compare(variable.Name, playerVariable.Name, StringComparison.OrdinalIgnoreCase));

					using (var output = File.Create(picker.SelectedPath))
					{
						SFXSaveGameFile.Write(SaveFile, output);
					}

					var shortName = picker.SelectedPath.Substring(_savePath.Length + 1);
					var hash = ComputeMD5Hash(picker.SelectedPath);

					if (_manualSaveHashes.ContainsKey(shortName))
					{
						_manualSaveHashes[shortName] = hash;
					}
					else
					{
						_manualSaveHashes.Add(shortName, hash);
					}

					UpdatePlayerVarAutoComplete();
				}
				else
				{
					MessageBox.Show(Localization.Editor_ThisShouldNeverHappen, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
		}

		private void OnSaveSaveToFile(object sender, EventArgs e)
		{
			if (SaveFile == null)
			{
				MessageBox.Show(Localization.Editor_NoActiveSave, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return;
			}

			_rootSaveGameSaveFileDialog.FilterIndex =
				SaveFile.ByteOrder == ByteOrder.LittleEndian ? 1 : 2;

			if (_rootSaveGameSaveFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			var dir = Path.GetDirectoryName(_rootSaveGameSaveFileDialog.FileName);
			_rootSaveGameSaveFileDialog.InitialDirectory = dir;

			SaveFile.ByteOrder = _rootSaveGameSaveFileDialog.FilterIndex != 2
				? ByteOrder.LittleEndian
				: ByteOrder.BigEndian;

			//SaveFile.PlayerVariables.Sort((variable, playerVariable) => string.Compare(variable.Name, playerVariable.Name, StringComparison.OrdinalIgnoreCase));

			using (var output = _rootSaveGameSaveFileDialog.OpenFile())
			{
				SFXSaveGameFile.Write(SaveFile, output);
			}

			UpdatePlayerVarAutoComplete();
		}

		private void OnSaveSaveToGeneric(object sender, EventArgs e)
		{
			if (_useCareerPickerMenuItem.Checked)
			{
				OnSaveSaveToCareer(sender, e);
			}
			else
			{
				OnSaveSaveToFile(sender, e);
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
					_rawChildPropertyGrid.SelectedObject = e.NewSelection.Value;
					_rawSplitContainer.Panel2Collapsed = false;

					var newPc = e.NewSelection.Value as INotifyPropertyChanged;

					if (newPc != null)
					{
						newPc.PropertyChanged += OnPropertyChanged;
					}

					return;
				}
			}

			_rawChildPropertyGrid.SelectedObject = null;
			_rawSplitContainer.Panel2Collapsed = true;
		}

		private void SaveFileWatcherOnChanged(object sender, FileSystemEventArgs eventArgs)
		{
			if (!_backupAutoSaves)
			{
				return;
			}

			var processes = Process.GetProcessesByName("MassEffect3");

			if (!processes.Any())
			{
				return;
			}

			var fullPath = eventArgs.FullPath;
			var savePath = Path.GetDirectoryName(fullPath) ?? fullPath;
			var name = eventArgs.Name;
			var saveName = Path.GetFileName(fullPath) ?? name;

			if (!name.EndsWith(@"\AutoSave.pcsav", StringComparison.InvariantCultureIgnoreCase)
				&& !name.EndsWith(@"\ChapterSave.pcsav", StringComparison.InvariantCultureIgnoreCase))
			{
				return;
			}

			var fileInfo = new FileInfo(fullPath);

			if (!fileInfo.Exists || fileInfo.Length == 0)
			{
				return;
			}

			switch (eventArgs.ChangeType)
			{
				case WatcherChangeTypes.Changed:
				case WatcherChangeTypes.Created:
				{
					DateTime? previousTime = null;
					var currentTime = DateTime.Now;

					if (_fileTimes.ContainsKey(name))
					{
						previousTime = _fileTimes[name];
					}
					else
					{
						_fileTimes.Add(name, currentTime);
					}

					var timeSinceLastEvent = currentTime - (previousTime ?? currentTime);
					var canBackupFile = (timeSinceLastEvent >= _fileTimeSpan) || timeSinceLastEvent.Milliseconds == 0;
					_fileTimes[name] = currentTime;

					if (!canBackupFile)
					{
						return;
					}

					var saveNumber = GetHighestSaveNumber(savePath) + 1;
					var path = Path.Combine(savePath, string.Format("Save_{0}.pcsav", saveNumber.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0')));

					/*if (!File.Exists(fullPath))
					{
						return;
					}*/

					if (_manualSaveHashes.ContainsKey(name) && _manualSaveHashes[name] != null)
					{
						if (_manualSaveHashes[name].Equals(ComputeMD5Hash(fullPath)))
						{
							return;
						}
					}

					using (var tempStream = File.OpenRead(fullPath))
					{
						var tempSave = SFXSaveGameFile.Read(tempStream);

						if (tempSave.Plot.GetBoolVariable(17700))
						{
							return;
						}
					}

					File.Copy(fullPath, path);

					break;
				}
				case WatcherChangeTypes.Deleted:
				case WatcherChangeTypes.Renamed:
				{
					return;
				}
			}
		}

		private void SaveFileWatcherOnError(object sender, ErrorEventArgs eventArgs)
		{
			//
		}

		private void UpdatePlayerVarAutoComplete()
		{
			_playerVarAutoCompleteCollection.Clear();

			foreach (var playerVar in _saveFile.PlayerVariables)
			{
				_playerVarAutoCompleteCollection.Add(playerVar.Name);
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

			foreach (var numericUpDown in _plotFloats)
			{
				var plot = numericUpDown.Tag as PlotFloat;

				if (plot == null)
				{
					continue;
				}

				numericUpDown.Value = new decimal(SaveFile.Plot.GetFloatVariable(plot.Id));
			}

			foreach (var numericUpDown in _plotPlayerVariables)
			{
				var plot = numericUpDown.Tag as PlotPlayerVariable;

				if (plot == null)
				{
					continue;
				}

				var firstOrDefault =
					SaveFile.PlayerVariables.FirstOrDefault(variable => variable.Name.Equals(plot.Id, StringComparison.InvariantCultureIgnoreCase));

				if (firstOrDefault != null)
				{
					numericUpDown.Value = firstOrDefault.Value;
				}
				else
				{
					numericUpDown.Value = 0;
				}
			}

			_updatingPlotEditors--;
		}

		private void UseCareerPickerMenuItem_OnCheckedChanged(object sender, EventArgs e)
		{
			_useCareerPicker = _useCareerPickerMenuItem.Checked;
		}

		private void compareToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var saves = new SFXSaveGameFile[2];

			// Save1
			if (_rootSaveGameOpenFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			var dir = Path.GetDirectoryName(_rootSaveGameOpenFileDialog.FileName);
			_rootSaveGameOpenFileDialog.InitialDirectory = dir;

			using (var input = _rootSaveGameOpenFileDialog.OpenFile())
			{
				saves[0] = LoadSave(input);
			}

			// Save2
			if (_rootSaveGameOpenFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			dir = Path.GetDirectoryName(_rootSaveGameOpenFileDialog.FileName);
			_rootSaveGameOpenFileDialog.InitialDirectory = dir;

			using (var input = _rootSaveGameOpenFileDialog.OpenFile())
			{
				saves[1] = LoadSave(input);
			}

			if (saves[0] == null || saves[1] == null)
			{
				return;
			}

			var stringBuilder = new StringBuilder();

			using (var writer = new StreamWriter("comparison.txt", false))
			{
				var validTypes = new[] { typeof(uint), typeof(bool), typeof(byte), typeof(int), typeof(string), typeof(float), typeof(Guid) };
				var processed = new HashSet<object>();
				SFXSaveGameFile.CompareObjects(saves[0], saves[1], "", validTypes, processed, stringBuilder);
				//File.WriteAllText(Application.StartupPath + "/compare_result.txt", stringBuilder.ToString());
				writer.Write(stringBuilder.ToString());

				/*writer.WriteLine();
				writer.WriteLine("Bools:");

				for (var i = 0; i < saves[0].Plot.BoolVariables.Count && i < saves[1].Plot.BoolVariables.Count; i++)
				{
					if (saves[0].Plot.BoolVariables[i] != saves[1].Plot.BoolVariables[i])
					{
						writer.WriteLine("{0}: {1} => {2}", i, saves[0].Plot.BoolVariables[i], saves[1].Plot.BoolVariables[i]);
					}
				}

				

				writer.WriteLine();
				writer.WriteLine("Floats:");

				for (var i = 0; i < saves[0].Plot.FloatVariables.Count && i < saves[1].Plot.FloatVariables.Count; i++)
				{
					var f1 = saves[0].Plot.FloatVariables[i];
					var f2 = saves[1].Plot.FloatVariables[i];

					if (f1.Index != f2.Index && f1.Value != f2.Value)
					{
						writer.WriteLine("[{1}] = {2} => [{3}] = {4}", i, f1.Index, f1.Value, f2.Index, f2.Value);
					}
				}

				writer.WriteLine();
				writer.WriteLine("Ints:");

				var diffInts = saves[0].Plot.IntVariables.Where(x => !saves[1].Plot.IntVariables.Any(x1 => x1.Index == x.Index && x1.Value != x.Value))
			.Union(saves[1].Plot.IntVariables.Where(x => !saves[0].Plot.IntVariables.Any(x1 => x1.Index == x.Index && x1.Value != x.Value)));


				foreach (var diffInt in diffInts)
				{
					//writer.WriteLine("[{1}] = {2} => [{3}] = {4}", diffInt., i1.Index, i1.Value, i2.Index, i2.Value);
					writer.WriteLine($"{diffInt.Index} => {diffInt.Value}");
				}*/

				/*for (var i = 0; i < saves[0].Plot.IntVariables.Count && i < saves[1].Plot.IntVariables.Count; i++)
				{
					var i1 = saves[0].Plot.IntVariables[i];
					var i2 = saves[1].Plot.IntVariables[i];

					if (i1.Index != i2.Index && i1.Value != i2.Value)
					{
						writer.WriteLine("[{1}] = {2} => [{3}] = {4}", i, i1.Index, i1.Value, i2.Index, i2.Value);
					}
				}*/
			}
		}
	}
}
