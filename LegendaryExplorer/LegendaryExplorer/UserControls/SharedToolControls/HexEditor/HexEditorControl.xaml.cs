using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace LegendaryExplorer.UserControls.SharedToolControls.HexEditor
{
    /// <summary>
    /// WPF Hex Editor Control with dynamic resizing support (4-16 bytes per line)
    /// </summary>
    public partial class HexEditorControl : UserControl
    {
        #region Constants
        private const int MIN_BYTES_PER_LINE = 4;
        private const int MAX_BYTES_PER_LINE = 16;
        private const int DEFAULT_BYTES_PER_LINE = 16;
        private const int GROUP_SIZE = 4;
        private const double CHAR_SPACING = 3.0; // Space between hex pairs
        #endregion

        #region Fields
        private IByteProvider _byteProvider;
        private Size _charSize;
        private int _bytesPerLine = DEFAULT_BYTES_PER_LINE;
        private int _verticalByteCount;
        private long _startByte;
        private long _endByte;
        private long _bytePos = -1;
        private int _byteCharacterPos; // 0 or 1 for hex nibble position
        private long _selectionStart = -1;
        private long _selectionLength;
        private bool _isMouseDown;
        private long _mouseDownBytePos = -1;
        private bool _isHexView = true; // true = hex view, false = string view
        private DispatcherTimer _caretTimer;
        private bool _caretVisible;
        private List<HighlightRegion> _highlightRegions = new();
        #endregion

        #region Dependency Properties

        public static readonly DependencyProperty LineInfoVisibleProperty =
            DependencyProperty.Register(nameof(LineInfoVisible), typeof(bool), typeof(HexEditorControl),
                new PropertyMetadata(true, OnLayoutPropertyChanged));

        public static readonly DependencyProperty ColumnInfoVisibleProperty =
            DependencyProperty.Register(nameof(ColumnInfoVisible), typeof(bool), typeof(HexEditorControl),
                new PropertyMetadata(true, OnLayoutPropertyChanged));

        public static readonly DependencyProperty StringViewVisibleProperty =
            DependencyProperty.Register(nameof(StringViewVisible), typeof(bool), typeof(HexEditorControl),
                new PropertyMetadata(true, OnLayoutPropertyChanged));

        public static readonly DependencyProperty VScrollBarVisibleProperty =
            DependencyProperty.Register(nameof(VScrollBarVisible), typeof(bool), typeof(HexEditorControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(HexEditorControl),
                new PropertyMetadata(false));

        public static readonly DependencyProperty GroupSeparatorVisibleProperty =
            DependencyProperty.Register(nameof(GroupSeparatorVisible), typeof(bool), typeof(HexEditorControl),
                new PropertyMetadata(false, OnLayoutPropertyChanged));

        public bool LineInfoVisible
        {
            get => (bool)GetValue(LineInfoVisibleProperty);
            set => SetValue(LineInfoVisibleProperty, value);
        }

        public bool ColumnInfoVisible
        {
            get => (bool)GetValue(ColumnInfoVisibleProperty);
            set => SetValue(ColumnInfoVisibleProperty, value);
        }

        public bool StringViewVisible
        {
            get => (bool)GetValue(StringViewVisibleProperty);
            set => SetValue(StringViewVisibleProperty, value);
        }

        public bool VScrollBarVisible
        {
            get => (bool)GetValue(VScrollBarVisibleProperty);
            set => SetValue(VScrollBarVisibleProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public bool GroupSeparatorVisible
        {
            get => (bool)GetValue(GroupSeparatorVisibleProperty);
            set => SetValue(GroupSeparatorVisibleProperty, value);
        }

        #endregion

        #region Properties

        public IByteProvider ByteProvider
        {
            get => _byteProvider;
            set
            {
                if (_byteProvider != null)
                {
                    _byteProvider.LengthChanged -= OnByteProviderLengthChanged;
                }

                _byteProvider = value;

                if (_byteProvider != null)
                {
                    _byteProvider.LengthChanged += OnByteProviderLengthChanged;
                }

                _bytePos = -1;
                _selectionStart = -1;
                _selectionLength = 0;
                _startByte = 0;

                UpdateScrollBar();
                InvalidateVisual();
            }
        }

        public long SelectionStart
        {
            get => _selectionStart;
            set
            {
                if (_selectionStart != value)
                {
                    _selectionStart = value;
                    _bytePos = value;
                    InvalidateVisual();
                }
            }
        }

        public long SelectionLength
        {
            get => _selectionLength;
            set
            {
                if (_selectionLength != value)
                {
                    _selectionLength = value;
                    InvalidateVisual();
                }
            }
        }

        public int BytesPerLine => _bytesPerLine;

        #endregion

        #region Constructor

        public HexEditorControl()
        {
            InitializeComponent();

            SizeChanged += OnSizeChanged;
            Loaded += OnLoaded;
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            MouseWheel += OnMouseWheel;

            // Setup caret timer
            _caretTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _caretTimer.Tick += CaretTimer_Tick;

            GotFocus += (s, e) => _caretTimer.Start();
            LostFocus += (s, e) =>
            {
                _caretTimer.Stop();
                Caret.Visibility = Visibility.Collapsed;
            };
        }

        #endregion

        #region Layout and Rendering

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            MeasureCharSize();
            CalculateLayout();
            InvalidateVisual();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
            {
                CalculateBytesPerLine();
            }
            CalculateLayout();
            InvalidateVisual();
        }

        private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorControl control)
            {
                control.CalculateLayout();
                control.InvalidateVisual();
            }
        }

        private void MeasureCharSize()
        {
            var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            var formattedText = new FormattedText("0", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, FontSize, Brushes.Black,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            _charSize = new Size(formattedText.Width, formattedText.Height);
        }

        private void CalculateBytesPerLine()
        {
            if (_charSize.Width == 0) return;

            double availableWidth = HexCanvas.ActualWidth;
            if (availableWidth <= 0) return;

            // Calculate space needed per byte: "XX " = 3 chars
            double byteCost = _charSize.Width * CHAR_SPACING;

            // Add space for string view if visible
            if (StringViewVisible)
            {
                byteCost += _charSize.Width; // 1 char in string view
            }

            // Calculate how many bytes fit
            int newBytesPerLine = (int)(availableWidth / byteCost);
            newBytesPerLine = Math.Max(MIN_BYTES_PER_LINE, Math.Min(MAX_BYTES_PER_LINE, newBytesPerLine));

            if (newBytesPerLine != _bytesPerLine)
            {
                _bytesPerLine = newBytesPerLine;
                UpdateScrollBar();
            }
        }

        private void CalculateLayout()
        {
            if (_charSize.Width == 0)
            {
                MeasureCharSize();
            }

            if (_byteProvider == null || _charSize.Height == 0) return;

            // Calculate vertical byte count
            double availableHeight = HexCanvas.ActualHeight;
            _verticalByteCount = Math.Max(1, (int)(availableHeight / _charSize.Height));

            _endByte = Math.Min(_byteProvider.Length - 1, _startByte + (_bytesPerLine * _verticalByteCount) - 1);

            UpdateScrollBar();
        }

        private void InvalidateVisual()
        {
            Dispatcher.InvokeAsync(() =>
            {
                RenderLineInfo();
                RenderColumnInfo();
                RenderHexView();
                RenderStringView();
                UpdateCaret();
            }, DispatcherPriority.Render);
        }

        private void RenderLineInfo()
        {
            LineInfoCanvas.Children.Clear();

            if (!LineInfoVisible || _byteProvider == null) return;

            var foreground = TryFindResource("InfoForegroundBrush") as Brush ?? Brushes.Gray;
            int lineCount = (int)Math.Ceiling((double)(_endByte - _startByte + 1) / _bytesPerLine);

            for (int i = 0; i < lineCount; i++)
            {
                long address = _startByte + (i * _bytesPerLine);
                var text = CreateFormattedText($"{address:X8}", foreground);

                var textBlock = new TextBlock
                {
                    Text = $"{address:X8}",
                    Foreground = foreground,
                    FontFamily = FontFamily,
                    FontSize = FontSize
                };

                Canvas.SetLeft(textBlock, 4);
                Canvas.SetTop(textBlock, i * _charSize.Height);
                LineInfoCanvas.Children.Add(textBlock);
            }
        }

        private void RenderColumnInfo()
        {
            ColumnInfoCanvas.Children.Clear();

            if (!ColumnInfoVisible) return;

            var foreground = TryFindResource("InfoForegroundBrush") as Brush ?? Brushes.Gray;

            for (int i = 0; i < _bytesPerLine; i++)
            {
                var textBlock = new TextBlock
                {
                    Text = $"{i:X2}",
                    Foreground = foreground,
                    FontFamily = FontFamily,
                    FontSize = FontSize
                };

                double x = i * _charSize.Width * CHAR_SPACING;
                Canvas.SetLeft(textBlock, x);
                Canvas.SetTop(textBlock, 2);
                ColumnInfoCanvas.Children.Add(textBlock);

                // Draw group separators
                if (GroupSeparatorVisible && i > 0 && i % GROUP_SIZE == 0)
                {
                    var line = new Line
                    {
                        X1 = x - _charSize.Width / 2,
                        Y1 = 0,
                        X2 = x - _charSize.Width / 2,
                        Y2 = ColumnInfoCanvas.Height,
                        Stroke = foreground,
                        StrokeThickness = 1
                    };
                    ColumnInfoCanvas.Children.Add(line);
                }
            }
        }

        private void RenderHexView()
        {
            HexCanvas.Children.Clear();

            if (_byteProvider == null) return;

            var defaultBrush = TryFindResource("TextBrush") as Brush ?? Brushes.Black;
            var selectionBrush = TryFindResource("SelectionBrush") as Brush ?? Brushes.Blue;
            var selectionTextBrush = TryFindResource("SelectionTextBrush") as Brush ?? Brushes.White;

            long byteIndex = _startByte;
            int row = 0;

            while (byteIndex <= _endByte && byteIndex < _byteProvider.Length)
            {
                int col = (int)((byteIndex - _startByte) % _bytesPerLine);
                if (col == 0 && byteIndex != _startByte) row++;

                byte b = _byteProvider.ReadByte(byteIndex);
                bool isSelected = IsByteSelected(byteIndex);
                bool isHighlighted = IsByteHighlighted(byteIndex, out var highlightRegion);

                Brush background = null;
                Brush foreground = defaultBrush;

                if (isSelected)
                {
                    background = selectionBrush;
                    foreground = selectionTextBrush;
                }
                else if (isHighlighted)
                {
                    background = new SolidColorBrush(highlightRegion.BackColor);
                    foreground = new SolidColorBrush(highlightRegion.ForeColor);
                }

                double x = col * _charSize.Width * CHAR_SPACING;
                double y = row * _charSize.Height;

                // Draw background if needed
                if (background != null)
                {
                    var rect = new System.Windows.Shapes.Rectangle
                    {
                        Width = _charSize.Width * 2.5,
                        Height = _charSize.Height,
                        Fill = background
                    };
                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, y);
                    HexCanvas.Children.Add(rect);
                }

                // Draw hex text
                var textBlock = new TextBlock
                {
                    Text = $"{b:X2}",
                    Foreground = foreground,
                    FontFamily = FontFamily,
                    FontSize = FontSize
                };

                Canvas.SetLeft(textBlock, x);
                Canvas.SetTop(textBlock, y);
                HexCanvas.Children.Add(textBlock);

                // Draw group separators
                if (GroupSeparatorVisible && col > 0 && col % GROUP_SIZE == 0)
                {
                    var line = new Line
                    {
                        X1 = x - _charSize.Width / 2,
                        Y1 = y,
                        X2 = x - _charSize.Width / 2,
                        Y2 = y + _charSize.Height,
                        Stroke = TryFindResource("BorderBrush") as Brush ?? Brushes.Gray,
                        StrokeThickness = 1
                    };
                    HexCanvas.Children.Add(line);
                }

                byteIndex++;
            }
        }

        private void RenderStringView()
        {
            StringCanvas.Children.Clear();

            if (_byteProvider == null || !StringViewVisible) return;

            var defaultBrush = TryFindResource("TextBrush") as Brush ?? Brushes.Black;
            var selectionBrush = TryFindResource("SelectionBrush") as Brush ?? Brushes.Blue;
            var selectionTextBrush = TryFindResource("SelectionTextBrush") as Brush ?? Brushes.White;

            StringCanvas.Width = _bytesPerLine * _charSize.Width;

            long byteIndex = _startByte;
            int row = 0;

            while (byteIndex <= _endByte && byteIndex < _byteProvider.Length)
            {
                int col = (int)((byteIndex - _startByte) % _bytesPerLine);
                if (col == 0 && byteIndex != _startByte) row++;

                byte b = _byteProvider.ReadByte(byteIndex);
                bool isSelected = IsByteSelected(byteIndex);
                bool isHighlighted = IsByteHighlighted(byteIndex, out var highlightRegion);

                char c = b >= 32 && b < 127 ? (char)b : '.';

                Brush background = null;
                Brush foreground = defaultBrush;

                if (isSelected)
                {
                    background = selectionBrush;
                    foreground = selectionTextBrush;
                }
                else if (isHighlighted)
                {
                    background = new SolidColorBrush(highlightRegion.BackColor);
                    foreground = new SolidColorBrush(highlightRegion.ForeColor);
                }

                double x = col * _charSize.Width;
                double y = row * _charSize.Height;

                // Draw background if needed
                if (background != null)
                {
                    var rect = new System.Windows.Shapes.Rectangle
                    {
                        Width = _charSize.Width,
                        Height = _charSize.Height,
                        Fill = background
                    };
                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, y);
                    StringCanvas.Children.Add(rect);
                }

                // Draw character
                var textBlock = new TextBlock
                {
                    Text = c.ToString(),
                    Foreground = foreground,
                    FontFamily = FontFamily,
                    FontSize = FontSize
                };

                Canvas.SetLeft(textBlock, x);
                Canvas.SetTop(textBlock, y);
                StringCanvas.Children.Add(textBlock);

                byteIndex++;
            }
        }

        private FormattedText CreateFormattedText(string text, Brush foreground)
        {
            return new FormattedText(text, CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
                FontSize, foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        #endregion

        #region Scrolling

        private void UpdateScrollBar()
        {
            if (_byteProvider == null || _bytesPerLine == 0 || _verticalByteCount == 0)
            {
                VerticalScrollBar.Visibility = Visibility.Collapsed;
                return;
            }

            long totalLines = (long)Math.Ceiling((double)_byteProvider.Length / _bytesPerLine);
            long visibleLines = _verticalByteCount;

            if (totalLines <= visibleLines)
            {
                VerticalScrollBar.Visibility = Visibility.Collapsed;
                return;
            }

            VerticalScrollBar.Visibility = VScrollBarVisible ? Visibility.Visible : Visibility.Collapsed;
            VerticalScrollBar.Maximum = Math.Max(0, totalLines - visibleLines);
            VerticalScrollBar.ViewportSize = visibleLines;
            VerticalScrollBar.Value = Math.Min(VerticalScrollBar.Value, VerticalScrollBar.Maximum);
        }

        private void VerticalScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            _startByte = (long)e.NewValue * _bytesPerLine;
            CalculateLayout();
            InvalidateVisual();
        }

        private void OnByteProviderLengthChanged(object sender, EventArgs e)
        {
            UpdateScrollBar();
            InvalidateVisual();
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_byteProvider == null || VerticalScrollBar.Visibility != Visibility.Visible)
                return;

            // Scroll by 3 lines per wheel click
            int linesToScroll = -e.Delta / 120 * 3;
            double newValue = Math.Max(0, Math.Min(VerticalScrollBar.Maximum,
                VerticalScrollBar.Value + linesToScroll));

            if (newValue != VerticalScrollBar.Value)
            {
                VerticalScrollBar.Value = newValue;
                _startByte = (long)newValue * _bytesPerLine;
                CalculateLayout();
                InvalidateVisual();
            }

            e.Handled = true;
        }

        public void ScrollToPosition(long position)
        {
            if (_byteProvider == null) return;

            long line = position / _bytesPerLine;
            VerticalScrollBar.Value = Math.Min(line, VerticalScrollBar.Maximum);
            _startByte = line * _bytesPerLine;
            CalculateLayout();
            InvalidateVisual();
        }

        #endregion

        #region Mouse Handling

        private void HexCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            _isHexView = true;
            _isMouseDown = true;

            var position = e.GetPosition(HexCanvas);
            long bytePos = GetBytePositionFromPoint(position, true);

            if (bytePos >= 0)
            {
                _mouseDownBytePos = bytePos;
                _bytePos = bytePos;
                _byteCharacterPos = GetNibblePosition(position);

                if (!Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
                {
                    _selectionStart = bytePos;
                    _selectionLength = 0;
                }

                InvalidateVisual();
            }

            HexCanvas.CaptureMouse();
        }

        private void HexCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMouseDown || _mouseDownBytePos < 0) return;

            var position = e.GetPosition(HexCanvas);
            long bytePos = GetBytePositionFromPoint(position, true);

            if (bytePos >= 0)
            {
                if (bytePos < _mouseDownBytePos)
                {
                    _selectionStart = bytePos;
                    _selectionLength = _mouseDownBytePos - bytePos + 1;
                }
                else
                {
                    _selectionStart = _mouseDownBytePos;
                    _selectionLength = bytePos - _mouseDownBytePos + 1;
                }

                _bytePos = bytePos;
                InvalidateVisual();
            }
        }

        private void HexCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = false;
            HexCanvas.ReleaseMouseCapture();
        }

        private void StringCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            _isHexView = false;
            _isMouseDown = true;

            var position = e.GetPosition(StringCanvas);
            long bytePos = GetBytePositionFromPoint(position, false);

            if (bytePos >= 0)
            {
                _mouseDownBytePos = bytePos;
                _bytePos = bytePos;
                _byteCharacterPos = 0;

                if (!Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
                {
                    _selectionStart = bytePos;
                    _selectionLength = 0;
                }

                InvalidateVisual();
            }

            StringCanvas.CaptureMouse();
        }

        private void StringCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMouseDown || _mouseDownBytePos < 0) return;

            var position = e.GetPosition(StringCanvas);
            long bytePos = GetBytePositionFromPoint(position, false);

            if (bytePos >= 0)
            {
                if (bytePos < _mouseDownBytePos)
                {
                    _selectionStart = bytePos;
                    _selectionLength = _mouseDownBytePos - bytePos + 1;
                }
                else
                {
                    _selectionStart = _mouseDownBytePos;
                    _selectionLength = bytePos - _mouseDownBytePos + 1;
                }

                _bytePos = bytePos;
                InvalidateVisual();
            }
        }

        private void StringCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = false;
            StringCanvas.ReleaseMouseCapture();
        }

        private long GetBytePositionFromPoint(Point point, bool isHexView)
        {
            if (_charSize.Height == 0) return -1;

            int row = (int)(point.Y / _charSize.Height);
            int col;

            if (isHexView)
            {
                col = (int)(point.X / (_charSize.Width * CHAR_SPACING));
            }
            else
            {
                col = (int)(point.X / _charSize.Width);
            }

            col = Math.Max(0, Math.Min(col, _bytesPerLine - 1));
            long bytePos = _startByte + (row * _bytesPerLine) + col;

            return bytePos < _byteProvider?.Length ? bytePos : -1;
        }

        private int GetNibblePosition(Point point)
        {
            double charOffset = point.X % (_charSize.Width * CHAR_SPACING);
            return charOffset < _charSize.Width ? 0 : 1;
        }

        #endregion

        #region Keyboard Handling

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (_byteProvider == null || _bytePos < 0) return;

            bool handled = true;
            bool shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            bool ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            switch (e.Key)
            {
                case Key.Left:
                    MoveLeft(shift);
                    break;
                case Key.Right:
                    MoveRight(shift);
                    break;
                case Key.Up:
                    MoveUp(shift);
                    break;
                case Key.Down:
                    MoveDown(shift);
                    break;
                case Key.PageUp:
                    MovePageUp(shift);
                    break;
                case Key.PageDown:
                    MovePageDown(shift);
                    break;
                case Key.Home:
                    MoveHome(shift, ctrl);
                    break;
                case Key.End:
                    MoveEnd(shift, ctrl);
                    break;
                case Key.Tab:
                    _isHexView = !_isHexView;
                    _byteCharacterPos = 0;
                    break;
                case Key.C when ctrl:
                    Copy();
                    break;
                case Key.V when ctrl:
                    Paste();
                    break;
                case Key.A when ctrl:
                    SelectAll();
                    break;
                default:
                    if (!ReadOnly && _isHexView)
                    {
                        handled = ProcessHexInput(e.Key);
                    }
                    else if (!ReadOnly && !_isHexView)
                    {
                        handled = ProcessStringInput(e.Key);
                    }
                    else
                    {
                        handled = false;
                    }
                    break;
            }

            if (handled)
            {
                e.Handled = true;
                InvalidateVisual();
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            // Handle key up events if needed
        }

        private bool ProcessHexInput(Key key)
        {
            if (_byteProvider == null || _bytePos >= _byteProvider.Length) return false;

            char c = KeyToChar(key);
            if (!Uri.IsHexDigit(c)) return false;

            byte currentByte = _byteProvider.ReadByte(_bytePos);
            string hex = currentByte.ToString("X2");

            if (_byteCharacterPos == 0)
            {
                hex = c + hex[1].ToString();
            }
            else
            {
                hex = hex[0].ToString() + c;
            }

            if (byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte newByte))
            {
                _byteProvider.WriteByte(_bytePos, newByte);
                MoveRight(false);
                return true;
            }

            return false;
        }

        private bool ProcessStringInput(Key key)
        {
            if (_byteProvider == null || _bytePos >= _byteProvider.Length) return false;

            char c = KeyToChar(key);
            if (c < 32 || c > 126) return false;

            _byteProvider.WriteByte(_bytePos, (byte)c);
            MoveRight(false);
            return true;
        }

        private char KeyToChar(Key key)
        {
            if (key >= Key.D0 && key <= Key.D9)
                return (char)('0' + (key - Key.D0));
            if (key >= Key.A && key <= Key.Z)
                return (char)('A' + (key - Key.A));
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return (char)('0' + (key - Key.NumPad0));

            return '\0';
        }

        #endregion

        #region Navigation

        private void MoveLeft(bool select)
        {
            if (_bytePos <= 0) return;

            if (_isHexView && _byteCharacterPos == 1)
            {
                _byteCharacterPos = 0;
            }
            else
            {
                _bytePos--;
                _byteCharacterPos = _isHexView ? 1 : 0;
                UpdateSelection(select);
            }

            EnsureVisible(_bytePos);
        }

        private void MoveRight(bool select)
        {
            if (_byteProvider == null || _bytePos >= _byteProvider.Length - 1) return;

            if (_isHexView && _byteCharacterPos == 0)
            {
                _byteCharacterPos = 1;
            }
            else
            {
                _bytePos++;
                _byteCharacterPos = 0;
                UpdateSelection(select);
            }

            EnsureVisible(_bytePos);
        }

        private void MoveUp(bool select)
        {
            if (_bytePos < _bytesPerLine) return;

            _bytePos -= _bytesPerLine;
            UpdateSelection(select);
            EnsureVisible(_bytePos);
        }

        private void MoveDown(bool select)
        {
            if (_byteProvider == null || _bytePos + _bytesPerLine >= _byteProvider.Length) return;

            _bytePos += _bytesPerLine;
            UpdateSelection(select);
            EnsureVisible(_bytePos);
        }

        private void MovePageUp(bool select)
        {
            _bytePos = Math.Max(0, _bytePos - (_bytesPerLine * _verticalByteCount));
            UpdateSelection(select);
            EnsureVisible(_bytePos);
        }

        private void MovePageDown(bool select)
        {
            if (_byteProvider == null) return;

            _bytePos = Math.Min(_byteProvider.Length - 1, _bytePos + (_bytesPerLine * _verticalByteCount));
            UpdateSelection(select);
            EnsureVisible(_bytePos);
        }

        private void MoveHome(bool select, bool ctrl)
        {
            if (ctrl)
            {
                _bytePos = 0;
            }
            else
            {
                long lineStart = (_bytePos / _bytesPerLine) * _bytesPerLine;
                _bytePos = lineStart;
            }

            _byteCharacterPos = 0;
            UpdateSelection(select);
            EnsureVisible(_bytePos);
        }

        private void MoveEnd(bool select, bool ctrl)
        {
            if (_byteProvider == null) return;

            if (ctrl)
            {
                _bytePos = _byteProvider.Length - 1;
            }
            else
            {
                long lineStart = (_bytePos / _bytesPerLine) * _bytesPerLine;
                _bytePos = Math.Min(_byteProvider.Length - 1, lineStart + _bytesPerLine - 1);
            }

            _byteCharacterPos = 0;
            UpdateSelection(select);
            EnsureVisible(_bytePos);
        }

        private void UpdateSelection(bool select)
        {
            if (select)
            {
                if (_selectionStart < 0)
                {
                    _selectionStart = _bytePos;
                    _selectionLength = 1;
                }
                else
                {
                    long start = Math.Min(_selectionStart, _bytePos);
                    long end = Math.Max(_selectionStart, _bytePos);
                    _selectionStart = start;
                    _selectionLength = end - start + 1;
                }
            }
            else
            {
                _selectionStart = _bytePos;
                _selectionLength = 0;
            }
        }

        private void EnsureVisible(long bytePos)
        {
            if (_byteProvider == null) return;

            long line = bytePos / _bytesPerLine;
            long currentLine = _startByte / _bytesPerLine;
            long lastVisibleLine = currentLine + _verticalByteCount - 1;

            if (line < currentLine)
            {
                VerticalScrollBar.Value = line;
                _startByte = line * _bytesPerLine;
            }
            else if (line > lastVisibleLine)
            {
                long newLine = line - _verticalByteCount + 1;
                VerticalScrollBar.Value = Math.Max(0, newLine);
                _startByte = newLine * _bytesPerLine;
            }
        }

        #endregion

        #region Caret

        private void CaretTimer_Tick(object sender, EventArgs e)
        {
            _caretVisible = !_caretVisible;
            Caret.Visibility = _caretVisible && IsFocused ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateCaret()
        {
            if (_bytePos < 0 || _bytePos < _startByte || _bytePos > _endByte)
            {
                Caret.Visibility = Visibility.Collapsed;
                return;
            }

            long relativePos = _bytePos - _startByte;
            int row = (int)(relativePos / _bytesPerLine);
            int col = (int)(relativePos % _bytesPerLine);

            double x, y;

            if (_isHexView)
            {
                x = col * _charSize.Width * CHAR_SPACING + (_byteCharacterPos * _charSize.Width);
                y = row * _charSize.Height;
                Canvas.SetLeft(Caret, HexCanvas.Margin.Left + x);
                Canvas.SetTop(Caret, HexCanvas.Margin.Top + y);
            }
            else
            {
                x = col * _charSize.Width;
                y = row * _charSize.Height;
                Canvas.SetLeft(Caret, HexCanvas.ActualWidth + StringCanvas.Margin.Left + x);
                Canvas.SetTop(Caret, StringCanvas.Margin.Top + y);
            }

            Caret.Height = _charSize.Height;
            Caret.Visibility = IsFocused && _caretVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Selection and Highlighting

        private bool IsByteSelected(long byteIndex)
        {
            if (_selectionLength == 0) return false;
            return byteIndex >= _selectionStart && byteIndex < _selectionStart + _selectionLength;
        }

        private bool IsByteHighlighted(long byteIndex, out HighlightRegion region)
        {
            region = _highlightRegions.FirstOrDefault(r => r.IsWithin(byteIndex));
            return region != null;
        }

        public void Highlight(long start, long length, Color foreColor, Color backColor, string label = null)
        {
            var region = new HighlightRegion
            {
                Start = start,
                End = start + length - 1,
                ForeColor = foreColor,
                BackColor = backColor,
                Label = label
            };

            _highlightRegions.Add(region);
            InvalidateVisual();
        }

        public void ClearHighlights()
        {
            _highlightRegions.Clear();
            InvalidateVisual();
        }

        public void SelectAll()
        {
            if (_byteProvider == null) return;

            _selectionStart = 0;
            _selectionLength = _byteProvider.Length;
            _bytePos = 0;
            InvalidateVisual();
        }

        #endregion

        #region Copy/Paste

        private void Copy()
        {
            if (_selectionLength == 0 || _byteProvider == null) return;

            var sb = new StringBuilder();
            for (long i = 0; i < _selectionLength; i++)
            {
                byte b = _byteProvider.ReadByte(_selectionStart + i);
                sb.Append($"{b:X2} ");
            }

            Clipboard.SetText(sb.ToString().TrimEnd());
        }

        private void Paste()
        {
            if (ReadOnly || _byteProvider == null || _bytePos < 0) return;

            string text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text)) return;

            var bytes = new List<byte>();
            var parts = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                if (byte.TryParse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                {
                    bytes.Add(b);
                }
            }

            for (int i = 0; i < bytes.Count && _bytePos + i < _byteProvider.Length; i++)
            {
                _byteProvider.WriteByte(_bytePos + i, bytes[i]);
            }

            InvalidateVisual();
        }

        #endregion

        #region Supporting Classes

        public class HighlightRegion
        {
            public long Start { get; set; }
            public long End { get; set; }
            public Color ForeColor { get; set; }
            public Color BackColor { get; set; }
            public string Label { get; set; }

            public bool IsWithin(long position)
            {
                return position >= Start && position <= End;
            }
        }

        #endregion
    }

    #region IByteProvider Interface

    public interface IByteProvider
    {
        long Length { get; }
        event EventHandler LengthChanged;
        byte ReadByte(long index);
        void WriteByte(long index, byte value);
        bool SupportsWriteByte();
        bool SupportsInsertBytes();
        bool SupportsDeleteBytes();
        void InsertBytes(long index, byte[] bs);
        void DeleteBytes(long index, long length);
    }

    #endregion
}
