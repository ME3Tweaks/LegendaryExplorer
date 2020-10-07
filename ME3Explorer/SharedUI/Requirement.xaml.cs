using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FontAwesome5;
using ME3Explorer.SharedUI;
using Xceed.Wpf.Toolkit.Core.Converters;

namespace ME3Explorer.AnimationExplorer
{
    /// <summary>
    /// Interaction logic for Requirement.xaml
    /// </summary>
    public partial class Requirement : NotifyPropertyChangedControlBase
    {
        public bool IsFullfilled
        {
            get => (bool)GetValue(IsFullfilledProperty);
            set => SetValue(IsFullfilledProperty, value);
        }
        public static readonly DependencyProperty IsFullfilledProperty = DependencyProperty.Register(
            nameof(IsFullfilled), typeof(bool), typeof(Requirement), new PropertyMetadata(default(bool)));
        public RequirementCommand RequirementCommand
        {
            get => (RequirementCommand)GetValue(RequirementCommandProperty);
            set => SetValue(RequirementCommandProperty, value);
        }
        public static readonly DependencyProperty RequirementCommandProperty = DependencyProperty.Register(
            nameof(RequirementCommand), typeof(RequirementCommand), typeof(Requirement), new PropertyMetadata(default(RequirementCommand)));
        public Requirement()
        {
            DataContext = this;
            InitializeComponent();
            this.bind(IsFullfilledProperty, fulfillButton, nameof(fulfillButton.IsEnabled), new InverseBoolConverter());
        }

        private string _fullfilledText;
        public string FullfilledText
        {
            get => _fullfilledText; set => SetProperty(ref _fullfilledText, value);
        }

        private string _unFullfilledText;
        public string UnFullfilledText
        {
            get => _unFullfilledText; set => SetProperty(ref _unFullfilledText, value);
        }

        private string _buttonText;
        public string ButtonText
        {
            get => _buttonText; set => SetProperty(ref _buttonText, value);
        }
    }


    [ValueConversion(typeof(bool), typeof(EFontAwesomeIcon))]
    public class EnabledIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value switch
            {
                true => EFontAwesomeIcon.Solid_TimesCircle,
                _ => EFontAwesomeIcon.Solid_CheckCircle
            };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }


    [ValueConversion(typeof(bool), typeof(Brush))]
    public class EnabledBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value switch
            {
                true => Brushes.Red,
                _ => Brushes.Green
            };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
