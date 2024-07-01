using System.Windows.Controls;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media;

namespace LegendaryExplorer.SharedUI.Controls
{
    public class EditableTextBlock : TextBlock
    {
        public bool IsInEditMode
        {
            get => (bool)GetValue(IsInEditModeProperty);
            set => SetValue(IsInEditModeProperty, value);
        }
        public bool IsEditable
        {
            get => (bool)GetValue(IsEditableProperty);
            set => SetValue(IsEditableProperty, value);
        }

        public static readonly DependencyProperty IsEditableProperty =
                            DependencyProperty.Register(
                            nameof(IsEditable), typeof(bool),
                            typeof(EditableTextBlock)
                            );

        private EditableTextBlockAdorner _adorner;

        // Using a DependencyProperty as the backing store for IsInEditMode.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsInEditModeProperty =
            DependencyProperty.Register("IsInEditMode", typeof(bool), typeof(EditableTextBlock), new UIPropertyMetadata(false, IsInEditModeUpdate));

        /// <summary>
        /// Determines whether [is in edit mode update] [the specified obj].
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <param name="e">The <see cref="System.Windows.DependencyPropertyChangedEventArgs"/> instance containing the event data.</param>
        private static void IsInEditModeUpdate(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            if (obj is EditableTextBlock textBlock)
            {
                //Get the adorner layer of the uielement (here TextBlock)
                AdornerLayer layer = AdornerLayer.GetAdornerLayer(textBlock);

                //If the IsInEditMode set to true means the user has enabled the edit mode then
                //add the adorner to the adorner layer of the TextBlock.
                if (textBlock.IsInEditMode)
                {
                    if (null == textBlock._adorner)
                    {
                        textBlock._adorner = new EditableTextBlockAdorner(textBlock);

                        //Events wired to exit edit mode when the user presses Enter key or leaves the control.
                        textBlock._adorner.TextBoxKeyUp += textBlock.TextBoxKeyUp;
                        textBlock._adorner.TextBoxLostFocus += textBlock.TextBoxLostFocus;
                    }
                    layer.Add(textBlock._adorner);
                }
                else
                {
                    //Remove the adorner from the adorner layer.
                    Adorner[] adorners = layer.GetAdorners(textBlock);
                    if (adorners != null)
                    {
                        foreach (Adorner adorner in adorners)
                        {
                            if (adorner is EditableTextBlockAdorner)
                            {
                                layer.Remove(adorner);
                            }
                        }
                    }

                    //Update the textblock's text binding.
                    BindingExpression expression = textBlock.GetBindingExpression(TextProperty);
                    expression?.UpdateTarget();
                }
            }
        }

        /// <summary>
        /// Gets or sets the length of the max.
        /// </summary>
        /// <value>The length of the max.</value>
        public int MaxLength
        {
            get => (int)GetValue(MaxLengthProperty);
            set => SetValue(MaxLengthProperty, value);
        }

        // Using a DependencyProperty as the backing store for MaxLength.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MaxLengthProperty =
            DependencyProperty.Register(nameof(MaxLength), typeof(int), typeof(EditableTextBlock), new UIPropertyMetadata(0));

        private void TextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            IsInEditMode = false;
        }

        /// <summary>
        /// release the edit mode when user presses enter.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.Windows.Input.KeyEventArgs"/> instance containing the event data.</param>
        private void TextBoxKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                IsInEditMode = false;
            }
        }

        /// <summary>
        /// Invoked when an unhandled <see cref="E:System.Windows.Input.Mouse.MouseDown"/> attached event reaches an element in its route that is derived from this class. Implement this method to add class handling for this event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.Windows.Input.MouseButtonEventArgs"/> that contains the event data. This event data reports details about the mouse button that was pressed and the handled state.</param>
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (!IsEditable)
            {
                return;
            }
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                IsInEditMode = true;
            }
            else if (e.ClickCount == 2)
            {
                IsInEditMode = true;
            }
        }
    }

    /// <summary>
    /// Adorner class which shows textbox over the text block when the Edit mode is on.
    /// </summary>
    public class EditableTextBlockAdorner : Adorner
    {
        private readonly VisualCollection _collection;

        private readonly TextBox _textBox;

        private readonly TextBlock _textBlock;

        public EditableTextBlockAdorner(EditableTextBlock adornedElement)
            : base(adornedElement)
        {
            _collection = new VisualCollection(this);
            _textBox = new TextBox();
            _textBlock = adornedElement;
            var binding = new Binding("Text") { Source = adornedElement };
            _textBox.SetBinding(TextBox.TextProperty, binding);
            _textBox.AcceptsReturn = true;
            _textBox.MaxLength = adornedElement.MaxLength;
            _textBox.KeyUp += _textBox_KeyUp;
            _collection.Add(_textBox);
        }

        void _textBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _textBox.Text = _textBox.Text.Replace("\r\n", string.Empty);
                BindingExpression expression = _textBox.GetBindingExpression(TextBox.TextProperty);
                expression?.UpdateSource();
            }
        }

        protected override Visual GetVisualChild(int index) => _collection[index];

        protected override int VisualChildrenCount => _collection.Count;

        protected override Size ArrangeOverride(Size finalSize)
        {
            _textBox.Arrange(new Rect(0, 0, _textBlock.DesiredSize.Width + 50, _textBlock.DesiredSize.Height * 1.5));
            _textBox.Focus();
            return finalSize;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            drawingContext.DrawRectangle(null, new Pen
            {
                Brush = Brushes.Gold,
                Thickness = 2
            }, new Rect(0, 0, _textBlock.DesiredSize.Width + 50, _textBlock.DesiredSize.Height * 1.5));
        }

        public event RoutedEventHandler TextBoxLostFocus
        {
            add => _textBox.LostFocus += value;
            remove => _textBox.LostFocus -= value;
        }

        public event KeyEventHandler TextBoxKeyUp
        {
            add => _textBox.KeyUp += value;
            remove => _textBox.KeyUp -= value;
        }
    }
}