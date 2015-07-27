using System;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private String _inputTlkFilePath = ConfigurationManager.AppSettings["InputTlkFilePath"];
        private String _outputTextFilePath = ConfigurationManager.AppSettings["OutputTextFilePath"];
        private String _inputXmlFilePath = ConfigurationManager.AppSettings["InputXmlFilePath"];
        private String _outputTlkFilePath = ConfigurationManager.AppSettings["OutputTlkFilePath"];

        public string InputTlkFilePath
        {
            get { return _inputTlkFilePath; }
            set { _inputTlkFilePath = value; }
        }

        public string OutputTextFilePath
        {
            get { return _outputTextFilePath; }
            set { _outputTextFilePath = value; }
        }

        public string InputXmlFilePath
        {
            get { return _inputXmlFilePath; }
            set { _inputTlkFilePath = value; }
        }

        public string OutputTlkFilePath
        {
            get { return _outputTlkFilePath; }
            set { _outputTlkFilePath = value; }
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void InputTlkFilePathButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = false;
            openFileDialog.Filter = Properties.Resources.TlkFilesFilter;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _inputTlkFilePath = openFileDialog.FileName;
                TextInputTlkFilePath.Text = _inputTlkFilePath;
            }
        }

        private void OutputTextFilePathButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = Properties.Resources.TextFilesFilter;
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _outputTextFilePath = saveFileDialog.FileName;
                TextOutputTextFilePath.Text = _outputTextFilePath;
            }
        }

        private void InputXmlFilePathButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = false;
            openFileDialog.Filter = Properties.Resources.XmlFilesFilter;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _inputXmlFilePath = openFileDialog.FileName;
                TextInputXmlFilePath.Text = _inputXmlFilePath;
            }
        }

        private void OutputTlkFilePathButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = Properties.Resources.TlkFilesFilter;
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _outputTlkFilePath = saveFileDialog.FileName;
                TextOutputTlkFilePath.Text = _outputTlkFilePath;
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void StartReadingTlkButton_Click(object sender, RoutedEventArgs e)
        {
            BusyReading(true);
            TalkFile.Fileformat ff = TalkFile.Fileformat.Xml;

            var loadingWorker = new BackgroundWorker();
            loadingWorker.WorkerReportsProgress = true;

            loadingWorker.ProgressChanged += delegate(object sender2, ProgressChangedEventArgs e2)
            {
                readingTlkProgressBar.Value = e2.ProgressPercentage;
            };

            loadingWorker.DoWork += delegate
            {
                try
                {
                    TalkFile tf = new TalkFile();
                    tf.LoadTlkData(_inputTlkFilePath);

                    tf.ProgressChanged += loadingWorker.ReportProgress;
                    tf.DumpToFile(_outputTextFilePath, ff);
                    // debug
                    // tf.PrintHuffmanTree();
                    tf.ProgressChanged -= loadingWorker.ReportProgress;
                }
                catch (FileNotFoundException)
                {
                    System.Windows.MessageBox.Show(
                        Properties.Resources.AlertExceptionTlkNotFound, Properties.Resources.Error,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (IOException)
                {
                    System.Windows.MessageBox.Show(
                        Properties.Resources.AlertExceptionTlkFormat, Properties.Resources.Error,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    string message = Properties.Resources.AlertExceptionGeneric;
                    message += Properties.Resources.AlertExceptionGenericDescription + ex.Message;
                    System.Windows.MessageBox.Show(message, Properties.Resources.Error,
                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            };

            loadingWorker.RunWorkerCompleted += delegate
            {
                BusyReading(false);
                System.Windows.MessageBox.Show(Properties.Resources.AlertTlkLoadingFinished, Properties.Resources.Done,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            };

            loadingWorker.RunWorkerAsync();
        }

        private void StartWritingTlkButton_Click(object sender, RoutedEventArgs e)
        {
            bool debugVersion = false;
            if (DebugCheckBox.IsChecked == true)
                debugVersion = true;
            BusyWriting(true);
            var writingWorker = new BackgroundWorker();

            writingWorker.DoWork += delegate
            {
                try
                {
                    HuffmanCompression hc = new HuffmanCompression();
                    hc.LoadInputData(_inputXmlFilePath, TalkFile.Fileformat.Xml, debugVersion);

                    hc.SaveToTlkFile(_outputTlkFilePath);
                }
                catch (FileNotFoundException)
                {
                    System.Windows.MessageBox.Show(
                        Properties.Resources.AlertExceptionXmlNotFound, Properties.Resources.Error,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    string message = Properties.Resources.AlertExceptionGeneric;
                    message += Properties.Resources.AlertExceptionGenericDescription + ex.Message;
                    System.Windows.MessageBox.Show(message, Properties.Resources.Error,
                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            };
            writingWorker.RunWorkerCompleted += delegate
            {
                BusyWriting(false);
                System.Windows.MessageBox.Show(Properties.Resources.AlertWritingTlkFinished, Properties.Resources.Done,
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
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            config.AppSettings.Settings["InputTlkFilePath"].Value = TextInputTlkFilePath.Text;
            config.AppSettings.Settings["OutputTextFilePath"].Value = TextOutputTextFilePath.Text;
            config.AppSettings.Settings["InputXmlFilePath"].Value = TextInputXmlFilePath.Text;
            config.AppSettings.Settings["OutputTlkFilePath"].Value = TextOutputTlkFilePath.Text;

            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow aboutWindow = new AboutWindow();
            aboutWindow.Owner = this;
            aboutWindow.ShowDialog();
        }

        private void HowTo_Click(object sender, RoutedEventArgs e)
        {
            HowToUseWindow howToWindow = new HowToUseWindow();
            howToWindow.Owner = this;
            howToWindow.Show();
        }
    }
}
