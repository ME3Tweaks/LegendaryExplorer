using System.Windows.Documents;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Adorner class which shows textbox over the text block when the Edit mode is on.
    /// </summary>
    public class EditableComboBoxAdorner : Adorner
    {
        private readonly VisualCollection _collection;

        private readonly ComboBox _comboBox;

        private readonly TextBlock _textBlock;

        public EditableComboBoxAdorner(EditableComboBox adornedElement)
            : base(adornedElement)
        {
            _collection = new VisualCollection(this);
            _comboBox = new ComboBox();
            _textBlock = adornedElement;
            Binding binding = new Binding("Text") {Source = adornedElement};
            _comboBox.SetBinding(TextBox.TextProperty, binding);
            _comboBox.KeyUp += _textBox_KeyUp;
            _collection.Add(_comboBox);
        }

        void _textBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _comboBox.Text = _comboBox.Text.Replace("\r\n", string.Empty);
                BindingExpression expression = _comboBox.GetBindingExpression(TextBox.TextProperty);
                if (null != expression)
                {
                    expression.UpdateSource();
                }
            }
        }

        protected override Visual GetVisualChild(int index)
        {
            return _collection[index];
        }

        protected override int VisualChildrenCount
        {
            get
            {
                return _collection.Count;
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _comboBox.Arrange(new Rect(0, 0, _textBlock.DesiredSize.Width + 50, _textBlock.DesiredSize.Height * 1.5));
            _comboBox.Focus();
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
            add
            {
                _comboBox.LostFocus += value;
            }
            remove
            {
                _comboBox.LostFocus -= value;
            }
        }

        public event KeyEventHandler TextBoxKeyUp
        {
            add
            {
                _comboBox.KeyUp += value;
            }
            remove
            {
                _comboBox.KeyUp -= value;
            }
        }
    }
}
