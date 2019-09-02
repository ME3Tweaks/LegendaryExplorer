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
        private string _inputXmlFilePath = ConfigurationManager.AppSettings["InputXmlFilePath"];

        public string InputTlkFilePath { get; set; } = ConfigurationManager.AppSettings["InputTlkFilePath"];

        public string OutputTextFilePath { get; set; } = ConfigurationManager.AppSettings["OutputTextFilePath"];

        public string InputXmlFilePath
        {
            get { return _inputXmlFilePath; }
            set { InputTlkFilePath = value; }
        }

        public string OutputTlkFilePath { get; set; } = ConfigurationManager.AppSettings["OutputTlkFilePath"];

        public TLKEditor()
        {
            InitializeComponent();
        }

        private void InputTlkFilePathButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Multiselect = false,
                Filter = "TLK Files (*.tlk)|*.tlk"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                InputTlkFilePath = openFileDialog.FileName;
                TextInputTlkFilePath.Text = InputTlkFilePath;
            }
        }

        private void OutputXmlFilePathButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "XML Files (*.xml)|*.xml"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                OutputTextFilePath = saveFileDialog.FileName;
                TextOutputXmlFilePath.Text = OutputTextFilePath;
            }
        }

        private void InputXmlFilePathButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Multiselect = false,
                Filter = "XML Files (*.xml)|*.xml"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                _inputXmlFilePath = openFileDialog.FileName;
                TextInputXmlFilePath.Text = _inputXmlFilePath;
            }
        }

        private void OutputTlkFilePathButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "TLK Files (*.tlk)|*.tlk"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                OutputTlkFilePath = saveFileDialog.FileName;
                TextOutputTlkFilePath.Text = OutputTlkFilePath;
            }
        }

        private void StartReadingTlkButton_Click(object sender, RoutedEventArgs e)
        {
            BusyReading(true);

            var loadingWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };

            loadingWorker.ProgressChanged += delegate(object sender2, ProgressChangedEventArgs e2)
            {
                readingTlkProgressBar.Value = e2.ProgressPercentage;
            };

            loadingWorker.DoWork += delegate
            {
                try
                {
                    TalkFile tf = new TalkFile();
                    tf.LoadTlkData(InputTlkFilePath);

                    tf.ProgressChanged += loadingWorker.ReportProgress;
                    tf.DumpToFile(OutputTextFilePath);
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

                    hc.SaveToTlkFile(OutputTlkFilePath);
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
            Process.Start(App.BugReportURL);
        }

        private void HowTo_Click(object sender, RoutedEventArgs e)
        {
            TLKEditorHowToUseWindow howToWindow = new TLKEditorHowToUseWindow
            {
                Owner = this
            };
            howToWindow.Show();
        }

        private void Forums_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://me3tweaks.com/forums");
        }
    }
}
