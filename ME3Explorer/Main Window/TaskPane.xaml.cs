using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using KFreonLib.MEDirectories;
using ME3Explorer.Packages;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for TaskPane.xaml
    /// </summary>
    public partial class TaskPane : UserControl
    {
        public event EventHandler<GenericWindow> ToolMouseOver;

        public TaskPane()
        {
            InitializeComponent();
            fileList.ItemsSource = MEPackageHandler.packagesInTools;
        }

        protected virtual void Button_Click(object sender, RoutedEventArgs e)
        {
            ((sender as Button)?.DataContext as GenericWindow)?.BringToFront();
        }


        protected void Button_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                Tool t = ((sender as Button)?.DataContext as GenericWindow)?.tool;
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
            ToolMouseOver?.Invoke(sender, (sender as Button)?.DataContext as GenericWindow);
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

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            ((sender as Button)?.DataContext as IMEPackage)?.save();
        }
    }

    [ValueConversion(typeof(MEGame), typeof(ImageSource))]
    public class GameToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            MEGame game;
            if (value is MEGame)
            {
                game = (MEGame)value;
                switch (game)
                {
                    case MEGame.ME1:
                        return Application.Current.FindResource("iconME1") as ImageSource;
                    case MEGame.ME2:
                        return Application.Current.FindResource("iconME2") as ImageSource;
                    case MEGame.ME3:
                        return Application.Current.FindResource("iconME3") as ImageSource;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(string), typeof(string))]
    public class PathToFilenameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string)
            {
                return System.IO.Path.GetFileName((string)value);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(string), typeof(string))]
    public class PathToLocationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string)
            {
                string path = (string)value;
                if (path.StartsWith(ME3Directory.cookedPath) || path.StartsWith(ME2Directory.cookedPath) || path.StartsWith(ME1Directory.cookedPath))
                {
                    return "Basegame";
                }
                else if (path.StartsWith(ME3Directory.DLCPath) || path.StartsWith(ME2Directory.DLCPath))
                {
                    return System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(path)));
                }
                else if (path.StartsWith(ME1Directory.DLCPath))
                {
                    int startIndex = path.IndexOf("DLC_");
                    return path.Substring(startIndex, path.Skip(startIndex).ToList().IndexOf('\\'));
                }
                else
                {
                    return "External";
                }
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(long), typeof(string))]
    public class FileSizeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is long)
            {
                return UsefulThings.General.GetFileSizeAsString((long)value);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(int), typeof(string))]
    public class CountToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int)
            {
                int val = (int)value;
                if (parameter is string)
                {
                    return val + " " + (string)parameter + (val != 1 ? "s" : "");
                }
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
