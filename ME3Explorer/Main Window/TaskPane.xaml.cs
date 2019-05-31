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
using ME3Explorer.Packages;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for TaskPane.xaml
    /// </summary>
    public partial class TaskPane : UserControl
    {
        public event EventHandler<GenericWindow> ToolMouseOver;

        Button highlightedButton;

        public TaskPane()
        {
            InitializeComponent();
            fileList.ItemsSource = MEPackageHandler.packagesInTools;
            MEPackageHandler.packagesInTools.CollectionChanged += PackagesInTools_CollectionChanged;
        }

        private async void PackagesInTools_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //wait for the UI to reflect the collection change
            await Task.Delay(100);
            if (fileList.ActualHeight > topScrollViewer.ActualHeight)
            {
                scrollIndicator.Visibility = Visibility.Visible;
            }
            else
            {
                scrollIndicator.Visibility = Visibility.Hidden;
            }

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove ||
                e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true); 
            }
        }

        protected virtual void Button_Click(object sender, RoutedEventArgs e)
        {
            ((sender as Button)?.DataContext as GenericWindow)?.BringToFront();
        }


        protected void Button_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) &&
                ((sender as Button)?.DataContext as GenericWindow)?.tool is Tool t)
            {
                t.IsFavorited = !t.IsFavorited;
            }
        }

        protected virtual void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Button b)
            {
                deselectButton(highlightedButton);
                highlightedButton = b;
                if (b.FindName("highlightUnderline") is Rectangle r)
                {
                    r.Visibility = Visibility.Visible;
                }
                if (b.FindName("toolIcon") is Image img)
                {
                    img.Opacity = 1;
                }
                ToolMouseOver?.Invoke(sender, b.DataContext as GenericWindow);
            }
        }

        private static void deselectButton(Button b)
        {
            if (b != null)
            {
                if (b.FindName("highlightUnderline") is Rectangle r)
                {
                    r.Visibility = Visibility.Hidden;
                }

                if (b.FindName("toolIcon") is Image img)
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
}
