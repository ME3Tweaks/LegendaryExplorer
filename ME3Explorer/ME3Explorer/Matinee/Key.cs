using System.Windows;
using System.Windows.Controls.Primitives;

namespace ME3Explorer.Matinee
{
    public class Key : Thumb
    {
        public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(
            nameof(Time), typeof(float), typeof(Key), new PropertyMetadata(default(float)));

        public static readonly DependencyProperty ToolTipProperty = DependencyProperty.Register(
            nameof(ToolTip), typeof(string), typeof(Key), new PropertyMetadata(default(string)));

        public float Time
        {
            get => (float)GetValue(TimeProperty);
            set => SetValue(TimeProperty, value);
        }

        public string ToolTip
        {
            get => (string)GetValue(ToolTipProperty);
            set => SetValue(ToolTipProperty, value);
        }

        public Key(float time = 0, string tooltip = null)
        {
            Time = time;
            ToolTip = tooltip;
        }
    }
}
