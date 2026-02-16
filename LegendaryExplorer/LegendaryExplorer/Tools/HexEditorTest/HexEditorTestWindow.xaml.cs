using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using LegendaryExplorer.UserControls.SharedToolControls.HexEditor;
using Microsoft.Win32;

namespace LegendaryExplorer.Tools.HexEditorTest
{
    /// <summary>
    /// Test window for the HexEditorControl
    /// </summary>
    public partial class HexEditorTestWindow : Window
    {
        private FileByteProvider _byteProvider;
        private Random _random = new Random();

        public HexEditorTestWindow()
        {
            InitializeComponent();
            
            // Update status bar when selection changes
            Loaded += (s, e) =>
            {
                UpdateStatusBar();
            };
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Open File to View in Hex Editor",
                Filter = "All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    LoadFile(openFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadFile(string filePath)
        {
            // Dispose previous provider if exists
            _byteProvider?.Dispose();

            // Create new provider
            _byteProvider = new FileByteProvider(filePath);
            HexEditor.ByteProvider = _byteProvider;

            // Update UI
            FilePathTextBlock.Text = filePath;
            FileSizeTextBlock.Text = $"Size: {FormatFileSize(_byteProvider.Length)}";
            StatusTextBlock.Text = $"Loaded {Path.GetFileName(filePath)}";

            UpdateStatusBar();
        }

        private void TestHighlight_Click(object sender, RoutedEventArgs e)
        {
            if (_byteProvider == null || _byteProvider.Length == 0)
            {
                MessageBox.Show("Please load a file first.", "No File Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Highlight random region
            long start = _random.Next(0, (int)Math.Min(_byteProvider.Length - 100, int.MaxValue));
            long length = _random.Next(10, 50);

            // Random colors
            Color foreColor = Color.FromRgb((byte)_random.Next(256), (byte)_random.Next(256), (byte)_random.Next(256));
            Color backColor = Color.FromRgb((byte)_random.Next(256), (byte)_random.Next(256), (byte)_random.Next(256));

            HexEditor.Highlight(start, length, foreColor, backColor, $"Test region at 0x{start:X}");
            HexEditor.ScrollToPosition(start);
            StatusTextBlock.Text = $"Highlighted bytes 0x{start:X} - 0x{(start + length):X}";
        }

        private void ClearHighlights_Click(object sender, RoutedEventArgs e)
        {
            HexEditor.ClearHighlights();
            StatusTextBlock.Text = "Highlights cleared";
        }

        private void UpdateStatusBar()
        {
            if (_byteProvider != null)
            {
                SelectionTextBlock.Text = HexEditor.SelectionLength > 0
                    ? $"Selection: 0x{HexEditor.SelectionStart:X} - 0x{(HexEditor.SelectionStart + HexEditor.SelectionLength):X} ({HexEditor.SelectionLength} bytes)"
                    : "Selection: None";

                BytesPerLineTextBlock.Text = $"Bytes/Line: {HexEditor.BytesPerLine}";
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]} ({bytes:N0} bytes)";
        }

        protected override void OnClosed(EventArgs e)
        {
            _byteProvider?.Dispose();
            base.OnClosed(e);
        }
    }

    /// <summary>
    /// IByteProvider implementation that reads from a file
    /// </summary>
    public class FileByteProvider : IByteProvider, IDisposable
    {
        private readonly byte[] _data;
        private bool _disposed;

        public FileByteProvider(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            _data = File.ReadAllBytes(filePath);
        }

        public long Length => _data?.Length ?? 0;

        public event EventHandler LengthChanged;

        public byte ReadByte(long index)
        {
            if (index < 0 || index >= Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _data[index];
        }

        public void WriteByte(long index, byte value)
        {
            if (index < 0 || index >= Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            _data[index] = value;
        }

        public bool SupportsWriteByte() => true;

        public bool SupportsInsertBytes() => false;

        public bool SupportsDeleteBytes() => false;

        public void InsertBytes(long index, byte[] bs)
        {
            throw new NotSupportedException("Insert is not supported by FileByteProvider");
        }

        public void DeleteBytes(long index, long length)
        {
            throw new NotSupportedException("Delete is not supported by FileByteProvider");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                // No unmanaged resources to dispose
            }
        }
    }
}
