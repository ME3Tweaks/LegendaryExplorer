using System;
using System.ComponentModel;
using System.IO;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Forms;
using Gammtek.Conduit;
using Gammtek.Conduit.Reflection;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace MassEffect3.TlkEditor
{
	/// <summary>
	///     Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : INotifyPropertyChanged
	{
		private String _inputXmlFilePath;
		private string _inputTlkFilePath;
		private string _outputTextFilePath;
		private string _inputXmlFilePath1;
		private string _outputTlkFilePath;

		public MainWindow()
		{
			InitializeComponent();
		}

		public string InputTlkFilePath
		{
			get { return _inputTlkFilePath; }
			set { SetProperty(ref _inputTlkFilePath, value); }
		}

		public string OutputTextFilePath
		{
			get { return _outputTextFilePath; }
			set { SetProperty(ref _outputTextFilePath, value); }
		}

		public string InputXmlFilePath
		{
			get { return _inputXmlFilePath1; }
			set { SetProperty(ref _inputXmlFilePath1, value); }
		}

		public string OutputTlkFilePath
		{
			get { return _outputTlkFilePath; }
			set { SetProperty(ref _outputTlkFilePath, value); }
		}

		private void InputTlkFilePathButton_Click(object sender, RoutedEventArgs e)
		{
			var openFileDialog = new OpenFileDialog
			{
				Multiselect = false,
				Filter = Properties.Resources.TlkFilesFilter
			};

			if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				InputTlkFilePath = openFileDialog.FileName;
				TextInputTlkFilePath.Text = InputTlkFilePath;

				OutputTextFilePath = Path.ChangeExtension(InputTlkFilePath, "xml");
				TextOutputTextFilePath.Text = OutputTextFilePath;
			}
		}

		private void OutputTextFilePathButton_Click(object sender, RoutedEventArgs e)
		{
			var saveFileDialog = new SaveFileDialog
			{
				Filter = Properties.Resources.TextFilesFilter
			};

			if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				OutputTextFilePath = saveFileDialog.FileName;
				TextOutputTextFilePath.Text = OutputTextFilePath;
			}
		}

		private void InputXmlFilePathButton_Click(object sender, RoutedEventArgs e)
		{
			var openFileDialog = new OpenFileDialog
			{
				Multiselect = false,
				Filter = Properties.Resources.XmlFilesFilter
			};

			if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				_inputXmlFilePath = openFileDialog.FileName;
				TextInputXmlFilePath.Text = _inputXmlFilePath;

				OutputTlkFilePath = Path.ChangeExtension(_inputXmlFilePath, "tlk");
				TextOutputTlkFilePath.Text = OutputTlkFilePath;
			}
		}

		private void OutputTlkFilePathButton_Click(object sender, RoutedEventArgs e)
		{
			var saveFileDialog = new SaveFileDialog
			{
				Filter = Properties.Resources.TlkFilesFilter
			};

			if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				OutputTlkFilePath = saveFileDialog.FileName;
				TextOutputTlkFilePath.Text = OutputTlkFilePath;
			}
		}

		private void Exit_Click(object sender, RoutedEventArgs e)
		{
			Application.Current.Shutdown();
		}

		private void StartReadingTlkButton_Click(object sender, RoutedEventArgs e)
		{
			BusyReading(true);
			const TalkFile.Fileformat ff = TalkFile.Fileformat.Xml;

			var loadingWorker = new BackgroundWorker {WorkerReportsProgress = true};

			loadingWorker.ProgressChanged += delegate(object sender2, ProgressChangedEventArgs e2) { readingTlkProgressBar.Value = e2.ProgressPercentage; };

			loadingWorker.DoWork += delegate
			{
				//try
				//{
					var tf = new TalkFile();
					tf.LoadTlkData(InputTlkFilePath);

					tf.ProgressChanged += loadingWorker.ReportProgress;
					tf.DumpToFile(OutputTextFilePath, ff);
					// debug
					// tf.PrintHuffmanTree();
					tf.ProgressChanged -= loadingWorker.ReportProgress;
				/*}
				catch (FileNotFoundException)
				{
					MessageBox.Show(Properties.Resources.AlertExceptionTlkNotFound, Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
				}
				catch (IOException)
				{
					MessageBox.Show(Properties.Resources.AlertExceptionTlkFormat, Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
				}
				catch (Exception ex)
				{*/
					/*var message = Properties.Resources.AlertExceptionGeneric;
					message += Properties.Resources.AlertExceptionGenericDescription + ex.Message;*/
					//MessageBox.Show(ex.StackTrace, Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Exclamation);
				//}
			};

			loadingWorker.RunWorkerCompleted += delegate
			{
				BusyReading(false);
				MessageBox.Show(Properties.Resources.AlertTlkLoadingFinished, Properties.Resources.Done, MessageBoxButton.OK, MessageBoxImage.Information);
			};

			loadingWorker.RunWorkerAsync();
		}

		private void StartWritingTlkButton_Click(object sender, RoutedEventArgs e)
		{
			var debugVersion = DebugCheckBox.IsChecked == true;
			BusyWriting(true);
			var writingWorker = new BackgroundWorker();

			writingWorker.DoWork += delegate
			{
				//try
				//{
					var hc = new HuffmanCompression();
					hc.LoadInputData(_inputXmlFilePath, TalkFile.Fileformat.Xml, debugVersion);

					hc.SaveToTlkFile(OutputTlkFilePath);
				/*}
				catch (FileNotFoundException)
				{
					MessageBox.Show(
						Properties.Resources.AlertExceptionXmlNotFound, Properties.Resources.Error,
						MessageBoxButton.OK, MessageBoxImage.Error);
				}
				catch (Exception ex)
				{
					var message = Properties.Resources.AlertExceptionGeneric;
					message += Properties.Resources.AlertExceptionGenericDescription + ex.Message;
					MessageBox.Show(message, Properties.Resources.Error,
									MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}*/
			};

			writingWorker.RunWorkerCompleted += delegate
			{
				BusyWriting(false);
				MessageBox.Show(Properties.Resources.AlertWritingTlkFinished, Properties.Resources.Done,
								MessageBoxButton.OK, MessageBoxImage.Information);
			};

			writingWorker.RunWorkerAsync();
		}

		private void BusyReading(bool busy)
		{
			if (busy)
			{
				InputTlkFilePathButton.IsEnabled = false;
				OutputTextFilePathButton.IsEnabled = false;
				tabItem2.IsEnabled = false;
				startReadingTlkButton.Visibility = Visibility.Hidden;
				readingTlkProgressBar.Visibility = Visibility.Visible;
				readingTlkProgressBar.Value = 0;
			}
			else
			{
				InputTlkFilePathButton.IsEnabled = true;
				OutputTextFilePathButton.IsEnabled = true;
				tabItem2.IsEnabled = true;
				startReadingTlkButton.Visibility = Visibility.Visible;
				readingTlkProgressBar.Visibility = Visibility.Hidden;
			}
		}

		private void BusyWriting(bool busy)
		{
			if (busy)
			{
				InputXmlFilePathButton.IsEnabled = false;
				OutputTlkFilePathButton.IsEnabled = false;
				tabItem1.IsEnabled = false;
				startWritingTlkButton.Visibility = Visibility.Hidden;
				writingTlkProgressBar.Visibility = Visibility.Visible;
				DebugCheckBox.IsEnabled = false;
			}
			else
			{
				InputXmlFilePathButton.IsEnabled = true;
				OutputTlkFilePathButton.IsEnabled = true;
				tabItem1.IsEnabled = true;
				startWritingTlkButton.Visibility = Visibility.Visible;
				writingTlkProgressBar.Visibility = Visibility.Hidden;
				DebugCheckBox.IsEnabled = true;
			}
		}

		private void Root_Closed(object sender, EventArgs e)
		{
			/*Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            config.AppSettings.Settings["InputTlkFilePath"].Value = TextInputTlkFilePath.Text;
            config.AppSettings.Settings["OutputTextFilePath"].Value = TextOutputTextFilePath.Text;
            config.AppSettings.Settings["InputXmlFilePath"].Value = TextInputXmlFilePath.Text;
            config.AppSettings.Settings["OutputTlkFilePath"].Value = TextOutputTlkFilePath.Text;

            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");*/
		}

		private void About_Click(object sender, RoutedEventArgs e)
		{
			var aboutWindow = new AboutWindow
			{
				Owner = this
			};
			aboutWindow.ShowDialog();
		}

		private void HowTo_Click(object sender, RoutedEventArgs e)
		{
			var howToWindow = new HowToUseWindow
			{
				Owner = this
			};
			howToWindow.Show();
		}

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
		{
			if (Equals(storage, value))
			{
				return false;
			}

			storage = value;

			if (propertyName != null)
			{
				OnPropertyChanged(propertyName);
			}

			return true;
		}

		[NotifyPropertyChangedInvocator]
		protected void OnPropertyChanged(string propertyName)
		{
			var handler = PropertyChanged;

			if (handler != null)
			{
				handler(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		[NotifyPropertyChangedInvocator]
		protected void OnPropertyChanged<T>(Expression<Func<T>> propertyExpression)
		{
			var propertyName = PropertySupport.ExtractPropertyName(propertyExpression);

			if (propertyName != null)
			{
				OnPropertyChanged(propertyName);
			}
		}
	}
}
