using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;

namespace ME3Explorer
{
    public abstract class ToolListControl : UserControl
    {
        protected List<Tool> tools;

        public virtual void setToolList(IEnumerable<Tool> enumerable)
        {
            tools = enumerable.ToList();
            tools.Sort((x, y) => x.name.CompareTo(y.name));
        }

        protected virtual void Button_Click(object sender, RoutedEventArgs e)
        {
            ((sender as Button)?.DataContext as Tool)?.open();
        }

        
        protected void Button_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                Tool t = ((sender as Button)?.DataContext as Tool);
                t.IsFavorited = !t.IsFavorited;
            }
        }

        protected virtual void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            Button b = sender as Button;
            if (b != null)
            {
                Rectangle r = b.FindName("highlightUnderline") as Rectangle;
                if (r != null)
                {
                    r.Visibility = Visibility.Visible;
                }
                Image img = b.FindName("toolIcon") as Image;
                if (img != null)
                {
                    img.Opacity = 1;
                }
            }
        }

        protected virtual void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            Button b = sender as Button;
            if (b != null)
            {
                Rectangle r = b.FindName("highlightUnderline") as Rectangle;
                if (r != null)
                {
                    r.Visibility = Visibility.Hidden;
                }
                Image img = b.FindName("toolIcon") as Image;
                if (img != null)
                {
                    img.Opacity = 0.85;
                }
            }
        }
    }
}
