using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class TLKEditor : Window
    {
        private string _inputTlkFilePath = ConfigurationManager.AppSettings["InputTlkFilePath"];
        private string _outputXmlFilePath = ConfigurationManager.AppSettings["OutputTextFilePath"];
        private string _inputXmlFilePath = ConfigurationManager.AppSettings["InputXmlFilePath"];
        private string _outputTlkFilePath = ConfigurationManager.AppSettings["OutputTlkFilePath"];

        public string InputTlkFilePath
        {
            get { return _inputTlkFilePath; }
            set { _inputTlkFilePath = value; }
        }

        public string OutputTextFilePath
        {
            get { return _outputXmlFilePath; }
            set { _outputXmlFilePath = value; }
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

        public TLKEditor()
        {
            InitializeComponent();
        }

        private void InputTlkFilePathButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = false;
            openFileDialog.Filter = "TLK Files (*.tlk)|*.tlk";
            if (openFileDialog.ShowDialog() == true)
            {
                _inputTlkFilePath = openFileDialog.FileName;
                TextInputTlkFilePath.Text = _inputTlkFilePath;
            }
        }

        private void OutputXmlFilePathButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "XML Files (*.xml)|*.xml";
            if (saveFileDialog.ShowDialog() == true)
            {
                _outputXmlFilePath = saveFileDialog.FileName;
                TextOutputXmlFilePath.Text = _outputXmlFilePath;
            }
        }

        private void InputXmlFilePathButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = false;
            openFileDialog.Filter = "XML Files (*.xml)|*.xml";
            if (openFileDialog.ShowDialog() == true)
            {
                _inputXmlFilePath = openFileDialog.FileName;
                TextInputXmlFilePath.Text = _inputXmlFilePath;
            }
        }

        private void OutputTlkFilePathButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "TLK Files (*.tlk)|*.tlk";
            if (saveFileDialog.ShowDialog() == true)
            {
                _outputTlkFilePath = saveFileDialog.FileName;
                TextOutputTlkFilePath.Text = _outputTlkFilePath;
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void StartReadingTlkButton_Click(object sender, RoutedEventArgs e)
        {
            BusyReading(true);

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
                    tf.DumpToFile(_outputXmlFilePath);
                    // debug
                    // tf.PrintHuffmanTree();
                    tf.ProgressChanged -= loadingWorker.ReportProgress;
                    MessageBox.Show("Finished reading TLK file.", "Done!",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (FileNotFoundException)
                {
                    MessageBox.Show("Selected TLK file was not found or is corrupted.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (IOException)
                {
                    MessageBox.Show("Error reading TLK file!\nPlease make sure you have chosen a file in a TLK format for Mass Effect 2 or 3.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error",
                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            };

            loadingWorker.RunWorkerCompleted += delegate
            {
                BusyReading(false);
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
                    hc.LoadInputData(_inputXmlFilePath, debugVersion);

                    hc.SaveToTlkFile(_outputTlkFilePath);
                    MessageBox.Show("Finished creating TLK file.", "Done!",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (FileNotFoundException)
                {
                    MessageBox.Show("Selected XML file was not found or is corrupted.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (System.Xml.XmlException ex)
                {
                    MessageBox.Show($"Parse Error: {ex.Message}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error",
                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            };
            writingWorker.RunWorkerCompleted += delegate
            {
                BusyWriting(false);
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
            config.AppSettings.Settings["OutputTextFilePath"].Value = TextOutputXmlFilePath.Text;
            config.AppSettings.Settings["InputXmlFilePath"].Value = TextInputXmlFilePath.Text;
            config.AppSettings.Settings["OutputTlkFilePath"].Value = TextOutputTlkFilePath.Text;

            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        private void ReportBugs_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/Mgamerz/ME3Explorer/issues");
        }

        private void HowTo_Click(object sender, RoutedEventArgs e)
        {
            TLKEditorHowToUseWindow howToWindow = new TLKEditorHowToUseWindow();
            howToWindow.Owner = this;
            howToWindow.Show();
        }

        private void Forums_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://me3tweaks.com/forums");
        }
    }
}
