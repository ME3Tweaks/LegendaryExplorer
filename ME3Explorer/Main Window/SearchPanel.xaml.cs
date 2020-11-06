using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for InfoPanel.xaml
    /// </summary>
    public partial class SearchPanel : ToolListControl
    {
        public ImageSource Icon
        {
            get => (ImageSource)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        // Using a DependencyProperty as the backing store for Icon.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(ImageSource), typeof(SearchPanel), new PropertyMetadata(null));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        // Using a DependencyProperty as the backing store for Title.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(SearchPanel), new PropertyMetadata(""));
        

        public SearchPanel()
        {
            InitializeComponent();
        }

        public override void setToolList(IEnumerable<Tool> enumerable)
        {
            base.setToolList(enumerable);
            ToolList.ItemsSource = tools;
            scrollIndicator.Visibility = tools.Count >= 10 ? Visibility.Visible : Visibility.Hidden;
        }
        
    }
}
