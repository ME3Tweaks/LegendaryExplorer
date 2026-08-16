using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.UserControls.ExportLoaderControls;

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

        /// <summary>
        /// The 0-based index of this key within its parent track's key array.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Reference to the parent InterpTrack that owns this key.
        /// </summary>
        public InterpTrack ParentTrack { get; set; }

        public Key(float time = 0, string tooltip = null, int index = -1, InterpTrack parentTrack = null)
        {
            Time = time;
            ToolTip = tooltip;
            Index = index;
            ParentTrack = parentTrack;

            BuildContextMenu();
            ToolTip = $"Time: {Time:F3}";
        }

        private void BuildContextMenu()
        {
            var menu = new ContextMenu();

            var editTimeItem = new MenuItem { Header = "Edit Time..." };
            editTimeItem.Click += EditTime_Click;
            menu.Items.Add(editTimeItem);

            var deleteItem = new MenuItem { Header = "Delete Key" };
            deleteItem.Click += DeleteKey_Click;
            menu.Items.Add(deleteItem);

            ContextMenu = menu;
        }

        private void EditTime_Click(object sender, RoutedEventArgs e)
        {
            if (ParentTrack == null || Index < 0) return;

            string result = PromptDialog.Prompt(null, "Enter new time value:", "Edit Key Time", Time.ToString("F4"));
            if (!string.IsNullOrEmpty(result) && float.TryParse(result, out float newTime))
            {
                Time = newTime;
                ParentTrack.UpdateKeyTime(Index, newTime);
            }
        }

        private void DeleteKey_Click(object sender, RoutedEventArgs e)
        {
            if (ParentTrack == null || Index < 0) return;

            if (MessageBox.Show($"Delete key at index {Index} (time: {Time:F3})?", "Confirm Delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ParentTrack.DeleteKey(Index);
            }
        }
    }
}
