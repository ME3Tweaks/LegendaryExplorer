using System.Windows;
using System.Windows.Controls.Primitives;

namespace LegendaryExplorer.Tools.InterpEditor
{
    public class Key : Thumb
    {
        public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(
            nameof(Time), typeof(float), typeof(Key), new PropertyMetadata(default(float)));

        public float Time
        {
            get => (float)GetValue(TimeProperty);
            set => SetValue(TimeProperty, value);
        }

        public Key(float time = 0, string tooltip = null)
        {
            Time = time;
            ToolTip = tooltip;
        }
    }
}
